// ============================================================================
// UpgradeDetailView.cs
// The detail panel on the right of the Build and Skills pages (inside the GameMenu prefab):
// the selected card's icon, group, name, effect, cost, needs, "after buying", a message and
// the Buy / Learn button.
//
// The artists can restyle and move everything. The slots tell the code which text is which;
// the labels "COST", "NEEDS", "AFTER BUYING" and "No refunds" are plain texts in the prefab
// that the code never touches. Any slot may be empty.
//
// DESIGN RULES: R7.2 (buy at the hub), R7.3, R7.4 (no refunds, no confirm dialog).
//
// HOW TO USE: on each page's detail panel in the GameMenu prefab (the Buy button's click is
// wired by the code, so leave its On Click list empty).
// HOW TO EDIT: in the prefab (Assets/Resources/UI/GameMenu.prefab).
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the Build / Skills detail panel.</summary>
    public class UpgradeDetailView : MonoBehaviour
    {
        [Tooltip("Big icon. Gets the upgrade's icon from GameMenu > Upgrade Icons, if it has one.")]
        public Image bigIcon;
        [Tooltip("The id on the big icon (hidden when there is an icon).")]
        public TMP_Text iconIdText;
        public TMP_Text groupText;
        public TMP_Text nameText;
        [Tooltip("What the upgrade does.")]
        public TMP_Text effectText;
        public TMP_Text costText;
        public TMP_Text needsText;
        [Tooltip("Litres left in Clean Storage after buying.")]
        public TMP_Text afterText;
        [Tooltip("\"Ready.\", \"You need 5 L more ...\" (coloured by GameMenu's state colours).")]
        public TMP_Text messageText;
        [Tooltip("The Buy / Learn button. The code sets its label and whether it can be pressed.")]
        public Button buyButton;
        [Tooltip("The Buy button's text (\"Buy for 15 L\").")]
        public TMP_Text buyLabel;

        const float DetailWidth = 340f;

        /// <summary>Builds the default black-and-white panel on the right of <paramref name="page"/>.</summary>
        public static UpgradeDetailView BuildDefault(RectTransform page, GameMenu m)
        {
            var panel = UiBuilder.Image("Detail Panel", page, m.panelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-DetailWidth, 0f);
            panel.offsetMax = Vector2.zero;
            var d = panel.gameObject.AddComponent<UpgradeDetailView>();

            d.bigIcon = UiBuilder.Image("Big Icon", panel, m.lineColor);
            UiBuilder.Place(d.bigIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(96f, 96f));
            d.iconIdText = UiBuilder.Text("Id", d.bigIcon.transform, "W1", 34f, m.textColor, TextAlignmentOptions.Center);
            d.iconIdText.fontStyle = FontStyles.Bold;

            d.groupText = Line(panel, "Group", "GROUP", 16f, m.mutedTextColor, -136f, 24f);
            d.groupText.fontStyle = FontStyles.Bold;
            d.nameText = Line(panel, "Name", "Upgrade name", 28f, m.textColor, -160f, 72f);
            d.nameText.fontStyle = FontStyles.Bold;
            d.effectText = Line(panel, "Effect", "What it does.", 19f, m.textColor, -236f, 120f);

            Label(panel, m.costLabel, -370f, m);
            d.costText = Value(panel, "Cost", -370f, m);
            Label(panel, m.needsLabel, -404f, m);
            d.needsText = Value(panel, "Needs", -404f, m);
            Label(panel, m.afterLabel, -438f, m);
            d.afterText = Value(panel, "After", -438f, m);
            d.messageText = Line(panel, "Message", "", 18f, m.warningAmber, -484f, 64f);

            d.buyButton = UiBuilder.Button("Buy Button", panel, "Buy for 10 L", 24f, m.waterBlue, m.screenColor, null);
            UiBuilder.Place((RectTransform)d.buyButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(DetailWidth - 48f, 56f));
            var colors = d.buyButton.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            d.buyButton.colors = colors;
            d.buyLabel = d.buyButton.GetComponentInChildren<TMP_Text>();
            d.buyLabel.fontStyle = FontStyles.Bold;

            var note = UiBuilder.Text("No Refunds", panel, m.noRefunds, 15f, m.mutedTextColor, TextAlignmentOptions.Center);
            UiBuilder.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(DetailWidth - 48f, 40f));
            return d;
        }

        static TMP_Text Line(RectTransform p, string n, string text, float size, Color c, float y, float h)
        {
            var t = UiBuilder.Text(n, p, text, size, c);
            UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(DetailWidth - 48f, h));
            return t;
        }

        static void Label(RectTransform p, string text, float y, GameMenu m)
        {
            var t = UiBuilder.Text(text + " Label", p, text, 15f, m.mutedTextColor);
            t.fontStyle = FontStyles.Bold;
            UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y - 3f), new Vector2(120f, 28f));
        }

        static TMP_Text Value(RectTransform p, string n, float y, GameMenu m)
        {
            var t = UiBuilder.Text(n, p, "-", 18f, m.textColor);
            UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, y), new Vector2(DetailWidth - 174f, 28f));
            return t;
        }
    }
}
