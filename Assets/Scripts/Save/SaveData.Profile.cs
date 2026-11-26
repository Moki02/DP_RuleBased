// ============================================================================
// SaveData.Profile.cs
// The player's name in the save, so Continue shows the same name (PlayerProfile).
// Same SaveData class, split over files so each feature adds its own fields.
//
// Written and read by SaveManager (its Capture / Restore).
//
// DESIGN RULES: R11.8 (saving).
//
// HOW TO EDIT: old saves without the field load with "" (PlayerProfile keeps its name then).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Profile")]
        [Tooltip("The name typed on the Name Entry screen for this run.")]
        public string playerName = "";
    }
}
