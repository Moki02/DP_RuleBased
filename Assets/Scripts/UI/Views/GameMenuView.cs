// ============================================================================
// GameMenuView.cs
// Goes on the root of the BUILD / SKILLS / MAP MENU PREFAB (Assets/Resources/UI/GameMenu.prefab,
// or whichever prefab is dragged into GameMenu > "Menu Prefab"). The prefab has its own
// Canvas, so the artists can open it and restyle or move every background, header text, tab,
// button and panel.
//
// The slots below tell the code which part is which. What the code does with them:
//   - Tabs: switches pages when clicked; shows the underline and colours the label of the
//     open page (GameMenu's Water Blue / Muted Text colours);
//   - Storage Text / Storage Fill: "45.0 / 50 L" and the bar (the fill's right edge moves);
//   - Day Text: "DAY 3 · DRY SPELL";
//   - Build Cards / Skills Cards: the areas the cards and their lines are put into, using the
//     card prefab and GameMenu's layout lists. "Column Heading Template" and "Line Template"
//     are hidden examples copied for each column title and each line: style those;
//   - Build Detail / Skills Detail: the panels on the right (UpgradeDetailView);
//   - To Skills / To Build buttons: switch pages;
//   - Map Page: the map screen (MapView prefab) is moved in here.
// Everything else in the prefab (titles, "Tab Close", labels, art) is plain and never changed
// by the code.
//
// DESIGN RULES: R7.1 (menu).
//
// HOW TO USE: on the prefab's root (the one with the Canvas). Leave the buttons' On Click
// lists empty: the code wires them.
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the Build / Skills / Map menu prefab.</summary>
    public class GameMenuView : MonoBehaviour
    {
        /// <summary>One header tab.</summary>
        [Serializable]
        public class Tab
        {
            [Tooltip("Clicked to open this page.")] public Button button;
            [Tooltip("Shown only while this page is open. Optional.")] public Image underline;
            [Tooltip("Coloured while this page is open. Optional.")] public TMP_Text label;
        }

        /// <summary>A page's card area.</summary>
        [Serializable]
        public class CardArea
        {
            [Tooltip("The area the columns are laid out in (measured from its top-left).")] public RectTransform area;
            [Tooltip("Lines between cards go in here (put it behind the cards).")] public RectTransform lineLayer;
            [Tooltip("Cards go in here.")] public RectTransform cardLayer;
        }

        [Header("Pages")]
        public GameObject buildPage;
        public GameObject skillsPage;
        public GameObject mapPage;

        [Header("Header")]
        public Tab buildTab = new Tab();
        public Tab skillsTab = new Tab();
        public Tab mapTab = new Tab();
        [Tooltip("\"45.0 / 50 L\"")] public TMP_Text storageText;
        [Tooltip("The storage bar's fill: its right edge (Anchor Max X) moves from 0 to 1.")] public RectTransform storageFill;
        [Tooltip("\"DAY 3 · DRY SPELL\"")] public TMP_Text dayText;

        [Header("Build and Skills pages")]
        public CardArea buildCards = new CardArea();
        public CardArea skillsCards = new CardArea();
        [Tooltip("Hidden example of a column title (WATER, CARRY ...). Copied for each column.")]
        public TMP_Text columnHeadingTemplate;
        [Tooltip("Hidden example of a line between two cards. Copied for each line; its colour turns Water Blue when the card above is owned.")]
        public Image lineTemplate;
        public UpgradeDetailView buildDetail;
        public UpgradeDetailView skillsDetail;
        [Tooltip("On the Build page: goes to Skills.")] public Button toSkillsButton;
        [Tooltip("On the Skills page: goes to Build.")] public Button toBuildButton;

        // Design sizes in pixels (1920 × 1080).
        const float HeaderHeight = 110f, Margin = 48f, DetailWidth = 340f;

        /// <summary>Builds the default black-and-white menu (used for the prefab, or when there is none).</summary>
        public static GameMenuView BuildDefault(Transform parent, GameMenu m)
        {
            var canvas = UiBuilder.CreateCanvas("GameMenu", parent, 65);
            var v = canvas.gameObject.AddComponent<GameMenuView>();
            var screen = UiBuilder.Rect("Menu", canvas.transform);
            UiBuilder.Image("Background", screen, m.screenColor, blocksClicks: true);

            BuildHeader(v, screen, m);

            var pages = UiBuilder.Rect("Pages", screen);
            UiBuilder.Fill(pages, Margin, Margin, HeaderHeight + 20f, 40f);
            var build = UiBuilder.Rect("Build Page", pages);
            var skills = UiBuilder.Rect("Skills Page", pages);
            var map = UiBuilder.Rect("Map Page", pages);
            v.buildPage = build.gameObject;
            v.skillsPage = skills.gameObject;
            v.mapPage = map.gameObject;

            v.buildCards = MakeCardArea(build);
            v.skillsCards = MakeCardArea(skills);
            v.buildDetail = UpgradeDetailView.BuildDefault(build, m);
            v.skillsDetail = UpgradeDetailView.BuildDefault(skills, m);

            // Templates (hidden): copied for each column title and each line.
            var templates = UiBuilder.Rect("Templates (hidden examples)", screen);
            v.columnHeadingTemplate = UiBuilder.Text("Column Heading Template", templates, "COLUMN", 20f, m.mutedTextColor);
            v.columnHeadingTemplate.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.columnHeadingTemplate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(160f, 30f));
            v.lineTemplate = UiBuilder.Image("Line Template", templates, m.lineColor);
            UiBuilder.Place(v.lineTemplate.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 40f));
            templates.gameObject.SetActive(false);

            // Page arrows on the outer edges of the card area (bottom, below the cards).
            v.toSkillsButton = UiBuilder.Button("To Skills Button", build, m.toSkills, 22f, m.cardColor, m.textColor, null);
            UiBuilder.Place((RectTransform)v.toSkillsButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(DetailWidth + 24f), 0f), new Vector2(150f, 52f));
            v.toBuildButton = UiBuilder.Button("To Build Button", skills, m.toBuild, 22f, m.cardColor, m.textColor, null);
            UiBuilder.Place((RectTransform)v.toBuildButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 52f));
            return v;
        }

        static CardArea MakeCardArea(RectTransform page)
        {
            var area = UiBuilder.Rect("Cards", page);
            UiBuilder.Fill(area, 0f, DetailWidth + 24f, 0f, 0f);
            return new CardArea
            {
                area = area,
                lineLayer = UiBuilder.Rect("Lines", area),
                cardLayer = UiBuilder.Rect("Card Layer", area),
            };
        }

        static void BuildHeader(GameMenuView v, RectTransform screen, GameMenu m)
        {
            var header = UiBuilder.Rect("Header", screen);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, HeaderHeight);

            var titleText = UiBuilder.Text("Title", header, m.title, 34f, m.textColor, TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Margin, 0f), new Vector2(520f, 50f));

            v.buildTab = MakeTab(header, "Build Tab", m.buildTab, -170f, m);
            v.skillsTab = MakeTab(header, "Skills Tab", m.skillsTab, 0f, m);
            v.mapTab = MakeTab(header, "Map Tab", m.mapTab, 170f, m);

            v.dayText = UiBuilder.Text("Day", header, "DAY 1", 20f, m.textColor, TextAlignmentOptions.Right);
            UiBuilder.Place(v.dayText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -24f), new Vector2(300f, 30f));
            var hint = UiBuilder.Text("Close Hint", header, m.closeHint, 18f, m.mutedTextColor, TextAlignmentOptions.Right);
            UiBuilder.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -60f), new Vector2(300f, 28f));

            float meterX = -(Margin + 300f + 40f);
            var label = UiBuilder.Text("Storage Label", header, m.storageLabel, 15f, m.mutedTextColor, TextAlignmentOptions.Left);
            UiBuilder.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -18f), new Vector2(260f, 22f));
            v.storageText = UiBuilder.Text("Storage", header, "0.0 / 25 L", 24f, m.textColor, TextAlignmentOptions.Left);
            v.storageText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.storageText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -40f), new Vector2(260f, 32f));
            var bar = UiBuilder.Image("Storage Bar", header, m.lineColor);
            UiBuilder.Place(bar.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -78f), new Vector2(260f, 8f));
            var fill = UiBuilder.Image("Storage Fill", bar.transform, m.waterBlue);
            fill.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            v.storageFill = fill.rectTransform;

            var divider = UiBuilder.Image("Divider", header, m.lineColor);
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 2f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
        }

        static Tab MakeTab(RectTransform header, string name, string label, float x, GameMenu m)
        {
            var button = UiBuilder.Button(name, header, label, 22f, new Color(0f, 0f, 0f, 0f), m.mutedTextColor, null);
            UiBuilder.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 48f));
            var text = button.GetComponentInChildren<TMP_Text>();
            text.fontStyle = FontStyles.Bold;
            var underline = UiBuilder.Image("Underline", button.transform, m.waterBlue);
            UiBuilder.Place(underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(110f, 3f));
            return new Tab { button = button, underline = underline, label = text };
        }
    }
}
