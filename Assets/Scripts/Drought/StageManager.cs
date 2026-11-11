// ============================================================================
// StageManager.cs
// Runs the four drought stages (El Niño Advisory, Dry Spell, Drought, State of Calamity):
//   - multiplies the player's HYD drain by the stage's heat (R2.1, section 10 table);
//   - makes the hand pump weaker as the water table drops, unless the player owns the
//     W4 Deep-Set Cylinder (section 4 water table table, R4.6 label, R4.7 extra depth);
//   - when the stage changes, shows a big banner and a "RADIO: ..." message, and changes
//     the HUD stage line "Stage N: <name>  (heat ×1.25)" (R10.1);
//   - on day 5 the radio says El Niño has peaked in the Pacific (R10.2);
//   - makes the sun warmer and brighter each stage (R10.1; a cheap stand-in until the
//     artist's lighting presets exist);
//   - debug key 8 jumps to the next stage (Scope tab, debug keys table).
// Stage 4 never ends (R10.3): the stage only ever goes up.
//
// Which stage it is comes from WorldState.StageIndex: the day (GameBalance > Drought
// stages > Start Day), or WorldState.StageOverride if debug key 8 pushed it further.
//
// DESIGN RULES: R2.1, R4.6, R4.7, R10.1, R10.2, R10.3, section 4 and section 10 tables.
//
// HOW TO USE: added to the game scene by itself (AutoSystems). Nothing to set.
// HOW TO EDIT: stage numbers are in GameBalance > "Drought stages"; pump tables are
// "Pump By Depth" / "Deep Set Pump By Depth"; radio lines, the day 5 line and the
// lighting are in GameBalance > "Drought: radio" / "Drought: stage lighting".
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Drought stages: heat on HYD drain, the hand pump's water table, radio messages, debug key 8.</summary>
    public class StageManager : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<StageManager>();

        /// <summary>The HydrationModifiers key for the stage heat.</summary>
        public const string ModifierKey = "stage";

        /// <summary>The SimpleHud line key for the stage badge.</summary>
        public const string HudKey = "stage";

        [Tooltip("The sun to tint each stage. Empty = the scene's sun (Lighting settings) or the first directional light.")]
        public Light sun;

        /// <summary>The stage index last announced (-1 = none yet).</summary>
        public int CurrentStage { get; private set; } = -1;

        GameBalance balance;
        GameClock_Manager clock;
        Color sunBaseColor;          // the sun's own colour before any stage tint
        float sunAppliedMult = 1f;   // the stage intensity multiplier the sun has now

        void Start()
        {
            balance = BalanceProvider.Current;
            if (balance == null || balance.stages == null || balance.stages.Length == 0)
            {
                Debug.LogWarning("[StageManager] No GameBalance stages: the drought is off.", this);
                enabled = false;
                return;
            }

            // R2.1: the stage heat multiplies every HYD drain (never adds).
            HydrationModifiers.Add(ModifierKey, ctx => WorldState.Stage != null ? WorldState.Stage.heatMult : 1f);

            // Section 4 table: the hand pump follows the water table (+1 m on a Water Table Drop, R4.7).
            HubPump_Manager.OutputFactor = PumpFactor;
            HubPump_Manager.OutputReason = PumpReason;

            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NewDay += OnNewDay;
            else Debug.LogWarning("[StageManager] No GameClock_Manager: stages follow day 1 only (debug key 8 still works).", this);

            DebugKeys.Register(Key.Digit8, "next drought stage", NextStage);

            FindSun();

            // The starting stage: no banner (the game just began), but the HUD line, the
            // lighting and, on day 1, the first radio message.
            ApplyStage(WorldState.StageIndex, announce: false);
            if (WorldState.Day <= 1) Radio(RadioLine(CurrentStage));
        }

        void OnDestroy()
        {
            HydrationModifiers.Remove(ModifierKey);
            // Only clear the pump hooks if they're still ours.
            if (HubPump_Manager.OutputFactor == (Func<float>)PumpFactor) HubPump_Manager.OutputFactor = null;
            if (HubPump_Manager.OutputReason == (Func<string>)PumpReason) HubPump_Manager.OutputReason = null;
            if (clock != null) clock.NewDay -= OnNewDay;
            DebugKeys.Unregister(Key.Digit8);
        }

        void Update()
        {
            // Catches every way the stage can change: a new day, debug key 8, a loaded save.
            int now = WorldState.StageIndex;
            if (now != CurrentStage) ApplyStage(now, announce: now > CurrentStage);
        }

        // ------------------------------------------------------------------ Day change (R10.1, R10.2)

        void OnNewDay(int day)
        {
            // R10.1: a stage starts at the start of its day. Announce it first, then the day 5 line.
            int now = WorldState.StageIndex;
            if (now != CurrentStage) ApplyStage(now, announce: now > CurrentStage);

            // R10.2: El Niño peaks on day 5 but the drought keeps getting worse.
            if (day == balance.elNinoPeakDay) Radio(balance.elNinoPeakLine);
        }

        // ------------------------------------------------------------------ Debug key 8

        /// <summary>Debug key 8: jumps to the next drought stage (stays on the last one).</summary>
        public void NextStage()
        {
            int last = balance.stages.Length - 1;
            int now = WorldState.StageIndex;
            if (now >= last)
            {
                SimpleHud.Toast("Already the last stage (" + balance.stages[last].displayName + ")");
                return;
            }
            WorldState.StageOverride = now + 1;
            ApplyStage(WorldState.StageIndex, announce: true);
        }

        // ------------------------------------------------------------------ Stage change

        void ApplyStage(int index, bool announce)
        {
            index = Mathf.Clamp(index, 0, balance.stages.Length - 1);
            CurrentStage = index;
            var s = balance.stages[index];

            // R10.1: the HUD stage badge.
            SimpleHud.SetLine(HudKey, $"Stage {index + 1}: {s.displayName}  (heat ×{s.heatMult:0.##})");

            ApplySun(index);

            if (!announce) return;
            // R10.1: a radio message announces the new stage.
            SimpleHud.Banner(s.displayName.ToUpperInvariant(), Pick(balance.stageBannerLines, index, ""), balance.stageBannerSeconds);
            Radio(RadioLine(index));
            Debug.Log($"[StageManager] Stage {index + 1}: {s.displayName} (day {WorldState.Day}, heat ×{s.heatMult}, " +
                      $"evaporation {s.evaporation:P0}, water table {s.waterTableDepth} m).", this);
        }

        string RadioLine(int index) => Pick(balance.stageRadioLines, index, balance.stages[index].displayName);

        void Radio(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            SimpleHud.Toast("RADIO: " + line, balance.radioToastSeconds);
        }

        static string Pick(string[] list, int index, string fallback) =>
            list != null && index >= 0 && index < list.Length && !string.IsNullOrEmpty(list[index]) ? list[index] : fallback;

        // ------------------------------------------------------------------ Hand pump (section 4, R4.6, R4.7)

        /// <summary>The water table depth right now in metres (stage depth + Water Table Drop).</summary>
        public static float WaterTableDepth
        {
            get
            {
                var s = WorldState.Stage;
                return (s != null ? s.waterTableDepth : 0f) + WorldState.WaterTableExtraMeters;
            }
        }

        float PumpFactor()
        {
            if (balance == null) return 1f;
            // W4 Deep-Set Cylinder: the right-hand column of the water table.
            return balance.GetPumpFactor(WaterTableDepth, Skills.B("pumpUsesDeepTable", false));
        }

        string PumpReason()
        {
            // R4.6: "Hand Pump: 80% (water table 5.5 m)".
            string text = $"water table {WaterTableDepth:0.#} m";
            if (WorldState.WaterTableExtraMeters > 0f) text += " (dropped)";
            if (Skills.B("pumpUsesDeepTable", false)) text += ", deep-set";
            return text;
        }

        // ------------------------------------------------------------------ Lighting (R10.1, cheap)

        void FindSun()
        {
            if (sun == null) sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null) return;
            sunBaseColor = sun.color;
            sunAppliedMult = 1f;
        }

        void ApplySun(int index)
        {
            if (sun == null || !balance.stageLighting) return;
            Color tint = balance.stageSunTint != null && index < balance.stageSunTint.Length ? balance.stageSunTint[index] : Color.white;
            float mult = balance.stageSunIntensity != null && index < balance.stageSunIntensity.Length ? balance.stageSunIntensity[index] : 1f;
            sun.color = sunBaseColor * tint;
            // Intensity is changed RELATIVELY (swap our old multiplier for the new one), because
            // the Thunderstorm (EventManager) also dims the sun relatively. Setting it outright
            // here would undo the storm's dimming, and the storm's "undim" later would then
            // leave the sun too bright for the rest of the game.
            if (sunAppliedMult > 0f) sun.intensity = sun.intensity / sunAppliedMult * mult;
            sunAppliedMult = mult;
        }

        // ------------------------------------------------------------------ Saving (R11.8)

        /// <summary>Right after the clock, so the stage from the saved day is known.</summary>
        public int RestoreOrder => RestoreOrders.Clock + 1;

        /// <summary>Saves a stage forced with debug key 8.</summary>
        public void Capture(SaveData data)
        {
            data.hasDrought = true;
            data.stageOverride = WorldState.StageOverride;
        }

        /// <summary>Puts back the forced stage and shows the loaded stage quietly (no banner).</summary>
        public void Restore(SaveData data)
        {
            WorldState.StageOverride = data.hasDrought ? data.stageOverride : -1;
            if (balance != null && balance.stages != null && balance.stages.Length > 0)
                ApplyStage(WorldState.StageIndex, announce: false);
        }
    }
}
