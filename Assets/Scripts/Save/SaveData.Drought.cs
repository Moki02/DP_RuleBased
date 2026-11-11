// ============================================================================
// SaveData.Drought.cs
// The drought's part of the save (R11.8): a stage forced ahead with debug key 8, so the
// jump survives F5 / F9. The stage itself comes from the saved day, so it isn't stored.
// Same SaveData class, split over files so each feature adds its own fields.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        /// <summary>True once the drought has written its part (old saves: false).</summary>
        [Header("Drought")]
        public bool hasDrought;

        /// <summary>WorldState.StageOverride when saved (-1 = none, the stage follows the day).</summary>
        public int stageOverride = -1;
    }
}
