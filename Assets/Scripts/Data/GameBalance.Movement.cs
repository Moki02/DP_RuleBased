// ============================================================================
// GameBalance.Movement.cs
// Crouch and jump numbers (new mechanics, added with the Synty locomotion animations).
// Same GameBalance asset, split over files so each feature keeps its own fields together.
//
//   Crouch (C toggles): slower, and costs LESS water than walking or standing.
//   Jump (Space): costs a one-off chunk of HYD at take-off, multiplied by the drought stage
//   and events like every other drain (R2.1). Blocked below the cost, while Dehydrated or
//   Collapsed, in a minigame and in menus.
//
// DESIGN RULES: R2.1 (drain = base × stage heat × event × skills). The crouch/jump rows
// are new and still to be added to the GDD.
//
// HOW TO USE: nothing to set up; PlayerLocomotion_Manager and PlayerHydration_Manager read these.
// HOW TO EDIT: change the values in the GameBalance asset's Inspector, under "Crouch and jump".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Crouch and jump")]
        [Tooltip("Crouch walking speed in m/s (walk is 4).")]
        [Min(0f)] public float crouchSpeed = 2f;

        [Tooltip("HYD lost per second while moving crouched, before stage, event and skill multipliers (walking is 0.5).")]
        [Min(0f)] public float crouchDrain = 0.35f;

        [Tooltip("HYD lost per second while crouched and still, before multipliers (standing is 0.25).")]
        [Min(0f)] public float crouchIdleDrain = 0.2f;

        [Tooltip("Jump height in metres.")]
        [Min(0f)] public float jumpHeight = 1.2f;

        [Tooltip("HYD taken once per jump at take-off, × drought stage and events like other drain. " +
                 "No jumping with less HYD than this.")]
        [Min(0f)] public float jumpHydCost = 3f;
    }
}
