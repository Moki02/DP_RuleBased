// ============================================================================
// SceneFlow.cs
// The one place that moves the game between its scenes:
//
//   MainMenu      (title, Settings)          -> SceneFlow.StartGame(New / Continue / Demo)
//   Playtest_Maze (the game)                 -> SceneFlow.ToWin / ToGameOver / ToMainMenu
//   Win, GameOver (end screens with stats)   -> SceneFlow.StartGame(...) or ToMainMenu
//
// Every change goes through the loading screen (LoadingScreen.Load). The end screens' title
// and text are kept here (EndTitle, EndBody) because the game scene and its stats are gone
// by the time the Win / GameOver scene shows them.
//
// Pressing Play straight in the game scene (in the editor) still works: the game just starts
// (a new run, or the save if Continue was picked last).
//
// DESIGN RULES: R11.4 (game over), R11.6 / R11.7 (win + stats), R11.8 (Continue), R13.1 (Demo).
//
// HOW TO USE: call the static methods. The scenes must be in File > Build Profiles > Scene
// List: Tools > The Last Drop > Build Menu Scenes adds all four.
// HOW TO EDIT: the scene names are the constants below.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Moves the game between the main menu, the game and the end scenes.</summary>
    public static class SceneFlow
    {
        /// <summary>The main menu scene.</summary>
        public const string MainMenuScene = "MainMenu";
        /// <summary>The game scene (same as SaveManager.GameSceneName).</summary>
        public const string GameScene = SaveManager.GameSceneName;
        /// <summary>The win scene.</summary>
        public const string WinScene = "Win";
        /// <summary>The game over scene.</summary>
        public const string GameOverScene = "GameOver";

        /// <summary>How a run starts.</summary>
        public enum StartMode { NewGame, Continue, Demo }

        /// <summary>Title for the Win / GameOver scene (e.g. "SURVIVOR", "THE DROUGHT WINS").</summary>
        public static string EndTitle { get; private set; } = "";
        /// <summary>Text under the title (the day line and the run's stats).</summary>
        public static string EndBody { get; private set; } = "";
        /// <summary>True if the run that just ended was a Demo run (Play Again starts another demo).</summary>
        public static bool EndWasDemo { get; private set; }

        // True = the game scene should load the save once it has started (Continue).
        static bool continuePending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EndTitle = "";
            EndBody = "";
            EndWasDemo = false;
            continuePending = false;
        }

        /// <summary>Starts a run from the main menu or an end screen.</summary>
        public static void StartGame(StartMode mode)
        {
            switch (mode)
            {
                case StartMode.NewGame:
                    BalanceProvider.UseDemo = false;
                    SaveManager.DeleteSave();
                    continuePending = false;
                    break;
                case StartMode.Continue:
                    BalanceProvider.UseDemo = false;
                    continuePending = SaveManager.HasSave;
                    break;
                case StartMode.Demo:
                    // A demo never touches the full game's save (R11.8 is one slot).
                    BalanceProvider.UseDemo = true;
                    continuePending = false;
                    break;
            }
            MainMenu.SkipMenuOnce = true;
            LoadingScreen.Load(GameScene);
        }

        /// <summary>The game scene asks this once when it starts: true = load the save now (Continue).</summary>
        public static bool ConsumeContinue()
        {
            bool c = continuePending;
            continuePending = false;
            return c;
        }

        /// <summary>Back to the main menu (from the pause menu or an end screen).</summary>
        public static void ToMainMenu()
        {
            BalanceProvider.UseDemo = false;
            LoadingScreen.Load(MainMenuScene);
        }

        /// <summary>The player escaped: show the Win scene with this title and text.</summary>
        public static void ToWin(string title, string body)
        {
            SetEnd(title, body);
            LoadingScreen.Load(WinScene);
        }

        /// <summary>The run is lost: show the GameOver scene with this title and text.</summary>
        public static void ToGameOver(string title, string body)
        {
            SetEnd(title, body);
            LoadingScreen.Load(GameOverScene);
        }

        static void SetEnd(string title, string body)
        {
            EndTitle = title ?? "";
            EndBody = body ?? "";
            EndWasDemo = BalanceProvider.UseDemo;
        }

        /// <summary>Closes the game (stops Play mode in the editor).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
