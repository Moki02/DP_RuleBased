// ============================================================================
// DayNightMath.cs
// The day/night cycle's maths as plain C# (no MonoBehaviour), so it can be tested:
//   - SkyTime: turns the clock's time of day into a 0-1 "sky time" where daylight is
//     always 0 to 0.8 and the night lockdown 0.8 to 1, whatever the real lengths are
//     (so the colour gradients stay right after a retune of GameBalance);
//   - SunDirection: where the sun (by day) or the moon (by night) is in the sky.
//     The sun rises in the east (+X), is highest in the south at noon and sets in the west;
//     the moon does the same during the night, a bit lower. North is +Z (like the compass).
//
// DESIGN RULES: R1.5 (240 s daylight), R8.19 (60 s night lockdown). Look: pending GDD entry
// (sky dome day/night, 2026-11-13).
//
// HOW TO USE: DayNightCycle calls these. You don't put this on an object.
// HOW TO EDIT: the heights are DayNightCycle's "Sun Max Height" / "Moon Max Height".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Sky time and sun/moon position for the day/night cycle.</summary>
    public static class DayNightMath
    {
        /// <summary>Sky time where daylight ends and the night lockdown starts.</summary>
        public const float DuskPoint = 0.8f;

        /// <summary>
        /// 0-1 sky time from the clock: 0 = dawn, 0.4 = noon, 0.8 = start of the night lockdown,
        /// 1 = the next dawn. With no night (old scenes) daylight fills 0 to 0.8 and stays there.
        /// </summary>
        public static float SkyTime(float timeOfDay, float daylightSeconds, float nightSeconds)
        {
            if (daylightSeconds <= 0f) return 0.4f;
            if (timeOfDay < daylightSeconds || nightSeconds <= 0f)
                return Mathf.Clamp01(timeOfDay / daylightSeconds) * DuskPoint;
            float n = Mathf.Clamp01((timeOfDay - daylightSeconds) / nightSeconds);
            return DuskPoint + n * (1f - DuskPoint);
        }

        /// <summary>True while the moon is the light (the night lockdown part of the sky time).</summary>
        public static bool IsMoon(float skyTime) => skyTime >= DuskPoint && skyTime < 1f;

        /// <summary>
        /// Unit vector pointing from the ground TO the sun (by day) or the moon (by night).
        /// Heights are in degrees above the horizon; <paramref name="lowest"/> keeps it from
        /// lying flat on the horizon (very long shadows).
        /// </summary>
        public static Vector3 SunDirection(float skyTime, float sunMaxHeight, float moonMaxHeight, float lowest)
        {
            bool moon = IsMoon(skyTime);
            float f = moon
                ? (skyTime - DuskPoint) / (1f - DuskPoint)       // 0-1 across the night
                : Mathf.Repeat(skyTime, 1f) / DuskPoint;          // 0-1 across daylight
            f = Mathf.Clamp01(f);
            float top = moon ? moonMaxHeight : sunMaxHeight;
            float height = Mathf.Lerp(lowest, Mathf.Max(lowest, top), Mathf.Sin(f * Mathf.PI)) * Mathf.Deg2Rad;
            float bearing = (90f + 180f * f) * Mathf.Deg2Rad;     // east (90) -> south (180) -> west (270)
            return new Vector3(Mathf.Sin(bearing) * Mathf.Cos(height), Mathf.Sin(height), Mathf.Cos(bearing) * Mathf.Cos(height));
        }
    }
}
