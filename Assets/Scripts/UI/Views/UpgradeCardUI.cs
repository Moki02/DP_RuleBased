// ============================================================================
// UpgradeCardUI.cs
// Goes on the root of the UPGRADE CARD PREFAB (Assets/Resources/UI/UpgradeCard.prefab, or
// whichever card prefab is dragged into GameMenu > "Card Prefab"). The Build and Skills
// pages make one copy of it per upgrade, so changing the prefab changes all of them.
//
// The artists can restyle everything in the prefab (sizes, sprites, fonts, colours, layout).
// The slots below tell the code which part is which; any slot can be left empty and that
// part is simply not updated. What the code changes while playing:
//   - Id Text / Name Text / Cost Text: the upgrade's id, name and cost;
//   - Icon: the upgrade's icon if one is set in GameMenu > "Upgrade Icons" (the id text is
//     hidden then);
//   - Border and Chip colours, Chip Text: the card's state (Owned, Needs W2, Short 12 L ...),
//     using the state colours on GameMenu;
//   - Select Ring: shown on the selected card;
//   - Fade (CanvasGroup): locked cards are faded.
// The card's position comes from GameMenu's layout lists (column / lane / row).
//
// DESIGN RULES: R7.1 to R7.4 (card states).
//
// HOW TO USE: on the card prefab's root (with a RectTransform). The Body needs a Button: that
// is what the player clicks.
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of one upgrade card (on the card prefab).</summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [Tooltip("Clicked to select the card. Usually on the card's body.")]
        public Button button;
        [Tooltip("Shown only on the selected card.")]
        public Image selectRing;
        [Tooltip("Coloured by the card's state (Owned, Can buy ...).")]
        public Image border;
        [Tooltip("Shows the upgrade's icon from GameMenu > Upgrade Icons, if it has one.")]
        public Image icon;
        [Tooltip("The upgrade's id (W1, E3 ...). Hidden when the upgrade has an icon.")]
        public TMP_Text idText;
        public TMP_Text nameText;
        [Tooltip("The cost, e.g. \"15 L\".")]
        public TMP_Text costText;
        [Tooltip("The water drop next to the cost. Empty sprite = the built-in drop.")]
        public Image drop;
        [Tooltip("The state chip's background, coloured by the state.")]
        public Image chip;
        [Tooltip("The state chip's words (OWNED, NEEDS W2, SHORT 12 L, CAN BUY ...).")]
        public TMP_Text chipText;
        [Tooltip("Fades locked cards. Optional.")]
        public CanvasGroup fade;

        /// <summary>The card's rectangle.</summary>
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Builds the default black-and-white card (used for the prefab, or when there is none).</summary>
        public static UpgradeCardUI BuildDefault(Transform parent, GameMenu style)
        {
            const float Width = UpgradeCardView.Width, Height = UpgradeCardView.Height;
            const float BorderWidth = 2f, RingWidth = 3f, IconSize = 44f;

            var rect = UiBuilder.Rect("UpgradeCard", parent);
            UiBuilder.Place(rect, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            var card = rect.gameObject.AddComponent<UpgradeCardUI>();
            card.fade = rect.gameObject.AddComponent<CanvasGroup>();

            // Selection ring, border and body are three stacked rectangles, each a bit smaller.
            card.selectRing = UiBuilder.Image("Select Ring", rect, style.selectedRingColor);
            Grow(card.selectRing.rectTransform, BorderWidth + RingWidth);
            card.border = UiBuilder.Image("Border", rect, style.lineColor);
            Grow(card.border.rectTransform, BorderWidth);
            var body = UiBuilder.Image("Body", rect, style.cardColor, blocksClicks: true);
            card.button = body.gameObject.AddComponent<Button>();
            card.button.targetGraphic = body;

            card.icon = UiBuilder.Image("Icon", body.transform, style.lineColor);
            UiBuilder.Place(card.icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(IconSize, IconSize));
            card.idText = UiBuilder.Text("Id", card.icon.transform, "W1", 16f, style.textColor, TextAlignmentOptions.Center);
            card.idText.fontStyle = FontStyles.Bold;

            card.nameText = UiBuilder.Text("Name", body.transform, "Upgrade name", 16f, style.textColor);
            UiBuilder.Place(card.nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -60f), new Vector2(Width - 20f, 40f));

            card.drop = UiBuilder.Image("Drop", body.transform, style.waterBlue);
            UiBuilder.Place(card.drop.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -104f), new Vector2(14f, 16f));
            card.costText = UiBuilder.Text("Cost", body.transform, "10 L", 16f, style.textColor);
            UiBuilder.Place(card.costText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -101f), new Vector2(100f, 22f));

            card.chip = UiBuilder.Image("Chip", body.transform, style.lineColor);
            UiBuilder.Place(card.chip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(Width - 16f, 22f));
            card.chipText = UiBuilder.Text("Chip Text", card.chip.transform, "CAN BUY", 12f, style.textColor, TextAlignmentOptions.Center);
            card.chipText.fontStyle = FontStyles.Bold;
            card.chipText.textWrappingMode = TextWrappingModes.NoWrap;
            card.chipText.overflowMode = TextOverflowModes.Ellipsis;
            return card;
        }

        static void Grow(RectTransform rt, float by)
        {
            rt.offsetMin = new Vector2(-by, -by);
            rt.offsetMax = new Vector2(by, by);
        }
    }
}
