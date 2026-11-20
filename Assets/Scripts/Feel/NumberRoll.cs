// ============================================================================
// NumberRoll.cs
// Makes a counter text roll up (or down) to its new value instead of jumping, e.g. the
// Clean Storage litres counting up after you bank water. Can bounce the text too (UiPunch).
//
// DESIGN RULES: none (feel only; it only changes what is shown, never the real value).
//
// HOW TO USE: add it next to a TextMeshPro text. From code call SetValue(newValue) when the
// number changes; SetInstant(value) jumps without rolling (e.g. when a save loads).
// HOW TO EDIT:
//   Format: how the number is written. "{0:1} L" = one decimal then " L"; "{0:0}" = whole
//           number. (TextMeshPro format: the digit after ":" is the number of decimals.)
//   Roll time is in FeelSettings > Number roll-up, unless Override is ticked.
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Rolls a TextMeshPro number to a new value over a short time.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class NumberRoll : MonoBehaviour
    {
        [Tooltip("How the number is written. {0:1} = the value with 1 decimal. Example: \"{0:1} L\".")]
        public string format = "{0:1} L";
        [Tooltip("Use the roll time below instead of FeelSettings.")]
        public bool overrideSettings;
        [Tooltip("Seconds to roll to the new value. Only used with Override.")]
        [Range(0f, 3f)] public float duration = 0.6f;
        [Tooltip("Bounce the text (needs a UiPunch on the same object) when the value goes up.")]
        public bool punchOnIncrease = true;
        [Tooltip("Bounce the text when the value goes down.")]
        public bool punchOnDecrease;

        TMP_Text text;
        UiPunch punch;
        float from, to, shown;
        float time = -1f;

        /// <summary>The value the counter is rolling towards.</summary>
        public float Target => to;

        /// <summary>The value currently shown.</summary>
        public float Shown => shown;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            punch = GetComponent<UiPunch>();
        }

        /// <summary>Rolls from what is shown now to <paramref name="value"/>.</summary>
        public void SetValue(float value)
        {
            if (text == null) Awake();
            if (Mathf.Approximately(value, to) && time < 0f) { SetInstant(value); return; }
            if (punch != null && ((value > to && punchOnIncrease) || (value < to && punchOnDecrease))) punch.Punch();
            from = shown;
            to = value;
            time = 0f;
        }

        /// <summary>Shows <paramref name="value"/> straight away with no roll.</summary>
        public void SetInstant(float value)
        {
            if (text == null) Awake();
            from = to = shown = value;
            time = -1f;
            Write();
        }

        void Update()
        {
            if (time < 0f) return;
            float length = overrideSettings ? duration : FeelSettings.Active.rollDuration;
            time += Time.unscaledDeltaTime;
            float t = length > 0f ? time / length : 1f;
            shown = FeelMath.Roll(from, to, t);
            if (t >= 1f)
            {
                shown = to;
                time = -1f;
            }
            Write();
        }

        // TextMeshPro's SetText with a number does not make garbage (safe every frame).
        void Write() => text.SetText(format, shown);
    }
}
