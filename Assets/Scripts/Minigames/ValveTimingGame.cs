// ValveTimingGame.cs
// The Valve Timing minigame (GDD Rules section 9, R9.1-R9.5). Logic only: the designer builds
// the uGUI panel and drags its pieces into the slots below.
//
// How it plays: a needle swings back and forth across a half circle. A green band sits
// somewhere on the dial. Press Space while the needle is inside the band. Enough hits wins,
// enough misses loses, Esc quits with no penalty. Numbers come from GameBalance.valveTiming
// ([0] zone 1, [1] zone 2, [2] zone 3, [3] exit valve) and GameBalance.valveBandMinMove.
//
// Setup:
//   Put this script on an object that is ALWAYS active (e.g. a "Minigames" object or the
//   Canvas), NOT on the panel itself: the panel is switched off between games.
//   There is one of these in the scene; every ValveGate uses it through ValveTimingGame.Instance.
//
// Angles: 0 = straight up, -90 = far left, +90 = far right (degrees, clockwise positive).

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DryLabyrinth.Data;

namespace DryLabyrinth.Minigames
{
    /// <summary>Which row of GameBalance.valveTiming a gate uses.</summary>
    public enum ValveLocation { Zone1 = 0, Zone2 = 1, Zone3 = 2, ExitValve = 3 }

    public enum MinigameResult { Won, Lost, Quit }

    public class ValveTimingGame : MonoBehaviour
    {
        public static ValveTimingGame Instance { get; private set; }

        /// <summary>True while a valve game is on screen. The player controller should ignore
        /// movement/jump input and the pause menu should ignore Esc while this is true.</summary>
        public static bool IsPlaying => Instance != null && Instance.playing;

        const float DialHalf = 90f;   // the dial runs from -90 to +90

        [Header("Data")]
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

        public Color pipOffColor = new Color(1f, 1f, 1f, 0.2f);
        public Color hitPipColor = new Color(0.30f, 0.85f, 0.45f);
        public Color missPipColor = new Color(0.90f, 0.25f, 0.20f);

        [Header("Optional audio")]
        public AudioSource audioSource;
        public AudioClip hitClip;    // clunk
        public AudioClip missClip;   // hiss
        public AudioClip winClip;
        public AudioClip loseClip;

        [Header("Feel")]
        [Tooltip("Seconds the result stays on screen before the panel closes.")]
        [Min(0f)] public float endDelay = 0.6f;
        [Min(0f)] public float flashSeconds = 0.3f;
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
            if (playing) return false;
            if (balance == null || balance.valveTiming == null || balance.valveTiming.Length == 0)
            {
                Debug.LogError("[ValveTimingGame] Assign a GameBalance asset with Valve Timing rows.", this);
                return false;
            }
            if (needle == null || band == null || panel == null)
            {
                Debug.LogError("[ValveTimingGame] Panel, Needle and Band must be assigned.", this);
                return false;
            }

            int row = Mathf.Clamp((int)location, 0, balance.valveTiming.Length - 1);
            settings = balance.valveTiming[row];
            this.onFinished = onFinished;

            hits = 0;
            misses = 0;
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

            if (ending)
            {
                endTimer -= dt;
                if (endTimer <= 0f) Close();
                return;
            }

            // Needle ping-pongs across the half circle.
            needleAngle += needleDir * settings.needleSpeed * dt;
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
                wheelTarget -= 360f / Mathf.Max(1, settings.hitsToWin);   // one notch clockwise
                Play(hitClip);
                if (hits >= settings.hitsToWin) { Finish(MinigameResult.Won, endDelay); return; }
                bandCentre = RandomBandCentre(bandCentre);
            }
            else
            {
                misses++;
                flashTimer = flashSeconds;
                Play(missClip);
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
