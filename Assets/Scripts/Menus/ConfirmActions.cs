// ============================================================================
// ConfirmActions.cs
// Ready-made "ask first" versions of the menu buttons, for a Button's On Click list. Each one
// shows the Confirm Dialog ("Are you sure?") and only does the action on Yes:
//   NewGame  - main menu: asks only when a save exists (a new game deletes it), then runs
//              the main menu's own New Game (so the Skip tutorial toggle still counts);
//   Restart  - Win / GameOver "Play Again": starts a new run;
//   Quit     - closes the game.
// (The pause menu's buttons are on PauseMenuUI, which asks the same way.)
//
// DESIGN RULES: none of its own (T42).
//
// HOW TO USE: put it on any object in the scene (the menu scenes have it on their "UI"
// object) and pick ConfirmActions > NewGame / Restart / Quit in the Button's On Click list.
// HOW TO EDIT: the questions are in the Inspector.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Menu buttons that ask "Are you sure?" first.</summary>
    public class ConfirmActions : MonoBehaviour
    {
        [Tooltip("Asked before New Game when a save exists.")]
        public string newGameQuestion = "Start a new game?\nYour saved game will be deleted.";
        [Tooltip("Asked before Play Again on the Win / GameOver screens.")]
        public string restartQuestion = "Start a new run?";
        [Tooltip("Asked before closing the game.")]
        public string quitQuestion = "Quit the game?";

        /// <summary>Main menu New Game: asks first when there is a save to lose.</summary>
        public void NewGame()
        {
            var menu = FindAnyObjectByType<MenuSceneUI>();
            System.Action start = () =>
            {
                if (menu != null) menu.NewGame();
                else SceneFlow.StartGame(SceneFlow.StartMode.NewGame);
            };
            if (SaveManager.HasSave) ConfirmDialog.Ask(newGameQuestion, start);
            else start();
        }

        /// <summary>Win / GameOver Play Again: asks, then starts a new run (a demo after a demo).</summary>
        public void Restart()
        {
            ConfirmDialog.Ask(restartQuestion, () =>
            {
                var end = FindAnyObjectByType<EndSceneUI>();
                if (end != null) end.PlayAgain();
                else CollapseManager.Restart();
            });
        }

        /// <summary>Asks, then closes the game.</summary>
        public void Quit() => ConfirmDialog.Ask(quitQuestion, SceneFlow.Quit);
    }
}
