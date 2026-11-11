// ============================================================================
// SaveData.MazeContent.cs
// The maze content's part of the save (R11.8): which map sections and milestones have
// paid (so they never pay twice, R8.28), and each room's outcome, water left, whether it
// was found and whether its note was read. Same SaveData class, split over files so each
// feature adds its own fields.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Map rewards (R8.2, R8.3)")]
        [Tooltip("True once MapRewards has written its part (older saves don't have it).")]
        public bool hasMapRewards;
        [Tooltip("Section numbers (MazeSections) that already paid their reward.")]
        public List<int> sectionsMapped = new List<int>();
        [Tooltip("Milestone numbers (0 = 25%, 1 = 50% ...) that already paid.")]
        public List<int> milestonesPaid = new List<int>();

        [Header("Rooms (R8.12 to R8.18)")]
        [Tooltip("True once RoomContent has written its part (older saves don't have it).")]
        public bool hasRooms;
        [Tooltip("Each room's outcome by room number: 0 empty, 1 cistern, 2 cache (R8.12: rolled once and saved).")]
        public List<int> roomOutcomes = new List<int>();
        [Tooltip("Litres left in each room's cistern or cache, by room number.")]
        public List<float> roomLitres = new List<float>();
        [Tooltip("Room numbers the player has walked into (R8.18).")]
        public List<int> roomsFound = new List<int>();
        [Tooltip("Room numbers whose note was read, in the order they were read (the journal, R8.7).")]
        public List<int> notesRead = new List<int>();
        [Tooltip("The day the cisterns last refilled (R8.14), so a dawn save never refills twice or misses one.")]
        public int cisternRefillDay;
    }
}
