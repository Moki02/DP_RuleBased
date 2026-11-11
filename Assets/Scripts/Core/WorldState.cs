// ============================================================================
// WorldState.cs
// Shared facts about the world right now, so systems can work together without knowing
// about each other:
//   - the drought stage (set by the drought stage system; worked out from the day until then),
//   - whether it's raining (Thunderstorm event) and whether a Heatwave is on,
//   - tonight's evaporation multiplier (×2 after a Heatwave),
//   - extra water table depth (Water Table Drop event, nice to have).
//
// Whoever owns a fact sets it; everyone else only reads it. Values reset when a scene loads.
// Owners: StageOverride = StageManager (debug key 8, saved); IsRaining, HeatwaveActive and
// EvaporationMultiplierTonight = EventManager; Evaporation reads the multiplier at dawn and
// puts it back to 1.
//
// DESIGN RULES: section 10 (stages and events) - this is only the shared notice board.
//
// HOW TO USE: read WorldState.StageIndex, WorldState.IsRaining, ... from any script.
// Owners set their facts in Start or later, never in Awake (the scene-load reset could
// run after an Awake and wipe it).
// HOW TO EDIT: nothing to tune; stage numbers are in GameBalance > Drought stages.
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>Shared facts about the world right now (stage, rain, heat).</summary>
    public static class WorldState
    {
        /// <summary>
        /// Stage index forced by the drought stage system (debug key 8 jumps ahead). -1 = work it
        /// out from the day with GameBalance.GetStageIndex.
        /// </summary>
        public static int StageOverride = -1;

        /// <summary>True while rain falls (Thunderstorm). Rain Catcher runs; Solar Still and SODIS stop.</summary>
        public static bool IsRaining;

        /// <summary>True while a Heatwave is on (until the end of the day).</summary>
        public static bool HeatwaveActive;

        /// <summary>Multiplies the evaporation at the next day change (2 after a Heatwave). Reset to 1 after use.</summary>
        public static float EvaporationMultiplierTonight = 1f;

        /// <summary>Extra metres on the water table until the day ends (Water Table Drop, R4.7).</summary>
        public static float WaterTableExtraMeters;

        /// <summary>The current day (1 if there's no clock).</summary>
        public static int Day => GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Day : 1;

        /// <summary>The current drought stage index (0 = El Niño Advisory ... 3 = State of Calamity).</summary>
        public static int StageIndex
        {
            get
            {
                var b = BalanceProvider.Current;
                int fromDay = b != null ? b.GetStageIndex(Day) : 0;
                return Mathf.Max(fromDay, StageOverride);
            }
        }

        /// <summary>The current stage's settings (null if there's no GameBalance).</summary>
        public static StageSettings Stage
        {
            get
            {
                var b = BalanceProvider.Current;
                if (b == null || b.stages == null || b.stages.Length == 0) return null;
                return b.stages[Mathf.Clamp(StageIndex, 0, b.stages.Length - 1)];
            }
        }

        /// <summary>True in the last stage (State of Calamity).</summary>
        public static bool IsFinalStage
        {
            get
            {
                var b = BalanceProvider.Current;
                return b != null && b.stages != null && b.stages.Length > 0 && StageIndex >= b.stages.Length - 1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            Reset();
            SceneManager.sceneLoaded += (s, m) => Reset();
        }

        /// <summary>Back to a calm Stage-from-day world (new scene or new run).</summary>
        public static void Reset()
        {
            StageOverride = -1;
            IsRaining = false;
            HeatwaveActive = false;
            EvaporationMultiplierTonight = 1f;
            WaterTableExtraMeters = 0f;
        }
    }
}
