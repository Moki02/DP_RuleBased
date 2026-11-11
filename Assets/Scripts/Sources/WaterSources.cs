// ============================================================================
// WaterSources.cs
// Runs the three automatic water sources the player can build (Build tab), every frame
// while the game isn't paused:
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
// When a source is owned, a simple model appears on a hub tile with a floating label that
// shows its live output and why it is slow or stopped (R4.6). While the player is in the
// hub a "Sources" line on the HUD sums them up.
//
// Debug key 4 turns rain on and off (until the Thunderstorm event from batch 3 is merged,
// that's the only way to test the Rain Catcher).
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
        MazeBuilder maze;
        UpgradeManager upgrades;

        Transform root;                 // parent of all source models
        string builtKey = null;         // what the models were last built for (rebuilt when it changes)
        bool dirty = true;
        FacingLabel rainLabel, stillLabel, wellLabel;
        float nextFullToast;

        // ------------------------------------------------------------------ Lifecycle

        void Start()
        {
            balance = BalanceProvider.Current;
            resources = GameResources_Manager.instance;
            maze = FindAnyObjectByType<MazeBuilder>();
            upgrades = UpgradeManager.Instance;
            if (upgrades != null) upgrades.Changed += OnUpgradesChanged;

            // Test key until the Thunderstorm event (batch 3) is merged.
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

            if (dirty) RebuildModelsIfNeeded();

            RainRate = StillRate = WellRate = 0f;
            bool running = Time.timeScale > 0f;
            float dt = running ? Time.deltaTime : 0f;

            string rainText = RunRainCatcher(dt);
            string stillText = RunSolarStill(dt);
            string wellText = RunDeepWell(dt);

            if (rainLabel != null) rainLabel.Text = rainText ?? "";
            if (stillLabel != null) stillLabel.Text = stillText ?? "";
            if (wellLabel != null) wellLabel.Text = wellText ?? "";

            UpdateHudLine();
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

        // ------------------------------------------------------------------ HUD

        void UpdateHudLine()
        {
            bool any = Skills.Owns(RainCatcherId) || Skills.Owns(SolarStillId) || Skills.Owns(DeepWellId);
            bool atHub = upgrades != null && upgrades.PlayerAtHub;
            if (!any || !atHub) { SimpleHud.SetLine("sources", null); return; }

            string text = "Sources:";
            if (Skills.Owns(RainCatcherId)) text += $" rain {Rate(RainRate)}";
            if (Skills.Owns(SolarStillId)) text += $" | still {Rate(StillRate)}";
            if (Skills.Owns(DeepWellId)) text += $" | well {Rate(WellRate)}";
            SimpleHud.SetLine("sources", text.Replace(": |", ":") + " L/s");
        }

        // ------------------------------------------------------------------ Hub models

        void RebuildModelsIfNeeded()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (maze == null || maze.Grid == null) return; // try again next frame
            dirty = false;

            bool rain = Skills.Owns(RainCatcherId), still = Skills.Owns(SolarStillId), well = Skills.Owns(DeepWellId);
            int stills = Mathf.Max(1, Mathf.RoundToInt(Skills.F("solarStillCount", 1f)));
            string key = $"{rain}|{CatchArea}|{still}|{stills}|{well}";
            if (key == builtKey) return;
            builtKey = key;

            if (root != null) Destroy(root.gameObject);
            root = new GameObject("WaterSources_Hub").transform;
            rainLabel = stillLabel = wellLabel = null;

            if (rain) rainLabel = BuildRainCatcher();
            if (still) stillLabel = BuildSolarStill(stills);
            if (well) wellLabel = BuildDeepWell();
        }

        // A parent object standing on a hub tile (offset from the centre in tiles).
        Transform Station(string name, Vector2Int offset)
        {
            var c = maze.Grid.Centre;
            int x = c.x + offset.x, y = c.y + offset.y;
            if (!maze.Grid.IsWalkable(x, y))
                Debug.LogWarning($"[WaterSources] {name}: tile ({x},{y}) is a wall. Change its tile in GameBalance > Water sources (hub).", this);
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = maze.GridToWorld(x, y);
            return t;
        }

        FacingLabel BuildRainCatcher()
        {
            var s = Station("RainCatcher", balance.rainCatcherTile);
            // Roof grows with the catch area (W3 / W8): a square of that many m².
            float side = Mathf.Sqrt(Mathf.Max(1f, CatchArea));
            SourceVisuals.Part(s, "Roof", PrimitiveType.Cube, new Vector3(0f, 2.2f, 0f), new Vector3(side, 0.1f, side), new Color(0.55f, 0.6f, 0.65f));
            SourceVisuals.Part(s, "Pipe", PrimitiveType.Cylinder, new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 0.8f, 0.12f), new Color(0.3f, 0.3f, 0.3f));
            SourceVisuals.Part(s, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.5f, 0.9f), new Color(0.2f, 0.45f, 0.75f));
            return SourceVisuals.Label(s, balance.sourceLabelHeight, balance.sourceLabelFontSize);
        }

        FacingLabel BuildSolarStill(int count)
        {
            var s = Station("SolarStill", balance.solarStillTile);
            for (int i = 0; i < count; i++)
            {
                float x = count > 1 ? (i == 0 ? -0.8f : 0.8f) : 0f;
                SourceVisuals.Part(s, $"Basin{i + 1}", PrimitiveType.Cube, new Vector3(x, 0.15f, 0f), new Vector3(1.2f, 0.3f, 0.9f), new Color(0.15f, 0.15f, 0.15f));
                var glass = SourceVisuals.Part(s, $"Glass{i + 1}", PrimitiveType.Cube, new Vector3(x, 0.45f, 0f), new Vector3(1.2f, 0.05f, 0.95f), new Color(0.6f, 0.85f, 1f));
                glass.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            }
            return SourceVisuals.Label(s, balance.sourceLabelHeight, balance.sourceLabelFontSize);
        }

        FacingLabel BuildDeepWell()
        {
            var s = Station("DeepWell", balance.deepWellTile);
            SourceVisuals.Part(s, "Wall", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.4f, 1.4f), new Color(0.55f, 0.5f, 0.45f));
            SourceVisuals.Part(s, "Water", PrimitiveType.Cylinder, new Vector3(0f, 0.81f, 0f), new Vector3(1.1f, 0.01f, 1.1f), new Color(0.1f, 0.25f, 0.45f));
            SourceVisuals.Part(s, "Post", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 0.1f, 0.1f), new Color(0.4f, 0.28f, 0.15f));
            return SourceVisuals.Label(s, balance.sourceLabelHeight, balance.sourceLabelFontSize);
        }
    }
}
