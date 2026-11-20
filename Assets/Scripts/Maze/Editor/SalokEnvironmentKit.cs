// ============================================================================
// SalokEnvironmentKit.cs   (Editor only)
// Builds the maze's environment art as normal, editable prefabs from the Synty packs already
// in the project, plus a Maze Tile Set that tells the MazeBuilder to use them.
//
// LOOK (moodboard + Maze Runner): tall grey stone/concrete walls with draped ivy and moss,
// heavy trims top and bottom, big pillars on the corners, parched sand floors with cracks,
// steel sluice gates (plain metal), and skeletons holding the survivor notes in rooms.
//
// MENU: Salok > Environment
//   1. Build kit prefabs      - makes every prefab under Assets/Prefabs/Salok_Environment
//                                and the clue bodies under Assets/Resources/Salok/ClueBodies.
//                                Prefabs that already exist are NOT overwritten (your edits are
//                                safe) unless you pick "Rebuild all" in the dialog.
//   2. Make environment scene - copies Art_Intergration.unity to Salok_Environment.unity,
//                                puts the Tile Set on the Maze and rebuilds it. The original
//                                scenes are not touched.
//   3. Make terrain floor      - one Unity Terrain under the whole maze, reaching 120 m past the
//                                outer wall (flat for the first 30 m, then dunes and edge hills).
//                                Ticks MazeBuilder > Terrain Floor so no floor tiles are made.
//                                Sculpt and paint it with Unity's terrain tools; running it again
//                                asks before touching your edits. Data: Assets/Art/Terrain.
//   Open kit preview           - a scratch scene with every kit prefab and the raw Synty
//                                pieces lined up, for checking the look.
//
// HOW TO EDIT
//   - Open any prefab in Assets/Prefabs/Salok_Environment and move, swap or add pieces.
//     Then select the Maze and press "Build maze" to see it in the maze.
//   - Wall panels: front faces +Z, the front sits on z = 0, the wall goes back into -Z.
//     Width 4 m (one tile), height = GameBalance wall height (11 m).
//   - Wall core: every wall is also filled with a solid block just behind the panels (Maze
//     Tile Set > Wall core), so seams, corners and tops never show a hole.
//   - Gates: span X (4 m), centred on z = 0. A steel panel (child "Leaf") 7 m tall with the
//     wall's stone above it. GateSlider slides the Leaf sideways (+X) into the wall to open.
//   - Room door: 4 m wide, pivot bottom middle. Steel frame with a 3 m panel (child "Leaf")
//     that rises into the frame when open (RoomDoor > Rise When Open).
//   - Floors: 4 x 4 m, top surface at y = 0, pivot in the middle.
//   - Clue bodies: the child named "Note" is what the player reads (press E). Move it to put
//     the note somewhere else on the body. Add more prefabs to the ClueBodies folder and rooms
//     will use them too.
//   - Swap a Synty piece for the whole kit: change the paths in the Recipes section below and
//     run "Build kit prefabs" > "Rebuild all".
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Builds Salok's maze environment prefabs and tile set from existing Synty pieces.</summary>
    public static class SalokEnvironmentKit
    {
        // ------------------------------------------------------------------ Folders

        const string KitRoot = "Assets/Prefabs/Salok_Environment";
        const string ClueRoot = "Assets/Resources/Salok/ClueBodies";
        const string TileSetPath = KitRoot + "/Salok_MazeTileSet.asset";
        const string SourceScene = "Assets/Scenes/Art_Intergration.unity";
        const string EnvScene = "Assets/Scenes/Salok_Environment.unity";

        // ------------------------------------------------------------------ Recipes (Synty pieces)

        const string P = "Assets/Prefabs/";
        // Walls
        const string WallStone1 = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_01.prefab";
        const string WallStone2 = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_02.prefab";
        const string WallStone3 = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_03.prefab";
        const string TrimTop = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_Trim_Top_01.prefab";
        const string TrimFloor = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_Trim_Floor_01.prefab";
        const string PillarLarge = P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_Pillar_Large_01.prefab";
        const string IvyDraped1 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_Draped_01.prefab";
        const string IvyDraped2 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_Draped_02.prefab";
        const string IvyDraped3 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_Draped_03.prefab";
        const string Ivy1 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_01.prefab";
        const string Ivy4 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_04.prefab";
        const string Ivy7 = P + "Generic_Polygon/Environment/SM_Gen_Env_Ivy_07.prefab";
        const string RustPlate = P + "Wasteland_Polygon/Props/Greebles/SM_Prop_Greeble_Wall_05.prefab";
        // Gates and doors
        const string SluiceGate = P + "Wasteland_Polygon/Props/SM_Prop_Gate_River_01.prefab";
        const string SluiceGateSmall = P + "Wasteland_Polygon/Props/SM_Prop_Gate_River_02.prefab";
        const string MetalDoor = P + "Dungeon_Polygon/Props/SM_Prop_Door_Metal_01.prefab";
        // Steel sluice parts (moodboard gate/door references, plain metal)
        const string Girder = P + "Wasteland_Polygon/Props/SM_Prop_Girder_Beam_01.prefab";
        const string SteelSheet = P + "Wasteland_Polygon/Props/SM_Prop_Container_Wall_01.prefab";   // corrugated steel
        const string Handwheel = P + "Dungeon_Polygon/Props/SM_Prop_Tech_Cog_01.prefab";
        const string Stem = P + "Wasteland_Polygon/Props/SM_Prop_Pole_01.prefab";
        const float GateOpeningHeight = 7f;   // big gates: steel panel this tall, wall stone above it
        const float RoomDoorHeight = 3f;      // must match RoomDoors > Door Height
        const string CoreMatPath = KitRoot + "/Salok_WallCore.mat";
        // Floors
        const string Sand = P + "Egypt_Polygon/Environment/SM_Env_Ground_Sand_Flat_01.prefab";
        const string Fissure1 = P + "Wasteland_Polygon/Environment/SM_Env_Ground_Fissure_01.prefab";
        const string Fissure2 = P + "Wasteland_Polygon/Environment/SM_Env_Ground_Fissure_02.prefab";
        const string Fissure3 = P + "Wasteland_Polygon/Environment/SM_Env_Ground_Fissure_03.prefab";
        const string CrackedRock = P + "Dungeon_Polygon/Environments/Floors/SM_Env_CrackedRock_01.prefab";
        const string RoomFloorMat = "Assets/Art/MazeMaterials/ground1.mat";
        // Clue bodies
        const string SkeletonLying = P + "Dungeon_Polygon/Props/SM_Prop_Skeleton_Slave_Lying_01.prefab";
        const string SkeletonSitting = P + "Dungeon_Polygon/Props/SM_Prop_Skeleton_Slave_Wall_Sitting_01.prefab";
        const string Mummy = P + "Egypt_Polygon/Props/SM_Prop_Mummy_01.prefab";
        const string Scroll = P + "Egypt_Polygon/Props/SM_Prop_Scroll_01.prefab";
        const string ScrollOpen = P + "Egypt_Polygon/Props/SM_Prop_Scroll_Open_01.prefab";
        const string LimestoneBlock = P + "Egypt_Polygon/Props/SM_Prop_Limestone_Block_01.prefab";
        const string BonePileSmall = P + "Dungeon_Polygon/Environments/Bones/SM_Env_BonePile_Small_01.prefab";

        // Raw pieces shown in the preview scene so the look can be compared.
        static readonly string[] PreviewCandidates =
        {
            WallStone1, WallStone2, WallStone3,
            P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_04.prefab",
            P + "Dungeon_Polygon/Environments/Walls/SM_Env_Wall_05.prefab",
            P + "Egypt_Polygon/Props/SM_Prop_Stonewall_01.prefab",
            P + "Egypt_Polygon/Environment/SM_Env_Rock_Wall_01.prefab",
            P + "Generic_Polygon/Environment/SM_Gen_Env_Cliff_01.prefab",
            P + "Wasteland_Polygon/Buildings/SM_Bld_Wall_Chunk_01.prefab",
            P + "Egypt_Polygon/Buildings/Base/SM_Bld_Base_Wall_01.prefab",
            PillarLarge, TrimTop, TrimFloor, IvyDraped1, Ivy1, RustPlate,
            SluiceGate, SluiceGateSmall, MetalDoor, Girder, SteelSheet, Handwheel, Stem,
            P + "Dungeon_Polygon/Environments/Walls/SM_Env_Door_Large_Stone_01.prefab",
            P + "Dungeon_Polygon/Environments/Misc/SM_Env_Portcullis_01.prefab",
            Sand, Fissure1, CrackedRock,
            SkeletonLying, SkeletonSitting, Mummy, Scroll, ScrollOpen,
        };

        // ------------------------------------------------------------------ Sizes

        static float tile = 4f;
        static float wallH = 11f;
        const float WallDepth = 0.6f;

        static void ReadBalance()
        {
            tile = 4f;
            wallH = 11f;
            foreach (var guid in AssetDatabase.FindAssets("t:GameBalance"))
            {
                var gb = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetDatabase.GUIDToAssetPath(guid));
                if (gb == null || gb.name != "GameBalance") continue;
                tile = gb.cellSize;
                wallH = gb.wallHeight;
            }
        }

        // ------------------------------------------------------------------ Menu

        [MenuItem("Salok/Environment/1. Build kit prefabs", priority = 1)]
        public static void BuildKitMenu()
        {
            bool anyExist = AssetDatabase.IsValidFolder(KitRoot);
            bool rebuild = false;
            if (anyExist)
            {
                int choice = EditorUtility.DisplayDialogComplex("Salok environment kit",
                    "Kit prefabs already exist. Keep your edited prefabs and only make the missing ones, or rebuild everything from the recipes?",
                    "Only missing", "Cancel", "Rebuild all");
                if (choice == 1) return;
                rebuild = choice == 2;
            }
            BuildKit(rebuild);
        }

        [MenuItem("Salok/Environment/2. Make environment scene", priority = 2)]
        public static void MakeSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var tileSet = AssetDatabase.LoadAssetAtPath<MazeTileSet>(TileSetPath);
            if (tileSet == null) BuildKit(false);
            tileSet = AssetDatabase.LoadAssetAtPath<MazeTileSet>(TileSetPath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvScene) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceScene, EnvScene))
                {
                    Debug.LogError($"[SalokKit] Could not copy {SourceScene}.");
                    return;
                }
            }
            var scene = EditorSceneManager.OpenScene(EnvScene, OpenSceneMode.Single);
            ApplyAndBuild(tileSet);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SalokKit] {EnvScene} ready. Original scenes untouched.");
        }

        [MenuItem("Salok/Environment/Apply tile set to open scene and rebuild", priority = 3)]
        public static void ApplyMenu()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<MazeTileSet>(TileSetPath);
            if (tileSet == null) { Debug.LogError("[SalokKit] Run 'Build kit prefabs' first."); return; }
            ApplyAndBuild(tileSet);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------------ Terrain floor

        const string TerrainFolder = "Assets/Art/Terrain";
        const string TerrainDataPath = TerrainFolder + "/Salok_TerrainData.asset";
        const string TerrainObjectName = "Salok_Terrain";
        const string GroundMat = "Assets/Art/MazeMaterials/DryGround.mat";
        const string GroundTexFallback = "Assets/Art/dry_ground_01_1k.blend/textures/dry_ground_01_diff_1k.jpg";
        const string CrackTex = "Assets/Art/Synty/PolygonApocalypseWasteland/Textures/SaltFlat_01.png";

        // Edit these to change the terrain, then run the menu again and pick "Rebuild".
        const float TerrainMargin = 120f;   // metres of land past the maze's outer wall, on every side
        const float FlatBand = 30f;         // flat ground right outside the wall (room for an ending area)
        const float DuneRamp = 40f;         // metres over which the dunes rise after the flat band
        const float DuneHeight = 6f;        // rolling dunes
        const float EdgeHillHeight = 18f;   // hills at the far edge so the world doesn't just stop
        const float TerrainBelow = 10f;     // how far the terrain can dig below y = 0 (maze floor = y 0)
        const float TerrainMaxHeight = 40f; // total height range of the terrain
        const int HeightRes = 513, AlphaRes = 512;

        [MenuItem("Salok/Environment/3. Make terrain floor", priority = 4)]
        public static void TerrainMenu()
        {
            var builders = Object.FindObjectsByType<MazeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (builders.Length == 0) { Debug.LogError("[SalokKit] Open the maze scene first (no MazeBuilder found)."); return; }
            var builder = builders[0];
            if (!builder.LoadGrid()) { Debug.LogError("[SalokKit] The MazeBuilder has no layout CSV."); return; }

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            bool paint = true;
            if (data != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Salok terrain",
                    "A terrain already exists. Keep its sculpting and painting (only line it up with the maze again), " +
                    "or rebuild its heights and paint from scratch?",
                    "Keep my edits", "Cancel", "Rebuild");
                if (choice == 1) return;
                paint = choice == 2;
            }

            // The maze's footprint in world space (outer edge of the outer wall).
            var grid = builder.Grid;
            float ts = builder.TileSize;
            Vector3 a = builder.GridToWorld(0, 0), b = builder.GridToWorld(grid.Width - 1, grid.Height - 1);
            float minX = Mathf.Min(a.x, b.x) - ts * 0.5f, maxX = Mathf.Max(a.x, b.x) + ts * 0.5f;
            float minZ = Mathf.Min(a.z, b.z) - ts * 0.5f, maxZ = Mathf.Max(a.z, b.z) + ts * 0.5f;
            float floorY = builder.transform.position.y;
            float size = Mathf.Max(maxX - minX, maxZ - minZ) + TerrainMargin * 2f;
            var origin = new Vector3((minX + maxX) * 0.5f - size * 0.5f, floorY - TerrainBelow, (minZ + maxZ) * 0.5f - size * 0.5f);

            EnsureFolder(TerrainFolder);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }
            if (paint)
            {
                data.heightmapResolution = HeightRes;
                data.alphamapResolution = AlphaRes;
                data.baseMapResolution = 1024;
                data.SetDetailResolution(512, 16);
            }
            data.size = new Vector3(size, TerrainMaxHeight, size);

            if (paint)
            {
                // Heights: dead flat (y = 0) under the maze and the flat band, then dunes, then edge hills.
                float flat = TerrainBelow / TerrainMaxHeight;
                var heights = new float[HeightRes, HeightRes];
                for (int zi = 0; zi < HeightRes; zi++)
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float px = origin.x + xi / (float)(HeightRes - 1) * size;
                    float pz = origin.z + zi / (float)(HeightRes - 1) * size;
                    float d = OutsideDistance(px, pz, minX, maxX, minZ, maxZ);
                    float dunes = Smooth(FlatBand, FlatBand + DuneRamp, d)
                                  * (Mathf.PerlinNoise(px * 0.025f + 11f, pz * 0.025f + 7f) * 0.7f
                                     + Mathf.PerlinNoise(px * 0.07f + 3f, pz * 0.07f + 19f) * 0.3f) * DuneHeight;
                    float hills = Smooth(TerrainMargin * 0.6f, TerrainMargin, d)
                                  * (0.6f + 0.4f * Mathf.PerlinNoise(px * 0.02f, pz * 0.02f)) * EdgeHillHeight;
                    heights[zi, xi] = flat + (dunes + hills) / TerrainMaxHeight;
                }
                data.SetHeights(0, 0, heights);

                // Paint: dry ground everywhere, patches of cracked salt flat. Artists repaint freely.
                data.terrainLayers = new[]
                {
                    MakeLayer("Salok_DryGround", GroundTexture(out var normal), normal, 4f),
                    MakeLayer("Salok_Cracked", AssetDatabase.LoadAssetAtPath<Texture2D>(CrackTex), null, 10f),
                };
                var alpha = new float[AlphaRes, AlphaRes, 2];
                for (int zi = 0; zi < AlphaRes; zi++)
                for (int xi = 0; xi < AlphaRes; xi++)
                {
                    float px = origin.x + (xi + 0.5f) / AlphaRes * size;
                    float pz = origin.z + (zi + 0.5f) / AlphaRes * size;
                    float crack = Smooth(0.58f, 0.72f, Mathf.PerlinNoise(px * 0.03f + 41f, pz * 0.03f + 5f)) * 0.85f;
                    alpha[zi, xi, 0] = 1f - crack;
                    alpha[zi, xi, 1] = crack;
                }
                data.SetAlphamaps(0, 0, alpha);
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            // The terrain object sits at the scene root, outside the maze, so "Build maze" never deletes it.
            var go = GameObject.Find(TerrainObjectName);
            if (go == null)
            {
                go = Terrain.CreateTerrainGameObject(data);
                go.name = TerrainObjectName;
                Undo.RegisterCreatedObjectUndo(go, "Make Salok terrain");
            }
            var terrain = go.GetComponent<Terrain>();
            var col = go.GetComponent<TerrainCollider>();
            terrain.terrainData = data;
            if (col != null) col.terrainData = data;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 250f;
            terrain.drawInstanced = true;
            go.transform.SetPositionAndRotation(origin, Quaternion.identity);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

            // Tell the maze to stop making floor tiles, and rebuild it.
            Undo.RecordObject(builder, "Use terrain floor");
            builder.terrainFloor = true;
            builder.Build();
            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            Selection.activeGameObject = go;
            Debug.Log($"[SalokKit] Terrain floor ready: {size:0} x {size:0} m, {TerrainMargin:0} m of land past the outer wall " +
                      $"({FlatBand:0} m of it flat). Maze floor tiles are off (MazeBuilder > Terrain Floor). Save the scene.");
        }

        static float OutsideDistance(float x, float z, float minX, float maxX, float minZ, float maxZ)
        {
            float dx = Mathf.Max(minX - x, 0f, x - maxX);
            float dz = Mathf.Max(minZ - z, 0f, z - maxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float Smooth(float from, float to, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, v));

        /// <summary>The team's DryGround material textures, or the raw dry ground photo if the material is missing.</summary>
        static Texture2D GroundTexture(out Texture2D normal)
        {
            normal = null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);
            Texture2D tex = null;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseMap")) tex = mat.GetTexture("_BaseMap") as Texture2D;
                if (tex == null && mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex") as Texture2D;
                if (mat.HasProperty("_BumpMap")) normal = mat.GetTexture("_BumpMap") as Texture2D;
            }
            return tex != null ? tex : AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexFallback);
        }

        static TerrainLayer MakeLayer(string name, Texture2D diffuse, Texture2D normal, float tileMetres)
        {
            string path = $"{TerrainFolder}/{name}.terrainLayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            if (diffuse == null) Debug.LogWarning($"[SalokKit] No texture found for terrain layer {name}; pick one on {path}.");
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tileMetres, tileMetres);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        [MenuItem("Salok/Environment/Open kit preview", priority = 20)]
        public static void PreviewMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<MazeTileSet>(TileSetPath) == null) BuildKit(false);
            BuildPreviewScene();
        }

        static void ApplyAndBuild(MazeTileSet tileSet)
        {
            var builders = Object.FindObjectsByType<MazeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (builders.Length == 0) { Debug.LogError("[SalokKit] No MazeBuilder in the open scene."); return; }
            foreach (var b in builders)
            {
                Undo.RecordObject(b, "Apply Salok tile set");
                b.tileSet = tileSet;
                b.Build();
                EditorUtility.SetDirty(b);
            }
        }

        // ------------------------------------------------------------------ Kit

        static bool rebuildAll;

        static void BuildKit(bool rebuild)
        {
            rebuildAll = rebuild;
            ReadBalance();
            EnsureFolder(KitRoot + "/Walls");
            EnsureFolder(KitRoot + "/Pillars");
            EnsureFolder(KitRoot + "/Gates");
            EnsureFolder(KitRoot + "/Doors");
            EnsureFolder(KitRoot + "/Floors");
            EnsureFolder(ClueRoot);

            var temp = EditorSceneManager.NewPreviewScene();
            try
            {
                // Walls: plain stone, draped ivy, ivy patches and moss, rusted plate.
                var wallA = Make(temp, KitRoot + "/Walls/SalokWall_Stone.prefab", r => WallBase(r, WallStone1));
                var wallB = Make(temp, KitRoot + "/Walls/SalokWall_Ivy.prefab", r =>
                {
                    WallBase(r, WallStone2, WallStone1);
                    Place(r, IvyDraped1, new Bounds(new Vector3(-0.4f, wallH - 3.5f, 0.15f), new Vector3(tile * 0.7f, 7f, 0.4f)),
                          0f, Fit.Uniform, Align.Center, Align.Max, Align.Max);
                });
                var wallC = Make(temp, KitRoot + "/Walls/SalokWall_Overgrown.prefab", r =>
                {
                    WallBase(r, WallStone3);
                    Place(r, IvyDraped2, new Bounds(new Vector3(0.6f, wallH - 2.5f, 0.15f), new Vector3(tile * 0.55f, 5f, 0.4f)),
                          0f, Fit.Uniform, Align.Center, Align.Max, Align.Max);
                    Place(r, Ivy4, new Bounds(new Vector3(-0.9f, 1.6f, 0.1f), new Vector3(tile * 0.5f, 3f, 0.3f)),
                          0f, Fit.Uniform, Align.Center, Align.Min, Align.Max);
                });
                var wallD = Make(temp, KitRoot + "/Walls/SalokWall_Cracked.prefab", r =>
                {
                    WallBase(r, WallStone1);
                    Place(r, IvyDraped3, new Bounds(new Vector3(-1.1f, wallH - 2f, 0.15f), new Vector3(tile * 0.35f, 4f, 0.4f)),
                          0f, Fit.Uniform, Align.Center, Align.Max, Align.Max);
                    Place(r, Ivy7, new Bounds(new Vector3(1.1f, 0.5f, 0.1f), new Vector3(tile * 0.4f, 2f, 0.3f)),
                          0f, Fit.Uniform, Align.Center, Align.Min, Align.Max);
                });
                var wallE = Make(temp, KitRoot + "/Walls/SalokWall_Rusted.prefab", r =>
                {
                    WallBase(r, WallStone2, WallStone3);
                    Place(r, RustPlate, new Bounds(new Vector3(0.3f, 3.2f, 0.08f), new Vector3(tile * 0.45f, 3f, 0.25f)),
                          0f, Fit.Uniform, Align.Center, Align.Center, Align.Max);
                    Place(r, Ivy1, new Bounds(new Vector3(-1.2f, wallH - 4f, 0.1f), new Vector3(tile * 0.35f, 3f, 0.3f)),
                          0f, Fit.Uniform, Align.Center, Align.Max, Align.Max);
                });

                // Corner pillar: hides the seams where panels meet.
                // Box collider so players can't walk through the part that sticks out of the wall.
                var pillar = Make(temp, KitRoot + "/Pillars/SalokPillar.prefab", r =>
                {
                    Stack(r, PillarLarge, new Bounds(new Vector3(0f, (wallH + 0.6f) * 0.5f, 0f), new Vector3(1.3f, wallH + 0.6f, 1.3f)), 0f);
                    ClearColliders(r);
                    Box(r, new Vector3(0f, (wallH + 0.6f) * 0.5f, 0f), new Vector3(1.3f, wallH + 0.6f, 1.3f));
                });

                // Gate (G tiles): a steel sluice that slides sideways into the wall, with the wall's own
                // stone and top trim above it so the gate reads as part of the wall.
                var gate = Make(temp, KitRoot + "/Gates/SalokGate_Sluice.prefab", BuildGate);

                // Hallway doors (D and T tiles; none in the current map, kept for later).
                var door = Make(temp, KitRoot + "/Doors/SalokDoor_Metal.prefab", r =>
                {
                    Place(r, MetalDoor, new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(tile, 3f, 0.4f)),
                          0f, Fit.Stretch, Align.Center, Align.Min, Align.Center);
                    SolidBox(r, new Vector3(0f, 1.5f, 0f), new Vector3(tile, 3f, 0.3f));
                });

                // Room door: steel frame with a panel that rises into it (stem and handwheel on top).
                var roomDoor = Make(temp, KitRoot + "/Doors/SalokRoomDoor_Sluice.prefab", BuildRoomDoor);

                // Floors: parched sand, some with cracks and rubble.
                var floorA = Make(temp, KitRoot + "/Floors/SalokFloor_Sand.prefab", r => FloorBase(r));
                var floorB = Make(temp, KitRoot + "/Floors/SalokFloor_Cracked.prefab", r =>
                {
                    FloorBase(r);
                    Decal(r, Fissure1, new Vector3(0.3f, 0f, -0.2f), tile * 0.8f, 20f);
                });
                var floorC = Make(temp, KitRoot + "/Floors/SalokFloor_Fissures.prefab", r =>
                {
                    FloorBase(r);
                    Decal(r, Fissure2, new Vector3(-0.6f, 0f, 0.5f), tile * 0.6f, 110f);
                    Decal(r, Fissure3, new Vector3(0.9f, 0f, -0.8f), tile * 0.45f, 250f);
                });
                var floorD = Make(temp, KitRoot + "/Floors/SalokFloor_Rubble.prefab", r =>
                {
                    FloorBase(r);
                    Place(r, CrackedRock, new Bounds(new Vector3(1.2f, 0.15f, 1.1f), new Vector3(0.9f, 0.3f, 0.9f)),
                          35f, Fit.Uniform, Align.Center, Align.Min, Align.Center);
                    Decal(r, Fissure1, new Vector3(-0.5f, 0f, -0.4f), tile * 0.5f, 300f);
                });
                // Room floor: the same look the rooms have today (ground1 on a 4 x 4 tile), so rooms don't change.
                var roomFloor = Make(temp, KitRoot + "/Floors/SalokFloor_RoomKeep.prefab", r =>
                {
                    var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slab.name = "RoomFloor";
                    slab.transform.SetParent(r.transform, false);
                    slab.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                    slab.transform.localScale = new Vector3(tile, 0.2f, tile);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(RoomFloorMat);
                    if (mat != null) slab.GetComponent<Renderer>().sharedMaterial = mat;
                });

                // Clue bodies (rooms): the "Note" child is what the player reads.
                Make(temp, ClueRoot + "/Clue_SkeletonLying.prefab", r =>
                {
                    var body = Place(r, SkeletonLying, new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1.9f, 0.5f, 0.8f)),
                                     0f, Fit.Uniform, Align.Center, Align.Min, Align.Center, fitByLongest: true);
                    var b = Measure(body);
                    NoteOn(r, Scroll, new Vector3(b.max.x - 0.25f, 0.12f, b.max.z - 0.1f), 70f);
                });
                Make(temp, ClueRoot + "/Clue_SkeletonSitting.prefab", r =>
                {
                    Place(r, LimestoneBlock, new Bounds(new Vector3(0f, 0f, -0.45f), new Vector3(1.4f, 1.1f, 0.6f)),
                          0f, Fit.Stretch, Align.Center, Align.Min, Align.Center);
                    var body = Place(r, SkeletonSitting, new Bounds(new Vector3(0f, 0f, 0.15f), new Vector3(1f, 1.1f, 1.2f)),
                                     0f, Fit.Uniform, Align.Center, Align.Min, Align.Center, fitByLongest: true);
                    var b = Measure(body);
                    NoteOn(r, ScrollOpen, new Vector3(b.center.x + 0.1f, b.min.y + 0.45f, b.max.z - 0.3f), 15f);
                });
                Make(temp, ClueRoot + "/Clue_Mummy.prefab", r =>
                {
                    var body = Place(r, Mummy, new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1.9f, 0.6f, 0.8f)),
                                     0f, Fit.Uniform, Align.Center, Align.Min, Align.Center, fitByLongest: true);
                    var b = Measure(body);
                    NoteOn(r, Scroll, new Vector3(b.center.x, b.max.y + 0.02f, b.center.z), 90f);
                    Place(r, BonePileSmall, new Bounds(new Vector3(b.min.x - 0.35f, 0f, b.center.z + 0.3f), new Vector3(0.5f, 0.3f, 0.5f)),
                          40f, Fit.Uniform, Align.Center, Align.Min, Align.Center);
                });

                // Tile set
                var set = AssetDatabase.LoadAssetAtPath<MazeTileSet>(TileSetPath);
                bool newSet = set == null || rebuildAll;
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<MazeTileSet>();
                    AssetDatabase.CreateAsset(set, TileSetPath);
                }
                if (set.wallCoreMaterial == null || newSet)
                {
                    set.wallCore = true;
                    set.wallCoreMaterial = CoreMaterial();
                    EditorUtility.SetDirty(set);
                }
                if (newSet)
                {
                    set.wallMode = WallMode.Faces;
                    set.wallFaces = Pieces((wallA, 3f), (wallB, 2f), (wallC, 1.5f), (wallD, 1.5f), (wallE, 1f));
                    set.wallCaps = new MazePiece[0];
                    set.wallBlocks = new MazePiece[0];
                    set.pillars = Pieces((pillar, 1f));
                    set.pillarsOnOuterCorners = true;
                    set.pillarsOnInnerCorners = true;
                    set.gates = Pieces((gate, 1f));
                    set.doors = Pieces((door, 1f));
                    set.roomDoor = roomDoor;
                    set.valves = new MazePiece[0];   // valve grates keep their current look
                    set.floors = Pieces((floorA, 4f), (floorB, 2f), (floorC, 1.5f), (floorD, 1f));
                    set.spawnFloors = Pieces((floorA, 4f), (floorB, 1f));
                    set.roomFloors = Pieces((roomFloor, 1f));
                    EditorUtility.SetDirty(set);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temp);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SalokKit] Kit ready in {KitRoot} and {ClueRoot} (tile {tile} m, wall {wallH} m).");
        }

        static MazePiece[] Pieces(params (GameObject prefab, float weight)[] list)
        {
            var result = new List<MazePiece>();
            foreach (var (prefab, weight) in list)
            {
                if (prefab == null) continue;
                result.Add(new MazePiece
                {
                    prefab = prefab, weight = weight, fit = FitMode.None, alignToBounds = false,
                    positionOffset = Vector3.zero, yRotation = 0f, scale = Vector3.one,
                });
            }
            return result.ToArray();
        }

        // ------------------------------------------------------------------ Building blocks

        /// <summary>The stone panel (stacked to wall height), floor and top trims, and a lid over the wall's half tile.</summary>
        /// <param name="stone">The bottom tier's stone piece.</param>
        /// <param name="upper">The piece for the tiers above it (null = the same). Wall_02 has a slot
        /// cut low in it, so it only goes on the bottom tier or the slot repeats up the wall.</param>
        static void WallBase(GameObject root, string stone, string upper = null)
        {
            // Turned so the side with the stone detail faces the corridor (+Z): a panel facing the
            // wrong way shows its invisible back, which looks like a hole.
            Stack(root, stone, new Bounds(new Vector3(0f, wallH * 0.5f, -WallDepth * 0.5f), new Vector3(tile, wallH, WallDepth)), FrontYaw(stone),
                  upper, upper != null ? FrontYaw(upper) : 0f);
            Place(root, TrimFloor, new Bounds(new Vector3(0f, 0.35f, -WallDepth * 0.5f + 0.15f), new Vector3(tile, 0.7f, WallDepth + 0.3f)),
                  FrontYaw(TrimFloor), Fit.Stretch, Align.Center, Align.Min, Align.Max);
            // Top trim reaches back over half the wall tile, so the wall top never looks hollow.
            Place(root, TrimTop, new Bounds(new Vector3(0f, wallH - 0.35f, -tile * 0.25f + 0.15f), new Vector3(tile, 0.7f, tile * 0.5f + 0.3f)),
                  0f, Fit.Stretch, Align.Center, Align.Max, Align.Max);
        }

        /// <summary>Sand tile with its top on y = 0 and one clean box collider.</summary>
        static void FloorBase(GameObject root)
        {
            Place(root, Sand, new Bounds(new Vector3(0f, -0.15f, 0f), new Vector3(tile, 0.3f, tile)),
                  0f, Fit.Stretch, Align.Center, Align.Max, Align.Center);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.1f, 0f);
            box.size = new Vector3(tile, 0.2f, tile);
        }

        /// <summary>A flat crack mesh lying on the floor (no collision).</summary>
        static void Decal(GameObject root, string path, Vector3 pos, float width, float yRot)
        {
            var go = Place(root, path, new Bounds(pos + Vector3.up * 0.05f, new Vector3(width, 0.1f, width)),
                           yRot, Fit.Uniform, Align.Center, Align.Min, Align.Center, fitByLongest: true);
            if (go == null) return;
            // Sit it just above the sand so it doesn't flicker.
            var b = Measure(go);
            go.transform.localPosition += Vector3.up * (0.01f - b.min.y);
        }

        /// <summary>The readable note: a scroll named "Note" with a small trigger-friendly collider.</summary>
        static void NoteOn(GameObject root, string path, Vector3 pos, float yRot)
        {
            var holder = new GameObject("Note");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = pos;
            holder.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            var scroll = Place(holder, path, new Bounds(Vector3.zero, new Vector3(0.45f, 0.15f, 0.45f)),
                               0f, Fit.Uniform, Align.Center, Align.Min, Align.Center, fitByLongest: true);
            if (scroll != null) scroll.name = "Scroll";
            var box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.08f, 0f);
            box.size = new Vector3(0.5f, 0.16f, 0.5f);
        }

        static void SolidBox(GameObject root, Vector3 centre, Vector3 size)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var box = root.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        // ------------------------------------------------------------------ Gate and room door

        static void BuildGate(GameObject r)
        {
            float w = tile, h = Mathf.Min(GateOpeningHeight, wallH - 1.5f), lintel = wallH - h;
            float stoneYaw = FrontYaw(WallStone1);

            // Above the opening: the same stone and top trim as the walls, seen from both sides.
            Stack(r, WallStone1, new Bounds(new Vector3(0f, h + lintel * 0.5f, 0.25f), new Vector3(w, lintel, 0.5f)), stoneYaw);
            Stack(r, WallStone1, new Bounds(new Vector3(0f, h + lintel * 0.5f, -0.25f), new Vector3(w, lintel, 0.5f)), stoneYaw + 180f);
            Place(r, TrimTop, new Bounds(new Vector3(0f, wallH - 0.35f, 0f), new Vector3(w, 0.7f, 1.3f)),
                  0f, Fit.Stretch, Align.Center, Align.Max, Align.Center);

            // Steel frame: a header beam on each face and guide posts both sides of the panel.
            foreach (float z in new[] { 0.35f, -0.35f })
                PlaceFit(r, Girder, new Bounds(new Vector3(0f, h + 0.2f, z), new Vector3(w + 0.2f, 0.4f, 0.3f)));
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { 0.3f, -0.3f })
                PlaceFit(r, Girder, new Bounds(new Vector3(x * (w * 0.5f - 0.15f), h * 0.5f, z), new Vector3(0.3f, h, 0.25f)));

            // Handwheels on the header, like the lifting wheels on the moodboard gate.
            foreach (float x in new[] { -1.1f, 1.1f })
            foreach (float z in new[] { 0.55f, -0.55f })
                PlaceFit(r, Handwheel, new Bounds(new Vector3(x, h + 0.95f, z), new Vector3(1f, 1f, 0.15f)));

            // The panel that slides: a steel sheet with horizontal ribs on both faces.
            var leaf = Child(r, "Leaf");
            PlaceFit(leaf, SteelSheet, new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.3f, h - 0.05f, 0.12f)));
            foreach (float t in new[] { 0.1f, 0.37f, 0.63f, 0.9f })
            foreach (float z in new[] { 0.1f, -0.1f })
                PlaceFit(leaf, Girder, new Bounds(new Vector3(0f, h * t, z), new Vector3(w - 0.4f, 0.22f, 0.1f)));

            ClearColliders(r);
            Box(leaf, new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.2f, h, 0.5f));
            foreach (float x in new[] { -1f, 1f })
                Box(r, new Vector3(x * (w * 0.5f - 0.15f), h * 0.5f, 0f), new Vector3(0.3f, h, 0.85f));

            var slider = r.AddComponent<GateSlider>();
            slider.leaf = leaf.transform;
            slider.slideDistance = w;
            slider.slideDirection = Vector3.right;
        }

        static void BuildRoomDoor(GameObject r)
        {
            float w = tile, h = RoomDoorHeight, frame = h * 2f + 0.4f;

            // Frame: two steel posts and a header, tall enough to take the raised panel.
            foreach (float x in new[] { -1f, 1f })
                PlaceFit(r, Girder, new Bounds(new Vector3(x * (w * 0.5f - 0.15f), frame * 0.5f, 0f), new Vector3(0.3f, frame, 0.35f)));
            PlaceFit(r, Girder, new Bounds(new Vector3(0f, frame + 0.2f, 0f), new Vector3(w + 0.1f, 0.4f, 0.4f)));
            PlaceFit(r, Handwheel, new Bounds(new Vector3(0f, frame + 0.9f, 0f), new Vector3(1f, 1f, 0.15f)));

            // The panel that rises: sheet, vertical ribs, a top bar and the lifting stem.
            var leaf = Child(r, "Leaf");
            PlaceFit(leaf, SteelSheet, new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.6f, h - 0.05f, 0.1f)));
            foreach (float x in new[] { -1.1f, 0f, 1.1f })
            foreach (float z in new[] { 0.08f, -0.08f })
                PlaceFit(leaf, Girder, new Bounds(new Vector3(x, h * 0.5f, z), new Vector3(0.18f, h - 0.2f, 0.1f)));
            PlaceFit(leaf, Girder, new Bounds(new Vector3(0f, h - 0.12f, 0f), new Vector3(w - 0.6f, 0.24f, 0.26f)));
            float stemLen = frame + 0.6f - h;
            PlaceFit(leaf, Stem, new Bounds(new Vector3(0f, h + stemLen * 0.5f, 0f), new Vector3(0.12f, stemLen, 0.12f)));

            ClearColliders(r);
            Box(leaf, new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.3f, h, 0.3f));
            foreach (float x in new[] { -1f, 1f })
                Box(r, new Vector3(x * (w * 0.5f - 0.15f), h, 0f), new Vector3(0.3f, h * 2f, 0.4f));
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void ClearColliders(GameObject root)
        {
            // Switched off (a prefab instance's parts can't always be removed); GateSlider and RoomDoor
            // only ever switch the colliders that were on, so these stay off.
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        static void Box(GameObject target, Vector3 centre, Vector3 size)
        {
            var box = target.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        static Material CoreMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CoreMatPath);
            if (mat != null) return mat;
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var shader = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Standard");
            mat = new Material(shader) { color = new Color(0.26f, 0.25f, 0.24f) };
            AssetDatabase.CreateAsset(mat, CoreMatPath);
            return mat;
        }

        /// <summary>
        /// The Y turn (0, 90, 180 or -90) that makes a piece's detailed side face +Z, found from its
        /// mesh: the triangles' facing directions are added up and the strongest side wins.
        /// 0 if it can't tell (a closed block, or a mesh that can't be read).
        /// </summary>
        static float FrontYaw(string path)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) return 0f;
            Vector3 sum = Vector3.zero;
            float area = 0f;
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                try
                {
                    var v = mesh.vertices;
                    var t = mesh.triangles;
                    var m = mf.transform.localToWorldMatrix;
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                        Vector3 n = Vector3.Cross(b - a, c - a);
                        sum += n;
                        area += n.magnitude;
                    }
                }
                catch (System.Exception) { return 0f; }
            }
            sum.y = 0f;
            if (area <= 0f || sum.magnitude < area * 0.05f) return 0f;   // faces every way equally: leave it
            if (Mathf.Abs(sum.x) > Mathf.Abs(sum.z)) return sum.x > 0f ? -90f : 90f;
            return sum.z >= 0f ? 0f : 180f;
        }

        static readonly Vector3[] AxisTurns =
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector3(90f, 0f, 0f),
            new Vector3(0f, 0f, 90f), new Vector3(90f, 90f, 0f), new Vector3(0f, 90f, 90f),
        };

        /// <summary>
        /// Puts a piece in a box, first turning it in 90 degree steps so its long side follows the
        /// box's long side and its thin side the box's thin side (a beam stood up as a post, a sheet
        /// stood up as a panel), then stretching it to fill the box exactly.
        /// </summary>
        static GameObject PlaceFit(GameObject root, string path, Bounds box)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning($"[SalokKit] Missing piece: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = src.transform.localScale;

            int wantMin = MinAxis(box.size), wantMax = MaxAxis(box.size);
            bool tie = SecondLargest(box.size) > box.size[wantMax] * 0.9f;   // e.g. a wheel: only the thin side matters
            Quaternion chosen = src.transform.localRotation;
            foreach (var e in AxisTurns)
            {
                go.transform.localRotation = Quaternion.Euler(e) * src.transform.localRotation;
                var s = Measure(go).size;
                if (s == Vector3.zero) break;
                if (MinAxis(s) == wantMin && (tie || MaxAxis(s) == wantMax)) { chosen = go.transform.localRotation; break; }
            }
            go.transform.localRotation = chosen;

            var b = Measure(go);
            if (b.size == Vector3.zero) return go;
            // Scale each of the piece's own axes by the box/size ratio of the world axis it now lines up with.
            Vector3 f = Vector3.one;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = go.transform.localRotation * new Vector3(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0);
                int wAxis = MaxAxis(new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
                f[i] = b.size[wAxis] > 1e-4f ? box.size[wAxis] / b.size[wAxis] : 1f;
            }
            go.transform.localScale = Vector3.Scale(go.transform.localScale, f);
            b = Measure(go);
            go.transform.localPosition += box.center - b.center;
            return go;
        }

        static int MinAxis(Vector3 v) => v.x <= v.y && v.x <= v.z ? 0 : v.y <= v.z ? 1 : 2;
        static int MaxAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2;
        static float SecondLargest(Vector3 v) => v.x + v.y + v.z - Mathf.Max(v.x, v.y, v.z) - Mathf.Min(v.x, v.y, v.z);

        enum Fit { Stretch, Uniform }
        enum Align { Min, Center, Max }

        /// <summary>Stacks copies of a piece on top of each other to fill a box (keeps the stone blocks from stretching too tall).</summary>
        static void Stack(GameObject root, string path, Bounds box, float yRot, string upperPath = null, float upperYRot = 0f)
        {
            var probe = Place(root, path, box, yRot, Fit.Uniform, Align.Center, Align.Min, Align.Center);
            if (probe == null) return;
            float natural = Measure(probe).size.y;   // height once its width fills the box
            Object.DestroyImmediate(probe);
            int tiers = Mathf.Clamp(Mathf.RoundToInt(box.size.y / Mathf.Max(0.1f, natural)), 1, 6);
            float h = box.size.y / tiers;
            for (int i = 0; i < tiers; i++)
            {
                var tierBox = new Bounds(new Vector3(box.center.x, box.min.y + h * (i + 0.5f), box.center.z), new Vector3(box.size.x, h, box.size.z));
                bool up = i > 0 && upperPath != null;
                var go = Place(root, up ? upperPath : path, tierBox, up ? upperYRot : yRot, Fit.Stretch, Align.Center, Align.Min, Align.Center);
                if (go != null) go.name = $"{go.name}_{i + 1}";
            }
        }

        /// <summary>
        /// Puts a prefab (kept linked, so it stays editable) inside a box in the root's space.
        /// Stretch fills the box on every axis; Uniform scales by the width (or the longest side)
        /// and keeps proportions. Align says which side of the box each axis lines up with.
        /// </summary>
        static GameObject Place(GameObject root, string path, Bounds box, float yRot, Fit fit,
                                Align ax, Align ay, Align az, bool fitByLongest = false)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning($"[SalokKit] Missing piece: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f) * src.transform.localRotation;
            go.transform.localScale = src.transform.localScale;

            var b = Measure(go);
            if (b.size == Vector3.zero) return go;

            Vector3 f;
            if (fit == Fit.Stretch)
            {
                f = new Vector3(Safe(box.size.x, b.size.x), Safe(box.size.y, b.size.y), Safe(box.size.z, b.size.z));
            }
            else
            {
                float k = fitByLongest
                    ? Mathf.Min(Safe(box.size.x, b.size.x), Safe(box.size.z, b.size.z), Safe(box.size.y, b.size.y))
                    : Safe(box.size.x, b.size.x);
                f = new Vector3(k, k, k);
            }
            // World X/Z factors map onto the piece's own X/Z, swapped if it's turned 90 degrees.
            if (Mathf.Abs(Mathf.Sin(yRot * Mathf.Deg2Rad)) > 0.7f) f = new Vector3(f.z, f.y, f.x);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, f);

            b = Measure(go);
            Vector3 want = new Vector3(Pick(box.min.x, box.center.x, box.max.x, ax),
                                       Pick(box.min.y, box.center.y, box.max.y, ay),
                                       Pick(box.min.z, box.center.z, box.max.z, az));
            Vector3 have = new Vector3(Pick(b.min.x, b.center.x, b.max.x, ax),
                                       Pick(b.min.y, b.center.y, b.max.y, ay),
                                       Pick(b.min.z, b.center.z, b.max.z, az));
            go.transform.localPosition += want - have;
            return go;

            static float Safe(float want, float have) => have > 1e-4f ? want / have : 1f;
            static float Pick(float min, float mid, float max, Align a) => a == Align.Min ? min : a == Align.Max ? max : mid;
        }

        /// <summary>Renderer bounds of an object in its kit root's space (the root sits at the origin).</summary>
        static Bounds Measure(GameObject go)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Bounds rb;
                if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) rb = TransformBounds(sk.transform, sk.sharedMesh.bounds);
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    rb = TransformBounds(r.transform, mf.sharedMesh.bounds);
                }
                if (first) { b = rb; first = false; } else b.Encapsulate(rb);
            }
            return b;
        }

        static Bounds TransformBounds(Transform t, Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            var m = t.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return b;
        }

        /// <summary>Builds one prefab (unless it exists and we're keeping edits) and returns the saved asset.</summary>
        static GameObject Make(UnityEngine.SceneManagement.Scene temp, string path, System.Action<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuildAll) return existing;

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, temp);
            try
            {
                build(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Preview scene

        static void BuildPreviewScene()
        {
            ReadBalance();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("KitPreview").transform;

            // Row 1: kit prefabs as they appear in the maze.
            float x = 0f;
            foreach (var path in KitPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.transform.position = new Vector3(x, 0f, 0f);
                Label(root, prefab.name, new Vector3(x, -0.2f, 2.5f));
                x += 6f;
            }

            // Row 2: the raw Synty pieces, at their own size, facing the camera.
            x = 0f;
            foreach (var path in PreviewCandidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                var b = Measure(go);
                go.transform.position = new Vector3(x - b.min.x, -b.min.y, 18f - b.center.z);
                Label(root, $"{prefab.name}\n{b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0}", new Vector3(x + b.size.x * 0.5f, -0.2f, 18f + 3f));
                x += Mathf.Max(3f, b.size.x) + 2f;
            }

            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(30f, 6f, 8f), Quaternion.Euler(18f, 180f, 0f), 45f);
                view.Repaint();
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static IEnumerable<string> KitPaths()
        {
            foreach (var folder in new[] { KitRoot + "/Walls", KitRoot + "/Pillars", KitRoot + "/Gates", KitRoot + "/Doors", KitRoot + "/Floors", ClueRoot })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                    yield return AssetDatabase.GUIDToAssetPath(guid);
            }
        }

        static void Label(Transform parent, string text, Vector3 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.18f;
            tm.fontSize = 40;
            tm.anchor = TextAnchor.UpperCenter;
            tm.color = Color.white;
        }
    }
}
