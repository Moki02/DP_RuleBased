// ============================================================================
// ValveTimingGame.cs
// The Valve Timing minigame (GDD Rules section 9, R9.1-R9.5). Logic only: the designer builds
// the uGUI panel and drags its pieces into the slots below.
//
// How it plays: a needle swings back and forth across a half circle. A green band sits
// somewhere on the dial. Press Space while the needle is inside the band. Enough hits wins,
// enough misses loses, Esc quits with no penalty. SPEED-UP: after the 1st and the 2nd green hit
// the needle gets faster (× GameBalance.valveSpeedUpMult each, for valveSpeedUps hits); misses
// never change it. Numbers come from GameBalance.valveTiming
// ([0] zone 1, [1] zone 2, [2] zone 3, [3] exit valve) and GameBalance.valveBandMinMove.
//
// Setup:
//   Put this script on an object that is ALWAYS active (e.g. a "Minigames" object or the
//   Canvas), NOT on the panel itself: the panel is switched off between games.
//   There is one of these in the scene; every ValveGate uses it through ValveTimingGame.Instance.
//
// Maze playtest: the maze gates call Begin(settings, ...) with settings from ValveDifficulty
// (GDD R9.6) instead of a fixed row. While a game is open and F1 (debug HUD) is on, a line
// at the top of the panel shows the difficulty t, band width and needle speed.
// Sound hooks: drag clips into Hit/Miss Clip, or add listeners to On Hit / On Miss.
//
// Angles: 0 = straight up, -90 = far left, +90 = far right (degrees, clockwise positive).
//
// DESIGN RULES: R9.1 (the world doesn't pause: it uses Time.deltaTime, and the Minigame
// state keeps time scale 1), R9.2 (Esc quits, no penalty), R9.3 (losing: the gate that
// started the game takes 5 HYD through SurvivalRules.MinigameLost), R9.4, R9.6 (difficulty).
//
// HOW TO USE: the Build Playtest Scene menu puts it on Systems and fills the slots. Gates call
// ValveTimingGame.Instance.Begin(...) and get Won / Lost / Quit back in their callback.
// HOW TO EDIT: band width, needle speed, hits and misses are in GameBalance (Valve Timing rows,
// Valve Band Min Move); the feel values (flash, wheel speed, end delay) are below.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using TMPro;

namespace TheLastDrop
{
    /// <summary>Which row of GameBalance.valveTiming a gate uses.</summary>
    public enum ValveLocation { Zone1 = 0, Zone2 = 1, Zone3 = 2, ExitValve = 3 }

    /// <summary>How a minigame ended: Won (gate opens), Lost (R9.3 penalty) or Quit (Esc, R9.2).</summary>
    public enum MinigameResult { Won, Lost, Quit }

    /// <summary>Runs the Valve Timing minigame on the panel the designer built.</summary>
    public class ValveTimingGame : MonoBehaviour
    {
        /// <summary>The one in this scene (null if there is none).</summary>
        public static ValveTimingGame Instance { get; private set; }

        /// <summary>True while a valve game is on screen. The player controller should ignore
        /// movement/jump input and the pause menu should ignore Esc while this is true.</summary>
        public static bool IsPlaying => Instance != null && Instance.playing;

        const float DialHalf = 90f;   // the dial runs from -90 to +90

        [Header("Data")]
        [Tooltip("Valve Timing rows and Valve Band Min Move. Empty = the game's current GameBalance (BalanceProvider).")]
        public GameBalance balance;

        [Header("UI you build (see setup notes)")]
        [Tooltip("The whole minigame panel. Switched on when a game starts, off when it ends.")]
        public GameObject panel;

        [Tooltip("Needle image. Pivot (0.5, 0) placed on the dial's centre point.")]
        public RectTransform needle;

        [Tooltip("Green band. A circle sprite (e.g. Knob), same centre as the needle pivot. " +
                 "The script sets it to Filled / Radial 360 / Top / Clockwise for you.")]
        public Image band;

        [Header("Optional UI")]
        [Tooltip("Valve wheel image. Turns one notch per hit; a full turn on the winning hit.")]
        public RectTransform valveWheel;

