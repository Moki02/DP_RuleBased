// ============================================================================
// GameBalance.HubPlaceholders.cs
// More GameBalance values: where the hub's placeholder Raw Basin, Clean Storage tank and
// spare jugs stand. Same GameBalance asset, split into a partial file (team rule), so no
// one has to edit GameBalance.cs. New fields take the defaults below.
//
// DESIGN RULES: R3.5 (spare jugs by the hub door), R5.3 (each storage tier is visible).
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Hub placeholders".
// Tiles are counted from the hub centre (x = east, y = south), like the water sources.
// Already taken: pump (-3,0), boiling pot (0,-3), Clean Storage tap (3,0),
// Rain Catcher (-3,-3), Solar Still (3,-3), Deep Well (3,3). The base runs -6..6.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Hub placeholders")]
        [Tooltip("Raw Basin tile, counted from the hub centre, in tiles. Next to the boiling pot by default.")]
        public Vector2Int rawBasinTile = new Vector2Int(-1, -3);

        [Tooltip("Clean Storage tank (Tapayan / Drum / Ferrocement / Cistern) tile, counted from the hub centre. " +
                 "Next to the team's Clean Storage tap by default.")]
        public Vector2Int cleanTankTile = new Vector2Int(3, 1);

        [Tooltip("Spare jugs tile, counted from the hub centre. On the way to the south hub gate by default (R3.5: by the hub door).")]
        public Vector2Int spareJugsTile = new Vector2Int(0, 5);

        [Tooltip("Litres one placeholder spare jug stands for (one jug model per this many litres).")]
        [Min(0.5f)] public float litresPerSpareJug = 5f;

        [Tooltip("Most spare jug models shown at once.")]
        [Min(1)] public int maxSpareJugModels = 12;
    }
}
