// ============================================================================
// MenuSceneUI.cs
// The MainMenu scene's buttons and Settings. The screen itself is a normal Canvas saved in
// the MainMenu scene (Tools > The Last Drop > Build Menu Scenes makes it once), so the
// artists can change every background, text, button, font, colour and position there in
// the Hierarchy / Inspector, or replace them with their own art.
//
// What this script does:
//   - the buttons call its public methods (set in each Button's On Click list):
//       NewGame, Continue, Demo, OpenSettings, CloseSettings, Quit;
//   - hides the Continue button when there is no save (R11.8);
//   - the Settings sliders call SetMusicVolume / SetSfxVolume / SetMouseSensitivity
//     (On Value Changed) and it writes the value next to each slider.
// Starting a run shows the loading screen (SceneFlow / LoadingScreen).
//
// DESIGN RULES: R11.8, R13.1, pending UI list (main menu, settings, loading screen).
//
// HOW TO USE: on the "MainMenu" object in the MainMenu scene, with the fields below dragged in
// (the menu command fills them). A new button: add a Button and pick one of the methods above
// in its On Click list.
// HOW TO EDIT: everything you see is in the Canvas. Only the slider ranges are set here.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Runs the main menu's buttons and Settings sliders (the look lives in the scene's Canvas).</summary>
    public class MenuSceneUI : MonoBehaviour
    {
        [Header("Screens")]
        [Tooltip("The title and main buttons. Hidden while Settings is open.")]
        public GameObject mainPanel;
        [Tooltip("The Settings panel. Hidden until Settings is pressed.")]
        public GameObject settingsPanel;

        [Header("Buttons")]
        [Tooltip("Hidden when there is no save to continue.")]
        public Button continueButton;

        [Header("Settings")]
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider sensitivitySlider;
        [Tooltip("Text next to each slider showing its value. Any can be empty.")]
        public TMP_Text musicValue, sfxValue, sensitivityValue;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (continueButton != null) continueButton.gameObject.SetActive(SaveManager.HasSave);

            Setup(musicSlider, 0f, 1f, GameSettings.MusicVolume);
            Setup(sfxSlider, 0f, 1f, GameSettings.SfxVolume);
            Setup(sensitivitySlider, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.MouseSensitivity);
            RefreshValues();
            CloseSettings();
        }

        static void Setup(Slider s, float min, float max, float value)
        {
            if (s == null) return;
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(value);
        }

        // ------------------------------------------------------------------ Buttons (On Click)

        /// <summary>New Game: deletes the save and starts a full run.</summary>
        public void NewGame() => SceneFlow.StartGame(SceneFlow.StartMode.NewGame);

        /// <summary>Continue: starts the game and loads the save (R11.8).</summary>
        public void Continue() => SceneFlow.StartGame(SaveManager.HasSave ? SceneFlow.StartMode.Continue : SceneFlow.StartMode.NewGame);

        /// <summary>Demo: a fresh run with the demo numbers (R13.1).</summary>
        public void Demo() => SceneFlow.StartGame(SceneFlow.StartMode.Demo);

        /// <summary>Shows Settings, hides the main buttons.</summary>
        public void OpenSettings() => ShowSettings(true);

        /// <summary>Saves the settings and goes back to the main buttons.</summary>
        public void CloseSettings() => ShowSettings(false);

        /// <summary>Closes the game.</summary>
        public void Quit() => SceneFlow.Quit();

        void ShowSettings(bool show)
        {
            if (!show) GameSettings.Save();
            if (settingsPanel != null) settingsPanel.SetActive(show);
            if (mainPanel != null) mainPanel.SetActive(!show);
        }

        // ------------------------------------------------------------------ Sliders (On Value Changed)

        /// <summary>Music volume slider (0-1).</summary>
        public void SetMusicVolume(float v) { GameSettings.MusicVolume = v; RefreshValues(); }

        /// <summary>SFX volume slider (0-1).</summary>
        public void SetSfxVolume(float v) { GameSettings.SfxVolume = v; RefreshValues(); }

        /// <summary>Mouse sensitivity slider.</summary>
        public void SetMouseSensitivity(float v) { GameSettings.MouseSensitivity = v; RefreshValues(); }

        void RefreshValues()
        {
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            if (sensitivityValue != null) sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00") + "×";
        }
    }
}
