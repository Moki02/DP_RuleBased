// ============================================================================
// ValveDifficulty.cs
// Works out how hard a gate's Valve Timing minigame is. Difficulty grows smoothly the
// further the gate is from the hub:
//     t     = clamp01((ring − 9) / 35)        0 at the hub wall, 1 at the edge of zone 2
//     band  = 30 − 15 × t   degrees          the green band gets narrower
//     speed = 120 + 80 × t  degrees/second   the needle gets faster
//     3 hits win, 3 misses lose.
// Zone 2's 8 exit gates use the exit valve settings instead (12°, 220°/s, 4 hits, 3 misses).
// In Demo mode GateManager passes zone 1's exits in as Zone2Exit, so they get the exit
// valve too (section 13: "Lock 1 becomes the exit: 8 L, then the exit valve").
// "ring" is the ring of the gate's tile nearest the centre (a valve is one tile).
//
// DESIGN RULES: R9.6 (difficulty by distance), R11.6 (exit valve), section 13 (Demo exit).
//
// HOW TO USE: GateManager calls ValveDifficulty.ForGate(...) when it sets up each gate.
// HOW TO EDIT: every number is in GameBalance under "Valve Timing difficulty by distance
// from the hub" and the exit valve row ([3]) of "Valve Timing".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Valve Timing difficulty for a gate, from its ring.</summary>
    public static class ValveDifficulty
    {
        /// <summary>
        /// t from 0 (easiest, at <paramref name="ringStart"/>) to 1 (hardest, <paramref name="ringSpan"/> rings further out).
        /// Rings inside the start (the hub gates, ring 7) count as 0.
        /// </summary>
        public static float T(int ring, int ringStart, int ringSpan) =>
            Mathf.Clamp01((ring - ringStart) / (float)Mathf.Max(1, ringSpan));

        /// <summary>t for a ring, using the numbers in GameBalance.</summary>
        public static float T(int ring, GameBalance b) => T(ring, b.valveDifficultyRingStart, b.valveDifficultyRingSpan);

        /// <summary>Band width (degrees) at difficulty t.</summary>
        public static float Band(float t, float bandAtHub, float bandShrink) => bandAtHub - bandShrink * t;

        /// <summary>Needle speed (degrees per second) at difficulty t.</summary>
        public static float Speed(float t, float speedAtHub, float speedGain) => speedAtHub + speedGain * t;

        /// <summary>Minigame settings for a normal gate or valve on this ring.</summary>
        public static ValveTimingSettings ForRing(int ring, GameBalance b)
        {
            float t = T(ring, b);
            return new ValveTimingSettings(
                Band(t, b.valveBandAtHub, b.valveBandShrink),
                Speed(t, b.valveSpeedAtHub, b.valveSpeedGain),
                b.valveHitsToWin,
                b.valveMissesToLose);
        }

        /// <summary>
        /// Minigame settings for a gate. Zone 2 exits get a copy of the exit valve row; every
        /// other gate uses the distance formula.
        /// </summary>
        public static ValveTimingSettings ForGate(GateKind kind, int ring, GameBalance b)
        {
            if (kind != GateKind.Zone2Exit) return ForRing(ring, b);
            var e = b.ExitValveSettings;
            return new ValveTimingSettings(e.bandWidth, e.needleSpeed, e.hitsToWin, e.missesToLose);
        }
    }
}
