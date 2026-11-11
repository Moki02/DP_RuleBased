// ============================================================================
// FacingLabel.cs
// A floating world-space TextMeshPro label (3D text, not UI) that turns to face the camera
// every frame, used over the water sources and the purifier.
//
// DESIGN RULES: R4.6 (each source shows a label with its current output).
//
// HOW TO USE: made by SourceVisuals.Label(...); set .Text each frame (it only redraws on change).
// HOW TO EDIT: size and height are in GameBalance > Water sources (hub).
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>A world-space TextMeshPro label that faces the camera (R4.6).</summary>
    public class FacingLabel : MonoBehaviour
    {
        [Tooltip("The TextMeshPro (3D, not UI) text this label shows.")]
        public TextMeshPro text;

        Camera cam;

        /// <summary>The label's text. Only redraws when it changes.</summary>
        public string Text
        {
            get => text != null ? text.text : "";
            set { if (text != null && text.text != value) text.text = value; }
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // Same as UI_FaceCamera: copy the camera's rotation so the text reads straight on.
            transform.rotation = cam.transform.rotation;
        }
    }
}
