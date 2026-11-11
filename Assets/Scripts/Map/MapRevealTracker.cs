// ============================================================================
// MapRevealTracker.cs
// Watches the player and fills in the map as they explore: each time they step onto a
// new tile, MapReveal reveals the tile, its neighbours and a few tiles ahead (in the
// camera's direction). Also handles the marker key F.
//
// DESIGN RULES: R8.1a to R8.1d (reveal), R8.4 (markers, key F), R8.28 (knowledge
// turns with zone 2; MapReveal stores tiles in CSV positions to do this).
//
// HOW TO USE: put this on the "Systems" object and drag the Maze object into "Maze".
// The map screen reads "Reveal" from here. Every tile the player sees for the first time
// fires MapReveal.TileSeen, which pays the 0.03 L tile reward (SurvivalDirector, R8.2a) and
// the section / milestone rewards (MapRewards, R8.2, R8.3). Debug key 0 (RevealAll) and
// loading a save don't fire it, so they never pay.
// HOW TO EDIT: look-ahead and marker limits are in GameBalance ("Map reveal" and
// "Maze and mapping"). Skills V1, V2 and V3 replace them through UpgradeManager.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Reveals the map around the player as they walk, and places markers with F.</summary>
    public class MapRevealTracker : MonoBehaviour, ISaveable
    {
        /// <summary>Restored after zone 2.</summary>
        public int RestoreOrder => RestoreOrders.Map;

        /// <summary>Saves revealed tiles and markers (by CSV / 0 degree tile).</summary>
        public void Capture(SaveData data)
        {
            if (Reveal != null) SaveCodec.CaptureMap(Reveal, data);
        }

        /// <summary>Puts revealed tiles and markers back.</summary>
        public void Restore(SaveData data)
        {
            if (Reveal == null) return;
            if (!SaveCodec.RestoreMap(Reveal, data))
                Debug.LogWarning("[MapRevealTracker] The saved map doesn't fit this maze; the map starts black.", this);
            hasLastTile = false; // reveal around the player's loaded position next frame
        }

        [Tooltip("The Maze object (with the MazeBuilder).")]
        public MazeBuilder maze;

        /// <summary>The player's map knowledge. Null until Start.</summary>
        public MapReveal Reveal { get; private set; }

        /// <summary>The world tile the player stood on at the last check.</summary>
        public Vector2Int PlayerTile { get; private set; }

        /// <summary>Fired after a marker key press, with what happened. The map screen can show it.</summary>
        public event Action<MarkerResult> MarkerToggled;

        [Tooltip("Skills V1 Trail Sense, V2 Landmarks and V3 Keen Eyes come from here. Empty = found at start; none = base values only.")]
        public UpgradeManager upgrades;

        GameBalance balance;
        Vector2Int lastFacing;
        bool hasLastTile;

        void Start()
        {
            balance = BalanceProvider.Current;
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            // The tooltip promises "Empty = found at start", so look for it (V1, V2, V3 need it).
            if (upgrades == null) upgrades = FindAnyObjectByType<UpgradeManager>();
            if (maze == null || maze.Grid == null)
            {
                Debug.LogError("[MapRevealTracker] No built maze to map.", this);
                enabled = false;
                return;
            }
            Reveal = new MapReveal(maze.Grid);
        }

        // ------------------------------------------------------------------ Rules with skills

        // Each rule starts from GameBalance's base value; an owned skill's effect (from the
        // Build and Skills effects CSV) replaces it.

        /// <summary>R8.1b: tiles to look ahead (2, or 4 with V1 Trail Sense: effect "lookAheadTiles").</summary>
        public int LookAheadTiles
        {
            get
            {
                if (balance == null) return 0;
                float baseValue = balance.mapLookAheadTiles;
                return Mathf.RoundToInt(upgrades != null ? upgrades.GetFloat("lookAheadTiles", baseValue) : baseValue);
            }
        }

        /// <summary>R8.4: most markers allowed (3, or 8 with V2 Landmarks: effect "maxMarkers").</summary>
        public int MaxMarkers
        {
            get
            {
                if (balance == null) return 0;
                float baseValue = balance.markerLimit;
                return Mathf.RoundToInt(upgrades != null ? upgrades.GetFloat("maxMarkers", baseValue) : baseValue);
            }
        }

        /// <summary>R8.1a: true with V3 Keen Eyes (effect "revealDiagonals"): reveal all 8 neighbours.</summary>
        public bool RevealDiagonals => upgrades != null && upgrades.GetBool("revealDiagonals", false);

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            var player = Player_Manager.instance;
            if (Reveal == null || player == null) return;

            Vector2Int tile = maze.WorldToGrid(player.transform.position);
            Vector2Int facing = CameraFacing();

            bool newTile = !hasLastTile || tile != PlayerTile;
            bool turned = facing != lastFacing;
            if (newTile)
                Reveal.EnterTile(tile, facing, RevealDiagonals, LookAheadTiles);
            else if (turned && balance != null && balance.mapLookAheadWhenTurning)
                Reveal.LookAhead(tile, facing, LookAheadTiles);

            PlayerTile = tile;
            lastFacing = facing;
            hasLastTile = true;

            // F: drop or remove a marker (only while the player is in control, not in menus).
            var kb = Keyboard.current;
            bool canAct = Game_Manager.instance == null || Game_Manager.instance.CanControlPlayer();
            if (kb != null && canAct && kb.fKey.wasPressedThisFrame) ToggleMarker();
        }

        /// <summary>Places a marker on the player's tile, or removes the one there (R8.4).</summary>
        public MarkerResult ToggleMarker()
        {
            var result = Reveal.ToggleMarker(PlayerTile, MaxMarkers);
            Debug.Log($"[Map] Marker {result.ToString().ToLower()} at tile {PlayerTile} " +
                      $"({Reveal.MarkersLocal.Count}/{MaxMarkers} markers).", this);
            MarkerToggled?.Invoke(result);
            return result;
        }

        /// <summary>Debug key 0: reveals the whole map.</summary>
        public void RevealAll()
        {
            Reveal?.RevealAll();
            Debug.Log("[Map] Whole map revealed (debug key 0).", this);
        }

        // The camera's forward direction, snapped to north/east/south/west.
        static Vector2Int CameraFacing()
        {
            Transform cam = Player_Camera.instance != null && Player_Camera.instance.cameraObject != null
                ? Player_Camera.instance.cameraObject.transform
                : (Camera.main != null ? Camera.main.transform : null);
            return cam == null ? Vector2Int.zero : MapReveal.SnapToTileDirection(cam.forward);
        }
    }
}
