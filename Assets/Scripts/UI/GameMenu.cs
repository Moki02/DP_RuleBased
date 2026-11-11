// ============================================================================
// GameMenu.cs
// The Tab menu: one menu with three pages, Build, Skills and Map (R7.1).
//   Tab  opens on the page used last (Build the first time); Tab again closes.
//   B    opens on Build;  M opens on Map;  the same key again, or Esc, closes.
//   The header has page tabs (Build | Skills | Map), the Clean Storage meter, the day and
//   drought stage, and "Tab  Close". Build has a "Skills ›" button on its right edge;
//   Skills has a "‹ Build" button on its left edge.
// While the menu is open the player can't move and the cursor is free. The clock pauses
// only while Build or Skills is open AT THE HUB; the Map page never pauses it.
// It can't open while the game is paused, in a minigame, collapsed, or behind the title /
// end screens (MenuState.Enter says no). If one of those takes over while it's open (for
// example the player collapses), it closes itself.
// The menu remembers the last page and the last selected card on each page (MenuMemory),
// and that memory is saved with the game.
//
// Build page: four columns (Water, Carry, Hub storage, Purification). Skills page: three
// branches (Endurance, Pathfinder, Scout). Cards needing another card in the same column
// are joined by a 2 px line, which turns water blue once the card above is owned. The
// detail panel on the right shows the selected card and the Buy / Learn button.
//
// DESIGN RULES: R7.1 (menu), R7.2 (buy at the hub), R7.3 (storage limits prices),
// R7.4 (permanent, no refunds, no confirm dialog).
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// It finds UpgradeManager, the map screen and the clock by itself and builds its own UI
// when the game starts (1920 × 1080 design, same as the artists' canvas).
// HOW TO EDIT: colours, every word players read, and where each card sits ("Build Layout"
// and "Skills Layout") are in the Inspector. Layout lines look like
//     Water|W1:0:0,W4:0:1,W7:0:2
// = column title, then id:lane:row for each card (lane = sub-column, row = level down).
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>The Build / Skills / Map menu.</summary>
    public class GameMenu : MonoBehaviour, ISaveable
    {
        /// <summary>Restored near the end.</summary>
        public int RestoreOrder => RestoreOrders.Menu;

        /// <summary>Saves the last page and the last selected card on each page.</summary>
        public void Capture(SaveData data)
        {
            data.menuLastPage = Memory.LastPage.ToString();
            data.menuLastBuildCard = Memory.LastBuildCard;
            data.menuLastSkillsCard = Memory.LastSkillsCard;
        }

        /// <summary>Puts the menu memory back (the menu closes if it was open).</summary>
        public void Restore(SaveData data) =>
            RestoreMemory(SaveCodec.PageFrom(data), data.menuLastBuildCard, data.menuLastSkillsCard);

        [Header("Links (empty = found by themselves)")]
        [Tooltip("Owned upgrades and Clean Storage.")] public UpgradeManager upgrades;
        [Tooltip("The map, shown as the Map page.")] public MapScreen mapScreen;

        [Header("Colours")]
        [Tooltip("#1B1713")] public Color screenColor = new Color32(27, 23, 19, 255);
        [Tooltip("#241F1A")] public Color panelColor = new Color32(36, 31, 26, 255);
        [Tooltip("#2A241E")] public Color cardColor = new Color32(42, 36, 30, 255);
        [Tooltip("#3A3129: lines, borders, empty bars.")] public Color lineColor = new Color32(58, 49, 41, 255);
        [Tooltip("#F2E9DC")] public Color textColor = new Color32(242, 233, 220, 255);
        [Tooltip("#A8998A")] public Color mutedTextColor = new Color32(168, 153, 138, 255);
        [Tooltip("#3FA9F5: owned, water.")] public Color waterBlue = new Color32(63, 169, 245, 255);
        [Tooltip("#7CC4F8: Can buy border.")] public Color canBuyBorder = new Color32(124, 196, 248, 255);
        [Tooltip("#E0B565: warnings (short of water).")] public Color warningAmber = new Color32(224, 181, 101, 255);
        [Tooltip("#8A6A2E: Needs bigger storage border.")] public Color biggerStorageBorder = new Color32(138, 106, 46, 255);
        [Tooltip("The 3 px ring around the selected card.")] public Color selectedRingColor = new Color32(242, 233, 220, 255);

        [Header("Text: header")]
        public string title = "BUILD AND SKILLS";
        public string buildTab = "BUILD", skillsTab = "SKILLS", mapTab = "MAP";
        public string storageLabel = "CLEAN STORAGE";
        [Tooltip("{0} = litres stored, {1} = size.")] public string storageFormat = "{0:0.0} / {1:0} L";
        [Tooltip("{0} = day, {1} = drought stage.")] public string dayFormat = "DAY {0}  ·  {1}";
        public string closeHint = "Tab   Close";
        public string toSkills = "Skills  ›", toBuild = "‹  Build";

        [Header("Text: card chips")]
        public string chipOwned = "OWNED";
        [Tooltip("{0} = the missing id.")] public string chipLocked = "NEEDS {0}";
        public string chipBiggerStorage = "BIGGER STORAGE";
        [Tooltip("{0} = litres missing.")] public string chipShort = "SHORT {0:0.#} L";
        public string chipCanBuy = "CAN BUY";

        [Header("Text: detail panel")]
        public string costLabel = "COST", needsLabel = "NEEDS", afterLabel = "AFTER BUYING";
        [Tooltip("{0} = litres.")] public string litresFormat = "{0:0.#} L";
        [Tooltip("{0} = litres left in Clean Storage after buying.")] public string afterFormat = "{0:0.#} L left";
        public string needsNone = "Nothing";
        [Tooltip("Put after an owned need. The font may not have ✓; then Owned Mark Fallback is used.")] public string ownedMark = " ✓";
        public string ownedMarkFallback = " (owned)";
        [Tooltip("{0} = cost.")] public string buyFormat = "Buy for {0:0} L";
        [Tooltip("{0} = cost.")] public string learnFormat = "Learn for {0:0} L";
        public string noRefunds = "Purchases are permanent. No refunds.";
        public string msgOwned = "You own this.";
        [Tooltip("{0} = the missing id.")] public string msgLocked = "Needs {0} first.";
        public string msgBiggerStorage = "Too big for your Clean Storage. Build bigger storage first.";
        [Tooltip("{0} = litres missing.")] public string msgShort = "You need {0:0.#} L more in Clean Storage.";
        public string msgCanBuy = "Ready.";
        public string msgNotAtHub = "Go back to the hub to buy.";

        [Header("Layout (column|id:lane:row, ...)")]
        public string[] buildLayout =
        {
            "Water|W1:0:0,W4:0:1,W7:0:2,W2:1:0,W3:1:1,W8:1:2,W5:2:1,W6:2:2",
            "Carry|S1:0:0,S4:0:1,S7:0:2",
            "Hub storage|S2:0:0,S3:0:1,S6:0:2,S5:1:0,S8:1:1",
            "Purification|P1:0:0,P3:0:1,P2:1:0",
        };
        public string[] skillsLayout =
        {
            "Endurance|E1:0:0,E2:0:1,E4:1:1,E3:0:2,E5:0:3",
            "Pathfinder|M1:1:0,M2:0:1,M3:1:1,M4:2:1,M5:0.5:2",
            "Scout|V1:1:0,V2:0:1,V3:1:1,V4:2:1,V5:1.5:2",
        };

        // Design sizes in pixels (1920 × 1080).
        const float HeaderHeight = 110f, Margin = 48f, DetailWidth = 340f, LanePitch = 160f, RowPitch = 196f, ColumnGap = 40f;

        /// <summary>What the menu remembers (saved with the game).</summary>
        public MenuMemory Memory { get; private set; } = new MenuMemory();

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => Memory.IsOpen;

        GameObject root;
        RectTransform buildPage, skillsPage, mapPage;
        readonly Dictionary<MenuPage, (Image underline, TextMeshProUGUI label)> tabs = new Dictionary<MenuPage, (Image, TextMeshProUGUI)>();
        readonly Dictionary<string, UpgradeCardView> cards = new Dictionary<string, UpgradeCardView>();
        readonly List<(string parentId, Image line)> lines = new List<(string, Image)>();
        TextMeshProUGUI storageText, dayText;
        Image storageFill;
        Detail buildDetail, skillsDetail;
        string selected;
        float refreshTimer;
        bool built;

        // ------------------------------------------------------------------ Setup

        void Start()
        {
            if (upgrades == null) upgrades = UpgradeManager.Instance != null ? UpgradeManager.Instance : FindAnyObjectByType<UpgradeManager>();
            if (mapScreen == null) mapScreen = FindAnyObjectByType<MapScreen>();
            if (upgrades == null || upgrades.Logic == null)
            {
                Debug.LogError("[GameMenu] Needs an UpgradeManager with its database.", this);
                enabled = false;
                return;
            }
            BuildUI();
            upgrades.Changed += Refresh;
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= Refresh;
        }

        // ------------------------------------------------------------------ Keys

        void Update()
        {
            if (!built) return;

            // Something else took over the screen while the menu was open (a collapse, the end
            // screen, the title screen's freeze): close the menu quietly, so it isn't left open
            // with the player walking around and the clock stuck paused after waking at the hub.
            var gm = Game_Manager.instance;
            if (IsOpen && gm != null && gm.currentGameState != Game_Manager.GameState.Menu)
            {
                Memory.IsOpen = false;
                ApplyMemory(false); // hides it and un-pauses the clock; the state is not ours to change
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame) Press(MenuKey.Tab);
                else if (kb.bKey.wasPressedThisFrame) Press(MenuKey.B);
                else if (kb.mKey.wasPressedThisFrame) Press(MenuKey.M);
                else if (kb.escapeKey.wasPressedThisFrame && IsOpen) Press(MenuKey.Esc);
            }

            if (IsOpen)
            {
                refreshTimer -= Time.unscaledDeltaTime;
                if (refreshTimer <= 0f) { refreshTimer = 0.25f; Refresh(); }
            }
        }

        /// <summary>Applies a menu key, as if it was pressed (also used by tests and buttons).</summary>
        public void Press(MenuKey key)
        {
            bool wasOpen = IsOpen;
            Memory.Press(key);
            if (!wasOpen && IsOpen && !MenuState.Enter())
            {
                Memory.IsOpen = false; // a minigame, pause or another screen is up: stay closed
                return;
            }
            ApplyMemory(wasOpen);
        }

        /// <summary>Goes to a page (header tabs, "Skills ›", "‹ Build").</summary>
        public void SwitchTo(MenuPage page)
        {
            bool wasOpen = IsOpen;
            if (!wasOpen && !MenuState.Enter()) return;
            Memory.SwitchTo(page);
            ApplyMemory(wasOpen);
        }

        /// <summary>Loading a save: puts back the remembered page and cards (the menu stays closed).</summary>
        public void RestoreMemory(MenuPage lastPage, string lastBuildCard, string lastSkillsCard)
        {
            if (IsOpen) Press(MenuKey.Esc);
            Memory.LastPage = lastPage;
            Memory.Current = lastPage;
            Memory.LastBuildCard = lastBuildCard ?? "";
            Memory.LastSkillsCard = lastSkillsCard ?? "";
        }

        void ApplyMemory(bool wasOpen)
        {
            if (!IsOpen)
            {
                root.SetActive(false);
                if (mapScreen != null) mapScreen.Hide();
                SetClockPaused(false);
                if (wasOpen) MenuState.Exit();
                return;
            }
            root.SetActive(true);
            buildPage.gameObject.SetActive(Memory.Current == MenuPage.Build);
            skillsPage.gameObject.SetActive(Memory.Current == MenuPage.Skills);
            mapPage.gameObject.SetActive(Memory.Current == MenuPage.Map);
            if (mapScreen != null)
            {
                if (Memory.Current == MenuPage.Map) mapScreen.Show(); else mapScreen.Hide();
            }
            if (Memory.Current != MenuPage.Map)
            {
                var tab = Memory.Current == MenuPage.Build ? UpgradeTab.Build : UpgradeTab.Skills;
                string remembered = Memory.SelectedFor(tab);
                Select(cards.ContainsKey(remembered) ? remembered : FirstCardOf(tab));
            }
            Refresh();
        }

        // The clock pauses only on Build or Skills, and only at the hub (the Map never pauses it).
        void SetClockPaused(bool paused)
        {
            if (GameClock_Manager.Instance != null) GameClock_Manager.Instance.SetClockPaused(paused);
        }

        void Select(string id)
        {
            var info = upgrades.Logic.Get(id);
            if (info == null) return;
            selected = id;
            Memory.Select(info.Tab, id);
            Refresh();
        }

        string FirstCardOf(UpgradeTab tab)
        {
            foreach (var u in upgrades.Logic.All) if (u.Tab == tab && cards.ContainsKey(u.Id)) return u.Id;
            return "";
        }

        // ------------------------------------------------------------------ Refresh

        void Refresh()
        {
            if (!built || !IsOpen) return;
            var logic = upgrades.Logic;

            SetClockPaused(Memory.Current != MenuPage.Map && upgrades.PlayerAtHub);

            foreach (var kv in tabs)
            {
                bool on = kv.Key == Memory.Current;
                kv.Value.underline.enabled = on;
                kv.Value.label.color = on ? waterBlue : mutedTextColor;
            }

            storageText.text = string.Format(storageFormat, logic.Stored, logic.Cap);
            storageFill.fillAmount = logic.Cap > 0f ? logic.Stored / logic.Cap : 0f;
            var balance = BalanceProvider.Current;
            int day = GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Day : 1;
            string stage = balance != null ? balance.GetStage(day).displayName : "";
            dayText.text = string.Format(dayFormat, day, stage.ToUpperInvariant());

            foreach (var card in cards.Values)
                card.Show(logic.GetCard(card.Id), card.Id == selected, this);
            foreach (var (parentId, line) in lines)
                line.color = logic.Owns(parentId) ? waterBlue : lineColor;

            var info = logic.Get(selected);
            if (info != null) (info.Tab == UpgradeTab.Build ? buildDetail : skillsDetail).Show(info, this);
        }

        // ------------------------------------------------------------------ Building the UI

        void BuildUI()
        {
            var canvas = UiBuilder.CreateCanvas("GameMenuCanvas", transform, 65);
            root = canvas.gameObject;
            var screen = UiBuilder.Rect("Menu", root.transform);
            UiBuilder.Image("Background", screen, screenColor, blocksClicks: true);

            BuildHeader(screen);

            var pages = UiBuilder.Rect("Pages", screen);
            UiBuilder.Fill(pages, Margin, Margin, HeaderHeight + 20f, 40f);
            buildPage = UiBuilder.Rect("BuildPage", pages);
            skillsPage = UiBuilder.Rect("SkillsPage", pages);
            mapPage = UiBuilder.Rect("MapPage", pages);

            buildDetail = BuildUpgradePage(buildPage, buildLayout, UpgradeTab.Build);
            skillsDetail = BuildUpgradePage(skillsPage, skillsLayout, UpgradeTab.Skills);

            // Page arrows on the outer edges of the card area (bottom, below the cards).
            var next = UiBuilder.Button("ToSkills", buildPage, toSkills, 22f, cardColor, textColor, () => SwitchTo(MenuPage.Skills));
            UiBuilder.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(DetailWidth + 24f), 0f), new Vector2(150f, 52f));
            var back = UiBuilder.Button("ToBuild", skillsPage, toBuild, 22f, cardColor, textColor, () => SwitchTo(MenuPage.Build));
            UiBuilder.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 52f));

            if (mapScreen != null) mapScreen.HostIn(mapPage);
            built = true;
        }

        void BuildHeader(RectTransform screen)
        {
            var header = UiBuilder.Rect("Header", screen);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, HeaderHeight);

            var titleText = UiBuilder.Text("Title", header, title, 34f, textColor, TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Margin, 0f), new Vector2(520f, 50f));

            // Page tabs in the middle.
            AddTab(header, MenuPage.Build, buildTab, -170f);
            AddTab(header, MenuPage.Skills, skillsTab, 0f);
            AddTab(header, MenuPage.Map, mapTab, 170f);

            // Right side: day and close hint, and the Clean Storage meter to their left.
            dayText = UiBuilder.Text("Day", header, "", 20f, textColor, TextAlignmentOptions.Right);
            UiBuilder.Place(dayText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -24f), new Vector2(300f, 30f));
            var hint = UiBuilder.Text("Close", header, closeHint, 18f, mutedTextColor, TextAlignmentOptions.Right);
            UiBuilder.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -60f), new Vector2(300f, 28f));

            float meterX = -(Margin + 300f + 40f);
            var label = UiBuilder.Text("StorageLabel", header, storageLabel, 15f, mutedTextColor, TextAlignmentOptions.Left);
            UiBuilder.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -18f), new Vector2(260f, 22f));
            storageText = UiBuilder.Text("Storage", header, "", 24f, textColor, TextAlignmentOptions.Left);
            storageText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(storageText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -40f), new Vector2(260f, 32f));
            var bar = UiBuilder.Image("StorageBar", header, lineColor);
            UiBuilder.Place(bar.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(meterX, -78f), new Vector2(260f, 8f));
            storageFill = UiBuilder.Image("Fill", bar.transform, waterBlue);
            storageFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            storageFill.type = Image.Type.Filled;
            storageFill.fillMethod = Image.FillMethod.Horizontal;

            var divider = UiBuilder.Image("Divider", header, lineColor);
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 2f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
        }

        void AddTab(RectTransform header, MenuPage page, string label, float x)
        {
            var button = UiBuilder.Button("Tab_" + page, header, label, 22f, new Color(0f, 0f, 0f, 0f), mutedTextColor, () => SwitchTo(page));
            UiBuilder.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 48f));
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Bold;
            var underline = UiBuilder.Image("Underline", button.transform, waterBlue);
            UiBuilder.Place(underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(110f, 3f));
            tabs[page] = (underline, text);
        }

        // One Build or Skills page: columns of cards with lines, plus the detail panel.
        Detail BuildUpgradePage(RectTransform page, string[] layout, UpgradeTab tab)
        {
            var area = UiBuilder.Rect("Cards", page);
            UiBuilder.Fill(area, 0f, DetailWidth + 24f, 0f, 0f);
            var lineLayer = UiBuilder.Rect("Lines", area);
            var cardLayer = UiBuilder.Rect("CardLayer", area);

            // Work out each column's width (in lanes) so the whole set can be centred.
            var columns = new List<(string title, List<(string id, float lane, int row)> items, int lanes)>();
            float totalWidth = 0f;
            foreach (var line in layout)
            {
                var col = ParseColumn(line);
                columns.Add(col);
                totalWidth += col.lanes * LanePitch;
            }
            totalWidth += ColumnGap * Mathf.Max(0, columns.Count - 1);

            float areaWidth = UiBuilder.ReferenceWidth - 2f * Margin - DetailWidth - 24f;
            float x = Mathf.Max(0f, (areaWidth - totalWidth) * 0.5f);
            var logic = upgrades.Logic;

            foreach (var col in columns)
            {
                var heading = UiBuilder.Text("Column", area, col.title.ToUpperInvariant(), 20f, mutedTextColor);
                heading.fontStyle = FontStyles.Bold;
                UiBuilder.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(col.lanes * LanePitch, 30f));

                var placed = new Dictionary<string, Vector2>();
                foreach (var (id, lane, row) in col.items)
                {
                    var info = logic.Get(id);
                    if (info == null) { Debug.LogWarning($"[GameMenu] Layout lists unknown id '{id}'.", this); continue; }
                    // Centre of the card, measured from the top-left of the card area (y goes down).
                    var centre = new Vector2(x + lane * LanePitch + UpgradeCardView.Width * 0.5f,
                                             -(48f + row * RowPitch + UpgradeCardView.Height * 0.5f));
                    string cardId = id;
                    var card = new UpgradeCardView(info, logic.CostOf(info), cardLayer, this, () => Select(cardId));
                    card.SetPosition(centre.x, centre.y);
                    cards[id] = card;
                    placed[id] = centre;
                }

                // Lines from each card to the cards it needs, if both are in this column.
                foreach (var kv in placed)
                    foreach (var need in logic.Get(kv.Key).Needs)
                        if (placed.TryGetValue(need, out var from))
                            lines.Add((need, MakeLine(lineLayer, from + Vector2.down * (UpgradeCardView.Height * 0.5f),
                                                                 kv.Value + Vector2.up * (UpgradeCardView.Height * 0.5f))));

                x += col.lanes * LanePitch + ColumnGap;
            }

            return new Detail(page, tab, this);
        }

        // A 2 px line between two points (pixels from the top-left of the card area).
        Image MakeLine(RectTransform parent, Vector2 a, Vector2 b)
        {
            var img = UiBuilder.Image("Line", parent, lineColor);
            var rt = img.rectTransform;
            Vector2 d = b - a;
            UiBuilder.Place(rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(2f, d.magnitude));
            // A 2 px tall-thin rectangle, turned to point from a to b.
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
            return img;
        }

        static (string title, List<(string id, float lane, int row)> items, int lanes) ParseColumn(string line)
        {
            var items = new List<(string id, float lane, int row)>();
            int bar = line.IndexOf('|');
            string title = bar >= 0 ? line.Substring(0, bar) : line;
            float maxLane = 0f;
            if (bar >= 0)
            {
                foreach (var part in line.Substring(bar + 1).Split(','))
                {
                    var bits = part.Split(':');
                    if (bits.Length < 3) continue;
                    float.TryParse(bits[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lane);
                    int.TryParse(bits[2], out int row);
                    items.Add((bits[0].Trim(), lane, row));
                    maxLane = Mathf.Max(maxLane, lane);
                }
            }
            return (title, items, Mathf.CeilToInt(maxLane) + 1);
        }

        // ------------------------------------------------------------------ Detail panel

        /// <summary>The 340 px panel on the right of a Build or Skills page.</summary>
        class Detail
        {
            readonly TextMeshProUGUI iconId, group, name, effect, cost, needs, after, message;
            readonly Button buy;
            readonly TextMeshProUGUI buyLabel;
            readonly GameMenu menu;
            readonly UpgradeTab tab;
            string shownId;

            public Detail(RectTransform page, UpgradeTab tab, GameMenu m)
            {
                menu = m;
                this.tab = tab;
                var panel = UiBuilder.Image("Detail", page, m.panelColor).rectTransform;
                panel.anchorMin = new Vector2(1f, 0f);
                panel.anchorMax = new Vector2(1f, 1f);
                panel.pivot = new Vector2(1f, 0.5f);
                panel.offsetMin = new Vector2(-DetailWidth, 0f);
                panel.offsetMax = Vector2.zero;

                var icon = UiBuilder.Image("BigIcon", panel, m.lineColor);
                UiBuilder.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(96f, 96f));
                iconId = UiBuilder.Text("Id", icon.transform, "", 34f, m.textColor, TextAlignmentOptions.Center);
                iconId.fontStyle = FontStyles.Bold;

                group = Line(panel, "Group", 16f, m.mutedTextColor, -136f, 24f);
                group.fontStyle = FontStyles.Bold;
                name = Line(panel, "Name", 28f, m.textColor, -160f, 72f);
                name.fontStyle = FontStyles.Bold;
                effect = Line(panel, "Effect", 19f, m.textColor, -236f, 120f);

                Label(panel, m.costLabel, -370f);
                cost = Value(panel, -370f);
                Label(panel, m.needsLabel, -404f);
                needs = Value(panel, -404f);
                Label(panel, m.afterLabel, -438f);
                after = Value(panel, -438f);
                message = Line(panel, "Message", 18f, m.warningAmber, -484f, 64f);

                buy = UiBuilder.Button("Buy", panel, "", 24f, m.waterBlue, m.screenColor, OnBuy);
                UiBuilder.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(DetailWidth - 48f, 56f));
                var colors = buy.colors;
                colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
                buy.colors = colors;
                buyLabel = buy.GetComponentInChildren<TextMeshProUGUI>();
                buyLabel.fontStyle = FontStyles.Bold;

                var note = UiBuilder.Text("NoRefunds", panel, m.noRefunds, 15f, m.mutedTextColor, TextAlignmentOptions.Center);
                UiBuilder.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(DetailWidth - 48f, 40f));
            }

            static TextMeshProUGUI Line(RectTransform p, string n, float size, Color c, float y, float h)
            {
                var t = UiBuilder.Text(n, p, "", size, c);
                UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(DetailWidth - 48f, h));
                return t;
            }

            void Label(RectTransform p, string text, float y)
            {
                var t = UiBuilder.Text("Label", p, text, 15f, menu.mutedTextColor);
                t.fontStyle = FontStyles.Bold;
                UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y - 3f), new Vector2(120f, 28f));
            }

            TextMeshProUGUI Value(RectTransform p, float y)
            {
                var t = UiBuilder.Text("Value", p, "", 18f, menu.textColor);
                UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, y), new Vector2(DetailWidth - 174f, 28f));
                return t;
            }

            public void Show(UpgradeInfo info, GameMenu m)
            {
                shownId = info.Id;
                var logic = m.upgrades.Logic;
                var card = logic.GetCard(info.Id);

                iconId.text = info.Id;
                group.text = (info.Group ?? "").ToUpperInvariant();
                name.text = info.Name;
                effect.text = info.Tooltip;
                cost.text = string.Format(m.litresFormat, card.Cost);

                string mark = m.FontHas(m.ownedMark) ? m.ownedMark : m.ownedMarkFallback;
                var parts = new List<string>();
                foreach (var need in info.Needs) parts.Add(logic.Owns(need) ? need + mark : need);
                needs.text = parts.Count == 0 ? m.needsNone : string.Join(", ", parts);
                after.text = card.State == CardState.Owned ? "-" : string.Format(m.afterFormat, Mathf.Max(0f, logic.Stored - card.Cost));

                bool hereOk = m.upgrades.CanBuyHere;
                switch (card.State)
                {
                    case CardState.Owned: message.text = m.msgOwned; message.color = m.waterBlue; break;
                    case CardState.Locked: message.text = string.Format(m.msgLocked, card.MissingId); message.color = m.mutedTextColor; break;
                    case CardState.NeedsBiggerStorage: message.text = m.msgBiggerStorage; message.color = m.warningAmber; break;
                    case CardState.ShortOfWater: message.text = string.Format(m.msgShort, card.ShortBy); message.color = m.warningAmber; break;
                    default:
                        message.text = hereOk ? m.msgCanBuy : m.msgNotAtHub;
                        message.color = hereOk ? m.canBuyBorder : m.warningAmber;
                        break;
                }

                buyLabel.text = string.Format(tab == UpgradeTab.Build ? m.buyFormat : m.learnFormat, card.Cost);
                buy.interactable = card.State == CardState.CanBuy && hereOk;
            }

            void OnBuy()
            {
                // No confirm dialog (R7.4): buying is instant and permanent.
                if (!string.IsNullOrEmpty(shownId)) menu.upgrades.Buy(shownId);
            }
        }

        // True if the default TMP font (or its fallbacks) can draw every character of s.
        bool FontHas(string s)
        {
            var font = TMP_Settings.defaultFontAsset;
            return font != null && font.HasCharacters(s, out _, true, true);
        }
    }
}
