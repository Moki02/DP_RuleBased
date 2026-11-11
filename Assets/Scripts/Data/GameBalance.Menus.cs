// ============================================================================
// GameBalance.Menus.cs
// More GameBalance values: the ending screen (which title the player gets for the day
// they escaped). Same GameBalance asset; the class is split over several files
// ("partial") so different features can add their numbers without editing one file.
//
// DESIGN RULES: R11.6, R11.7 and the ending titles table (section 11), R13.4.
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Ending screen".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Ending screen (R11.7)")]
        [Tooltip("Escape on this day or earlier = \"Water Keeper\" (day number, ending titles table).")]
        [Min(1)] public int endingWaterKeeperMaxDay = 15;

        [Tooltip("Escape on this day or earlier (and after the Water Keeper day) = \"Survivor\". Later = \"Barely Made It\" (day number).")]
        [Min(1)] public int endingSurvivorMaxDay = 22;

        /// <summary>The ending title for escaping on <paramref name="day"/> ("Demo complete" in Demo mode, R13.4).</summary>
        public string GetEndingTitle(int day)
        {
            if (isDemoMode) return "Demo complete";
            if (day <= endingWaterKeeperMaxDay) return "Water Keeper";
            if (day <= endingSurvivorMaxDay) return "Survivor";
            return "Barely Made It";
        }
    }
}
