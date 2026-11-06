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

using UnityEngine;
using UnityEngine.Events;
using DryLabyrinth.Data;

namespace DryLabyrinth.Minigames
{
    public class ValveGate : Interactable_Manager
    {
        [Header("Valve gate")]
        public GameBalance balance;

        [Tooltip("Picks the difficulty row in GameBalance.valveTiming (GDD section 9 table).")]
        public ValveLocation location = ValveLocation.Zone1;

        public string promptMessage = "Press [E] to turn the valve";

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
            if (!game.Begin(location, OnResult)) return;

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
                    Open();
                    break;
                case MinigameResult.Lost:   // R9.3 (HYD cost goes on On Failed later)
                    jammedUntil = Time.time + (balance != null ? balance.minigameLockout : 10f);
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
            ApplySolid(false);
            onOpened.Invoke();
        }

        /// <summary>For the night lockdown script later (R8.21 / R8.23): true closes an opened gate,
        /// false reopens it. A gate that was never opened stays shut either way.</summary>
        public void SetLockdown(bool closed)
        {
            lockdownClosed = closed;
            if (IsOpen) ApplySolid(closed);
        }

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
