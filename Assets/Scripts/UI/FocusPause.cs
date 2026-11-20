// ============================================================================
// FocusPause.cs
// When the game window loses focus during play (Alt+Tab, a click on another window), the game
// pauses and the pause menu opens (T47), so the player doesn't come back to a collapse.
// It only does this while the player is exploring: not when a menu, the map, a minigame, the
// How to Play card, a cutscene / full-screen panel (the game is frozen then) or the pause menu
// is already showing, and not while a scene is loading. The main menu has no game, so nothing
// happens there.
//
// DESIGN RULES: none of its own (T47).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: untick "Pause On Focus Loss" on the object at Play to switch it off.
// Note: in the editor, clicking outside the Game view (Inspector, Console) also counts as
// losing focus, so the game pauses then too. Press Esc to resume.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Opens the pause menu when the game window loses focus during play.</summary>
    public class FocusPause : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<FocusPause>();

        [Tooltip("Pause when the window loses focus.")]
        public bool pauseOnFocusLoss = true;

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !pauseOnFocusLoss) return;
            if (LoadingScreen.IsLoading || MainMenu.IsFrozen || ConfirmDialog.IsOpen) return;
            var gm = Game_Manager.instance;
            // Exploring only: Menu (map, Build / Skills, notes), Minigame, Paused and Collapsed
            // (How to Play, end panels, collapse) are left as they are.
            if (gm == null || gm.currentGameState != Game_Manager.GameState.Exploring) return;
            gm.ChangeGameState(Game_Manager.GameState.Paused);
        }
    }
}
