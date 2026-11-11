// ============================================================================
// MazeBuilder.cs
// Builds a maze from the designer's CSV layout. Walls, gates, doors, valve grates, room
// and base areas and the player spawn are all generated; artists only place props (grass,
// trees, rocks) by hand.
//
// DESIGN RULES: GDD maze tab (tile codes), R8.8 (valve grates), R8.24 to R8.28 (zone 2 turns).
//
// 2-LAYER MAP (Playtest_Maze): tick "Split For Zone 2 Rotation". The builder then makes:
//   _Generated/Maze_Static  - hub, zone 1 and the outer wall (never move, marked static)
//   _Generated/Zone2Pivot   - everything in zone 2 (rings 28 to 44), NOT static, so it can turn.
//                             The pivot sits on the centre tile, so turning it +90 degrees on Y
//                             turns zone 2 one step clockwise (seen from above).
// Put this object at world position (188, 0, 188) with "Center On Transform" on. Then tile
// (x, y) lands at world (x × 4, 0, (94 - y) × 4): the CSV reads like a top-down map, north = +Z.
// Tile size and wall height come from GameBalance when it's assigned.
//
// How to use:
//   1. Export the zone tab from Google Sheets (File > Download > CSV) into Assets/Maps.
//   2. Add an empty GameObject to the scene, add MazeBuilder, drag the CSV into Layout Csv.
//   3. Click "Build maze" in the Inspector. Everything lands under a child named _Generated.
//   4. Grey-box: with no Tile Set, every piece is a ProBuilder block, so you can test right
//      away. Art: make a Maze Tile Set (Synty pieces or kitbashed prefabs), fill its lists and
//      rebuild. Empty lists stay grey-box, so pieces can be swapped in one at a time.
//
// Testing: untick "Gates Enabled" / "Doors Enabled" to walk the whole maze without gates.
// This works instantly, in the editor or in Play mode, with no rebuild.
//
// IMPORTANT: "Build maze" deletes and recreates _Generated. Never put props inside it.
// Keep props in their own GameObject (for example "Zone1_Props"). Props that must turn with
// zone 2 go under a child of Zone2Pivot after building... and are lost on the next build, so
// ask the developer before doing that.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace TheLastDrop
{
    public class MazeBuilder : MonoBehaviour, INightLockable
    {
        /// <summary>Name of the child that holds everything the builder makes.</summary>
        public const string GeneratedRootName = "_Generated";
        /// <summary>Name of the part that never moves (split mode only).</summary>
        public const string StaticRootName = "Maze_Static";
        /// <summary>Name of the part that turns with zone 2 (split mode only).</summary>
        public const string Zone2PivotName = "Zone2Pivot";
        const string GatesName = "Gates", DoorsName = "Doors", ValvesName = "Valves";

        [Header("Layout")]
        [Tooltip("CSV exported from the zone's Google Sheet tab.")]
        public TextAsset layoutCsv;

        [Tooltip("Zone number, 1 to 3. Used for names and for picking per-zone values later.")]
        [Range(1, 3)] public int zone = 1;

        [Tooltip("Art for this zone (Synty or kitbashed prefabs). Leave empty for ProBuilder grey-box.")]
        public MazeTileSet tileSet;

        [Tooltip("2-layer map only: build zone 2 under its own Zone2Pivot so it can turn (R8.24). " +
                 "Needs GameBalance (for the ring limits) and a square CSV with an odd size. " +
                 "Leave off for the old single-zone scenes.")]
        public bool splitForZone2Rotation = false;

        [Header("Testing")]
        [Tooltip("Untick to hide every gate (G) so you can walk the whole maze. No rebuild needed. They still appear during the night lockdown.")]
        public bool gatesEnabled = true;

        [Tooltip("Untick to hide every door (D). No rebuild needed.")]
        public bool doorsEnabled = true;

        [Header("Size")]
        [Tooltip("If set, the tile size comes from GameBalance.cellSize instead of the field below.")]
        public GameBalance balance;

        [Tooltip("Metres per tile (GDD: 4 m for testing).")]
        [Min(0.5f)] public float tileSize = 4f;

        [Tooltip("Wall height in metres for grey-box walls and the generated wall collision (GDD: 10 to 12 m). " +
                 "Ignored when GameBalance is set (GameBalance > Wall Height is used instead).")]
        [Min(0.5f)] public float wallHeight = 11f;

        [Tooltip("Put the centre of the maze on this GameObject's position. Off = top-left tile at the position.")]
        public bool centerOnTransform = true;

        [Header("Collision")]
        [Tooltip("Make clean invisible box colliders for every wall, whatever the art looks like. " +
                 "Stops players slipping through gaps between kitbashed pieces.")]
        public bool generateWallColliders = true;

        [Tooltip("When generating wall colliders, switch off the solid colliders that came with wall, cap and pillar prefabs.")]
        public bool disableArtWallColliders = true;

        [Header("Grey-box (used when the Tile Set has no piece)")]
        [Tooltip("Join touching wall tiles into bigger blocks. Far fewer objects; looks the same.")]
        public bool mergeGreyboxWalls = true;

        public Material wallMaterial;
        public Material floorMaterial;
        public Material gateMaterial;
        public Material doorMaterial;
        public Material roomMaterial;
        public Material spawnMaterial;
        [Tooltip("Valve grates (V tiles) in grey-box. Empty = orange, the map's valve colour.")]
        public Material valveMaterial;

        [Header("Runtime")]
        [Tooltip("Build on Play if the maze hasn't been built in the editor yet.")]
        public bool buildOnStartIfEmpty = true;

        /// <summary>The loaded layout. Null until LoadGrid() or Build() has run.</summary>
        public MazeGrid Grid { get; private set; }
        /// <summary>Where the player starts (centre of the S tiles).</summary>
        public Transform PlayerSpawn { get; private set; }
        /// <summary>Split mode: the object that turns zone 2. Null otherwise.</summary>
        public Transform Zone2Pivot { get; private set; }
        /// <summary>Split mode: the parent of the hub, zone 1 and the outer wall. Null otherwise.</summary>
        public Transform StaticRoot { get; private set; }
        /// <summary>Every room and base area the builder made.</summary>
        public readonly List<MazeArea> Areas = new List<MazeArea>();

        /// <summary>Metres per tile (GameBalance.cellSize when set).</summary>
        public float TileSize => balance != null ? balance.cellSize : tileSize;
        /// <summary>Wall height in metres (GameBalance.wallHeight when set).</summary>
        public float WallHeight => balance != null ? balance.wallHeight : wallHeight;

        // While building one part of the maze, only tiles where this says true are built.
        // In the normal (not split) mode it lets every tile through.
        Func<int, int, bool> inPart = AllTiles;
        bool markPartStatic = true;
        static bool AllTiles(int x, int y) => true;

        enum Anchor { Block, Face, Passage, Floor, Pillar }

        static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        static readonly string[] DirNames = { "N", "E", "S", "W" };

        readonly Dictionary<string, Material> fallbackMaterials = new Dictionary<string, Material>();
        readonly Dictionary<GameObject, List<(Matrix4x4 m, Bounds b)>> meshCache = new Dictionary<GameObject, List<(Matrix4x4, Bounds)>>();

        // ------------------------------------------------------------------ Lifecycle

        void Awake()
        {
            LoadGrid();
            CacheBuiltObjects();
        }

        void Start()
        {
            if (buildOnStartIfEmpty && transform.Find(GeneratedRootName) == null)
                Build();
            else
                ApplyTestToggles();
        }

        // ------------------------------------------------------------------ Coordinates

        /// <summary>Tile centre at floor level, in this object's local space.</summary>
        public Vector3 GridToLocal(int x, int y)
        {
            int w = Grid != null ? Grid.Width : 0;
            int h = Grid != null ? Grid.Height : 0;
            float ts = TileSize;
            float ox = centerOnTransform ? -(w - 1) * 0.5f * ts : 0f;
            float oz = centerOnTransform ? (h - 1) * 0.5f * ts : 0f;
            // Row 0 (top of the sheet) is the far north edge (+Z).
            return new Vector3(ox + x * ts, 0f, oz - y * ts);
        }

        /// <summary>Tile centre at floor level, in world space (metres).</summary>
        public Vector3 GridToWorld(int x, int y) => transform.TransformPoint(GridToLocal(x, y));

        /// <summary>
        /// The tile under a world position. This is the "world" tile (where things are right now),
        /// so for zone 2 use Grid.WorldToLocal to get the CSV tile.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector2 t = WorldToGridFloat(world);
            return new Vector2Int(Mathf.RoundToInt(t.x), Mathf.RoundToInt(t.y));
        }

        /// <summary>
        /// Like WorldToGrid but without rounding: (10.5, 3.2) means halfway between tiles 10 and 11.
        /// Tile centres are whole numbers. Used to draw the player smoothly on the map.
        /// </summary>
        public Vector2 WorldToGridFloat(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 origin = GridToLocal(0, 0);
            float ts = TileSize;
            // x grows to the east (+X); y grows to the south, which is -Z (row 0 is the north edge).
            return new Vector2((local.x - origin.x) / ts, (origin.z - local.z) / ts);
        }

        /// <summary>Grid direction (x right, y down the sheet) to a local-space direction.</summary>
        static Vector3 DirToLocal(Vector2Int d) => new Vector3(d.x, 0f, -d.y);

        // ------------------------------------------------------------------ Testing toggles

        bool nightLocked;

        /// <summary>Night lockdown (NightLockdown calls this): gates hidden for testing come back while it is night.</summary>
        public void SetNightLock(bool locked)
        {
            nightLocked = locked;
            ApplyTestToggles();
        }

        /// <summary>Shows or hides all gates and doors. Called on build, on Play, and when the boxes are ticked.</summary>
        public void ApplyTestToggles()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            // In split mode there is a Gates (and Doors) folder in each part, so check them all.
            foreach (var partRoot in PartRoots(root))
            {
                SetActive(partRoot.Find(GatesName), gatesEnabled || nightLocked);
                SetActive(partRoot.Find(DoorsName), doorsEnabled);
            }

            static void SetActive(Transform t, bool on)
            {
                if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            }
        }

        // The folders that hold Floor, Walls, Gates ...: _Generated itself (normal mode), or
        // Maze_Static and Zone2Pivot (split mode).
        static IEnumerable<Transform> PartRoots(Transform root)
        {
            yield return root;
            var s = root.Find(StaticRootName);
            if (s != null) yield return s;
            var z = root.Find(Zone2PivotName);
            if (z != null) yield return z;
        }

        // ------------------------------------------------------------------ Build

        /// <summary>Reads the CSV into Grid (and the ring limits from GameBalance). False if there's no CSV.</summary>
        public bool LoadGrid()
        {
            Grid = layoutCsv != null ? MazeGrid.FromCsv(layoutCsv.text, layoutCsv.name) : null;
            if (Grid != null && balance != null) Grid.Rings = balance.GetMazeRings();
            return Grid != null;
        }

        /// <summary>Deletes the old maze and builds a new one from the CSV under _Generated.</summary>
        [ContextMenu("Build maze")]
        public void Build()
        {
            if (!LoadGrid())
            {
                Debug.LogError($"[MazeBuilder] {name}: no layout CSV assigned, or it's empty.", this);
                return;
            }

            bool split = splitForZone2Rotation;
            if (split && balance == null)
            {
                Debug.LogError($"[MazeBuilder] {name}: 'Split For Zone 2 Rotation' needs GameBalance (for the ring limits).", this);
                return;
            }
            if (split && !Grid.CanRotate)
            {
                Debug.LogError($"[MazeBuilder] {name}: zone 2 can only turn on a square CSV with an odd size; this one is {Grid.Width} x {Grid.Height}.", this);
                return;
            }

            Clear();
            fallbackMaterials.Clear();
            meshCache.Clear();
            Areas.Clear();

            var root = NewChild(GeneratedRootName, transform);
            var counts = new BuildCounts();

            if (!split)
            {
                // Old single-zone scenes: everything straight under _Generated, as before.
                BuildPart(root, AllTiles, isStatic: true, withFloorSlab: true, counts);
            }
            else
            {
                StaticRoot = NewChild(StaticRootName, root);
                BuildPart(StaticRoot, (x, y) => !Grid.IsInZone2(x, y), isStatic: true, withFloorSlab: true, counts);

                // Zone 2 is built with the pivot still at the builder's origin, so every piece can
                // use the normal tile positions. Afterwards the pivot moves onto the centre tile and
                // its folders move back by the same amount: nothing changes in the world, but the
                // pivot's turning point is now the centre of the maze.
                // The grey-box floor slab stays in Maze_Static: turning maps floor to floor, so one
                // flat slab under the whole maze is always correct.
                Zone2Pivot = NewChild(Zone2PivotName, root);
                BuildPart(Zone2Pivot, (x, y) => Grid.IsInZone2(x, y), isStatic: false, withFloorSlab: false, counts);
                Vector3 centre = GridToLocal(Grid.CentreX, Grid.CentreY);
                Zone2Pivot.localPosition = centre;
                foreach (Transform child in Zone2Pivot) child.localPosition -= centre;
            }

            inPart = AllTiles;
            markPartStatic = true;

            BuildSpawn(root);
            ApplyTestToggles();

            Debug.Log($"[MazeBuilder] Zone {zone} built: {Grid.Width} x {Grid.Height} tiles, " +
                      $"{Grid.Count(TileType.Wall)} wall tiles as {counts.wallObjects} objects, {counts.gates} gate tiles, " +
                      $"{counts.doors} door tiles, {counts.valves} valve grates, {Areas.Count} areas" +
                      (split ? ", zone 2 under Zone2Pivot." : "."), this);
        }

        class BuildCounts { public int wallObjects, gates, doors, valves; }

        /// <summary>Builds one part of the maze (only tiles where <paramref name="filter"/> is true) under <paramref name="partRoot"/>.</summary>
        void BuildPart(Transform partRoot, Func<int, int, bool> filter, bool isStatic, bool withFloorSlab, BuildCounts counts)
        {
            inPart = filter;
            markPartStatic = isStatic; // static objects can't move at runtime, so zone 2 must not be static

            BuildFloor(NewChild("Floor", partRoot), withFloorSlab);
            counts.wallObjects += BuildWalls(NewChild("Walls", partRoot));
            if (generateWallColliders) BuildWallColliders(NewChild("WallColliders", partRoot));
            var gateRoot = NewChild(GatesName, partRoot);
            var doorRoot = NewChild(DoorsName, partRoot);
            var valveRoot = NewChild(ValvesName, partRoot);
            counts.gates += BuildPassages(gateRoot, TileType.Gate);
            counts.doors += BuildPassages(doorRoot, TileType.Door);
            counts.doors += BuildPassages(doorRoot, TileType.TimedDoor);
            counts.valves += BuildPassages(valveRoot, TileType.Valve);
            var areaRoot = NewChild("Areas", partRoot);
            BuildAreas(areaRoot, TileType.Room, MazeArea.Kind.Room);
            BuildAreas(areaRoot, TileType.Spawn, MazeArea.Kind.Base);
        }

        // True if this tile is a wall AND belongs to the part being built right now.
        bool IsWallHere(int x, int y) => Grid.IsWall(x, y) && inPart(x, y);

        /// <summary>Deletes everything the builder made (the _Generated child).</summary>
        [ContextMenu("Clear maze")]
        public void Clear()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
            PlayerSpawn = null;
            Zone2Pivot = null;
            StaticRoot = null;
            Areas.Clear();
        }

        /// <summary>Finds the spawn point and areas of a maze that was built in the editor.</summary>
        void CacheBuiltObjects()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            PlayerSpawn = root.Find("PlayerSpawn");
            Zone2Pivot = root.Find(Zone2PivotName);
            StaticRoot = root.Find(StaticRootName);
            Areas.Clear();
            Areas.AddRange(root.GetComponentsInChildren<MazeArea>(true));
        }

        // ------------------------------------------------------------------ Floor

        void BuildFloor(Transform parent, bool withSlab)
        {
            bool hasFloor = tileSet != null && MazeTileSet.HasAny(tileSet.floors);
            bool hasRoom = tileSet != null && MazeTileSet.HasAny(tileSet.roomFloors);
            bool hasSpawn = tileSet != null && MazeTileSet.HasAny(tileSet.spawnFloors);

            if (hasFloor || hasRoom || hasSpawn)
            {
                for (int y = 0; y < Grid.Height; y++)
                for (int x = 0; x < Grid.Width; x++)
                {
                    var t = Grid.Get(x, y);
                    if (t == TileType.Wall || !inPart(x, y)) continue;
                    MazePiece[] list = tileSet.floors;
                    if (t == TileType.Room && hasRoom) list = tileSet.roomFloors;
                    else if (t == TileType.Spawn && hasSpawn) list = tileSet.spawnFloors;
                    var piece = MazeTileSet.Pick(list, MazeTileSet.Hash(x, y, 7));
                    PlacePiece(piece, parent, GridToLocal(x, y), Quaternion.identity, Anchor.Floor, $"Floor_{x}_{y}");
                }
                if (hasFloor) return; // every walkable tile has art
            }

            if (!withSlab) return;

            // One grey-box slab under the whole zone; top surface at y = 0.
            float ts = TileSize;
            Vector3 a = GridToLocal(0, 0), b = GridToLocal(Grid.Width - 1, Grid.Height - 1);
            var size = new Vector3(Grid.Width * ts, 0.2f, Grid.Height * ts);
            CreateBox("Floor_Greybox", parent, new Vector3((a.x + b.x) * 0.5f, -0.1f, (a.z + b.z) * 0.5f),
                      size, Mat(floorMaterial, "Floor", new Color(0.55f, 0.48f, 0.38f)), true);
        }

        // ------------------------------------------------------------------ Walls

        int BuildWalls(Transform parent)
        {
            bool useBlocks = tileSet != null && tileSet.wallMode == WallMode.Blocks && MazeTileSet.HasAny(tileSet.wallBlocks);
            bool useFaces = tileSet != null && tileSet.wallMode == WallMode.Faces && MazeTileSet.HasAny(tileSet.wallFaces);
            if (!useBlocks && !useFaces) return BuildGreyboxWalls(parent);

            int objects = 0;
            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                if (!IsWallHere(x, y)) continue;
                Vector3 centre = GridToLocal(x, y);

                if (useBlocks)
                {
                    var rot = tileSet.rotateBlocks
                        ? Quaternion.Euler(0f, 90f * (MazeTileSet.Hash(y, x, 3) % 4), 0f)
                        : Quaternion.identity;
                    if (PlaceWallArt(MazeTileSet.Pick(tileSet.wallBlocks, MazeTileSet.Hash(x, y)), parent, centre, rot,
                                     Anchor.Block, $"Wall_{x}_{y}")) objects++;
                    continue;
                }

                // Faces: one panel on each side that looks into a walkable tile.
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dirs[d].x, ny = y + Dirs[d].y;
                    // A wall on the border with another part (for example zone 1's outer wall next
                    // to zone 2) gets a panel on that side even if a wall is there now: after zone
                    // 2 turns, a corridor may end up on that side and the wall must not look hollow.
                    bool facesOtherPart = Grid.InBounds(nx, ny) && !inPart(nx, ny);
                    if (!Grid.IsWalkable(nx, ny) && !facesOtherPart) continue;
                    Vector3 dir = DirToLocal(Dirs[d]);
                    var piece = MazeTileSet.Pick(tileSet.wallFaces, MazeTileSet.Hash(x, y, d + 1));
                    if (PlaceWallArt(piece, parent, centre + dir * TileSize * 0.5f, Quaternion.LookRotation(dir),
                                     Anchor.Face, $"Wall_{x}_{y}_{DirNames[d]}")) objects++;
                }

                var cap = MazeTileSet.Pick(tileSet.wallCaps, MazeTileSet.Hash(x, y, 11));
                if (PlaceWallArt(cap, parent, centre, Quaternion.identity, Anchor.Block, $"WallCap_{x}_{y}")) objects++;
            }

            if (useFaces) objects += BuildPillars(parent);
            return objects;
        }

        int BuildPillars(Transform parent)
        {
            if (!MazeTileSet.HasAny(tileSet.pillars)) return 0;
            if (!tileSet.pillarsOnOuterCorners && !tileSet.pillarsOnInnerCorners) return 0;

            int count = 0;
            float half = TileSize * 0.5f;
            var done = new HashSet<Vector2Int>();
            int[] sx = { 1, 1, -1, -1 }, sy = { -1, 1, 1, -1 };

            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                if (!inPart(x, y)) continue;
                bool wall = Grid.IsWall(x, y);
                for (int c = 0; c < 4; c++)
                {
                    int dx = sx[c], dy = sy[c];
                    bool place = wall
                        ? tileSet.pillarsOnOuterCorners && Grid.IsWalkable(x + dx, y) && Grid.IsWalkable(x, y + dy)
                        : tileSet.pillarsOnInnerCorners && Grid.IsWall(x + dx, y) && Grid.IsWall(x, y + dy);
                    if (!place) continue;

                    var corner = new Vector2Int(x + (dx > 0 ? 1 : 0), y + (dy > 0 ? 1 : 0));
                    if (!done.Add(corner)) continue;

                    Vector3 pos = GridToLocal(x, y) + new Vector3(dx * half, 0f, -dy * half);
                    var piece = MazeTileSet.Pick(tileSet.pillars, MazeTileSet.Hash(corner.x, corner.y, 5));
                    if (PlaceWallArt(piece, parent, pos, Quaternion.identity, Anchor.Pillar, $"Pillar_{corner.x}_{corner.y}")) count++;
                }
            }
            return count;
        }

        bool PlaceWallArt(MazePiece piece, Transform parent, Vector3 pos, Quaternion rot, Anchor anchor, string objName)
        {
            var go = PlacePiece(piece, parent, pos, rot, anchor, objName);
            if (go == null) return false;
            if (generateWallColliders && disableArtWallColliders)
                foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    if (!col.isTrigger) col.enabled = false;
            return true;
        }

        int BuildGreyboxWalls(Transform parent)
        {
            var mat = Mat(wallMaterial, "Wall", new Color(0.70f, 0.61f, 0.27f));
            bool solid = !generateWallColliders; // otherwise collision comes from WallColliders
            int objects = 0;
            foreach (var r in WallRects(mergeGreyboxWalls))
            {
                var go = CreateBox($"Wall_{r.xMin}_{r.yMin}", parent, RectCentre(r, WallHeight * 0.5f),
                                   RectSize(r, WallHeight), mat, solid);
                MarkStatic(go);
                objects++;
            }
            return objects;
        }

        void BuildWallColliders(Transform parent)
        {
            foreach (var r in WallRects(true))
            {
                var go = new GameObject($"WallCollider_{r.xMin}_{r.yMin}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = RectCentre(r, WallHeight * 0.5f);
                go.AddComponent<BoxCollider>().size = RectSize(r, WallHeight);
                MarkStatic(go);
            }
        }

        /// <summary>Wall tiles as rectangles: merged (grow right, then down) or one per tile.</summary>
        List<RectInt> WallRects(bool merge)
        {
            var rects = new List<RectInt>();
            var used = new bool[Grid.Width, Grid.Height];
            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                if (used[x, y] || !IsWallHere(x, y)) continue;
                int x1 = x, y1 = y;
                if (merge)
                {
                    while (x1 + 1 < Grid.Width && IsWallHere(x1 + 1, y) && !used[x1 + 1, y]) x1++;
                    while (y1 + 1 < Grid.Height && RowIsFreeWall(x, x1, y1 + 1, used)) y1++;
                }
                for (int yy = y; yy <= y1; yy++)
                    for (int xx = x; xx <= x1; xx++)
                        used[xx, yy] = true;
                rects.Add(new RectInt(x, y, x1 - x + 1, y1 - y + 1));
            }
            return rects;
        }

        bool RowIsFreeWall(int x0, int x1, int y, bool[,] used)
        {
            for (int x = x0; x <= x1; x++)
                if (!IsWallHere(x, y) || used[x, y]) return false;
            return true;
        }

        Vector3 RectCentre(RectInt r, float y)
        {
            Vector3 a = GridToLocal(r.xMin, r.yMin), b = GridToLocal(r.xMax - 1, r.yMax - 1);
            return new Vector3((a.x + b.x) * 0.5f, y, (a.z + b.z) * 0.5f);
        }

        Vector3 RectSize(RectInt r, float height) => new Vector3(r.width * TileSize, height, r.height * TileSize);

        // ------------------------------------------------------------------ Gates and doors

        /// <summary>Gates (G), doors (D and T) and valve grates (V). Returns the number of tiles.</summary>
        int BuildPassages(Transform parent, TileType type)
        {
            int count = 0;
            float ts = TileSize;
            bool isGate = type == TileType.Gate;
            bool isValve = type == TileType.Valve;

            MazePiece[] list = null;
            if (tileSet != null) list = isGate ? tileSet.gates : isValve ? tileSet.valves : tileSet.doors;
            string labelStart = isGate ? "Gate" : isValve ? "Valve" : type == TileType.TimedDoor ? "TimedDoor" : "Door";
            int hashSalt = isGate ? 13 : isValve ? 19 : 17;

            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                if (Grid.Get(x, y) != type || !inPart(x, y)) continue;
                count++;

                // Walls on the left and right = the corridor runs north-south (along Z).
                bool runsAlongZ = Grid.IsWall(x - 1, y) && Grid.IsWall(x + 1, y);
                var rot = runsAlongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                string label = $"{labelStart}_{x}_{y}";

                var piece = MazeTileSet.Pick(list, MazeTileSet.Hash(x, y, hashSalt));
                if (PlacePiece(piece, parent, GridToLocal(x, y), rot, Anchor.Passage, label) != null) continue;

                // Grey-box: a solid slab across the corridor, filling the tile's width.
                // Gates are full wall height; doors and valve grates are a 3 m slab.
                float thick = isGate ? 0.5f : 0.3f;
                float height = isGate ? WallHeight : Mathf.Min(3f, WallHeight);
                var size = runsAlongZ ? new Vector3(ts, height, thick) : new Vector3(thick, height, ts);
                Material mat;
                if (isGate) mat = Mat(gateMaterial, "Gate", new Color(0.31f, 0.64f, 0.88f));       // blue
                else if (isValve) mat = Mat(valveMaterial, "Valve", new Color(0.95f, 0.50f, 0.10f)); // orange
                else mat = Mat(doorMaterial, "Door", new Color(0.88f, 0.54f, 0.24f));
                CreateBox(label, parent, GridToLocal(x, y) + Vector3.up * height * 0.5f, size, mat, true);
            }
            return count;
        }

        // ------------------------------------------------------------------ Rooms, base, spawn

        void BuildAreas(Transform parent, TileType type, MazeArea.Kind kind)
        {
            int index = 0;
            foreach (var r in Grid.FindRegions(type))
            {
                index++; // counted before the part check, so room names stay unique across parts
                if (!inPart(r.xMin + r.width / 2, r.yMin + r.height / 2)) continue;
                var size = RectSize(r, WallHeight);
                var go = NewChild(kind == MazeArea.Kind.Room ? $"Room_{index}" : "Base", parent).gameObject;
                go.transform.localPosition = RectCentre(r, 0f);

                var area = go.AddComponent<MazeArea>();
                area.kind = kind;
                area.tiles = r;
                Areas.Add(area);

                var trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = Vector3.up * WallHeight * 0.5f;
                trigger.size = size;

                // Tinted floor patch so rooms and the base are easy to see in grey-box.
                bool hasArtFloor = tileSet != null && MazeTileSet.HasAny(
                    kind == MazeArea.Kind.Room ? tileSet.roomFloors : tileSet.spawnFloors);
                if (!hasArtFloor)
                {
                    var mat = kind == MazeArea.Kind.Room
                        ? Mat(roomMaterial, "Room", new Color(0.18f, 0.42f, 0.31f))
                        : Mat(spawnMaterial, "Base", new Color(0.12f, 0.30f, 0.48f));
                    CreateBox("FloorTint", go.transform, Vector3.up * 0.01f, new Vector3(size.x, 0.02f, size.z), mat, false);
                }
            }
        }

        void BuildSpawn(Transform parent)
        {
            var spawnTiles = Grid.FindRegions(TileType.Spawn);
            Vector3 pos;
            if (spawnTiles.Count > 0)
            {
                pos = RectCentre(spawnTiles[0], 0f);
            }
            else
            {
                Debug.LogWarning("[MazeBuilder] No S tiles in the layout; PlayerSpawn placed at the maze centre.", this);
                pos = (GridToLocal(0, 0) + GridToLocal(Grid.Width - 1, Grid.Height - 1)) * 0.5f;
            }

            PlayerSpawn = NewChild("PlayerSpawn", parent);
            PlayerSpawn.localPosition = pos + Vector3.up * 0.1f;
        }

        // ------------------------------------------------------------------ Placing art

        /// <summary>
        /// Places one tile-set piece. The anchor is the tile centre (blocks, floors, passages),
        /// the tile edge facing the corridor (faces) or a tile corner (pillars). With Align To
        /// Bounds, the prefab's pivot doesn't matter: the builder lines the mesh up itself.
        /// </summary>
        GameObject PlacePiece(MazePiece piece, Transform parent, Vector3 anchorPos, Quaternion anchorRot,
                              Anchor anchor, string objName)
        {
            if (piece == null || !piece.IsValid) return null;
            var prefab = piece.prefab;
            float ts = TileSize;

            Quaternion q = Quaternion.Euler(0f, piece.yRotation, 0f) * prefab.transform.localRotation;
            Vector3 s = Vector3.Scale(prefab.transform.localScale, piece.Scale);

            // Fit to one tile, measured in placement space (after rotation and scale).
            if (piece.fit != FitMode.None && anchor != Anchor.Pillar && TryMeasure(prefab, q, s, out var b0) && b0.size.x > 1e-4f)
            {
                bool depthToo = anchor == Anchor.Block || anchor == Anchor.Floor;
                float fx = ts / b0.size.x;
                float fz = b0.size.z > 1e-4f ? ts / b0.size.z : 1f;

                if (piece.fit == FitMode.Uniform)
                {
                    s *= depthToo ? ts / Mathf.Max(b0.size.x, b0.size.z) : fx;
                }
                else
                {
                    // Placement X/Z map onto the prefab's own X/Z, or swapped if it's turned 90 degrees.
                    bool swap = Mathf.Abs(Mathf.Sin(q.eulerAngles.y * Mathf.Deg2Rad)) > 0.7f;
                    if (swap) { s.z *= fx; if (depthToo) s.x *= fz; }
                    else { s.x *= fx; if (depthToo) s.z *= fz; }
                }
            }

            Vector3 offset = Vector3.zero;
            if (piece.alignToBounds && TryMeasure(prefab, q, s, out var b))
            {
                switch (anchor)
                {
                    case Anchor.Face:  offset = new Vector3(-b.center.x, -b.min.y, -b.max.z); break; // front on the tile edge, depth inside the wall
                    case Anchor.Floor: offset = new Vector3(-b.center.x, -b.max.y, -b.center.z); break; // top surface at y = 0
                    default:           offset = new Vector3(-b.center.x, -b.min.y, -b.center.z); break; // centred, sitting on the floor
                }
            }

            var go = Place(prefab, parent, objName);
            go.transform.localPosition = anchorPos + anchorRot * (offset + piece.positionOffset);
            go.transform.localRotation = anchorRot * q;
            go.transform.localScale = s;
            MarkStatic(go);
            return go;
        }

        /// <summary>Mesh bounds of a prefab after the given rotation and scale (pivot at the origin).</summary>
        bool TryMeasure(GameObject prefab, Quaternion rot, Vector3 scale, out Bounds bounds)
        {
            bounds = default;
            var meshes = GetMeshes(prefab);
            if (meshes.Count == 0) return false;

            var placement = Matrix4x4.TRS(Vector3.zero, rot, scale);
            bool first = true;
            foreach (var (m, mb) in meshes)
            {
                var full = placement * m;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var p = full.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return true;
        }

        /// <summary>Every mesh in a prefab with its matrix relative to the prefab root (root's own transform removed).</summary>
        List<(Matrix4x4 m, Bounds b)> GetMeshes(GameObject prefab)
        {
            if (meshCache.TryGetValue(prefab, out var list)) return list;
            list = new List<(Matrix4x4, Bounds)>();
            var toRoot = prefab.transform.worldToLocalMatrix;

            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                    list.Add((toRoot * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds));
            foreach (var sk in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sk.sharedMesh != null)
                    list.Add((toRoot * sk.transform.localToWorldMatrix, sk.sharedMesh.bounds));

            meshCache[prefab] = list;
            return list;
        }

        /// <summary>Measured size of every piece in the tile set, for checking new art.</summary>
        public string DescribeTileSet()
        {
            if (tileSet == null) return "No Tile Set assigned.";
            meshCache.Clear();
            var sb = new StringBuilder($"[MazeBuilder] Tile set '{tileSet.name}' (tile = {TileSize} m, wall mode = {tileSet.wallMode}):\n");
            void AddList(string label, MazePiece[] pieces)
            {
                if (!MazeTileSet.HasAny(pieces)) { sb.AppendLine($"  {label}: empty (grey-box)"); return; }
                foreach (var p in pieces)
                {
                    if (p == null || !p.IsValid) continue;
                    var q = Quaternion.Euler(0f, p.yRotation, 0f) * p.prefab.transform.localRotation;
                    var s = Vector3.Scale(p.prefab.transform.localScale, p.Scale);
                    string size = TryMeasure(p.prefab, q, s, out var b)
                        ? $"{b.size.x:0.##} x {b.size.y:0.##} x {b.size.z:0.##} m (W x H x D), pivot offset {b.center.x:0.##}, {b.min.y:0.##}, {b.center.z:0.##}"
                        : "no meshes found";
                    sb.AppendLine($"  {label}: {p.prefab.name} = {size}, fit {p.fit}");
                }
            }
            AddList("Wall blocks", tileSet.wallBlocks);
            AddList("Wall faces", tileSet.wallFaces);
            AddList("Wall caps", tileSet.wallCaps);
            AddList("Pillars", tileSet.pillars);
            AddList("Gates", tileSet.gates);
            AddList("Doors", tileSet.doors);
            AddList("Floors", tileSet.floors);
            AddList("Room floors", tileSet.roomFloors);
            AddList("Base floors", tileSet.spawnFloors);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Helpers

        static Transform NewChild(string childName, Transform parent)
        {
            var t = new GameObject(childName).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Place(GameObject prefab, Transform parent, string objName)
        {
            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            else
#endif
                go = Instantiate(prefab, parent);
            go.name = objName;
            return go;
        }

        /// <summary>A ProBuilder box, so grey-box pieces stay editable with the ProBuilder tools.</summary>
        static GameObject CreateBox(string objName, Transform parent, Vector3 localCentre, Vector3 size,
                                    Material mat, bool collider)
        {
            var positions = new List<Vector3>(24);
            var faces = new List<Face>(6);
            Vector3 h = size * 0.5f;

            // Each face listed clockwise as seen from outside (Unity's front-face winding).
            AddQuad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z)); // -Z
            AddQuad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z));     // +Z
            AddQuad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z)); // -X
            AddQuad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z));     // +X
            AddQuad(new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z));     // +Y
            AddQuad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z)); // -Y

            var pb = ProBuilderMesh.Create(positions, faces);
            pb.ToMesh();
            pb.Refresh();

            var go = pb.gameObject;
            go.name = objName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>();
            return go;

            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = positions.Count;
                positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
                faces.Add(new Face(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }));
            }
        }

        Material Mat(Material assigned, string key, Color colour)
        {
            if (assigned != null) return assigned;
            if (fallbackMaterials.TryGetValue(key, out var m) && m != null) return m;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { name = $"Greybox_{key}" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            else m.color = colour;
            fallbackMaterials[key] = m;
            return m;
        }

        // Static objects are faster to draw but Unity won't let them move in Play mode, so
        // zone 2 (which turns) is never marked static.
        void MarkStatic(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && markPartStatic) go.isStatic = true;
#endif
        }
    }
}
