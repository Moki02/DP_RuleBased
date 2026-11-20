// ============================================================================
// UiPunch.cs
// Makes a UI element (a number, a bar, an icon) bounce when something changes: it grows
// quickly, wobbles once and settles back to its normal size. Uses real time, so it still
// works while the game is paused or in hit-stop.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: add it to the UI object that should bounce, then call Punch() from code or
// from a UnityEvent / Button (it shows up in the event list). Punch(1.5f) bounces harder.
// HOW TO EDIT: size and length are in FeelSettings > UI punch. Tick "Override" to give
// this one object its own values.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Scale bounce for a UI element, played with Punch().</summary>
    [DisallowMultipleComponent]
    public class UiPunch : MonoBehaviour
    {
        [Tooltip("Use the numbers below instead of FeelSettings for this object.")]
        public bool overrideSettings;
        [Tooltip("How big it grows at the top of the bounce (1.2 = 20% bigger). Only used with Override.")]
        [Range(1f, 2f)] public float scale = 1.2f;
        [Tooltip("How long the bounce lasts in seconds. Only used with Override.")]
        [Range(0.05f, 1f)] public float duration = 0.28f;

        Vector3 baseScale;
        float time = -1f;
        float strength = 1f;
        bool hasBase;

        void Awake() => CaptureBase();

        void CaptureBase()
        {
            if (hasBase) return;
            baseScale = transform.localScale;
            hasBase = true;
        }

        /// <summary>Plays the bounce at normal strength.</summary>
        public void Punch() => Punch(1f);

        /// <summary>Plays the bounce; <paramref name="power"/> 2 = twice as much growth.</summary>
        public void Punch(float power)
        {
            CaptureBase();
            strength = Mathf.Max(0f, power);
            time = 0f;
            enabled = true;
        }

        void OnDisable()
        {
            if (hasBase) transform.localScale = baseScale;
            time = -1f;
        }

        void Update()
        {
            if (time < 0f) return;
            var s = FeelSettings.Active;
            float peak = overrideSettings ? scale : s.punchScale;
            float length = overrideSettings ? duration : s.punchDuration;

            time += Time.unscaledDeltaTime;
            float t = length > 0f ? time / length : 1f;
            float k = FeelMath.PunchScale(t, 1f + (peak - 1f) * strength);
            transform.localScale = baseScale * k;
            if (t >= 1f)
            {
                transform.localScale = baseScale;
                time = -1f;
            }
        }
    }
}
