// ============================================================================
// BalanceProvider.cs
// Holds the scene's GameBalance asset in one place, so every system in the scene reads
// the same numbers. Other scripts use BalanceProvider.Current instead of each keeping
// their own reference.
//
// DESIGN RULES: none of its own; it only points at GameBalance (GDD section 12).
//
// HOW TO USE: put ONE of these on the "Systems" object and drag GameBalance (or
// GameBalance_Demo) into "Balance". The Build Playtest Scene menu does this for you.
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

        /// <summary>The GameBalance of the active scene, or null if the scene has no BalanceProvider.</summary>
        public static GameBalance Current { get; private set; }

        void Awake()
        {
            if (balance == null)
                Debug.LogError("[BalanceProvider] No GameBalance asset assigned.", this);
            Current = balance;
        }

        void OnDestroy()
        {
            if (Current == balance) Current = null;
        }
    }
}
