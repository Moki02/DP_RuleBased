// ============================================================================
// MazeGrid.cs
// The maze layout in memory: one TileType per tile, read from the CSV the designer
// exports from the Google Sheet. The builder, the map, the gates and the zone 2
// rotation all read this same grid. Plain C# (no MonoBehaviour), so it can be tested.
//
// DESIGN RULES: R8.24 to R8.28 (zone 2 turns 90 degrees clockwise; tiles outside
// zone 2 never move). Tile codes from the GDD maze tab.
//
// COORDINATES (tile numbers, not metres):
//   x = column, 0 on the left (west) of the sheet.
//   y = row,    0 on the top (north) of the sheet.
//   The centre tile is ((Width - 1) / 2, (Height - 1) / 2), which is (47, 47) on the 95 × 95 map.
//   Ring = max(|x - centre|, |y - centre|): how many "square steps" a tile is from the centre.
//   Rings decide the part of the maze: hub, zone 1, zone 2 or the outer wall. The ring limits
//   come from GameBalance ("Maze layout" header) through the Rings property.
//
// TWO KINDS OF TILE POSITION (important for zone 2):
//   "Local" = where the tile is in the CSV, i.e. zone 2 at 0 degrees. Never changes.
//   "World" = where the tile is right now, after zone 2 has turned Zone2Turns times.
//   Outside zone 2 the two are always the same.
//   Get(x, y) reads the CSV as stored (local). CodeAt(x, y) reads what is there now (world).
//
// HOW TO USE: you don't put this on an object. MazeBuilder loads it from the CSV.
// Other scripts get it from MazeBuilder.Grid.
// HOW TO EDIT: ring limits are in GameBalance. Tile codes are in ParseCode() below.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>What a tile is. New codes go at the END so saved values never change meaning.</summary>
    public enum TileType : byte
    {
        /// <summary>Blank cell: walkable corridor.</summary>
        Floor,
        /// <summary>W: a full wall block.</summary>
        Wall,
        /// <summary>G: part of a gate (each gate is a strip of 3 G tiles with a lock basin).</summary>
        Gate,
        /// <summary>D: a door. Not used on the 2-layer map.</summary>
        Door,
        /// <summary>R: room floor (5 × 5, open).</summary>
        Room,
        /// <summary>S: the base / spawn area.</summary>
        Spawn,
        /// <summary>V: valve gate, a grate on a corridor tile that opens after its minigame.</summary>
        Valve,
        /// <summary>T: timed door. Not used on the 2-layer map; accepted so the loader doesn't break.</summary>
        TimedDoor
    }

    /// <summary>The four parts of the 2-layer maze, from the centre outwards.</summary>
    public enum MazePart
    {
        /// <summary>The base and its 3-tile wall with 4 gates. Never turns.</summary>
        Hub,
        /// <summary>Zone 1 and its outer wall with 4 exit gates. Never turns.</summary>
        Zone1,
        /// <summary>Zone 2: the part that turns 90 degrees clockwise (R8.24).</summary>
        Zone2,
        /// <summary>The outer wall with zone 2's 8 exit gates. Never turns.</summary>
        OuterWall
    }

    /// <summary>What a gate leads out of, decided by the ring of its tile nearest the centre.</summary>
    public enum GateKind
    {
        /// <summary>One of the 4 gates in the hub wall (ring 7 on the 2-layer map).</summary>
        HubGate,
        /// <summary>One of zone 1's 4 exit gates (ring 25).</summary>
        Zone1Exit,
        /// <summary>One of zone 2's 8 exit gates (ring 45). Winning one wins the game (R11.6).</summary>
        Zone2Exit,
        /// <summary>A gate somewhere else (inside zone 2). None on the current map.</summary>
        Other
    }

    /// <summary>
    /// The last ring of each maze part. Filled from GameBalance (GetMazeRings), so the
    /// numbers stay in one place.
    /// </summary>
    [Serializable]
    public struct MazeRings
    {
        /// <summary>Last ring of the hub.</summary>
        public int hubMax;
        /// <summary>Last ring of zone 1 (includes its outer wall).</summary>
        public int zone1Max;
        /// <summary>Last ring of zone 2, the part that turns.</summary>
        public int zone2Max;

        /// <summary>Makes a set of ring limits.</summary>
        public MazeRings(int hubMax, int zone1Max, int zone2Max)
        {
            this.hubMax = hubMax;
            this.zone1Max = zone1Max;
            this.zone2Max = zone2Max;
        }
    }

    /// <summary>One gate: a group of touching G tiles (a 3-tile strip on the 2-layer map).</summary>
    public class GateInfo
    {
        /// <summary>Position in the Gates list. Stable for a given CSV, so it can be saved.</summary>
        public int Index;
        /// <summary>Every tile of the gate, in local (0 degree) coordinates.</summary>
        public List<Vector2Int> Tiles = new List<Vector2Int>();
        /// <summary>The gate's tile nearest the centre. Used to name and classify the gate.</summary>
        public Vector2Int InnerTile;
        /// <summary>Ring of the inner tile (7, 25 or 45 on the 2-layer map).</summary>
        public int Ring;
        /// <summary>Hub gate, zone 1 exit, zone 2 exit or other.</summary>
        public GateKind Kind;
    }

    /// <summary>One valve gate (a V tile).</summary>
    public class ValveInfo
    {
        /// <summary>Position in the Valves list. Stable for a given CSV, so it can be saved.</summary>
        public int Index;
        /// <summary>The valve's tile in local (0 degree) coordinates.</summary>
        public Vector2Int Tile;
        /// <summary>Ring of the tile (decides the minigame difficulty, R9.6).</summary>
        public int Ring;
        /// <summary>Which part of the maze it's in. Zone 2 valves turn with the zone.</summary>
        public MazePart Part;
    }

    /// <summary>One room: a group of touching R tiles (5 × 5 on the 2-layer map).</summary>
    public class RoomInfo
    {
        /// <summary>Position in the Rooms list.</summary>
        public int Index;
        /// <summary>The room's tiles as a rectangle, in local (0 degree) coordinates.</summary>
        public RectInt Tiles;
        /// <summary>Which part of the maze it's in (by its centre tile).</summary>
        public MazePart Part;
    }

    /// <summary>
    /// The maze layout: a tile type for every tile, plus helpers for rings, maze parts,
    /// gates, valves, rooms and the zone 2 rotation.
    /// </summary>
    public class MazeGrid
    {
        readonly TileType[,] tiles;
        int zone2Turns;

        // Worked out the first time someone asks, then kept (the CSV layout never changes).
        List<GateInfo> gates;
        List<ValveInfo> valves;
        List<RoomInfo> rooms;

        /// <summary>Number of columns (tiles from west to east).</summary>
        public int Width { get; }

        /// <summary>Number of rows (tiles from north to south).</summary>
        public int Height { get; }

        /// <summary>Column of the centre tile (47 on the 95 × 95 map).</summary>
        public int CentreX => (Width - 1) / 2;

        /// <summary>Row of the centre tile (47 on the 95 × 95 map).</summary>
        public int CentreY => (Height - 1) / 2;

        /// <summary>The centre tile.</summary>
        public Vector2Int Centre => new Vector2Int(CentreX, CentreY);

        /// <summary>Zone 2 can only turn on a square grid with a single centre tile (odd size).</summary>
        public bool CanRotate => Width == Height && Width % 2 == 1;

        /// <summary>Ring limits for the maze parts. Set this from GameBalance.GetMazeRings() after loading.</summary>
        public MazeRings Rings { get; set; }

        /// <summary>
        /// How many times zone 2 has turned 90 degrees clockwise, 0 to 3 (4 turns = back to start).
        /// Setting it never moves objects; Zone2Rotator does that and keeps this in step.
        /// </summary>
        public int Zone2Turns
        {
            get => zone2Turns;
            set
            {
                if (!CanRotate && value % 4 != 0)
                {
                    Debug.LogWarning($"[MazeGrid] Can't turn zone 2 on a {Width} x {Height} grid (it must be square with an odd size).");
                    return;
                }
                // "% 4" can be negative in C# for negative numbers, so add 4 and wrap again.
                zone2Turns = ((value % 4) + 4) % 4;
            }
        }

        /// <summary>Makes an empty grid (all floor) of the given size.</summary>
        public MazeGrid(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileType[width, height];
        }

        /// <summary>True if (x, y) is inside the grid.</summary>
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // ------------------------------------------------------------------ Reading tiles

        /// <summary>
        /// The tile as stored in the CSV (zone 2 at 0 degrees). Outside the grid counts as wall,
        /// so edge checks never need a bounds test. For what is there right now, use CodeAt.
        /// </summary>
        public TileType Get(int x, int y) => InBounds(x, y) ? tiles[x, y] : TileType.Wall;

        /// <summary>Same as Get(x, y), for a Vector2Int.</summary>
        public TileType Get(Vector2Int t) => Get(t.x, t.y);

        /// <summary>
        /// The tile that is at world tile (x, y) right now, after zone 2's turns.
        /// Outside zone 2 this is the same as Get.
        /// </summary>
        public TileType CodeAt(int x, int y)
        {
            if (!InBounds(x, y)) return TileType.Wall;
            Vector2Int local = WorldToLocal(new Vector2Int(x, y));
            return tiles[local.x, local.y];
        }

        /// <summary>Same as CodeAt(x, y), for a Vector2Int.</summary>
        public TileType CodeAt(Vector2Int t) => CodeAt(t.x, t.y);

        /// <summary>Changes one tile of the stored (0 degree) layout. Used by the CSV loader and tests.</summary>
        public void Set(int x, int y, TileType type)
        {
            if (!InBounds(x, y)) return;
            tiles[x, y] = type;
            gates = null; valves = null; rooms = null; // layout changed: work the lists out again
        }

        /// <summary>True if the stored (0 degree) tile is a wall.</summary>
        public bool IsWall(int x, int y) => Get(x, y) == TileType.Wall;

        /// <summary>Gates, doors and valves count as walkable here; whether one is open is the gate's job.</summary>
        public bool IsWalkable(int x, int y) => Get(x, y) != TileType.Wall;

        /// <summary>How many tiles of this type the stored layout has.</summary>
        public int Count(TileType type)
        {
            int n = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (tiles[x, y] == type) n++;
            return n;
        }

        // ------------------------------------------------------------------ Rings and parts

        /// <summary>
        /// The tile's ring: max(|x - centre|, |y - centre|). Ring 0 is the centre tile, ring 1 the
        /// 8 tiles around it, and so on. Turning zone 2 never changes a tile's ring.
        /// </summary>
        public int Ring(int x, int y) => Mathf.Max(Mathf.Abs(x - CentreX), Mathf.Abs(y - CentreY));

        /// <summary>Same as Ring(x, y), for a Vector2Int.</summary>
        public int Ring(Vector2Int t) => Ring(t.x, t.y);

        /// <summary>Which part of the maze a tile belongs to, by its ring (see Rings).</summary>
        public MazePart PartOf(int x, int y) => PartOfRing(Ring(x, y));

        /// <summary>Same as PartOf(x, y), for a Vector2Int.</summary>
        public MazePart PartOf(Vector2Int t) => PartOf(t.x, t.y);

        /// <summary>Which part of the maze a ring belongs to.</summary>
        public MazePart PartOfRing(int ring)
        {
            if (ring <= Rings.hubMax) return MazePart.Hub;
            if (ring <= Rings.zone1Max) return MazePart.Zone1;
            if (ring <= Rings.zone2Max) return MazePart.Zone2;
            return MazePart.OuterWall;
        }

        /// <summary>True if the tile is in zone 2 (the part that turns).</summary>
        public bool IsInZone2(int x, int y) => PartOf(x, y) == MazePart.Zone2;

        /// <summary>Same as IsInZone2(x, y), for a Vector2Int.</summary>
        public bool IsInZone2(Vector2Int t) => IsInZone2(t.x, t.y);

        // ------------------------------------------------------------------ Zone 2 rotation

        /// <summary>
        /// Turns a tile position 90 degrees clockwise (seen from above) around the centre,
        /// <paramref name="turns"/> times. One turn moves (x, y) to (size - 1 - y, x).
        /// This does NOT check the zone; use LocalToWorld for that.
        /// </summary>
        public static Vector2Int RotateClockwise(Vector2Int t, int turns, int size)
        {
            // Why (size - 1 - y, x)? Picture the sheet: y grows downwards. Turning clockwise, the
            // top row (y = 0) becomes the right-hand column (x = size - 1), and the left column
            // (x = 0) becomes the top row (y = 0). So the new x comes from the old y (flipped),
            // and the new y is the old x. Example on the 95 map: (60, 15) -> (79, 60).
            int k = ((turns % 4) + 4) % 4;
            for (int i = 0; i < k; i++)
                t = new Vector2Int(size - 1 - t.y, t.x);
            return t;
        }

        /// <summary>
        /// Undoes RotateClockwise: turns a tile 90 degrees anticlockwise <paramref name="turns"/>
        /// times. One step moves (x, y) to (y, size - 1 - x).
        /// </summary>
        public static Vector2Int RotateAnticlockwise(Vector2Int t, int turns, int size)
        {
            // This is the exact reverse of the clockwise step above: (x, y) -> (y, size - 1 - x).
            int k = ((turns % 4) + 4) % 4;
            for (int i = 0; i < k; i++)
                t = new Vector2Int(t.y, size - 1 - t.x);
            return t;
        }

        /// <summary>
        /// Where a CSV (0 degree) tile is right now. Zone 2 tiles are turned Zone2Turns times;
        /// every other tile stays put.
        /// </summary>
        public Vector2Int LocalToWorld(Vector2Int local)
        {
            if (zone2Turns == 0 || !IsInZone2(local)) return local;
            return RotateClockwise(local, zone2Turns, Width);
        }

        /// <summary>
        /// Which CSV (0 degree) tile is at this world tile right now. The reverse of LocalToWorld.
        /// Used to store map knowledge and markers in 0 degree coordinates, so they turn with zone 2.
        /// </summary>
        public Vector2Int WorldToLocal(Vector2Int world)
        {
            // Turning keeps a tile's ring, so a world tile in zone 2 always came from zone 2.
            if (zone2Turns == 0 || !IsInZone2(world)) return world;
            return RotateAnticlockwise(world, zone2Turns, Width);
        }

        // ------------------------------------------------------------------ Gates, valves, rooms

        /// <summary>Every gate (group of touching G tiles), worked out from the 0 degree layout.</summary>
        public IReadOnlyList<GateInfo> Gates => gates ??= BuildGates();

        /// <summary>Every valve gate (V tile), worked out from the 0 degree layout.</summary>
        public IReadOnlyList<ValveInfo> Valves => valves ??= BuildValves();

        /// <summary>Every room (group of touching R tiles), worked out from the 0 degree layout.</summary>
        public IReadOnlyList<RoomInfo> Rooms => rooms ??= BuildRooms();

        /// <summary>Which kind of gate has its inner tile on this ring.</summary>
        public GateKind GateKindForRing(int ring)
        {
            switch (PartOfRing(ring))
            {
                case MazePart.Hub: return GateKind.HubGate;
                case MazePart.Zone1: return GateKind.Zone1Exit;
                case MazePart.OuterWall: return GateKind.Zone2Exit;
                default: return GateKind.Other;
            }
        }

        List<GateInfo> BuildGates()
        {
            var list = new List<GateInfo>();
            foreach (var group in FindGroups(TileType.Gate))
            {
                // Inner tile = the one nearest the centre. Smallest ring first; if two tiles share
                // a ring, the one closer in a straight line wins; then top-most, then left-most,
                // so the answer is always the same.
                Vector2Int inner = group[0];
                foreach (var t in group)
                    if (IsNearerCentre(t, inner)) inner = t;

                int ring = Ring(inner);
                list.Add(new GateInfo
                {
                    Tiles = group,
                    InnerTile = inner,
                    Ring = ring,
                    Kind = GateKindForRing(ring),
                });
            }
            SortAndNumber(list, g => g.InnerTile, (g, i) => g.Index = i);
            return list;
        }

        List<ValveInfo> BuildValves()
        {
            var list = new List<ValveInfo>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (tiles[x, y] != TileType.Valve) continue;
                list.Add(new ValveInfo { Tile = new Vector2Int(x, y), Ring = Ring(x, y), Part = PartOf(x, y) });
            }
            SortAndNumber(list, v => v.Tile, (v, i) => v.Index = i);
            return list;
        }

        List<RoomInfo> BuildRooms()
        {
            var list = new List<RoomInfo>();
            foreach (var r in FindRegions(TileType.Room))
            {
                var centre = new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2);
                list.Add(new RoomInfo { Tiles = r, Part = PartOf(centre) });
            }
            SortAndNumber(list, r => r.Tiles.min, (r, i) => r.Index = i);
            return list;
        }

        bool IsNearerCentre(Vector2Int a, Vector2Int b)
        {
            int ra = Ring(a), rb = Ring(b);
            if (ra != rb) return ra < rb;
            int da = (a - Centre).sqrMagnitude, db = (b - Centre).sqrMagnitude;
            if (da != db) return da < db;
            if (a.y != b.y) return a.y < b.y;
            return a.x < b.x;
        }

        // Sorts top-to-bottom, then left-to-right, and numbers the items 0, 1, 2 ... so a gate's
        // Index stays the same every time the same CSV is loaded (saves rely on this).
        static void SortAndNumber<T>(List<T> list, Func<T, Vector2Int> key, Action<T, int> setIndex)
        {
            list.Sort((a, b) =>
            {
                Vector2Int ka = key(a), kb = key(b);
                return ka.y != kb.y ? ka.y.CompareTo(kb.y) : ka.x.CompareTo(kb.x);
            });
            for (int i = 0; i < list.Count; i++) setIndex(list[i], i);
        }

        // ------------------------------------------------------------------ Connected groups

        /// <summary>
        /// Groups touching tiles of one type. "Touching" means sharing an edge (north, east,
        /// south or west), not just a corner. Each group lists its tiles in 0 degree coordinates.
        /// </summary>
        public List<List<Vector2Int>> FindGroups(TileType type)
        {
            var result = new List<List<Vector2Int>>();
            var seen = new bool[Width, Height];
            var stack = new Stack<Vector2Int>();

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (seen[x, y] || tiles[x, y] != type) continue;

                // "Flood fill": start from this tile and keep adding touching tiles of the same type.
                var group = new List<Vector2Int>();
                seen[x, y] = true;
                stack.Push(new Vector2Int(x, y));
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    group.Add(p);
                    TryPush(p.x + 1, p.y); TryPush(p.x - 1, p.y);
                    TryPush(p.x, p.y + 1); TryPush(p.x, p.y - 1);
                }
                result.Add(group);
            }
            return result;

            void TryPush(int px, int py)
            {
                if (!InBounds(px, py) || seen[px, py] || tiles[px, py] != type) return;
                seen[px, py] = true;
                stack.Push(new Vector2Int(px, py));
            }
        }

        /// <summary>
        /// Groups touching tiles of one type and returns each group's bounding box.
        /// Used for rooms and the base, which are rectangles in the layout.
        /// </summary>
        public List<RectInt> FindRegions(TileType type)
        {
            var result = new List<RectInt>();
            foreach (var group in FindGroups(type))
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var p in group)
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
                result.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
            return result;
        }

        // ------------------------------------------------------------------ Loading

        /// <summary>
        /// Turns one CSV cell into a tile type. Unknown codes become floor and set
        /// <paramref name="known"/> to false so the loader can warn.
        /// </summary>
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
                case "V": return TileType.Valve;
                case "T": return TileType.TimedDoor;
                default: known = false; return TileType.Floor;
            }
        }

        /// <summary>
        /// Reads a Google Sheets CSV export (no header row; row 1 is y = 0). Returns null if
        /// the text has no rows. Remember to set Rings afterwards (MazeBuilder does this).
        /// </summary>
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

        /// <summary>Reads a CSV and sets the ring limits in one go.</summary>
        public static MazeGrid FromCsv(string text, string sourceName, MazeRings rings)
        {
            var grid = FromCsv(text, sourceName);
            if (grid != null) grid.Rings = rings;
            return grid;
        }
    }
}
