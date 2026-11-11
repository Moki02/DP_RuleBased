// ============================================================================
// SaveData.cs
// Everything in one save, as plain data that Unity's JsonUtility turns into JSON text.
// Each system fills in its own part (see ISaveable), so adding a system later (for
// example the water systems) only means adding fields here and one ISaveable.
//
// DESIGN RULES: R11.8 (saving).
//
// HOW TO USE: SaveManager makes one, asks every ISaveable to fill it, and writes it.
// HOW TO EDIT: if you add, remove or rename a field in a way old saves can't read, raise
// SaveFile.CurrentVersion so old files are rejected cleanly instead of loading wrong.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>One save file's contents.</summary>
    [Serializable]
    public class SaveData
    {
        [Tooltip("Save format number. Files with another number are rejected.")]
        public int version;
        [Tooltip("When it was saved (for people reading the file).")]
        public string savedAt;

        [Header("Clock and zone 2")]
        public int day = 1;
        [Tooltip("Seconds since dawn. 0 for the dawn autosave.")]
        public float timeOfDay;
        [Tooltip("Zone 2's turns when saved. Worked out again from the day on load; kept as a check.")]
        public int zone2Turns;

        [Header("Water (playtest: only numbers)")]
        public float cleanStorage;
        public float canteen;

        [Header("Build and Skills")]
        [Tooltip("Owned ids in the order they were bought.")]
        public List<string> ownedUpgrades = new List<string>();

        [Header("Gates")]
        [Tooltip("One entry per gate: litres poured and whether it was opened.")]
        public List<GateSave> gates = new List<GateSave>();
        [Tooltip("Index of every valve gate (MazeGrid.Valves) that was won.")]
        public List<int> valvesWon = new List<int>();

        [Header("Map")]
        public int mapWidth;
        public int mapHeight;
        [Tooltip("Revealed tiles, one bit per tile by CSV (0 degree) position, packed into text.")]
        public string revealedTiles = "";
        [Tooltip("Markers by CSV (0 degree) tile.")]
        public List<Vector2Int> markers = new List<Vector2Int>();

        [Header("Menu memory")]
        public string menuLastPage = nameof(MenuPage.Build);
        public string menuLastBuildCard = "";
        public string menuLastSkillsCard = "";

        [Header("Player")]
        public bool hasPlayer;
        public Vector3 playerPosition;
        [Tooltip("Facing, degrees clockwise from north.")]
        public float playerYaw;
    }

    /// <summary>One gate's saved state.</summary>
    [Serializable]
    public class GateSave
    {
        [Tooltip("The gate's index in MazeGrid.Gates.")] public int index;
        [Tooltip("Litres poured into its basin.")] public float litres;
        [Tooltip("True if its valve minigame was won.")] public bool open;
    }

    /// <summary>A system that saves and loads its own part of SaveData.</summary>
    public interface ISaveable
    {
        /// <summary>Lower numbers are restored first (the clock before zone 2, zone 2 before the player).</summary>
        int RestoreOrder { get; }

        /// <summary>Writes this system's part into the save.</summary>
        void Capture(SaveData data);

        /// <summary>Puts this system back the way the save says.</summary>
        void Restore(SaveData data);
    }

    /// <summary>The RestoreOrder of each system, in one place.</summary>
    public static class RestoreOrders
    {
        /// <summary>Day and time first: other systems depend on it.</summary>
        public const int Clock = 0;
        /// <summary>Night lockdown follows the clock.</summary>
        public const int Lockdown = 10;
        /// <summary>Zone 2's turn follows the day.</summary>
        public const int Zone2 = 20;
        /// <summary>Build and Skills, Clean Storage.</summary>
        public const int Upgrades = 30;
        /// <summary>Canteen.</summary>
        public const int Resources = 35;
        /// <summary>Gates and valves.</summary>
        public const int Gates = 40;
        /// <summary>Map knowledge and markers.</summary>
        public const int Map = 50;
        /// <summary>Menu memory.</summary>
        public const int Menu = 60;
        /// <summary>The player last, once zone 2 is in place.</summary>
        public const int Player = 100;
    }
}
