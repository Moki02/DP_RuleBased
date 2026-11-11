// ============================================================================
// SimpleHudView.cs
// Goes on the root of the HUD MESSAGES PREFAB (Assets/Resources/UI/SimpleHud.prefab): the
// screen texts every system uses through SimpleHud (status lines, toasts, banners, the black
// fade, and the in-game pop-up panel such as the plain pause panel). The prefab has its own
// Canvas, so the artists can move, restyle and re-font all of it.
//
// The slots tell the code which part is which (any may be empty):
//   Status Text    - the bottom-right lines (Sick, lockdown, stage, event ...);
//   Toasts Text    - short messages at the top ("+3.0 L banked");
//   Banner Title / Banner Line - big middle messages ("THUNDERSTORM");
//   Fade           - a full-screen image; the code sets its alpha (collapse fade);
//   Panel          - a full-screen pop-up (blocks clicks) with Panel Title, Panel Body and a
//                    Panel Buttons area; buttons are copies of "Panel Button Template" (a hidden
//                    example button: style that one), stacked downwards.
// The Canvas's Sort Order is changed by the code: 9 normally (under the team HUD), 90 while
// the panel is up.
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: on the prefab's root (the one with the Canvas). SimpleHud loads it by itself.
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the HUD messages prefab.</summary>
    public class SimpleHudView : MonoBehaviour
    {
        [Header("Lines and messages")]
        [Tooltip("Bottom-right status lines.")] public TMP_Text statusText;
        [Tooltip("Short messages at the top.")] public TMP_Text toastsText;
        [Tooltip("Big message in the middle.")] public TMP_Text bannerTitle;
        [Tooltip("The line under the banner.")] public TMP_Text bannerLine;
        [Tooltip("Full-screen black image for fades (its alpha is set by the code).")] public Image fade;

        [Header("Pop-up panel")]
        [Tooltip("Full-screen pop-up (should block clicks). Hidden until something shows it.")] public GameObject panel;
        public TMP_Text panelTitle;
        public TMP_Text panelBody;
        [Tooltip("Where the panel's buttons go (stacked downwards from its top).")] public RectTransform panelButtons;
        [Tooltip("Hidden example button, copied for each panel button. Its height sets the spacing.")] public Button panelButtonTemplate;

        const float LineSize = 26f, ToastSize = 30f;

        /// <summary>Builds the default black-and-white HUD texts (used for the prefab, or when there is none).</summary>
        public static SimpleHudView BuildDefault(Transform parent)
        {
            var textColor = new Color(0.92f, 0.95f, 1f);
            var canvas = UiBuilder.CreateCanvas("SimpleHud", parent, 9);
            var v = canvas.gameObject.AddComponent<SimpleHudView>();
            var root = canvas.transform;

            // Status lines: bottom-right corner, right-aligned, growing upwards.
            v.statusText = UiBuilder.Text("Status", root, "", LineSize, textColor, TextAlignmentOptions.BottomRight);
            UiBuilder.Place(v.statusText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(620f, 360f));
            v.statusText.textWrappingMode = TextWrappingModes.NoWrap;
            Outline(v.statusText, 0.2f);

            v.toastsText = UiBuilder.Text("Toasts", root, "", ToastSize, new Color(0.55f, 0.85f, 1f), TextAlignmentOptions.Top);
            UiBuilder.Place(v.toastsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1200f, 260f));
            Outline(v.toastsText, 0.2f);

            v.bannerTitle = UiBuilder.Text("Banner Title", root, "", 64f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(v.bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1400f, 90f));
            v.bannerTitle.fontStyle = FontStyles.Bold;
            Outline(v.bannerTitle, 0.25f);
            v.bannerLine = UiBuilder.Text("Banner Line", root, "", 32f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(v.bannerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 60f));
            Outline(v.bannerLine, 0.2f);

            v.fade = UiBuilder.Image("Fade", root, Color.clear);

            var bg = UiBuilder.Image("Panel", root, new Color(0f, 0f, 0f, 0.92f), blocksClicks: true);
            v.panel = bg.gameObject;
            var panel = bg.rectTransform;
            v.panelTitle = UiBuilder.Text("Panel Title", panel, "TITLE", 72f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Place(v.panelTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1600f, 110f));
            v.panelTitle.fontStyle = FontStyles.Bold;
            v.panelBody = UiBuilder.Text("Panel Body", panel, "", 32f, textColor, TextAlignmentOptions.Top);
            UiBuilder.Place(v.panelBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 230f), new Vector2(1200f, 420f));
            v.panelButtons = UiBuilder.Rect("Panel Buttons", panel);
            UiBuilder.Place(v.panelButtons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(400f, 300f));
            v.panelButtonTemplate = UiBuilder.Button("Panel Button Template", panel, "Button", 30f, new Color(0.16f, 0.16f, 0.16f), Color.white, null);
            UiBuilder.Place((RectTransform)v.panelButtonTemplate.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(360f, 64f));
            v.panelButtonTemplate.gameObject.SetActive(false);
            return v;
        }

        static void Outline(TMP_Text t, float width)
        {
            t.outlineWidth = width;
            t.outlineColor = Color.black;
        }
    }
}
