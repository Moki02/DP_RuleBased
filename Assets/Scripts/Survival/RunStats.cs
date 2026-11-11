// ============================================================================
// RunStats.cs
// Counts what the ending and game over screens show (R11.7): collapses and the total
// litres collected. Other numbers (day, map %, skills owned) are read from their own
// systems when the screen opens.
//
// DESIGN RULES: R11.7 (ending screen stats).
//
// HOW TO USE: added to the game scene by itself. Anything that brings water into the
// game calls RunStats.AddCollected(litres) once, when the water first appears (pump,
// sources, rewards, new tiles, the Safe Return bonus). Moving water between containers
// (banking, purifying, the tap) is NOT counted again.
// HOW TO EDIT: nothing to tune. The end screen text is in Summary().
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The run's statistics, saved with the game.</summary>
    public class RunStats : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<RunStats>();

        /// <summary>The stats in this scene (null in old scenes).</summary>
        public static RunStats Instance { get; private set; }

        [Tooltip("Collapses so far.")] public int collapses;
        [Tooltip("Litres collected from sources, rewards and banking.")] public float litersCollected;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Adds litres to "total liters collected".</summary>
        public static void AddCollected(float litres)
        {
            if (Instance != null && litres > 0f) Instance.litersCollected += litres;
        }

        /// <summary>One more collapse.</summary>
        public static void AddCollapse()
        {
            if (Instance != null) Instance.collapses++;
        }

        /// <summary>The stats as text lines for an end screen.</summary>
        public static string Summary()
        {
            int day = GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Day : 1;
            var tracker = FindAnyObjectByType<MapRevealTracker>();
            float map = tracker != null && tracker.Reveal != null ? tracker.Reveal.MapPercent : 0f;
            int skills = UpgradeManager.Instance != null && UpgradeManager.Instance.Logic != null
                ? UpgradeManager.Instance.Logic.Owned.Count : 0;
            int collapses = Instance != null ? Instance.collapses : 0;
            float litres = Instance != null ? Instance.litersCollected : 0f;
            return $"Day {day}\nMap {map:0}% explored\n{litres:0.0} L collected\n" +
                   $"{skills} Build items and skills bought\n{collapses} collapse{(collapses == 1 ? "" : "s")}";
        }

        /// <summary>Restored with the water.</summary>
        public int RestoreOrder => RestoreOrders.Resources;

        /// <summary>Saves the counters.</summary>
        public void Capture(SaveData data)
        {
            data.collapses = collapses;
            data.litersCollected = litersCollected;
        }

        /// <summary>Puts the counters back.</summary>
        public void Restore(SaveData data)
        {
            collapses = data.collapses;
            litersCollected = data.litersCollected;
        }
    }
}
