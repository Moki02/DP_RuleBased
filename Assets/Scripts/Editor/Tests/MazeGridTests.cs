// ============================================================================
// MazeGridTests.cs   (EditMode tests)
// Checks that MazeGrid reads the 2-layer maze CSV correctly: tile counts, the 16 gates,
// the 8 valves, the 7 rooms, the spawn tile, rings and maze parts.
//
// DESIGN RULES: GDD maze tab (tile codes), R8.24 (ring limits of each part).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: if the designer changes the maze CSV on purpose, update the expected
// numbers below to match. The ring limits come from GameBalance's default values.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for loading the 2-layer maze into MazeGrid.</summary>
    public class MazeGridTests
    {
        /// <summary>Path of the 0 degree maze CSV.</summary>
        public const string Maze0Path = "Assets/_Project/Data/Maze/Maze2Layer_0deg.csv";

        MazeGrid grid;

        /// <summary>Loads a fresh grid with GameBalance's default ring limits before each test.</summary>
        [SetUp]
        public void LoadMaze()
        {
            grid = LoadGrid(Maze0Path);
        }

        /// <summary>Loads a maze CSV with GameBalance's default ring limits. Shared with other test files.</summary>
        public static MazeGrid LoadGrid(string path)
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(csv, $"Maze CSV not found at {path}");
            var balance = ScriptableObject.CreateInstance<GameBalance>();
            var g = MazeGrid.FromCsv(csv.text, csv.name, balance.GetMazeRings());
            Object.DestroyImmediate(balance);
            return g;
        }

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        // ------------------------------------------------------------------ Size and codes

        [Test]
        public void Grid_Is95By95_WithCentre47()
        {
            Assert.AreEqual(95, grid.Width);
            Assert.AreEqual(95, grid.Height);
            Assert.AreEqual(V(47, 47), grid.Centre);
            Assert.IsTrue(grid.CanRotate);
        }

        [Test]
        public void CodeCounts_MatchTheDesignersFile()
        {
            Assert.AreEqual(4993, grid.Count(TileType.Wall), "W");
            Assert.AreEqual(48, grid.Count(TileType.Gate), "G");
            Assert.AreEqual(3632, grid.Count(TileType.Floor), "floor (blank)");
            Assert.AreEqual(175, grid.Count(TileType.Room), "R");
            Assert.AreEqual(169, grid.Count(TileType.Spawn), "S");
            Assert.AreEqual(8, grid.Count(TileType.Valve), "V");
            Assert.AreEqual(0, grid.Count(TileType.Door), "D (not used on this map)");
            Assert.AreEqual(0, grid.Count(TileType.TimedDoor), "T (not used on this map)");
        }

        [Test]
        public void SpawnTile_IsBase()
        {
            Assert.AreEqual(TileType.Spawn, grid.Get(47, 47));
        }

        [Test]
        public void ParseCode_AcceptsValveAndTimedDoor()
        {
            Assert.AreEqual(TileType.Valve, MazeGrid.ParseCode("V", out bool kv));
            Assert.IsTrue(kv);
            Assert.AreEqual(TileType.TimedDoor, MazeGrid.ParseCode(" t ", out bool kt));
            Assert.IsTrue(kt);
            Assert.AreEqual(TileType.Door, MazeGrid.ParseCode("D", out _));
            MazeGrid.ParseCode("Q", out bool kq);
            Assert.IsFalse(kq, "unknown codes are reported");
        }

        // ------------------------------------------------------------------ Rings and parts

        [TestCase(47, 47, 0, MazePart.Hub)]
        [TestCase(40, 47, 7, MazePart.Hub)]
        [TestCase(37, 46, 10, MazePart.Zone1)]
        [TestCase(22, 35, 25, MazePart.Zone1)]
        [TestCase(20, 35, 27, MazePart.Zone1)]
        [TestCase(60, 15, 32, MazePart.Zone2)]
        [TestCase(72, 3, 44, MazePart.Zone2)]
        [TestCase(2, 27, 45, MazePart.OuterWall)]
        [TestCase(0, 0, 47, MazePart.OuterWall)]
        public void RingAndPart(int x, int y, int ring, MazePart part)
        {
            Assert.AreEqual(ring, grid.Ring(x, y));
            Assert.AreEqual(part, grid.PartOf(x, y));
        }

        // ------------------------------------------------------------------ Gates

        [Test]
        public void Gates_16GroupsOf3Tiles()
        {
            Assert.AreEqual(16, grid.Gates.Count);
            foreach (var g in grid.Gates)
                Assert.AreEqual(3, g.Tiles.Count, $"gate at {g.InnerTile}");
        }

        [Test]
        public void Gates_InnerTilesAndKinds()
        {
            AssertGates(GateKind.HubGate, 7, V(40, 47), V(47, 40), V(47, 54), V(54, 47));
            AssertGates(GateKind.Zone1Exit, 25, V(22, 35), V(35, 72), V(59, 22), V(72, 59));
            AssertGates(GateKind.Zone2Exit, 45, V(2, 27), V(2, 67), V(27, 2), V(27, 92),
                                                 V(67, 2), V(67, 92), V(92, 27), V(92, 67));
            Assert.IsFalse(grid.Gates.Any(g => g.Kind == GateKind.Other));
        }

        void AssertGates(GateKind kind, int ring, params Vector2Int[] expected)
        {
            var found = grid.Gates.Where(g => g.Kind == kind).ToList();
            Assert.That(found.Select(g => g.InnerTile), Is.EquivalentTo(expected), $"{kind} inner tiles");
            foreach (var g in found) Assert.AreEqual(ring, g.Ring, $"{kind} at {g.InnerTile}");
        }

        [Test]
        public void Gates_IndexMatchesListPosition()
        {
            for (int i = 0; i < grid.Gates.Count; i++) Assert.AreEqual(i, grid.Gates[i].Index);
        }

        // ------------------------------------------------------------------ Valves and rooms

        [Test]
        public void Valves_3InZone1_5InZone2()
        {
            Assert.AreEqual(8, grid.Valves.Count);
            Assert.That(grid.Valves.Where(v => v.Part == MazePart.Zone1).Select(v => v.Tile),
                Is.EquivalentTo(new[] { V(64, 39), V(37, 46), V(35, 56) }));
            Assert.That(grid.Valves.Where(v => v.Part == MazePart.Zone2).Select(v => v.Tile),
                Is.EquivalentTo(new[] { V(72, 3), V(60, 15), V(9, 18), V(77, 50), V(12, 81) }));
        }

        [Test]
        public void Rooms_Seven5By5()
        {
            Assert.AreEqual(7, grid.Rooms.Count);
            foreach (var r in grid.Rooms)
            {
                Assert.AreEqual(5, r.Tiles.width, $"room at {r.Tiles.min}");
                Assert.AreEqual(5, r.Tiles.height, $"room at {r.Tiles.min}");
            }
        }

        // ------------------------------------------------------------------ Local and world tiles

        [Test]
        public void LocalToWorld_OutsideZone2_NeverMoves()
        {
            var outside = new List<Vector2Int> { V(47, 47), V(22, 35), V(2, 27), V(0, 0) };
            for (int k = 0; k < 4; k++)
            {
                grid.Zone2Turns = k;
                foreach (var t in outside)
                {
                    Assert.AreEqual(t, grid.LocalToWorld(t), $"k={k} {t}");
                    Assert.AreEqual(t, grid.WorldToLocal(t), $"k={k} {t}");
                }
            }
        }

        [Test]
        public void WorldToLocal_UndoesLocalToWorld_ForEveryTile()
        {
            for (int k = 0; k < 4; k++)
            {
                grid.Zone2Turns = k;
                for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var t = V(x, y);
                    Assert.AreEqual(t, grid.WorldToLocal(grid.LocalToWorld(t)), $"k={k} {t}");
                }
            }
        }

        [Test]
        public void Zone2Turns_WrapsAround()
        {
            grid.Zone2Turns = 5;
            Assert.AreEqual(1, grid.Zone2Turns);
            grid.Zone2Turns = -1;
            Assert.AreEqual(3, grid.Zone2Turns);
        }
    }
}
