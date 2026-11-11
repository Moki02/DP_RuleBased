// ============================================================================
// UpgradeData.cs
// One Build item or skill as an asset you can see in the Project window
// (Assets/_Project/Data/BuildAndSkills/Upgrades/W1.asset and so on).
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: don't make these by hand. Edit the CSVs and run
// Tools > The Last Drop > Import Build and Skills CSV; it creates or updates one asset per row.
// HOW TO EDIT: change the CSV and re-import. Changes made here by hand are overwritten
// by the next import.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>One Build item or skill (made by the CSV importer).</summary>
    [CreateAssetMenu(fileName = "Upgrade", menuName = "The Last Drop/Upgrade (made by the CSV importer)")]
    public class UpgradeData : ScriptableObject
    {
        [Tooltip("Short id, e.g. W1. Other upgrades list it in their Needs.")]
        public string id;
        [Tooltip("Name players see.")]
        public string displayName;
        [Tooltip("Build page or Skills page.")]
        public UpgradeTab tab;
        [Tooltip("Column on the Build page (Water, Carry, Hub storage, Purification) or branch on the Skills page (Endurance, Pathfinder, Scout).")]
        public string group;
        [Tooltip("Price in litres of Clean Storage.")]
        [Min(0f)] public float costLiters;
        [Tooltip("Ids that must be owned before this can be bought.")]
        public string[] prerequisiteIds = new string[0];
        [Tooltip("Team only, never shown to players: Must ship, or Nice to have.")]
        public UpgradePriority priority;
        [Tooltip("The effect text players read on the card's detail panel.")]
        [TextArea(2, 5)] public string tooltip;
        [Tooltip("What it changes: a field name and value each (from BuildAndSkills_Effects.csv).")]
        public List<UpgradeEffect> effects = new List<UpgradeEffect>();

        /// <summary>This asset as plain data for UpgradeLogic.</summary>
        public UpgradeInfo ToInfo() => new UpgradeInfo
        {
            Id = id,
            Name = displayName,
            Tab = tab,
            Group = group,
            Cost = costLiters,
            Needs = prerequisiteIds ?? new string[0],
            Priority = priority,
            Tooltip = tooltip,
            Effects = new List<UpgradeEffect>(effects),
        };

        /// <summary>Copies everything from plain data (used by the importer).</summary>
        public void CopyFrom(UpgradeInfo info)
        {
            id = info.Id;
            displayName = info.Name;
            tab = info.Tab;
            group = info.Group;
            costLiters = info.Cost;
            prerequisiteIds = info.Needs;
            priority = info.Priority;
            tooltip = info.Tooltip;
            effects = new List<UpgradeEffect>(info.Effects);
        }
    }
}
