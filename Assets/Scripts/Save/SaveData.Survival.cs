// ============================================================================
// SaveData.Survival.cs
// The survival loop's part of the save (R11.8): the hub water, unbanked water, the
// player's HYD and the run's statistics. Same SaveData class, split over files so each
// feature adds its own fields.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Survival")]
        public bool hasSurvival;
        public float rawBasin;
        public float unbanked;
        public float spareJugs;
        public float hydration = 100f;
        public float sickSeconds;

        [Header("Run statistics")]
        public int collapses;
        public float litersCollected;
    }
}
