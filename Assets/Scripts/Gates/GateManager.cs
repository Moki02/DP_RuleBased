// ============================================================================
// GateManager.cs
// Sets up every gate in the maze when the game starts (and, since 2026-11-12, wins the game
// when the player walks out through an opened exit gate: see Update):
//   - each G gate (16 of them) gets a lock basin on its hub side (R8.9):
//       hub gates 4 L, zone 1 exits 10 L, zone 2 exits 16 L,
//     and a Valve Timing minigame whose difficulty grows with distance from the hub (R9.6);
//     zone 2's 8 exits use the exit valve settings; opening one and walking out wins (R11.6);
//     in Demo mode (section 13) zone 1's exits become the exit instead: 8 L (Lock 1 of
//     GameBalance_Demo) and the exit valve; walking out of one shows "Demo complete" (R13.4);
//   - each V tile (8 of them) gets a ValveGate on its grate (R8.8).
// It also tells the map which gates are open, opens the win screen (WinScreen), and, when
// night falls, moves a player standing in a gate's doorway back to the hub side so the
// closing gate can't trap them.
//
// DESIGN RULES: R8.8, R8.9, R8.21 / R8.23, R9.6, R11.6, section 13 / R13.4 (Demo exit).
//
// HOW TO USE: put this on the "Systems" object and fill the links (the Build Playtest
// Scene menu does it). Salok > Build > 3. Place gate basins in scene puts all 16 basins
// under Systems/LockBasins so they can be moved by hand; any gate without a placed basin
// (found by name, e.g. LockBasin_HubGate_40_47) gets one made at Play.
// HOW TO EDIT: litres, pour rate, reach and minigame numbers are in GameBalance. Where the
// basin sits inside its tile is under "Basin placement" below.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Creates and tracks the lock basins and valve gates of the maze.</summary>
    // Start before the basins' own Start: basins placed in the scene must get their litres
    // and gate pieces from here before LockBasin_Manager.Start builds the lock from them.
    [DefaultExecutionOrder(-10)]
    public class GateManager : MonoBehaviour, ISaveable
    {
        /// <summary>Restored after Build and Skills.</summary>
        public int RestoreOrder => RestoreOrders.Gates;

        /// <summary>Saves each gate's litres and open flag, and which valves were won.</summary>
        public void Capture(SaveData data)
        {
            data.gates.Clear();
            for (int i = 0; i < basins.Count; i++)
                if (basins[i] != null && basins[i].Lock != null) SaveCodec.CaptureGate(i, basins[i].Lock, data);
            data.valvesWon.Clear();
            for (int i = 0; i < valves.Count; i++)
                if (valves[i] != null && valves[i].IsOpen) data.valvesWon.Add(i);
        }

        /// <summary>Puts every gate and valve back the way the save says (missing entries = untouched gate).</summary>
        public void Restore(SaveData data)
        {
            for (int i = 0; i < basins.Count; i++)
            {
                if (basins[i] == null) continue;
                var saved = SaveCodec.FindGate(data, i);
                bool open = saved != null && saved.open;
                basins[i].RestoreState(saved != null ? saved.litres : 0f, open);
                // An exit gate opened before the save: walking out through it still wins.
                if (open && basins[i].isExitGate && exitOuterRing.TryGetValue(basins[i], out int ring))
                    openedExitRing = openedExitRing < 0 ? ring : Mathf.Min(openedExitRing, ring);
            }
            for (int i = 0; i < valves.Count; i++)
                if (valves[i] != null) valves[i].SetOpenState(data.valvesWon != null && data.valvesWon.Contains(i));
        }

        [Header("Links")]
        [Tooltip("The Maze object (with the MazeBuilder).")]
        public MazeBuilder maze;
        [Tooltip("The Lock Basin prefab (with LockBasin_Manager and its bar). Empty = a plain grey cylinder.")]
        public GameObject lockBasinPrefab;
        [Tooltip("The map screen, so it can draw open gates light blue. Empty = found by itself.")]
        public MapScreen mapScreen;
        [Tooltip("Old \"You escaped\" panel. NOT USED any more: a win now opens WinScreen (title, stats, Restart). " +
                 "Kept only so the Build Playtest Scene menu, which still fills this slot, compiles.")]
        public EscapePanel escapePanel;

        [Header("Basin placement (metres)")]
        [Tooltip("How far the basin sits from the middle of its tile, towards the gate.")]
        public float basinTowardGate = 1.2f;
        [Tooltip("How far the basin sits to the side of the corridor, so it doesn't block the way.")]
        public float basinToSide = 2.59f;

        /// <summary>One basin per gate, in the same order as MazeGrid.Gates (index = gate index).</summary>
        public IReadOnlyList<LockBasin_Manager> Basins => basins;

        /// <summary>One ValveGate per valve, in the same order as MazeGrid.Valves (index = valve index).</summary>
        public IReadOnlyList<ValveGate> Valves => valves;

        readonly List<LockBasin_Manager> basins = new List<LockBasin_Manager>();
        readonly List<ValveGate> valves = new List<ValveGate>();
        readonly Dictionary<Vector2Int, LockBasin_Manager> basinByTile = new Dictionary<Vector2Int, LockBasin_Manager>();
        // Exit gates: the ring of the gate's outermost tile. Walking out to it after the gate opens wins.
        readonly Dictionary<LockBasin_Manager, int> exitOuterRing = new Dictionary<LockBasin_Manager, int>();
        int openedExitRing = -1;   // -1 = no exit gate opened yet
        bool escaped;
        GameBalance balance;
        GameClock_Manager clock;

        void Start()
        {
            balance = BalanceProvider.Current;
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (mapScreen == null) mapScreen = FindAnyObjectByType<MapScreen>();
            if (escapePanel == null) escapePanel = FindAnyObjectByType<EscapePanel>();
            if (maze == null || maze.Grid == null || balance == null)
            {
                Debug.LogError("[GateManager] Needs a built maze and a BalanceProvider with GameBalance.", this);
                enabled = false;
                return;
            }

            var pieces = IndexMazePieces();
            // Basins placed in the scene (Salok > Build > 3. Place gate basins in scene) are used
            // as they are; any gate without one gets a basin made now.
            var parent = transform.Find(BasinsParentName);
            if (parent == null)
            {
                parent = new GameObject(BasinsParentName).transform;
                parent.SetParent(transform, false);
            }
            foreach (var gate in maze.Grid.Gates)
            {
                var placed = parent.Find(BasinName(gate));
                var go = placed != null ? placed.gameObject : SpawnBasin(gate, parent);
                basins.Add(ConfigureBasin(go, gate, pieces));
            }
            foreach (var valve in maze.Grid.Valves) valves.Add(MakeValve(valve, pieces));

            if (mapScreen != null)
                mapScreen.isGateTileOpen = tile => basinByTile.TryGetValue(tile, out var b) && b.IsPassable;

            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NightStarted += OnNightStarted;

            Debug.Log($"[GateManager] {basins.Count} lock basins and {valves.Count} valve gates ready.", this);
        }

        void OnDestroy()
        {
            if (clock != null) clock.NightStarted -= OnNightStarted;
        }

        // Every object the MazeBuilder made, by name (for example "Gate_40_47", "Valve_60_15").
        Dictionary<string, GameObject> IndexMazePieces()
        {
            var byName = new Dictionary<string, GameObject>();
            foreach (var t in maze.GetComponentsInChildren<Transform>(true))
                byName[t.name] = t.gameObject;
            return byName;
        }

        // ------------------------------------------------------------------ Lock basins

        /// <summary>Name of the child that holds the basins.</summary>
        public const string BasinsParentName = "LockBasins";

        /// <summary>The scene name of a gate's basin, e.g. "LockBasin_HubGate_40_47" (how placed basins are found).</summary>
        public static string BasinName(GateInfo gate) => $"LockBasin_{gate.Kind}_{gate.InnerTile.x}_{gate.InnerTile.y}";

        /// <summary>
        /// Makes every gate's basin under this object, so they are saved in the scene (edit mode:
        /// prefab instances). Replaces any basins placed before. Used by the editor menu.
        /// </summary>
        public void PlaceBasinsInScene()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (maze == null) { Debug.LogError("[GateManager] No MazeBuilder in the scene.", this); return; }
            if (maze.Grid == null) maze.LoadGrid();
            if (maze.Grid == null) { Debug.LogError("[GateManager] The maze has no layout.", this); return; }

            var old = transform.Find(BasinsParentName);
            if (old != null) DestroyImmediate(old.gameObject);
            var parent = new GameObject(BasinsParentName).transform;
            parent.SetParent(transform, false);
            foreach (var gate in maze.Grid.Gates) SpawnBasin(gate, parent);
        }

        // A new basin on the hub side of the gate, standing on the floor.
        GameObject SpawnBasin(GateInfo gate, Transform parent)
        {
            Vector2Int step = StepTowardCentre(gate.InnerTile);
            Vector2Int hubSide = gate.InnerTile + step;   // the tile just inside the gate, on the hub side

            // World directions: towards the gate, and across the corridor (to the side).
            // Tile y grows to the south, which is -Z in the world, hence the minus.
            Vector3 towardGate = new Vector3(-step.x, 0f, step.y);
            Vector3 side = new Vector3(towardGate.z, 0f, -towardGate.x);
            Vector3 pos = BasinPosition(maze.GridToWorld(hubSide.x, hubSide.y) + towardGate * basinTowardGate, side);
            Quaternion rot = Quaternion.LookRotation(-towardGate); // facing back towards the hub

            GameObject go;
            if (lockBasinPrefab == null) go = MakePlaceholderBasin(pos, rot, parent);
            else
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(lockBasinPrefab, parent);
                    go.transform.SetPositionAndRotation(pos, rot);
                }
                else
