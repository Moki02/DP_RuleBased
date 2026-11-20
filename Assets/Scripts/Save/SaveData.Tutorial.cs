// ============================================================================
// SaveData.Tutorial.cs
// The tutorial hints' part of the save: which one-time hints the player has already seen,
// so Continue doesn't show them again. Same SaveData class, split over files so each
// feature adds its own fields.
//
// Written by TutorialHints.
//
// DESIGN RULES: R11.8 (saving).
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults
// (TutorialHints treats an old save past day 1 as "every hint seen").
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Tutorial hints")]
        [Tooltip("True once the hints wrote their part (older saves: false).")]
        public bool hasTutorial;
        [Tooltip("Ids of the hints already shown (see TutorialHints.BuildHints).")]
        public List<string> hintsSeen = new List<string>();
    }
}
