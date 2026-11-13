// ============================================================================
// RoomDoorLayout.cs
// Finds every room entrance in the maze layout: each place where a room tile (R) touches a
// walkable tile that isn't part of the room (corridor, valve, gate). One door goes on each
// of those edges. Plain maths on the CSV grid (no MonoBehaviour), so it can be tested.
//
// The order is fixed (rooms in MazeGrid.Rooms order, then each room's tiles row by row,
// then north, east, south, west), so a door's number never changes and can be saved.
//
// DESIGN RULES: none yet (pending GDD entry: room doors, 2026-11-13).
//
// HOW TO USE: RoomDoors calls Find(grid). You don't put this on an object.
// HOW TO EDIT: nothing to tune; entrances come from the CSV.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>One room entrance: the room tile on the inside and the direction out of the room.</summary>
    public struct RoomDoorSpot
    {
        /// <summary>Door number (fixed order, used by the save).</summary>
        public int Index;
        /// <summary>Which room (MazeGrid.Rooms index).</summary>
        public int Room;
        /// <summary>The room tile next to the doorway (CSV / 0 degree tile).</summary>
        public Vector2Int Inside;
        /// <summary>Step from Inside to the tile outside the room (x right, y down the sheet).</summary>
        public Vector2Int Out;
        /// <summary>Ring of the inside tile (sets the minigame difficulty, like valves).</summary>
        public int Ring;

        /// <summary>The tile just outside the room.</summary>
        public Vector2Int Outside => Inside + Out;
    }

    /// <summary>Works out where the room doors go.</summary>
    public static class RoomDoorLayout
    {
        // North (up the sheet), east, south, west.
        static readonly Vector2Int[] Steps = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };

        /// <summary>Every room entrance in the layout, numbered in a fixed order.</summary>
        public static List<RoomDoorSpot> Find(MazeGrid grid)
        {
            var list = new List<RoomDoorSpot>();
            if (grid == null) return list;
            var rooms = grid.Rooms;
            for (int r = 0; r < rooms.Count; r++)
            {
                RectInt rect = rooms[r].Tiles;
                for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    var inside = new Vector2Int(x, y);
                    foreach (var step in Steps)
                    {
                        var o = inside + step;
                        if (rect.Contains(o) || !IsEntrance(grid, o)) continue;
                        list.Add(new RoomDoorSpot { Index = list.Count, Room = r, Inside = inside, Out = step, Ring = grid.Ring(inside) });
                    }
                }
            }
            return list;
        }

        // A tile you can walk in from outside the room: not a wall, not off the map, not
        // another room's floor (two rooms touching would share an opening, not need a door).
        static bool IsEntrance(MazeGrid grid, Vector2Int t)
        {
            if (!grid.InBounds(t.x, t.y)) return false;
            var code = grid.CodeAt(t);
            return code != TileType.Wall && code != TileType.Room;
        }
    }
}
