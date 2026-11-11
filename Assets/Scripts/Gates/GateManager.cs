// ============================================================================
// GateManager.cs
// Sets up every gate in the maze when the game starts:
//   - each G gate (16 of them) gets a lock basin on its hub side (R8.9):
//       hub gates 4 L, zone 1 exits 10 L, zone 2 exits 18 L,
//     and a Valve Timing minigame whose difficulty grows with distance from the hub (R9.6);
//     zone 2's 8 exits use the exit valve settings, and winning one wins the game (R11.6);
//     in Demo mode (section 13) zone 1's exits become the exit instead: 8 L (Lock 1 of
//     GameBalance_Demo) and the exit valve, and winning one shows "Demo complete" (R13.4);
//   - each V tile (8 of them) gets a ValveGate on its grate (R8.8).
// It also tells the map which gates are open, opens the win screen (WinScreen), and, when
// night falls, moves a player standing in a gate's doorway back to the hub side so the
// closing gate can't trap them.
//
// DESIGN RULES: R8.8, R8.9, R8.21 / R8.23, R9.6, R11.6, section 13 / R13.4 (Demo exit).
//
// HOW TO USE: put this on the "Systems" object and fill the links (the Build Playtest
// Scene menu does it). Gates are made at Play; nothing to place by hand.
// HOW TO EDIT: litres, pour rate, reach and minigame numbers are in GameBalance. Where the
// basin sits inside its tile is under "Basin placement" below.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Creates and tracks the lock basins and valve gates of the maze.</summary>
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
                basins[i].RestoreState(saved != null ? saved.litres : 0f, saved != null && saved.open);
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
        public float basinToSide = 1.3f;

        /// <summary>One basin per gate, in the same order as MazeGrid.Gates (index = gate index).</summary>
        public IReadOnlyList<LockBasin_Manager> Basins => basins;

        /// <summary>One ValveGate per valve, in the same order as MazeGrid.Valves (index = valve index).</summary>
        public IReadOnlyList<ValveGate> Valves => valves;

        readonly List<LockBasin_Manager> basins = new List<LockBasin_Manager>();
        readonly List<ValveGate> valves = new List<ValveGate>();
        readonly Dictionary<Vector2Int, LockBasin_Manager> basinByTile = new Dictionary<Vector2Int, LockBasin_Manager>();
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
            var parent = new GameObject("LockBasins").transform;
            parent.SetParent(transform, false);
            foreach (var gate in maze.Grid.Gates) basins.Add(MakeBasin(gate, pieces, parent));
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

        LockBasin_Manager MakeBasin(GateInfo gate, Dictionary<string, GameObject> pieces, Transform parent)
        {
            Vector2Int step = StepTowardCentre(gate.InnerTile);
            Vector2Int hubSide = gate.InnerTile + step;   // the tile just inside the gate, on the hub side

            // World directions: towards the gate, and across the corridor (to the side).
            // Tile y grows to the south, which is -Z in the world, hence the minus.
            Vector3 towardGate = new Vector3(-step.x, 0f, step.y);
            Vector3 side = new Vector3(towardGate.z, 0f, -towardGate.x);
            Vector3 pos = maze.GridToWorld(hubSide.x, hubSide.y) + towardGate * basinTowardGate + side * basinToSide;
            Quaternion rot = Quaternion.LookRotation(-towardGate); // facing back towards the hub

            GameObject go = lockBasinPrefab != null
                ? Instantiate(lockBasinPrefab, pos, rot, parent)
                : MakePlaceholderBasin(pos, rot, parent);
            go.name = $"LockBasin_{gate.Kind}_{gate.InnerTile.x}_{gate.InnerTile.y}";

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

        // A lock's valve was won. LockBasin_Manager has already put the game back to Exploring;
        // WinScreen then freezes it behind the end screen (MainMenu.Freeze).
        void OnBasinOpened(LockBasin_Manager basin)
        {
            if (!basin.isExitGate) return;
            int day = clock != null ? clock.Day : 1;
            // The win screen with the ending title and stats (R11.6, R11.7). EscapePanel is no longer used.
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
