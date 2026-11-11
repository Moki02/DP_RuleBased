// GameBalance.cs
// Every tunable number in The Last Drop lives here, so the designer can retune the game
// in the Inspector without touching code. Values match the GDD (Rules tab, section 12
// "Balance sheet", plus sections 4, 5, 6, 9, 10, 11 and 13).
//
// How to use:
//   1. Right-click in the Project window > Create > The Last Drop > Game Balance.
//      Name it "GameBalance". It starts with the full-game values from the GDD.
//   2. Create a second one named "GameBalance_Demo", select it, click the "⋮" menu in the
//      Inspector header and choose "Apply Demo Mode preset" (GDD Rules section 13).
//   3. Give every system a reference to the active GameBalance asset (for example through a
//      GameManager), and read values from it instead of hard-coding numbers.
//
// If you change a value here because of a playtest, update the GDD Balance sheet and the
// Rule change log in the same sitting so the doc and the game never disagree.

using System;
using UnityEngine;

namespace TheLastDrop
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "The Last Drop/Game Balance")]
    public partial class GameBalance : ScriptableObject
    {
        // ------------------------------------------------------------------ Mode
        [Header("Mode")]
        [Tooltip("True only on the GameBalance_Demo asset (GDD Rules section 13).")]
        public bool isDemoMode = false;

        [Tooltip("Debug keys on (Scope and roles > Developer handoff): 0 reveal map, 5 turn zone 2, 6 clock × speed, " +
                 "7 +Clean Storage, 4 toggle rain, 8 next drought stage, 9 next event, F1 debug HUD, F5 save, F8 win now, F9 load. " +
                 "Turn off for the final build.")]
        public bool enableDebugKeys = true;

        [Tooltip("How many maze zones are used. Full game 3, Demo mode 1 (Lock 1 becomes the exit).")]
        [Range(1, 3)] public int zoneCount = 2;

        [Tooltip("Multiplies every Build item and skill cost. Full game 1, Demo mode 0.5 (rounded up).")]
        [Min(0f)] public float skillCostMultiplier = 1f;

        [Tooltip("Playtest: buy Build items and skills anywhere in the maze. Off = only at the hub (R7.2). " +
                 "The Build Playtest Scene menu turns this on.")]
        public bool allowBuyingAnywhere = false;

        [Tooltip("Debug key 7 adds this many litres to Clean Storage (never past its size).")]
        [Min(0f)] public float debugCleanStorageLiters = 50f;

        [Tooltip("NOT USED by the code: a collapse never ends the game on its own; only R11.4 (less than " +
                 "2 L at home) does. Keep it off (GDD Developer handoff).")]
        public bool collapseEndsGame = false;

        // ------------------------------------------------------------------ Clock (R1.5)
        [Header("Clock")]
        [Tooltip("Daylight: real seconds from dawn until the night lockdown starts (R1.5). " +
                 "A whole day is this plus Night Lockdown Seconds (240 + 60 = 300 s).")]
        [Min(1f)] public float dayLengthSeconds = 240f;

        [Tooltip("Night lockdown: real seconds from the start of the night until the next dawn (R8.19). " +
                 "Every gate is closed during this time (R8.21, R8.23).")]
        [Min(0f)] public float nightLockdownSeconds = 60f;

        [Tooltip("Debug key 6 makes the clock run this many times faster (press again for normal speed).")]
        [Min(1f)] public float debugClockSpeed = 4f;

        [Tooltip("Clock time lost when the player collapses (R11.2).")]
        [Min(0f)] public float collapseClockPenalty = 30f;

        // ------------------------------------------------------------------ Player
        [Header("Player")]
        [Min(0f)] public float walkSpeed = 4f;          // m/s
        [Min(0f)] public float sprintSpeed = 6.5f;      // m/s
        [Range(0f, 1f)] public float dehydratedSpeedMult = 0.75f;

        [Tooltip("Jerry Can: slowdown while carrying more than this many liters (R5.4).")]
        [Min(0f)] public float heavyCarryThreshold = 6f;
        [Range(0f, 1f)] public float heavyCarrySpeedMult = 0.9f;

        [Tooltip("How close the player must be to pump, pour, pick up or start a minigame (m).")]
        [Min(0f)] public float interactRange = 2f;

        // ------------------------------------------------------------------ Hydration (section 2)
        [Header("Hydration")]
        [Min(1f)] public float hydMax = 100f;

        [Tooltip("HYD lost per second, before stage, event and skill multipliers (R2.1).")]
        [Min(0f)] public float drainIdle = 0.25f;
        [Min(0f)] public float drainWalk = 0.5f;
        [Min(0f)] public float drainSprint = 1.2f;
        [Min(0f)] public float drainPump = 0.6f;
        [Min(0f)] public float drainMinigame = 0.25f;

        [Tooltip("Drain multiplier inside the hub (shade, R2.2).")]
        [Range(0f, 1f)] public float hubShadeMult = 0.5f;

        [Tooltip("HYD at or below this is Thirsty.")]
        public float thirstyAt = 59f;
        [Tooltip("HYD at or below this is Dehydrated (no sprint, slower walk).")]
        public float dehydratedAt = 29f;
        [Min(1f)] public float thirstySprintMult = 1.5f;

        [Tooltip("HYD gained per liter drunk (Water Discipline skill raises this to 30).")]
        [Min(0f)] public float hydPerLiter = 25f;
        [Min(0f)] public float drinkRate = 0.5f;        // L/s
        [Min(0f)] public float refillRate = 1f;         // L/s at the hub tap

        [Tooltip("Sick status from drinking raw water (R2.5).")]
        [Min(0f)] public float sickSeconds = 30f;
        [Min(1f)] public float sickMult = 1.5f;

        // ------------------------------------------------------------------ Start values
        [Header("Start values")]
        [Min(0f)] public float startClean = 10f;        // L in Clean Storage
        [Min(0f)] public float startCanteen = 2f;       // L, a full starting canteen
        [Min(0f)] public float startRaw = 0f;           // L in the Raw Basin

        // ------------------------------------------------------------------ Sources (section 4)
        [Header("Sources")]
        [Min(0f)] public float pumpLitersPerStroke = 0.3f;
        [Tooltip("Seconds between strokes. Greased Handle skill: 0.35.")]
        [Min(0.01f)] public float pumpStrokeInterval = 0.5f;

        [Tooltip("Suction pumps can't lift water deeper than this (m). Past it the hand pump makes nothing.")]
        [Min(0f)] public float suctionLimit = 7f;

        [Tooltip("Hand pump output by water table depth, without the Deep-Set Cylinder.")]
        public DepthFactor[] pumpByDepth =
        {
            new DepthFactor(4f, 1f),
            new DepthFactor(5.5f, 0.8f),
            new DepthFactor(6.5f, 0.5f),
            new DepthFactor(7f, 0.4f),
        };

        [Tooltip("Hand pump output by water table depth, with the Deep-Set Cylinder skill.")]
        public DepthFactor[] deepSetPumpByDepth =
        {
            new DepthFactor(4f, 1f),
            new DepthFactor(5.5f, 1f),
            new DepthFactor(6.5f, 0.8f),
            new DepthFactor(7.5f, 0.6f),
            new DepthFactor(8.5f, 0.4f),
            new DepthFactor(10f, 0f),
        };

        [Tooltip("Liters per m² per second while it rains.")]
        [Min(0f)] public float rainLitersPerM2s = 0.1f;
        [Range(0f, 1f)] public float runoff = 0.9f;
        [Tooltip("Rain Catcher area in m² (Bigger Roof 8, Wide Roof 16).")]
        [Min(0f)] public float catchArea = 4f;

        [Tooltip("Solar Still base output (L/s, clean). Multiplied by the stage sun factor.")]
        [Min(0f)] public float stillRate = 0.02f;

        [Tooltip("Deep Well base output (L/s, raw). Multiplied by the stage deep-well factor.")]
        [Min(0f)] public float wellRate = 0.08f;

        // ------------------------------------------------------------------ Purifiers (section 6)
        [Header("Purifiers")]
        [Min(0f)] public float boilRate = 0.167f;       // L/s, 1 L every 6 s
        [Min(0f)] public float filterRate = 0.4f;       // L/s when ripe

        [Tooltip("Biosand Filter rate multiplier: [0] the day it's bought, [1] the next day, [2] after that.")]
        public float[] filterRipeningByDay = { 0.5f, 0.75f, 1f };

        [Min(0f)] public float sodisLiters = 8f;        // per batch (4 bottles)
        [Min(0f)] public float sodisSeconds = 90f;
        [Min(0f)] public float chlorineSeconds = 30f;
        [Min(0)] public int chlorineTabletMax = 5;
        [Min(0f)] public float chlorineTabletCost = 1f; // L of clean water per tablet

        // ------------------------------------------------------------------ Storage (section 5)
        [Header("Storage tiers (index 0 = starting tier)")]
        public StorageTier[] canteenTiers =
        {
            new StorageTier("Bamboo Canteen", 2f, 0f),
            new StorageTier("Water Jug", 4f, 0f),
            new StorageTier("Hydration Pack", 6f, 0f),
            new StorageTier("Jerry Can", 10f, 0f),
        };

        [Tooltip("Evaporation factor: 1 = open, 0.5 = covered, 0.1 = underground.")]
        public StorageTier[] cleanStorageTiers =
        {
            new StorageTier("Tapayan", 25f, 1f),
            new StorageTier("Plastic Drum", 50f, 0.5f),
            new StorageTier("Ferrocement Tank", 150f, 0.5f),
            new StorageTier("Underground Cistern", 300f, 0.1f),
        };

        public StorageTier[] rawBasinTiers =
        {
            new StorageTier("Basin", 15f, 1f),
            new StorageTier("Bigger Basin", 40f, 1f),
            new StorageTier("Concrete Basin", 100f, 1f),
        };

        // ------------------------------------------------------------------ Drought stages (section 10)
        [Header("Drought stages (in order)")]
        public StageSettings[] stages =
        {
            new StageSettings("El Niño Advisory", 1, 1.0f, 0.50f, 0.02f, 4.0f, 1.0f, 1.00f,
                new EventWeights(50, 10, 0, 0, 0, 0, 0, 40)),
            new StageSettings("Dry Spell", 3, 1.1f, 0.35f, 0.03f, 5.5f, 1.2f, 0.95f,
                new EventWeights(35, 20, 10, 0, 10, 0, 5, 20)),
            new StageSettings("Drought", 5, 1.25f, 0.15f, 0.05f, 6.5f, 1.4f, 0.85f,
                new EventWeights(15, 25, 15, 15, 10, 5, 5, 10)),
            new StageSettings("State of Calamity", 12, 1.4f, 0.05f, 0.08f, 7.5f, 1.6f, 0.75f,
                new EventWeights(5, 30, 15, 15, 10, 10, 10, 5)),
        };

        // ------------------------------------------------------------------ Maze and mapping (section 8)
        [Header("Maze and mapping")]
        [Tooltip("Size of one maze tile in metres (GDD: 4 m). The builder places tile (x, y) at x × this on X and (last row − y) × this on Z.")]
        [Min(0.1f)] public float cellSize = 4f;         // m
        [Tooltip("OLD: from the 41-cell layout. Not used any more; the maze size now comes from the CSV (95 × 95).")]
        [Min(1)] public int gridSize = 41;              // cells per side

        // ------------------------------------------------------------------ Maze layout (rings)
        // A tile's "ring" is how many steps it is from the centre tile, counting diagonal steps
        // as one: ring = max(|x − centre|, |y − centre|). Each part of the maze is a band of rings.
        [Header("Maze layout (2-layer map, GDD R8.24)")]
        [Tooltip("Height of grey-box walls and of the invisible wall colliders, in metres (GDD: 10 to 12 m).")]
        [Min(0.5f)] public float wallHeight = 11f;

        [Tooltip("Last ring of the hub (rings 0 to this). The hub is the 13 × 13 base plus its 3-tile wall with 4 gates.")]
        [Min(0)] public int hubMaxRing = 9;

        [Tooltip("Last ring of zone 1, including its outer wall with the 4 exit gates. Zone 1 never turns.")]
        [Min(0)] public int zone1MaxRing = 27;

        [Tooltip("Last ring of zone 2, the part that turns. Rings after this are the outer wall with zone 2's 8 exit gates.")]
        [Min(0)] public int zone2MaxRing = 44;

        [Tooltip("Zone 2 turns 90 degrees clockwise at the start of the night lockdown of every Nth day (R8.24). " +
                 "2 = the nights of days 2, 4, 6 ...")]
        [Min(1)] public int zone2DaysBetweenTurns = 2;
        [Tooltip("OLD: circle reveal in metres. Not used any more; the map now uses the tile rules R8.1a to R8.1d (fields below).")]
        [Min(0f)] public float revealRadius = 8f;       // m
        [Tooltip("OLD: circle reveal with Trail Sense, in metres. Not used any more (see Map Look Ahead Tiles Trail Sense).")]
        [Min(0f)] public float revealRadiusTrailSense = 14f;
        [Tooltip("Share of a section's tiles (0-1) that counts as 'mapped' for the section reward. Not used in the playtest.")]
        [Range(0f, 1f)] public float sectionMappedPct = 0.75f;
        [Tooltip("Map markers the player can place with F (R8.4).")]
        [Min(0)] public int markerLimit = 3;
        [Tooltip("REFERENCE ONLY: the real V2 Landmarks value is the \"maxMarkers\" effect in BuildAndSkills_Effects.csv (8). Keep the two the same.")]
        [Min(0)] public int markerLimitLandmarks = 8;

        [Header("Map reveal (R8.1a to R8.1d)")]
        [Tooltip("R8.1b: tiles revealed ahead of the player, in the camera's facing direction (north/east/south/west).")]
        [Min(0)] public int mapLookAheadTiles = 2;
        [Tooltip("REFERENCE ONLY: the real V1 Trail Sense value is the \"lookAheadTiles\" effect in BuildAndSkills_Effects.csv (4). Keep the two the same.")]
        [Min(0)] public int mapLookAheadTilesTrailSense = 4;
        [Tooltip("Off = the look-ahead only happens when the player steps onto a new tile (the rule as written). " +
                 "On = it also happens when the player turns the camera while standing still.")]
        public bool mapLookAheadWhenTurning = false;
        [Tooltip("How long revealed zone 2 tiles flash the first time the map opens after zone 2 turned, in seconds.")]
        [Min(0f)] public float mapTurnFlashSeconds = 2f;

        // ------------------------------------------------------------------ Rewards
        [Header("Rewards (go to unbanked water)")]
        [Tooltip("Section mapped reward per zone: [0] zone 1, [1] zone 2, [2] zone 3.")]
        public float[] sectionReward = { 2f, 3f, 4f };

        [Tooltip("Whole-maze map % thresholds (0-1) and their rewards, same order.")]
        public float[] milestonePercents = { 0.25f, 0.5f, 0.75f, 1f };
        public float[] milestoneRewards = { 10f, 15f, 20f, 30f };

        [Tooltip("First-open reward for a minigame (valve) gate in litres, per zone: [0] zone 1, [1] zone 2, [2] zone 3 (R8.8). Paid once into unbanked water.")]
        public float[] gateReward = { 2f, 2f, 3f };

        [Tooltip("Share of unbanked water lost on collapse (R11.2).")]
        [Range(0f, 1f)] public float collapseUnbankedLoss = 0.5f;

        // ------------------------------------------------------------------ Lock basins
        [Header("Lock basins")]
        [Tooltip("Litres to fill each lock basin (R8.9): [0] hub gates, [1] zone 1 exits, [2] zone 2 exits. Demo mode uses only [0].")]
        public float[] lockLiters = { 4f, 10f, 18f };
        [Tooltip("Litres per second poured from the canteen into a lock basin while E is held.")]
        [Min(0f)] public float pourRate = 1f;           // L/s

        // ------------------------------------------------------------------ Minigames (section 9)
        [Header("Minigames")]
        [Tooltip("HYD lost when a minigame is lost (R9.3). Applied by SurvivalRules.MinigameLost.")]
        [Min(0f)] public float minigameFailHyd = 5f;
        [Tooltip("Seconds a gate's minigame stays locked after a loss (R9.3).")]
        [Min(0f)] public float minigameLockout = 10f;   // s

        [Header("Valve Timing difficulty by distance from the hub (R9.6)")]
        [Tooltip("t = clamp01((ring − this) / Ring Span). The ring is that of the gate's tile nearest the centre. At this ring t = 0 (easiest).")]
        public int valveDifficultyRingStart = 9;
        [Tooltip("How many rings it takes for t to go from 0 to 1 (hardest). 35 = from the hub wall to the edge of zone 2.")]
        [Min(1)] public int valveDifficultyRingSpan = 35;
        [Tooltip("Green band width at t = 0, in degrees.")]
        [Min(1f)] public float valveBandAtHub = 30f;
        [Tooltip("How many degrees the band shrinks by t = 1. Band = Band At Hub − this × t.")]
        [Min(0f)] public float valveBandShrink = 15f;
        [Tooltip("Needle speed at t = 0, in degrees per second.")]
        [Min(1f)] public float valveSpeedAtHub = 120f;
        [Tooltip("How many degrees per second the needle gains by t = 1. Speed = Speed At Hub + this × t.")]
        [Min(0f)] public float valveSpeedGain = 80f;
        [Tooltip("Hits needed to win a normal gate or valve.")]
        [Min(1)] public int valveHitsToWin = 3;
        [Tooltip("Misses that lose a normal gate or valve.")]
        [Min(1)] public int valveMissesToLose = 3;

        [Tooltip("Valve Timing rows: [3] is the EXIT VALVE, used by zone 2's 8 exit gates (12°, 220°/s, 4 hits, 3 misses). " +
                 "[0] to [2] are the old per-zone rows, still used by hand-placed ValveGates in the old scenes.")]
        public ValveTimingSettings[] valveTiming =
        {
            new ValveTimingSettings(30f, 120f, 3, 3),
            new ValveTimingSettings(22f, 160f, 3, 3),
            new ValveTimingSettings(15f, 200f, 3, 3),
            new ValveTimingSettings(12f, 220f, 4, 3),
        };
        [Tooltip("After a hit, the green band moves at least this many degrees.")]
        [Min(0f)] public float valveBandMinMove = 40f;

        [Tooltip("Pipe Connect (nice to have): grid size and time limit per zone.")]
        public int[] pipeGridSize = { 4, 5, 5 };
        public float[] pipeTimeLimit = { 45f, 60f, 60f };

        [Tooltip("Leak Patch (nice to have).")]
        [Min(0f)] public float leakClickWindow = 1.5f;
        [Min(1)] public int leaksToWin = 10;
        [Min(1)] public int leakMissesToLose = 3;
        [Min(0f)] public float leakIntervalStart = 1.2f;
        [Min(0f)] public float leakIntervalEnd = 0.6f;

        // ------------------------------------------------------------------ Events (section 10)
        [Header("Events")]
        [Tooltip("The daily event fires at a random time between these two values (s into the day).")]
        [Min(0f)] public float eventWindowMin = 40f;
        [Min(0f)] public float eventWindowMax = 260f;

        [Header("Thunderstorm")]
        [Min(0f)] public float stormWarn = 20f;
        [Min(0f)] public float stormSeconds = 60f;
        [Min(0f)] public float stormDrainMult = 0.5f;
        [Min(0f)] public float stormCanteenRefill = 0.05f;  // L/s while standing in the open

        [Header("Heatwave")]
        [Min(1f)] public float heatwaveMult = 1.3f;
        [Min(1f)] public float heatwaveMultStage4 = 1.6f;
        [Min(1f)] public float heatwaveStillMult = 2f;
        [Min(1f)] public float heatwaveEvapMult = 2f;
        public int heatIndexDanger = 46;            // °C shown on the HUD
        public int heatIndexExtreme = 53;

        [Header("Water Table Drop")]
        [Min(0f)] public float waterTableDropMeters = 1f;

        [Header("Cloud Seeding")]
        [Min(0f)] public float seedWait = 90f;
        [Range(0f, 1f)] public float seedChance = 0.7f;
        [Min(0f)] public float seedStorm = 45f;

        [Header("Nice-to-have events")]
        [Min(0f)] public float rationSeconds = 60f;
        [Min(0f)] public float tankerSeconds = 60f;
        [Min(0f)] public float tankerLiters = 20f;
        [Min(0f)] public float fireSeconds = 90f;
        [Min(0f)] public float fireHydPerSec = 10f;
        [Min(0f)] public float fireLiters = 2f;

        [Tooltip("Events that always happen at a set time (Demo mode). Leave empty for the full game.")]
        public ScriptedEvent[] scriptedEvents = Array.Empty<ScriptedEvent>();

        // ------------------------------------------------------------------ Collapse (section 11)
        [Header("Collapse and game over")]
        [Tooltip("Liters auto-drunk from Clean Storage on waking. Wake HYD = this × hydPerLiter.")]
        [Min(0f)] public float wakeLiters = 2f;
        [Tooltip("Game over if Clean Storage holds less than this on waking (R11.4).")]
        [Min(0f)] public float gameOverBelowLiters = 2f;

        // ================================================================== Helpers

        /// <summary>Index into <see cref="stages"/> for a 1-based day number.</summary>
        public int GetStageIndex(int day)
        {
            int index = 0;
            for (int i = 0; i < stages.Length; i++)
            {
                if (day >= stages[i].startDay) index = i;
            }
            return index;
        }

        public StageSettings GetStage(int day) => stages[GetStageIndex(day)];

        /// <summary>True for the last stage (State of Calamity), which uses the stronger heatwave.</summary>
        public bool IsFinalStage(int day) => GetStageIndex(day) == stages.Length - 1;

        /// <summary>Hand pump output multiplier (0-1) for a water table depth in meters.</summary>
        public float GetPumpFactor(float depth, bool hasDeepSetCylinder)
        {
            if (!hasDeepSetCylinder && depth > suctionLimit) return 0f;
            return Evaluate(hasDeepSetCylinder ? deepSetPumpByDepth : pumpByDepth, depth);
        }

        /// <summary>The exit valve's Valve Timing settings (row [3]), used by zone 2's exit gates (R11.6).</summary>
        public ValveTimingSettings ExitValveSettings => valveTiming[Mathf.Min(3, valveTiming.Length - 1)];

        /// <summary>The ring limits of the hub, zone 1 and zone 2, packed for <see cref="MazeGrid"/>.</summary>
        public MazeRings GetMazeRings() => new MazeRings(hubMaxRing, zone1MaxRing, zone2MaxRing);

        /// <summary>Wake-up HYD after a collapse (R11.3).</summary>
        public float GetWakeHyd(float currentHydPerLiter) => Mathf.Min(hydMax, wakeLiters * currentHydPerLiter);

        /// <summary>Skill cost after the Demo mode multiplier, rounded up to whole liters.</summary>
        public float GetSkillCost(float baseCost) => Mathf.Ceil(baseCost * skillCostMultiplier);

        /// <summary>Biosand Filter multiplier for the number of days since it was bought.</summary>
        public float GetFilterRipeness(int daysSincePurchase)
        {
            if (filterRipeningByDay == null || filterRipeningByDay.Length == 0) return 1f;
            int i = Mathf.Clamp(daysSincePurchase, 0, filterRipeningByDay.Length - 1);
            return filterRipeningByDay[i];
        }

        /// <summary>Lock size for a zone index (0 = Lock 1). In Demo mode Lock 1 is the exit.</summary>
        public float GetLockLiters(int zoneIndex) =>
            lockLiters[Mathf.Clamp(zoneIndex, 0, lockLiters.Length - 1)];

        // Piecewise-linear lookup, clamped at both ends.
        static float Evaluate(DepthFactor[] points, float depth)
        {
            if (points == null || points.Length == 0) return 1f;
            if (depth <= points[0].depth) return points[0].factor;
            for (int i = 1; i < points.Length; i++)
            {
                if (depth <= points[i].depth)
                {
                    DepthFactor a = points[i - 1];
                    DepthFactor b = points[i];
                    float t = Mathf.InverseLerp(a.depth, b.depth, depth);
                    return Mathf.Lerp(a.factor, b.factor, t);
                }
            }
            return points[points.Length - 1].factor;
        }

        // ================================================================== Presets

        [ContextMenu("Apply Demo Mode preset")]
        void ApplyDemoPreset()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Demo Mode preset");
#endif
            ResetToFullGame();
            isDemoMode = true;
            zoneCount = 1;
            dayLengthSeconds = 75f;
            startClean = 15f;
            skillCostMultiplier = 0.5f;
            lockLiters = new[] { 8f };
            stages[0].startDay = 1;
            stages[1].startDay = 2;
            stages[2].startDay = 3;
            stages[3].startDay = 4;
            scriptedEvents = new[]
            {
                new ScriptedEvent(2, 20f, GameEventType.Thunderstorm),
                new ScriptedEvent(3, 20f, GameEventType.Heatwave),
            };
            MarkDirty();
            Debug.Log("GameBalance: Demo Mode preset applied (GDD Rules section 13).", this);
        }

        [ContextMenu("Reset to full-game values")]
        void ResetToFullGame()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Reset GameBalance");
