// ============================================================================
// NotePanelView.cs
// Goes on the root of the NOTE PANEL PREFAB (Assets/Resources/UI/NotePanel.prefab): the box
// that shows a survivor note, and the journal on the map. The prefab has its own Canvas, so
// the artists can restyle the box, fonts, colours, add paper art, move the Close button.
//
// The slots tell the code which part is which:
//   Panel       - shown while a note or the journal is open (hidden otherwise);
//   Title Text / Body Text / Hint Text - filled with the note's title, its words, and the key
//                 hint ("E / Esc Close");
//   Close Button - closes it (leave its On Click list empty: the code wires it).
//
// DESIGN RULES: R8.7 (notes and journal).
//
// HOW TO USE: on the prefab's root (the one with the Canvas).
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the note / journal panel prefab.</summary>
    public class NotePanelView : MonoBehaviour
    {
        [Tooltip("Shown while a note or the journal is open.")] public GameObject panel;
        public TMP_Text titleText;
        public TMP_Text bodyText;
        [Tooltip("The key hint, e.g. \"E / Esc   Close\".")] public TMP_Text hintText;
        public Button closeButton;

        /// <summary>Builds the default black-and-white panel (used for the prefab, or when there is none).</summary>
        public static NotePanelView BuildDefault(Transform parent)
        {
            var canvas = UiBuilder.CreateCanvas("NotePanel", parent, 70); // above the map (60) and the Tab menu (65)
            var v = canvas.gameObject.AddComponent<NotePanelView>();

            var box = UiBuilder.Image("Box", canvas.transform, new Color(0.08f, 0.08f, 0.08f, 0.96f), blocksClicks: true).rectTransform;
            UiBuilder.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 900f));
            v.panel = box.gameObject;

            v.titleText = UiBuilder.Text("Title", box, "Note title", 40f, Color.white);
            v.titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1140f, 56f));

            v.bodyText = UiBuilder.Text("Body", box, "The note's words go here.", 25f, new Color(0.82f, 0.82f, 0.82f));
            v.bodyText.overflowMode = TextOverflowModes.Truncate;
            UiBuilder.Place(v.bodyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1140f, 680f));

            v.hintText = UiBuilder.Text("Hint", box, "E / Esc   Close", 20f, new Color(0.55f, 0.55f, 0.55f), TextAlignmentOptions.BottomLeft);
            UiBuilder.Place(v.hintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 30f), new Vector2(800f, 40f));

            v.closeButton = UiBuilder.Button("Close Button", box, "Close", 26f, new Color(0.2f, 0.2f, 0.2f), Color.white, null);
            UiBuilder.Place((RectTransform)v.closeButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 24f), new Vector2(200f, 56f));
            return v;
        }
    }
}
