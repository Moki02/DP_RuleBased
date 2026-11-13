// ============================================================================
// SaveData.Doors.cs
// The room doors' part of the save: which doors the player has unlocked with the minigame
// (they stay open for good). Same SaveData class, split over files so each feature adds
// its own fields.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults (all shut).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Room doors")]
        [Tooltip("Door numbers (RoomDoorLayout order) unlocked with the minigame.")]
        public List<int> doorsOpened = new List<int>();
    }
}
