// ============================================================================
// PauseMenuUI.cs
// The Esc pause menu's buttons (on the "Player Pause" prefab):
//   Resume                - back to the game (Esc does the same);
//   Settings              - the shared Settings panel (volume, sensitivity, fullscreen, resolution);
//   Controls              - the list of keys;
//   Save and quit to menu - asks "Are you sure?", saves (not in Demo mode) and goes to the main menu;
//   Quit                  - asks "Are you sure?" and closes the game (no save, like the other Quit buttons).
// Game_Manager shows and hides the whole prefab (its "Pause Menu" slot); this script only
// switches between the button list, Settings and Controls inside it.
//
// DESIGN RULES: Controls table, R11.8 (one save slot; Demo never saves), T42.
//
// HOW TO USE: on the root of Assets/Prefabs/Game Prefab/UI/Player Pause.prefab with the slots
// below filled. The buttons are wired here at start, so their On Click lists can stay empty.
// HOW TO EDIT: everything you see is in the prefab. The Controls text is plain text in it.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Runs the pause menu's buttons and its Settings / Controls pages.</summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Pages")]
        [Tooltip("The title and the button list. Hidden while Settings or Controls is open.")]
        public GameObject mainPage;
        [Tooltip("The shared Settings panel (prefab inside this one).")]
        public SettingsPanelUI settingsPanel;
        [Tooltip("The Controls page (list of keys).")]
        public GameObject controlsPage;

        [Header("Buttons")]
        public Button resumeButton;
        public Button settingsButton;
        public Button controlsButton;
        public Button saveQuitButton;
        public Button quitButton;
        [Tooltip("The Controls page's Back button.")]
        public Button controlsBackButton;

        [Header("Questions")]
        public string saveQuitQuestion = "Save and quit to the main menu?";
        public string demoQuitQuestion = "Quit to the main menu?\nDemo runs are not saved.";
        [Tooltip("Quit does not save (only Save and quit to menu does), so the question says so.")]
        public string quitQuestion = "Quit the game without saving?\nUse \"Save and quit to menu\" to keep your progress.";

        [Header("Words")]
        public string saveQuitLabel = "Save and quit to menu";
        public string demoSaveQuitLabel = "Quit to menu";

        bool wired;

        static bool IsDemo => BalanceProvider.Current != null && BalanceProvider.Current.isDemoMode;

        void Awake() => Wire();

        void OnEnable()
        {
            ShowPage(mainPage);
            if (saveQuitButton != null)
            {
                var label = saveQuitButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null) label.text = IsDemo ? demoSaveQuitLabel : saveQuitLabel;
            }
        }

        void Wire()
        {
            if (wired) return;
            wired = true;
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (controlsButton != null) controlsButton.onClick.AddListener(() => ShowPage(controlsPage));
            if (controlsBackButton != null) controlsBackButton.onClick.AddListener(() => ShowPage(mainPage));
            if (saveQuitButton != null) saveQuitButton.onClick.AddListener(SaveAndQuitToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (settingsPanel != null) settingsPanel.onClosed.AddListener(() => ShowPage(mainPage));
        }

        void ShowPage(GameObject page)
        {
            if (mainPage != null) mainPage.SetActive(page == mainPage);
            if (controlsPage != null) controlsPage.SetActive(page == controlsPage);
            if (settingsPanel != null && page != settingsPanel.gameObject) settingsPanel.gameObject.SetActive(false);
        }

        /// <summary>Back to the game.</summary>
        public void Resume()
        {
            if (Game_Manager.instance != null) Game_Manager.instance.ResumeGame();
        }

        /// <summary>Shows the Settings panel.</summary>
        public void OpenSettings()
        {
            if (settingsPanel == null) return;
            ShowPage(settingsPanel.gameObject);
            settingsPanel.Open();
        }

        /// <summary>Asks, then saves (not in Demo) and goes to the main menu.</summary>
        public void SaveAndQuitToMenu() =>
            ConfirmDialog.Ask(IsDemo ? demoQuitQuestion : saveQuitQuestion, MainMenu.SaveAndQuitToMenu);

        /// <summary>Asks, then closes the game (without saving, like every other Quit button).</summary>
        public void Quit() => ConfirmDialog.Ask(quitQuestion, SceneFlow.Quit);
    }
}