#endif
                    go = Instantiate(lockBasinPrefab, pos, rot, parent);
                SitOnFloor(go, pos.y);
            }
            go.name = BasinName(gate);
            return go;
        }

        // Basin To Side metres to the side, beside the gate's pillar. Some gates have a wall on
        // that side, so the basin is pulled back towards the corridor until it stands on floor
        // (with a little room for the model), never inside the wall.
        Vector3 BasinPosition(Vector3 start, Vector3 side)
        {
            const float room = 0.35f;   // about half the basin's width
            for (float d = basinToSide; d > 0f; d -= 0.05f)
            {
                Vector3 p = start + side * d;
                Vector2Int t = maze.WorldToGrid(p + side * room);
                if (maze.Grid.IsWalkable(t.x, t.y)) return p;
            }
            return start;
        }

        /// <summary>
        /// Moves every basin already placed in the scene to the spot the current Basin Toward Gate /
        /// Basin To Side give (height kept). Used by the editor after changing those numbers.
        /// </summary>
        public void RepositionPlacedBasins()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (maze.Grid == null) maze.LoadGrid();
            var parent = transform.Find(BasinsParentName);
            if (parent == null) return;
            foreach (var gate in maze.Grid.Gates)
            {
                var b = parent.Find(BasinName(gate));
                if (b == null) continue;
                Vector2Int step = StepTowardCentre(gate.InnerTile);
                Vector2Int hubSide = gate.InnerTile + step;
                Vector3 towardGate = new Vector3(-step.x, 0f, step.y);
                Vector3 side = new Vector3(towardGate.z, 0f, -towardGate.x);
                Vector3 pos = BasinPosition(maze.GridToWorld(hubSide.x, hubSide.y) + towardGate * basinTowardGate, side);
                pos.y = b.position.y;
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(b, "Reposition gate basins");
#endif
                b.SetPositionAndRotation(pos, Quaternion.LookRotation(-towardGate));
            }
        }

        // The Lock Basin prefab's model sits half a metre below its pivot, so it sank into the
        // floor. Lift the whole basin so the bottom of its model is on the floor.
        static void SitOnFloor(GameObject go, float floorY)
        {
            bool any = false;
            float bottom = 0f;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue; // world text, not the model
                float y = r.bounds.min.y;
                bottom = any ? Mathf.Min(bottom, y) : y;
                any = true;
            }
            if (any) go.transform.position += Vector3.up * (floorY - bottom);
        }

        // Sets a basin up for its gate (litres, minigame, gate pieces). Works for placed and new basins.
        LockBasin_Manager ConfigureBasin(GameObject go, GateInfo gate, Dictionary<string, GameObject> pieces)
        {
            var basin = go.GetComponentInChildren<LockBasin_Manager>();
            if (basin == null) basin = go.AddComponent<LockBasin_Manager>();

            // Demo mode (section 13): Lock 1 becomes the exit, so zone 1's exits are exit gates
            // with GetLockLiters(0) (8 L) and the exit valve; winning one wins the demo (R13.4).
            bool demoExit = balance.isDemoMode && gate.Kind == GateKind.Zone1Exit;
            bool exitGate = gate.Kind == GateKind.Zone2Exit || demoExit;

            basin.basin_MaxWater = demoExit ? balance.GetLockLiters(0) : balance.GetLockLiters(LockIndex(gate.Kind));
            basin.basin_Water = 0f;
            basin.pourRate = balance.pourRate;
            basin.openWithValveMinigame = true;
            basin.minigameSettings = ValveDifficulty.ForGate(exitGate ? GateKind.Zone2Exit : gate.Kind, gate.Ring, balance);
            basin.difficultyT = exitGate ? -1f : ValveDifficulty.T(gate.Ring, balance);
            basin.lockoutSeconds = balance.minigameLockout;
            basin.failHydLog = balance.minigameFailHyd;
            basin.isExitGate = exitGate;
            basin.proximityRange = balance.interactRange;
            basin.gateObjects.Clear();
            foreach (var tile in gate.Tiles)
            {
                if (pieces.TryGetValue($"Gate_{tile.x}_{tile.y}", out var piece)) basin.gateObjects.Add(piece);
                basinByTile[tile] = basin;
            }
            basin.Opened += OnBasinOpened;
            if (exitGate)
            {
                int outer = maze.Grid.Ring(gate.InnerTile);
                foreach (var tile in gate.Tiles) outer = Mathf.Max(outer, maze.Grid.Ring(tile));
                exitOuterRing[basin] = outer;
            }
            return basin;
        }

        static int LockIndex(GateKind kind)
        {
            switch (kind)
            {
                case GateKind.HubGate: return 0;
                case GateKind.Zone2Exit: return 2;
                default: return 1; // zone 1 exits (and any other gate)
            }
        }

        static GameObject MakePlaceholderBasin(Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.5f, rot);
            go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            go.transform.SetParent(parent, true);
            return go;
        }

        // One tile step from a gate's inner tile towards the maze centre (north, east, south or west).
        Vector2Int StepTowardCentre(Vector2Int tile)
        {
            Vector2Int d = maze.Grid.Centre - tile;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? new Vector2Int(d.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, d.y > 0 ? 1 : -1);
        }

        // A lock's valve was won. For an exit gate the player still has to walk out through it:
        // Update wins once they reach the gate's outermost tile (they've left zone 2, or zone 1
        // in Demo mode).
        void OnBasinOpened(LockBasin_Manager basin)
        {
            if (!basin.isExitGate) return;
            int ring = exitOuterRing.TryGetValue(basin, out int r) ? r : 0;
            openedExitRing = openedExitRing < 0 ? ring : Mathf.Min(openedExitRing, ring);
            SimpleHud.Banner("THE EXIT IS OPEN", "Walk out through the gate to escape.", 5f);
        }

        void Update()
        {
            if (escaped || openedExitRing < 0 || maze == null || maze.Grid == null) return;
            var player = Player_Manager.instance;
            if (player == null) return;

            // The player's ring as a decimal (tile centres are whole numbers).
            Vector2 t = maze.WorldToGridFloat(player.transform.position);
            var grid = maze.Grid;
            float ring = Mathf.Max(Mathf.Abs(t.x - grid.CentreX), Mathf.Abs(t.y - grid.CentreY));
            if (ring < openedExitRing - 0.3f) return;

            escaped = true;
            int day = clock != null ? clock.Day : 1;
            // The Win scene with the ending title and stats (R11.6, R11.7).
            WinScreen.Show(day);
        }

        // ------------------------------------------------------------------ Valve gates

        ValveGate MakeValve(ValveInfo valve, Dictionary<string, GameObject> pieces)
        {
            // Grates are named by their CSV tile, so zone 2 grates are found even after a turn.
            if (!pieces.TryGetValue($"Valve_{valve.Tile.x}_{valve.Tile.y}", out var grate))
            {
                Debug.LogWarning($"[GateManager] No grate object for the valve at {valve.Tile}.", this);
                return null;
            }
            var gate = grate.GetComponent<ValveGate>();
            if (gate == null) gate = grate.AddComponent<ValveGate>();
            gate.balance = balance;
            gate.useCustomSettings = true;
            gate.customSettings = ValveDifficulty.ForRing(valve.Ring, balance);
            gate.difficultyT = ValveDifficulty.T(valve.Ring, balance);
            gate.rewardZoneIndex = valve.Part == MazePart.Zone2 ? 1 : 0;
            return gate;
        }

        // ------------------------------------------------------------------ Night

        // A gate shuts at night. If the player is standing in its doorway, move them out first.
        void OnNightStarted(int day)
        {
            var player = Player_Manager.instance;
            if (player == null) return;
            var grid = maze.Grid;
            Vector2Int tile = maze.WorldToGrid(player.transform.position);
            Vector2Int? safe = null;

            foreach (var gate in grid.Gates)
                if (gate.Tiles.Contains(tile)) { safe = gate.InnerTile + StepTowardCentre(gate.InnerTile); break; }

            if (safe == null && grid.CodeAt(tile) == TileType.Valve)
            {
                // Step to the walkable neighbour nearest the hub.
                foreach (var s in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    var n = tile + s;
                    if (!grid.InBounds(n.x, n.y) || grid.CodeAt(n) == TileType.Wall || grid.CodeAt(n) == TileType.Valve) continue;
                    if (safe == null || grid.Ring(n) < grid.Ring(safe.Value)) safe = n;
                }
            }
            if (safe == null) return;

            var cc = player.characterController;
            bool on = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            Vector3 p = maze.GridToWorld(safe.Value.x, safe.Value.y);
            player.transform.position = new Vector3(p.x, player.transform.position.y, p.z);
            if (cc != null) cc.enabled = on;
            Debug.Log($"[GateManager] Night: the player was standing in a gate and was moved to tile {safe.Value}.", this);
        }
    }
}
