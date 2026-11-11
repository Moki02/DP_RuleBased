// ============================================================================
// SaveData.Survival.cs
// The survival loop's part of the save (R11.8): the hub water, unbanked water, the
// player's HYD and the run's statistics. Same SaveData class, split over files so each
// feature adds its own fields.
//
// Written by GameResources_Manager (water), PlayerHydration_Manager (HYD, Sick) and RunStats.
// Clean Storage itself is saved by UpgradeManager in SaveData.cleanStorage.
//
// DESIGN RULES: R11.8 (saving).
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
        [Tooltip("True once the survival loop wrote its part (saves from before it: false, so these are skipped).")]
        public bool hasSurvival;
        [Tooltip("Litres in the Raw Basin.")] public float rawBasin;
        [Tooltip("Litres of unbanked water (earned in the maze, not yet home).")] public float unbanked;
        [Tooltip("Litres in the spare jugs by the hub door (R3.5).")] public float spareJugs;
        [Tooltip("The player's HYD (loaded as at least 1).")] public float hydration = 100f;
        [Tooltip("Seconds of Sick left.")] public float sickSeconds;

        [Header("Run statistics")]
        [Tooltip("Collapses so far (end screen, R11.7).")] public int collapses;
        [Tooltip("Total litres that entered the game (end screen, R11.7).")] public float litersCollected;
    }
}
