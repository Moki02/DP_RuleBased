// ============================================================================
// Zone2RotationTests.cs   (EditMode tests)
// Checks zone 2's turning:
//   - the turn count for days 1 to 10, day and night;
//   - after 1, 2 and 3 turns, every tile matches the designer's 90/180/270 CSV, and tiles
//     outside zone 2 never change;
//   - the valve at (60, 15) moves to (79, 60), (34, 79), (15, 34);
//   - turning a world point around the pivot lands on the right tile;
//   - with valves closed and gates open, all 8 zone 2 exits can be reached from the
//     spawn in every orientation, and the shortest walks are 71 / 71 / 79 / 75 tiles.
//
// DESIGN RULES: R8.24 to R8.28.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: if the maze CSVs change on purpose, update the expected walks below.
// "Shortest walk" = steps from (47, 47) to the outer (map edge) tile of the nearest exit.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for the zone 2 turn schedule, tile rotation and reachability.</summary>
    public class Zone2RotationTests
    {
        const string MazeFolder = "Assets/_Project/Data/Maze/";
        static readonly string[] TurnFiles = { "Maze2Layer_0deg.csv", "Maze2Layer_90deg.csv", "Maze2Layer_180deg.csv", "Maze2Layer_270deg.csv" };

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        // ------------------------------------------------------------------ Schedule

        // Columns: day, turns by day, turns that night.
        [TestCase(1, 0, 0)]
        [TestCase(2, 0, 1)]
        [TestCase(3, 1, 1)]
        [TestCase(4, 1, 2)]
        [TestCase(5, 2, 2)]
        [TestCase(6, 2, 3)]
        [TestCase(7, 3, 3)]
        [TestCase(8, 3, 0)]
        [TestCase(9, 0, 0)]
        [TestCase(10, 0, 1)]
        public void TurnsForDay(int day, int turnsByDay, int turnsAtNight)
        {
            var balance = ScriptableObject.CreateInstance<GameBalance>();
            int n = balance.zone2DaysBetweenTurns;
            Object.DestroyImmediate(balance);

            Assert.AreEqual(2, n, "GameBalance default: zone 2 turns every 2 days");
            Assert.AreEqual(turnsByDay, Zone2TurnMath.TurnsForDay(day, false, n), $"day {day}");
            Assert.AreEqual(turnsAtNight, Zone2TurnMath.TurnsForDay(day, true, n), $"night of day {day}");
        }

        // ------------------------------------------------------------------ Tiles

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CodeAt_MatchesTurnedCsv(int turns)
        {
            var grid = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[0]);
            var expected = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[turns]);
            grid.Zone2Turns = turns;

            int wrong = 0;
            string firstWrong = null;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.CodeAt(x, y) == expected.Get(x, y)) continue;
                wrong++;
                firstWrong ??= $"({x}, {y}): got {grid.CodeAt(x, y)}, file says {expected.Get(x, y)}";
            }
            Assert.AreEqual(0, wrong, $"{wrong} tiles differ; first: {firstWrong}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TilesOutsideZone2_NeverChange(int turns)
        {
            var grid = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[0]);
            grid.Zone2Turns = turns;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (!grid.IsInZone2(x, y))
                    Assert.AreEqual(grid.Get(x, y), grid.CodeAt(x, y), $"({x}, {y}) after {turns} turns");
        }

        [Test]
        public void Valve60_15_MovesAroundTheZone()
        {
            var grid = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[0]);
            Vector2Int[] expected = { V(60, 15), V(79, 60), V(34, 79), V(15, 34) };
            for (int k = 0; k < 4; k++)
            {
                grid.Zone2Turns = k;
                Vector2Int now = grid.LocalToWorld(V(60, 15));
                Assert.AreEqual(expected[k], now, $"after {k} turns");
                Assert.AreEqual(TileType.Valve, grid.CodeAt(now));
                Assert.AreEqual(V(60, 15), grid.WorldToLocal(now));
            }
        }

        [Test]
        public void RotateAroundPivot_LandsOnTheTurnedTile()
        {
            // Same layout as the playtest scene: tile (x, y) at (x × 4, 0, (94 − y) × 4).
            var balance = ScriptableObject.CreateInstance<GameBalance>();
            float ts = balance.cellSize;
            Object.DestroyImmediate(balance);
            Vector3 World(Vector2Int t) => new Vector3(t.x * ts, 0f, (94 - t.y) * ts);

            Vector3 pivot = World(V(47, 47));
            Assert.AreEqual(new Vector3(188f, 0f, 188f), pivot);

            Vector3 p = World(V(60, 15)) + Vector3.up * 1.5f; // height must not change
            Vector3 turned = Zone2TurnMath.RotateAroundPivot(p, pivot, 1);
            Assert.AreEqual(World(V(79, 60)) + Vector3.up * 1.5f, turned);
            Assert.AreEqual(p, Zone2TurnMath.RotateAroundPivot(p, pivot, 4), "4 turns = back to start");

            // A clockwise turn sends north (+Z) to east (+X), the same as +90 degrees on Y.
            Vector3 north = pivot + Vector3.forward * 10f;
            Assert.AreEqual(pivot + Vector3.right * 10f, Zone2TurnMath.RotateAroundPivot(north, pivot, 1));
        }

        // ------------------------------------------------------------------ Reachability

        [TestCase(0, 71)]
        [TestCase(1, 71)]
        [TestCase(2, 79)]
        [TestCase(3, 75)]
        public void AllZone2Exits_Reachable(int turns, int shortestWalk)
        {
            var grid = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[0]);
            grid.Zone2Turns = turns;
            int[,] dist = WalkDistances(grid, grid.Centre);

            var exits = grid.Gates.Where(g => g.Kind == GateKind.Zone2Exit).ToList();
            Assert.AreEqual(8, exits.Count);

            int best = int.MaxValue;
            foreach (var gate in exits)
            {
                // The exit's outer tile is on the map edge. The outer wall never turns, so
                // its local and world tiles are the same.
                Vector2Int outer = gate.Tiles.OrderByDescending(t => grid.Ring(t)).First();
                int d = dist[outer.x, outer.y];
                Assert.GreaterOrEqual(d, 0, $"exit at {gate.InnerTile} can't be reached after {turns} turns");
                best = Mathf.Min(best, d);
            }
            Assert.AreEqual(shortestWalk, best, $"shortest walk after {turns} turns");
        }

        /// <summary>
        /// From the "2layer maze reference" sheet: in all 4 orientations, every zone 1 exit
        /// reaches every zone 2 exit (4 × 8 × 4 = 128 routes), with the valve gates closed.
        /// </summary>
        [Test]
        public void EveryZone1Exit_ReachesEveryZone2Exit_AllOrientations()
        {
            var grid = MazeGridTests.LoadGrid(MazeFolder + TurnFiles[0]);
            var z1 = grid.Gates.Where(g => g.Kind == GateKind.Zone1Exit).ToList();
            var z2 = grid.Gates.Where(g => g.Kind == GateKind.Zone2Exit).ToList();
            Assert.AreEqual(4, z1.Count);
            Assert.AreEqual(8, z2.Count);

            int routes = 0;
            for (int turns = 0; turns < 4; turns++)
            {
                grid.Zone2Turns = turns;
                foreach (var from in z1)
                {
                    // Start on the zone 1 exit's outer tile (the side facing zone 2).
                    Vector2Int start = from.Tiles.OrderByDescending(t => grid.Ring(t)).First();
                    int[,] dist = WalkDistances(grid, start);
                    foreach (var to in z2)
                    {
                        Vector2Int end = to.Tiles.OrderByDescending(t => grid.Ring(t)).First();
                        Assert.GreaterOrEqual(dist[end.x, end.y], 0,
                            $"{turns * 90}°: zone 1 exit {from.InnerTile} can't reach zone 2 exit {to.InnerTile}");
                        routes++;
                    }
                }
            }
            Assert.AreEqual(128, routes);
        }

        // Steps from start to every tile, walking north/east/south/west. Walls and valve
        // grates (closed) block; gates count as open. -1 = can't be reached.
        static int[,] WalkDistances(MazeGrid grid, Vector2Int start)
        {
            var dist = new int[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    dist[x, y] = -1;

            var queue = new Queue<Vector2Int>();
            dist[start.x, start.y] = 0;
            queue.Enqueue(start);
            Vector2Int[] steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var s in steps)
                {
                    var n = p + s;
                    if (!grid.InBounds(n.x, n.y) || dist[n.x, n.y] >= 0) continue;
                    var code = grid.CodeAt(n);
                    if (code == TileType.Wall || code == TileType.Valve) continue;
                    dist[n.x, n.y] = dist[p.x, p.y] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
