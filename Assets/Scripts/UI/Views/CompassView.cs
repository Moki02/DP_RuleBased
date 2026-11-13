// ============================================================================
// CompassView.cs
// Goes on the root of the COMPASS PREFAB (Assets/Resources/UI/Compass.prefab): the strip at
// the top of the screen that slides as the player turns (N, NE, E, SE, S, SW, W, NW with small
// ticks between). The prefab has its own Canvas, so the artists can move, resize, recolour
// and re-font all of it.
//
// The slots tell the code which part is which:
//   Strip                  - the visible window. Marks outside it are cut off (it has a
//                            RectMask2D). Its WIDTH decides how far the marks spread.
//   Marks                  - an empty area inside the strip; the code puts the marks here.
//   Cardinal Template      - hidden example text for N / E / S / W. Style this one.
//   Intercardinal Template - hidden example text for NE / SE / SW / NW. Style this one.
//   Tick Template          - hidden example image for the small ticks. Style this one.
//   Center Marker          - optional; the little arrow showing "you are facing here".
// The code copies the templates at start (one copy per mark) and only moves them sideways
// and fades them near the edges, so any colour, font, size or sprite set here is kept.
//
// DESIGN RULES: none yet (pending GDD entry: HUD compass; North = world +Z).
//
// HOW TO USE: on the prefab's root (the one with the Canvas). Compass.cs loads it by itself.
// HOW TO EDIT: in the prefab. Tools > The Last Drop > Build UI Prefabs makes the default one.
// The three numbers under "Spacing" change how the compass reads.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the compass prefab.</summary>
    public class CompassView : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The visible window at the top of the screen. Needs a RectMask2D so marks are cut off at its edges.")]
        public RectTransform strip;
        [Tooltip("Empty area inside the strip where the marks are placed.")]
        public RectTransform marks;
        [Tooltip("Hidden example text for N, E, S, W.")]
        public TMP_Text cardinalTemplate;
        [Tooltip("Hidden example text for NE, SE, SW, NW.")]
        public TMP_Text intercardinalTemplate;
        [Tooltip("Hidden example image for the small ticks between letters.")]
        public Graphic tickTemplate;
        [Tooltip("Optional arrow in the middle showing where the player faces.")]
        public Graphic centerMarker;

        [Header("Spacing")]
        [Tooltip("How many degrees fit across the strip's width. 180 = you see half the compass at once. Smaller = marks further apart.")]
        [Range(60f, 360f)] public float degreesVisible = 180f;
        [Tooltip("A tick every this many degrees (letters sit on the 45s). 15 = two ticks between each letter.")]
        [Range(5f, 45f)] public float tickEvery = 15f;
        [Tooltip("How much of each end of the strip fades marks out (0 = hard cut, 0.3 = soft).")]
        [Range(0f, 0.5f)] public float edgeFade = 0.2f;

        /// <summary>Builds the default black-and-white compass (used for the prefab, or when there is none).</summary>
        public static CompassView BuildDefault(Transform parent)
        {
            var white = new Color(0.92f, 0.95f, 1f);
            var grey = new Color(0.65f, 0.68f, 0.72f);

            // Sort order 8: under the HUD messages (9) and the team HUD and pause menu (10).
            // Nothing in it blocks clicks (every image and text has Raycast Target off).
            var canvas = UiBuilder.CreateCanvas("Compass", parent, 8);
            var v = canvas.gameObject.AddComponent<CompassView>();
            var root = canvas.transform;

            // The strip: top centre, a dark see-through band.
            var bg = UiBuilder.Image("Strip", root, new Color(0f, 0f, 0f, 0.55f));
            v.strip = bg.rectTransform;
            UiBuilder.Place(v.strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(720f, 52f));
            bg.gameObject.AddComponent<RectMask2D>();

            v.marks = UiBuilder.Rect("Marks", v.strip);

            v.cardinalTemplate = UiBuilder.Text("Cardinal Template", v.marks, "N", 32f, white, TextAlignmentOptions.Center);
            UiBuilder.Place(v.cardinalTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 52f));
            v.cardinalTemplate.fontStyle = FontStyles.Bold;
            v.cardinalTemplate.textWrappingMode = TextWrappingModes.NoWrap;
            v.cardinalTemplate.gameObject.SetActive(false);

            v.intercardinalTemplate = UiBuilder.Text("Intercardinal Template", v.marks, "NE", 22f, grey, TextAlignmentOptions.Center);
            UiBuilder.Place(v.intercardinalTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 52f));
            v.intercardinalTemplate.textWrappingMode = TextWrappingModes.NoWrap;
            v.intercardinalTemplate.gameObject.SetActive(false);

            var tick = UiBuilder.Image("Tick Template", v.marks, grey);
            UiBuilder.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 12f));
            v.tickTemplate = tick;
            tick.gameObject.SetActive(false);

            // A short white bar under the middle of the strip ("you face here"). Plain image so
            // it saves in the prefab; the artists can give it an arrow sprite.
            var marker = UiBuilder.Image("Center Marker", root, white);
            UiBuilder.Place(marker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(3f, 14f));
            v.centerMarker = marker;
            return v;
        }
    }
}
