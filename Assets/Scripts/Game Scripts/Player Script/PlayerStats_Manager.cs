// ============================================================================
// PlayerStats_Manager.cs
// The player's basic numbers: turning and movement speeds, and HYD (hydration points).
// Other scripts read and change these; this script doesn't do anything by itself.
//
// DESIGN RULES: Resources table (HYD 0 to 100), R2.x (PlayerHydration_Manager changes HYD).
//
// HOW TO USE: on the Player prefab.
// HOW TO EDIT: speeds here in the Inspector. In the maze game, "Max Hydration" is replaced
// at Start by GameBalance > Hydration (hydMax) when Survival On is ticked.
// ============================================================================

using UnityEngine;

/// <summary>The player's speeds and HYD.</summary>
public class PlayerStats_Manager : MonoBehaviour
{
    [Header("Movement Variable")]
    [Tooltip("How fast the player turns to face the move direction (higher = snappier).")]
    public float player_RotationalSpeed = 15;
    [Tooltip("Walking speed in metres per second.")]
    public float player_WalkingSpeed = 4f;
    [Tooltip("Sprinting speed in metres per second.")]
    public float player_RunningSpeed = 6.5f;

    [Header("Hydration")]
    [Tooltip("Most HYD the player can have (points). Overwritten by GameBalance when Survival On is ticked.")]
    public float player_MaxHydration = 100f;
    [Tooltip("Current HYD (points). 0 = collapse.")]
    public float player_Hydration = 100f;

    /// <summary>Adds <paramref name="amount"/> HYD (negative takes it away), kept between 0 and max.</summary>
    public void ChangeHydration(float amount)
    {
        player_Hydration = Mathf.Clamp(player_Hydration + amount, 0f, player_MaxHydration);
    }


}
