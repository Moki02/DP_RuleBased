// ============================================================================
// UpgradeCardView.cs
// One card on the Build or Skills page: an icon slot (a placeholder showing the id until
// the artists' icons arrive), the name, a water drop with the cost, and a state chip
// (Owned / Needs W2 / Needs bigger storage / Short 12 L / Can buy). The border colour
// shows the state; a light ring shows the selected card.
//
// DESIGN RULES: R7.1 to R7.4 (card states: see UpgradeLogic).
//
// HOW TO USE: GameMenu makes one per upgrade. You don't put this on an object.
// HOW TO EDIT: sizes are the constants below (1920 × 1080 design); colours and words are
// on GameMenu in the Inspector.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>The on-screen card for one upgrade.</summary>
    public class UpgradeCardView
    {
        /// <summary>Card width in pixels (design).</summary>
        public const float Width = 140f;
        /// <summary>Card height in pixels (design).</summary>
        public const float Height = 156f;
        const float BorderWidth = 2f, RingWidth = 3f, IconSize = 44f;

        /// <summary>The upgrade's id.</summary>
        public readonly string Id;
        /// <summary>The card's rectangle (its centre is its position).</summary>
        public readonly RectTransform Rect;

        readonly Image ring, border, body, chip;
        readonly TextMeshProUGUI chipText;
        readonly CanvasGroup fade;

        /// <summary>Builds the card under <paramref name="parent"/>. <paramref name="onClick"/> runs when it's clicked.</summary>
        public UpgradeCardView(UpgradeInfo info, float cost, Transform parent, GameMenu style, UnityEngine.Events.UnityAction onClick)
        {
            Id = info.Id;
            Rect = UiBuilder.Rect("Card_" + info.Id, parent);
            UiBuilder.Place(Rect, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            fade = Rect.gameObject.AddComponent<CanvasGroup>();

            // Selection ring, border and body are three stacked rectangles, each a bit smaller.
            ring = UiBuilder.Image("SelectRing", Rect, style.selectedRingColor);
            Grow(ring.rectTransform, BorderWidth + RingWidth);
            border = UiBuilder.Image("Border", Rect, style.lineColor);
            Grow(border.rectTransform, BorderWidth);
            body = UiBuilder.Image("Body", Rect, style.cardColor, blocksClicks: true);
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.onClick.AddListener(onClick);

            // Icon slot with the id, until real icons exist.
            var icon = UiBuilder.Image("Icon", body.transform, style.lineColor);
            UiBuilder.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(IconSize, IconSize));
            var iconText = UiBuilder.Text("Id", icon.transform, info.Id, 16f, style.textColor, TextAlignmentOptions.Center);
            iconText.fontStyle = FontStyles.Bold;

            var name = UiBuilder.Text("Name", body.transform, info.Name, 16f, style.textColor);
            UiBuilder.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -60f), new Vector2(Width - 20f, 40f));

            var drop = UiBuilder.Image("Drop", body.transform, style.waterBlue);
            drop.sprite = UiBuilder.DropSprite;
            UiBuilder.Place(drop.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -104f), new Vector2(14f, 16f));
            var costText = UiBuilder.Text("Cost", body.transform, $"{cost:0} L", 16f, style.textColor);
            UiBuilder.Place(costText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -101f), new Vector2(100f, 22f));

            chip = UiBuilder.Image("Chip", body.transform, style.lineColor);
            UiBuilder.Place(chip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(Width - 16f, 22f));
            chipText = UiBuilder.Text("ChipText", chip.transform, "", 12f, style.textColor, TextAlignmentOptions.Center);
            chipText.fontStyle = FontStyles.Bold;
            chipText.textWrappingMode = TextWrappingModes.NoWrap;
            chipText.overflowMode = TextOverflowModes.Ellipsis;
        }

        static void Grow(RectTransform rt, float by)
        {
            rt.offsetMin = new Vector2(-by, -by);
            rt.offsetMax = new Vector2(by, by);
        }

        /// <summary>Moves the card so its centre is at (x, y) pixels from the parent's top-left corner (y goes down, so it's negative).</summary>
        public void SetPosition(float x, float y) => Rect.anchoredPosition = new Vector2(x, y);

        /// <summary>Updates the border, chip and fade for a state.</summary>
        public void Show(CardInfo card, bool selected, GameMenu style)
        {
            ring.enabled = selected;
            Color borderColor = style.lineColor, chipColor = style.lineColor, chipTextColor = style.textColor;
            string chipLabel;
            float alpha = 1f;
            switch (card.State)
            {
                case CardState.Owned:
                    borderColor = style.waterBlue; chipColor = style.waterBlue; chipTextColor = style.screenColor;
                    chipLabel = style.chipOwned; break;
                case CardState.Locked:
                    chipTextColor = style.mutedTextColor; alpha = 0.6f;
                    chipLabel = string.Format(style.chipLocked, card.MissingId); break;
                case CardState.NeedsBiggerStorage:
                    borderColor = style.biggerStorageBorder; chipColor = style.biggerStorageBorder;
                    chipLabel = style.chipBiggerStorage; break;
                case CardState.ShortOfWater:
                    chipColor = style.warningAmber; chipTextColor = style.screenColor;
                    chipLabel = string.Format(style.chipShort, card.ShortBy); break;
                default:
                    borderColor = style.canBuyBorder; chipColor = style.canBuyBorder; chipTextColor = style.screenColor;
                    chipLabel = style.chipCanBuy; break;
            }
            border.color = borderColor;
            chip.color = chipColor;
            chipText.color = chipTextColor;
            chipText.text = chipLabel;
            fade.alpha = selected ? 1f : alpha;
        }
    }
}
