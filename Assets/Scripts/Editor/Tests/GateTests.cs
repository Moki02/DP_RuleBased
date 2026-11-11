// ============================================================================
// GateTests.cs   (EditMode tests)
// Checks the Valve Timing difficulty function against the GDD table, and the lock basin
// state machine: empty -> filling -> full -> minigame -> open, lose / quit, and shut at night.
//
//   Gate                          t       Band     Speed
//   Hub gates (ring 7)            0       30°      120
//   Zone 1 exits (ring 25)        0.457   23.1°    156.6
//   Valve at (37,46), ring 10     0.029   29.6°    122.3
//   Valve at (60,15), ring 32     0.657   20.1°    172.6
//   Zone 2 exits                  exit    12°      220   (4 hits, 3 misses)
//
// DESIGN RULES: R8.8, R8.9, R8.21 / R8.23, R9.2, R9.3, R9.6, R11.6.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: the expected numbers come from the GDD; they use GameBalance's defaults.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for ValveDifficulty and GateLockLogic.</summary>
    public class GateTests
    {
        GameBalance balance;

        /// <summary>A GameBalance with default values for each test.</summary>
        [SetUp]
        public void MakeBalance() => balance = ScriptableObject.CreateInstance<GameBalance>();

        /// <summary>Cleans up the temporary GameBalance.</summary>
        [TearDown]
        public void CleanUp() => Object.DestroyImmediate(balance);

        // ------------------------------------------------------------------ Difficulty (R9.6)

        // ring, expected t, band, speed (rounded like the GDD table)
        [TestCase(7, 0f, 30f, 120f)]          // hub gates
        [TestCase(25, 0.457f, 23.1f, 156.6f)] // zone 1 exits
        [TestCase(10, 0.029f, 29.6f, 122.3f)] // valve at (37, 46)
        [TestCase(32, 0.657f, 20.1f, 172.6f)] // valve at (60, 15)
        public void Difficulty_MatchesGddTable(int ring, float t, float band, float speed)
        {
            Assert.AreEqual(t, ValveDifficulty.T(ring, balance), 0.0005f, "t");
            var s = ValveDifficulty.ForRing(ring, balance);
            Assert.AreEqual(band, s.bandWidth, 0.05f, "band");
            Assert.AreEqual(speed, s.needleSpeed, 0.05f, "speed");
            Assert.AreEqual(3, s.hitsToWin);
            Assert.AreEqual(3, s.missesToLose);
        }

        [Test]
        public void Difficulty_ZoneTwoExitsUseExitValve()
        {
            var s = ValveDifficulty.ForGate(GateKind.Zone2Exit, 45, balance);
            Assert.AreEqual(12f, s.bandWidth);
            Assert.AreEqual(220f, s.needleSpeed);
            Assert.AreEqual(4, s.hitsToWin);
            Assert.AreEqual(3, s.missesToLose);
            Assert.AreNotSame(balance.ExitValveSettings, s, "a copy, so a gate can't change GameBalance");
        }

        [Test]
        public void Difficulty_OtherGatesUseFormula()
        {
            var hub = ValveDifficulty.ForGate(GateKind.HubGate, 7, balance);
            Assert.AreEqual(30f, hub.bandWidth, 1e-4f);
            var z1 = ValveDifficulty.ForGate(GateKind.Zone1Exit, 25, balance);
            Assert.AreEqual(156.571f, z1.needleSpeed, 0.01f);
            Assert.AreEqual(1f, ValveDifficulty.T(60, balance), "t never goes past 1");
        }

        [Test]
        public void Difficulty_RealGatesFromTheMaze()
        {
            var grid = MazeGridTests.LoadGrid(MazeGridTests.Maze0Path);
            foreach (var g in grid.Gates)
            {
                var s = ValveDifficulty.ForGate(g.Kind, g.Ring, balance);
                if (g.Kind == GateKind.HubGate) Assert.AreEqual(30f, s.bandWidth, 1e-4f, $"{g.InnerTile}");
                if (g.Kind == GateKind.Zone1Exit) Assert.AreEqual(23.143f, s.bandWidth, 0.01f, $"{g.InnerTile}");
                if (g.Kind == GateKind.Zone2Exit) Assert.AreEqual(12f, s.bandWidth, $"{g.InnerTile}");
            }
        }

        [Test]
        public void LockLitres_4_10_18()
        {
            Assert.AreEqual(4f, balance.GetLockLiters(0), "hub gates");
            Assert.AreEqual(10f, balance.GetLockLiters(1), "zone 1 exits");
            Assert.AreEqual(18f, balance.GetLockLiters(2), "zone 2 exits");
        }

        // ------------------------------------------------------------------ Lock state machine

        [Test]
        public void Lock_EmptyFillingFullMinigameOpen()
        {
            var l = new GateLockLogic(4f);
            Assert.AreEqual(GateLockState.Empty, l.State);
            Assert.IsFalse(l.IsPassable);

            Assert.AreEqual(1f, l.Pour(1f));
            Assert.AreEqual(GateLockState.Filling, l.State);
            Assert.IsFalse(l.CanStartMinigame(0f), "not full yet");

            Assert.AreEqual(3f, l.Pour(5f), "only 3 L fit");
            Assert.AreEqual(GateLockState.Full, l.State);
            Assert.AreEqual(0f, l.Pour(1f), "a full basin takes no more");

            Assert.IsTrue(l.StartMinigame(0f));
            Assert.AreEqual(GateLockState.Minigame, l.State);

            l.FinishMinigame(MinigameResult.Won, 5f, 10f);
            Assert.AreEqual(GateLockState.Open, l.State);
            Assert.IsTrue(l.IsPassable);
        }

        [Test]
        public void Lock_PouringInSmallSteps_FillsExactly()
        {
            var l = new GateLockLogic(18f);
            for (int i = 0; i < 2000; i++) l.Pour(1f * 0.016f); // 1 L/s at about 60 frames a second
            Assert.AreEqual(GateLockState.Full, l.State);
            Assert.AreEqual(18f, l.Litres, 1e-4f);
        }

        [Test]
        public void Lock_LoseKeepsWater_AndLocksFor10s()
        {
            var l = new GateLockLogic(4f);
            l.Pour(4f);
            l.StartMinigame(100f);
            l.FinishMinigame(MinigameResult.Lost, 100f, balance.minigameLockout);
            Assert.AreEqual(GateLockState.Full, l.State);
            Assert.AreEqual(4f, l.Litres, "the water stays");
            Assert.IsFalse(l.CanStartMinigame(105f), "still locked");
            Assert.AreEqual(5f, l.LockoutLeft(105f), 1e-4f);
            Assert.IsTrue(l.CanStartMinigame(110f), "unlocked after 10 s");
        }

        [Test]
        public void Lock_QuitHasNoPenalty()
        {
            var l = new GateLockLogic(4f);
            l.Pour(4f);
            l.StartMinigame(0f);
            l.FinishMinigame(MinigameResult.Quit, 0f, 10f);
            Assert.AreEqual(GateLockState.Full, l.State);
            Assert.IsTrue(l.CanStartMinigame(0f), "can try again at once");
        }

        [Test]
        public void Lock_ShutAtNight_OpenAgainAtDawn()
        {
            var l = new GateLockLogic(4f);
            l.Restore(4f, open: true);
            Assert.IsTrue(l.IsPassable);

            l.NightLocked = true;
            Assert.IsFalse(l.IsPassable, "shut at night (R8.21)");
            Assert.AreEqual(GateLockState.Open, l.State, "still counts as won");

            l.NightLocked = false;
            Assert.IsTrue(l.IsPassable, "open again at dawn (R8.23)");
        }

        [Test]
        public void Lock_NoMinigameAtNight()
        {
            var l = new GateLockLogic(4f);
            l.Pour(4f);
            l.NightLocked = true;
            Assert.IsFalse(l.StartMinigame(0f));
            l.NightLocked = false;
            Assert.IsTrue(l.StartMinigame(0f));
        }

        [Test]
        public void Lock_ValveGateHasNoBasin_StartsFull()
        {
            var l = new GateLockLogic(0f);
            Assert.AreEqual(GateLockState.Full, l.State);
            Assert.IsTrue(l.CanStartMinigame(0f));
        }
    }
}