        [Tooltip("Full-panel red image with alpha 0 and Raycast Target off. Flashes on a miss.")]
        public Image missFlash;

        [Tooltip("One image per hit needed (make 4 for the exit valve; extras are hidden).")]
        public Image[] hitPips;

        [Tooltip("One image per allowed miss (make 3).")]
        public Image[] missPips;

        [Tooltip("Shows e.g. \"Hits 1/3   Misses 0/3\".")]
        public TMP_Text counterText;

        [Tooltip("Colour of a pip not earned yet.")] public Color pipOffColor = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Colour of an earned hit pip.")] public Color hitPipColor = new Color(0.30f, 0.85f, 0.45f);
        [Tooltip("Colour of a used miss pip.")] public Color missPipColor = new Color(0.90f, 0.25f, 0.20f);

        [Header("Optional audio")]
        [Tooltip("Plays the clips below. Empty = no sound.")] public AudioSource audioSource;
        [Tooltip("A hit (the clunk).")] public AudioClip hitClip;
        [Tooltip("A miss (the hiss).")] public AudioClip missClip;
        [Tooltip("The winning hit.")] public AudioClip winClip;
        [Tooltip("The losing miss.")] public AudioClip loseClip;

        [Header("Hooks (sounds, effects)")]
        [Tooltip("Called on every hit (the clunk). Add a sound or effect here.")]
        public UnityEvent onHit = new UnityEvent();
        [Tooltip("Called on every miss (the hiss). Add a sound or effect here.")]
        public UnityEvent onMiss = new UnityEvent();

        [Header("Debug (F1)")]
        [Tooltip("Text for the F1 debug line. Empty = one is made at the top of the panel.")]
        public TMP_Text debugText;

        [Header("Feel")]
        [Tooltip("Seconds the result stays on screen before the panel closes.")]
        [Min(0f)] public float endDelay = 0.6f;
        [Tooltip("Seconds the red miss flash takes to fade.")]
        [Min(0f)] public float flashSeconds = 0.3f;
        [Tooltip("How strong the red miss flash starts (0 = invisible, 1 = solid).")]
        [Range(0f, 1f)] public float flashAlpha = 0.5f;
        [Tooltip("How fast the valve wheel catches up to its target angle.")]
        [Min(0.1f)] public float wheelTurnSpeed = 12f;

        // ------------------------------------------------------------------ State
        bool playing;
        bool ending;
        float endTimer;
        MinigameResult pendingResult;
        Action<MinigameResult> onFinished;

        ValveTimingSettings settings;
        float needleAngle;
        float needleDir = 1f;
        float bandCentre;
        int hits, misses;
        float flashTimer;
        float wheelAngle, wheelTarget;
        int startFrame;
        float currentT = -1f;
        float needleSpeed;   // this game's speed right now: the settings' speed, raised by green hits

        /// <summary>The settings of the game being played (null before the first game).</summary>
        public ValveTimingSettings CurrentSettings => settings;

        /// <summary>Difficulty t of the game being played (0-1), or -1 if not known.</summary>
        public float CurrentT => currentT;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ValveTimingGame] More than one in the scene; this one is turned off.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Public API

        /// <summary>Starts a game. Returns false if one is already running or setup is missing.
        /// onFinished is called once with Won, Lost or Quit.</summary>
        public bool Begin(ValveLocation location, Action<MinigameResult> onFinished)
        {
            if (balance == null) balance = BalanceProvider.Current;
            if (balance == null || balance.valveTiming == null || balance.valveTiming.Length == 0)
            {
                Debug.LogError("[ValveTimingGame] Assign a GameBalance asset with Valve Timing rows.", this);
                return false;
            }
            int row = Mathf.Clamp((int)location, 0, balance.valveTiming.Length - 1);
            return Begin(balance.valveTiming[row], onFinished);
        }

