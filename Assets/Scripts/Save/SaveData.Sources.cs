// ============================================================================
// SaveData.Sources.cs
// The water sources' part of the save (R11.8): the day the Biosand Filter was bought, so
// it keeps ripening at the right speed after a load (R6.1: 50% / 75% / 100%).
// The sources themselves need nothing saved: they come back from the owned Build items.
// Same SaveData class, split over files so each feature adds its own fields.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Water sources")]
        [Tooltip("True if the save was written by a game that has the water sources (old saves: false).")]
        public bool hasSources;
        [Tooltip("Day the Biosand Filter (P1) was bought. 0 = not owned.")]
        public int biosandPurchaseDay;
    }
}
