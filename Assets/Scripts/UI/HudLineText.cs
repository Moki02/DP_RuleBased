// ============================================================================
// HudLineText.cs
// Shows one SimpleHud status line in its own text, placed anywhere in the HUD, instead of in
// the dark status box under the clock. Example: the drought stage ("stage") sits on the
// clock's wooden plank, beside the day.
//
// DESIGN RULES: none of its own (R10.1 stage badge placement).
//
// HOW TO USE: add it to a TextMeshPro text in the HUD and type the status key (e.g. "stage",
// see the key list in SimpleHud.cs). While it is enabled, that line is drawn here and not
// in the status box. Disable or remove it and the line goes back to the box.
// HOW TO EDIT: the look is the text's own (font, size, auto size, colour, position).
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Draws one SimpleHud status line (by key) in this text instead of the status box.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class HudLineText : MonoBehaviour
    {
        [Tooltip("The SimpleHud status key this text shows, e.g. \"stage\".")]
        public string key = "stage";
        [Tooltip("Hide the text's object while the line is empty.")]
        public bool hideWhenEmpty = true;

        static readonly Dictionary<string, HudLineText> active = new Dictionary<string, HudLineText>();
        TMP_Text text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        void Awake() => text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            if (string.IsNullOrEmpty(key)) return;
            active[key] = this;
            // A line set before this text woke up moves over from the status box.
            Show(SimpleHud.TakeLine(key));
        }

        void OnDisable()
        {
            if (!string.IsNullOrEmpty(key) && active.TryGetValue(key, out var h) && h == this) active.Remove(key);
        }

        /// <summary>True (and shows it) if a HudLineText owns <paramref name="lineKey"/>.</summary>
        public static bool TryShow(string lineKey, string value)
        {
            if (lineKey == null || !active.TryGetValue(lineKey, out var h) || h == null) return false;
            h.Show(value);
            return true;
        }

        void Show(string value)
        {
            if (text == null) text = GetComponent<TMP_Text>();
            text.text = value ?? "";
            if (hideWhenEmpty) text.enabled = !string.IsNullOrEmpty(value);
        }
    }
}
