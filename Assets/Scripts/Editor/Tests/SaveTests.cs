// ============================================================================
// SaveTests.cs   (EditMode tests)
// Checks saving:
//   - every SaveData field survives a write/read round-trip;
//   - the game logic (map, upgrades, gates, menu memory) comes back the same after
//     capture -> JSON -> restore;
//   - loading a day-5 save gives zone 2 two turns (180°) and the right tiles;
//   - a bad or old-version file is rejected with a clear message and nothing crashes;
//   - the file is written through a temporary file and no temporary file is left behind.
//
// DESIGN RULES: R11.8, R8.24.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for SaveFile and SaveCodec.</summary>
    public class SaveTests
    {
        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        static SaveData Full() => new SaveData
        {
            savedAt = "test",
            day = 7, timeOfDay = 123.5f, zone2Turns = 3,
            cleanStorage = 38.4f, canteen = 1.5f,
            ownedUpgrades = new List<string> { "S2", "W1", "M1" },
            gates = new List<GateSave> { new GateSave { index = 3, litres = 4f, open = true }, new GateSave { index = 9, litres = 6.5f } },
            valvesWon = new List<int> { 1, 4 },
            mapWidth = 95, mapHeight = 95, revealedTiles = "AQID",
            markers = new List<Vector2Int> { V(60, 15), V(47, 47) },
            menuLastPage = "Skills", menuLastBuildCard = "W4", menuLastSkillsCard = "V3",
            hasPlayer = true, playerPosition = new Vector3(188f, 0.1f, 190f), playerYaw = 270f,
        };

        // ------------------------------------------------------------------ Round trips

        [Test]
        public void RoundTrip_EveryField()
        {
            var a = Full();
            Assert.IsTrue(SaveFile.TryParse(SaveFile.ToJson(a), out var b, out string error), error);

            Assert.AreEqual(SaveFile.CurrentVersion, b.version);
            Assert.AreEqual(a.day, b.day);
            Assert.AreEqual(a.timeOfDay, b.timeOfDay);
            Assert.AreEqual(a.zone2Turns, b.zone2Turns);
            Assert.AreEqual(a.cleanStorage, b.cleanStorage);
            Assert.AreEqual(a.canteen, b.canteen);
            CollectionAssert.AreEqual(a.ownedUpgrades, b.ownedUpgrades);
            Assert.AreEqual(2, b.gates.Count);
            Assert.AreEqual(9, b.gates[1].index);
            Assert.AreEqual(6.5f, b.gates[1].litres);
            Assert.IsTrue(b.gates[0].open);
            CollectionAssert.AreEqual(a.valvesWon, b.valvesWon);
            Assert.AreEqual(a.mapWidth, b.mapWidth);
            Assert.AreEqual(a.mapHeight, b.mapHeight);
            Assert.AreEqual(a.revealedTiles, b.revealedTiles);
            CollectionAssert.AreEqual(a.markers, b.markers);
            Assert.AreEqual(a.menuLastPage, b.menuLastPage);
            Assert.AreEqual(a.menuLastBuildCard, b.menuLastBuildCard);
            Assert.AreEqual(a.menuLastSkillsCard, b.menuLastSkillsCard);
            Assert.AreEqual(a.hasPlayer, b.hasPlayer);
            Assert.AreEqual(a.playerPosition, b.playerPosition);
            Assert.AreEqual(a.playerYaw, b.playerYaw);
        }

        [Test]
        public void RoundTrip_GameLogic()
        {
            // Map: some tiles and two markers, one in zone 2.
            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            var map = new MapReveal(grid);
            map.EnterTile(V(47, 47), V(1, 0), true, 4);
            map.Reveal(V(60, 15));
            map.ToggleMarker(V(60, 15), 3);
            map.ToggleMarker(V(40, 47), 3);

            // Upgrades: two bought.
            var upgrades = new UpgradeLogic(UpgradeTests.LoadReal(), 10f, 25f);
            upgrades.AddStored(100f);
            upgrades.Buy("S2", true, false);
            upgrades.Buy("W1", true, false);

            // Gates: one half full, one won.
            var half = new GateLockLogic(10f);
            half.Pour(6f);
            var won = new GateLockLogic(4f);
            won.Pour(4f);
            won.StartMinigame(0f);
            won.FinishMinigame(MinigameResult.Won, 0f, 10f);

            var memory = new MenuMemory();
            memory.Press(MenuKey.B);
            memory.SwitchTo(MenuPage.Skills);
            memory.Select(UpgradeTab.Build, "W4");
            memory.Select(UpgradeTab.Skills, "V3");

            // Capture -> JSON -> parse.
            var data = new SaveData { day = 3 };
            SaveCodec.CaptureMap(map, data);
            data.ownedUpgrades = upgrades.Owned.ToList();
            data.cleanStorage = upgrades.Stored;
            SaveCodec.CaptureGate(0, half, data);
            SaveCodec.CaptureGate(1, won, data);
            data.menuLastPage = memory.LastPage.ToString();
            data.menuLastBuildCard = memory.LastBuildCard;
            data.menuLastSkillsCard = memory.LastSkillsCard;
            Assert.IsTrue(SaveFile.TryParse(SaveFile.ToJson(data), out var loaded, out string error), error);

            // Restore into fresh objects and compare.
            var map2 = new MapReveal(grid);
            Assert.IsTrue(SaveCodec.RestoreMap(map2, loaded));
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    Assert.AreEqual(map.IsRevealedLocal(x, y), map2.IsRevealedLocal(x, y), $"tile ({x}, {y})");
            CollectionAssert.AreEquivalent(map.MarkersLocal, map2.MarkersLocal);
            Assert.AreEqual(map.MapPercent, map2.MapPercent, 1e-4f);

            var upgrades2 = new UpgradeLogic(UpgradeTests.LoadReal(), 10f, 25f);
            upgrades2.Restore(loaded.ownedUpgrades, loaded.cleanStorage);
            CollectionAssert.AreEqual(upgrades.Owned, upgrades2.Owned);
            Assert.AreEqual(upgrades.Stored, upgrades2.Stored, 1e-4f);
            Assert.AreEqual(50f, upgrades2.Cap, "S2 restored, so the cap is restored too");

            var half2 = new GateLockLogic(10f);
            var g0 = SaveCodec.FindGate(loaded, 0);
            half2.Restore(g0.litres, g0.open);
            Assert.AreEqual(6f, half2.Litres, 1e-4f);
            Assert.AreEqual(GateLockState.Filling, half2.State);
            var won2 = new GateLockLogic(4f);
            var g1 = SaveCodec.FindGate(loaded, 1);
            won2.Restore(g1.litres, g1.open);
            Assert.AreEqual(GateLockState.Open, won2.State);
            Assert.IsNull(SaveCodec.FindGate(loaded, 7), "a gate that wasn't saved");

            var memory2 = new MenuMemory
            {
                LastPage = SaveCodec.PageFrom(loaded),
                LastBuildCard = loaded.menuLastBuildCard,
                LastSkillsCard = loaded.menuLastSkillsCard,
            };
            memory2.Press(MenuKey.Tab);
            Assert.AreEqual(MenuPage.Skills, memory2.Current, "Tab reopens the saved page");
            Assert.AreEqual("W4", memory2.SelectedFor(UpgradeTab.Build));
            Assert.AreEqual("V3", memory2.SelectedFor(UpgradeTab.Skills));
        }

        // ------------------------------------------------------------------ Day 5 -> 180 degrees

        [Test]
        public void Day5Save_Gives180Degrees_AndRightTiles()
        {
            var balance = ScriptableObject.CreateInstance<GameBalance>();
            float daylight = balance.dayLengthSeconds;
            int n = balance.zone2DaysBetweenTurns;
            Object.DestroyImmediate(balance);

            var data = new SaveData { day = 5, timeOfDay = 0f, zone2Turns = 2 };
            Assert.IsTrue(SaveFile.TryParse(SaveFile.ToJson(data), out var loaded, out _));
            int turns = SaveCodec.Zone2TurnsFor(loaded, daylight, n);
            Assert.AreEqual(2, turns, "day 5 = 180 degrees");

            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            var expected = MazeGridTests.LoadGrid("Assets/_Project/Data/Maze/Maze2Layer_180deg.csv");
            grid.Zone2Turns = turns;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    Assert.AreEqual(expected.Get(x, y), grid.CodeAt(x, y), $"({x}, {y})");

            Assert.AreEqual(1, SaveCodec.Zone2TurnsFor(new SaveData { day = 4, timeOfDay = 10f }, daylight, n), "day 4 by day");
            Assert.AreEqual(2, SaveCodec.Zone2TurnsFor(new SaveData { day = 4, timeOfDay = daylight + 5f }, daylight, n), "night of day 4");
        }

        // ------------------------------------------------------------------ Bad files

        [TestCase("", "empty")]
        [TestCase("this is not a save", "valid")]
        [TestCase("{\"version\":0,\"day\":3}", "version")]
        [TestCase("{\"version\":99,\"day\":3}", "version")]
        [TestCase("{\"version\":1,\"day\":0}", "day")]
        public void BadFile_IsRejectedWithAReason(string json, string reasonContains)
        {
            bool ok = true;
            string error = null;
            Assert.DoesNotThrow(() => ok = SaveFile.TryParse(json, out _, out error));
            Assert.IsFalse(ok);
            StringAssert.Contains(reasonContains, error);
        }

        [Test]
        public void MissingFile_IsRejected()
        {
            string path = Path.Combine(Path.GetTempPath(), "thelastdrop_missing_" + System.Guid.NewGuid() + ".json");
            Assert.IsFalse(SaveFile.TryLoad(path, out _, out string error));
            StringAssert.Contains("no save", error);
        }

        [Test]
        public void MapSaveForAnotherMaze_IsRefused()
        {
            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            var map = new MapReveal(grid);
            Assert.IsFalse(SaveCodec.RestoreMap(map, new SaveData { mapWidth = 55, mapHeight = 55, revealedTiles = "" }));
            Assert.AreEqual(0f, map.MapPercent, "map stays black");
        }

        // ------------------------------------------------------------------ Writing

        [Test]
        public void Write_ReplacesFile_AndLeavesNoTempFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "thelastdrop_test_" + System.Guid.NewGuid());
            string path = Path.Combine(dir, "save.json");
            try
            {
                SaveFile.Write(path, new SaveData { day = 2 });
                SaveFile.Write(path, new SaveData { day = 9 });  // overwrite
                Assert.IsTrue(SaveFile.TryLoad(path, out var data, out string error), error);
                Assert.AreEqual(9, data.day);
                Assert.IsFalse(File.Exists(path + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
