// ============================================================================
// MapScreen.cs
// The full-screen map (the GDD's 2D tile map, also called the "mini map"). Opens with M
// and closes with M or Esc. The clock keeps running while it's open; the player can't
// move. Each maze tile is one pixel of a 95 × 95 picture, scaled up with sharp edges.
//
// Shows: revealed tiles in colour (unrevealed = black), the player as a white arrow
// pointing the way they face, map markers, a thin outline around zone 2, a legend,
// "Map %" (revealed walkable tiles out of all walkable tiles), and the zone 2 turn
// indicator ("Zone 2: 90°" with an arrow). The first time the map opens after zone 2
// turns, the revealed zone 2 tiles flash for 2 s so the player sees what moved.
//
// DESIGN RULES: R8.1 (reveal), R8.4 (markers), R8.28 (knowledge turns with zone 2), new
// rule: everyone gets the zone 2 turn indicator. Batch 5 added: R8.18 room icons (through
// extraIcons), R8.7 the journal (J or the Journal button fire JournalRequested), and V2
// Landmarks (effect "gatesOnMap"): gates show on the map even before they are revealed.
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// It builds its own canvas when the game starts. From Phase 5 it is also the Map page of
// the Tab menu; the menu then handles the keys ("Handle Own Keys" off).
// HOW TO EDIT: colours and every word the player sees are in the Inspector under
// "Colours" and "Text". The flash length is "Map Turn Flash Seconds" in GameBalance.
// Debug key 0 reveals the whole map.
// ============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>The full-screen tile map.</summary>
    public class MapScreen : MonoBehaviour
    {
        [Header("Links (empty = found by themselves)")]
        [Tooltip("Gives the revealed tiles and markers.")]
        public MapRevealTracker tracker;
        [Tooltip("Tells the map when zone 2 turns (for the flash and the turn indicator).")]
        public Zone2Rotator rotator;
        [Tooltip("The Maze object (to place the player arrow).")]
        public MazeBuilder maze;

        [Header("Keys")]
        [Tooltip("On: M opens/closes the map and Esc closes it. Turned off when the Tab menu (Phase 5) shows the map as one of its pages.")]
        public bool handleOwnKeys = true;

        [Header("Colours")]
        [Tooltip("Tiles the player hasn't seen yet.")] public Color unrevealedColor = Color.black;
        [Tooltip("Corridor floor.")] public Color floorColor = new Color32(92, 75, 54, 255);
        [Tooltip("The hub base (S tiles).")] public Color baseColor = new Color32(122, 100, 71, 255);
        [Tooltip("Walls.")] public Color wallColor = new Color32(94, 107, 43, 255);
        [Tooltip("A gate that is shut.")] public Color gateClosedColor = new Color32(47, 111, 214, 255);
        [Tooltip("A gate that is open.")] public Color gateOpenColor = new Color32(143, 203, 255, 255);
        [Tooltip("Room floor.")] public Color roomColor = new Color32(62, 142, 78, 255);
        [Tooltip("Valve gates.")] public Color valveColor = new Color32(240, 138, 36, 255);
        [Tooltip("The player's arrow.")] public Color playerColor = Color.white;
        [Tooltip("Map markers (F).")] public Color markerColor = new Color32(255, 213, 74, 255);
        [Tooltip("The thin line around zone 2.")] public Color zone2OutlineColor = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("Zone 2 tiles blink to this colour after a turn.")] public Color flashColor = new Color32(255, 242, 176, 255);
        [Tooltip("Screen background.")] public Color screenColor = new Color32(27, 23, 19, 255);
        [Tooltip("Side panel background.")] public Color panelColor = new Color32(36, 31, 26, 255);
        [Tooltip("Main text.")] public Color textColor = new Color32(242, 233, 220, 255);
        [Tooltip("Small grey text.")] public Color mutedTextColor = new Color32(168, 153, 138, 255);

        [Header("Text (what the player reads)")]
        [Tooltip("Title in the top-left corner.")] public string title = "MAP";
        [Tooltip("{0} = map %.")] public string percentFormat = "Map {0:0.0}%";
        [Tooltip("{0} = 0, 90, 180 or 270.")] public string zone2Format = "Zone 2: {0}°";
        [Tooltip("Shown next to the turn indicator while zone 2 tiles flash.")] public string turnedNote = "  turned!";
        [Tooltip("{0} = markers placed, {1} = most allowed.")] public string markersFormat = "Markers {0}/{1}   (F)";
        [Tooltip("Legend heading.")] public string legendTitle = "LEGEND";
        [Tooltip("Key hint at the bottom of the panel.")] public string closeHint = "M / Esc   Close";
        [Tooltip("The journal button above the key hint (batch 5, R8.7).")] public string journalLabel = "J   Journal";
        [Tooltip("Legend labels, in this order: floor, base, wall, gate shut, gate open, room, valve, marker, you, zone 2 edge.")]
        public string[] legendLabels = { "Floor", "Base", "Wall", "Gate (shut)", "Gate (open)", "Room", "Valve", "Marker", "You", "Zone 2 edge" };

        /// <summary>
        /// Phase 4 sets this: returns true if the gate on this world tile is open right now.
        /// Empty = every gate is drawn shut.
        /// </summary>
        public Func<Vector2Int, bool> isGateTileOpen;

        /// <summary>
        /// Batch 5 (R8.18): extra icons to draw on the map, such as found rooms. Return world tiles.
        /// Empty = no extra icons.
        /// </summary>
        public Func<IEnumerable<MapIcon>> extraIcons;

        /// <summary>Batch 5 (R8.7): fired when the player presses J or the Journal button while the map is open.</summary>
        public event Action JournalRequested;

        /// <summary>True while the map is on screen.</summary>
        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>The map's whole screen. The Tab menu (Phase 5) moves this into its Map page.</summary>
        public RectTransform Content => content;

        GameBalance balance;
        GameObject root;            // the canvas (standalone) that holds everything
        RectTransform content;      // the map screen itself
        RectTransform mapRect;      // the square map picture
        RawImage mapImage;
        Texture2D texture;
        Color32[] pixels;
        RectTransform playerArrow, zone2Arrow, markerLayer, iconLayer;
        readonly List<Image> markerIcons = new List<Image>();
        readonly List<Image> extraIconImages = new List<Image>();
        TextMeshProUGUI percentText, zone2Text, markersText, titleText;
        bool framesBuilt;
        bool flashPending;
        float flashUntil;

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            BuildUI();
            root.SetActive(false);
        }

        void Start()
        {
            balance = BalanceProvider.Current;
            if (tracker == null) tracker = FindAnyObjectByType<MapRevealTracker>();
            if (rotator == null) rotator = FindAnyObjectByType<Zone2Rotator>();
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (rotator != null) rotator.Zone2Turned += OnZone2Turned;
        }

        void OnDestroy()
        {
            if (rotator != null) rotator.Zone2Turned -= OnZone2Turned;
            if (texture != null) Destroy(texture);
        }

        void OnZone2Turned(int oldTurns, int newTurns)
        {
            flashPending = true;
            if (IsOpen) StartFlash(); // already looking at the map: flash straight away
        }

        // ------------------------------------------------------------------ Open / close

        void Update()
        {
            if (handleOwnKeys)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.mKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
                    else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
                }
            }
            // J opens the journal (R8.7), also when the map is a page of the Tab menu. J only works
            // while the map is on screen, and no other script uses J (Tab/B/M menu, F marker,
            // E use, Q drink, digits and F-keys for debug).
            var keys = Keyboard.current;
            if (IsOpen && keys != null && keys.jKey.wasPressedThisFrame) JournalRequested?.Invoke();
            if (IsOpen) Redraw();
        }

        /// <summary>Opens the map on its own (M key). False if a minigame, pause or another menu is up.</summary>
        public bool Open()
        {
            if (IsOpen) return true;
            if (!MenuState.Enter()) return false;
            Show();
            return true;
        }

        /// <summary>Closes the map and gives control back to the player.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            MenuState.Exit();
        }

        /// <summary>Shows the map without changing the game state (the Tab menu uses this).</summary>
        public void Show()
        {
            root.SetActive(true);
            if (flashPending) StartFlash();
            Redraw();
        }

        /// <summary>Hides the map without changing the game state (the Tab menu uses this).</summary>
        public void Hide() => root.SetActive(false);

        /// <summary>
        /// Moves the map into another screen (the Tab menu's Map page) and stops handling its
        /// own keys. The menu's header replaces the map's title, and the map's own canvas goes.
        /// </summary>
        public void HostIn(RectTransform page)
        {
            if (content.parent == page) return;
            var oldCanvas = root;
            content.SetParent(page, false);
            UiBuilder.Fill(content);
            root = content.gameObject;        // Show/Hide/IsOpen now switch the map content itself
            root.SetActive(false);
            if (oldCanvas != null && oldCanvas != root) Destroy(oldCanvas);
            if (titleText != null) titleText.gameObject.SetActive(false);
            handleOwnKeys = false;
        }

        void StartFlash()
        {
            flashPending = false;
            flashUntil = Time.unscaledTime + (balance != null ? balance.mapTurnFlashSeconds : 0f);
        }

        // ------------------------------------------------------------------ Drawing

        void Redraw()
        {
            var reveal = tracker != null ? tracker.Reveal : null;
            if (reveal == null) return;
            var grid = reveal.Grid;
            EnsureTexture(grid);

            bool flashing = Time.unscaledTime < flashUntil;
            // Blink about 3 times a second while flashing.
            bool flashOn = flashing && Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0;
            // V2 Landmarks: gates show on the map even before the player has seen them.
            bool gatesOnMap = Skills.B("gatesOnMap", false);

            int w = grid.Width, h = grid.Height;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c;
                if (!reveal.IsRevealed(x, y))
                    c = gatesOnMap && grid.CodeAt(x, y) == TileType.Gate ? TileColor(TileType.Gate, new Vector2Int(x, y)) : unrevealedColor;
                else if (flashOn && grid.IsInZone2(x, y)) c = flashColor;
                else c = TileColor(grid.CodeAt(x, y), new Vector2Int(x, y));
                // Texture rows start at the BOTTOM, but tile row 0 is the TOP (north), so flip y.
                pixels[(h - 1 - y) * w + x] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);

            DrawPlayer(grid);
            DrawMarkers(grid, reveal);
            DrawExtraIcons(grid);
            if (!framesBuilt) BuildZone2Outline(grid);

            int turns = grid.Zone2Turns;
            percentText.text = string.Format(percentFormat, reveal.MapPercent);
            zone2Text.text = string.Format(zone2Format, turns * 90) + (flashing ? turnedNote : "");
            // The arrow points up at 0 degrees and turns clockwise with zone 2 (UI angles go anticlockwise, hence the minus).
            zone2Arrow.localEulerAngles = new Vector3(0f, 0f, -90f * turns);
            markersText.text = string.Format(markersFormat, reveal.MarkersLocal.Count, tracker.MaxMarkers);
        }

        Color TileColor(TileType code, Vector2Int worldTile)
        {
            switch (code)
            {
                case TileType.Wall: return wallColor;
                case TileType.Room: return roomColor;
                case TileType.Spawn: return baseColor;
                case TileType.Valve: return valveColor;
                case TileType.Gate:
                    return isGateTileOpen != null && isGateTileOpen(worldTile) ? gateOpenColor : gateClosedColor;
                default: return floorColor; // floor, doors and timed doors
            }
        }

        void EnsureTexture(MazeGrid grid)
        {
            if (texture != null && texture.width == grid.Width && texture.height == grid.Height) return;
            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // sharp square tiles, no blur
                wrapMode = TextureWrapMode.Clamp,
                name = "MapTexture"
            };
            pixels = new Color32[grid.Width * grid.Height];
            mapImage.texture = texture;
            mapRect.GetComponent<AspectRatioFitter>().aspectRatio = (float)grid.Width / grid.Height;
        }

        // Where a tile position (whole numbers = tile centres) sits on the map picture, 0-1 each way.
        static Vector2 MapPoint(MazeGrid grid, float tileX, float tileY) =>
            new Vector2((tileX + 0.5f) / grid.Width, 1f - (tileY + 0.5f) / grid.Height);

        void DrawPlayer(MazeGrid grid)
        {
            var player = Player_Manager.instance;
            bool show = player != null && maze != null;
            playerArrow.gameObject.SetActive(show);
            if (!show) return;

            Vector2 t = maze.WorldToGridFloat(player.transform.position);
            Vector2 p = MapPoint(grid, t.x, t.y);
            playerArrow.anchorMin = playerArrow.anchorMax = p;
            playerArrow.anchoredPosition = Vector2.zero;

            // The player's compass heading: 0 = north (+Z), 90 = east (+X).
            Vector3 f = player.transform.forward;
            float heading = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            playerArrow.localEulerAngles = new Vector3(0f, 0f, -heading);
        }

        void DrawMarkers(MazeGrid grid, MapReveal reveal)
        {
            int i = 0;
            foreach (var tile in reveal.MarkersWorld())
            {
                if (i >= markerIcons.Count)
                {
                    var icon = UiBuilder.Image("Marker", markerLayer, markerColor);
                    icon.sprite = UiBuilder.DiamondSprite;
                    markerIcons.Add(icon);
                }
                var rt = markerIcons[i].rectTransform;
                UiBuilder.Place(rt, MapPoint(grid, tile.x, tile.y), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
                markerIcons[i].color = markerColor;
                markerIcons[i].gameObject.SetActive(true);
                i++;
            }
            for (; i < markerIcons.Count; i++) markerIcons[i].gameObject.SetActive(false);
        }

        // Batch 5: icons from extraIcons (found rooms, R8.18), each a small square with a letter.
        void DrawExtraIcons(MazeGrid grid)
        {
            int i = 0;
            if (extraIcons != null)
            {
                foreach (var icon in extraIcons())
                {
                    if (i >= extraIconImages.Count)
                    {
                        var img = UiBuilder.Image("RoomIcon", iconLayer, icon.color);
                        var letter = UiBuilder.Text("Letter", img.transform, "", 16f, Color.black, TextAlignmentOptions.Center);
                        letter.fontStyle = FontStyles.Bold;
                        extraIconImages.Add(img);
                    }
                    var image = extraIconImages[i];
                    UiBuilder.Place(image.rectTransform, MapPoint(grid, icon.worldTile.x, icon.worldTile.y),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
                    image.color = icon.color;
                    image.GetComponentInChildren<TextMeshProUGUI>().text = icon.letter;
                    image.gameObject.SetActive(true);
                    i++;
                }
            }
            for (; i < extraIconImages.Count; i++) extraIconImages[i].gameObject.SetActive(false);
        }

        // Two square outlines: zone 2's inner edge (next to zone 1) and its outer edge.
        void BuildZone2Outline(MazeGrid grid)
        {
            framesBuilt = true;
            int c = grid.CentreX;
            AddFrame(grid, c - grid.Rings.zone1Max - 1, c + grid.Rings.zone1Max + 1);  // first and last tile of ring zone1Max + 1
            AddFrame(grid, c - grid.Rings.zone2Max, c + grid.Rings.zone2Max);          // first and last tile of ring zone2Max
            playerArrow.SetAsLastSibling(); // keep the player on top
        }

        // A thin square line around the tiles from 'first' to 'last' (both ways), on the tiles' outer edges.
        void AddFrame(MazeGrid grid, int first, int last)
        {
            float u0 = (float)first / grid.Width, u1 = (float)(last + 1) / grid.Width;
            float v1 = 1f - (float)first / grid.Height, v0 = 1f - (float)(last + 1) / grid.Height;
            const float thickness = 2f; // pixels
            Line(new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(0f, thickness)); // top
            Line(new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(0f, thickness)); // bottom
            Line(new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(thickness, 0f)); // left
            Line(new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(thickness, 0f)); // right

            void Line(Vector2 from, Vector2 to, Vector2 size)
            {
                var img = UiBuilder.Image("Zone2Edge", mapRect, zone2OutlineColor);
                var rt = img.rectTransform;
                rt.anchorMin = from;
                rt.anchorMax = to;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = size;
            }
        }

        // ------------------------------------------------------------------ Building the UI

        void BuildUI()
        {
            var canvas = UiBuilder.CreateCanvas("MapCanvas", transform, 60);
            root = canvas.gameObject;
            content = UiBuilder.Rect("MapScreen", root.transform);
            UiBuilder.Image("Background", content, screenColor, blocksClicks: true);

            titleText = UiBuilder.Text("Title", content, title, 44f, textColor);
            titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -36f), new Vector2(800f, 60f));

            // The map picture: as big a square as fits left of the side panel.
            var area = UiBuilder.Rect("MapArea", content);
            UiBuilder.Fill(area, 60f, 440f, 110f, 60f);
            mapRect = UiBuilder.Rect("Map", area);
            var fitter = mapRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            mapImage = mapRect.gameObject.AddComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            markerLayer = UiBuilder.Rect("Markers", mapRect);
            iconLayer = UiBuilder.Rect("Icons", mapRect);   // batch 5: room icons
            var arrow = UiBuilder.Image("Player", mapRect, playerColor);
            arrow.sprite = UiBuilder.ArrowSprite;
            playerArrow = arrow.rectTransform;
            UiBuilder.Place(playerArrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 22f));

            BuildSidePanel();
        }

        void BuildSidePanel()
        {
            var panel = UiBuilder.Image("SidePanel", content, panelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.offsetMin = new Vector2(-400f, 60f);   // 360 px wide, 40 px from the right edge
            panel.offsetMax = new Vector2(-40f, -110f);

            percentText = TopLeftText(panel, "MapPercent", "", 34f, textColor, 24f, -24f, 320f, 48f);
            percentText.fontStyle = FontStyles.Bold;

            var arrowImg = UiBuilder.Image("Zone2Arrow", panel, textColor);
            arrowImg.sprite = UiBuilder.ArrowSprite;
            zone2Arrow = arrowImg.rectTransform;
            UiBuilder.Place(zone2Arrow, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(44f, -110f), new Vector2(28f, 34f));
            zone2Text = TopLeftText(panel, "Zone2", "", 28f, textColor, 76f, -92f, 280f, 40f);

            markersText = TopLeftText(panel, "Markers", "", 22f, mutedTextColor, 24f, -150f, 320f, 32f);

            TopLeftText(panel, "LegendTitle", legendTitle, 20f, mutedTextColor, 24f, -210f, 320f, 30f);
            Color[] swatches = { floorColor, baseColor, wallColor, gateClosedColor, gateOpenColor, roomColor, valveColor, markerColor, playerColor, zone2OutlineColor };
            for (int i = 0; i < swatches.Length && i < legendLabels.Length; i++)
            {
                float y = -248f - i * 34f;
                var sw = UiBuilder.Image("Swatch", panel, swatches[i]);
                if (i == 7) sw.sprite = UiBuilder.DiamondSprite;
                if (i == 8) sw.sprite = UiBuilder.ArrowSprite;
                UiBuilder.Place(sw.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(22f, 22f));
                TopLeftText(panel, "Label", legendLabels[i], 20f, textColor, 60f, y + 2f, 280f, 28f);
            }

            // Batch 5 (R8.7): the journal of survivor notes read so far.
            var journal = UiBuilder.Button("Journal", panel, journalLabel, 22f, new Color32(62, 54, 45, 255), textColor,
                () => JournalRequested?.Invoke());
            UiBuilder.Place((RectTransform)journal.transform, Vector2.zero, Vector2.zero, new Vector2(24f, 64f), new Vector2(200f, 40f));

            var hint = UiBuilder.Text("Hint", panel, closeHint, 20f, mutedTextColor, TextAlignmentOptions.BottomLeft);
            UiBuilder.Place(hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 20f), new Vector2(320f, 30f));
        }

        static TextMeshProUGUI TopLeftText(RectTransform parent, string name, string text, float size, Color color,
                                           float x, float y, float width, float height)
        {
            var t = UiBuilder.Text(name, parent, text, size, color);
            UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            return t;
        }
    }
}
