// ============================================================================
// WaterSources.cs
// Runs the three automatic water sources the player can build (Build tab), every frame
// while the game isn't paused (they also stop while the clock is paused on the Build and
// Skills screen at the hub, R1.5):
//
//   Rain Catcher (W2; W3 / W8 bigger roof)  only while it rains (WorldState.IsRaining):
//       catch area (m²) × rain per m² per second × runoff  ->  Raw Basin.
//       If the Raw Basin is full the rain is LOST and "FULL" shows (R1.4: rain is the one
//       thing that spills).
//   Solar Still (W5; W6 second still)       always, except while it rains:
//       still rate × stage sun factor × number of stills (× 2 in a Heatwave)
//       ->  Clean Storage directly (it skips the purifier, R3.1). Pauses when Clean Storage
//       is full; nothing is lost.
//   Deep Well (W7)                          always:
//       well rate × stage deep-well factor  ->  Raw Basin. Pauses when the basin is full.
//
// Every litre that goes in is counted in "total liters collected" (RunStats).
// Every source's model is already placed in the scene (HubSceneObjects); it is turned on
// when the source is owned, with a floating name label (no rates: kept off the hub on purpose).
//
// Rain comes from the Thunderstorm event (EventManager sets WorldState.IsRaining).
// Debug key 4 also turns rain on and off by hand, to test the Rain Catcher without waiting
// for a storm (debug key 9 starts a storm too). A real storm turns the rain off when it ends.
//
// DESIGN RULES: R1.4, R3.1, R3.3, R4.3, R4.4, R4.5, R4.6, section 4 table, section 10
// (stage sun / deep-well factors, Heatwave doubles the still).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems) and reads
// the owned Build items with Skills.Owns / Skills.F (effects CSV:
// unlockSource, catchArea, stillRate, solarStillCount, wellRate).
// HOW TO EDIT: rates are in GameBalance > Sources (Rain Liters Per M2s, Runoff, Catch Area,
// Still Rate, Well Rate) and each stage's Solar Sun Factor / Deep Well Factor; positions
// and label size are in GameBalance > Water sources (hub). Build item numbers are in
// Assets/_Project/Data/BuildAndSkills/BuildAndSkills_Effects.csv.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Rain Catcher, Solar Still and Deep Well: production, hub models and labels.</summary>
    public class WaterSources : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<WaterSources>();

        /// <summary>Build item ids (Build tab) for each source.</summary>
        public const string RainCatcherId = "W2", SolarStillId = "W5", SecondStillId = "W6", DeepWellId = "W7";

        /// <summary>Litres per second the Rain Catcher is adding right now (0 when dry or spilling).</summary>
        public float RainRate { get; private set; }
        /// <summary>Litres per second the Solar Still(s) are adding right now.</summary>
        public float StillRate { get; private set; }
        /// <summary>Litres per second the Deep Well is adding right now.</summary>
        public float WellRate { get; private set; }

        GameBalance balance;
        GameResources_Manager resources;
        UpgradeManager upgrades;

        bool dirty = true;              // owned sources may have changed: update the models
        float nextFullToast;

        // ------------------------------------------------------------------ Lifecycle

        void Start()
        {
            balance = BalanceProvider.Current;
            resources = GameResources_Manager.instance;
            upgrades = UpgradeManager.Instance;
            if (upgrades != null) upgrades.Changed += OnUpgradesChanged;

            // Test key: rain on/off by hand (the Thunderstorm event is the real source of rain).
            DebugKeys.Register(Key.Digit4, "toggle rain", ToggleRain);
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= OnUpgradesChanged;
            DebugKeys.Unregister(Key.Digit4);
            SimpleHud.SetLine("sources", null);
        }

        // Bought something or loaded a save: rebuild the models if needed.
        void OnUpgradesChanged() => dirty = true;

        static void ToggleRain()
        {
            WorldState.IsRaining = !WorldState.IsRaining;
            SimpleHud.Toast(WorldState.IsRaining ? "Rain ON (debug key 4)" : "Rain OFF (debug key 4)");
        }

        void Update()
        {
            if (balance == null) balance = BalanceProvider.Current;
            if (resources == null) resources = GameResources_Manager.instance;
            if (balance == null || resources == null) return;

            if (dirty) UpdateModels();

            RainRate = StillRate = WellRate = 0f;
            // Sources run on game time: nothing is made while the game is frozen (pause, title or
            // end screen) or while the clock is paused on the Build and Skills screen at the hub
            // (R1.5, R7.1). Otherwise the player could sit in that screen and collect free water
            // (and a storm's timer stops there too, so the Rain Catcher would never stop).
            var clock = GameClock_Manager.Instance;
            bool clockPaused = clock != null && clock.Clock != null && clock.Clock.Paused;
            bool running = Time.timeScale > 0f && !clockPaused;
            float dt = running ? Time.deltaTime : 0f;

            // Their status texts (rates) aren't shown any more: the hub labels show names only.
            RunRainCatcher(dt);
            RunSolarStill(dt);
            RunDeepWell(dt);
        }

        // ------------------------------------------------------------------ The three sources

        /// <summary>Area of the Rain Catcher roof in m² (W2 4, W3 8, W8 16).</summary>
        float CatchArea => Skills.F("catchArea", balance.catchArea);

        // R4.3, R1.4. Returns the label text.
        string RunRainCatcher(float dt)
        {
            if (!Skills.Owns(RainCatcherId)) return null;
            float area = CatchArea;
            string head = $"Rain Catcher {area:0.#} m²";
            if (!WorldState.IsRaining) return $"{head}: 0 L/s (no rain)";

            float rate = area * balance.rainLitersPerM2s * balance.runoff;
            bool full = resources.rawBasin >= resources.rawBasin_Max - 0.0001f;
            if (dt > 0f)
            {
                float want = rate * dt;
                float room = Mathf.Max(0f, resources.rawBasin_Max - resources.rawBasin);
                // Add only what fits (no double FULL event); the rest is lost (R1.4).
                float added = resources.AddRawWater(Mathf.Min(want, room));
                RunStats.AddCollected(added);
                if (added < want - 0.00001f)
                {
                    full = true;
                    if (Time.unscaledTime >= nextFullToast)
                    {
                        SimpleHud.Toast("FULL: Raw Basin. Rain is spilling over!");
                        nextFullToast = Time.unscaledTime + balance.rainFullToastSeconds;
                    }
                }
            }
            if (full) return $"{head}: FULL, rain lost ({Rate(rate)} L/s spilling)";
            RainRate = rate;
            return $"{head}: {Rate(rate)} L/s (raining)";
        }

        // R4.4, R3.1, R3.3, section 10 (sun factor, Heatwave × 2). Returns the label text.
        string RunSolarStill(float dt)
        {
            if (!Skills.Owns(SolarStillId)) return null;
            int count = Mathf.Max(1, Mathf.RoundToInt(Skills.F("solarStillCount", 1f)));
            string head = count > 1 ? $"Solar Still ×{count}" : "Solar Still";
            if (WorldState.IsRaining) return $"{head}: 0 L/s (paused: rain)";

            var stage = WorldState.Stage;
            float sun = stage != null ? stage.solarSunFactor : 1f;
            bool heat = WorldState.HeatwaveActive;
            float rate = Skills.F("stillRate", balance.stillRate) * sun * count
                         * (heat ? balance.heatwaveStillMult : 1f);

            float room = Mathf.Max(0f, resources.cleanStorage_Max - resources.cleanStorage);
            if (room <= 0.0001f) return $"{head}: 0 L/s (paused: Clean Storage FULL)";

            if (dt > 0f)
            {
                // Clean water straight into Clean Storage; never more than fits (no loss).
                float added = resources.AddCleanWater(Mathf.Min(rate * dt, room));
                RunStats.AddCollected(added);
            }
            StillRate = rate;
            string why = $"sun ×{sun:0.##}" + (heat ? ", heatwave ×" + balance.heatwaveStillMult.ToString("0.#") : "");
            return $"{head}: {Rate(rate)} L/s ({why})";
        }

        // R4.5, R3.3, section 10 (deep-well factor). Returns the label text.
        string RunDeepWell(float dt)
        {
            if (!Skills.Owns(DeepWellId)) return null;
            var stage = WorldState.Stage;
            float factor = stage != null ? stage.deepWellFactor : 1f;
            float rate = Skills.F("wellRate", balance.wellRate) * factor;

            float room = Mathf.Max(0f, resources.rawBasin_Max - resources.rawBasin);
            if (room <= 0.0001f) return "Deep Well: 0 L/s (paused: Raw Basin FULL)";

            if (dt > 0f)
            {
                float added = resources.AddRawWater(Mathf.Min(rate * dt, room));
                RunStats.AddCollected(added);
            }
            WellRate = rate;
            string stageName = stage != null ? stage.displayName : "stage";
            return $"Deep Well: {Rate(rate)} L/s ({stageName} ×{factor:0.##})";
        }

        /// <summary>0.72 for bigger rates, 0.028 for small ones, so slow sources don't read 0.0.</summary>
        static string Rate(float r) => r >= 0.1f ? r.ToString("0.00") : r.ToString("0.000");

        // ------------------------------------------------------------------ Hub models (placed in the scene)

        // Shows the owned sources' models (HubSceneObjects) with a name label. No rates on the
        // labels or the HUD: the player only needs to see which sources they own.
        void UpdateModels()
        {
            var hub = HubSceneObjects.Instance;
            if (hub == null) return; // still dirty: HubPlaceholders makes one at Play if the scene has none
            dirty = false;

            bool rain = Skills.Owns(RainCatcherId), still = Skills.Owns(SolarStillId), well = Skills.Owns(DeepWellId);
            int stills = Mathf.Max(1, Mathf.RoundToInt(Skills.F("solarStillCount", 1f)));

            // The model for this catch area (4 m² Normal, 8 m² Wider, 16 m² Bigger).
            string rainTier = !rain ? null : CatchArea >= 16f ? "Bigger" : CatchArea >= 8f ? "Wider" : "Normal";
            HubSceneObjects.ShowTier(hub.rainCatcherTiers, rainTier);
            SetLabel(hub.rainCatcherLabel, rain, "Rain Catcher");

            if (hub.solarStills != null)
                for (int i = 0; i < hub.solarStills.Length; i++)
                    if (hub.solarStills[i] != null) hub.solarStills[i].SetActive(still && i < stills);
            SetLabel(hub.solarStillLabel, still, stills > 1 ? $"Solar Still ×{stills}" : "Solar Still");

            if (hub.deepWell != null) hub.deepWell.SetActive(well);
            SetLabel(hub.deepWellLabel, well, "Deep Well");
        }

        static void SetLabel(FacingLabel label, bool on, string text)
        {
            HubSceneObjects.ShowLabel(label, on);
            if (label != null && on) label.Text = text;
        }
    }
}
