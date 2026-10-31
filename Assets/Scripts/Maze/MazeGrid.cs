// MazeGrid.cs
// The maze layout in memory: one TileType per tile, read from the CSV the designer exports
// from the Google Sheet (File > Download > CSV). The builder, the map reveal and the
// lockdown logic all read this same grid.
//
// Coordinates: x = column (0 = left), y = row (0 = top row of the sheet).
// Codes: blank = Floor, W = Wall, G = Gate, D = Door, R = Room, S = Spawn/base.

using System.Collections.Generic;
using UnityEngine;

namespace DryLabyrinth.Maze
{
    public enum TileType : byte { Floor, Wall, Gate, Door, Room, Spawn }

    public class MazeGrid
    {
        readonly TileType[,] tiles;

        public int Width { get; }
        public int Height { get; }

        public MazeGrid(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileType[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Outside the grid counts as wall, so edge checks never need a bounds test.</summary>
        public TileType Get(int x, int y) => InBounds(x, y) ? tiles[x, y] : TileType.Wall;

        public void Set(int x, int y, TileType type)
        {
            if (InBounds(x, y)) tiles[x, y] = type;
        }

        public bool IsWall(int x, int y) => Get(x, y) == TileType.Wall;

        /// <summary>Gates and doors count as walkable here; whether one is open is the gate's job.</summary>
        public bool IsWalkable(int x, int y) => Get(x, y) != TileType.Wall;

        public int Count(TileType type)
        {
            int n = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (tiles[x, y] == type) n++;
            return n;
        }

        /// <summary>
        /// Groups touching tiles of one type (4-way) and returns each group's bounding box.
        /// Used for rooms and the base, which are rectangles in the layout.
        /// </summary>
        public List<RectInt> FindRegions(TileType type)
        {
            var result = new List<RectInt>();
            var seen = new bool[Width, Height];
            var stack = new Stack<Vector2Int>();

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (seen[x, y] || tiles[x, y] != type) continue;

                int minX = x, maxX = x, minY = y, maxY = y;
                seen[x, y] = true;
                stack.Push(new Vector2Int(x, y));
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    TryPush(p.x + 1, p.y); TryPush(p.x - 1, p.y);
                    TryPush(p.x, p.y + 1); TryPush(p.x, p.y - 1);
                }
                result.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
            return result;

            void TryPush(int px, int py)
            {
                if (!InBounds(px, py) || seen[px, py] || tiles[px, py] != type) return;
                seen[px, py] = true;
                stack.Push(new Vector2Int(px, py));
            }
        }

        public static TileType ParseCode(string raw, out bool known)
        {
            known = true;
            string s = raw == null ? "" : raw.Trim().ToUpperInvariant();
            switch (s)
            {
                case "": case ".": return TileType.Floor;
                case "W": case "#": return TileType.Wall;
                case "G": return TileType.Gate;
                case "D": return TileType.Door;
                case "R": return TileType.Room;
                case "S": return TileType.Spawn;
                default: known = false; return TileType.Floor;
            }
        }

        /// <summary>Reads a Google Sheets CSV export. Returns null if the text has no rows.</summary>
        public static MazeGrid FromCsv(string text, string sourceName = "layout")
        {
            if (string.IsNullOrEmpty(text)) return null;

            var rows = new List<string[]>();
            foreach (string line in text.Replace("\r", "").Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                rows.Add(line.Split(','));
            }
            if (rows.Count == 0) return null;

            int width = 0;
            foreach (var r in rows) width = Mathf.Max(width, r.Length);

            var grid = new MazeGrid(width, rows.Count);
            int unknown = 0;
            for (int y = 0; y < rows.Count; y++)
            {
                var cells = rows[y];
                if (cells.Length < width)
                    Debug.LogWarning($"[MazeGrid] {sourceName}: row {y + 1} has {cells.Length} cells, expected {width}. Missing cells become floor.");

                for (int x = 0; x < width; x++)
                {
                    string cell = x < cells.Length ? cells[x] : "";
                    grid.tiles[x, y] = ParseCode(cell, out bool known);
                    if (!known && unknown++ < 10)
                        Debug.LogWarning($"[MazeGrid] {sourceName}: unknown code '{cell}' at column {x + 1}, row {y + 1}. Treated as floor.");
                }
            }
            return grid;
        }
    }
}
