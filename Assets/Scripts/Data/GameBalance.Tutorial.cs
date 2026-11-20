// ============================================================================
// GameBalance.Tutorial.cs
// The guided first playthrough's numbers (Assets/Scripts/Tutorial). Same GameBalance asset,
// split over files so each feature keeps its own fields together.
//
// DESIGN RULES: none of its own (the tutorial teaches R1 to R11 in play).
//
// HOW TO USE: nothing to set up; TutorialDirector reads these.
// HOW TO EDIT: change the values in the GameBalance asset's Inspector, under "Tutorial".
// The words are not here: they are in the Tutorial Texts asset
// (Assets/Resources/Tutorial/TutorialTexts.asset, made by Salok > Tutorial > Set Up Tutorial).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Tutorial (guided first playthrough)")]
        [Tooltip("ON: the guided tutorial also runs in Demo mode. OFF: Demo mode never shows it (its 75 s day is too short).")]
        public bool tutorialInDemo = false;

        [Tooltip("Move step: metres the player must walk before it counts as done.")]
        [Min(0.5f)] public float tutorialMoveMetres = 3f;

        [Tooltip("Pump step: litres to pump into the Raw Basin before it counts as done.")]
        [Min(0.1f)] public float tutorialPumpLitres = 1f;

        [Tooltip("Boiling pot step: litres the pot must move into Clean Storage before it counts as done.")]
        [Min(0.05f)] public float tutorialPotLitres = 0.3f;

        [Tooltip("Canteen step: litres the player must drink with Q before it counts as done.")]
        [Min(0.05f)] public float tutorialDrinkLitres = 0.1f;

        [Tooltip("Maze step: seconds outside the hub before it counts as done.")]
        [Min(1f)] public float tutorialOutsideSeconds = 15f;

        [Tooltip("Seconds a 'read this' step (Raw Basin, Clean Storage) stays before it counts as done.")]
        [Min(1f)] public float tutorialReadSeconds = 4.5f;

        [Tooltip("Seconds the 'Done' tick shows before the next step starts.")]
        [Min(0.2f)] public float tutorialDoneSeconds = 1.2f;

        [Tooltip("Seconds on one step before the card offers to skip it (so the player can never get stuck).")]
        [Min(10f)] public float tutorialStuckSeconds = 90f;

        [Tooltip("Seconds a side lesson (night, rooms, weather ...) stays on screen if the player doesn't do what it says.")]
        [Min(2f)] public float tutorialLessonSeconds = 9f;

        [Header("Tutorial: first valve minigame (easier, free to fail)")]
        [Tooltip("Green band width (degrees) of the guided first minigame. Normal hub gate: Valve Band At Hub (30).")]
        [Min(1f)] public float tutorialValveBand = 45f;

        [Tooltip("Needle speed (degrees per second) of the guided first minigame. Normal hub gate: Valve Speed At Hub (120). " +
                 "The needle never speeds up in this game.")]
        [Min(1f)] public float tutorialValveSpeed = 80f;
    }
}
