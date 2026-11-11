// ============================================================================
// GameBalance.Sources.cs
// More GameBalance values: where the extra water sources stand in the hub and how their
// labels look. The source RATES themselves (rain per m², runoff, Solar Still, Deep Well,
// boiling pot, Biosand Filter and its ripening) were already in GameBalance.cs under
// "Sources" and "Purifiers", so they are not repeated here.
// This is the same GameBalance asset; the class is split over several files ("partial")
// so different features can add their numbers without editing the same file.
//
// DESIGN RULES: R1.4 (rain spills, FULL popup), R4.3 to R4.6 (sources and their labels),
// R6.1 (Biosand Filter).
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Water sources (hub)".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Water sources (hub)")]
        [Tooltip("Rain Catcher tile, counted from the hub centre (x = east, y = south), in tiles. " +
                 "Keep clear of the pump (-3,0), boiling pot (0,-3) and Clean Storage (+3,0).")]
        public Vector2Int rainCatcherTile = new Vector2Int(-3, -3);

        [Tooltip("Solar Still tile, counted from the hub centre, in tiles.")]
        public Vector2Int solarStillTile = new Vector2Int(3, -3);

        [Tooltip("Deep Well tile, counted from the hub centre, in tiles.")]
        public Vector2Int deepWellTile = new Vector2Int(3, 3);

        [Tooltip("Height of the floating source labels above the floor (m).")]
        [Min(0f)] public float sourceLabelHeight = 3f;

        [Tooltip("Font size of the floating source labels (world-space TextMeshPro; about 1 m tall per 10).")]
        [Min(0.1f)] public float sourceLabelFontSize = 4f;

        [Tooltip("While rain spills over a full Raw Basin, the FULL message repeats this often (seconds, R1.4).")]
        [Min(0.5f)] public float rainFullToastSeconds = 4f;

        [Tooltip("How far from the boiling pot the Biosand Filter model stands (m, to the pot's right).")]
        [Min(0f)] public float biosandSideOffset = 1.5f;
    }
}