#endif
            GameBalance defaults = CreateInstance<GameBalance>();
            string keepName = name;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), this);
            name = keepName;
            if (Application.isPlaying) Destroy(defaults); else DestroyImmediate(defaults);
            MarkDirty();
        }

        // Makes sure preset changes are saved to the asset file.
        void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void OnValidate()
        {
            if (eventWindowMax < eventWindowMin) eventWindowMax = eventWindowMin;
            if (dehydratedAt > thirstyAt) dehydratedAt = thirstyAt;
            // A whole day is daylight + night (240 + 60 = 300 s); events roll up to 260 s into it (R10.4).
            float wholeDay = dayLengthSeconds + nightLockdownSeconds;
            if (wholeDay > 0f && eventWindowMax > wholeDay) eventWindowMax = wholeDay;
            // The maze parts must go outwards in order: hub, then zone 1, then zone 2.
            if (zone1MaxRing < hubMaxRing) zone1MaxRing = hubMaxRing;
            if (zone2MaxRing < zone1MaxRing) zone2MaxRing = zone1MaxRing;
        }
    }

    // ====================================================================== Data types

    [Serializable]
    public class DepthFactor
    {
        [Tooltip("Water table depth (m).")] public float depth;
        [Tooltip("Pump output multiplier at that depth (0-1).")] [Range(0f, 1f)] public float factor;

        public DepthFactor(float depth, float factor)
        {
            this.depth = depth;
            this.factor = factor;
        }
    }

    [Serializable]
    public class StorageTier
    {
        public string displayName;
        [Min(0f)] public float capacity;
        [Tooltip("Evaporation multiplier: 1 open, 0.5 covered, 0.1 underground, 0 never.")]
        [Range(0f, 1f)] public float evaporationFactor;

        public StorageTier(string displayName, float capacity, float evaporationFactor)
        {
            this.displayName = displayName;
            this.capacity = capacity;
            this.evaporationFactor = evaporationFactor;
        }
    }

    [Serializable]
    public class StageSettings
    {
        public string displayName;
        [Tooltip("First day of this stage (1-based).")] [Min(1)] public int startDay;
        [Tooltip("Multiplies all HYD drain.")] [Min(0f)] public float heatMult;
        [Tooltip("For reference: should equal the Thunderstorm weight / 100.")] [Range(0f, 1f)] public float rainChance;
        [Tooltip("Share of stored water lost at the end of each day in an open container.")] [Range(0f, 1f)] public float evaporation;
        [Tooltip("Water table depth in meters (drives the hand pump).")] [Min(0f)] public float waterTableDepth;
        [Tooltip("Solar Still multiplier.")] [Min(0f)] public float solarSunFactor;
        [Tooltip("Deep Well multiplier.")] [Range(0f, 1f)] public float deepWellFactor;
        public EventWeights eventWeights;

        public StageSettings(string displayName, int startDay, float heatMult, float rainChance,
            float evaporation, float waterTableDepth, float solarSunFactor, float deepWellFactor,
            EventWeights eventWeights)
        {
            this.displayName = displayName;
            this.startDay = startDay;
            this.heatMult = heatMult;
            this.rainChance = rainChance;
            this.evaporation = evaporation;
            this.waterTableDepth = waterTableDepth;
            this.solarSunFactor = solarSunFactor;
            this.deepWellFactor = deepWellFactor;
            this.eventWeights = eventWeights;
        }
    }

    /// <summary>Daily event roll weights out of 100 (R10.4, R10.5). If a nice-to-have event
    /// isn't built, EventManager should add its weight to Calm.</summary>
    [Serializable]
    public class EventWeights
    {
        [Min(0)] public int thunderstorm;
        [Min(0)] public int heatwave;
        [Min(0)] public int waterTableDrop;
        [Min(0)] public int cloudSeeding;
        [Min(0)] public int rationing;
        [Min(0)] public int reliefTanker;
        [Min(0)] public int grassFire;
        [Min(0)] public int calm;

        public EventWeights(int thunderstorm, int heatwave, int waterTableDrop, int cloudSeeding,
            int rationing, int reliefTanker, int grassFire, int calm)
        {
            this.thunderstorm = thunderstorm;
            this.heatwave = heatwave;
            this.waterTableDrop = waterTableDrop;
            this.cloudSeeding = cloudSeeding;
            this.rationing = rationing;
            this.reliefTanker = reliefTanker;
            this.grassFire = grassFire;
            this.calm = calm;
        }

        public int Get(GameEventType type)
        {
            switch (type)
            {
                case GameEventType.Thunderstorm: return thunderstorm;
                case GameEventType.Heatwave: return heatwave;
                case GameEventType.WaterTableDrop: return waterTableDrop;
                case GameEventType.CloudSeeding: return cloudSeeding;
                case GameEventType.Rationing: return rationing;
                case GameEventType.ReliefTanker: return reliefTanker;
                case GameEventType.GrassFire: return grassFire;
                default: return calm;
            }
        }

        public int Total => thunderstorm + heatwave + waterTableDrop + cloudSeeding
                            + rationing + reliefTanker + grassFire + calm;
    }

    [Serializable]
    public class ValveTimingSettings
    {
        [Tooltip("Green band width in degrees.")] [Min(1f)] public float bandWidth;
        [Tooltip("Needle speed in degrees per second.")] [Min(1f)] public float needleSpeed;
        [Min(1)] public int hitsToWin;
        [Min(1)] public int missesToLose;

        public ValveTimingSettings(float bandWidth, float needleSpeed, int hitsToWin, int missesToLose)
        {
            this.bandWidth = bandWidth;
            this.needleSpeed = needleSpeed;
            this.hitsToWin = hitsToWin;
            this.missesToLose = missesToLose;
        }
    }

    [Serializable]
    public class ScriptedEvent
    {
        [Min(1)] public int day;
        [Min(0f)] public float secondsIntoDay;
        public GameEventType type;

        public ScriptedEvent(int day, float secondsIntoDay, GameEventType type)
        {
            this.day = day;
            this.secondsIntoDay = secondsIntoDay;
            this.type = type;
        }
    }

    public enum GameEventType
    {
        Calm,
        Thunderstorm,
        Heatwave,
        WaterTableDrop,
        CloudSeeding,
        Rationing,
        ReliefTanker,
        GrassFire,
    }
}
