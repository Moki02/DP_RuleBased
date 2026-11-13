// ============================================================================
// RoomDoorTests.cs   (EditMode tests)
// Checks where the room doors go on the real 2-layer maze: 40 doors in 7 rooms, every room
// has at least one, each door sits between a room tile and a walkable non-room tile, no
// edge gets two doors, and the numbering is fixed (the save relies on it).
//
// DESIGN RULES: none yet (pending GDD entry: room doors, 2026-11-13).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: if the maze CSV changes, update the counts below.
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for RoomDoorLayout.</summary>
    public class RoomDoorTests
    {
        MazeGrid grid;
        List<RoomDoorSpot> doors;

        [SetUp]
        public void Load()
        {
            grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            doors = RoomDoorLayout.Find(grid);
        }

        [Test]
        public void RealMaze_Has40Doors_In7Rooms()
        {
            Assert.AreEqual(7, grid.Rooms.Count);
            Assert.AreEqual(40, doors.Count);
            var perRoom = new int[grid.Rooms.Count];
            foreach (var d in doors) perRoom[d.Room]++;
            for (int r = 0; r < perRoom.Length; r++)
                Assert.GreaterOrEqual(perRoom[r], 1, $"room {r} has no door");
        }

        [Test]
        public void EveryDoor_IsBetweenARoomTile_AndAWalkableTile()
        {
            foreach (var d in doors)
            {
                Assert.AreEqual(TileType.Room, grid.CodeAt(d.Inside), $"door {d.Index} inside");
                var outCode = grid.CodeAt(d.Outside);
                Assert.AreNotEqual(TileType.Wall, outCode, $"door {d.Index} opens onto a wall");
                Assert.AreNotEqual(TileType.Room, outCode, $"door {d.Index} opens onto room floor");
                Assert.AreEqual(1, Mathf.Abs(d.Out.x) + Mathf.Abs(d.Out.y), $"door {d.Index} step");
                Assert.IsTrue(grid.Rooms[d.Room].Tiles.Contains(d.Inside), $"door {d.Index} room");
            }
        }

        [Test]
        public void NoEdge_HasTwoDoors_AndNumbersAreInOrder()
        {
            var edges = new HashSet<(Vector2Int, Vector2Int)>();
            for (int i = 0; i < doors.Count; i++)
            {
                Assert.AreEqual(i, doors[i].Index);
                Assert.IsTrue(edges.Add((doors[i].Inside, doors[i].Outside)), $"door {i} doubled");
            }
        }

        [Test]
        public void Numbering_IsTheSameEveryTime()
        {
            var again = RoomDoorLayout.Find(MazeGridTests.LoadGrid(MazeGridTests.Maze0Path));
            Assert.AreEqual(doors.Count, again.Count);
            for (int i = 0; i < doors.Count; i++)
            {
                Assert.AreEqual(doors[i].Inside, again[i].Inside);
                Assert.AreEqual(doors[i].Out, again[i].Out);
            }
        }
    }
}
