// ============================================================================
// GameBalance.MazeContent.cs
// More GameBalance values: what is inside the maze. Map sections and their rewards, the
// rooms (empty / cistern / cache) and how fast you fill the canteen from them.
// This is the same GameBalance asset; the class is split over several files ("partial")
// so different features can add their numbers without editing the same file.
//
// Already in GameBalance.cs (not repeated here): sectionMappedPct (75%), sectionReward
// (2 / 3 / 4 L), milestonePercents and milestoneRewards (25/50/75/100% -> 10/15/20/30 L).
//
// DESIGN RULES: R8.2 (sections), R8.3 (milestones), R8.12 to R8.16 (rooms).
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Maze content".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Maze content: sections (R8.2)")]
        [Tooltip("How many map sections zone 1 is cut into, like slices of a cake around the centre (GDD: 6, A to F).")]
        [Min(1)] public int zone1SectionCount = 6;

        [Tooltip("How many map sections zone 2 is cut into, like slices of a cake around the centre (GDD: 8, G to N).")]
        [Min(1)] public int zone2SectionCount = 8;

        [Header("Maze content: rooms (R8.12 to R8.16)")]
        [Tooltip("Fixed seed for the room roll (R8.12). Same seed = same rooms every playthrough. " +
                 "Seed 8 gives, by room number 0-6: cistern, cistern, cache, cistern, cache, cistern, empty.")]
        public int roomSeed = 8;

        [Tooltip("Chance (0-1) that a room has no water, at any distance (R8.13: 20%).")]
        [Range(0f, 1f)] public float roomEmptyChance = 0.2f;

        [Tooltip("Chance (0-1) of a cistern in the room nearest the hub, d = 0 (R8.13: 16%). The rest is a cache.")]
        [Range(0f, 1f)] public float roomCisternChanceNear = 0.16f;

        [Tooltip("Chance (0-1) of a cistern in the farthest room, d = 1 (R8.13: 64%). Rooms in between are interpolated " +
                 "(d = 0.5 gives 40%). The rest is a cache.")]
        [Range(0f, 1f)] public float roomCisternChanceFar = 0.64f;

        [Tooltip("Cistern size in litres at d = 0 (R8.14: 2 + 6 × d).")]
        [Min(0f)] public float cisternBaseLiters = 2f;

        [Tooltip("Extra cistern litres per unit of d (R8.14: 2 + 6 × d).")]
        [Min(0f)] public float cisternLitersPerD = 6f;

        [Tooltip("Cistern refill at dawn, as a share of its size, per drought stage (R8.14: ×1.0, 0.85, 0.7, 0.5).")]
        public float[] cisternRefillByStage = { 1f, 0.85f, 0.7f, 0.5f };

        [Tooltip("Cache (sealed jug) size in litres at d = 0 (R8.15: 3 + 7 × d).")]
        [Min(0f)] public float cacheBaseLiters = 3f;

        [Tooltip("Extra cache litres per unit of d (R8.15: 3 + 7 × d).")]
        [Min(0f)] public float cacheLitersPerD = 7f;

        [Tooltip("Litres per second that go into the canteen while E is held on a cistern or cache (×1.5 with M4 Quick Hands).")]
        [Min(0f)] public float roomFillRate = 1f;      // L/s

        [Tooltip("The designer's hand overrides (R8.12), by room number (0 = top-left room of the sheet, then left to right, " +
                 "top to bottom). Rolled = use the seed. Leave empty to roll every room.")]
        public RoomOutcomeOverride[] roomOutcomeOverride = new RoomOutcomeOverride[0];

        /// <summary>The designer's override for a room, or Rolled if there is none.</summary>
        public RoomOutcomeOverride GetRoomOverride(int roomIndex) =>
            roomOutcomeOverride != null && roomIndex >= 0 && roomIndex < roomOutcomeOverride.Length
                ? roomOutcomeOverride[roomIndex]
                : RoomOutcomeOverride.Rolled;
    }
}
