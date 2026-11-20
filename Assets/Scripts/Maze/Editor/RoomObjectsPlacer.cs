// ============================================================================
// RoomObjectsPlacer.cs   (Editor only)
// Puts every room's objects into the scene so they can be seen and moved in the editor,
// not only during Play.
//
// MENU: Salok > Rooms
//   Place room objects in scene - for every room, makes MazeBuilder > Room_Objects > RoomContent_N
//                                  with its water object (Cistern, SealedJug or Remains: the same
//                                  outcome the game rolls with GameBalance's room seed) and its
//                                  clue body (skeleton or mummy holding the note).
//                                  Running it again asks: keep what you moved and only add
//                                  missing rooms, or reset everything.
//   Remove room objects          - deletes Room_Objects (the game goes back to making them itself).
//   Place room doors in scene    - every room door (the Maze Tile Set's Room Door prefab, still linked)
//                                  under MazeBuilder > Room_Doors as RoomDoor_<n>_<x>_<y>_<side>.
//                                  Move or turn them; keep their names. At Play they get their
//                                  lock and minigame and move under their room (so zone 2's turn).
//   Remove room doors            - deletes Room_Doors (doors go back to their default spots).
//
// HOW TO EDIT
//   - Select a room's objects under Room_Objects (or click them in the Scene view) and move,
//     turn or scale them. Keep their names: the game finds them by name
//     (Cistern / SealedJug / Remains, ClueBody, and the clue body's child "Note").
//   - Don't move the RoomContent_N object itself: at Play it snaps onto its room. Move its children.
//   - Swap a look: replace the Cistern cylinder with any model named "Cistern" (same for the others).
//   - Room_Objects is outside _Generated, so "Build maze" doesn't delete it. If the maze layout
//     (CSV) changes, run "Place room objects in scene" > "Reset all".
//   - Changing the room seed or a room override can change a room's outcome. The game then swaps
//     in the right object on the same spot; run the menu again to see it in the editor.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastDrop
{
    /// <summary>Places the rooms' water objects and clue bodies into the open scene.</summary>
    public static class RoomObjectsPlacer
    {
        const string MaterialFolder = "Assets/Art/Rooms";

        [MenuItem("Salok/Rooms/Place room objects in scene", priority = 1)]
        public static void PlaceMenu()
        {
            var builder = Object.FindAnyObjectByType<MazeBuilder>(FindObjectsInactive.Include);
            if (builder == null) { Debug.LogError("[RoomObjects] Open the maze scene first (no MazeBuilder found)."); return; }
            if (builder.transform.Find(MazeBuilder.GeneratedRootName) == null)
            {
                Debug.LogError("[RoomObjects] Build the maze first (select the Maze, press Build maze).");
                return;
            }
            if (!builder.LoadGrid()) { Debug.LogError("[RoomObjects] The MazeBuilder has no layout CSV."); return; }
            var balance = builder.balance != null ? builder.balance : FindBalance();
            if (balance == null) { Debug.LogError("[RoomObjects] No GameBalance asset found (needed for the room seed)."); return; }

            var root = builder.transform.Find(RoomContent.PlacedRootName);
            bool keep = false;
            if (root != null && root.childCount > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Room objects",
                    "Room objects are already in the scene. Keep the ones you moved and only add missing rooms, " +
                    "or reset every room to its default spot?",
                    "Keep my edits", "Cancel", "Reset all");
                if (choice == 1) return;
                keep = choice == 0;
                if (!keep)
                {
                    Undo.DestroyObjectImmediate(root.gameObject);
                    root = null;
                }
            }
            if (root == null)
            {
                root = new GameObject(RoomContent.PlacedRootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Place room objects");
                root.SetParent(builder.transform, false);
            }

            // Same maths as RoomContent: d (0 near the hub, 1 farthest) by ring, then the seeded roll.
            var grid = builder.Grid;
            var infos = grid.Rooms;
            int nearest = int.MaxValue, farthest = int.MinValue;
            foreach (var info in infos)
            {
                int ring = grid.Ring(Centre(info));
                nearest = Mathf.Min(nearest, ring);
                farthest = Mathf.Max(farthest, ring);
            }

            var areas = builder.GetComponentsInChildren<MazeArea>(true);
            var bodies = Resources.LoadAll<GameObject>("Salok/ClueBodies");
            var scene = Object.FindAnyObjectByType<RoomContent>(FindObjectsInactive.Include);
            float noteOffset = (scene != null ? scene.noteOffsetTiles : 1f) * builder.TileSize;
            int made = 0, kept = 0, swapped = 0;

            for (int i = 0; i < infos.Count; i++)
            {
                float d = RoomRules.Distance01(grid.Ring(Centre(infos[i])), nearest, farthest);
                var outcome = RoomRules.Decide(balance, i, d);
                MazeArea area = null;
                foreach (var a in areas)
                    if (a != null && a.kind == MazeArea.Kind.Room && a.tiles.Equals(infos[i].Tiles)) { area = a; break; }
                Vector3 centre = area != null ? area.transform.position
                    : builder.GridToWorld(Centre(infos[i]).x, Centre(infos[i]).y);

                var holder = root.Find($"RoomContent_{i}");
                if (holder != null && keep)
                {
                    kept++;
                    if (SwapWaterIfNeeded(holder, outcome)) swapped++;
                    if (holder.Find("ClueBody") == null && holder.Find("SurvivorNote") == null)
                        AddClueBody(holder, bodies, i, noteOffset);
                    continue;
                }

                holder = new GameObject($"RoomContent_{i}").transform;
                holder.SetParent(root, false);
                holder.position = centre;
                holder.rotation = Quaternion.identity;
                MakeWater(holder, outcome);
                AddClueBody(holder, bodies, i, noteOffset);
                made++;
            }

            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log($"[RoomObjects] {infos.Count} rooms: {made} placed, {kept} kept" +
                      (swapped > 0 ? $", {swapped} water objects swapped for a new outcome" : "") +
                      ". Move the children of Room_Objects > RoomContent_N, then save the scene.");
        }

        [MenuItem("Salok/Rooms/Remove room objects", priority = 2)]
        public static void RemoveMenu()
        {
            var builder = Object.FindAnyObjectByType<MazeBuilder>(FindObjectsInactive.Include);
            var root = builder != null ? builder.transform.Find(RoomContent.PlacedRootName) : null;
            if (root == null) { Debug.Log("[RoomObjects] Nothing to remove."); return; }
            if (!EditorUtility.DisplayDialog("Room objects", "Delete Room_Objects and every change made to it?", "Delete", "Cancel")) return;
            Undo.DestroyObjectImmediate(root.gameObject);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        // ------------------------------------------------------------------ Doors

        [MenuItem("Salok/Rooms/Place room doors in scene", priority = 11)]
        public static void PlaceDoorsMenu()
        {
            var builder = Object.FindAnyObjectByType<MazeBuilder>(FindObjectsInactive.Include);
            if (builder == null) { Debug.LogError("[RoomDoors] Open the maze scene first (no MazeBuilder found)."); return; }
            if (!builder.LoadGrid()) { Debug.LogError("[RoomDoors] The MazeBuilder has no layout CSV."); return; }

            var root = builder.transform.Find(RoomDoors.PlacedRootName);
            bool keep = false;
            if (root != null && root.childCount > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Room doors",
                    "Room doors are already in the scene. Keep the ones you moved and only add missing doors, " +
                    "or reset every door to its default spot and look?",
                    "Keep my edits", "Cancel", "Reset all");
                if (choice == 1) return;
                keep = choice == 0;
                if (!keep) { Undo.DestroyObjectImmediate(root.gameObject); root = null; }
            }
            if (root == null)
            {
                root = new GameObject(RoomDoors.PlacedRootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Place room doors");
                root.SetParent(builder.transform, false);
            }

            // Same size as at Play (RoomDoors > Door Height, capped at the wall height).
            var sceneDoors = Object.FindAnyObjectByType<RoomDoors>(FindObjectsInactive.Include);
            float height = Mathf.Min(sceneDoors != null ? sceneDoors.doorHeight : 3f, builder.WallHeight);
            float thick = sceneDoors != null ? sceneDoors.doorThickness : 0.25f;
            var prefab = builder.tileSet != null ? builder.tileSet.roomDoor : null;

            int made = 0, kept = 0;
            foreach (var spot in RoomDoorLayout.Find(builder.Grid))
            {
                string name = RoomDoors.DoorName(spot);
                if (keep && root.Find(name) != null) { kept++; continue; }
                RoomDoors.DefaultPose(builder, spot, out var pos, out var rot);
                var door = new GameObject(name).transform;
                door.SetParent(root, false);
                door.localPosition = pos;
                door.localRotation = rot;
                if (prefab != null)
                {
                    var art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, door);   // stays linked to its prefab
                    art.name = "DoorArt";
                }
                else
                {
                    var slab = Primitive(PrimitiveType.Cube, door, new Vector3(0f, height * 0.5f, 0f),
                                         new Vector3(builder.TileSize, height, thick),
                                         builder.doorMaterial != null ? builder.doorMaterial : Mat("Room_DoorGreybox", new Color(0.88f, 0.54f, 0.24f)));
                    slab.name = "Leaf";
                }
                made++;
            }

            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log($"[RoomDoors] {made} doors placed, {kept} kept. Move or turn the RoomDoor_... objects under Room_Doors, then save the scene.");
        }

        [MenuItem("Salok/Rooms/Remove room doors", priority = 12)]
        public static void RemoveDoorsMenu()
        {
            var builder = Object.FindAnyObjectByType<MazeBuilder>(FindObjectsInactive.Include);
            var root = builder != null ? builder.transform.Find(RoomDoors.PlacedRootName) : null;
            if (root == null) { Debug.Log("[RoomDoors] Nothing to remove."); return; }
            if (!EditorUtility.DisplayDialog("Room doors", "Delete Room_Doors and every change made to it?", "Delete", "Cancel")) return;
            Undo.DestroyObjectImmediate(root.gameObject);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        // ------------------------------------------------------------------ Pieces

        static Vector2Int Centre(RoomInfo r) =>
            new Vector2Int(r.Tiles.xMin + r.Tiles.width / 2, r.Tiles.yMin + r.Tiles.height / 2);

        static GameObject MakeWater(Transform holder, RoomOutcome outcome)
        {
            GameObject go;
            switch (outcome)
            {
                case RoomOutcome.Cistern:
                    go = Primitive(PrimitiveType.Cylinder, holder, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 0.6f, 1.6f),
                                   Mat("Room_Cistern", new Color32(70, 140, 220, 255)));
                    break;
                case RoomOutcome.Cache:
                    go = Primitive(PrimitiveType.Cylinder, holder, new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.4f, 0.6f),
                                   Mat("Room_SealedJug", new Color32(230, 150, 60, 255)));
                    break;
                default:
                    go = Primitive(PrimitiveType.Cube, holder, new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 0.9f),
                                   Mat("Room_Remains", new Color32(150, 145, 140, 255)));
                    break;
            }
            go.name = RoomContent.WaterName(outcome);
            return go;
        }

        // Keeps a moved water object's spot if the room's outcome changed.
        static bool SwapWaterIfNeeded(Transform holder, RoomOutcome outcome)
        {
            if (holder.Find(RoomContent.WaterName(outcome)) != null) return false;
            Transform old = null;
            foreach (RoomOutcome o in System.Enum.GetValues(typeof(RoomOutcome)))
                if (old == null) old = holder.Find(RoomContent.WaterName(o));
            var made = MakeWater(holder, outcome);
            if (old != null)
            {
                var p = old.localPosition;
                made.transform.localPosition = new Vector3(p.x, made.transform.localPosition.y, p.z);
                made.transform.localRotation = old.localRotation;
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            return true;
        }

        static void AddClueBody(Transform holder, GameObject[] bodies, int roomIndex, float offset)
        {
            var pos = new Vector3(offset, 0f, 0f);
            if (bodies == null || bodies.Length == 0)
            {
                // No clue bodies: the old note on a crate.
                var crate = Primitive(PrimitiveType.Cube, holder, pos + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.7f),
                                      Mat("Room_Remains", new Color32(150, 145, 140, 255)));
                crate.name = "NoteCrate";
                var note = Primitive(PrimitiveType.Cube, holder, pos + new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.05f, 0.35f),
                                     Mat("Room_Note", new Color32(240, 235, 220, 255)));
                note.name = "SurvivorNote";
                return;
            }
            var prefab = bodies[roomIndex % bodies.Length];
            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);   // stays linked to its prefab
            body.name = "ClueBody";
            body.transform.localPosition = pos;
            body.transform.localRotation = Quaternion.Euler(0f, (roomIndex * 97) % 360, 0f);
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // A plain coloured material saved as an asset (scene objects can't keep a made-up material).
        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            EnsureFolder(MaterialFolder);
            var rp = GraphicsSettings.currentRenderPipeline;
            var shader = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameBalance FindBalance()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameBalance"))
            {
                var gb = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetDatabase.GUIDToAssetPath(guid));
                if (gb != null && gb.name == "GameBalance") return gb;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
