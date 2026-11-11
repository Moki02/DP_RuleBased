// ============================================================================
// MapIcon.cs
// One extra icon for the map screen: a coloured square with a letter on a tile. Rooms use
// it (R8.18: a room shows as an icon on the map once found).
//
// HOW TO USE: give MapScreen.extraIcons a function that returns the icons to draw, with
// WORLD tiles (use MazeGrid.LocalToWorld for zone 2, so the icons turn with the zone).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>An icon on the map screen.</summary>
    public struct MapIcon
    {
        /// <summary>Where it is right now (world tile, after zone 2's turns).</summary>
        public Vector2Int worldTile;
        /// <summary>The square's colour.</summary>
        public Color color;
        /// <summary>One or two letters drawn on it (for example "C" for a cistern).</summary>
        public string letter;

        /// <summary>Makes an icon.</summary>
        public MapIcon(Vector2Int worldTile, Color color, string letter)
        {
            this.worldTile = worldTile;
            this.color = color;
            this.letter = letter;
        }
    }
}
