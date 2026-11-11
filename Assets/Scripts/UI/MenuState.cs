// ============================================================================
// MenuState.cs
// One place that puts the game into "a menu is open" and back. While a menu is open the
// player can't move or look around, the cursor is free and visible, and the world (and
// the clock) keep running (the Tab menu pauses only the clock, at the hub). Used by the
// Tab menu (GameMenu), the map screen and the survivor note panel.
//
// Enter() only works from Exploring. So while the game is paused, in a minigame, collapsed,
// or frozen behind the title / win / game over screen (Game_Manager's Collapsed state,
// MainMenu.Freeze), Tab, M and B can't open anything.
//
// DESIGN RULES: R7.1 (menus), R9.1 (the world doesn't pause).
//
// HOW TO USE: call MenuState.Enter() when a menu opens (it says no if another screen,
// a minigame or the pause menu is already up) and MenuState.Exit() when it closes.
// If the state changes under an open menu (a collapse), the menu should just hide itself
// without calling Exit (GameMenu and NotePanel do this).
// HOW TO EDIT: nothing to tune here.
// ============================================================================

namespace TheLastDrop
{
    /// <summary>Switches Game_Manager into and out of the Menu state.</summary>
    public static class MenuState
    {
        /// <summary>True while a menu screen is open.</summary>
        public static bool IsOpen =>
            Game_Manager.instance != null && Game_Manager.instance.currentGameState == Game_Manager.GameState.Menu;

        /// <summary>
        /// Puts the game into the Menu state. Returns false (and does nothing) if the player isn't
        /// free to open a menu right now (paused, in a minigame, collapsed, another menu open).
        /// </summary>
        public static bool Enter()
        {
            var gm = Game_Manager.instance;
            if (gm == null) return true; // scenes without a Game Manager: nothing to switch
            if (gm.currentGameState != Game_Manager.GameState.Exploring) return false;
            gm.ChangeGameState(Game_Manager.GameState.Menu);

            // Stop the walk animation: movement code doesn't run while a menu is open.
            var player = Player_Manager.instance;
            if (player != null && player.playerAnimator_Manager != null)
                player.playerAnimator_Manager.UpdateAnimatorMovementParameters(0f, false);
            return true;
        }

        /// <summary>Back to normal play, if the game is still in the Menu state.</summary>
        public static void Exit()
        {
            var gm = Game_Manager.instance;
            if (gm != null && gm.currentGameState == Game_Manager.GameState.Menu)
                gm.ChangeGameState(Game_Manager.GameState.Exploring);
        }
    }
}
