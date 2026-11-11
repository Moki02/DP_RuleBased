// ============================================================================
// WorldLabel.cs
// A plain TextMeshPro text floating in the world (for example "Cistern 4.0 / 6.5 L" above a
// room's tank) that always turns to face the camera. No art, no effects.
//
// DESIGN RULES: none of its own (UI rule: all UI is plain TextMeshPro text).
//
// HOW TO USE: WorldLabel.Create(parent, localPosition, fontSize, colour), then label.Set("text").
// HOW TO EDIT: size and colour are passed in by whoever makes the label.
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>A world-space TextMeshPro text that faces the camera.</summary>
    public class WorldLabel : MonoBehaviour
    {
        /// <summary>The text component.</summary>
        public TextMeshPro Text { get; private set; }

        /// <summary>Makes a label under <paramref name="parent"/> at <paramref name="localPosition"/> (metres).</summary>
        public static WorldLabel Create(Transform parent, Vector3 localPosition, float fontSize, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<WorldLabel>();
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.2f;
            t.outlineColor = Color.black;
            t.rectTransform.sizeDelta = new Vector2(6f, 2f);
            label.Text = t;
            return label;
        }

        /// <summary>Changes the text (does nothing if it's the same).</summary>
        public void Set(string text)
        {
            if (Text != null && Text.text != text) Text.text = text;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Face the same way as the camera, so the text reads left to right and is never mirrored.
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
