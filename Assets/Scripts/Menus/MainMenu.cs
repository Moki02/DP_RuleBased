// ============================================================================
// MainMenu.cs   (the game scene's side of the menus)
// The title screen now has its own scene (MainMenu, MenuSceneUI.cs). This script stays in
// the game scene and:
//   - starts the run the way the main menu asked (SceneFlow): New Game and Demo just play;
//     Continue loads the save once every system has started (R11.8). Pressing Play straight
//     in the game scene in the editor just starts a new run;
//   - freezes the game behind a full-screen panel when something asks (Freeze / Unfreeze:
//     time scale 0, cursor free, Esc / Tab / M / B do nothing);
//   - adds a "Save and quit to menu" button to the team's pause menu (or wires the artists'
//     own Button named "Quit To Menu Button" in the pause menu prefab), and shows a plain
//     pause panel (Resume / Save and quit to menu) if the team's pause menu can't be seen;
//   - shows a "DEMO" HUD line in Demo mode, where autosave is off (R11.8 is one slot).
//
// DESIGN RULES: R11.8 (Continue), section 13 and R13.1 (Demo).
//
// HOW TO USE: added to the game scene by itself (needs a BalanceProvider). Nothing to set.
// HOW TO EDIT: the words are the constants below.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Starts the run the main menu asked for, freezes the game behind panels, and handles pause-to-menu.</summary>
    public class MainMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<MainMenu>();

        const string QuitToMenuLabel = "Save and quit to menu";
        const string DemoQuitToMenuLabel = "Quit to menu";

        /// <summary>Kept for older callers: the game scene never shows a title screen now.</summary>
        public static bool SkipMenuOnce;

        /// <summary>Always false now (the title screen is the MainMenu scene). Kept for older callers.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>True while the game is held frozen behind a full-screen panel.</summary>
        public static bool IsFrozen => holdFrozen;

        static bool holdFrozen;

        bool pausePanelShown;
        bool pauseButtonAdded;

        // Statics survive Play mode in the editor when domain reload is off: start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SkipMenuOnce = false;
            IsOpen = false;
            holdFrozen = false;
        }

        void Awake()
        {
            // A new scene: nothing is frozen yet.
            holdFrozen = false;
            IsOpen = false;
        }

        IEnumerator Start()
        {
            SkipMenuOnce = false;
            var balance = BalanceProvider.Current;
            if (balance != null && balance.isDemoMode)
            {
                SimpleHud.SetLine("demo", "DEMO");
                // A demo run must not overwrite the full game's save (R11.8 is one slot).
                var save = FindAnyObjectByType<SaveManager>();
                if (save != null) save.autosaveAtDawn = false;
            }

            if (SceneFlow.ConsumeContinue())
            {
                // Wait so every system has finished its own Start (same as SaveManager.LoadAndStart).
                yield return null;
                yield return null;
                yield return null;
                LoadSaveNow();
            }
        }

        void OnDestroy()
        {
            if (holdFrozen) SimpleHud.HidePanel();
            IsOpen = false;
            holdFrozen = false;
        }

        void LoadSaveNow()
        {
            var save = SaveManager.Instance != null ? SaveManager.Instance : FindAnyObjectByType<SaveManager>();
            if (save == null || !save.Load())
                SimpleHud.Toast("The save couldn't be loaded. Starting a new game.");
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

        /// <summary>Reloads the game scene (time scale back to 1).</summary>
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
            UpdatePause();
        }

        // Esc pauses the game (Game_Manager's Paused state). The team's pause menu gets a
        // "quit to menu" button; if it can't be seen, a plain pause panel shows instead, so the
        // player is never stuck on a frozen screen.
        void UpdatePause()
        {
            var gm = Game_Manager.instance;
            bool paused = gm != null && gm.currentGameState == Game_Manager.GameState.Paused;
            bool teamMenuVisible = paused && gm.pauseMenu != null && gm.pauseMenu.activeInHierarchy &&
                                   gm.pauseMenu.GetComponentInParent<Canvas>(true) != null;

            if (teamMenuVisible && !pauseButtonAdded) AddQuitButton(gm.pauseMenu.transform);

            if (paused && !teamMenuVisible && !pausePanelShown)
            {
                pausePanelShown = true;
                SimpleHud.ShowPanel("PAUSED", "Esc to resume.",
                    ("Resume", new System.Action(Resume)),
                    (IsDemo ? DemoQuitToMenuLabel : QuitToMenuLabel, new System.Action(SaveAndQuitToMenu)));
            }
            else if (!paused && pausePanelShown)
            {
                pausePanelShown = false;
                SimpleHud.HidePanel();
            }
        }

        /// <summary>Name of the pause menu's "quit to menu" button. Add a Button with this name to the team's pause menu to use your own.</summary>
        public const string QuitButtonName = "Quit To Menu Button";

        // The team's pause menu gets a "quit to menu" button. If the artists added a Button named
        // "Quit To Menu Button" to the pause menu prefab, that one is used (styled and placed by
        // them); otherwise a plain black-and-white one is added at the bottom.
        void AddQuitButton(Transform pauseMenu)
        {
            pauseButtonAdded = true;
            Button b = null;
            foreach (var candidate in pauseMenu.GetComponentsInChildren<Button>(true))
                if (candidate.name == QuitButtonName) { b = candidate; break; }
            if (b == null)
            {
                b = UiBuilder.Button(QuitButtonName, pauseMenu, IsDemo ? DemoQuitToMenuLabel : QuitToMenuLabel, 28f,
                                     new Color(0.16f, 0.16f, 0.16f), Color.white, null);
                UiBuilder.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 60f));
            }
            b.onClick.AddListener(SaveAndQuitToMenu);
        }

        static bool IsDemo => BalanceProvider.Current != null && BalanceProvider.Current.isDemoMode;

        void Resume()
        {
            var gm = Game_Manager.instance;
            if (gm != null) gm.ResumeGame();
        }

        void SaveAndQuitToMenu()
        {
            if (!IsDemo && SaveManager.Instance != null) SaveManager.Instance.Save();
            pausePanelShown = false;
            holdFrozen = false;
            SceneFlow.ToMainMenu();
        }
    }
}
