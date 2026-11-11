// ============================================================================
// SourceVisuals.cs
// Small helpers for the water sources' placeholder art: plain coloured Unity primitives
// (no colliders, so they never block the base) and a floating world-space TextMeshPro
// label that always turns to face the camera.
//
// DESIGN RULES: R4.6 (every source shows a label with its current output).
//
// HOW TO USE: SourceVisuals.Part(...) makes a coloured primitive; SourceVisuals.Label(...)
// makes a FacingLabel; set label.Text every frame (it only redraws when the text changes).
// HOW TO EDIT: colours are chosen where the parts are made (WaterSources, BiosandFilter);
// the label height and size are in GameBalance > Water sources (hub).
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Makes the placeholder models and floating labels for the water sources.</summary>
    public static class SourceVisuals
    {
        /// <summary>
        /// A coloured primitive under <paramref name="parent"/>, at <paramref name="localPos"/>
        /// (centre) with size <paramref name="scale"/> in metres. Its collider is removed.
        /// </summary>
        public static GameObject Part(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 scale, Color colour)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            // No collider: the base must stay walkable (team note: keep them out of the way).
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Plain colour on the primitive's own material (works in URP and Built-in).
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var m = r.material; // a copy just for this object
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
                else m.color = colour;
            }
            return go;
        }

        /// <summary>A floating label <paramref name="height"/> metres above <paramref name="parent"/>.</summary>
        public static FacingLabel Label(Transform parent, float height, float fontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = new Color(0.92f, 0.97f, 1f);
            tmp.outlineWidth = 0.2f;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            var label = go.AddComponent<FacingLabel>();
            label.text = tmp;
            return label;
        }
    }
}
