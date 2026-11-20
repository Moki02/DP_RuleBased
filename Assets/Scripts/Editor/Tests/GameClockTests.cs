// ============================================================================
// GameClockTests.cs   (EditMode tests)
// Checks the day/night clock: when night starts, when a new day starts, that big time
// steps never skip an event, the debug speed, pausing, and that the clock and the zone 2
// turn schedule agree with each other.
//
// DESIGN RULES: R1.5, R8.19 (300 s daylight + 60 s night, T29/T30), R8.24 (turn schedule).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: the numbers below are GameBalance's default day and night lengths.
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for GameClockLogic.</summary>
    public class GameClockTests
    {
        GameBalance defaults;
        GameClockLogic clock;
        List<string> events;

        /// <summary>A fresh clock with GameBalance's default lengths before each test.</summary>
        [SetUp]
        public void MakeClock()
        {
            defaults = ScriptableObject.CreateInstance<GameBalance>();
            clock = new GameClockLogic(defaults.dayLengthSeconds, defaults.nightLockdownSeconds);
            events = new List<string>();
            clock.NightStarted += d => events.Add($"night {d}");
            clock.NewDay += d => events.Add($"day {d}");
        }

        /// <summary>Cleans up the temporary GameBalance.</summary>
        [TearDown]
        public void CleanUp() => Object.DestroyImmediate(defaults);

        [Test]
        public void Defaults_Are300DaylightAnd60Night()
        {
            Assert.AreEqual(300f, clock.DaylightSeconds);
            Assert.AreEqual(60f, clock.NightSeconds);
            Assert.AreEqual(360f, clock.DayLength);
            Assert.AreEqual(1, clock.Day);
            Assert.IsFalse(clock.IsNight);
        }

        [Test]
        public void NightStartsAt300_NewDayAt360()
        {
            clock.Advance(299f);
            CollectionAssert.IsEmpty(events);
            Assert.IsFalse(clock.IsNight);

            clock.Advance(1f);
            CollectionAssert.AreEqual(new[] { "night 1" }, events);
            Assert.IsTrue(clock.IsNight);
            Assert.AreEqual(1, clock.Day);

            clock.Advance(60f);
            CollectionAssert.AreEqual(new[] { "night 1", "day 2" }, events);
            Assert.IsFalse(clock.IsNight);
            Assert.AreEqual(2, clock.Day);
            Assert.AreEqual(0f, clock.TimeOfDay, 1e-4f);
        }

        [Test]
        public void BigStep_FiresEveryEventInOrder()
        {
            clock.Advance(3f * 360f); // three whole days in one go
            CollectionAssert.AreEqual(new[] { "night 1", "day 2", "night 2", "day 3", "night 3", "day 4" }, events);
            Assert.AreEqual(4, clock.Day);
        }

        [Test]
        public void DebugSpeed_RunsFourTimesFaster()
        {
            clock.SpeedMultiplier = defaults.debugClockSpeed;
            Assert.AreEqual(4f, clock.SpeedMultiplier);
            clock.Advance(75f); // 75 s × 4 = 300 s of game time
            CollectionAssert.AreEqual(new[] { "night 1" }, events);
        }

        [Test]
        public void Paused_DoesNotMove()
        {
            clock.Paused = true;
            clock.Advance(1000f);
            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(0f, clock.TimeOfDay);
            CollectionAssert.IsEmpty(events);
        }

        [Test]
        public void NoNight_OnlyFiresNewDay()
        {
            var oldClock = new GameClockLogic(240f, 0f);
            var oldEvents = new List<string>();
            oldClock.NightStarted += d => oldEvents.Add($"night {d}");
            oldClock.NewDay += d => oldEvents.Add($"day {d}");
            oldClock.Advance(480f);
            CollectionAssert.AreEqual(new[] { "day 2", "day 3" }, oldEvents);
        }

        [Test]
        public void SetTime_JumpsWithoutEvents()
        {
            clock.SetTime(5, 330f);
            Assert.AreEqual(5, clock.Day);
            Assert.IsTrue(clock.IsNight);
            CollectionAssert.IsEmpty(events);
        }

        /// <summary>
        /// Plays 10 days on the clock, turning zone 2 on each turn night exactly like
        /// Zone2Rotator does, and checks the count always matches TurnsForDay.
        /// </summary>
        [Test]
        public void ClockAndTurnSchedule_Agree()
        {
            int n = defaults.zone2DaysBetweenTurns;
            int turns = 0;
            clock.NightStarted += d =>
            {
                if (Zone2TurnMath.IsTurnNight(d, n)) turns = (turns + 1) % 4;
                Assert.AreEqual(Zone2TurnMath.TurnsForDay(d, true, n), turns, $"night of day {d}");
            };
            clock.NewDay += d => Assert.AreEqual(Zone2TurnMath.TurnsForDay(d, false, n), turns, $"dawn of day {d}");

            for (int i = 0; i < 10 * 36; i++) clock.Advance(10f); // 10 days of 360 s in 10 s steps
            Assert.AreEqual(11, clock.Day);
        }
    }
}
