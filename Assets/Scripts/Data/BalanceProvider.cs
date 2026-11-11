// ============================================================================
// BalanceProvider.cs
// Holds the scene's GameBalance asset in one place, so every system in the scene reads
// the same numbers. Other scripts use BalanceProvider.Current instead of each keeping
// their own reference.
//
// DESIGN RULES: none of its own; it only points at GameBalance (GDD section 12).
// Demo mode (section 13, R13.1): the main menu's Demo button sets UseDemo and reloads the
// scene; this script then hands out "Demo Balance" instead of "Balance".
//
// HOW TO USE: put ONE of these on the "Systems" object and drag GameBalance into
// "Balance" and GameBalance_Demo into "Demo Balance". The Build Playtest Scene menu does this.
// HOW TO EDIT: swap the asset in the slot to switch between the full game and Demo mode.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Gives every script in the scene the same GameBalance asset.</summary>
    [DefaultExecutionOrder(-1000)] // set Current before any other script's Awake reads it
    public class BalanceProvider : MonoBehaviour
    {
        [Tooltip("The GameBalance asset this scene uses (GameBalance for the full game, GameBalance_Demo for Demo mode).")]
        public GameBalance balance;

        [Tooltip("The Demo mode asset (GameBalance_Demo), used when the main menu's Demo button is pressed (R13.1).")]
        public GameBalance demoBalance;

        /// <summary>The GameBalance of the active scene, or null if the scene has no BalanceProvider.</summary>
        public static GameBalance Current { get; private set; }

        /// <summary>
        /// True = the next scenes use "Demo Balance" (set by the main menu's Demo button, R13.1).
        /// Stays on through Restart; the main menu turns it off.
        /// </summary>
        public static bool UseDemo;

        // Statics survive Play mode in the editor when domain reload is off: start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => UseDemo = false;

        void Awake()
        {
            if (balance == null)
                Debug.LogError("[BalanceProvider] No GameBalance asset assigned.", this);
            GameBalance chosen = balance;
            if (UseDemo)
            {
                if (demoBalance != null) chosen = demoBalance;
                else Debug.LogWarning("[BalanceProvider] Demo mode was picked but \"Demo Balance\" is empty " +
                                      "(run Build Playtest Scene again). Playing the full game instead.", this);
            }
            Current = chosen;
        }

        void OnDestroy()
        {
            if (Current != null && (Current == balance || Current == demoBalance)) Current = null;
        }
    }
}
