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
// Zoom and move (2026-11-12): the scroll wheel zooms in and out around the mouse (1× to
// "Max Zoom"), and holding the left mouse button on the map and dragging moves it. Icons,
// markers and the player arrow keep their size when zoomed. Zoom stays as it was between
// openings; zooming all the way out puts the map back in the middle.
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// From Phase 5 it is also the Map page of the Tab menu; the menu then handles the keys
// ("Handle Own Keys" off).
//
// THE LOOK IS A PREFAB WITH ITS OWN CANVAS: "Map Prefab" (drag and drop; empty =
// Assets/Resources/UI/Map.prefab, see MapView.cs). If none exists yet, the plain built-in
// look is used (Tools > The Last Drop > Build UI Prefabs saves it as the prefab).
// The player arrow, markers and room icons are placed by code; their sprite, colour and
// size are the "Map icons" fields below (drag a sprite in; empty = the built-in shape).
//
// HOW TO EDIT: tile colours (the map picture and its legend) and the words that change while
// playing are in the Inspector under "Colours" and "Text"; fixed words and the screen's
// look are in the prefab. The flash length is "Map Turn Flash Seconds" in GameBalance.
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

        [Header("UI prefab (drag and drop)")]
        [Tooltip("The map screen's look: a prefab with a Canvas and a MapView on its root. Empty = Resources/UI/Map.")]
        public GameObject mapPrefab;

        /// <summary>Where the default map prefab lives inside a Resources folder.</summary>
        public const string MapPrefabResource = "UI/Map";

        [Header("Map icons (drag and drop the look; the code places them)")]
        [Tooltip("The player's arrow (points up = the way they face). Empty = the built-in triangle.")]
        public Sprite playerArrowSprite;
        [Tooltip("Player arrow size in pixels.")] public Vector2 playerArrowSize = new Vector2(18f, 22f);
        [Tooltip("Map markers (F). Empty = the built-in diamond.")]
        public Sprite markerSprite;
        [Tooltip("Marker size in pixels.")] public Vector2 markerSize = new Vector2(16f, 16f);
        [Tooltip("Found rooms. Empty = a plain square. The colour comes from the room (RoomContent).")]
        public Sprite roomIconSprite;
        [Tooltip("Room icon size in pixels.")] public Vector2 roomIconSize = new Vector2(22f, 22f);
        [Tooltip("Show the room's letter (C, J, E) on its icon.")] public bool roomIconLetters = true;
        [Tooltip("Colour of the room letters.")] public Color roomLetterColor = Color.black;

        [Header("Keys")]
        [Tooltip("On: M opens/closes the map and Esc closes it. Turned off when the Tab menu (Phase 5) shows the map as one of its pages.")]
        public bool handleOwnKeys = true;

        [Header("Colours")]
        [Tooltip("On: the colours below are replaced at start by the black-and-white look (black background, " +
                 "white text, grey panel, tiles in shades of grey with darker walls, blue for gates and valves). " +
                 "Off: the colours below are used.")]
        public bool blackAndWhite = true;
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
        [Tooltip("The Journal button.")] public Color journalButtonColor = new Color32(62, 54, 45, 255);

        [Header("Zoom and drag")]
        [Tooltip("Most zoom with the scroll wheel (1 = the whole map fits).")]
        [Min(1f)] public float maxZoom = 6f;
        [Tooltip("How much one scroll-wheel notch zooms (0.2 = 20%).")]
        [Range(0.05f, 1f)] public float zoomStep = 0.2f;

        [Header("Text (what the player reads; fixed words are used when the prefab is first made)")]
        [Tooltip("Title in the top-left corner.")] public string title = "MAP";
        [Tooltip("{0} = map %.")] public string percentFormat = "Map {0:0.0}%";
        [Tooltip("{0} = 0, 90, 180 or 270.")] public string zone2Format = "Zone 2: {0}°";
        [Tooltip("Shown next to the turn indicator while zone 2 tiles flash.")] public string turnedNote = "  turned!";
        [Tooltip("{0} = markers placed, {1} = most allowed.")] public string markersFormat = "Markers {0}/{1}   (F)";
        [Tooltip("Legend heading.")] public string legendTitle = "LEGEND";
        [Tooltip("Key hint at the bottom of the panel.")] public string closeHint = "M / Esc   Close";
        [Tooltip("Mouse hint above the journal button.")] public string zoomHint = "Scroll   Zoom\nDrag     Move";
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
        RectTransform mapArea;      // the space the map is shown in (clips the zoomed map)
        RectTransform zoomRoot;     // scaled and moved to zoom / drag the map
        RectTransform mapRect;      // the square map picture
        RawImage mapImage;
        Texture2D texture;
        Color32[] pixels;
        RectTransform playerArrow, zone2Arrow, markerLayer, iconLayer;
        MapView view;
        // Kept apart from the MapView: inside the Tab menu the prefab's root (and its MapView) is removed.
        Image playerArrowImage, zone2ArrowImage;
        Image[] legendSwatches = new Image[0];
        readonly List<Image> markerIcons = new List<Image>();
        readonly List<Image> extraIconImages = new List<Image>();
        TMP_Text percentText, zone2Text, markersText, titleText;
        readonly List<(Image line, bool horizontal)> frameLines = new List<(Image, bool)>();
        float zoom = 1f;
        Vector2 pan;                // zoomRoot's offset from the middle of the map area (pixels)
        bool dragging;
        Vector2 lastDragPoint;
        bool framesBuilt;
        bool flashPending;
        float flashUntil;

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            if (blackAndWhite) ApplyBlackAndWhite();
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

        // Black background, white text, grey panel. Tiles go from black (not seen) through dark
        // walls to lighter floor, rooms and base; gates and valves keep the blue water accent
        // (that's where water is paid), markers and the player are white.
        /// <summary>The black-and-white colours (also used by Tools > Build UI Prefabs).</summary>
        public void ApplyBlackAndWhite()
        {
            unrevealedColor = Color.black;
            wallColor = new Color32(46, 46, 46, 255);            // #2E2E2E darkest
            floorColor = new Color32(112, 112, 112, 255);        // #707070
            roomColor = new Color32(160, 160, 160, 255);         // #A0A0A0
            baseColor = new Color32(205, 205, 205, 255);         // #CDCDCD
            gateClosedColor = new Color32(63, 169, 245, 255);    // #3FA9F5 water blue
            gateOpenColor = new Color32(166, 212, 247, 255);     // #A6D4F7 light blue
            valveColor = new Color32(31, 111, 178, 255);         // #1F6FB2 deep blue
            playerColor = Color.white;
            markerColor = Color.white;
            zone2OutlineColor = new Color(1f, 1f, 1f, 0.35f);
            flashColor = Color.white;
            screenColor = new Color32(10, 10, 10, 255);          // #0A0A0A
            panelColor = new Color32(28, 28, 28, 255);           // #1C1C1C
            textColor = Color.white;
            mutedTextColor = new Color32(160, 160, 160, 255);    // #A0A0A0
            journalButtonColor = new Color32(56, 56, 56, 255);   // #383838
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
            if (IsOpen) HandleZoomAndDrag();
            else dragging = false;
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
            ApplyIconLook();
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
            if (texture == null) return;

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
            if (percentText != null) percentText.text = string.Format(percentFormat, reveal.MapPercent);
            if (zone2Text != null) zone2Text.text = string.Format(zone2Format, turns * 90) + (flashing ? turnedNote : "");
            // The arrow points up at 0 degrees and turns clockwise with zone 2 (UI angles go anticlockwise, hence the minus).
            if (zone2Arrow != null) zone2Arrow.localEulerAngles = new Vector3(0f, 0f, -90f * turns);
            if (markersText != null) markersText.text = string.Format(markersFormat, reveal.MarkersLocal.Count, tracker.MaxMarkers);
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
            if (mapImage == null) return;
            if (texture != null && texture.width == grid.Width && texture.height == grid.Height) return;
            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // sharp square tiles, no blur
                wrapMode = TextureWrapMode.Clamp,
                name = "MapTexture"
            };
            pixels = new Color32[grid.Width * grid.Height];
            mapImage.texture = texture;
            var fitter = mapRect.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = (float)grid.Width / grid.Height;
        }

        // Where a tile position (whole numbers = tile centres) sits on the map picture, 0-1 each way.
        static Vector2 MapPoint(MazeGrid grid, float tileX, float tileY) =>
            new Vector2((tileX + 0.5f) / grid.Width, 1f - (tileY + 0.5f) / grid.Height);

        void DrawPlayer(MazeGrid grid)
        {
            if (playerArrow == null) return;
            var player = Player_Manager.instance;
            bool show = player != null && maze != null;
            playerArrow.gameObject.SetActive(show);
            if (!show) return;

            Vector2 t = maze.WorldToGridFloat(player.transform.position);
            Vector2 p = MapPoint(grid, t.x, t.y);
            playerArrow.anchorMin = playerArrow.anchorMax = p;
            playerArrow.anchoredPosition = Vector2.zero;
            playerArrow.localScale = IconScale;

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
                    markerIcons.Add(icon);
                }
                var rt = markerIcons[i].rectTransform;
                UiBuilder.Place(rt, MapPoint(grid, tile.x, tile.y), new Vector2(0.5f, 0.5f), Vector2.zero, markerSize);
                markerIcons[i].sprite = markerSprite != null ? markerSprite : UiBuilder.DiamondSprite;
                markerIcons[i].color = markerColor;
                rt.localScale = IconScale;
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
                        var letter = UiBuilder.Text("Letter", img.transform, "", 16f, roomLetterColor, TextAlignmentOptions.Center);
                        letter.fontStyle = FontStyles.Bold;
                        extraIconImages.Add(img);
                    }
                    var image = extraIconImages[i];
                    UiBuilder.Place(image.rectTransform, MapPoint(grid, icon.worldTile.x, icon.worldTile.y),
                        new Vector2(0.5f, 0.5f), Vector2.zero, roomIconSize);
                    image.sprite = roomIconSprite;
                    image.preserveAspect = roomIconSprite != null;
                    image.color = icon.color;
                    image.rectTransform.localScale = IconScale;
                    var letterText = image.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (letterText != null)
                    {
                        letterText.text = icon.letter;
                        letterText.color = roomLetterColor;
                        letterText.gameObject.SetActive(roomIconLetters);
                    }
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
            if (playerArrow != null) playerArrow.SetAsLastSibling(); // keep the player on top
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
                frameLines.Add((img, size.y > 0f));
                var rt = img.rectTransform;
                rt.anchorMin = from;
                rt.anchorMax = to;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = size;
            }
        }

        // ------------------------------------------------------------------ Zoom and drag

        // Icons, markers and the arrow are children of the zoomed map: undo the zoom on them so
        // they stay the same size on screen.
        Vector3 IconScale => Vector3.one / Mathf.Max(1f, zoom);

        void HandleZoomAndDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null || mapArea == null || zoomRoot == null) return;

            Camera cam = CanvasCamera();
            Vector2 screen = mouse.position.ReadValue();
            bool overMap = RectTransformUtility.RectangleContainsScreenPoint(mapArea, screen, cam);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, screen, cam, out Vector2 local);

            // Scroll wheel: zoom in / out, keeping the spot under the mouse where it is.
            float scroll = mouse.scroll.ReadValue().y;
            if (overMap && Mathf.Abs(scroll) > 0.01f)
            {
                float newZoom = Mathf.Clamp(zoom * (scroll > 0f ? 1f + zoomStep : 1f / (1f + zoomStep)), 1f, Mathf.Max(1f, maxZoom));
                // The map point under the mouse (before zooming), then put it back under the mouse.
                Vector2 point = (local - pan) / zoom;
                zoom = newZoom;
                pan = local - point * zoom;
            }

            // Left mouse held on the map: drag it around.
            if (mouse.leftButton.wasPressedThisFrame && overMap)
            {
                dragging = true;
                lastDragPoint = local;
            }
            if (!mouse.leftButton.isPressed) dragging = false;
            if (dragging)
            {
                pan += local - lastDragPoint;
                lastDragPoint = local;
            }

            ApplyZoom();
        }

        // Keeps the zoomed map covering the area (no empty space dragged in) and applies it.
        void ApplyZoom()
        {
            Vector2 half = mapArea.rect.size * 0.5f;
            Vector2 limit = half * (zoom - 1f);
            pan = new Vector2(Mathf.Clamp(pan.x, -limit.x, limit.x), Mathf.Clamp(pan.y, -limit.y, limit.y));

            zoomRoot.localScale = new Vector3(zoom, zoom, 1f);
            zoomRoot.anchoredPosition = pan;

            // The zone 2 outline stays a thin line at any zoom.
            float thickness = 2f / zoom;
            foreach (var (line, horizontal) in frameLines)
                if (line != null)
                    line.rectTransform.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
        }

        /// <summary>Back to the whole map, centred.</summary>
        public void ResetZoom()
        {
            zoom = 1f;
            pan = Vector2.zero;
            dragging = false;
            if (zoomRoot != null && mapArea != null) ApplyZoom();
        }

        Camera CanvasCamera()
        {
            var canvas = mapArea.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        // ------------------------------------------------------------------ Building the UI

        void BuildUI()
        {
            // The map prefab (dragged in, or Resources/UI/Map), else the built-in look.
            view = UiBuilder.InstantiateView<MapView>(mapPrefab, MapPrefabResource, transform);
            if (view == null) view = MapView.BuildDefault(transform, this);
            var canvas = view.GetComponentInParent<Canvas>();
            root = canvas != null ? canvas.gameObject : view.gameObject;

            content = view.content != null ? view.content : (RectTransform)view.transform;
            titleText = view.titleText;
            mapArea = view.mapArea;
            zoomRoot = view.zoomRoot;
            mapImage = view.mapImage;
            mapRect = mapImage != null ? mapImage.rectTransform : null;
            markerLayer = view.markerLayer != null ? view.markerLayer : mapRect;
            iconLayer = view.iconLayer != null ? view.iconLayer : mapRect;
            playerArrow = view.playerArrow != null ? view.playerArrow.rectTransform : null;
            percentText = view.percentText;
            zone2Text = view.zone2Text;
            zone2Arrow = view.zone2Arrow;
            markersText = view.markersText;
            if (view.journalButton != null) view.journalButton.onClick.AddListener(() => JournalRequested?.Invoke());
            playerArrowImage = view.playerArrow;
            zone2ArrowImage = zone2Arrow != null ? zone2Arrow.GetComponent<Image>() : null;
            legendSwatches = view.legendSwatches ?? new Image[0];

            if (mapImage == null)
                Debug.LogError("[MapScreen] The map prefab has no Map Image (RawImage) linked on its MapView: the map can't be drawn.", this);
            ApplyIconLook();
        }

        // Applies the "Map icons" look to the player arrow and the side panel's zone 2 arrow, and
        // colours the legend from the tile colours so it always matches the map.
        void ApplyIconLook()
        {
            if (playerArrowImage != null)
            {
                if (playerArrowSprite != null) playerArrowImage.sprite = playerArrowSprite;
                else if (playerArrowImage.sprite == null) playerArrowImage.sprite = UiBuilder.ArrowSprite;
                playerArrowImage.color = playerColor;
                playerArrowImage.rectTransform.sizeDelta = playerArrowSize;
            }
            if (zone2ArrowImage != null && zone2ArrowImage.sprite == null) zone2ArrowImage.sprite = UiBuilder.ArrowSprite;

            Color[] swatches = { floorColor, baseColor, wallColor, gateClosedColor, gateOpenColor, roomColor, valveColor, markerColor, playerColor, zone2OutlineColor };
            for (int i = 0; i < legendSwatches.Length && i < swatches.Length; i++)
            {
                var sw = legendSwatches[i];
                if (sw == null) continue;
                sw.color = swatches[i];
                if (i == 7) sw.sprite = markerSprite != null ? markerSprite : UiBuilder.DiamondSprite;
                if (i == 8) sw.sprite = playerArrowSprite != null ? playerArrowSprite : UiBuilder.ArrowSprite;
                sw.preserveAspect = i == 7 || i == 8;
            }
        }
    }
}
