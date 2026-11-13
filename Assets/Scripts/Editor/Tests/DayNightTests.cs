// ============================================================================
// DayNightTests.cs   (EditMode tests)
// Checks the day/night maths: the clock's 240 s daylight + 60 s night map to sky time
// 0-0.8 and 0.8-1; the sun rises in the east (+X), is highest in the south at noon and
// sets in the west; the moon is above the horizon all night.
//
// DESIGN RULES: R1.5, R8.19. Look: pending GDD entry (sky dome day/night, 2026-11-13).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for DayNightMath.</summary>
    public class DayNightTests
    {
        const float Day = 240f, Night = 60f;

        [Test]
        public void SkyTime_DaylightIs0To08_NightIs08To1()
        {
            Assert.AreEqual(0f, DayNightMath.SkyTime(0f, Day, Night), 0.0001f, "dawn");
            Assert.AreEqual(0.4f, DayNightMath.SkyTime(120f, Day, Night), 0.0001f, "noon");
            Assert.AreEqual(0.8f, DayNightMath.SkyTime(240f, Day, Night), 0.0001f, "lockdown starts");
            Assert.AreEqual(0.9f, DayNightMath.SkyTime(270f, Day, Night), 0.0001f, "middle of the night");
            Assert.AreEqual(1f, DayNightMath.SkyTime(300f, Day, Night), 0.0001f, "next dawn");
        }

        [Test]
        public void SkyTime_SameShape_WhenLengthsChange()
        {
            Assert.AreEqual(0.4f, DayNightMath.SkyTime(60f, 120f, 30f), 0.0001f);
            Assert.AreEqual(0.9f, DayNightMath.SkyTime(135f, 120f, 30f), 0.0001f);
            Assert.AreEqual(0.4f, DayNightMath.SkyTime(10f, 0f, 0f), 0.0001f, "no clock lengths: noon");
        }

        [Test]
        public void Sun_RisesEast_HighestSouthAtNoon_SetsWest()
        {
            var rise = DayNightMath.SunDirection(0.001f, 65f, 45f, 8f);
            var noon = DayNightMath.SunDirection(0.4f, 65f, 45f, 8f);
            var set = DayNightMath.SunDirection(0.799f, 65f, 45f, 8f);
            Assert.Greater(rise.x, 0.9f, "sunrise in the east (+X)");
            Assert.Less(set.x, -0.9f, "sunset in the west (-X)");
            Assert.Less(noon.z, 0f, "noon sun in the south (-Z)");
            Assert.AreEqual(Mathf.Sin(65f * Mathf.Deg2Rad), noon.y, 0.001f, "noon height 65 degrees");
            Assert.Greater(noon.y, rise.y);
            Assert.Greater(rise.y, 0f, "never below the horizon");
        }

        [Test]
        public void Moon_AboveHorizon_AllNight()
        {
            for (float t = 0.8f; t < 1f; t += 0.01f)
            {
                Assert.IsTrue(DayNightMath.IsMoon(t));
                Assert.Greater(DayNightMath.SunDirection(t, 65f, 45f, 8f).y, 0.1f, $"moon at {t}");
            }
            Assert.IsFalse(DayNightMath.IsMoon(0.5f));
            Assert.IsFalse(DayNightMath.IsMoon(1f), "1 = next dawn = sun");
        }

        [Test]
        public void Direction_IsAlwaysUnitLength()
        {
            for (float t = 0f; t <= 1f; t += 0.05f)
                Assert.AreEqual(1f, DayNightMath.SunDirection(t, 65f, 45f, 8f).magnitude, 0.0001f);
        }
    }
}
