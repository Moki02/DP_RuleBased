// ============================================================================
// MapView.cs
// Goes on the root of the MAP PREFAB (Assets/Resources/UI/Map.prefab, or whichever prefab is
// dragged into MapScreen > "Map Prefab"). The prefab has its own Canvas, so the artists can
// open it and restyle or move the background, title, map frame, side panel, texts, legend
// and Journal button. On the Tab menu's Map page, "Content" is moved into the menu.
//
// What stays in code: the map picture itself (drawn tile by tile from the maze into Map
// Image), and WHERE the player arrow, markers and room icons sit. Their LOOK (sprite, colour,
// size) is set on MapScreen in the Inspector ("Map icons"), with drag-and-drop sprite slots.
//
// The slots below tell the code which part is which (any may be empty):
//   Content          - the whole map screen (moved into the Tab menu's Map page);
//   Title Text       - hidden when the map is shown inside the Tab menu (the menu has tabs);
//   Map Area         - the space the map is shown in; it should clip (RectMask2D) so the
//                      zoomed map doesn't spill out. Scroll to zoom and drag to move happen here;
//   Zoom Root        - a child of Map Area filling it, scaled and moved by zoom / drag;
//   Map Image        - a RawImage inside Zoom Root (with an Aspect Ratio Fitter, Fit In Parent),
//                      showing the drawn maze;
//   Marker Layer / Icon Layer - children of Map Image the markers and room icons are put in;
//   Player Arrow     - an Image inside Map Image, moved and turned to the player;
//   Percent / Zone 2 / Markers texts, Zone 2 Arrow (turns with zone 2);
//   Legend Swatches  - 10 images, in this order: floor, base, wall, gate shut, gate open, room,
//                      valve, marker, you, zone 2 edge. Coloured from MapScreen's colours so
//                      the legend always matches the map;
//   Journal Button   - opens the survivor notes journal (R8.7).
//
// DESIGN RULES: R8.1, R8.4, R8.7, R8.18, R8.28 (see MapScreen).
//
// HOW TO USE: on the prefab's root (the one with the Canvas). Leave the Journal button's On
// Click list empty: the code wires it.
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the map prefab.</summary>
    public class MapView : MonoBehaviour
    {
        [Header("Screen")]
        [Tooltip("The whole map screen. Moved into the Tab menu's Map page.")] public RectTransform content;
        [Tooltip("\"MAP\". Hidden inside the Tab menu.")] public TMP_Text titleText;

        [Header("Map picture")]
        [Tooltip("Where the map is shown; clips the zoomed map. Scroll / drag happen over it.")] public RectTransform mapArea;
        [Tooltip("Child of Map Area, filling it. Scaled and moved to zoom and drag.")] public RectTransform zoomRoot;
        [Tooltip("RawImage showing the drawn maze (needs an Aspect Ratio Fitter, Fit In Parent).")] public RawImage mapImage;
        [Tooltip("Markers (F) are put in here. A child of Map Image, filling it.")] public RectTransform markerLayer;
        [Tooltip("Room icons are put in here. A child of Map Image, filling it.")] public RectTransform iconLayer;
        [Tooltip("The player's arrow. A child of Map Image. Its sprite / colour / size come from MapScreen > Map icons.")] public Image playerArrow;

        [Header("Side panel")]
        [Tooltip("\"Map 42.0%\"")] public TMP_Text percentText;
        [Tooltip("\"Zone 2: 90°\"")] public TMP_Text zone2Text;
        [Tooltip("Turns with zone 2 (points up at 0°).")] public RectTransform zone2Arrow;
        [Tooltip("\"Markers 2/5 (F)\"")] public TMP_Text markersText;
        [Tooltip("Floor, base, wall, gate shut, gate open, room, valve, marker, you, zone 2 edge (in this order).")]
        public Image[] legendSwatches = new Image[0];
        [Tooltip("Opens the journal.")] public Button journalButton;

        /// <summary>Builds the default black-and-white map screen (used for the prefab, or when there is none).</summary>
        public static MapView BuildDefault(Transform parent, MapScreen m)
        {
            var canvas = UiBuilder.CreateCanvas("Map", parent, 60);
            var v = canvas.gameObject.AddComponent<MapView>();
            v.content = UiBuilder.Rect("Map Screen", canvas.transform);
            UiBuilder.Image("Background", v.content, m.screenColor, blocksClicks: true);

            v.titleText = UiBuilder.Text("Title", v.content, m.title, 44f, m.textColor);
            v.titleText.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -36f), new Vector2(800f, 60f));

            // The map picture: as big a square as fits left of the side panel.
            v.mapArea = UiBuilder.Rect("Map Area", v.content);
            UiBuilder.Fill(v.mapArea, 60f, 440f, 110f, 60f);
            v.mapArea.gameObject.AddComponent<RectMask2D>();   // the zoomed map is cut off at the area's edge
            v.zoomRoot = UiBuilder.Rect("Zoom Root", v.mapArea);
            var mapRect = UiBuilder.Rect("Map Image", v.zoomRoot);
            var fitter = mapRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            v.mapImage = mapRect.gameObject.AddComponent<RawImage>();
            v.mapImage.color = Color.white;
            v.mapImage.raycastTarget = false;

            v.markerLayer = UiBuilder.Rect("Marker Layer", mapRect);
            v.iconLayer = UiBuilder.Rect("Icon Layer", mapRect);
            v.playerArrow = UiBuilder.Image("Player Arrow", mapRect, m.playerColor);
            UiBuilder.Place(v.playerArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, m.playerArrowSize);

            BuildSidePanel(v, m);
            return v;
        }

        static void BuildSidePanel(MapView v, MapScreen m)
        {
            var panel = UiBuilder.Image("Side Panel", v.content, m.panelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.offsetMin = new Vector2(-400f, 60f);   // 360 px wide, 40 px from the right edge
            panel.offsetMax = new Vector2(-40f, -110f);

            v.percentText = TopLeftText(panel, "Map Percent", "Map 0.0%", 34f, m.textColor, 24f, -24f, 320f, 48f);
            v.percentText.fontStyle = FontStyles.Bold;

            var arrowImg = UiBuilder.Image("Zone 2 Arrow", panel, m.textColor);
            v.zone2Arrow = arrowImg.rectTransform;
            UiBuilder.Place(v.zone2Arrow, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(44f, -110f), new Vector2(28f, 34f));
            v.zone2Text = TopLeftText(panel, "Zone 2", "Zone 2: 0°", 28f, m.textColor, 76f, -92f, 280f, 40f);

            v.markersText = TopLeftText(panel, "Markers", "Markers 0/5", 22f, m.mutedTextColor, 24f, -150f, 320f, 32f);

            TopLeftText(panel, "Legend Title", m.legendTitle, 20f, m.mutedTextColor, 24f, -210f, 320f, 30f);
            int count = Mathf.Min(10, m.legendLabels.Length);
            v.legendSwatches = new Image[count];
            for (int i = 0; i < count; i++)
            {
                float y = -248f - i * 34f;
                var sw = UiBuilder.Image("Swatch " + m.legendLabels[i], panel, Color.white);
                UiBuilder.Place(sw.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(22f, 22f));
                v.legendSwatches[i] = sw;
                TopLeftText(panel, "Label " + m.legendLabels[i], m.legendLabels[i], 20f, m.textColor, 60f, y + 2f, 280f, 28f);
            }

            v.journalButton = UiBuilder.Button("Journal Button", panel, m.journalLabel, 22f, m.journalButtonColor, m.textColor, null);
            UiBuilder.Place((RectTransform)v.journalButton.transform, Vector2.zero, Vector2.zero, new Vector2(24f, 64f), new Vector2(200f, 40f));

            var mouseHint = UiBuilder.Text("Zoom Hint", panel, m.zoomHint, 20f, m.mutedTextColor, TextAlignmentOptions.BottomLeft);
            UiBuilder.Place(mouseHint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 116f), new Vector2(320f, 56f));

            var hint = UiBuilder.Text("Close Hint", panel, m.closeHint, 20f, m.mutedTextColor, TextAlignmentOptions.BottomLeft);
            UiBuilder.Place(hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 20f), new Vector2(320f, 30f));
        }

        static TMP_Text TopLeftText(RectTransform parent, string name, string text, float size, Color color,
                                    float x, float y, float width, float height)
        {
            var t = UiBuilder.Text(name, parent, text, size, color);
            UiBuilder.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            return t;
        }
    }
}
