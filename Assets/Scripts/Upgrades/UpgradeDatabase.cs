// ============================================================================
// UpgradeDatabase.cs
// The list of every Build item and skill (34 of them), in CSV order.
// Asset: Assets/_Project/Data/BuildAndSkills/UpgradeDatabase.asset
//
// DESIGN RULES: R7.1 to R7.4.
//
// HOW TO USE: made and filled by Tools > The Last Drop > Import Build and Skills CSV.
// Drag it into UpgradeManager's "Database" slot (the Build Playtest Scene menu does this).
// HOW TO EDIT: edit the CSVs and re-import.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Every Build item and skill.</summary>
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "The Last Drop/Upgrade Database (made by the CSV importer)")]
    public class UpgradeDatabase : ScriptableObject
    {
        [Tooltip("Every upgrade, in CSV order. Filled by the importer.")]
        public List<UpgradeData> upgrades = new List<UpgradeData>();

        /// <summary>The upgrade with this id, or null.</summary>
        public UpgradeData Get(string id) => upgrades.Find(u => u != null && u.id == id);

        /// <summary>Every upgrade as plain data, for UpgradeLogic.</summary>
        public List<UpgradeInfo> ToInfos()
        {
            var list = new List<UpgradeInfo>();
            foreach (var u in upgrades) if (u != null) list.Add(u.ToInfo());
            return list;
        }
    }
}
