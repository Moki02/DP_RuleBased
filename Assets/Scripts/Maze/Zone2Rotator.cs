// ============================================================================
// Zone2Rotator.cs
// Turns zone 2 of the maze 90 degrees clockwise during the night lockdown of every
// even day (days 2, 4, 6 ...). If the player is standing in zone 2, they turn with it.
//
// DESIGN RULES: R8.24 to R8.28. R8.26: turning maps floor onto floor, so a player who
// turns with the zone never ends up inside a wall.
//
// HOW TO USE: put this on the "Maze" object (the one with the MazeBuilder). The pivot is
// found by itself (MazeBuilder > _Generated > Zone2Pivot); you can also drag it into "Pivot".
// The clock tells it when night starts; you don't call it. Other scripts can listen to
// Zone2Turned (the map does, to flash what moved).
// HOW TO EDIT: how often it turns is "Zone 2 Days Between Turns" in GameBalance.
// Press 5 in Play mode to force a turn while testing (debug keys must be on).
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Turns zone 2 on the nights of even days, and turns the player with it if they're inside.</summary>
    [RequireComponent(typeof(MazeBuilder))]
    public class Zone2Rotator : MonoBehaviour, ISaveable
    {
        /// <summary>Restored after the clock, before the player.</summary>
        public int RestoreOrder => RestoreOrders.Zone2;

        /// <summary>Saves the turn count (only as a check: on load it comes from the day).</summary>
        public void Capture(SaveData data) => data.zone2Turns = Turns;

        /// <summary>Turns zone 2 to match the loaded day (R8.24). Warns if that differs from the saved count.</summary>
        public void Restore(SaveData data)
        {
            if (balance == null) balance = BalanceProvider.Current;
            float daylight = balance != null ? balance.dayLengthSeconds : float.MaxValue;
            int turns = SaveCodec.Zone2TurnsFor(data, daylight, DaysBetweenTurns);
            if (turns != data.zone2Turns)
                Debug.LogWarning($"[Zone2Rotator] The save says zone 2 had {data.zone2Turns} turns, but day {data.day} means {turns}. " +
                                 "Using the day (a debug-key turn isn't saved).", this);
            SetTurns(turns);
        }

        [Tooltip("The object that holds all of zone 2. Empty = found by itself under the MazeBuilder's _Generated.")]
        public Transform pivot;

        [Tooltip("Write a line in the Console every time zone 2 turns.")]
        public bool logTurns = true;

        /// <summary>How many quarter turns zone 2 has made, 0 to 3.</summary>
        public int Turns { get; private set; }

        /// <summary>Fired after every turn with (old turns, new turns), each 0 to 3.</summary>
        public event Action<int, int> Zone2Turned;

        MazeBuilder builder;
        GameClock_Manager clock;
        GameBalance balance;

        /// <summary>The maze grid this rotator keeps in step (the MazeBuilder's grid).</summary>
        public MazeGrid Grid => builder != null ? builder.Grid : null;

        void Awake()
        {
            builder = GetComponent<MazeBuilder>();
        }

        void Start()
        {
            balance = BalanceProvider.Current != null ? BalanceProvider.Current : builder.balance;
            if (pivot == null) pivot = builder.Zone2Pivot;
            if (pivot == null)
            {
                Debug.LogError("[Zone2Rotator] No Zone2Pivot. Tick 'Split For Zone 2 Rotation' on the MazeBuilder and rebuild.", this);
                enabled = false;
                return;
            }

            clock = GameClock_Manager.Instance;
            if (clock != null)
            {
                clock.NightStarted += OnNightStarted;
                // Match the clock's day (matters after loading a save).
                SetTurns(Zone2TurnMath.TurnsForDay(clock.Day, clock.IsNight, DaysBetweenTurns));
            }
            else
            {
                Debug.LogWarning("[Zone2Rotator] No GameClock_Manager in the scene; zone 2 only turns with debug key 5.", this);
                SetTurns(0);
            }
        }

        void OnDestroy()
        {
            if (clock != null) clock.NightStarted -= OnNightStarted;
        }

        int DaysBetweenTurns => balance != null ? balance.zone2DaysBetweenTurns : 2;

        void OnNightStarted(int day)
        {
            if (Zone2TurnMath.IsTurnNight(day, DaysBetweenTurns))
                TurnOnce($"night of day {day}");
        }

        /// <summary>Turns zone 2 one step clockwise now, taking the player along if they're inside. Debug key 5 calls this.</summary>
        public void TurnOnce(string reason = "forced")
        {
            if (!enabled || pivot == null) return;
            int oldTurns = Turns;
            int newTurns = (oldTurns + 1) % 4;

            // Check the player BEFORE turning: are they standing in zone 2?
            bool playerTurns = MovePlayerWithZone();
            SetTurns(newTurns);

            if (logTurns)
                Debug.Log($"[Zone2Rotator] Zone 2 turned {oldTurns * 90}° -> {newTurns * 90}° ({reason})" +
                          (playerTurns ? "; the player was inside and turned with it." : "."), this);
            Zone2Turned?.Invoke(oldTurns, newTurns);
        }

        /// <summary>
        /// Puts zone 2 straight at a turn count (0 to 3) without moving the player or firing
        /// Zone2Turned. Used at start and when loading a save.
        /// </summary>
        public void SetTurns(int turns)
        {
            Turns = ((turns % 4) + 4) % 4;
            // One clockwise turn (seen from above) is +90 degrees on Y in Unity.
            if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, 90f * Turns, 0f);
            if (Grid != null) Grid.Zone2Turns = Turns;
        }

        /// <summary>If the player stands in zone 2, moves them one quarter turn around the pivot. Returns true if they moved.</summary>
        bool MovePlayerWithZone()
        {
            var player = Player_Manager.instance;
            if (player == null || Grid == null) return false;

            Vector2Int tile = builder.WorldToGrid(player.transform.position);
            if (!Grid.InBounds(tile.x, tile.y) || !Grid.IsInZone2(tile)) return false;

            // The CharacterController fights being moved by hand, so switch it off while we move.
            var cc = player.characterController;
            bool ccWasOn = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            Vector3 newPos = Zone2TurnMath.RotateAroundPivot(player.transform.position, pivot.position, 1);
            Quaternion newRot = Quaternion.Euler(0f, 90f, 0f) * player.transform.rotation; // facing turns too
            player.transform.SetPositionAndRotation(newPos, newRot);

            if (cc != null) cc.enabled = ccWasOn;

            // The camera turns as well, so the player's view of zone 2 doesn't jump.
            if (Player_Camera.instance != null) Player_Camera.instance.TurnWithPlayer(90f);
            return true;
        }
    }
}
