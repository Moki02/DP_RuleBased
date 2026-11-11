// ============================================================================
// UpgradeInfo.cs
// The plain data of one Build item or skill: id, name, page (Build or Skills), group,
// cost, what it needs first, priority, tooltip and its effects. Plain C# so the rules
// can be tested straight from the CSV. The UpgradeData asset (what you see in the
// Project window) turns into one of these with ToInfo().
//
// DESIGN RULES: R7.1 to R7.4 (Build and Skills).
//
// HOW TO USE: nothing to set up. Made by UpgradeCsv (from the CSV) or UpgradeData.ToInfo().
// HOW TO EDIT: change prices, needs and effects in
// Assets/_Project/Data/BuildAndSkills/BuildAndSkills.csv and ..._Effects.csv, then run
// Tools > The Last Drop > Import Build and Skills CSV.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Which page of the menu an upgrade is on.</summary>
    public enum UpgradeTab
    {
        /// <summary>Things you build at the hub (water, carry, storage, purification).</summary>
        Build,
        /// <summary>Things you learn (endurance, pathfinder, scout).</summary>
        Skills
    }

    /// <summary>Team-only label (players never see it): must ship, or nice to have.</summary>
    public enum UpgradePriority
    {
        /// <summary>Must be in the game.</summary>
        Must,
        /// <summary>Nice to have.</summary>
        Nice
    }

    /// <summary>One effect of an upgrade: a field name and its new value, e.g. walkSpeed = 4.4.</summary>
    [Serializable]
    public class UpgradeEffect
    {
        [Tooltip("The name game code looks up, e.g. walkSpeed, lookAheadTiles, cleanStorageCap.")]
        public string field;
        [Tooltip("The new value as text: a number (4.4), true/false, or a name (RainCatcher).")]
        public string value;
        [Tooltip("Note for the team (units, what it was before). Not shown to players.")]
        public string note;

        /// <summary>An empty effect (Unity needs this to load saved assets).</summary>
        public UpgradeEffect() { }

        /// <summary>Makes an effect.</summary>
        public UpgradeEffect(string field, string value, string note)
        {
            this.field = field;
            this.value = value;
            this.note = note;
        }
    }

    /// <summary>One Build item or skill as plain data.</summary>
    public class UpgradeInfo
    {
        /// <summary>Short id, e.g. "W1".</summary>
        public string Id;
        /// <summary>Name players see, e.g. "Greased Handle".</summary>
        public string Name;
        /// <summary>Build or Skills page.</summary>
        public UpgradeTab Tab;
        /// <summary>Column (Build) or branch (Skills), e.g. "Water", "Scout".</summary>
        public string Group;
        /// <summary>Cost in litres of Clean Storage, before the Demo mode multiplier.</summary>
        public float Cost;
        /// <summary>Ids that must be owned first.</summary>
        public string[] Needs = Array.Empty<string>();
        /// <summary>Team-only priority.</summary>
        public UpgradePriority Priority;
        /// <summary>The effect text players read.</summary>
        public string Tooltip;
        /// <summary>What it changes.</summary>
        public List<UpgradeEffect> Effects = new List<UpgradeEffect>();
    }
}
