// MazeBuilder.cs
// Builds a maze zone from the designer's CSV layout. Walls, gates, doors, room and base
// areas and the player spawn are all generated; artists only place props (grass, trees,
// rocks) by hand.
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
// Keep props in their own GameObject (for example "Zone1_Props").

using System.Collections.Generic;
using System.Text;
using DryLabyrinth.Data;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace DryLabyrinth.Maze
{
    public class MazeBuilder : MonoBehaviour
    {
        public const string GeneratedRootName = "_Generated";
        const string GatesName = "Gates", DoorsName = "Doors";

        [Header("Layout")]
        [Tooltip("CSV exported from the zone's Google Sheet tab.")]
        public TextAsset layoutCsv;

        [Tooltip("Zone number, 1 to 3. Used for names and for picking per-zone values later.")]
        [Range(1, 3)] public int zone = 1;

        [Tooltip("Art for this zone (Synty or kitbashed prefabs). Leave empty for ProBuilder grey-box.")]
        public MazeTileSet tileSet;

        [Header("Testing")]
        [Tooltip("Untick to hide every gate (G) so you can walk the whole maze. No rebuild needed.")]
        public bool gatesEnabled = true;

        [Tooltip("Untick to hide every door (D). No rebuild needed.")]
        public bool doorsEnabled = true;

        [Header("Size")]
        [Tooltip("If set, the tile size comes from GameBalance.cellSize instead of the field below.")]
        public GameBalance balance;

        [Tooltip("Metres per tile (GDD: 4 m for testing).")]
        [Min(0.5f)] public float tileSize = 4f;

        [Tooltip("Wall height in metres for grey-box walls and the generated wall collision (GDD: 10 to 12 m).")]
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

        [Header("Runtime")]
        [Tooltip("Build on Play if the maze hasn't been built in the editor yet.")]
        public bool buildOnStartIfEmpty = true;

        public MazeGrid Grid { get; private set; }
        public Transform PlayerSpawn { get; private set; }
        public readonly List<MazeArea> Areas = new List<MazeArea>();

        public float TileSize => balance != null ? balance.cellSize : tileSize;

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

        public Vector3 GridToWorld(int x, int y) => transform.TransformPoint(GridToLocal(x, y));

        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 origin = GridToLocal(0, 0);
            float ts = TileSize;
            return new Vector2Int(
                Mathf.RoundToInt((local.x - origin.x) / ts),
                Mathf.RoundToInt((origin.z - local.z) / ts));
        }

        /// <summary>Grid direction (x right, y down the sheet) to a local-space direction.</summary>
        static Vector3 DirToLocal(Vector2Int d) => new Vector3(d.x, 0f, -d.y);

        // ------------------------------------------------------------------ Testing toggles

        /// <summary>Shows or hides all gates and doors. Called on build, on Play, and when the boxes are ticked.</summary>
        public void ApplyTestToggles()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            var gates = root.Find(GatesName);
            var doors = root.Find(DoorsName);
            if (gates != null && gates.gameObject.activeSelf != gatesEnabled) gates.gameObject.SetActive(gatesEnabled);
            if (doors != null && doors.gameObject.activeSelf != doorsEnabled) doors.gameObject.SetActive(doorsEnabled);
        }

        // ------------------------------------------------------------------ Build

        public bool LoadGrid()
        {
            Grid = layoutCsv != null ? MazeGrid.FromCsv(layoutCsv.text, layoutCsv.name) : null;
            return Grid != null;
        }

        [ContextMenu("Build maze")]
        public void Build()
        {
            if (!LoadGrid())
            {
                Debug.LogError($"[MazeBuilder] {name}: no layout CSV assigned, or it's empty.", this);
                return;
            }

            Clear();
            fallbackMaterials.Clear();
            meshCache.Clear();
            Areas.Clear();

            var root = NewChild(GeneratedRootName, transform);
            var floorRoot = NewChild("Floor", root);
            var wallRoot = NewChild("Walls", root);
            var gateRoot = NewChild(GatesName, root);
            var doorRoot = NewChild(DoorsName, root);
            var areaRoot = NewChild("Areas", root);

            BuildFloor(floorRoot);
            int wallObjects = BuildWalls(wallRoot);
            if (generateWallColliders) BuildWallColliders(NewChild("WallColliders", root));
            int gates = BuildPassages(gateRoot, TileType.Gate);
            int doors = BuildPassages(doorRoot, TileType.Door);
            BuildAreas(areaRoot, TileType.Room, MazeArea.Kind.Room);
            BuildAreas(areaRoot, TileType.Spawn, MazeArea.Kind.Base);
            BuildSpawn(root);
            ApplyTestToggles();

            Debug.Log($"[MazeBuilder] Zone {zone} built: {Grid.Width} x {Grid.Height} tiles, " +
                      $"{Grid.Count(TileType.Wall)} wall tiles as {wallObjects} objects, {gates} gate tiles, " +
                      $"{doors} door tiles, {Areas.Count} areas.", this);
        }

        [ContextMenu("Clear maze")]
        public void Clear()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
            PlayerSpawn = null;
            Areas.Clear();
        }

        /// <summary>Finds the spawn point and areas of a maze that was built in the editor.</summary>
        void CacheBuiltObjects()
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null) return;
            PlayerSpawn = root.Find("PlayerSpawn");
            Areas.Clear();
            Areas.AddRange(root.GetComponentsInChildren<MazeArea>(true));
        }

        // ------------------------------------------------------------------ Floor

        void BuildFloor(Transform parent)
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
                    if (t == TileType.Wall) continue;
                    MazePiece[] list = tileSet.floors;
                    if (t == TileType.Room && hasRoom) list = tileSet.roomFloors;
                    else if (t == TileType.Spawn && hasSpawn) list = tileSet.spawnFloors;
                    var piece = MazeTileSet.Pick(list, MazeTileSet.Hash(x, y, 7));
                    PlacePiece(piece, parent, GridToLocal(x, y), Quaternion.identity, Anchor.Floor, $"Floor_{x}_{y}");
                }
                if (hasFloor) return; // every walkable tile has art
            }

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
                if (!Grid.IsWall(x, y)) continue;
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
                    if (!Grid.IsWalkable(x + Dirs[d].x, y + Dirs[d].y)) continue;
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
                var go = CreateBox($"Wall_{r.xMin}_{r.yMin}", parent, RectCentre(r, wallHeight * 0.5f),
                                   RectSize(r, wallHeight), mat, solid);
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
                go.transform.localPosition = RectCentre(r, wallHeight * 0.5f);
                go.AddComponent<BoxCollider>().size = RectSize(r, wallHeight);
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
                if (used[x, y] || !Grid.IsWall(x, y)) continue;
                int x1 = x, y1 = y;
                if (merge)
                {
                    while (x1 + 1 < Grid.Width && Grid.IsWall(x1 + 1, y) && !used[x1 + 1, y]) x1++;
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
                if (!Grid.IsWall(x, y) || used[x, y]) return false;
            return true;
        }

        Vector3 RectCentre(RectInt r, float y)
        {
            Vector3 a = GridToLocal(r.xMin, r.yMin), b = GridToLocal(r.xMax - 1, r.yMax - 1);
            return new Vector3((a.x + b.x) * 0.5f, y, (a.z + b.z) * 0.5f);
        }

        Vector3 RectSize(RectInt r, float height) => new Vector3(r.width * TileSize, height, r.height * TileSize);

        // ------------------------------------------------------------------ Gates and doors

        /// <summary>Gates and doors. Returns the number of tiles.</summary>
        int BuildPassages(Transform parent, TileType type)
        {
            int count = 0;
            float ts = TileSize;
            bool isGate = type == TileType.Gate;
            MazePiece[] list = tileSet == null ? null : (isGate ? tileSet.gates : tileSet.doors);

            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                if (Grid.Get(x, y) != type) continue;
                count++;

                // Walls on the left and right = the corridor runs north-south (along Z).
                bool runsAlongZ = Grid.IsWall(x - 1, y) && Grid.IsWall(x + 1, y);
                var rot = runsAlongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                string label = $"{(isGate ? "Gate" : "Door")}_{x}_{y}";

                var piece = MazeTileSet.Pick(list, MazeTileSet.Hash(x, y, isGate ? 13 : 17));
                if (PlacePiece(piece, parent, GridToLocal(x, y), rot, Anchor.Passage, label) != null) continue;

                // Grey-box: a solid slab across the corridor, filling the tile's width.
                float thick = isGate ? 0.5f : 0.3f;
                float height = isGate ? wallHeight : Mathf.Min(3f, wallHeight);
                var size = runsAlongZ ? new Vector3(ts, height, thick) : new Vector3(thick, height, ts);
                var colour = isGate ? new Color(0.31f, 0.64f, 0.88f) : new Color(0.88f, 0.54f, 0.24f);
                var mat = isGate ? Mat(gateMaterial, "Gate", colour) : Mat(doorMaterial, "Door", colour);
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
                index++;
                var size = RectSize(r, wallHeight);
                var go = NewChild(kind == MazeArea.Kind.Room ? $"Room_{index}" : "Base", parent).gameObject;
                go.transform.localPosition = RectCentre(r, 0f);

                var area = go.AddComponent<MazeArea>();
                area.kind = kind;
                area.tiles = r;
                Areas.Add(area);

                var trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = Vector3.up * wallHeight * 0.5f;
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

        static void MarkStatic(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) go.isStatic = true;
#endif
        }
    }
}
