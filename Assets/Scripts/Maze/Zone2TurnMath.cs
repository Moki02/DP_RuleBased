// ============================================================================
// Zone2TurnMath.cs
// The sums behind zone 2's turning, as plain C# so they can be tested:
//   - how many times zone 2 has turned on a given day (day or night),
//   - which nights it turns,
//   - where a point in the world ends up when zone 2 turns around its pivot.
//
// DESIGN RULES: R8.24 (zone 2 turns 90 degrees clockwise at the start of the night
// lockdown of days 2, 4, 6 ...), R8.26 (a player inside zone 2 turns with it).
//
// HOW TO USE: Zone2Rotator calls these. You don't put this on an object.
// HOW TO EDIT: "Zone 2 Days Between Turns" in GameBalance (2 by default).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Sums for zone 2's turns: which day, how many turns, and where things move to.</summary>
    public static class Zone2TurnMath
    {
        /// <summary>
        /// True if zone 2 turns at the start of this day's night.
        /// With 2 days between turns: days 2, 4, 6 ...
        /// </summary>
        public static bool IsTurnNight(int day, int daysBetweenTurns)
        {
            int n = Mathf.Max(1, daysBetweenTurns);
            return day >= 1 && day % n == 0;
        }

        /// <summary>
        /// How many quarter turns zone 2 has made (0 to 3) on <paramref name="day"/>, during the
        /// day or during that day's night.
        /// </summary>
        /// <remarks>
        /// With 2 days between turns: days 1-2 are at 0 turns, the night of day 2 turns to 1,
        /// days 3-4 stay at 1, the night of day 4 turns to 2 ... day 9 is back at 0.
        /// Formula: floor((day - 1) / 2), plus 1 at night on a turn night, then wrap to 0..3.
        /// </remarks>
        public static int TurnsForDay(int day, bool night, int daysBetweenTurns)
        {
            int n = Mathf.Max(1, daysBetweenTurns);
            int d = Mathf.Max(1, day);
            int turns = (d - 1) / n;                        // turns made on the nights BEFORE today
            if (night && IsTurnNight(d, n)) turns += 1;     // tonight's turn has happened too
            return turns % 4;
        }

        /// <summary>
        /// Turns a world point around the pivot, clockwise seen from above, by
        /// <paramref name="quarterTurns"/> × 90 degrees (the same as +90 degrees on Y per turn).
        /// Height (Y) doesn't change.
        /// </summary>
        public static Vector3 RotateAroundPivot(Vector3 point, Vector3 pivot, int quarterTurns)
        {
            // Work with the offset from the pivot. Turning +90 degrees on Y sends +Z (north) to
            // +X (east): (x, z) becomes (z, -x). Swapping like this is exact (no sin/cos rounding),
            // so a tile centre always lands exactly on another tile centre.
            float dx = point.x - pivot.x, dz = point.z - pivot.z;
            int k = ((quarterTurns % 4) + 4) % 4;
            for (int i = 0; i < k; i++)
            {
                float oldX = dx;
                dx = dz;
                dz = -oldX;
            }
            return new Vector3(pivot.x + dx, point.y, pivot.z + dz);
        }
    }
}
