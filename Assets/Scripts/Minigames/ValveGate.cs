// ValveGate.cs
// A minigame gate (GDD Rules R8.8, R9.1-R9.5) that plugs into the developer's interaction system
// (Interactable_Manager / PlayerInteraction_Manager): walk up to the gate, the HUD shows
// "Press [E] to turn the valve", press E, and the Valve Timing game opens.
//
// Win: the gate opens for good. Lose: the gate jams for GameBalance.minigameLockout seconds.
// Esc: closes the game, no penalty, gate stays shut.
// While playing, the game state is Minigame (player can't move, cursor free) and hydration
// drains at the minigame rate (isPlayingMinigame), so the world keeps going (R9.1).
//
// Water cost/reward is NOT applied yet. Hook it up later through On Opened / On Failed:
//   On Failed  -> take GameBalance.minigameFailHyd HYD (R9.3)
//   On Opened  -> pay GameBalance.gateReward[zone] once (R8.8 / R9.4)
//
// For the exit valve (R11.6), set Location to Exit Valve and keep this component disabled
// until the Exit Lock is full; the lock script then enables it.
//
// Maze playtest: GateManager adds one of these to every V tile's grate, with "Use Custom
// Settings" on and the difficulty worked out from the valve's ring (ValveDifficulty, R9.6).
// It also logs the HYD cost of a loss (R9.3) and the water reward of a win (R8.8); in the
// playtest those are only log lines.

using UnityEngine;
using UnityEngine.Events;

namespace TheLastDrop
{
    public class ValveGate : Interactable_Manager, INightLockable
    {
        [Header("Valve gate")]
        public GameBalance balance;

        [Tooltip("Picks the difficulty row in GameBalance.valveTiming (GDD section 9 table).")]
        public ValveLocation location = ValveLocation.Zone1;

        public string promptMessage = "Press [E] to turn the valve";

        [Tooltip("On: use Custom Settings below instead of the Location row (the maze playtest sets this).")]
        public bool useCustomSettings = false;

        [Tooltip("Band width (degrees), needle speed (degrees/second), hits to win and misses to lose for this valve.")]
        public ValveTimingSettings customSettings;

        [Tooltip("Difficulty t (0-1) shown on the F1 debug line. -1 = not known.")]
        public float difficultyT = -1f;

        [Tooltip("Which GameBalance.gateReward entry this valve pays when first opened: 0 zone 1, 1 zone 2, 2 zone 3.")]
        public int rewardZoneIndex = 0;

        [Tooltip("Write the HYD cost of a loss and the reward of a win to the Console (the playtest doesn't apply them).")]
        public bool logWaterChanges = true;

        /// <summary>Called when the valve opens for good, with this valve.</summary>
        public event System.Action<ValveGate> Opened;

        [Tooltip("Hide the gate's renderers when it opens. Untick if On Opened plays an animation instead.")]
        public bool hideWhenOpen = true;

        [Header("Events (hook water, sounds, animations here later)")]
        public UnityEvent onOpened;
        public UnityEvent onFailed;
        public UnityEvent onQuit;

        public bool IsOpen { get; private set; }
        public bool IsJammed => Time.time < jammedUntil;

        float jammedUntil;
        bool lockdownClosed;
        bool activeGame;
        Collider[] colliders;
        Renderer[] renderers;

        void Awake()
        {
            interactionType = InteractionType.Press;
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);

            // The player's interaction hitbox is small and sits in front of the player at chest
            // height, so the gate's centre (often high up and inside the wall) is never inside it.
            // This point is moved to the spot on the gate right in front of the player instead.
            if (interactionPoint == null)
            {
                interactionPoint = new GameObject("ValveInteractionPoint").transform;
                interactionPoint.SetParent(transform, false);
            }
        }

        void Update()
        {
            var p = Player_Manager.instance;
            if (p == null) return;

            // Keep the interaction point on the gate surface nearest the player's chest.
            Vector3 chest = p.transform.position + Vector3.up * 0.9f;
            float best = float.MaxValue;
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                Vector3 q = c.ClosestPoint(chest);
                float d = (q - chest).sqrMagnitude;
                if (d < best) { best = d; interactionPoint.position = q; }
            }

