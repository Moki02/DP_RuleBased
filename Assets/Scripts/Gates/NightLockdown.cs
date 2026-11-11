// ============================================================================
// NightLockdown.cs
// The night lockdown, playtest version. When night starts, every gate closes (its
// collider comes back and it looks shut); at dawn they reopen. A player who is outside
// the hub and outside every room at night gets a "caught outside at night" line in
// the Console instead of losing health.
//
// DESIGN RULES: R8.19 (night lockdown), R8.21 and R8.23 (gates close at night and
// reopen at dawn).
//
// HOW TO USE: put this on the "Systems" object and drag the Maze object (with the
// MazeBuilder) into "Maze". Anything that should close at night implements
// INightLockable; this script finds them all when night starts.
// HOW TO EDIT: the night length is "Night Lockdown Seconds" in GameBalance.
// TESTING TIP: untick "Gates Enabled" on the MazeBuilder to walk through gates by day;
// they still appear at night, so you can watch the lockdown work.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Anything that closes during the night lockdown (gates, valve grates).</summary>
    public interface INightLockable
    {
        /// <summary>True when night starts (close), false at dawn (back to how it was by day).</summary>
        void SetNightLock(bool locked);
    }

    /// <summary>Closes every INightLockable at night and reopens them at dawn. Logs when the player is caught outside.</summary>
    public class NightLockdown : MonoBehaviour, ISaveable
    {
        /// <summary>Restored right after the clock.</summary>
        public int RestoreOrder => RestoreOrders.Lockdown;

        /// <summary>Nothing of its own to save: night or day comes from the clock.</summary>
        public void Capture(SaveData data) { }

        /// <summary>After loading, shuts or opens every gate to match the loaded time of day.</summary>
        public void Restore(SaveData data)
        {
            if (clock == null) clock = GameClock_Manager.Instance;
            if (clock != null) SetLocked(clock.IsNight);
        }

        [Tooltip("The Maze object with the MazeBuilder (used to know where the hub and the rooms are).")]
        public MazeBuilder maze;

        [Tooltip("How often to check whether the player is outside at night, in seconds.")]
        [Min(0.05f)] public float checkInterval = 0.5f;

        /// <summary>True during the night lockdown.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>True if the player was outside the hub and every room at the last check.</summary>
        public bool PlayerOutside { get; private set; }

        GameClock_Manager clock;
        float checkTimer;

        void Start()
        {
            clock = GameClock_Manager.Instance;
            if (clock == null)
            {
                Debug.LogWarning("[NightLockdown] No GameClock_Manager in the scene.", this);
                return;
            }
            clock.NightStarted += OnNightStarted;
            clock.NewDay += OnNewDay;
            if (clock.IsNight) SetLocked(true); // e.g. a scene that starts at night
        }

        void OnDestroy()
        {
            if (clock == null) return;
            clock.NightStarted -= OnNightStarted;
            clock.NewDay -= OnNewDay;
        }

        void OnNightStarted(int day)
        {
            SetLocked(true);
            Debug.Log($"[NightLockdown] Night of day {day}: every gate is closed until dawn.", this);
            PlayerOutside = false;      // so the first check below always reports
            CheckPlayer();
        }

        void OnNewDay(int day)
        {
            SetLocked(false);
            Debug.Log($"[NightLockdown] Dawn of day {day}: gates are back to normal.", this);
        }

        void SetLocked(bool locked)
        {
            IsLocked = locked;
            // Find every script that can be locked. This only runs twice a day, so a full search is fine.
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is INightLockable lockable) lockable.SetNightLock(locked);
        }

        void Update()
        {
            if (!IsLocked) return;
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = checkInterval;
            CheckPlayer();
        }

        // Logs once each time the player is (or steps) outside the hub and the rooms at night.
        void CheckPlayer()
        {
            var player = Player_Manager.instance;
            if (player == null || maze == null || maze.Grid == null) return;

            bool outside = !IsSheltered(player.transform.position);
            if (outside && !PlayerOutside)
                Debug.LogWarning("[NightLockdown] Caught outside at night! (Playtest: no health loss, this is only a log.)", player);
            PlayerOutside = outside;
        }

        /// <summary>True if the world position is inside the hub base or a room.</summary>
        public bool IsSheltered(Vector3 worldPosition)
        {
            Vector2Int worldTile = maze.WorldToGrid(worldPosition);
            // Areas store their tiles as in the CSV (0 degrees), so turn the world tile back
            // to its CSV tile first; this keeps zone 2 rooms correct after zone 2 turns.
            Vector2Int localTile = maze.Grid.WorldToLocal(worldTile);
            foreach (var area in maze.Areas)
                if (area != null && area.Contains(localTile)) return true;
            return false;
        }
    }
}
