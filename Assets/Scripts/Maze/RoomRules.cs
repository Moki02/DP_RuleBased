// ============================================================================
// RoomRules.cs
// The room rules as plain maths (no MonoBehaviour), so they can be tested: each room's
// distance d from the hub, its fixed-seed roll (empty / cistern / cache) and how many
// litres it holds.
//
// DESIGN RULES:
//   R8.12 Each room's outcome is rolled once with a fixed seed, so every playthrough is the
//         same. The designer can override any room (GameBalance.roomOutcomeOverride).
//   R8.13 d = 0 for the room nearest the hub, 1 for the farthest, by ring distance (so zone 2
//         turning never changes it). Odds: empty 20% always; cistern 16% at d = 0, 40% at
//         d = 0.5, 64% at d = 1 (a straight line, so we interpolate); cache is the rest.
//   R8.14 Cistern: 2 + 6 × d L, refilled at dawn × stage factor (1.0, 0.85, 0.7, 0.5).
//   R8.15 Cache: 3 + 7 × d L, taken once.
//
// HOW TO USE: RoomContent calls these. You don't put this on an object.
// HOW TO EDIT: the numbers are in GameBalance ("Maze content: rooms").
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>What a room holds (R8.12). Saved as a number, so new values go at the END.</summary>
    public enum RoomOutcome
    {
        /// <summary>Dead survivors and a note, no water (R8.16).</summary>
        Empty,
        /// <summary>A tank that refills at dawn (R8.14).</summary>
        Cistern,
        /// <summary>A sealed jug, taken once (R8.15, R8.5).</summary>
        Cache
    }

    /// <summary>The designer's hand override for one room (R8.12).</summary>
    public enum RoomOutcomeOverride
    {
        /// <summary>No override: use the fixed-seed roll.</summary>
        Rolled,
        /// <summary>Always empty.</summary>
        Empty,
        /// <summary>Always a cistern.</summary>
        Cistern,
        /// <summary>Always a cache.</summary>
        Cache
    }

    /// <summary>Room maths: distance, roll and litres.</summary>
    public static class RoomRules
    {
        /// <summary>
        /// A number from 0 (included) to 1 (not included) that depends only on the seed and the
        /// room number. Same inputs, same answer, on every computer (unlike UnityEngine.Random).
        /// </summary>
        public static float Hash01(int seed, int index)
        {
            unchecked
            {
                // A small integer "hash": mixes the bits so that nearby seeds give unrelated numbers.
                uint h = (uint)seed * 2654435761u ^ (uint)(index + 1) * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;   // keep 24 bits, which a float holds exactly
            }
        }

        /// <summary>R8.13: d from 0 (ring nearest the hub of all rooms) to 1 (farthest). 0 if every room is on one ring.</summary>
        public static float Distance01(int ring, int nearestRing, int farthestRing) =>
            farthestRing <= nearestRing ? 0f : Mathf.Clamp01((float)(ring - nearestRing) / (farthestRing - nearestRing));

        /// <summary>R8.13: chance of a cistern at distance d (a straight line from near to far).</summary>
        public static float CisternChance(float d, float nearChance, float farChance) =>
            Mathf.Lerp(nearChance, farChance, Mathf.Clamp01(d));

        /// <summary>R8.12 / R8.13: rolls one room with the fixed seed.</summary>
        public static RoomOutcome Roll(int seed, int roomIndex, float d, float emptyChance, float cisternNear, float cisternFar)
        {
            float r = Hash01(seed, roomIndex);
            if (r < emptyChance) return RoomOutcome.Empty;
            if (r < emptyChance + CisternChance(d, cisternNear, cisternFar)) return RoomOutcome.Cistern;
            return RoomOutcome.Cache;
        }

        /// <summary>R8.12: the roll, unless the designer overrode this room.</summary>
        public static RoomOutcome Decide(GameBalance b, int roomIndex, float d)
        {
            switch (b.GetRoomOverride(roomIndex))
            {
                case RoomOutcomeOverride.Empty: return RoomOutcome.Empty;
                case RoomOutcomeOverride.Cistern: return RoomOutcome.Cistern;
                case RoomOutcomeOverride.Cache: return RoomOutcome.Cache;
                default:
                    return Roll(b.roomSeed, roomIndex, d, b.roomEmptyChance, b.roomCisternChanceNear, b.roomCisternChanceFar);
            }
        }

        /// <summary>R8.14 / R8.15: how many litres the room holds when full (0 for an empty room).</summary>
        public static float Capacity(GameBalance b, RoomOutcome outcome, float d)
        {
            switch (outcome)
            {
                case RoomOutcome.Cistern: return b.cisternBaseLiters + b.cisternLitersPerD * d;
                case RoomOutcome.Cache: return b.cacheBaseLiters + b.cacheLitersPerD * d;
                default: return 0f;
            }
        }

        /// <summary>R8.14: the share a cistern refills to at dawn in this drought stage (1 if not set).</summary>
        public static float RefillFactor(GameBalance b, int stageIndex)
        {
            var f = b.cisternRefillByStage;
            if (f == null || f.Length == 0) return 1f;
            return f[Mathf.Clamp(stageIndex, 0, f.Length - 1)];
        }
    }
}