        /// <summary>
        /// Starts a game with these exact settings (the maze gates use ValveDifficulty to make them,
        /// GDD R9.6). <paramref name="difficultyT"/> is only shown on the F1 debug line (-1 = not known).
        /// Returns false if a game is already running or setup is missing.
        /// </summary>
        public bool Begin(ValveTimingSettings gameSettings, Action<MinigameResult> onFinished, float difficultyT = -1f)
        {
            if (playing) return false;
            if (balance == null) balance = BalanceProvider.Current;   // slot left empty: use the game's GameBalance
            if (gameSettings == null || balance == null)
            {
                Debug.LogError("[ValveTimingGame] Missing settings or GameBalance.", this);
                return false;
            }
            if (needle == null || band == null || panel == null)
            {
                Debug.LogError("[ValveTimingGame] Panel, Needle and Band must be assigned.", this);
                return false;
            }

            settings = gameSettings;
            currentT = difficultyT;
            this.onFinished = onFinished;

            hits = 0;
            misses = 0;
            needleSpeed = settings.needleSpeed;
            ending = false;
            flashTimer = 0f;
            wheelAngle = wheelTarget = 0f;
            startFrame = Time.frameCount;

            // Needle starts at a random edge so the first swing is always a full one.
            needleDir = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            needleAngle = -needleDir * DialHalf;

            SetupBandImage();
            bandCentre = RandomBandCentre(float.NaN);

            panel.SetActive(true);
            playing = true;
            RefreshAll();
            return true;
        }

        /// <summary>Closes the game as a Quit (no penalty). Used by Esc and by a gate when the
        /// player walks away.</summary>
        public void Cancel()
        {
            if (!playing || ending) return;
            Finish(MinigameResult.Quit, 0f);
        }

        // ------------------------------------------------------------------ Loop

        void Update()
        {
            if (!playing) return;
            float dt = Time.deltaTime;   // the world doesn't pause (R9.1); the pause menu's timeScale does

            UpdateFeedback(dt);
            UpdateDebugLine();

            if (ending)
            {
                endTimer -= dt;
                if (endTimer <= 0f) Close();
                return;
            }

            // Needle ping-pongs across the half circle.
            needleAngle += needleDir * needleSpeed * dt;
            if (needleAngle > DialHalf) { needleAngle = 2f * DialHalf - needleAngle; needleDir = -1f; }
            if (needleAngle < -DialHalf) { needleAngle = -2f * DialHalf - needleAngle; needleDir = 1f; }
            needleAngle = Mathf.Clamp(needleAngle, -DialHalf, DialHalf);
            needle.localEulerAngles = new Vector3(0f, 0f, -needleAngle);

            var kb = Keyboard.current;
            if (kb == null || Time.frameCount == startFrame) return;   // ignore the frame the game opened

            if (kb.escapeKey.wasPressedThisFrame)   // R9.2
            {
                Finish(MinigameResult.Quit, 0f);
                return;
            }

            if (kb.spaceKey.wasPressedThisFrame) Press();
        }

        void Press()
        {
            bool inBand = Mathf.Abs(needleAngle - bandCentre) <= settings.bandWidth * 0.5f;

            if (inBand)
            {
                hits++;
                // Speed-up after each of the first few green hits (GameBalance.valveSpeedUps).
                needleSpeed = ValveDifficulty.SpeedAfterHits(settings.needleSpeed, hits,
                                                             balance != null ? balance.valveSpeedUpMult : 1f,
                                                             balance != null ? balance.valveSpeedUps : 0);
                wheelTarget -= 360f / Mathf.Max(1, settings.hitsToWin);   // one notch clockwise
                Play(hitClip);
                onHit.Invoke();
                if (hits >= settings.hitsToWin) { Finish(MinigameResult.Won, endDelay); return; }
                bandCentre = RandomBandCentre(bandCentre);
            }
            else
            {
                misses++;
                flashTimer = flashSeconds;
                Play(missClip);
                onMiss.Invoke();
                if (misses >= settings.missesToLose) { Finish(MinigameResult.Lost, endDelay); return; }
            }
            RefreshAll();
        }

