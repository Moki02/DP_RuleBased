// ============================================================================
// HowToPlayView.cs
// Goes on the root of the HOW TO PLAY PREFAB (Assets/Resources/UI/HowToPlay.prefab): the
// big instructions panel that pops up after the game scene loads. The prefab has its own
// Canvas, so the artists can restyle the box, fonts and colours, add art, move the button.
//
// The slots tell the code which part is which:
//   Panel        - the whole screen (shown while the instructions are up, hidden otherwise);
//   Title Text   - "HOW TO PLAY";
//   Body Text    - the instructions (filled by HowToPlay.cs; it sits inside a scroll view,
//                  so long text scrolls with the mouse wheel);
//   Scroll       - the ScrollRect around the body (put back at the top each time it opens);
//   Hint Text    - the key hint at the bottom;
//   Start Button - closes the panel and starts play (leave its On Click list empty: the code wires it).
//
// DESIGN RULES: none of its own (it explains the Rules tab to the player).
//
// HOW TO USE: on the prefab's root (the one with the Canvas).
// HOW TO EDIT: the look in the prefab; the WORDS in HowToPlay.cs.
// Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the How to Play panel prefab.</summary>
    public class HowToPlayView : MonoBehaviour
    {
        [Tooltip("The whole screen: shown while the instructions are up.")] public GameObject panel;
        public TMP_Text titleText;
        [Tooltip("The instructions. Filled by HowToPlay.cs.")] public TMP_Text bodyText;
        [Tooltip("The scroll view around the body text (optional).")] public ScrollRect scroll;
        [Tooltip("The key hint at the bottom, e.g. \"Enter   Start\".")] public TMP_Text hintText;
        [Tooltip("Closes the panel and starts play. Leave On Click empty: the code wires it.")] public Button startButton;

        /// <summary>Builds the default black-and-white panel (used for the prefab, or when there is none).</summary>
        public static HowToPlayView BuildDefault(Transform parent)
        {
            // Above the HUD (10), map (60), Tab menu (65), notes and minigame (70).
            var canvas = UiBuilder.CreateCanvas("HowToPlay", parent, 80);
            var v = canvas.gameObject.AddComponent<HowToPlayView>();

            // Dark backdrop over the whole screen (blocks clicks to anything behind).
            var backdrop = UiBuilder.Image("Backdrop", canvas.transform, new Color(0f, 0f, 0f, 0.85f), blocksClicks: true);
            v.panel = backdrop.gameObject;

            var box = UiBuilder.Image("Box", backdrop.transform, new Color(0.08f, 0.08f, 0.08f, 0.98f), blocksClicks: true).rectTransform;
            UiBuilder.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 980f));

            v.titleText = UiBuilder.Text("Title", box, "HOW TO PLAY", 48f, Color.white, TextAlignmentOptions.Center);
            v.titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1400f, 64f));

            // ---- Scroll view: Scroll (ScrollRect) > Viewport (mask) > Body (text that grows to fit)
            var scrollRt = UiBuilder.Rect("Scroll", box);
            UiBuilder.Place(scrollRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1420f, 740f));
            var sr = scrollRt.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;

            // Transparent image so the mouse wheel works anywhere over the text.
            var viewport = UiBuilder.Image("Viewport", scrollRt, new Color(0f, 0f, 0f, 0f), blocksClicks: true).rectTransform;
            UiBuilder.Fill(viewport, 0f, 30f, 0f, 0f); // leave room on the right for the scrollbar
            viewport.gameObject.AddComponent<RectMask2D>();

            v.bodyText = UiBuilder.Text("Body", viewport, "Instructions go here.", 26f, new Color(0.85f, 0.85f, 0.85f));
            var body = v.bodyText.rectTransform;
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = new Vector2(10f, 0f);
            body.offsetMax = new Vector2(-10f, 0f);
            body.sizeDelta = new Vector2(body.sizeDelta.x, 740f);
            var fitter = v.bodyText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            v.bodyText.richText = true;

            sr.viewport = viewport;
            sr.content = body;

            // Vertical scrollbar on the right.
            var barBg = UiBuilder.Image("Scrollbar", scrollRt, new Color(0.18f, 0.18f, 0.18f), blocksClicks: true).rectTransform;
            barBg.anchorMin = new Vector2(1f, 0f);
            barBg.anchorMax = new Vector2(1f, 1f);
            barBg.pivot = new Vector2(1f, 0.5f);
            barBg.sizeDelta = new Vector2(16f, 0f);
            barBg.anchoredPosition = Vector2.zero;
            var area = UiBuilder.Rect("Sliding Area", barBg);
            var handle = UiBuilder.Image("Handle", area, new Color(0.6f, 0.6f, 0.6f), blocksClicks: true);
            var bar = barBg.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = bar;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            v.scroll = sr;

            v.hintText = UiBuilder.Text("Hint", box, "Mouse wheel   Scroll        Enter   Start        H   Show this again", 22f,
                                        new Color(0.55f, 0.55f, 0.55f), TextAlignmentOptions.BottomLeft);
            UiBuilder.Place(v.hintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 34f), new Vector2(1000f, 40f));

            // Blue = water, the one accent colour (team rule for the black-and-white screens).
            v.startButton = UiBuilder.Button("Start Button", box, "START", 30f, new Color(0.2f, 0.5f, 0.95f), Color.white, null);
            UiBuilder.Place((RectTransform)v.startButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 26f), new Vector2(260f, 64f));
            return v;
        }
    }
}
