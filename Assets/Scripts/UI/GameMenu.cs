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
// It finds UpgradeManager, the map screen and the clock by itself.
//
// THE LOOK IS A PREFAB WITH ITS OWN CANVAS (drag-and-drop slots under "UI prefabs"):
//   Menu Prefab  - the whole menu (header, tabs, pages, detail panels, buttons). Empty =
//                  Assets/Resources/UI/GameMenu.prefab. See GameMenuView.cs.
//   Card Prefab  - one upgrade card, copied for every upgrade. Empty =
//                  Assets/Resources/UI/UpgradeCard.prefab. See UpgradeCardUI.cs.
//   Upgrade Icons - drag a sprite in next to an id (W1, E3 ...) to show it on that card and
//                  in the detail panel.
// If no prefab exists yet, the plain built-in look is used (Tools > The Last Drop >
// Build UI Prefabs saves that look as the prefabs, for the artists to edit).
//
// HOW TO EDIT: the colours below are the STATE colours (card borders and chips, the open
// tab, owned lines, messages) and the colours the prefab is first made with. The words that
// change while playing (formats, chips, messages) are below too; fixed words (title, tab
// names, labels) are in the prefab. Where each card sits ("Build Layout" and "Skills
// Layout") is in the Inspector. Layout lines look like
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

        [Header("UI prefabs (drag and drop)")]
        [Tooltip("The menu's look: a prefab with a Canvas and a GameMenuView on its root. Empty = Resources/UI/GameMenu.")]
        public GameObject menuPrefab;
        [Tooltip("One upgrade card: a prefab with an UpgradeCardUI on its root. Empty = Resources/UI/UpgradeCard.")]
        public UpgradeCardUI cardPrefab;
        [Tooltip("Icons for the cards and the detail panel. Drag a sprite in next to an id (W1, S2, E3 ...). Upgrades without one show their id.")]
        public List<UpgradeIcon> upgradeIcons = new List<UpgradeIcon>();

        /// <summary>A card icon for one upgrade id.</summary>
        [System.Serializable]
        public class UpgradeIcon
        {
            [Tooltip("The upgrade's id, e.g. W1.")] public string id;
            [Tooltip("The picture shown on its card and in the detail panel.")] public Sprite icon;
        }

        /// <summary>Where the default prefabs live inside a Resources folder.</summary>
        public const string MenuPrefabResource = "UI/GameMenu", CardPrefabResource = "UI/UpgradeCard";

        [Header("Colours")]
        [Tooltip("On: the colours below are replaced at start by the black-and-white look (black background, " +
                 "white text, grey panels / cards / borders, blue only for water). Off: the colours below are used.")]
        public bool blackAndWhite = true;
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

        [Header("Text: header (fixed words: used when the prefab is first made)")]
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
            "Scout|V1:1:0,V6:2:0,V2:0:1,V3:1:1,V4:2:1,V5:1.5:2",
        };

        // Design sizes in pixels (1920 × 1080).
        const float Margin = 48f, DetailWidth = 340f, LanePitch = 160f, RowPitch = 196f, ColumnGap = 40f;

        /// <summary>What the menu remembers (saved with the game).</summary>
        public MenuMemory Memory { get; private set; } = new MenuMemory();

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => Memory.IsOpen;

        GameObject root;
        GameMenuView view;
        readonly Dictionary<MenuPage, GameMenuView.Tab> tabs = new Dictionary<MenuPage, GameMenuView.Tab>();
        readonly Dictionary<string, UpgradeCardView> cards = new Dictionary<string, UpgradeCardView>();
        readonly List<(string parentId, Image line)> lines = new List<(string, Image)>();
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
            if (blackAndWhite) ApplyBlackAndWhite();
            BuildUI();
            upgrades.Changed += Refresh;
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= Refresh;
        }

        /// <summary>
        /// Black background, white text, grey panels / cards / borders; blue stays the water accent
        /// (owned, Can buy, the storage bar). Warnings are light grey instead of amber.
        /// (Also used by Tools > Build UI Prefabs to make the default prefabs.)
        /// </summary>
        public void ApplyBlackAndWhite()
        {
            screenColor = new Color32(10, 10, 10, 255);          // #0A0A0A
            panelColor = new Color32(28, 28, 28, 255);           // #1C1C1C
            cardColor = new Color32(38, 38, 38, 255);            // #262626
            lineColor = new Color32(70, 70, 70, 255);            // #464646
            textColor = Color.white;
            mutedTextColor = new Color32(160, 160, 160, 255);    // #A0A0A0
            waterBlue = new Color32(63, 169, 245, 255);          // #3FA9F5 (water accent)
            canBuyBorder = new Color32(124, 196, 248, 255);      // #7CC4F8 (water accent)
            warningAmber = new Color32(220, 220, 220, 255);      // #DCDCDC
            biggerStorageBorder = new Color32(110, 110, 110, 255); // #6E6E6E
            selectedRingColor = Color.white;
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
            if (!wasOpen)
            {
                if (key == MenuKey.Esc) return; // Esc with the menu closed: nothing to do
                // Ask first, so a key pressed while a note, minigame, pause or another screen is
                // up does nothing at all: the player has to close the note before Tab / B / M
                // work, and the page Tab opens next time doesn't change.
                if (!MenuState.Enter()) return;
            }
            Memory.Press(key);
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
            if (view.buildPage != null) view.buildPage.SetActive(Memory.Current == MenuPage.Build);
            if (view.skillsPage != null) view.skillsPage.SetActive(Memory.Current == MenuPage.Skills);
            if (view.mapPage != null) view.mapPage.SetActive(Memory.Current == MenuPage.Map);
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
                if (kv.Value.underline != null) kv.Value.underline.enabled = on;
                if (kv.Value.label != null) kv.Value.label.color = on ? waterBlue : mutedTextColor;
            }

            if (view.storageText != null) view.storageText.text = string.Format(storageFormat, logic.Stored, logic.Cap);
            if (view.storageFill != null)
                view.storageFill.anchorMax = new Vector2(logic.Cap > 0f ? Mathf.Clamp01(logic.Stored / logic.Cap) : 0f, view.storageFill.anchorMax.y);
            var balance = BalanceProvider.Current;
            int day = GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Day : 1;
            string stage = balance != null ? balance.GetStage(day).displayName : "";
            if (view.dayText != null) view.dayText.text = string.Format(dayFormat, day, stage.ToUpperInvariant());

            foreach (var card in cards.Values)
                card.Show(logic.GetCard(card.Id), card.Id == selected, this);
            foreach (var (parentId, line) in lines)
                line.color = logic.Owns(parentId) ? waterBlue : lineColor;

            var info = logic.Get(selected);
            var detail = info == null ? null : (info.Tab == UpgradeTab.Build ? buildDetail : skillsDetail);
            if (detail != null) detail.Show(info, this);
        }

        // ------------------------------------------------------------------ Building the UI

        void BuildUI()
        {
            // The menu prefab (dragged in, or Resources/UI/GameMenu), else the built-in look.
            view = UiBuilder.InstantiateView<GameMenuView>(menuPrefab, MenuPrefabResource, transform);
            if (view == null) view = GameMenuView.BuildDefault(transform, this);
            var canvas = view.GetComponentInParent<Canvas>();
            root = canvas != null ? canvas.gameObject : view.gameObject;

            AddTab(MenuPage.Build, view.buildTab);
            AddTab(MenuPage.Skills, view.skillsTab);
            AddTab(MenuPage.Map, view.mapTab);
            if (view.toSkillsButton != null) view.toSkillsButton.onClick.AddListener(() => SwitchTo(MenuPage.Skills));
            if (view.toBuildButton != null) view.toBuildButton.onClick.AddListener(() => SwitchTo(MenuPage.Build));

            // The card prefab (dragged in, or Resources/UI/UpgradeCard), else the built-in card.
            var card = cardPrefab;
            if (card == null)
            {
                var go = Resources.Load<GameObject>(CardPrefabResource);
                if (go != null) card = go.GetComponent<UpgradeCardUI>();
            }

            BuildUpgradePage(view.buildCards, buildLayout, card);
            BuildUpgradePage(view.skillsCards, skillsLayout, card);
            buildDetail = view.buildDetail != null ? new Detail(view.buildDetail, UpgradeTab.Build, this) : null;
            skillsDetail = view.skillsDetail != null ? new Detail(view.skillsDetail, UpgradeTab.Skills, this) : null;

            if (mapScreen != null && view.mapPage != null) mapScreen.HostIn((RectTransform)view.mapPage.transform);
            built = true;
        }

        void AddTab(MenuPage page, GameMenuView.Tab tab)
        {
            if (tab == null) return;
            if (tab.button != null) tab.button.onClick.AddListener(() => SwitchTo(page));
            tabs[page] = tab;
        }

        /// <summary>The icon dragged in for an upgrade id (null if none).</summary>
        public Sprite IconFor(string id)
        {
            foreach (var entry in upgradeIcons)
                if (entry != null && entry.icon != null && string.Equals(entry.id?.Trim(), id, System.StringComparison.OrdinalIgnoreCase))
                    return entry.icon;
            return null;
        }

        // One Build or Skills page: columns of cards (from the layout list) with lines.
        void BuildUpgradePage(GameMenuView.CardArea cardArea, string[] layout, UpgradeCardUI cardPrefabToUse)
        {
            if (cardArea == null || cardArea.area == null) return;
            var area = cardArea.area;
            var lineLayer = cardArea.lineLayer != null ? cardArea.lineLayer : area;
            var cardLayer = cardArea.cardLayer != null ? cardArea.cardLayer : area;

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

            // The card area's design width (1920 × 1080 layout), so the columns are centred in it.
            float areaWidth = UiBuilder.ReferenceWidth - 2f * Margin - DetailWidth - 24f;
            float x = Mathf.Max(0f, (areaWidth - totalWidth) * 0.5f);
            var logic = upgrades.Logic;

            foreach (var col in columns)
            {
                var heading = view.columnHeadingTemplate != null
                    ? Instantiate(view.columnHeadingTemplate, area, false)
                    : UiBuilder.Text("Column", area, "", 20f, mutedTextColor);
                heading.gameObject.name = "Column " + col.title;
                heading.gameObject.SetActive(true);
                heading.text = col.title.ToUpperInvariant();
                UiBuilder.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f),
                                new Vector2(col.lanes * LanePitch, heading.rectTransform.sizeDelta.y > 0f ? heading.rectTransform.sizeDelta.y : 30f));

                var placed = new Dictionary<string, Vector2>();
                foreach (var (id, lane, row) in col.items)
                {
                    var info = logic.Get(id);
                    if (info == null) { Debug.LogWarning($"[GameMenu] Layout lists unknown id '{id}'.", this); continue; }
                    // Centre of the card, measured from the top-left of the card area (y goes down).
                    var centre = new Vector2(x + lane * LanePitch + UpgradeCardView.Width * 0.5f,
                                             -(48f + row * RowPitch + UpgradeCardView.Height * 0.5f));
                    string cardId = id;
                    var card = new UpgradeCardView(info, logic.CostOf(info), cardLayer, this, cardPrefabToUse, IconFor(id), () => Select(cardId));
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
        }

        // A line between two points (pixels from the top-left of the card area), copied from the
        // prefab's Line Template (its width = the line's thickness).
        Image MakeLine(RectTransform parent, Vector2 a, Vector2 b)
        {
            Image img;
            float thickness = 2f;
            if (view.lineTemplate != null)
            {
                img = Instantiate(view.lineTemplate, parent, false);
                img.gameObject.SetActive(true);
                thickness = Mathf.Max(1f, view.lineTemplate.rectTransform.sizeDelta.x);
            }
            else img = UiBuilder.Image("Line", parent, lineColor);
            img.gameObject.name = "Line";
            var rt = img.rectTransform;
            Vector2 d = b - a;
            UiBuilder.Place(rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(thickness, d.magnitude));
            // A thin tall rectangle, turned to point from a to b.
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

        /// <summary>Fills a page's detail panel (UpgradeDetailView in the prefab) for the selected card.</summary>
        class Detail
        {
            readonly UpgradeDetailView v;
            readonly GameMenu menu;
            readonly UpgradeTab tab;
            string shownId;

            public Detail(UpgradeDetailView view, UpgradeTab tab, GameMenu m)
            {
                v = view;
                menu = m;
                this.tab = tab;
                if (v.buyButton != null) v.buyButton.onClick.AddListener(OnBuy);
            }

            static void Set(TMP_Text t, string text) { if (t != null) t.text = text; }

            public void Show(UpgradeInfo info, GameMenu m)
            {
                shownId = info.Id;
                var logic = m.upgrades.Logic;
                var card = logic.GetCard(info.Id);

                var icon = m.IconFor(info.Id);
                if (v.bigIcon != null && icon != null) { v.bigIcon.sprite = icon; v.bigIcon.color = Color.white; v.bigIcon.preserveAspect = true; }
                else if (v.bigIcon != null && v.bigIcon.sprite != null && IsUpgradeIcon(m, v.bigIcon.sprite)) { v.bigIcon.sprite = null; v.bigIcon.color = m.lineColor; }
                if (v.iconIdText != null) { v.iconIdText.text = info.Id; v.iconIdText.gameObject.SetActive(icon == null); }

                Set(v.groupText, (info.Group ?? "").ToUpperInvariant());
                Set(v.nameText, info.Name);
                Set(v.effectText, info.Tooltip);
                Set(v.costText, string.Format(m.litresFormat, card.Cost));

                string mark = m.FontHas(m.ownedMark) ? m.ownedMark : m.ownedMarkFallback;
                var parts = new List<string>();
                foreach (var need in info.Needs) parts.Add(logic.Owns(need) ? need + mark : need);
                Set(v.needsText, parts.Count == 0 ? m.needsNone : string.Join(", ", parts));
                Set(v.afterText, card.State == CardState.Owned ? "-" : string.Format(m.afterFormat, Mathf.Max(0f, logic.Stored - card.Cost)));

                bool hereOk = m.upgrades.CanBuyHere;
                string message;
                Color color;
                switch (card.State)
                {
                    case CardState.Owned: message = m.msgOwned; color = m.waterBlue; break;
                    case CardState.Locked: message = string.Format(m.msgLocked, card.MissingId); color = m.mutedTextColor; break;
                    case CardState.NeedsBiggerStorage: message = m.msgBiggerStorage; color = m.warningAmber; break;
                    case CardState.ShortOfWater: message = string.Format(m.msgShort, card.ShortBy); color = m.warningAmber; break;
                    default:
                        message = hereOk ? m.msgCanBuy : m.msgNotAtHub;
                        color = hereOk ? m.canBuyBorder : m.warningAmber;
                        break;
                }
                if (v.messageText != null) { v.messageText.text = message; v.messageText.color = color; }

                Set(v.buyLabel, string.Format(tab == UpgradeTab.Build ? m.buyFormat : m.learnFormat, card.Cost));
                if (v.buyButton != null) v.buyButton.interactable = card.State == CardState.CanBuy && hereOk;
            }

            // True if this sprite is one of the upgrade icons (so a previous card's icon is cleared).
            static bool IsUpgradeIcon(GameMenu m, Sprite s)
            {
                foreach (var e in m.upgradeIcons) if (e != null && e.icon == s) return true;
                return false;
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
