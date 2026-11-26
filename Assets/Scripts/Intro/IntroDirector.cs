// ============================================================================
// IntroDirector.cs
// Plays the text-only intro (Intro scene) before a New Game:
//   - types each beat of the IntroScript asset line by line (about 35 characters a second,
//     a soft click every 2 characters), holds it, fades it out, then the next beat;
//   - [LOUDSPEAKER] lines are typed fast in the loudspeaker colour, without clicks;
//   - fires the beat's sound cues (one-shots, loops fading in and out) on IntroAudio;
//   - ends on the SALOK title card, then lets the game scene in;
//   - Enter or Esc held for Skip Hold Seconds skips straight to the game ("Hold Enter to
//     skip" fades in at the bottom right after a few seconds);
//   - pauses (text, timers and sound) while the window has no focus.
// The game scene is loaded in the background from the start (allowSceneActivation = false)
// at low priority, so the typing stays smooth on the web build. If it is not ready when the
// intro ends, the normal loading screen shows until it is.
//
// The typing never allocates per frame: each beat's text is set once and revealed with
// maxVisibleCharacters.
//
// DESIGN RULES: none (intro).
//
// HOW TO USE: on the "Intro" object in Assets/Scenes/Intro.unity (Salok > Intro > Build Intro
// Scene makes the scene). Everything you see is in that scene's Canvas: change fonts, sizes,
// colours and positions there.
// HOW TO EDIT: words, sounds and timings: the IntroScript asset. Skip timing: fields below.
// See Docs/Agents/Intro_Cutscene.md.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Types the intro beats, plays their sounds, shows the title card and lets the game scene in.</summary>
    public class IntroDirector : MonoBehaviour
    {
        /// <summary>Where the default script asset lives (used when the field is empty).</summary>
        public const string ScriptResource = "Intro/IntroScript";

        [Header("Script")]
        [Tooltip("The words, sound cues and timings. Empty = Resources/Intro/IntroScript.")]
        public IntroScript script;

        [Header("Screen (in this scene's Canvas)")]
        [Tooltip("Shows the beat being typed.")]
        public TMP_Text beatText;
        [Tooltip("Fades the beat text in and out. Empty = one is added to the text.")]
        public CanvasGroup beatGroup;
        [Tooltip("The SALOK title card (fades in at the end).")]
        public CanvasGroup titleCard;
        [Tooltip("\"Hold Enter to skip\" (fades in after Hint Delay).")]
        public CanvasGroup skipHint;
        [Tooltip("Optional: a Filled image that fills while Enter / Esc is held.")]
        public Image skipFill;
        [Tooltip("Colour of [LOUDSPEAKER] lines.")]
        public Color loudspeakerColor = new Color32(0x3B, 0x7A, 0x8C, 0xFF);

        [Header("Sound")]
        [Tooltip("Clicks, one-shots and loops.")]
        public IntroAudio introAudio;

        [Header("Skip")]
        [Tooltip("Seconds Enter or Esc must be held to skip.")]
        [Min(0.1f)] public float skipHoldSeconds = 0.8f;
        [Tooltip("Seconds before the skip hint fades in.")]
        [Min(0f)] public float hintDelay = 3f;
        [Tooltip("Seconds the skip hint takes to fade in.")]
        [Min(0f)] public float hintFade = 0.5f;

        [Header("Focus")]
        [Tooltip("Pause the intro (text, timers, sound) while the window has no focus.")]
        public bool pauseOnFocusLoss = true;

        AsyncOperation gameLoad;
        bool leaving, paused;
        float hintTimer, skipHeld;
        string loudHex;
        readonly StringBuilder builder = new StringBuilder(512);
        readonly List<int> lineEnds = new List<int>(16);       // visible-character index where each line ends
        readonly List<PendingCue> pending = new List<PendingCue>(8);
        ThreadPriority oldPriority;

        struct PendingCue { public IntroCue cue; public float wait; }

        /// <summary>Real seconds since last frame, 0 while paused (clamped so a hitch doesn't skip text).</summary>
        float Dt => paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        void Start()
        {
            Time.timeScale = 1f;
            if (script == null) script = Resources.Load<IntroScript>(ScriptResource);
            loudHex = ColorUtility.ToHtmlStringRGB(loudspeakerColor);
            if (beatText != null && beatGroup == null) beatGroup = beatText.gameObject.AddComponent<CanvasGroup>();
            SetAlpha(beatGroup, 0f);
            SetAlpha(titleCard, 0f);
            SetAlpha(skipHint, 0f);
            if (skipFill != null) skipFill.fillAmount = 0f;
            if (beatText != null) { beatText.text = ""; beatText.maxVisibleCharacters = 0; }

            // The game scene loads behind the intro, gently, so typing stays smooth (web build).
            oldPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            gameLoad = SceneManager.LoadSceneAsync(SceneFlow.GameScene);
            if (gameLoad != null) gameLoad.allowSceneActivation = false;
            else Debug.LogError($"[IntroDirector] Can't load \"{SceneFlow.GameScene}\". Is it in the Build Profiles scene list?", this);

            if (script == null)
            {
                Debug.LogWarning("[IntroDirector] No IntroScript asset: going straight to the game.", this);
                Leave(false);
                return;
            }
            StartCoroutine(Play());
        }

        void OnDestroy()
        {
            Application.backgroundLoadingPriority = oldPriority;
            if (paused) AudioListener.pause = false;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!pauseOnFocusLoss || leaving) return;
            paused = !hasFocus;
            AudioListener.pause = paused;
        }

        // ------------------------------------------------------------------ The script

        IEnumerator Play()
        {
            string playerName = PlayerProfile.Name;
            for (int b = 0; b < script.beats.Count; b++)
            {
                var beat = script.beats[b];
                if (beat == null) continue;
                var lines = IntroScript.SplitLines(beat.text, playerName);
                if (lines.Count == 0) { FireCues(beat, 0, 0); continue; }
                Layout(lines);
                SetAlpha(beatGroup, 1f);

                for (int i = 0; i < lines.Count; i++)
                {
                    FireCues(beat, i, lines.Count);
                    if (i > 0) yield return Wait(beat.pauseBetweenLines);
                    bool loud = lines[i].loudspeaker;
                    float cps = (loud ? script.loudspeakerCharsPerSecond : script.charsPerSecond) * beat.speedScale;
                    yield return TypeLine(i, cps, clicks: !loud);
                }
                FireCues(beat, lines.Count, lines.Count);

                yield return Wait(script.holdAfterBeat);
                yield return Fade(beatGroup, 1f, 0f, script.fadeOutSeconds);
                if (beatText != null) beatText.maxVisibleCharacters = 0;
                yield return Wait(beat.blackAfter);
            }

            yield return Fade(titleCard, 0f, 1f, script.titleFadeIn);
            yield return Wait(script.titleHold);
            Leave(false);
        }

        // Sets the whole beat once (loudspeaker lines coloured) and finds where each line ends.
        void Layout(List<IntroScript.Line> lines)
        {
            if (beatText == null) return;
            builder.Length = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                if (lines[i].loudspeaker) builder.Append("<color=#").Append(loudHex).Append('>');
                builder.Append(lines[i].text);
                if (lines[i].loudspeaker) builder.Append("</color>");
            }
            beatText.text = builder.ToString();
            beatText.maxVisibleCharacters = 0;
            beatText.ForceMeshUpdate();

            // Line ends in visible characters (tags don't count; each '\n' is one character).
            lineEnds.Clear();
            var info = beatText.textInfo;
            for (int c = 0; c < info.characterCount; c++)
                if (info.characterInfo[c].character == '\n') lineEnds.Add(c);
            lineEnds.Add(info.characterCount);
        }

        IEnumerator TypeLine(int line, float cps, bool clicks)
        {
            if (beatText == null || line >= lineEnds.Count) yield break;
            int start = line == 0 ? 0 : lineEnds[line - 1] + 1;  // skip the '\n'
            int end = lineEnds[line];
            var info = beatText.textInfo;
            float shown = 0f;
            int visible = start, typed = 0;
            beatText.maxVisibleCharacters = start;
            while (visible < end)
            {
                if (leaving) yield break;
                shown += cps * Dt;
                int targetCount = Mathf.Min(end, start + (int)shown);
                while (visible < targetCount)
                {
                    char ch = info.characterInfo[visible].character;
                    visible++;
                    if (clicks && !char.IsWhiteSpace(ch) && ++typed % script.charsPerClick == 0 && introAudio != null)
                        introAudio.Click(script.clickClip, script.clickVolume, script.clickPitch);
                }
                beatText.maxVisibleCharacters = visible;
                yield return null;
            }
        }

        // Fires the cues set for just before line <paramref name="line"/>. At the beat's end
        // (line == lineCount) it also fires every cue numbered past the last line.
        void FireCues(IntroBeat beat, int line, int lineCount)
        {
            for (int i = 0; i < beat.cues.Count; i++)
            {
                var cue = beat.cues[i];
                if (cue == null) continue;
                bool now = line < lineCount ? cue.beforeLine == line : cue.beforeLine >= lineCount;
                if (!now) continue;
                if (cue.delay > 0f) pending.Add(new PendingCue { cue = cue, wait = cue.delay });
                else Run(cue);
            }
        }

        void Run(IntroCue cue)
        {
            if (introAudio == null || cue == null) return;
            switch (cue.action)
            {
                case IntroCue.Action.OneShot: introAudio.OneShot(cue.clip, cue.volume); break;
                case IntroCue.Action.StartLoop: introAudio.StartLoop(cue.clip, cue.volume, cue.fadeSeconds); break;
                case IntroCue.Action.StopLoop: introAudio.StopLoop(cue.clip, cue.fadeSeconds); break;
                case IntroCue.Action.StopAllLoops: introAudio.StopAllLoops(cue.fadeSeconds); break;
            }
        }

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            float dt = Dt;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                p.wait -= dt;
                if (p.wait > 0f) { pending[i] = p; continue; }
                pending.RemoveAt(i);
                if (!leaving) Run(p.cue);
            }

            if (leaving) return;

            hintTimer += dt;
            if (skipHint != null && hintTimer > hintDelay)
                skipHint.alpha = hintFade > 0f ? Mathf.Clamp01((hintTimer - hintDelay) / hintFade) : 1f;

            var kb = Keyboard.current;
            bool held = !paused && kb != null &&
                        (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed || kb.escapeKey.isPressed);
            skipHeld = held ? skipHeld + dt : 0f;
            if (skipFill != null) skipFill.fillAmount = Mathf.Clamp01(skipHeld / skipHoldSeconds);
            if (skipHeld >= skipHoldSeconds) Leave(true);
        }

        // ------------------------------------------------------------------ Leaving

        /// <summary>Ends the intro and lets the game scene in (the loading screen shows if it isn't ready).</summary>
        public void Leave(bool skipped)
        {
            if (leaving) return;
            leaving = true;
            StopAllCoroutines();
            if (paused) { paused = false; AudioListener.pause = false; }
            SetAlpha(skipHint, 0f);
            Application.backgroundLoadingPriority = oldPriority;

            // The loops still playing (wind, hub day) carry into the game scene and fade out there,
            // under the game's own ambience. Skipped: a shorter fade.
            float fade = script != null ? script.carryOverFade : 1f;
            if (introAudio != null) introAudio.CarryOver(skipped ? Mathf.Min(fade, 1f) : fade);

            Time.timeScale = 1f;
            if (gameLoad == null) { LoadingScreen.Load(SceneFlow.GameScene); return; }
            if (gameLoad.progress >= 0.9f) gameLoad.allowSceneActivation = true;
            else LoadingScreen.ShowWhileLoading(gameLoad, SceneFlow.GameScene);
        }

        // ------------------------------------------------------------------ Helpers

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Dt; yield return null; }
        }

        IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
        {
            if (g == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            g.alpha = to;
        }

        static void SetAlpha(CanvasGroup g, float a)
        {
            if (g != null) g.alpha = a;
        }
    }
}
