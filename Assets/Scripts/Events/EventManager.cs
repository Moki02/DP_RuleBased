// ============================================================================
// EventManager.cs
// The daily El Niño event (GDD section 10 "Daily events"). Once a day, at a random time,
// it rolls one event from the current drought stage's table and runs it:
//   - Calm:         nothing happens (a small "Calm day" toast).
//   - Thunderstorm: 20 s warning ("clouds darkening, thunder"), then 60 s of rain.
//                   While it rains: WorldState.IsRaining = true (the Rain Catcher runs, the
//                   Solar Still stops), HYD drain × 0.5, and the canteen refills at 0.05 L/s
//                   everywhere except inside a maze room (rooms have a roof). The sun dims a bit.
//   - Heatwave:     no warning, lasts until dawn. HYD drain × 1.3 (× 1.6 in the last stage),
//                   WorldState.HeatwaveActive = true (Solar Still × 2), tonight's evaporation × 2.
//                   The HUD shows the heat index ("Danger 46°C" / "Extreme Danger 53°C").
// Water Table Drop, Cloud Seeding, Rationing, Relief Tanker and Grass Fire are not built:
// their weights are added to Calm (R10.5). NOTE: the GDD's event table still marks Water
// Table Drop and Cloud Seeding as "Must"; the batch plan (BATCHES.md) moved them to nice to
// have. Until they're built, Calm days are more common than the table says (stage 3: 60%
// Calm instead of 10%).
//
// When does it happen?
//   - The time is rolled at dawn, between Event Window Min and Max seconds into the day
//     (R10.4). GDD R10.4 says "up to 260 s" in a 300 s day, so by default the event can
//     start in the first 20 s of the night lockdown. Tick "Events Only In Daylight" to cut
//     the window at the end of daylight instead. The window never goes past the whole day.
//   - Demo mode (GDD section 13): if GameBalance > Scripted Events has an entry for today,
//     that event happens at that time instead of the roll. Days up to the last scripted day
//     that have no entry are Calm (so the demo's day 1 is quiet); after it, the roll is used.
//   - Only one event runs at a time (R10.7). If the time comes while another event is still
//     running (a debug event, or a storm that started late and runs past dawn), the roll
//     waits until it ends.
//   - A storm that is still going at dawn keeps going until its timer ends; a heatwave ends
//     at dawn ("rest of the day").
//
// DESIGN RULES: R2.1 (drain multipliers multiply), R10.4, R10.5, R10.6, R10.7, section 10
// "What each event does" (Thunderstorm, Heatwave, Calm), section 13 (Demo scripted events).
//
// HOW TO USE: added to the game scene by itself (AutoSystems). Nothing to set.
//   Debug key 9: ends the running event and starts the next one in the order
//   Thunderstorm, Heatwave, Calm, Thunderstorm, ... (it does NOT use up the day's roll).
// HOW TO EDIT: GameBalance > Events (window, storm, heatwave numbers, scripted events),
//   GameBalance > Drought stages > Event Weights (the roll), GameBalance > Events (extra).
// SAVING: running events are not saved (the autosave is at dawn, before any event). A
//   mid-day save remembers whether today's event already happened (so loading never gives
//   a second one) and the day a heatwave started (it comes back only if the save is from
//   that same day, so a dawn save never brings back yesterday's heatwave). A storm is lost
//   on load.
// SUN: a storm dims the sun relatively (× Storm Light Mult) and undoes it when the rain
//   ends, unless the drought stages set the sun in between (then their value is kept).
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Rolls and runs the daily El Niño event (Calm, Thunderstorm, Heatwave).</summary>
    public class EventManager : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<EventManager>();

        /// <summary>The key this system uses in HydrationModifiers.</summary>
        public const string ModifierKey = "event";

        /// <summary>The key of the event's line on the HUD.</summary>
        public const string HudKey = "event";

        /// <summary>Restored after the clock (it needs the loaded day) and before the player.</summary>
        public const int EventsRestoreOrder = RestoreOrders.Lockdown + 5;

        /// <summary>What part of an event is running now.</summary>
        public enum Phase { None, StormWarning, StormRain, Heatwave }

        /// <summary>The EventManager in this scene (null if there isn't one).</summary>
        public static EventManager Instance { get; private set; }

        /// <summary>What is running now (None between events).</summary>
        public Phase Current => phase;

        /// <summary>True while any event (or a storm's warning) is running (R10.7).</summary>
        public bool EventRunning => phase != Phase.None;

        /// <summary>Seconds into today when today's event starts, or -1 if there is none today.</summary>
        public float PlannedTime => plannedTime;

        GameBalance balance;
        GameClock_Manager clock;
        MazeBuilder maze;

        Phase phase = Phase.None;
        float phaseTimer;               // seconds left in the current phase (storm only)

        int plannedDay;                 // the day plannedTime belongs to
        float plannedTime = -1f;        // seconds into plannedDay; -1 = no event that day
        bool plannedIsScripted;
        GameEventType plannedType;      // only used when scripted
        int firedDay;                   // the last day whose event has started

        int debugCycle;                 // debug key 9: 0 Thunderstorm, 1 Heatwave, 2 Calm
        bool registeredDebugKey;

        int heatwaveDay;                // the day the running heatwave started on (for the save)
        bool evapSetByHeatwave;         // we set WorldState.EvaporationMultiplierTonight
        bool resetEvapNextFrame;        // after dawn: put it back to 1 once the evaporation has used it

        Light sun;
        float sunDimmedBy = 1f;         // the factor we multiplied the sun by (1 = not dimmed)
        float sunDimmedValue;           // the intensity we left the sun at (to spot someone else changing it)

        // ------------------------------------------------------------------ Unity

        void Start()
        {
            Instance = this;
            balance = BalanceProvider.Current;
            clock = GameClock_Manager.Instance;
            if (balance == null || clock == null || clock.Clock == null)
            {
                Debug.LogWarning("[EventManager] No GameBalance or no clock in this scene: daily events are off.", this);
                enabled = false;
                return;
            }

            maze = FindAnyObjectByType<MazeBuilder>();
            clock.NewDay += OnNewDay;
            HydrationModifiers.Add(ModifierKey, DrainMultiplier);
            DebugKeys.Register(Key.Digit9, "next event (Thunderstorm > Heatwave > Calm)", DebugNextEvent);
            registeredDebugKey = true;

            PlanDay(clock.Day, clock.Clock.TimeOfDay);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (clock != null) clock.NewDay -= OnNewDay;
            HydrationModifiers.Remove(ModifierKey);
            if (registeredDebugKey) DebugKeys.Unregister(Key.Digit9);

            // Don't leave our flags on for the next scene. (No SimpleHud calls here: the HUD
            // may already be gone while the scene unloads.)
            if (phase == Phase.StormRain) WorldState.IsRaining = false;
            if (phase == Phase.Heatwave) WorldState.HeatwaveActive = false;
            RestoreSun();
        }

        void Update()
        {
            if (clock == null || clock.Clock == null) return;   // the clock is gone (scene unloading)

            // Event timers follow the game clock: they stop when the clock is paused (Build menu,
            // pause menu) and run faster with debug key 6, like the day itself.
            float dt = clock.Clock.Paused ? 0f : Time.deltaTime * clock.Clock.SpeedMultiplier;

            if (resetEvapNextFrame)
            {
                // The day change has happened and the evaporation has read the multiplier by now.
                resetEvapNextFrame = false;
                if (evapSetByHeatwave && phase != Phase.Heatwave) WorldState.EvaporationMultiplierTonight = 1f;
                evapSetByHeatwave = phase == Phase.Heatwave;
            }

            TickPhase(dt);
            CheckPlannedEvent();
            if (phase == Phase.StormRain) RefillCanteenInRain(dt);
            RefreshHudLine();
        }

        // ------------------------------------------------------------------ Planning the day (R10.4)

        void OnNewDay(int day)
        {
            // The heatwave lasts "the rest of the day" (section 10). A storm keeps its own timer.
            if (phase == Phase.Heatwave)
            {
                EndHeatwave(atDawn: true);
                SimpleHud.Toast("The heatwave breaks with the new day.", 3f);
            }
            PlanDay(day, 0f);
        }

        /// <summary>Works out when (and, for scripted days, which) event happens on <paramref name="day"/>.
        /// <paramref name="now"/> is the time of day right now; the plan is never in the past.</summary>
        void PlanDay(int day, float now)
        {
            plannedDay = day;
            plannedIsScripted = false;
            plannedTime = -1f;

            // Demo mode: scripted events (GDD section 13).
            var scripted = balance.scriptedEvents;
            int lastScriptedDay = 0;
            ScriptedEvent today = null;
            if (scripted != null)
                foreach (var s in scripted)
                {
                    if (s == null) continue;
                    if (s.day > lastScriptedDay) lastScriptedDay = s.day;
                    if (s.day == day && today == null) today = s;
                }

            if (today != null)
            {
                plannedIsScripted = true;
                plannedType = today.type;
                plannedTime = Mathf.Max(today.secondsIntoDay, now);
            }
            else if (day <= lastScriptedDay)
            {
                plannedTime = -1f;      // a demo day before the random days start: Calm, quietly
            }
            else
            {
                GetWindow(out float lo, out float hi);
                lo = Mathf.Max(lo, now);
                plannedTime = lo >= hi ? now : Random.Range(lo, hi);
            }

            if (plannedTime >= 0f)
                Debug.Log($"[EventManager] Day {day}: event at {plannedTime:0} s into the day" +
                          (plannedIsScripted ? $" (scripted {plannedType})." : " (rolled then)."), this);
        }

        /// <summary>The roll window in seconds into the day (R10.4), kept inside the day.</summary>
        void GetWindow(out float lo, out float hi)
        {
            float limit = balance.eventsOnlyInDaylight ? clock.Clock.DaylightSeconds : clock.Clock.DayLength;
            limit = Mathf.Max(0f, limit - 1f);     // leave a second so it never lands on the day change
            hi = Mathf.Min(Mathf.Max(balance.eventWindowMin, balance.eventWindowMax), limit);
            lo = Mathf.Min(balance.eventWindowMin, hi);
        }

        void CheckPlannedEvent()
        {
            int day = clock.Day;
            if (plannedDay != day || plannedTime < 0f || firedDay == day) return;
            if (clock.Clock.TimeOfDay < plannedTime) return;
            if (EventRunning) return;   // R10.7: wait for the running event to end

            firedDay = day;
            GameEventType type = plannedIsScripted ? plannedType : Roll(WorldState.Stage != null ? WorldState.Stage.eventWeights : null);
            Debug.Log($"[EventManager] Day {day}: {type}" + (plannedIsScripted ? " (scripted)." : $" (rolled, stage {WorldState.StageIndex + 1})."), this);
            StartEvent(type);
        }

        /// <summary>Rolls one event from a stage's weights (R10.4). Only Thunderstorm and Heatwave are
        /// built; every other weight is added to Calm (R10.5).</summary>
        public static GameEventType Roll(EventWeights weights)
        {
            if (weights == null) return GameEventType.Calm;
            int total = Mathf.Max(0, weights.thunderstorm) + Mathf.Max(0, weights.heatwave) + CalmWeight(weights);
            if (total <= 0) return GameEventType.Calm;
            return Pick(weights, Random.Range(0, total));
        }

        /// <summary>Calm's weight after the unbuilt events' weights are moved to it (R10.5).</summary>
        public static int CalmWeight(EventWeights w) =>
            Mathf.Max(0, w.calm) + Mathf.Max(0, w.waterTableDrop) + Mathf.Max(0, w.cloudSeeding) +
            Mathf.Max(0, w.rationing) + Mathf.Max(0, w.reliefTanker) + Mathf.Max(0, w.grassFire);

        /// <summary>The event for a roll <paramref name="r"/> from 0 to (total − 1): Thunderstorm first, then Heatwave, then Calm.</summary>
        public static GameEventType Pick(EventWeights w, int r)
        {
            int storm = Mathf.Max(0, w.thunderstorm);
            int heat = Mathf.Max(0, w.heatwave);
            if (r < storm) return GameEventType.Thunderstorm;
            if (r < storm + heat) return GameEventType.Heatwave;
            return GameEventType.Calm;
        }

        // ------------------------------------------------------------------ Running events

        /// <summary>Starts an event now. Anything not built (nice-to-have) runs as Calm (R10.5).</summary>
        void StartEvent(GameEventType type)
        {
            switch (type)
            {
                case GameEventType.Thunderstorm: StartStorm(); break;
                case GameEventType.Heatwave: StartHeatwave(quiet: false); break;
                default:
                    if (type != GameEventType.Calm)
                        Debug.Log($"[EventManager] {type} isn't built (nice to have): Calm instead (R10.5).", this);
                    SimpleHud.Toast("Calm day: no El Niño event today.", 3f);
                    break;
            }
        }

        void TickPhase(float dt)
        {
            if (phase != Phase.StormWarning && phase != Phase.StormRain) return;
            phaseTimer -= dt;
            if (phaseTimer > 0f) return;
            if (phase == Phase.StormWarning) StartRain();
            else EndStorm(quiet: false);
        }

        // Thunderstorm: warning first (section 10, "20 s of darkening clouds and thunder").
        void StartStorm()
        {
            if (balance.stormWarn <= 0f) { StartRain(); return; }
            phase = Phase.StormWarning;
            phaseTimer = balance.stormWarn;
            SimpleHud.Banner("THUNDERSTORM COMING",
                $"Clouds darkening, thunder. Rain in {balance.stormWarn:0} s: stand in the open to refill your canteen.",
                balance.eventBannerSeconds);
        }

        void StartRain()
        {
            phase = Phase.StormRain;
            phaseTimer = balance.stormSeconds;
            WorldState.IsRaining = true;
            DimSun();
            SimpleHud.Banner("THUNDERSTORM",
                $"Rain for {balance.stormSeconds:0} s. Thirst ×{balance.stormDrainMult:0.##}; " +
                $"your canteen refills {balance.stormCanteenRefill:0.##} L/s in the open.",
                balance.eventBannerSeconds);
        }

        void EndStorm(bool quiet)
        {
            phase = Phase.None;
            WorldState.IsRaining = false;
            RestoreSun();
            if (!quiet) SimpleHud.Toast("The rain has stopped.", 3f);
        }

        // Heatwave: no warning, until dawn (section 10).
        void StartHeatwave(bool quiet)
        {
            phase = Phase.Heatwave;
            heatwaveDay = clock.Day;
            WorldState.HeatwaveActive = true;
            WorldState.EvaporationMultiplierTonight = balance.heatwaveEvapMult;
            evapSetByHeatwave = true;
            resetEvapNextFrame = false;
            if (!quiet)
                SimpleHud.Banner("HEATWAVE",
                    $"Heat index: {HeatIndexText()}. Thirst ×{HeatwaveMult():0.##} until dawn; " +
                    $"tonight's evaporation ×{balance.heatwaveEvapMult:0.##}.",
                    balance.eventBannerSeconds);
        }

        void EndHeatwave(bool atDawn)
        {
            phase = Phase.None;
            WorldState.HeatwaveActive = false;
            // At dawn the evaporation system still needs tonight's ×2: put it back to 1 a frame later.
            // Ended any other way (debug key, loading): the heatwave never reached the night, so undo it now.
            if (atDawn) resetEvapNextFrame = true;
            else if (evapSetByHeatwave)
            {
                WorldState.EvaporationMultiplierTonight = 1f;
                evapSetByHeatwave = false;
            }
        }

        /// <summary>Stops whatever is running, with no messages (debug key, loading a save).</summary>
        void CancelEvent()
        {
            if (phase == Phase.StormWarning || phase == Phase.StormRain) EndStorm(quiet: true);
            else if (phase == Phase.Heatwave) EndHeatwave(atDawn: false);
            phase = Phase.None;
        }

        /// <summary>The heatwave's drain multiplier: 1.3, or 1.6 in the last stage.</summary>
        float HeatwaveMult() => WorldState.IsFinalStage ? balance.heatwaveMultStage4 : balance.heatwaveMult;

        /// <summary>"Danger 46°C", or "Extreme Danger 53°C" in the last stage (PAGASA bands).</summary>
        string HeatIndexText() => WorldState.IsFinalStage
            ? $"Extreme Danger {balance.heatIndexExtreme}°C"
            : $"Danger {balance.heatIndexDanger}°C";

        /// <summary>This system's HYD drain multiplier (R2.1): × 0.5 in the rain, × 1.3 / 1.6 in a heatwave.</summary>
        float DrainMultiplier(DrainContext ctx)
        {
            if (balance == null) return 1f;
            if (phase == Phase.StormRain) return balance.stormDrainMult;
            if (phase == Phase.Heatwave) return HeatwaveMult();
            return 1f;
        }

        // ------------------------------------------------------------------ Rain on the canteen

        void RefillCanteenInRain(float dt)
        {
            if (dt <= 0f) return;
            var player = Player_Manager.instance;
            var res = GameResources_Manager.instance;
            if (player == null || res == null) return;
            if (res.canteen >= res.canteen_Max) return;       // full: don't spam the "water full" event
            if (UnderRoof(player.transform.position)) return;

            float added = res.AddCanteenWater(balance.stormCanteenRefill * dt);
            if (added > 0f) RunStats.AddCollected(added);   // R1.3: this water comes from the rain
        }

        /// <summary>True inside a maze room (rooms have a roof). Everywhere else, the hub included, is "the open".</summary>
        public bool UnderRoof(Vector3 worldPosition)
        {
            if (maze == null || maze.Grid == null) return false;
            // Areas store their tiles as in the CSV, so turn the world tile back first (zone 2 turns).
            Vector2Int localTile = maze.Grid.WorldToLocal(maze.WorldToGrid(worldPosition));
            foreach (var area in maze.Areas)
                if (area != null && area.kind == MazeArea.Kind.Room && area.Contains(localTile)) return true;
            return false;
        }

        // ------------------------------------------------------------------ Sun (cheap storm look)

        void DimSun()
        {
            if (sunDimmedBy != 1f) return;
            if (sun == null) sun = FindSun();
            if (sun == null) return;
            sunDimmedBy = balance.stormLightMult;
            sun.intensity *= sunDimmedBy;   // relative, so it doesn't fight the stage lighting
            sunDimmedValue = sun.intensity;
        }

        void RestoreSun()
        {
            if (sunDimmedBy == 1f) return;
            // Undo the dimming only if nobody changed the sun since. The drought stages set the
            // sun's intensity outright (new stage at dawn, debug key 8, loading a save): that value
            // is already the undimmed one, and dividing it again would make the sun too bright.
            if (sun != null && sunDimmedBy > 0f && Mathf.Approximately(sun.intensity, sunDimmedValue))
                sun.intensity /= sunDimmedBy;
            sunDimmedBy = 1f;
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        // ------------------------------------------------------------------ HUD (R10.6)

        void RefreshHudLine()
        {
            string text = null;
            switch (phase)
            {
                case Phase.StormWarning:
                    text = $"STORM COMING {Mathf.CeilToInt(phaseTimer)} s (clouds darkening)";
                    break;
                case Phase.StormRain:
                    var p = Player_Manager.instance;
                    bool roof = p != null && UnderRoof(p.transform.position);
                    text = $"THUNDERSTORM {Mathf.CeilToInt(phaseTimer)} s (thirst ×{balance.stormDrainMult:0.##}, " +
                           (roof ? "under a roof: no refill)" : "canteen refilling)");
                    break;
                case Phase.Heatwave:
                    float toDawn = Mathf.Max(0f, clock.Clock.DayLength - clock.Clock.TimeOfDay);
                    text = $"HEATWAVE {HeatIndexText()} {Mathf.CeilToInt(toDawn)} s (thirst ×{HeatwaveMult():0.##})";
                    break;
            }
            SimpleHud.SetLine(HudKey, text);
        }

        // ------------------------------------------------------------------ Debug key 9

        /// <summary>Debug key 9: ends the running event and starts the next one (Thunderstorm, Heatwave, Calm, ...).
        /// The day's own roll still happens later (once nothing is running).</summary>
        void DebugNextEvent()
        {
            GameEventType next = debugCycle == 0 ? GameEventType.Thunderstorm
                               : debugCycle == 1 ? GameEventType.Heatwave
                               : GameEventType.Calm;
            debugCycle = (debugCycle + 1) % 3;
            CancelEvent();
            Debug.Log($"[EventManager] Debug key 9: {next}.", this);
            StartEvent(next);
        }

        // ------------------------------------------------------------------ Saving (R11.8)

        /// <summary>After the clock (it needs the loaded day).</summary>
        public int RestoreOrder => EventsRestoreOrder;

        /// <summary>Saves whether today's event has happened and whether a heatwave is on.</summary>
        public void Capture(SaveData data)
        {
            data.hasEvents = true;
            data.eventFiredDay = firedDay;
            // The day the heatwave STARTED, not clock.Day: the dawn autosave can run before this
            // script ends the heatwave, and by then clock.Day is already the new day. Saving the
            // new day would bring yesterday's heatwave back when that dawn save is loaded.
            data.heatwaveDay = phase == Phase.Heatwave ? heatwaveDay : 0;
        }

        /// <summary>Clears every event flag, then plans the loaded day (and brings back its heatwave).</summary>
        public void Restore(SaveData data)
        {
            if (!enabled || clock == null) return;
            CancelEvent();
            WorldState.IsRaining = false;
            WorldState.HeatwaveActive = false;
            WorldState.EvaporationMultiplierTonight = 1f;
            evapSetByHeatwave = false;
            resetEvapNextFrame = false;

            int day = clock.Day;
            firedDay = data.hasEvents ? data.eventFiredDay : 0;
            PlanDay(day, clock.Clock.TimeOfDay);

            if (data.hasEvents && data.heatwaveDay == day)
            {
                firedDay = day;
                StartHeatwave(quiet: true);
            }
            RefreshHudLine();
        }
    }
}
