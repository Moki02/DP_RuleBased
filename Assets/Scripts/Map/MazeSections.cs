// ============================================================================
// MazeSections.cs
// Cuts zones 1 and 2 into map sections, so mapping a section can pay a reward (R8.2).
// Plain C# (no MonoBehaviour), so it can be tested.
//
// ASSUMPTION (developer's call, not in the GDD): the layout CSV has no section codes, so
// sections are made in code. Each zone is cut by angle around the maze centre, like
// slices of a cake: zone 1 (rings hubMaxRing + 1 to zone1MaxRing) into 6 slices named
// A to F, zone 2 (rings zone1MaxRing + 1 to zone2MaxRing) into 8 slices named G to N.
// Slice 0 starts at north and they go clockwise. The hub and the outer wall have no
// sections. If the designer later marks sections in the sheet, replace the constructor.
//
// Sections are worked out on CSV ("local", 0 degree) tiles, so zone 2's sections turn
// with zone 2 (R8.28): a section is always the same set of corridors.
//
// DESIGN RULES: R8.2 (a section is mapped at 75% of its floor; 60% with V5), GDD layout
// table (zone 1: 6 sections, zone 2: 8 sections).
//
// HOW TO USE: MapRewards makes one. SectionAt(localTile) gives the section number (-1 = none).
// HOW TO EDIT: the slice counts are in GameBalance ("Maze content: sections").
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The map sections of zones 1 and 2, made by angle around the centre.</summary>
    public class MazeSections
    {
        readonly MazeGrid grid;
        readonly int[,] sectionOf;                 // [x, y] local tile -> section number, -1 = none
        readonly List<Vector2Int>[] tiles;         // walkable local tiles of each section
        readonly MazePart[] parts;
        readonly string[] names;

        /// <summary>How many sections there are (zone 1's first, then zone 2's).</summary>
        public int Count => tiles.Length;

        /// <summary>
        /// Cuts the maze into sections. <paramref name="zone1Count"/> slices for zone 1 and
        /// <paramref name="zone2Count"/> for zone 2. The grid's Rings must already be set.
        /// </summary>
        public MazeSections(MazeGrid grid, int zone1Count, int zone2Count)
        {
            this.grid = grid;
            zone1Count = Mathf.Max(1, zone1Count);
            zone2Count = Mathf.Max(1, zone2Count);
            int total = zone1Count + zone2Count;

            tiles = new List<Vector2Int>[total];
            parts = new MazePart[total];
            names = new string[total];
            for (int s = 0; s < total; s++)
            {
                tiles[s] = new List<Vector2Int>();
                parts[s] = s < zone1Count ? MazePart.Zone1 : MazePart.Zone2;
                // A, B, C ... carrying on from zone 1 into zone 2 (G to N with 6 + 8), like the GDD.
                names[s] = s < 26 ? ((char)('A' + s)).ToString() : (s + 1).ToString();
            }

            sectionOf = new int[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                sectionOf[x, y] = -1;
                MazePart part = grid.PartOf(x, y);
                int s;
                if (part == MazePart.Zone1) s = Slice(x, y, zone1Count);
                else if (part == MazePart.Zone2) s = zone1Count + Slice(x, y, zone2Count);
                else continue; // hub and outer wall: no section

                sectionOf[x, y] = s;
                // Only walkable tiles count, the same tiles MapReveal counts for Map %.
                if (grid.Get(x, y) != TileType.Wall) tiles[s].Add(new Vector2Int(x, y));
            }
        }

        // Which of 'count' equal slices the tile is in. 0 starts at north and they go clockwise.
        int Slice(int x, int y, int count)
        {
            float dx = x - grid.CentreX;
            float dy = y - grid.CentreY;   // y grows to the south, so north is dy < 0
            // Atan2(east, north): 0 degrees = north, 90 = east, 180 = south, 270 = west.
            float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int s = Mathf.FloorToInt(angle / (360f / count));
            return Mathf.Clamp(s, 0, count - 1);
        }

        /// <summary>The section of a CSV (local) tile, or -1 for the hub, the outer wall or outside the grid.</summary>
        public int SectionAt(Vector2Int local) =>
            grid.InBounds(local.x, local.y) ? sectionOf[local.x, local.y] : -1;

        /// <summary>Which zone a section is in (Zone1 or Zone2).</summary>
        public MazePart PartOf(int section) => parts[section];

        /// <summary>The section's letter (A to N).</summary>
        public string NameOf(int section) => names[section];

        /// <summary>Walkable tiles in the section.</summary>
        public int TileCount(int section) => tiles[section].Count;

        /// <summary>How many of the section's walkable tiles the map has revealed.</summary>
        public int RevealedCount(int section, MapReveal reveal)
        {
            int n = 0;
            foreach (var t in tiles[section])
                if (reveal.IsRevealedLocal(t.x, t.y)) n++;
            return n;
        }

        /// <summary>Share (0-1) of the section's walkable tiles that are revealed. 0 for an empty section.</summary>
        public float RevealedFraction(int section, MapReveal reveal)
        {
            int total = tiles[section].Count;
            return total == 0 ? 0f : (float)RevealedCount(section, reveal) / total;
        }
    }
}
