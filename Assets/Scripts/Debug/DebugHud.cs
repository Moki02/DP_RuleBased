// ============================================================================
// DebugHud.cs
// The F1 debug HUD for testers: a small panel in the top-left corner that shows
//   - day, clock, day or night (and time left), clock speed;
//   - zone 2's turn (0 / 90 / 180 / 270°);
//   - the player's tile (x, y), ring, maze part and the tile code under them;
//   - Clean Storage;
//   - the nearest gate or valve: its state, litres and difficulty t.
// F1 also shows the minigame's debug line (t, band, speed) while a minigame is open.
//
// DESIGN RULES: none (testing only). F1 only works while GameBalance "Enable Debug Keys" is on.
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// It finds everything by itself. Press F1 in Play mode.
// HOW TO EDIT: colours and how often it updates are in the Inspector.
// ============================================================================

using System.Text;
using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Shows test information on screen while F1 debug info is on.</summary>
    public class DebugHud : MonoBehaviour
    {
        [Tooltip("Panel background.")] public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        [Tooltip("Text colour.")] public Color textColor = new Color32(255, 230, 120, 255);
        [Tooltip("Updates per second.")] [Min(1f)] public float updatesPerSecond = 10f;

        GameObject root;
        TextMeshProUGUI text;
        float timer;
        MazeBuilder maze;
        Zone2Rotator rotator;
        GateManager gates;
        GameBalance balance;
        readonly StringBuilder sb = new StringBuilder();

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas("DebugHudCanvas", transform, 95);
            root = canvas.gameObject;
            var panel = UiBuilder.Image("Panel", root.transform, backgroundColor).rectTransform;
            UiBuilder.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(560f, 300f));
            text = UiBuilder.Text("Text", panel, "", 18f, textColor);
            UiBuilder.Fill(text.rectTransform, 12f, 12f, 10f, 10f);
            text.richText = true;
            root.SetActive(false);
        }

        void Start()
        {
            balance = BalanceProvider.Current;
            maze = FindAnyObjectByType<MazeBuilder>();
            rotator = FindAnyObjectByType<Zone2Rotator>();
            gates = FindAnyObjectByType<GateManager>();
        }

        void Update()
        {
            bool show = DebugKeys.ShowDebugInfo;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 1f / updatesPerSecond;
            text.text = BuildText();
        }

        string BuildText()
        {
            sb.Clear();
            sb.AppendLine("<b>DEBUG  (F1)</b>");

            var clock = GameClock_Manager.Instance;
            if (clock != null && clock.Clock != null)
            {
                var c = clock.Clock;
                sb.Append($"Day {c.Day}   {Clock(c.TimeOfDay)} / {Clock(c.DayLength)}   ");
                sb.Append(c.IsNight ? $"NIGHT ({Clock(c.SecondsLeftInPhase)} to dawn)" : $"day ({Clock(c.SecondsLeftInPhase)} to night)");
                if (c.SpeedMultiplier > 1f) sb.Append($"   x{c.SpeedMultiplier:0.#}");
                if (c.Paused) sb.Append("   PAUSED");
                sb.AppendLine();
            }

            if (rotator != null) sb.AppendLine($"Zone 2 turn: {rotator.Turns * 90}°  ({rotator.Turns} turns)");

            var player = Player_Manager.instance;
            if (player != null && maze != null && maze.Grid != null)
            {
                var grid = maze.Grid;
                Vector2Int tile = maze.WorldToGrid(player.transform.position);
                if (grid.InBounds(tile.x, tile.y))
                {
                    Vector2Int local = grid.WorldToLocal(tile);
                    sb.Append($"Tile ({tile.x}, {tile.y})   ring {grid.Ring(tile)}   {grid.PartOf(tile)}   code {grid.CodeAt(tile)}");
                    if (local != tile) sb.Append($"   [CSV tile ({local.x}, {local.y})]");
                    sb.AppendLine();
                }
                else sb.AppendLine($"Tile ({tile.x}, {tile.y}): outside the maze");
            }

            var upgrades = UpgradeManager.Instance;
            if (upgrades != null && upgrades.Logic != null)
                sb.AppendLine($"Clean Storage: {upgrades.Logic.Stored:0.0} / {upgrades.Logic.Cap:0} L   " +
                              $"at hub: {(upgrades.PlayerAtHub ? "yes" : "no")}   owned: {upgrades.Logic.Owned.Count}");

            if (player != null) AppendNearestGate(player.transform.position);
            return sb.ToString();
        }

        // The nearest lock basin or valve gate, with its state and difficulty.
        void AppendNearestGate(Vector3 playerPos)
        {
            if (gates == null) return;
            float best = float.MaxValue;
            string line = null;

            foreach (var b in gates.Basins)
            {
                if (b == null || b.Lock == null) continue;
                float d = Vector3.Distance(playerPos, b.transform.position);
                if (d >= best) continue;
                best = d;
                string t = b.difficultyT < 0f ? "exit valve" : $"t {b.difficultyT:0.000}";
                float left = b.Lock.LockoutLeft(Time.time);
                line = $"Nearest gate: {b.name}  {d:0.0} m\n  {b.Lock.State}{(b.Lock.NightLocked ? " (night: shut)" : "")}   " +
                       $"{b.Lock.Litres:0.0} / {b.Lock.Capacity:0.#} L   {t}" +
                       (b.minigameSettings != null ? $"   band {b.minigameSettings.bandWidth:0.0}°  speed {b.minigameSettings.needleSpeed:0.0}°/s" : "") +
                       (left > 0f ? $"   locked {left:0}s" : "");
            }

            foreach (var v in gates.Valves)
            {
                if (v == null) continue;
                float d = Vector3.Distance(playerPos, v.transform.position);
                if (d >= best) continue;
                best = d;
                string state = v.IsOpen ? "Open" : (v.IsJammed ? "Jammed" : "Shut");
                line = $"Nearest gate: {v.name}  {d:0.0} m\n  valve {state}   t {v.difficultyT:0.000}" +
                       (v.customSettings != null ? $"   band {v.customSettings.bandWidth:0.0}°  speed {v.customSettings.needleSpeed:0.0}°/s" : "");
            }

            if (line != null) sb.AppendLine(line);
        }

        static string Clock(float seconds) => $"{Mathf.FloorToInt(seconds / 60f):00}:{Mathf.FloorToInt(seconds % 60f):00}";
    }
}
