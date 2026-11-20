// ============================================================================
// CollapseFade.cs
// A full-screen fade to black and back, for when the player collapses (or any other
// "blackout" moment). Fades out, stays black for a moment (when you can move the player /
// change the scene), then fades back in.
//
// DESIGN RULES: none (feel only; the collapse rules stay in CollapseManager).
//
// HOW TO USE: the prefab Assets/VFX/Resources/Feel/CollapseFade.prefab is a Canvas with a
// black Image and a CanvasGroup. Put a copy in the scene, then from code:
//     collapseFade.Play(() => { /* runs while the screen is black */ });
// or FadeOut() / FadeIn() on their own (also usable from UnityEvents).
// HOW TO EDIT: colour / image in the prefab. Times in FeelSettings > Collapse fade.
// Uses real time, so it works when the game is paused.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Full-screen fade to black and back.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class CollapseFade : MonoBehaviour
    {
        CanvasGroup group;
        Coroutine running;

        /// <summary>True while a fade is playing.</summary>
        public bool IsPlaying => running != null;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>Fade out, hold, call <paramref name="whileBlack"/>, fade back in.</summary>
        public void Play(Action whileBlack = null)
        {
            var s = FeelSettings.Active;
            Run(Sequence(s.collapseFadeOut, s.collapseHold, s.collapseFadeIn, whileBlack));
        }

        /// <summary>For UnityEvents: the full fade with nothing in the middle.</summary>
        public void PlayFull() => Play(null);

        /// <summary>Fade to black and stay black.</summary>
        public void FadeOut() => Run(FadeTo(1f, FeelSettings.Active.collapseFadeOut));

        /// <summary>Fade from black back to the game.</summary>
        public void FadeIn() => Run(FadeTo(0f, FeelSettings.Active.collapseFadeIn));

        void Run(IEnumerator routine)
        {
            if (group == null) Awake();
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Wrap(routine));
        }

        IEnumerator Wrap(IEnumerator routine)
        {
            yield return routine;
            running = null;
        }

        IEnumerator Sequence(float outTime, float hold, float inTime, Action whileBlack)
        {
            yield return FadeTo(1f, outTime);
            whileBlack?.Invoke();
            float t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
            yield return FadeTo(0f, inTime);
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            group.blocksRaycasts = true; // no clicking through while fading
            float start = group.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            group.alpha = target;
            group.blocksRaycasts = target > 0.5f;
        }
    }
}
