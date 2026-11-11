// ============================================================================
// MapReveal.cs
// Remembers which maze tiles the player has seen, and where their map markers are.
// Plain C# (no MonoBehaviour), so the reveal rules can be tested.
//
// DESIGN RULES:
//   R8.1a  Entering a tile reveals it and its 4 neighbours (all 8 with V3 Keen Eyes).
//   R8.1b  Also reveal up to 2 tiles ahead (4 with V1 Trail Sense) in the facing direction
//          (north, east, south or west). Stop at the first wall and reveal that wall.
//   R8.1c  A wall reveals nothing behind it.
//   R8.1d  Revealed tiles stay revealed. The map starts all black.
//   R8.4   Markers: placed or removed on the player's tile; 3 at most (8 with V2 Landmarks).
//   R8.28  What the player knows about zone 2 turns with zone 2.
//
// HOW IT HANDLES ZONE 2 TURNING: every tile is stored by its CSV ("local", 0 degree)
// position. A world tile is turned back to its CSV tile before it's stored, and turned
// forward again when it's read. So when zone 2 turns, everything the player revealed in
// zone 2 (and their markers there) turns with it, with no extra work. Tiles outside
// zone 2 never move, so for them local and world are the same.
//
// HOW TO USE: you don't put this on an object. MapRevealTracker owns one.
// HOW TO EDIT: look-ahead lengths and marker limits are in GameBalance.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>What happened when the player pressed the marker key.</summary>
    public enum MarkerResult
    {
        /// <summary>A new marker was placed on the tile.</summary>
        Added,
        /// <summary>The tile already had a marker, so it was removed.</summary>
        Removed,
        /// <summary>No marker was placed: the player already has the maximum.</summary>
        Full
    }

    /// <summary>The player's map knowledge: revealed tiles and markers, stored in CSV (0 degree) tiles.</summary>
    public class MapReveal
    {
        readonly MazeGrid grid;
        readonly bool[,] revealed;     // [x, y] by CSV (local) tile
        readonly List<Vector2Int> markers = new List<Vector2Int>(); // CSV (local) tiles
        int revealedWalkable;
        readonly int totalWalkable;

        // North, east, south, west in tile steps (y grows downwards / southwards).
        static readonly Vector2Int[] Sides = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        static readonly Vector2Int[] Corners = { new Vector2Int(1, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };

        /// <summary>Makes an all-black map for this maze (R8.1d).</summary>
        public MapReveal(MazeGrid grid)
        {
            this.grid = grid;
            revealed = new bool[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.Get(x, y) != TileType.Wall) totalWalkable++;
        }

        /// <summary>The maze this map belongs to.</summary>
        public MazeGrid Grid => grid;

        // ------------------------------------------------------------------ Reading

        /// <summary>True if the tile that is at world tile (x, y) right now has been revealed.</summary>
        public bool IsRevealed(int x, int y)
        {
            if (!grid.InBounds(x, y)) return false;
            Vector2Int local = grid.WorldToLocal(new Vector2Int(x, y));
            return revealed[local.x, local.y];
        }

        /// <summary>Same as IsRevealed(x, y), for a Vector2Int.</summary>
        public bool IsRevealed(Vector2Int worldTile) => IsRevealed(worldTile.x, worldTile.y);

        /// <summary>True if this CSV (local) tile has been revealed. Used by saving.</summary>
        public bool IsRevealedLocal(int x, int y) => grid.InBounds(x, y) && revealed[x, y];

        /// <summary>Revealed walkable tiles (everything but walls).</summary>
        public int RevealedWalkableCount => revealedWalkable;

        /// <summary>All walkable tiles in the maze (everything but walls: floor, rooms, base, gates, valves).</summary>
        public int TotalWalkableCount => totalWalkable;

        /// <summary>
        /// Map %: revealed walkable tiles out of all walkable tiles, from 0 to 100.
        /// Walls don't count, so revealing a wall never changes the %.
        /// Turning zone 2 never changes it either (it turns floor onto floor).
        /// </summary>
        public float MapPercent => totalWalkable == 0 ? 0f : 100f * revealedWalkable / totalWalkable;

        // ------------------------------------------------------------------ Revealing

        /// <summary>Reveals the tile that is at this world tile right now. Returns true if it was new.</summary>
        public bool Reveal(Vector2Int worldTile)
        {
            if (!grid.InBounds(worldTile.x, worldTile.y)) return false;
            Vector2Int local = grid.WorldToLocal(worldTile);
            if (!RevealLocal(local)) return false;
            TileSeen?.Invoke(local);
            return true;
        }

        /// <summary>
        /// Fired when the player SEES a tile for the first time (walking or looking ahead), with
        /// its CSV (local) position. Not fired by loading a save or by debug key 0, so rewards
        /// that listen to it (R8.2a, R8.2) are never paid twice.
        /// </summary>
        public event System.Action<Vector2Int> TileSeen;

        /// <summary>Reveals a CSV (local) tile. Used by Reveal and by loading a save.</summary>
        public bool RevealLocal(Vector2Int local)
        {
            if (!grid.InBounds(local.x, local.y) || revealed[local.x, local.y]) return false;
            revealed[local.x, local.y] = true;
            if (grid.Get(local.x, local.y) != TileType.Wall) revealedWalkable++;
            return true;
        }

        /// <summary>
        /// The player stepped onto <paramref name="worldTile"/> (R8.1a, R8.1b, R8.1c).
        /// </summary>
        /// <param name="worldTile">The tile the player is on now (world, i.e. after any zone 2 turns).</param>
        /// <param name="facing">One of north (0,-1), east (1,0), south (0,1) or west (-1,0). Zero = no look-ahead.</param>
        /// <param name="revealDiagonals">True with V3 Keen Eyes: reveal all 8 neighbours instead of 4.</param>
        /// <param name="lookAheadTiles">How far to look ahead (2, or 4 with V1 Trail Sense).</param>
        /// <returns>How many tiles were newly revealed.</returns>
        public int EnterTile(Vector2Int worldTile, Vector2Int facing, bool revealDiagonals, int lookAheadTiles)
        {
            int newTiles = 0;
            if (Reveal(worldTile)) newTiles++;

            // R8.1a: the 4 touching tiles (and the 4 corner tiles with Keen Eyes).
            foreach (var s in Sides)
                if (Reveal(worldTile + s)) newTiles++;
            if (revealDiagonals)
                foreach (var c in Corners)
                    if (Reveal(worldTile + c)) newTiles++;

            newTiles += LookAhead(worldTile, facing, lookAheadTiles);
            return newTiles;
        }

        /// <summary>
        /// R8.1b and R8.1c: reveals up to <paramref name="tiles"/> tiles in a straight line from
        /// <paramref name="worldTile"/>, stopping at (and revealing) the first wall.
        /// </summary>
        public int LookAhead(Vector2Int worldTile, Vector2Int facing, int tiles)
        {
            if (facing == Vector2Int.zero) return 0;
            int newTiles = 0;
            for (int i = 1; i <= tiles; i++)
            {
                Vector2Int t = worldTile + facing * i;
                if (!grid.InBounds(t.x, t.y)) break;
                if (Reveal(t)) newTiles++;
                // CodeAt = what is there right now, so a turned zone 2 wall blocks the view correctly.
                if (grid.CodeAt(t) == TileType.Wall) break; // R8.1c: nothing behind a wall
            }
            return newTiles;
        }

        /// <summary>Debug key 0: reveals every tile.</summary>
        public void RevealAll()
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    RevealLocal(new Vector2Int(x, y));
        }

        /// <summary>Makes the map all black again and removes every marker.</summary>
        public void Clear()
        {
            System.Array.Clear(revealed, 0, revealed.Length);
            revealedWalkable = 0;
            markers.Clear();
        }

        // ------------------------------------------------------------------ Markers (R8.4)

        /// <summary>Markers as CSV (local) tiles. Use LocalToWorld on the grid to draw them.</summary>
        public IReadOnlyList<Vector2Int> MarkersLocal => markers;

        /// <summary>The markers' world tiles right now (zone 2 markers turn with the zone).</summary>
        public IEnumerable<Vector2Int> MarkersWorld()
        {
            foreach (var m in markers) yield return grid.LocalToWorld(m);
        }

        /// <summary>
        /// Places a marker on the world tile, or removes the one already there.
        /// <paramref name="maxMarkers"/> is 3, or 8 with V2 Landmarks.
        /// </summary>
        public MarkerResult ToggleMarker(Vector2Int worldTile, int maxMarkers)
        {
            Vector2Int local = grid.WorldToLocal(worldTile);
            if (markers.Remove(local)) return MarkerResult.Removed;
            if (markers.Count >= maxMarkers) return MarkerResult.Full;
            markers.Add(local);
            return MarkerResult.Added;
        }

        /// <summary>Adds a marker straight on a CSV (local) tile. Used by loading a save.</summary>
        public void AddMarkerLocal(Vector2Int local)
        {
            if (!markers.Contains(local)) markers.Add(local);
        }

        // ------------------------------------------------------------------ Facing

        /// <summary>
        /// Snaps a world direction (for example the camera's forward) to the nearest of north,
        /// east, south or west, as a tile step. North is +Z in the world and y - 1 on the map.
        /// </summary>
        public static Vector2Int SnapToTileDirection(Vector3 worldForward)
        {
            if (Mathf.Abs(worldForward.x) < 1e-4f && Mathf.Abs(worldForward.z) < 1e-4f) return Vector2Int.zero;
            // Whichever axis is bigger wins. World +Z is north, which is "up" the sheet (y - 1).
            if (Mathf.Abs(worldForward.x) >= Mathf.Abs(worldForward.z))
                return new Vector2Int(worldForward.x > 0f ? 1 : -1, 0);
            return new Vector2Int(0, worldForward.z > 0f ? -1 : 1);
        }
    }
}
