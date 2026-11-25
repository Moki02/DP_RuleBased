// ============================================================================
// TutorialDirector.cs
// The guided first playthrough. It teaches each mechanic the moment the player meets it,
// outlines the real object, and only moves on once the player has done it. It replaces the
// old one-card hints (TutorialHints.cs).
//
// THE MAIN STEPS (in order; each waits for the action):
//   goal      the start card (HowToPlay) says the goal; closed = done
//   move      walk a few metres                          (GameBalance > Tutorial Move Metres)
//   pump      hold E at the pump until the Raw Basin has 1 L       (unlocks the pump)
//   rawBasin  read: the Raw Basin, raw water is dirty
//   pot       the boiling pot moves water into Clean Storage
//   storage   read: Clean Storage is your water and your money
//   hud       HYD bar and canteen pulse; drink with Q
//   tap       refill the canteen at the tap                         (unlocks the tap)
//   gatePour  pour into a hub gate's lock basin until it is full     (unlocks lock basins)
//   valve     the guided first valve minigame (easier, free to fail; TutorialGate)
//   maze      some time outside the hub                             (unlocks the maze)
//   map       open the map (M)                                      (unlocks the map)
//   return    back into the hub (banks the water)
//   build     open Build and Skills (Tab)                           (unlocks Build and Skills)
// A step that can't be done right now (dry pump, empty storage ...) passes by itself, and
// after GameBalance > Tutorial Stuck Seconds on one step the card offers K to skip it.
// Build and Skills also unlock at the first dawn, so a player who spends the first night
// in the hub isn't locked out of them.
//
// SIDE LESSONS (once each, the first time it happens): dusk warning, lockdown, dehydrated,
// sick, first room, room door, valve grate, banking, zone 2 turning, Calamity, drought
// stage, and the first Thunderstorm / Heatwave / Calm day.
// FIRST COLLAPSE: a screen with what was lost, what was kept and what to do next time.
//
// The clock never stops for the tutorial. If night falls during the gate steps, the
// objective says the gate lesson goes on at dawn; the pump and tap keep working.
//
// RUNS ON THE FIRST NEW GAME ONLY: when it finishes (or is skipped) a flag is saved
// (PlayerPrefs, see SkipNextNewGame); the main menu's "Skip tutorial" toggle shows and
// changes it. "Skip tutorial" is also in the pause menu while it runs. Progress is saved
// with the game (SaveData.Tutorial.cs), so Continue resumes the step the player was on.
// Not in Demo mode unless GameBalance > Tutorial In Demo is on.
//
// DESIGN RULES: none of its own (teaches R1 to R11 in play).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems), with
// TutorialOutline. Salok > Tutorial > Set Up Tutorial makes the prefab, the texts asset and
// the main menu toggle.
// HOW TO EDIT: WORDS: the Tutorial Texts asset (Assets/Resources/Tutorial). NUMBERS: GameBalance
// > Tutorial. LOOK: Assets/Resources/UI/Tutorial.prefab (TutorialView) and the outline fields
// on TutorialOutline. ORDER / CONDITIONS: BuildSteps and BuildLessons below.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Runs the guided first playthrough: steps, side lessons, outline, first collapse screen.</summary>
    public class TutorialDirector : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<TutorialDirector>();

        /// <summary>The one in this scene (null if none).</summary>
        public static TutorialDirector Instance { get; private set; }

        /// <summary>PlayerPrefs key: 1 = a new game starts without the tutorial.</summary>
        public const string SkipPrefKey = "Salok.SkipTutorial";

        /// <summary>True if the next new game starts without the tutorial (set once it is finished or skipped).</summary>
        public static bool SkipNextNewGame
        {
            get => PlayerPrefs.GetInt(SkipPrefKey, 0) == 1;
            set { PlayerPrefs.SetInt(SkipPrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [Header("Setup (optional)")]
        [Tooltip("The tutorial screen prefab. Empty = Assets/Resources/UI/Tutorial.prefab (or the built-in look).")]
        public GameObject viewPrefab;
        [Tooltip("The words. Empty = Assets/Resources/Tutorial/TutorialTexts.asset (or the default words).")]
        public TutorialTexts texts;

        [Header("Keys")]
        [Tooltip("Skips the current step once the card offers it (after Tutorial Stuck Seconds).")]
        public Key skipStepKey = Key.K;

        [Header("Reach")]
        [Tooltip("How close (metres) the player must be to a room door, valve grate or room object for its side lesson.")]
        [Min(1f)] public float nearRange = 5f;

        /// <summary>The steps and lessons (for tests and the debug HUD).</summary>
        public TutorialFlow Flow { get; } = new TutorialFlow();

        GameBalance balance;
        TutorialView view;
        TutorialOutline outline;
        GameMenu gameMenu;
        MapScreen mapScreen;
        MazeBuilder maze;
        NightLockdown lockdown;
        Zone2Rotator rotator;
        GameClock_Manager clockManager;
        ValveTimingGame valveGame;
        EventManager events;

        // Facts the steps need, tracked over time.
        Vector3 movePos;
        float raw0, clean0, canteen0;
        float outsideTime;
        float controllableTime;
        int zone2Turns;
        int bankCount;
        GameEventType? lastEvent;
        string guideOverride;
        float guideOverrideLeft;

        // What is on screen now (texts are only rebuilt when these change).
        string shownObjective, shownCard, shownGuide;
        bool deathOpen;
        float targetRetry;
        readonly List<GameObject> targets = new List<GameObject>();
        readonly List<RectTransform> hudTargets = new List<RectTransform>();
        readonly Dictionary<string, string> tokens = new Dictionary<string, string>();

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            Instance = this;
            balance = BalanceProvider.Current;
            if (texts == null) texts = TutorialTexts.Load();

            view = UiBuilder.InstantiateView<TutorialView>(viewPrefab, TutorialView.PrefabResource, transform);
            if (view == null) view = TutorialView.BuildDefault(transform);
            HideAll();
            if (view.skipButton != null) view.skipButton.onClick.AddListener(SkipTutorial);
            if (view.deathContinueButton != null) view.deathContinueButton.onClick.AddListener(CloseDeathScreen);
            ApplyFixedWords();

            outline = GetComponent<TutorialOutline>();
            if (outline == null) outline = gameObject.AddComponent<TutorialOutline>();

            if (balance != null)
            {
                Flow.DoneSeconds = balance.tutorialDoneSeconds;
                Flow.StuckSeconds = balance.tutorialStuckSeconds;
                Flow.LessonSeconds = balance.tutorialLessonSeconds;
            }
            BuildSteps();
            BuildLessons();
            Flow.StepStarted += OnStepStarted;
            Flow.StepCompleted += OnStepCompleted;
            Flow.Finished += OnFinished;
            Flow.LessonStarted += OnLessonStarted;

            TutorialGate.FeatureCheck = Flow.Allows;
            TutorialGate.EasyMinigameCheck = IsEasyMinigame;
            TutorialGate.Blocked = ShowBlocked;
        }

        void Start()
        {
            gameMenu = FindAnyObjectByType<GameMenu>();
            mapScreen = FindAnyObjectByType<MapScreen>();
            maze = FindAnyObjectByType<MazeBuilder>();
            lockdown = FindAnyObjectByType<NightLockdown>();
            rotator = FindAnyObjectByType<Zone2Rotator>();
            clockManager = GameClock_Manager.Instance;
            events = EventManager.Instance != null ? EventManager.Instance : FindAnyObjectByType<EventManager>();

            if (rotator != null) rotator.Zone2Turned += OnZone2Turned;
            if (clockManager != null) clockManager.NewDay += OnNewDay;
            if (events != null) events.EventStarted += OnEventStarted;
            SurvivalDirector.Banked += OnBanked;
            CollapseManager.WokeUp += OnWokeUp;
            HookValveGame();

            // A new game: run unless skipped, already done, or Demo mode. Continue overrides
            // this a few frames later from the save (Restore).
            bool demo = balance != null && balance.isDemoMode;
            bool allowed = balance == null || !demo || balance.tutorialInDemo;
            if (allowed && !SkipNextNewGame) Flow.Begin(0);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (rotator != null) rotator.Zone2Turned -= OnZone2Turned;
            if (clockManager != null) clockManager.NewDay -= OnNewDay;
            if (events != null) events.EventStarted -= OnEventStarted;
            SurvivalDirector.Banked -= OnBanked;
            CollapseManager.WokeUp -= OnWokeUp;
            if (valveGame != null)
            {
                valveGame.onHit.RemoveListener(OnValveHit);
                valveGame.onMiss.RemoveListener(OnValveMiss);
            }
            TutorialGate.FeatureCheck = null;
            TutorialGate.EasyMinigameCheck = null;
            TutorialGate.Blocked = null;
        }

        void HookValveGame()
        {
            if (valveGame != null || ValveTimingGame.Instance == null) return;
            valveGame = ValveTimingGame.Instance;
            valveGame.onHit.AddListener(OnValveHit);
            valveGame.onMiss.AddListener(OnValveMiss);
        }

        // ================================================================== THE STEPS

        void BuildSteps()
        {
            var b = balance;
            float moveM = b != null ? b.tutorialMoveMetres : 3f;
            float pumpL = b != null ? b.tutorialPumpLitres : 1f;
            float potL = b != null ? b.tutorialPotLitres : 0.3f;
            float drinkL = b != null ? b.tutorialDrinkLitres : 0.1f;
            float outsideS = b != null ? b.tutorialOutsideSeconds : 15f;
            float readS = b != null ? b.tutorialReadSeconds : 4.5f;

            Flow.Add(new TutorialStep
            {
                Id = "goal", Silent = true,
                // The start card opens a few frames after loading; half a second of free play = it was closed.
                IsDone = () => controllableTime >= 0.5f
            });
            Flow.Add(new TutorialStep
            {
                Id = "move",
                OnEnter = () => movePos = PlayerPos,
                IsDone = () => Flat(PlayerPos - movePos) >= moveM
            });
            Flow.Add(new TutorialStep
            {
                Id = "pump", Unlocks = TutorialFeature.Pump,
                OnEnter = () => raw0 = Res != null ? Res.rawBasin : 0f,
                IsDone = () => Res != null && Res.rawBasin >= Mathf.Min(raw0 + pumpL, Res.rawBasin_Max - 0.01f),
                CannotBeDone = () => Res == null || PumpDry || !AnyInteractable<HubPump_Manager>()
            });
            Flow.Add(new TutorialStep { Id = "rawBasin", ReadOnly = true, ReadSeconds = readS });
            Flow.Add(new TutorialStep
            {
                Id = "pot",
                OnEnter = () => clean0 = Res != null ? Res.cleanStorage : 0f,
                IsDone = () => Res != null && Res.cleanStorage >= clean0 + potL,
                CannotBeDone = () => Res == null || FindAnyObjectByType<HubPurifier_Manager>() == null ||
                                     Res.cleanStorage >= Res.cleanStorage_Max - 0.01f ||
                                     (Res.rawBasin <= 0.0001f && PumpDry)
            });
            Flow.Add(new TutorialStep { Id = "storage", ReadOnly = true, ReadSeconds = readS });
            Flow.Add(new TutorialStep
            {
                Id = "hud",
                OnEnter = () => canteen0 = Res != null ? Res.canteen : 0f,
                IsDone = () => Res != null && canteen0 - Res.canteen >= drinkL,
                CannotBeDone = () => Res == null || Res.infiniteCanteen || Res.canteen <= 0.0001f
            });
            Flow.Add(new TutorialStep
            {
                Id = "tap", Unlocks = TutorialFeature.Tap,
                IsDone = () => Res != null && Res.canteen >= Res.canteen_Max - 0.01f,
                CannotBeDone = () => Res == null || Res.cleanStorage <= 0.05f || FindAnyObjectByType<HubTap_Manager>() == null
            });
            Flow.Add(new TutorialStep
            {
                Id = "gatePour", Unlocks = TutorialFeature.Gates,
                IsDone = () => AnyHubBasin(BasinFullOrOpen),
                CannotBeDone = () => !AnyHubBasin(null) || CantMakeWater()
            });
            Flow.Add(new TutorialStep
            {
                Id = "valve",
                IsDone = () => AnyHubBasin(BasinOpen),
                CannotBeDone = () => !AnyHubBasin(null)
            });
            Flow.Add(new TutorialStep
            {
                Id = "maze", Unlocks = TutorialFeature.Maze,
                OnEnter = () => outsideTime = 0f,
                IsDone = () => outsideTime >= outsideS
            });
            Flow.Add(new TutorialStep
            {
                Id = "map", Unlocks = TutorialFeature.Map,
                IsDone = MapOpen,
                CannotBeDone = () => gameMenu == null && mapScreen == null
            });
            Flow.Add(new TutorialStep { Id = "return", IsDone = () => AtHub });
            Flow.Add(new TutorialStep
            {
                Id = "build", Unlocks = TutorialFeature.BuildSkills,
                IsDone = () => gameMenu != null && gameMenu.IsOpen && gameMenu.Memory.Current != MenuPage.Map,
                CannotBeDone = () => gameMenu == null || UpgradeManager.Instance == null
            });
        }

        /// <summary>A different objective line while a step waits on something (night, an empty basin ...). Null = the step's own.</summary>
        string ObjectiveVariant(TutorialStep step)
        {
            switch (step.Id)
            {
                case "pot": return Res != null && Res.rawBasin <= 0.0001f ? "pot.empty" : null;
                case "hud":
                    var stats = Player_Manager.instance != null ? Player_Manager.instance.playerStats_Manager : null;
                    return stats != null && stats.player_Hydration >= stats.player_MaxHydration - 0.01f ? "hud.full" : null;
                case "gatePour": return IsNight ? "gatePour.night" : null;
                case "valve": return IsNight ? "valve.night" : null;
                case "maze": return IsNight ? "maze.night" : AtHub ? "maze.go" : null;
            }
            return null;
        }

        // ================================================================== SIDE LESSONS

        void BuildLessons()
        {
            Lesson("dusk", () => Clock != null && !Clock.IsNight && Clock.SecondsLeftInPhase <= FirstWarning && Clock.SecondsLeftInPhase > 1f);
            Lesson("lockdown", () => Clock != null && Clock.IsNight);
            Lesson("dehydrated", () =>
            {
                var p = Player_Manager.instance;
                if (p == null || Hyd == null || Hyd.IsCollapsed()) return false;
                float at = balance != null ? balance.dehydratedAt : 29f;
                return p.playerStats_Manager.player_Hydration < at;
            }, () => Hyd != null && Hyd.IsDrinking);
            Lesson("sick", () => Hyd != null && Hyd.SickSecondsLeft > 0f);
            Lesson("room", () => InRoom() || Near<RoomNoteObject>() || Near<RoomWaterSource>());
            Lesson("roomDoor", NearLockedRoomDoor);
            Lesson("valveGrate", NearShutValve);
            Lesson("bank", () => bankCount > 0);
            Lesson("zone2", () => zone2Turns > 0, MapOpen);
            Lesson("calamity", () => WorldState.IsFinalStage && WorldState.StageIndex > 0);
            Lesson("stage", () => WorldState.StageIndex > 0);
            Lesson("weather.storm", () => lastEvent == GameEventType.Thunderstorm);
            Lesson("weather.heat", () => lastEvent == GameEventType.Heatwave);
            Lesson("weather.calm", () => lastEvent == GameEventType.Calm);
        }

        void Lesson(string id, Func<bool> trigger, Func<bool> done = null) =>
            Flow.AddLesson(new TutorialLesson { Id = id, Trigger = trigger, IsDone = done });

        // ================================================================== EVERY FRAME

        void Update()
        {
            HookValveGame();
            TrackFacts();

            bool cardVisible = CardCanShow();
            // Real time, but nothing moves on while the game is frozen (pause, collapse, start card).
            Flow.Tick(Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f, cardVisible);

            // Stuck on a step: K skips it.
            var kb = Keyboard.current;
            if (cardVisible && Flow.IsStuck && kb != null && kb[skipStepKey].wasPressedThisFrame) Flow.SkipStep();

            if (deathOpen && Game_Manager.instance != null &&
                Game_Manager.instance.currentGameState == Game_Manager.GameState.Exploring)
                MenuState.Enter(); // the panel needs the cursor; ask again if a state change took it

            RetryTargets();
            Refresh(cardVisible);
        }

        void TrackFacts()
        {
            var gm = Game_Manager.instance;
            bool controllable = gm != null && gm.CanControlPlayer() && !LoadingScreen.IsLoading &&
                                !HowToPlay.IsOpen && !MainMenu.IsFrozen;
            controllableTime = controllable ? controllableTime + Time.unscaledDeltaTime : 0f;

            if (Time.timeScale > 0f && Player_Manager.instance != null && !AtHub && Flow.Current != null && Flow.Current.Id == "maze")
                outsideTime += Time.deltaTime;

            if (guideOverrideLeft > 0f)
            {
                guideOverrideLeft -= Time.unscaledDeltaTime;
                if (guideOverrideLeft <= 0f) guideOverride = null;
            }
        }

        /// <summary>The card and objective can be seen: normal play or a menu (the map), nothing in front.</summary>
        bool CardCanShow()
        {
            var gm = Game_Manager.instance;
            if (gm == null || LoadingScreen.IsLoading || HowToPlay.IsOpen || MainMenu.IsFrozen || deathOpen) return false;
            return gm.currentGameState == Game_Manager.GameState.Exploring || gm.currentGameState == Game_Manager.GameState.Menu;
        }

        // ------------------------------------------------------------------ Screen

        void Refresh(bool cardVisible)
        {
            if (view == null) return;
            var step = Flow.Current;
            var lesson = Flow.CurrentLesson;
            var gm = Game_Manager.instance;

            // Objective line.
            string objId = null;
            if (step != null && cardVisible)
                objId = ObjectiveVariant(step) ?? step.Id;
            SetObjective(objId);

            // Done tick.
            SetActive(view.doneBadge, cardVisible && Flow.ShowingDone);

            // Card: a side lesson takes it; otherwise the main step's card.
            string cardId = null;
            if (cardVisible)
            {
                if (lesson != null) cardId = lesson.Id;
                else if (step != null && !Flow.ShowingDone) cardId = step.Id;
            }
            SetCard(cardId);
            if (view.stuckText != null)
            {
                bool stuck = cardVisible && lesson == null && Flow.IsStuck;
                SetActive(view.stuckText.gameObject, stuck);
            }

            // Guide under the guided first valve minigame.
            string guideId = null;
            if (ValveTimingGame.IsPlaying && step != null && step.Id == "valve" && !deathOpen)
                guideId = guideOverride ?? "valve.play";
            SetGuide(guideId);

            // Skip tutorial while the pause menu is open.
            if (view.skipButton != null)
                SetActive(view.skipButton.gameObject, Flow.State == TutorialFlow.Mode.Running && gm != null &&
                                                     gm.currentGameState == Game_Manager.GameState.Paused);
        }

        void SetObjective(string id)
        {
            if (id == shownObjective) return;
            shownObjective = id;
            string text = id != null ? Format(texts.Get(id).objective) : null;
            SetActive(view.objectivePanel, !string.IsNullOrEmpty(text));
            if (view.objectiveText != null) view.objectiveText.text = text ?? "";
        }

        void SetCard(string id)
        {
            if (id == shownCard) return;
            shownCard = id;
            string text = id != null ? Format(texts.Get(id).card) : null;
            SetActive(view.cardPanel, !string.IsNullOrEmpty(text));
            if (view.cardText != null) view.cardText.text = text ?? "";
        }

        void SetGuide(string id)
        {
            if (id == shownGuide) return;
            shownGuide = id;
            string text = id != null ? Format(texts.Get(id).card) : null;
            SetActive(view.guidePanel, !string.IsNullOrEmpty(text));
            if (view.guideText != null) view.guideText.text = text ?? "";
        }

        void ApplyFixedWords()
        {
            if (view.cardLabel != null) view.cardLabel.text = texts.cardLabel;
            if (view.doneText != null) view.doneText.text = texts.doneWord;
            if (view.stuckText != null) view.stuckText.text = texts.stuckHint.Replace("{key}", skipStepKey.ToString());
            if (view.skipButtonLabel != null) view.skipButtonLabel.text = texts.skipButtonLabel;
            if (view.deathTitle != null) view.deathTitle.text = texts.deathTitle;
            if (view.lostHeading != null) view.lostHeading.text = texts.deathLostHeading;
            if (view.keptHeading != null) view.keptHeading.text = texts.deathKeptHeading;
            if (view.nextHeading != null) view.nextHeading.text = texts.deathNextHeading;
            if (view.deathContinueLabel != null) view.deathContinueLabel.text = texts.deathContinueLabel;
        }

        void HideAll()
        {
            SetActive(view.objectivePanel, false);
            SetActive(view.cardPanel, false);
            SetActive(view.doneBadge, false);
            SetActive(view.guidePanel, false);
            if (view.stuckText != null) SetActive(view.stuckText.gameObject, false);
            if (view.skipButton != null) SetActive(view.skipButton.gameObject, false);
            SetActive(view.deathPanel, false);
            shownObjective = shownCard = shownGuide = null;
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        // ------------------------------------------------------------------ Flow events

        void OnStepStarted(TutorialStep step)
        {
            targetRetry = 0f;
            CollectTargets(step.Id);
            shownCard = shownObjective = "\u0000"; // force a redraw
        }

        void OnStepCompleted(TutorialStep step)
        {
            outline.Clear();
            targets.Clear();
            hudTargets.Clear();
            if (step.Id == "return") Flow.MarkSeen("bank"); // the step just taught it
        }

        void OnFinished()
        {
            outline.Clear();
            SkipNextNewGame = true;
            HideAll();
        }

        void OnLessonStarted(TutorialLesson lesson)
        {
            if (lesson.Id == "calamity") Flow.MarkSeen("stage"); // Calamity already says the drought got worse
            if (lesson.Id == "lockdown") Flow.MarkSeen("dusk");  // too late for the warning
            shownCard = "\u0000";
        }

        /// <summary>Skip tutorial (pause menu button): everything unlocks, nothing more shows, and the next new game skips it too.</summary>
        public void SkipTutorial()
        {
            Flow.SkipAll();
            SkipNextNewGame = true;
            outline.Clear();
            HideAll();
            SimpleHud.Toast("Tutorial skipped. Everything is unlocked.", 3f);
        }

        // ------------------------------------------------------------------ Outline targets

        // Objects built after the step starts (hub placeholders, gates) are picked up by retrying
        // once a second, only while a step that wants targets has none yet.
        void RetryTargets()
        {
            var step = Flow.Current;
            if (step == null || Flow.ShowingDone || targets.Count > 0 || hudTargets.Count > 0) return;
            targetRetry -= Time.unscaledDeltaTime;
            if (targetRetry > 0f) return;
            targetRetry = 1f;
            CollectTargets(step.Id);
        }

        void CollectTargets(string id)
        {
            targets.Clear();
            hudTargets.Clear();
            switch (id)
            {
                case "pump":
                    foreach (var i in Interactable_Manager.allInteractables)
                        if (i is HubPump_Manager && i != null) targets.Add(i.gameObject);
                    break;
                case "rawBasin":
                    AddPlaceholder("RawBasin");
                    break;
                case "pot":
                    var pot = FindAnyObjectByType<HubPurifier_Manager>();
                    if (pot != null) targets.Add(pot.gameObject);
                    break;
                case "storage":
                case "tap":
                    var tap = FindAnyObjectByType<HubTap_Manager>();
                    if (tap != null) targets.Add(tap.gameObject); // the storage tiers sit on the tap object
                    break;
                case "hud":
                    foreach (var bar in FindObjectsByType<UI_StatBar>(FindObjectsSortMode.None))
                    {
                        var canvas = bar.GetComponentInParent<Canvas>();
                        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                            hudTargets.Add((RectTransform)bar.transform);
                    }
                    break;
                case "gatePour":
                case "valve":
                    foreach (var i in Interactable_Manager.allInteractables)
                    {
                        if (!(i is LockBasin_Manager basin) || basin == null || !IsHubBasin(basin)) continue;
                        if (id == "valve" && !(basin.Lock != null && basin.Lock.State != GateLockState.Empty &&
                                               basin.Lock.State != GateLockState.Filling)) continue;
                        targets.Add(basin.gameObject);
                        if (basin.gateObject != null) targets.Add(basin.gateObject);
                        foreach (var piece in basin.gateObjects) if (piece != null) targets.Add(piece);
                    }
                    break;
            }
            outline.SetTargets(targets);
            outline.SetHudTargets(hudTargets);
        }

        void AddPlaceholder(string child)
        {
            var hub = HubSceneObjects.Instance;
            var t = hub != null ? hub.transform.Find(child) : null;
            if (t != null) targets.Add(t.gameObject);
        }

        // ------------------------------------------------------------------ Hooks from other systems

        void OnZone2Turned(int from, int to) => zone2Turns++;

        void OnBanked(float carried, float bonus) => bankCount++;

        // Events that aren't built yet run as Calm (R10.5), so they teach the Calm lesson.
        void OnEventStarted(GameEventType type) =>
            lastEvent = type == GameEventType.Thunderstorm || type == GameEventType.Heatwave ? type : GameEventType.Calm;

        // Build and Skills open at the first dawn, even if the tutorial hasn't got there.
        void OnNewDay(int day)
        {
            if (Flow.State == TutorialFlow.Mode.Running) Flow.Unlock(TutorialFeature.BuildSkills);
        }

        void OnValveHit()
        {
            guideOverride = "valve.hit";
            guideOverrideLeft = 1f;
        }

        void OnValveMiss()
        {
            guideOverride = "valve.miss";
            guideOverrideLeft = 1.8f;
        }

        void ShowBlocked(TutorialFeature feature)
        {
            string what = feature == TutorialFeature.Map ? "The map" : "Build and Skills";
            SimpleHud.Toast(texts.lockedToast.Replace("{what}", what), 2.5f);
        }

        // The guided easy minigame: any hub gate during the gate steps.
        bool IsEasyMinigame(LockBasin_Manager basin)
        {
            var step = Flow.Current;
            return step != null && (step.Id == "valve" || step.Id == "gatePour") && IsHubBasin(basin);
        }

        // ------------------------------------------------------------------ First collapse screen

        void OnWokeUp(CollapseReport report)
        {
            if (Flow.State == TutorialFlow.Mode.Off || Flow.HasSeen("death") || view.deathPanel == null) return;
            Flow.MarkSeen("death");

            RefreshTokens();
            tokens["lostCanteen"] = F(report.lostCanteen, "0.0");
            tokens["lostUnbanked"] = F(report.lostUnbanked, "0.0");
            tokens["clockLost"] = F(report.clockSeconds, "0");
            tokens["storageLeft"] = F(report.cleanStorageLeft, "0.0");
            tokens["wakeLitres"] = F(report.wakeLitres, "0.#");
            tokens["banked"] = F(report.banked, "0.0");

            string lost = Replace(texts.Get("death.lost").card);
            if (report.keptCanteen) lost = lost.Replace("Your canteen water", "Canteen kept (Second Wind)");
            if (view.lostText != null) view.lostText.text = lost;
            if (view.keptText != null) view.keptText.text = Replace(texts.Get("death.kept").card);
            if (view.nextText != null) view.nextText.text = Replace(texts.Get("death.next").card);

            deathOpen = true;
            view.deathPanel.SetActive(true);
            MenuState.Enter();
        }

        /// <summary>The first collapse screen's Continue button.</summary>
        public void CloseDeathScreen()
        {
            if (!deathOpen) return;
            deathOpen = false;
            SetActive(view.deathPanel, false);
            MenuState.Exit();
        }

        // ------------------------------------------------------------------ Words

        string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            RefreshTokens();
            return Replace(text);
        }

        string Replace(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var kv in tokens) text = text.Replace("{" + kv.Key + "}", kv.Value);
            return text;
        }

        /// <summary>The {tokens} the words may use, filled from GameBalance and the game right now.</summary>
        void RefreshTokens()
        {
            var b = balance;
            if (b == null) return;
            tokens["pumpStroke"] = F(b.pumpLitersPerStroke, "0.##");
            tokens["boilRate"] = F(b.boilRate, "0.##");
            tokens["hubLock"] = F(b.GetLockLiters(0), "0.#");
            tokens["canteenMax"] = F(Res != null ? Res.canteen_Max : b.startCanteen, "0.#");
            tokens["hits"] = b.valveHitsToWin.ToString(CultureInfo.InvariantCulture);
            tokens["tileReward"] = F(b.tileReward, "0.##");
            tokens["sectionReward"] = RangeText(b.sectionReward, Mathf.Max(1, b.zoneCount));
            tokens["milestones"] = ListText(b.milestoneRewards);
            tokens["markers"] = F(Skills.F("maxMarkers", b.markerLimit), "0");
            tokens["safeBonus"] = F(b.safeReturnBonus * 100f, "0");
            tokens["lockdownDrain"] = F(b.lockdownDrain, "0.#");
            tokens["nightSeconds"] = F(b.nightLockdownSeconds, "0");
            tokens["sickMult"] = F(b.sickMult, "0.#");
            tokens["sickSeconds"] = F(Hyd != null ? Hyd.SickDuration : b.sickSeconds, "0");
            tokens["failHyd"] = F(b.minigameFailHyd, "0");
            tokens["zone2Days"] = b.zone2DaysBetweenTurns.ToString(CultureInfo.InvariantCulture);
            var stage = WorldState.Stage;
            tokens["stageName"] = stage != null ? stage.displayName : "";
            tokens["heatMult"] = F(WorldState.IsFinalStage ? b.heatwaveMultStage4 : b.heatwaveMult, "0.#");
            tokens["gameOverLitres"] = F(b.gameOverBelowLiters, "0.#");
            tokens["key"] = skipStepKey.ToString();
        }

        static string F(float v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        static string RangeText(float[] values, int count)
        {
            if (values == null || values.Length == 0) return "";
            count = Mathf.Clamp(count, 1, values.Length);
            float lo = values[0], hi = values[0];
            for (int i = 1; i < count; i++) { lo = Mathf.Min(lo, values[i]); hi = Mathf.Max(hi, values[i]); }
            return Mathf.Approximately(lo, hi) ? F(lo, "0.#") : F(lo, "0.#") + " to " + F(hi, "0.#");
        }

        static string ListText(float[] values)
        {
            if (values == null || values.Length == 0) return "";
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = F(values[i], "0.#");
            return string.Join(" / ", parts);
        }

        // ------------------------------------------------------------------ Saving (ISaveable)

        /// <summary>After the menu memory, so the Tab menu is in place first.</summary>
        public int RestoreOrder => RestoreOrders.Menu + 1;

        /// <summary>Writes the step the player is on and the lessons already shown.</summary>
        public void Capture(SaveData data)
        {
            var s = Flow.Capture();
            data.hasTutorial = true;
            data.tutorialVersion = SaveData.TutorialVersion;
            data.tutorialMode = (int)s.mode;
            data.tutorialStep = s.stepId;
            data.tutorialUnlocks = s.extraUnlocks;
            data.hintsSeen = s.seen;
        }

        /// <summary>Puts the tutorial back where the save says. Saves from before the guided tutorial: off.</summary>
        public void Restore(SaveData data)
        {
            CloseDeathScreen();
            outline.Clear();
            HideAll();
            if (data == null || !data.hasTutorial || data.tutorialVersion < SaveData.TutorialVersion)
            {
                Flow.Restore(null);
                return;
            }
            Flow.Restore(new TutorialSaveState
            {
                mode = (TutorialFlow.Mode)data.tutorialMode,
                stepId = data.tutorialStep ?? "",
                extraUnlocks = data.tutorialUnlocks,
                seen = data.hintsSeen ?? new List<string>()
            });
        }

        // ================================================================== Helpers

        static Player_Manager Player => Player_Manager.instance;
        static PlayerHydration_Manager Hyd => Player != null ? Player.playerHydration_Manager : null;
        static GameResources_Manager Res => GameResources_Manager.instance;
        static GameClockLogic Clock => GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Clock : null;
        static bool IsNight => GameClock_Manager.Instance != null && GameClock_Manager.Instance.IsNight;
        static Vector3 PlayerPos => Player != null ? Player.transform.position : Vector3.zero;
        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
        static bool PumpDry => HubPump_Manager.CurrentFactor <= 0.001f;

        bool AtHub
        {
            get
            {
                if (UpgradeManager.Instance != null && maze != null) return UpgradeManager.Instance.PlayerAtHub;
                return Hyd != null && Hyd.IsInsideHub();
            }
        }

        float FirstWarning
        {
            get
            {
                float first = 0f;
                if (balance != null && balance.lockdownWarnings != null)
                    foreach (var w in balance.lockdownWarnings) first = Mathf.Max(first, w);
                return first;
            }
        }

        bool MapOpen() =>
            (mapScreen != null && mapScreen.IsOpen) ||
            (gameMenu != null && gameMenu.IsOpen && gameMenu.Memory.Current == MenuPage.Map);

        /// <summary>True if this lock basin is one of the hub's gates (inside the hub's rings).</summary>
        bool IsHubBasin(LockBasin_Manager basin)
        {
            if (basin == null) return false;
            if (maze == null || maze.Grid == null || balance == null) return true; // old scenes: every basin counts
            return maze.Grid.Ring(maze.WorldToGrid(basin.transform.position)) <= balance.hubMaxRing;
        }

        bool AnyHubBasin(Func<LockBasin_Manager, bool> test)
        {
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is LockBasin_Manager b && b != null && IsHubBasin(b) && (test == null || test(b)))
                    return true;
            return false;
        }

        static bool BasinFullOrOpen(LockBasin_Manager b) =>
            b.IsPassable || (b.Lock != null && b.Lock.State != GateLockState.Empty && b.Lock.State != GateLockState.Filling);

        static bool BasinOpen(LockBasin_Manager b) => b.Lock != null && b.Lock.State == GateLockState.Open;

        // No water anywhere: nothing in the canteen or Clean Storage, and the pump is dry.
        static bool CantMakeWater() =>
            Res != null && Res.canteen <= 0.05f && Res.cleanStorage <= 0.05f && Res.rawBasin <= 0.05f && PumpDry;

        static bool AnyInteractable<T>() where T : Interactable_Manager
        {
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is T && i != null) return true;
            return false;
        }

        bool Near<T>() where T : Interactable_Manager
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is T && i != null && (i.GetInteractionPosition() - p.transform.position).sqrMagnitude <= r2)
                    return true;
            return false;
        }

        bool NearLockedRoomDoor()
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is RoomDoor d && d != null && !d.IsUnlocked &&
                    (d.GetInteractionPosition() - p.transform.position).sqrMagnitude <= r2)
                    return true;
            return false;
        }

        bool NearShutValve()
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is ValveGate v && v != null && !v.IsOpen &&
                    (v.GetInteractionPosition() - p.transform.position).sqrMagnitude <= r2)
                    return true;
            return false;
        }

        // In a room: sheltered (hub or room) but not at the hub.
        bool InRoom() => lockdown != null && Player != null && maze != null && maze.Grid != null && !AtHub &&
                         lockdown.IsSheltered(Player.transform.position);
    }
}