        void Finish(MinigameResult result, float delay)
        {
            pendingResult = result;
            RefreshAll();
            if (result == MinigameResult.Won) Play(winClip);
            if (result == MinigameResult.Lost) Play(loseClip);

            if (delay <= 0f) { Close(); return; }
            ending = true;
            endTimer = delay;
        }

        void Close()
        {
            playing = false;
            ending = false;
            if (missFlash != null) SetAlpha(missFlash, 0f);
            panel.SetActive(false);

            var callback = onFinished;
            onFinished = null;
            callback?.Invoke(pendingResult);
        }

        // ------------------------------------------------------------------ Band

        float RandomBandCentre(float previous)
        {
            float half = settings.bandWidth * 0.5f;
            float min = -DialHalf + half, max = DialHalf - half;
            if (min >= max) return 0f;
            if (float.IsNaN(previous)) return UnityEngine.Random.Range(min, max);

            float minMove = balance.valveBandMinMove;
            for (int i = 0; i < 30; i++)
            {
                float c = UnityEngine.Random.Range(min, max);
                if (Mathf.Abs(c - previous) >= minMove) return c;
            }
            // Fallback: whichever end of the dial is farther away.
            return Mathf.Abs(min - previous) > Mathf.Abs(max - previous) ? min : max;
        }

        void SetupBandImage()
        {
            band.type = Image.Type.Filled;
            band.fillMethod = Image.FillMethod.Radial360;
            band.fillOrigin = (int)Image.Origin360.Top;
            band.fillClockwise = true;
        }

        // ------------------------------------------------------------------ Visuals

        void RefreshAll()
        {
            // Band: fill covers [start, start + width] clockwise from the top.
            band.fillAmount = settings.bandWidth / 360f;
            float start = bandCentre - settings.bandWidth * 0.5f;
            band.rectTransform.localEulerAngles = new Vector3(0f, 0f, -start);

            needle.localEulerAngles = new Vector3(0f, 0f, -needleAngle);

            SetPips(hitPips, settings.hitsToWin, hits, hitPipColor);
            SetPips(missPips, settings.missesToLose, misses, missPipColor);

            if (counterText != null)
                counterText.text = $"Hits {hits}/{settings.hitsToWin}   Misses {misses}/{settings.missesToLose}";
        }

        // F1 debug line: difficulty t, band width and needle speed of the game on screen.
        void UpdateDebugLine()
        {
            bool show = DebugKeys.ShowDebugInfo;
            if (debugText == null)
            {
                if (!show || panel == null) return;
                debugText = UiBuilder.Text("DebugLine", panel.transform, "", 22f, Color.yellow);
                UiBuilder.Place(debugText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                new Vector2(0f, -8f), new Vector2(900f, 34f));
                debugText.alignment = TextAlignmentOptions.Top;
            }
            debugText.gameObject.SetActive(show);
            if (!show || settings == null) return;
            // -1 = the gate passed a fixed row (exit valve or an old ValveGate), not a t from R9.6.
            string t = currentT < 0f ? "fixed row (no t)" : $"t {currentT:0.000}";
            debugText.text = $"[F1] {t}   band {settings.bandWidth:0.0}°   speed {needleSpeed:0.0}°/s (start {settings.needleSpeed:0.0})   " +
                             $"win {settings.hitsToWin} hits / lose {settings.missesToLose} misses";
        }

        void SetPips(Image[] pips, int needed, int filled, Color onColor)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].gameObject.SetActive(i < needed);
                pips[i].color = i < filled ? onColor : pipOffColor;
            }
        }

        void UpdateFeedback(float dt)
        {
            if (missFlash != null)
            {
                flashTimer = Mathf.Max(0f, flashTimer - dt);
                float t = flashSeconds > 0f ? flashTimer / flashSeconds : 0f;
                SetAlpha(missFlash, flashAlpha * t);
            }

            if (valveWheel != null)
            {
                wheelAngle = Mathf.Lerp(wheelAngle, wheelTarget, 1f - Mathf.Exp(-wheelTurnSpeed * dt));
                valveWheel.localEulerAngles = new Vector3(0f, 0f, wheelAngle);
            }
        }

        static void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
