// ============================================================================
// Evaporation.cs
// Once a day, when the day changes, stored water evaporates (R3.4, R5.2). Each container
// loses (stage evaporation rate × cover factor × tonight's multiplier) of what it holds:
//   - Raw Basin: open, full rate (cover factor 1);
//   - Clean Storage: Tapayan open 1, Plastic Drum and Ferrocement Tank 0.5 (covered),
//     Underground Cistern 0.1 (effect field "cleanCoverFactor" from S2 / S3 / S6);
//   - spare jugs by the hub door: open, full rate (R3.5);
//   - the canteen and the lock basins never evaporate (R3.4).
// Tonight's multiplier is WorldState.EvaporationMultiplierTonight (2 after a Heatwave,
// section 10); it goes back to 1 once used. A toast shows "Evaporation overnight: -x.x L".
//
// WHICH STAGE: the stage of the day that just ended (the water sat through that day's
// heat). So on the dawn of day 3 (Dry Spell starts) the loss still uses Stage 1's 2%.
// A stage forced ahead with debug key 8 counts as well.
//
// DESIGN RULES: R3.4, R3.5 (spare jugs), R5.2, section 5 storage tables, section 10 stages.
//
// HOW TO USE: added to the game scene by itself (AutoSystems). Nothing to set.
// HOW TO EDIT: stage rates are GameBalance > Drought stages > Evaporation; the Raw Basin and
// spare jug factors and the toast threshold are in GameBalance > "Drought: evaporation";
// Clean Storage's cover factor comes from the Build and Skills effects CSV (cleanCoverFactor).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Takes the nightly evaporation from the Raw Basin, Clean Storage and spare jugs.</summary>
    public class Evaporation : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<Evaporation>();

        /// <summary>Litres lost at the last day change (for the debug HUD or tests).</summary>
        public float LastLoss { get; private set; }

        GameClock_Manager clock;

        void Awake()
        {
            // Listen as early as possible so the water is taken BEFORE the dawn autosave
            // (SaveManager listens in its Start). AutoSystems adds this after the scene's own
            // Awakes, so the clock already exists here.
            Subscribe();
        }

        void Start()
        {
            Subscribe(); // in case the clock wasn't there in Awake
            if (clock == null) Debug.LogWarning("[Evaporation] No GameClock_Manager: nothing evaporates.", this);
        }

        void Subscribe()
        {
            if (clock != null || GameClock_Manager.Instance == null) return;
            clock = GameClock_Manager.Instance;
            clock.NewDay += OnNewDay;
        }

        void OnDestroy()
        {
            if (clock != null) clock.NewDay -= OnNewDay;
        }

        void OnNewDay(int day)
        {
            var balance = BalanceProvider.Current;
            var res = GameResources_Manager.instance;
            if (balance == null || balance.stages == null || balance.stages.Length == 0 || res == null) return;

            // The stage of the day that just ended (see the header).
            int endedDay = Mathf.Max(1, day - 1);
            int stageIndex = Mathf.Clamp(Mathf.Max(balance.GetStageIndex(endedDay), WorldState.StageOverride),
                                         0, balance.stages.Length - 1);
            var stage = balance.stages[stageIndex];

            // Section 10: ×2 the night after a Heatwave. Used once, then back to 1.
            float tonight = Mathf.Max(0f, WorldState.EvaporationMultiplierTonight);
            WorldState.EvaporationMultiplierTonight = 1f;

            float rate = stage.evaporation * tonight;

            // R5.2: Clean Storage's cover factor (Tapayan 1, drum/tank 0.5, cistern 0.1).
            float tapayan = balance.cleanStorageTiers != null && balance.cleanStorageTiers.Length > 0
                ? balance.cleanStorageTiers[0].evaporationFactor : 1f;
            float cleanCover = Skills.F("cleanCoverFactor", tapayan);

            float raw = res.TakeRawWater(res.rawBasin * Mathf.Clamp01(rate * balance.rawBasinEvaporationFactor));
            float clean = res.TakeCleanWater(res.cleanStorage * Mathf.Clamp01(rate * cleanCover));
            float jugs = res.spareJugs * Mathf.Clamp01(rate * balance.spareJugEvaporationFactor);
            res.spareJugs -= jugs;
            // The canteen and the lock basins are never touched (R3.4).

            LastLoss = raw + clean + jugs;
            Debug.Log($"[Evaporation] Dawn of day {day} (stage {stageIndex + 1} {stage.displayName}, {stage.evaporation:P0} × {tonight:0.##}): " +
                      $"Raw Basin -{raw:0.00} L, Clean Storage -{clean:0.00} L (cover ×{cleanCover:0.##}), spare jugs -{jugs:0.00} L.", this);

            if (LastLoss >= balance.evaporationToastMinLiters)
                SimpleHud.Toast($"Evaporation overnight: -{LastLoss:0.0} L" + (tonight > 1f ? "  (heatwave ×" + tonight.ToString("0.#") + ")" : ""), 4f);
        }
    }
}
