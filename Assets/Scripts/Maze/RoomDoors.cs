// ============================================================================
// RoomDoors.cs
// Puts a door in every room entrance when the game starts (40 on the 2-layer map: each room
// has 5 to 7 openings) and saves which ones the player has unlocked. How a door behaves is
// in RoomDoor.cs: unlock it once with the valve minigame, it shuts at night but opens with E.
//
// Each door is made under its room's own object (the "Room_N" MazeArea the MazeBuilder made),
// so zone 2's doors turn with zone 2. Nothing is added to the maze build: rebuilding the maze
// never deletes or duplicates doors, and the CSV needs no new codes.
//
// Where a door goes: on the edge between a room tile and the corridor tile next to it (the
// line where the two wall blocks on either side end), facing out of the room.
//
// TESTING: untick "Doors Enabled" on the MazeBuilder to have every room door open (works
// in Play mode, no rebuild).
//
// DESIGN RULES: none yet (pending GDD entry: room doors, 2026-11-13). Difficulty by ring as
// R9.6 (the same table as the valve grates).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// SEE AND MOVE DOORS IN THE EDITOR: Salok > Rooms > Place room doors in scene puts every door
// under MazeBuilder > Room_Doors. Move or turn the RoomDoor_... objects (keep their names); at
// Play the game uses them. Doors that aren't placed are still made on their default spot.
// HOW TO EDIT: THE LOOK is the Maze Tile Set's "Room Door" prefab (modelled for a doorway
// along X, facing +Z, pivot at the bottom middle). Empty = an orange grey-box slab (the
// MazeBuilder's "Door Material" if set). Door height and thickness are below; the width is
// one tile.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Makes and saves the room doors.</summary>
    public class RoomDoors : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<RoomDoors>();

        [Tooltip("Door height in metres (the grey-box slab; capped at the wall height).")]
        public float doorHeight = 3f;
        [Tooltip("Door thickness in metres (grey-box slab).")]
        public float doorThickness = 0.25f;
        [Tooltip("Grey-box colour when the MazeBuilder has no Door Material.")]
        public Color greyBoxColor = new Color(0.88f, 0.54f, 0.24f);

        /// <summary>Every room door, in RoomDoorLayout order (index = door number).</summary>
        public IReadOnlyList<RoomDoor> Doors => doors;

        readonly List<RoomDoor> doors = new List<RoomDoor>();
        MazeBuilder maze;
        GameBalance balance;
        bool built;
        bool lastDoorsEnabled = true;

        // ------------------------------------------------------------------ Save

        /// <summary>Restored right after the gates and valves.</summary>
        public int RestoreOrder => RestoreOrders.Gates + 1;

        /// <summary>Saves which doors are unlocked.</summary>
        public void Capture(SaveData data)
        {
            data.doorsOpened.Clear();
            for (int i = 0; i < doors.Count; i++)
                if (doors[i] != null && doors[i].IsUnlocked) data.doorsOpened.Add(i);
        }

        /// <summary>Unlocks the saved doors; every other door goes back to shut.</summary>
        public void Restore(SaveData data)
        {
            if (!built) TryBuild();
            for (int i = 0; i < doors.Count; i++)
                if (doors[i] != null) doors[i].SetUnlockedState(data.doorsOpened != null && data.doorsOpened.Contains(i));
        }

        // ------------------------------------------------------------------ Setup

        void Start() => TryBuild();

        void Update()
        {
            if (!built) { TryBuild(); return; }
            // MazeBuilder "Doors Enabled" (testing): unticked = every room door open.
            if (maze != null && maze.doorsEnabled != lastDoorsEnabled)
            {
                lastDoorsEnabled = maze.doorsEnabled;
                foreach (var d in doors) if (d != null) d.SetTestOpen(!lastDoorsEnabled);
            }
        }

        void TryBuild()
        {
            if (built) return;
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (balance == null) balance = BalanceProvider.Current;
            if (maze == null || maze.Grid == null || balance == null) return;
            built = true;

            var spots = RoomDoorLayout.Find(maze.Grid);
            foreach (var spot in spots) doors.Add(MakeDoor(spot));

            lastDoorsEnabled = maze.doorsEnabled;
            if (!lastDoorsEnabled) foreach (var d in doors) if (d != null) d.SetTestOpen(true);
            Debug.Log($"[RoomDoors] {doors.Count} room doors in {maze.Grid.Rooms.Count} rooms.", this);
        }

        /// <summary>Name of the folder (under the MazeBuilder) that holds doors placed in the editor.</summary>
        public const string PlacedRootName = "Room_Doors";

        /// <summary>
        /// Where a door goes by default, in the MazeBuilder's own space (0 degree layout, north = +Z):
        /// on the edge between the room tile and the corridor tile, facing out of the room.
        /// </summary>
        public static void DefaultPose(MazeBuilder maze, RoomDoorSpot spot, out Vector3 localPos, out Quaternion localRot)
        {
            Vector3 inside = maze.GridToLocal(spot.Inside.x, spot.Inside.y);
            Vector3 outside = maze.GridToLocal(spot.Outside.x, spot.Outside.y);
            localPos = (inside + outside) * 0.5f;
            localRot = Quaternion.LookRotation((outside - inside).normalized);
        }

        /// <summary>The object's name for a door (the same in the editor and at Play).</summary>
        public static string DoorName(RoomDoorSpot spot) =>
            $"RoomDoor_{spot.Index}_{spot.Inside.x}_{spot.Inside.y}_{DirName(spot.Out)}";

        RoomDoor MakeDoor(RoomDoorSpot spot)
        {
            var grid = maze.Grid;
            RectInt rect = grid.Rooms[spot.Room].Tiles;
            float tile = maze.TileSize;
            float height = Mathf.Min(doorHeight, maze.WallHeight);
            Vector3 roomMiddle = (maze.GridToLocal(rect.xMin, rect.yMin) + maze.GridToLocal(rect.xMax - 1, rect.yMax - 1)) * 0.5f;

            // A door placed in the editor (Salok > Rooms > Place room doors in scene) keeps the spot
            // and turn it was given; otherwise it goes on the default spot.
            var placedRoot = maze.transform.Find(PlacedRootName);
            var placed = placedRoot != null ? placedRoot.Find(DoorName(spot)) : null;
            Vector3 pos;
            Quaternion rot;
            if (placed != null) { pos = placed.localPosition; rot = placed.localRotation; }
            else DefaultPose(maze, spot, out pos, out rot);

            var go = placed != null ? placed.gameObject : new GameObject(DoorName(spot));
            var area = FindArea(rect);
            if (area != null)
            {
                // The room object sits on the room's middle at floor level, turned like the maze
                // at 0 degrees, so this offset stays right after zone 2 turns.
                go.transform.SetParent(area.transform, false);
                go.transform.localPosition = pos - roomMiddle;
                go.transform.localRotation = rot;
            }
            else
            {
                Debug.LogWarning($"[RoomDoors] No room object for room {spot.Room}; door {spot.Index} won't turn with zone 2.", this);
                go.transform.SetParent(maze.transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
            }

            var door = go.GetComponent<RoomDoor>();
            if (door == null) door = go.AddComponent<RoomDoor>();
            door.index = spot.Index;
            door.height = height;
            door.width = tile;
            door.settings = ValveDifficulty.ForRing(spot.Ring, balance);
            door.difficultyT = ValveDifficulty.T(spot.Ring, balance);
            door.leaf = placed != null ? PlacedLeaf(go.transform, tile, height) : null;
            if (door.leaf == null) door.leaf = MakeLeaf(go.transform, tile, height);
            door.Init();
            return door;
        }

        // The moving part of a placed door: its art's "Leaf" child, the art itself, or a "Leaf" slab.
        Transform PlacedLeaf(Transform door, float width, float height)
        {
            var art = door.Find("DoorArt");
            Transform moving = art != null ? (art.Find("Leaf") ?? art) : door.Find("Leaf");
            if (moving == null) return null;
            EnsureCollider(moving.gameObject, width, height);
            return moving;
        }

        void EnsureCollider(GameObject moving, float width, float height)
        {
            foreach (var c in moving.GetComponentsInChildren<Collider>(true))
                if (c.enabled && !c.isTrigger) return;
            var box = moving.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(width, height, doorThickness);
        }

        // The Maze Tile Set's Room Door prefab, or an orange slab.
        Transform MakeLeaf(Transform parent, float width, float height)
        {
            var prefab = maze.tileSet != null ? maze.tileSet.roomDoor : null;
            if (prefab != null)
            {
                var art = Instantiate(prefab, parent, false);
                // A prefab with a child named "Leaf" keeps its frame still and moves only the leaf.
                var leafPart = art.transform.Find("Leaf");
                art.name = "DoorArt";
                var moving = leafPart != null ? leafPart.gameObject : art;
                EnsureCollider(moving, width, height);
                return moving.transform;
            }

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Leaf";
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            slab.transform.localScale = new Vector3(width, height, doorThickness);
            var r = slab.GetComponent<Renderer>();
            if (maze.doorMaterial != null) r.sharedMaterial = maze.doorMaterial;
            else r.material.color = greyBoxColor;
            return slab.transform;
        }

        MazeArea FindArea(RectInt rect)
        {
            foreach (var a in maze.Areas)
                if (a != null && a.kind == MazeArea.Kind.Room && a.tiles.Equals(rect)) return a;
            return null;
        }

        static string DirName(Vector2Int d) => d.y < 0 ? "N" : d.x > 0 ? "E" : d.y > 0 ? "S" : "W";
    }
}
