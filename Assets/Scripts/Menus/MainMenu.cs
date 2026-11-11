// ============================================================================
// MainMenu.cs
// The title screen. The project has no separate menu scene, so this shows a full-screen
// panel over the game scene when it starts:
//
//     SALOK
//     [New Game]  [Continue]  [Demo]  [Quit]
//
//   - New Game: deletes the save and starts playing.
//   - Continue (only shown if there is a save): loads the save and starts playing (R11.8).
//   - Demo: switches to the GameBalance_Demo asset and starts a fresh run (R13.1).
//   - Quit: closes the game (stops Play mode in the editor).
// While the menu (or an end screen) is up the game is frozen (time scale 0, cursor free)
// and Esc / Tab / M / B do nothing, because the game is held in the Collapsed state,
// where the pause key and MenuState.Enter() refuse to open anything.
//
// After a reload asked for by these buttons, or by Restart on an end screen, the menu is
// skipped (SkipMenuOnce) and the run starts straight away.
// Demo mode never autosaves (one save slot, R11.8, belongs to the full game). Debug keys
// still work behind the title screen: don't press F5 there, it saves the fresh day 1 over
// the real save.
//
// It also shows a plain pause panel (Resume / Save and quit to menu) when Esc pauses
// the game but the team's pause menu can't be seen, and a "DEMO" HUD line in Demo mode.
//
// DESIGN RULES: R11.8 (Continue), section 13 and R13.1 (Demo from the main menu).
//
// HOW TO USE: added to the game scene by itself (needs a BalanceProvider). Nothing to set.
// HOW TO EDIT: the words are the constants below. The look is SimpleHud's panel.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>The title screen (New Game, Continue, Demo, Quit) and the game freeze behind menus.</summary>
    public class MainMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<MainMenu>();

        const string GameTitle = "SALOK";
        const string Subtitle = "Survive the drought. Find the way out.";

        /// <summary>True = the next time a game scene starts, skip the title screen and play at once.</summary>
        public static bool SkipMenuOnce;

        /// <summary>True while the title screen is on screen.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>True while the game is held frozen behind the title screen or an end screen.</summary>
        public static bool IsFrozen => holdFrozen;

        static bool holdFrozen;
        static bool loadSaveOnStart; // Continue pressed while a Demo run was loaded: load after the reload

        bool pausePanelShown;

        // Statics survive Play mode in the editor when domain reload is off: start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SkipMenuOnce = false;
            IsOpen = false;
            holdFrozen = false;
            loadSaveOnStart = false;
        }

        void Awake()
        {
            // A new scene: nothing is frozen yet.
            holdFrozen = false;
            IsOpen = false;
        }

        IEnumerator Start()
        {
            var balance = BalanceProvider.Current;
            if (balance != null && balance.isDemoMode)
            {
                SimpleHud.SetLine("demo", "DEMO");
                // A demo run must not overwrite the full game's save (R11.8 is one slot).
                var save = FindAnyObjectByType<SaveManager>();
                if (save != null) save.autosaveAtDawn = false;
            }

            if (loadSaveOnStart)
            {
                loadSaveOnStart = false;
                SkipMenuOnce = false;
                // Wait so every system has finished its own Start (same as SaveManager.LoadAndStart).
                yield return null;
                yield return null;
                yield return null;
                LoadSaveNow();
                yield break;
            }

            if (SkipMenuOnce)
            {
                SkipMenuOnce = false;
                yield break;
            }
            ShowTitle();
        }

        void OnDestroy()
        {
            if (IsOpen || holdFrozen) SimpleHud.HidePanel();
            IsOpen = false;
            holdFrozen = false;
        }

        // ------------------------------------------------------------------ Title screen

        void ShowTitle()
        {
            IsOpen = true;
            Freeze();
            string body = Subtitle;
            if (SaveManager.HasSave) body += "\n\nA saved game was found.";
            if (SaveManager.HasSave)
                SimpleHud.ShowPanel(GameTitle, body,
                    ("New Game", new System.Action(NewGame)),
                    ("Continue", new System.Action(Continue)),
                    ("Demo", new System.Action(Demo)),
                    ("Quit", new System.Action(CollapseManager.Quit)));
            else
                SimpleHud.ShowPanel(GameTitle, body,
                    ("New Game", new System.Action(NewGame)),
                    ("Demo", new System.Action(Demo)),
                    ("Quit", new System.Action(CollapseManager.Quit)));
        }

        /// <summary>New Game: delete the save and play (a fresh full run).</summary>
        public void NewGame()
        {
            SaveManager.DeleteSave();
            if (BalanceProvider.UseDemo)
            {
                // This scene was loaded with the demo numbers: reload with the full game's.
                BalanceProvider.UseDemo = false;
                ReloadScene(skipMenu: true);
                return;
            }
            CloseTitle();
            Debug.Log("[MainMenu] New Game.", this);
        }

        /// <summary>Continue: load the save and play (R11.8).</summary>
        public void Continue()
        {
            if (!SaveManager.HasSave) { NewGame(); return; }
            if (BalanceProvider.UseDemo)
            {
                BalanceProvider.UseDemo = false;
                loadSaveOnStart = true;
                ReloadScene(skipMenu: true);
                return;
            }
            CloseTitle();
            LoadSaveNow();
        }

        /// <summary>Demo: switch to GameBalance_Demo and start a fresh run (section 13, R13.1).</summary>
        public void Demo()
        {
            BalanceProvider.UseDemo = true;
            Debug.Log("[MainMenu] Demo mode: reloading the scene with GameBalance_Demo.", this);
            ReloadScene(skipMenu: true);
        }

        void LoadSaveNow()
        {
            var save = SaveManager.Instance != null ? SaveManager.Instance : FindAnyObjectByType<SaveManager>();
            if (save != null)
            {
                if (!save.Load()) SimpleHud.Toast("The save couldn't be loaded. Starting a new game.");
            }
            else
            {
                // No SaveManager in this scene: let it open the game scene and load there.
                SaveManager.LoadAndStart();
            }
        }

        void CloseTitle()
        {
            IsOpen = false;
            Unfreeze();
        }

        // ------------------------------------------------------------------ Freezing

        /// <summary>
        /// Freezes the game behind a full-screen panel: time scale 0, cursor free, no other menu
        /// can open (the game is held in the Collapsed state). Lasts until Unfreeze or a scene load.
        /// </summary>
        public static void Freeze()
        {
            holdFrozen = true;
            ApplyFreeze();
        }

        /// <summary>Hides the panel and gives control back to the player.</summary>
        public static void Unfreeze()
        {
            holdFrozen = false;
            SimpleHud.HidePanel();
            var gm = Game_Manager.instance;
            if (gm != null) gm.ChangeGameState(Game_Manager.GameState.Exploring);
            else Time.timeScale = 1f;
        }

        static void ApplyFreeze()
        {
            var gm = Game_Manager.instance;
            if (gm != null)
            {
                if (gm.currentGameState != Game_Manager.GameState.Collapsed)
                    gm.ChangeGameState(Game_Manager.GameState.Collapsed); // time scale 0, cursor free
            }
            else
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>Reloads the game scene (time scale back to 1). skipMenu = go straight into play.</summary>
        public static void ReloadScene(bool skipMenu)
        {
            SkipMenuOnce = skipMenu;
            holdFrozen = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ Every frame

        void Update()
        {
            // Runs at time scale 0 too (Update still runs; only Time.deltaTime is 0).
            // Something else (Game_Manager's own Start, a minigame closing) may have switched the
            // state back to Exploring: keep the game frozen while a panel is up.
            if (holdFrozen) { ApplyFreeze(); return; }
            UpdatePauseFallback();
        }

        // Esc pauses the game (Game_Manager's Paused state). If the team's pause menu can't be
        // seen, show a plain one so the player isn't stuck on a frozen screen.
        void UpdatePauseFallback()
        {
            var gm = Game_Manager.instance;
            bool paused = gm != null && gm.currentGameState == Game_Manager.GameState.Paused;
            bool teamMenuVisible = paused && gm.pauseMenu != null && gm.pauseMenu.activeInHierarchy &&
                                   gm.pauseMenu.GetComponentInParent<Canvas>(true) != null;

            if (paused && !teamMenuVisible && !pausePanelShown)
            {
                pausePanelShown = true;
                bool demo = BalanceProvider.Current != null && BalanceProvider.Current.isDemoMode;
                SimpleHud.ShowPanel("PAUSED", "Esc to resume.",
                    ("Resume", new System.Action(Resume)),
                    (demo ? "Quit to menu" : "Save and quit to menu", new System.Action(SaveAndQuitToMenu)));
            }
            else if (!paused && pausePanelShown)
            {
                pausePanelShown = false;
                SimpleHud.HidePanel();
            }
        }

        void Resume()
        {
            var gm = Game_Manager.instance;
            if (gm != null) gm.ResumeGame();
        }

        void SaveAndQuitToMenu()
        {
            bool demo = BalanceProvider.Current != null && BalanceProvider.Current.isDemoMode;
            if (!demo && SaveManager.Instance != null) SaveManager.Instance.Save();
            BalanceProvider.UseDemo = false;
            pausePanelShown = false;
            ReloadScene(skipMenu: false);
        }
    }
}
