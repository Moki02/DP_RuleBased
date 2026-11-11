// ============================================================================
// MapRevealTests.cs   (EditMode tests)
// Checks the map reveal rules on a small hand-made room, the map % sum, markers, and
// that what the player revealed in zone 2 turns with zone 2.
//
// DESIGN RULES: R8.1a to R8.1d (reveal), R8.4 (markers), R8.28 (knowledge turns).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: the small test room is drawn in Room() below: '#' = wall, '.' = floor.
// ============================================================================

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for MapReveal.</summary>
    public class MapRevealTests
    {
        static Vector2Int V(int x, int y) => new Vector2Int(x, y);
        static readonly Vector2Int North = V(0, -1), East = V(1, 0), None = Vector2Int.zero;

        // A 9 × 7 room with walls all round: 7 × 5 = 35 floor tiles. Extra walls can be added.
        static MazeGrid Room(params Vector2Int[] extraWalls)
        {
            string[] rows =
            {
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            };
            var g = new MazeGrid(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    g.Set(x, y, rows[y][x] == '#' ? TileType.Wall : TileType.Floor);
            foreach (var w in extraWalls) g.Set(w.x, w.y, TileType.Wall);
            g.Rings = new MazeRings(100, 100, 100); // the whole room counts as hub: nothing turns
            return g;
        }

        static int RevealedCount(MapReveal map)
        {
            int n = 0;
            for (int y = 0; y < map.Grid.Height; y++)
                for (int x = 0; x < map.Grid.Width; x++)
                    if (map.IsRevealed(x, y)) n++;
            return n;
        }

        // ------------------------------------------------------------------ R8.1d

        [Test]
        public void StartsAllBlack()
        {
            var map = new MapReveal(Room());
            Assert.AreEqual(0, RevealedCount(map));
            Assert.AreEqual(0f, map.MapPercent);
        }

        // ------------------------------------------------------------------ R8.1a

        [Test]
        public void Entering_RevealsTileAnd4Neighbours()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(4, 3), None, revealDiagonals: false, lookAheadTiles: 2);
            Assert.AreEqual(5, RevealedCount(map));
            foreach (var t in new[] { V(4, 3), V(4, 2), V(5, 3), V(4, 4), V(3, 3) })
                Assert.IsTrue(map.IsRevealed(t), $"{t}");
            Assert.IsFalse(map.IsRevealed(V(5, 2)), "corners need Keen Eyes");
        }

        [Test]
        public void KeenEyes_RevealsAll8Neighbours()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(4, 3), None, revealDiagonals: true, lookAheadTiles: 2);
            Assert.AreEqual(9, RevealedCount(map));
            Assert.IsTrue(map.IsRevealed(V(5, 2)));
            Assert.IsTrue(map.IsRevealed(V(3, 4)));
        }

        // ------------------------------------------------------------------ R8.1b / R8.1c

        [Test]
        public void LookAhead_2TilesInFacingDirection()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(2, 3), East, false, 2);
            Assert.IsTrue(map.IsRevealed(V(3, 3)));
            Assert.IsTrue(map.IsRevealed(V(4, 3)));
            Assert.IsFalse(map.IsRevealed(V(5, 3)), "only 2 tiles ahead");
        }

        [Test]
        public void TrailSense_Looks4Ahead()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(2, 3), East, false, 4);
            for (int x = 3; x <= 6; x++) Assert.IsTrue(map.IsRevealed(V(x, 3)), $"({x}, 3)");
            Assert.IsFalse(map.IsRevealed(V(7, 3)), "only 4 tiles ahead");
        }

        [Test]
        public void LookAhead_StopsAtFirstWall_AndRevealsIt()
        {
            var map = new MapReveal(Room(V(4, 3)));   // a wall 2 tiles east of the player
            map.EnterTile(V(2, 3), East, false, 4);
            Assert.IsTrue(map.IsRevealed(V(3, 3)));
            Assert.IsTrue(map.IsRevealed(V(4, 3)), "the wall itself is revealed");
            Assert.IsFalse(map.IsRevealed(V(5, 3)), "R8.1c: nothing behind a wall");
            Assert.IsFalse(map.IsRevealed(V(6, 3)));
        }

        [Test]
        public void LookAhead_GoesNorthToo()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(4, 5), North, false, 2);
            Assert.IsTrue(map.IsRevealed(V(4, 3)));
            Assert.IsFalse(map.IsRevealed(V(4, 2)));
        }

        [Test]
        public void RevealedTiles_StayRevealed()
        {
            var map = new MapReveal(Room());
            map.EnterTile(V(4, 3), None, false, 2);
            map.EnterTile(V(1, 1), None, false, 2);
            Assert.IsTrue(map.IsRevealed(V(4, 3)));
            Assert.IsTrue(map.IsRevealed(V(1, 1)));
        }

        [Test]
        public void SnapToTileDirection_PicksNearestCompassPoint()
        {
            Assert.AreEqual(North, MapReveal.SnapToTileDirection(new Vector3(0f, 0f, 1f)));
            Assert.AreEqual(East, MapReveal.SnapToTileDirection(new Vector3(1f, -0.5f, 0.2f)));
            Assert.AreEqual(V(0, 1), MapReveal.SnapToTileDirection(new Vector3(-0.2f, 0f, -1f)), "south");
            Assert.AreEqual(V(-1, 0), MapReveal.SnapToTileDirection(new Vector3(-0.9f, 0f, 0.5f)), "west");
            Assert.AreEqual(None, MapReveal.SnapToTileDirection(Vector3.up), "straight up = no direction");
        }

        // ------------------------------------------------------------------ Map %

        [Test]
        public void MapPercent_CountsWalkableTilesOnly()
        {
            var map = new MapReveal(Room());
            Assert.AreEqual(35, map.TotalWalkableCount);

            map.Reveal(V(4, 3));
            Assert.AreEqual(100f / 35f, map.MapPercent, 1e-4f);

            map.Reveal(V(0, 0)); // a wall: doesn't count
            Assert.AreEqual(100f / 35f, map.MapPercent, 1e-4f);

            map.Reveal(V(4, 3)); // already revealed: doesn't count twice
            Assert.AreEqual(1, map.RevealedWalkableCount);

            map.RevealAll();
            Assert.AreEqual(100f, map.MapPercent, 1e-4f);
        }

        // ------------------------------------------------------------------ Markers (R8.4)

        [Test]
        public void Markers_MaxThenRemove()
        {
            var map = new MapReveal(Room());
            Assert.AreEqual(MarkerResult.Added, map.ToggleMarker(V(1, 1), 3));
            Assert.AreEqual(MarkerResult.Added, map.ToggleMarker(V(2, 1), 3));
            Assert.AreEqual(MarkerResult.Added, map.ToggleMarker(V(3, 1), 3));
            Assert.AreEqual(MarkerResult.Full, map.ToggleMarker(V(4, 1), 3));
            Assert.AreEqual(3, map.MarkersLocal.Count);

            Assert.AreEqual(MarkerResult.Removed, map.ToggleMarker(V(2, 1), 3));
            Assert.AreEqual(2, map.MarkersLocal.Count);
            Assert.AreEqual(MarkerResult.Added, map.ToggleMarker(V(4, 1), 3), "room for one more now");

            Assert.AreEqual(MarkerResult.Added, map.ToggleMarker(V(5, 1), 8), "Landmarks allows 8");
        }

        // ------------------------------------------------------------------ Zone 2 turns (R8.28)

        [Test]
        public void RevealedZone2Tile_ShowsAtTurnedPosition()
        {
            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            var map = new MapReveal(grid);
            map.Reveal(V(60, 15));     // a zone 2 valve, at 0 degrees
            map.Reveal(V(22, 35));     // a zone 1 gate tile: never moves
            float percent = map.MapPercent;

            grid.Zone2Turns = 1;
            Assert.IsTrue(map.IsRevealed(V(79, 60)), "the zone 2 tile moved with the zone");
            Assert.IsFalse(map.IsRevealed(V(60, 15)), "its old spot now holds a different, unseen tile");
            Assert.IsTrue(map.IsRevealed(V(22, 35)), "zone 1 doesn't move");
            Assert.AreEqual(percent, map.MapPercent, "turning never changes map %");

            grid.Zone2Turns = 3;
            Assert.IsTrue(map.IsRevealed(V(15, 34)));
        }

        [Test]
        public void Zone2Marker_TurnsWithTheZone()
        {
            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            var map = new MapReveal(grid);
            map.ToggleMarker(V(60, 15), 3);
            map.ToggleMarker(V(47, 47), 3); // in the hub

            grid.Zone2Turns = 2;
            var now = map.MarkersWorld().ToList();
            CollectionAssert.Contains(now, V(34, 79));
            CollectionAssert.Contains(now, V(47, 47));

            // Pressing F on the turned spot removes the same marker.
            Assert.AreEqual(MarkerResult.Removed, map.ToggleMarker(V(34, 79), 3));
        }
    }
}