            // Collapsing mid-game closes the minigame (counts as quitting, no extra penalty).
            if (activeGame && p.playerHydration_Manager != null &&
                p.playerHydration_Manager.IsCollapsed() && ValveTimingGame.Instance != null)
                ValveTimingGame.Instance.Cancel();
        }

        // ------------------------------------------------------------------ Interactable_Manager

        public override bool CanInteract(Player_Manager targetPlayer)
        {
            if (targetPlayer == null || IsOpen || lockdownClosed || ValveTimingGame.IsPlaying) return false;
            if (Game_Manager.instance == null || !Game_Manager.instance.CanControlPlayer()) return false;
            return targetPlayer.playerHydration_Manager == null || !targetPlayer.playerHydration_Manager.IsCollapsed();
        }

        public override string GetInteractionText()
        {
            return IsJammed
                ? $"Valve jammed ({Mathf.CeilToInt(jammedUntil - Time.time)}s)"
                : promptMessage;
        }

        public override void Interact(float deltaTime)
        {
            if (IsJammed || activeGame) return;
            StartMinigame();
        }

        // ------------------------------------------------------------------ Minigame

        void StartMinigame()
        {
            var game = ValveTimingGame.Instance;
            if (game == null)
            {
                Debug.LogError("[ValveGate] No ValveTimingGame in the scene.", this);
                return;
            }
            bool started = useCustomSettings && customSettings != null
                ? game.Begin(customSettings, OnResult, difficultyT)
                : game.Begin(location, OnResult);
            if (!started) return;

            activeGame = true;
            SetPlayingState(true);
        }

        void OnResult(MinigameResult result)
        {
            activeGame = false;
            SetPlayingState(false);

            switch (result)
            {
                case MinigameResult.Won:
                    LogWater($"won: +{Reward:0.#} L water reward (R8.8)");
                    Open();
                    break;
                case MinigameResult.Lost:   // R9.3 (HYD cost goes on On Failed later)
                    jammedUntil = Time.time + (balance != null ? balance.minigameLockout : 10f);
                    LogWater($"lost: -{(balance != null ? balance.minigameFailHyd : 0f):0.#} HYD (R9.3), minigame locked for {(balance != null ? balance.minigameLockout : 10f):0.#} s");
                    onFailed.Invoke();
                    break;
                case MinigameResult.Quit:   // R9.2
                    onQuit.Invoke();
                    break;
            }
        }

        void SetPlayingState(bool playing)
        {
            var p = Player_Manager.instance;
            if (p != null && p.playerHydration_Manager != null)
                p.playerHydration_Manager.isPlayingMinigame = playing;

            var gm = Game_Manager.instance;
            if (gm == null) return;
            if (playing)
                gm.ChangeGameState(Game_Manager.GameState.Minigame);
            else if (gm.currentGameState == Game_Manager.GameState.Minigame)   // don't undo a collapse
                gm.ChangeGameState(Game_Manager.GameState.Exploring);
        }

        // ------------------------------------------------------------------ Gate

        /// <summary>Opens the gate for good (R9.4). Also callable from other scripts.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            ApplySolid(lockdownClosed); // opened at night (e.g. loading a save): stays shut until dawn
            onOpened.Invoke();
            Opened?.Invoke(this);
        }

        /// <summary>For the night lockdown script later (R8.21 / R8.23): true closes an opened gate,
        /// false reopens it. A gate that was never opened stays shut either way.</summary>
        public void SetLockdown(bool closed)
        {
            lockdownClosed = closed;
            if (IsOpen) ApplySolid(closed);
        }

        // Water reward for first opening this valve (GameBalance.gateReward), 0 if not set.
        float Reward => balance != null && balance.gateReward != null && balance.gateReward.Length > 0
            ? balance.gateReward[Mathf.Clamp(rewardZoneIndex, 0, balance.gateReward.Length - 1)]
            : 0f;

        void LogWater(string what)
        {
            if (logWaterChanges)
                Debug.Log($"[ValveGate] {name} {what}. (Playtest: water does nothing, this is only a log.)", this);
        }

        /// <summary>
        /// Loading a save: sets the valve open or shut without events or log lines.
        /// An open valve stays shut until dawn if it's night.
        /// </summary>
        public void SetOpenState(bool open)
        {
            IsOpen = open;
            jammedUntil = 0f;
            ApplySolid(!open || lockdownClosed);
        }

        /// <summary>Called by NightLockdown at night start (true) and at dawn (false).</summary>
        public void SetNightLock(bool locked) => SetLockdown(locked);

        void ApplySolid(bool solid)
        {
            foreach (var c in colliders) if (c != null && !c.isTrigger) c.enabled = solid;
            if (hideWhenOpen) foreach (var r in renderers) if (r != null) r.enabled = solid;
        }

        [ContextMenu("Test: start minigame now")]
        void TestStart()
        {
            if (!Application.isPlaying) { Debug.Log("[ValveGate] Enter Play mode first."); return; }
            if (!IsOpen && !activeGame) StartMinigame();
        }

        [ContextMenu("Test: open gate")]
        void TestOpen() { if (Application.isPlaying) Open(); }
    }
}
