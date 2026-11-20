// ============================================================================
// SaveData.Tutorial.cs
// The tutorial's part of the save: which step of the guided first playthrough the player is
// on, what it unlocked early, and which one-time lessons were already shown, so Continue
// resumes it. Same SaveData class, split over files so each feature adds its own fields.
//
// Written by TutorialDirector (Assets/Scripts/Tutorial).
//
// DESIGN RULES: R11.8 (saving).
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults. A save from
// before the guided tutorial (Tutorial Version below 2, e.g. one written by the old
// TutorialHints) loads with the tutorial off.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        /// <summary>The tutorial save format written now (2 = the guided tutorial).</summary>
        public const int TutorialVersion = 2;

        [Header("Tutorial")]
        [Tooltip("True once the tutorial wrote its part (older saves: false).")]
        public bool hasTutorial;
        [Tooltip("2 = the guided tutorial. 0 or 1 = an older save: the tutorial stays off.")]
        public int tutorialVersion;
        [Tooltip("0 = off (skipped or done before), 1 = running, 2 = main steps finished (one-time lessons still show).")]
        public int tutorialMode;
        [Tooltip("Id of the step the player was on (see TutorialDirector.BuildSteps). It starts again from its beginning.")]
        public string tutorialStep = "";
        [Tooltip("Features unlocked early (Build and Skills at the first dawn), as TutorialFeature flags.")]
        public int tutorialUnlocks;
        [Tooltip("Ids of the one-time lessons already shown (and \"death\" for the first collapse screen).")]
        public List<string> hintsSeen = new List<string>();
    }
}
