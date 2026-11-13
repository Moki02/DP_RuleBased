// ============================================================================
// RoomDoor.cs
// One door in a room entrance. Walk up to it and the HUD shows "Press [E] to unlock the
// door"; press E and the Valve Timing minigame opens (same game and same difficulty by ring
// as the valve grates). Win and the door opens for good. Lose or Esc: nothing happens, the
// door stays shut and you can try again straight away (no HYD cost, no jam, no water reward).
//
// NIGHT LOCKDOWN: every door you have unlocked swings shut as a "lockdown" cue. Press E
// (no minigame) to open it again; it then stays open until dawn. Doors you never unlocked
// still need the minigame, day or night. At dawn every unlocked door opens again.
//
// The door "opens" by sinking into the floor, then its meshes are hidden and its colliders
// switched off. If the player is standing in the doorway when it shuts at night, its
// colliders wait until they step out, so nobody gets stuck inside a door.
//
// DESIGN RULES: none yet (pending GDD entry: room doors, 2026-11-13). Uses R9.1 (the world
// keeps running during the minigame) and R9.6 (difficulty by ring) like the valves.
//
// HOW TO USE: RoomDoors makes one per room entrance at Play. Don't add it by hand.
// HOW TO EDIT: the words are the fields below. The look is the Maze Tile Set's "Room Door"
// prefab (empty = an orange grey-box slab). Open speed is "Open Seconds".
// ============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace TheLastDrop
{
    /// <summary>A room door: unlock it once with the valve minigame; it shuts (but opens with E) at night.</summary>
    public class RoomDoor : Interactable_Manager, INightLockable
    {
        [Header("Door")]
        [Tooltip("Door number (RoomDoorLayout order). Set by RoomDoors.")]
        public int index;
        [Tooltip("Minigame band, needle speed, hits and misses. Set by RoomDoors from the door's ring (like the valves).")]
        public ValveTimingSettings settings;
        [Tooltip("Difficulty t (0-1) shown on the F1 debug line.")]
        public float difficultyT = -1f;

        [Header("Words")]
        public string unlockPrompt = "Press [E] to unlock the door";
        public string openPrompt = "Press [E] to open the door";

        [Header("Look")]
        [Tooltip("The moving part (it sinks into the floor when open). Set by RoomDoors.")]
        public Transform leaf;
        [Tooltip("How tall the door is, in metres (how far it sinks).")]
        public float height = 3f;
        [Tooltip("How wide the doorway is, in metres (one tile). Used to tell if the player is standing in it.")]
        public float width = 4f;
        [Tooltip("Seconds to open or shut.")]
        [Min(0.01f)] public float openSeconds = 0.45f;

        [Header("Events (sounds and animations later)")]
        public UnityEvent onUnlocked;
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        /// <summary>Called once when the door is unlocked with the minigame.</summary>
        public event System.Action<RoomDoor> Unlocked;

        /// <summary>True once the minigame was won (saved).</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>True if the player can walk through right now.</summary>
        public bool IsPassable => testOpen || (IsUnlocked && (!nightLocked || openedTonight));

        bool nightLocked;      // lockdown is on
        bool openedTonight;    // pressed E on an unlocked door during this lockdown
        bool testOpen;         // MazeBuilder "Doors Enabled" unticked
        bool activeGame;
        float shown = -1f;     // 0 = shut, 1 = fully open (-1 = not placed yet)
        Collider[] colliders;
        Renderer[] renderers;
        Vector3 leafClosedPos;

        void Awake()
        {
            interactionType = InteractionType.Press;
            if (interactionPoint == null)
            {
                interactionPoint = new GameObject("DoorInteractionPoint").transform;
                interactionPoint.SetParent(transform, false);
                interactionPoint.localPosition = Vector3.up;
            }
        }

        /// <summary>Called by RoomDoors once the leaf (grey-box or prefab) is in place.</summary>
        public void Init()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            if (leaf == null) leaf = transform;
            leafClosedPos = leaf.localPosition;
            Snap();
        }

        void Update()
        {
            var p = Player_Manager.instance;
            if (colliders == null) return;

            // Slide towards open or shut.
            float target = IsPassable ? 1f : 0f;
            if (!Mathf.Approximately(shown, target))
            {
                if (target < 0.5f && shown >= 0.999f) SetRenderers(true);   // starting to shut: show it
                shown = Mathf.MoveTowards(Mathf.Max(0f, shown), target, Time.deltaTime / openSeconds);
                PlaceLeaf();
                if (shown >= 0.999f) SetRenderers(false);
            }

            // Solid only when shut and nobody is standing in the doorway.
            bool solid = !IsPassable && !PlayerInDoorway(p);
            SetColliders(solid);

            if (p == null) return;

            // Keep the interaction point on the door, at the spot nearest the player's chest.
            Vector3 chest = p.transform.position + Vector3.up * 0.9f;
            float best = float.MaxValue;
            bool found = false;
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                Vector3 q = c.ClosestPoint(chest);
                float d = (q - chest).sqrMagnitude;
                if (d < best) { best = d; interactionPoint.position = q; found = true; }
            }
            if (!found) interactionPoint.position = transform.position + Vector3.up * 0.9f;

            // Collapsing mid-game closes the minigame (counts as quitting).
            if (activeGame && p.playerHydration_Manager != null &&
                p.playerHydration_Manager.IsCollapsed() && ValveTimingGame.Instance != null)
                ValveTimingGame.Instance.Cancel();
        }

        // ------------------------------------------------------------------ Interactable_Manager

        public override bool CanInteract(Player_Manager targetPlayer)
        {
            if (targetPlayer == null || IsPassable || ValveTimingGame.IsPlaying) return false;
            if (Game_Manager.instance == null || !Game_Manager.instance.CanControlPlayer()) return false;
            return targetPlayer.playerHydration_Manager == null || !targetPlayer.playerHydration_Manager.IsCollapsed();
        }

        public override string GetInteractionText() => IsUnlocked ? openPrompt : unlockPrompt;

        public override void Interact(float deltaTime)
        {
            if (activeGame || IsPassable) return;
            if (IsUnlocked)
            {
                // Unlocked door shut by the lockdown: E opens it until dawn, no minigame.
                openedTonight = true;
                onOpened.Invoke();
                return;
            }
            StartMinigame();
        }

        // ------------------------------------------------------------------ Minigame

        void StartMinigame()
        {
            var game = ValveTimingGame.Instance;
            if (game == null) { Debug.LogError("[RoomDoor] No ValveTimingGame in the scene.", this); return; }
            if (settings == null) { Debug.LogError("[RoomDoor] No minigame settings (RoomDoors sets them).", this); return; }
            if (!game.Begin(settings, OnResult, difficultyT)) return;
            activeGame = true;
            SetPlayingState(true);
        }

        void OnResult(MinigameResult result)
        {
            activeGame = false;
            SetPlayingState(false);
            if (result != MinigameResult.Won) return;   // lose or Esc: no cost, try again
            IsUnlocked = true;
            openedTonight = true;                        // won at night: open until dawn too
            onUnlocked.Invoke();
            onOpened.Invoke();
            Unlocked?.Invoke(this);
        }

        // Same as ValveGate: Minigame state (player can't move) and the minigame drain rate (R9.1).
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

        // ------------------------------------------------------------------ State

        /// <summary>Called by NightLockdown: true at night start (unlocked doors shut), false at dawn.</summary>
        public void SetNightLock(bool locked)
        {
            bool was = IsPassable;
            nightLocked = locked;
            openedTonight = false;
            if (was && !IsPassable) onClosed.Invoke();
            else if (!was && IsPassable) onOpened.Invoke();
        }

        /// <summary>Loading a save: unlocked or not, without events. Jumps straight to the right look.</summary>
        public void SetUnlockedState(bool unlocked)
        {
            IsUnlocked = unlocked;
            openedTonight = false;
            Snap();
        }

        /// <summary>Testing (MazeBuilder "Doors Enabled" off): every door open.</summary>
        public void SetTestOpen(bool open) => testOpen = open;

        // Jump to the current state with no animation.
        void Snap()
        {
            shown = IsPassable ? 1f : 0f;
            PlaceLeaf();
            SetRenderers(shown < 0.999f);
            SetColliders(!IsPassable && !PlayerInDoorway(Player_Manager.instance));
        }

        void PlaceLeaf()
        {
            if (leaf == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shown));
            leaf.localPosition = leafClosedPos + Vector3.down * (height + 0.05f) * eased;
        }

        void SetRenderers(bool on)
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null && r.enabled != on) r.enabled = on;
        }

        void SetColliders(bool on)
        {
            if (colliders == null) return;
            foreach (var c in colliders) if (c != null && !c.isTrigger && c.enabled != on) c.enabled = on;
        }

        // True if the player overlaps the doorway (within about a body's width of the door).
        bool PlayerInDoorway(Player_Manager p)
        {
            if (p == null) return false;
            Vector3 local = transform.InverseTransformPoint(p.transform.position);
            return Mathf.Abs(local.z) < 0.75f && Mathf.Abs(local.x) < width * 0.5f + 0.5f &&
                   local.y > -1f && local.y < height + 1f;
        }

        [ContextMenu("Test: start minigame now")]
        void TestStart() { if (Application.isPlaying && !IsUnlocked && !activeGame) StartMinigame(); }

        [ContextMenu("Test: unlock door")]
        void TestUnlock()
        {
            if (!Application.isPlaying || IsUnlocked) return;
            IsUnlocked = true;
            openedTonight = true;
            onUnlocked.Invoke();
            Unlocked?.Invoke(this);
        }
    }
}
