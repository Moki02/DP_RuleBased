// ============================================================================
// ValveSpeedUpTests.cs   (EditMode tests)
// Checks the Valve Timing speed-up (2026-11-13): the needle gets × 1.25 faster after the
// 1st and the 2nd green hit and then stays at that speed (also for the exit valve's 4th
// hit); a multiplier of 1 or 0 speed-ups means no change.
//
// DESIGN RULES: none yet (pending GDD entry: valve speed-up, 2026-11-13).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for ValveDifficulty.SpeedAfterHits.</summary>
    public class ValveSpeedUpTests
    {
        [Test]
        public void SpeedsUp_AfterHit1_AndHit2_ThenStays()
        {
            Assert.AreEqual(120f, ValveDifficulty.SpeedAfterHits(120f, 0, 1.25f, 2), 0.01f, "start");
            Assert.AreEqual(150f, ValveDifficulty.SpeedAfterHits(120f, 1, 1.25f, 2), 0.01f, "after 1st green");
            Assert.AreEqual(187.5f, ValveDifficulty.SpeedAfterHits(120f, 2, 1.25f, 2), 0.01f, "after 2nd green");
            Assert.AreEqual(187.5f, ValveDifficulty.SpeedAfterHits(120f, 3, 1.25f, 2), 0.01f, "exit valve: 3rd hit keeps it");
        }

        [Test]
        public void NoSpeedUp_WhenTurnedOff()
        {
            Assert.AreEqual(200f, ValveDifficulty.SpeedAfterHits(200f, 2, 1f, 2), 0.01f, "multiplier 1");
            Assert.AreEqual(200f, ValveDifficulty.SpeedAfterHits(200f, 2, 1.25f, 0), 0.01f, "0 speed-ups");
        }

        [Test]
        public void DefaultBalance_Is_1_25_Twice()
        {
            var b = ScriptableObject.CreateInstance<GameBalance>();
            Assert.AreEqual(1.25f, b.valveSpeedUpMult, 0.0001f);
            Assert.AreEqual(2, b.valveSpeedUps);
            Object.DestroyImmediate(b);
        }
    }
}
