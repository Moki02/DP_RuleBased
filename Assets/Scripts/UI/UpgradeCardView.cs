// ============================================================================
// UpgradeCardView.cs
// One card on the Build or Skills page, made from the card prefab (UpgradeCardUI): fills in
// the id, name, cost and icon once, and updates the border, chip and fade for the card's
// state (Owned / Needs W2 / Needs bigger storage / Short 12 L / Can buy). A ring shows the
// selected card.
//
// DESIGN RULES: R7.1 to R7.4 (card states: see UpgradeLogic).
//
// HOW TO USE: GameMenu makes one per upgrade from its card prefab. You don't put this on an object.
// HOW TO EDIT: the card's look is the card prefab (GameMenu > Card Prefab, default
// Assets/Resources/UI/UpgradeCard.prefab). State colours and words are on GameMenu.
// Width / Height below are the layout grid's card size (where cards and lines are placed).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The on-screen card for one upgrade.</summary>
    public class UpgradeCardView
    {
        /// <summary>Card width in pixels used for the layout (design).</summary>
        public const float Width = 140f;
        /// <summary>Card height in pixels used for the layout (design).</summary>
        public const float Height = 156f;

        /// <summary>The upgrade's id.</summary>
        public readonly string Id;
        /// <summary>The card's rectangle (its centre is its position).</summary>
        public readonly RectTransform Rect;

        readonly UpgradeCardUI ui;

        /// <summary>Makes the card from <paramref name="prefab"/> (or the built-in card if null) under <paramref name="parent"/>.</summary>
        public UpgradeCardView(UpgradeInfo info, float cost, Transform parent, GameMenu style,
                               UpgradeCardUI prefab, Sprite iconSprite, UnityEngine.Events.UnityAction onClick)
        {
            Id = info.Id;
            ui = prefab != null ? Object.Instantiate(prefab, parent, false) : UpgradeCardUI.BuildDefault(parent, style);
            ui.gameObject.name = "Card_" + info.Id;
            ui.gameObject.SetActive(true);
            Rect = ui.Rect;
            // Cards are placed by their centre, measured from the card area's top-left.
            Rect.anchorMin = Rect.anchorMax = new Vector2(0f, 1f);
            Rect.pivot = new Vector2(0.5f, 0.5f);

            if (ui.button != null) ui.button.onClick.AddListener(onClick);
            if (ui.nameText != null) ui.nameText.text = info.Name;
            if (ui.costText != null) ui.costText.text = $"{cost:0} L";
            if (ui.drop != null && ui.drop.sprite == null) ui.drop.sprite = UiBuilder.DropSprite;

            bool hasIcon = iconSprite != null;
            if (ui.icon != null && hasIcon)
            {
                ui.icon.sprite = iconSprite;
                ui.icon.color = Color.white;
                ui.icon.preserveAspect = true;
            }
            if (ui.idText != null)
            {
                ui.idText.text = info.Id;
                ui.idText.gameObject.SetActive(!hasIcon);
            }
        }

        /// <summary>Moves the card so its centre is at (x, y) pixels from the parent's top-left corner (y goes down, so it's negative).</summary>
        public void SetPosition(float x, float y) => Rect.anchoredPosition = new Vector2(x, y);

        /// <summary>Updates the border, chip and fade for a state.</summary>
        public void Show(CardInfo card, bool selected, GameMenu style)
        {
            if (ui.selectRing != null) ui.selectRing.enabled = selected;
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
            if (ui.border != null) ui.border.color = borderColor;
            if (ui.chip != null) ui.chip.color = chipColor;
            if (ui.chipText != null)
            {
                ui.chipText.color = chipTextColor;
                ui.chipText.text = chipLabel;
            }
            if (ui.fade != null) ui.fade.alpha = selected ? 1f : alpha;
        }
    }
}
