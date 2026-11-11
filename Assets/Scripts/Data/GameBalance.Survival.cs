// ============================================================================
// GameBalance.Survival.cs
// More GameBalance values: the survival loop (hydration on, banking, the night drain).
// This is the same GameBalance asset; the class is split over several files ("partial")
// so different features can add their numbers without editing the same file.
//
// DESIGN RULES: R3.5 (banking, Safe Return, spare jugs), R8.2a (water per new tile),
// R8.20 and R8.21 (lockdown warnings and drain).
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Survival loop".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Survival loop (hydration, banking, night)")]
        [Tooltip("ON: hydration drains, the canteen runs out, and collapse costs water (the real game). " +
                 "OFF: the old maze playtest (no drain, endless canteen).")]
        public bool survivalOn = true;

        [Tooltip("HYD lost per second outside the hub and every room during the night lockdown (R8.21).")]
        [Min(0f)] public float lockdownDrain = 4f;

        [Tooltip("Seconds before the lockdown when a warning plays (R8.20).")]
        public float[] lockdownWarnings = { 60f, 40f, 20f, 10f, 5f };

        [Tooltip("Litres of unbanked water for each newly revealed floor or room tile (R8.2a).")]
        [Min(0f)] public float tileReward = 0.03f;

        [Tooltip("Safe Return bonus: share of the unbanked water you bring home before the lockdown (R3.5). 0.1 = +10%.")]
        [Range(0f, 1f)] public float safeReturnBonus = 0.1f;

        [Tooltip("Litres per second spare jugs pour into Clean Storage when it has room (R3.5).")]
        [Min(0f)] public float spareJugPourRate = 1f;

        [Tooltip("Seconds the screen takes to fade to black on a collapse (R11.1).")]
        [Min(0f)] public float collapseFadeSeconds = 2f;
    }
}
