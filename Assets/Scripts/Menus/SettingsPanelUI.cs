// ============================================================================
// SettingsPanelUI.cs
// The Settings panel, one prefab used in two places: the main menu and the in-game pause
// menu (Assets/Prefabs/UI/SettingsPanel.prefab). It runs:
//   - Music volume, SFX volume and Mouse sensitivity sliders (with the value next to each);
//   - the Fullscreen toggle and the Resolution dropdown (T42).
// Every change is saved in GameSettings (PlayerPrefs) straight away and written to disk when
// the panel closes. Fullscreen and resolution are applied at once and again at every start.
//
// DESIGN RULES: none (pending UI list: settings menu; T42).
//
// HOW TO USE: place the prefab inside a Canvas. Call Open() to show it. Its Back button calls
// Close(), which hides it and runs "On Closed" (the main menu shows its buttons again there;
// the pause menu shows its own buttons again).
// HOW TO EDIT: everything you see is in the prefab. The slider ranges come from GameSettings.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Music / SFX / sensitivity sliders, fullscreen toggle and resolution dropdown.</summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("Sliders")]
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider sensitivitySlider;
        [Tooltip("Text next to each slider showing its value. Any can be empty.")]
        public TMP_Text musicValue, sfxValue, sensitivityValue;

        [Header("Display")]
        [Tooltip("Ticked = fullscreen, unticked = a window.")]
        public Toggle fullscreenToggle;
        [Tooltip("Lists the monitor's resolutions.")]
        public TMP_Dropdown resolutionDropdown;

        [Header("Buttons")]
        [Tooltip("Hides the panel (calls Close).")]
        public Button backButton;

        [Tooltip("Runs after the panel closes (Back). The main menu and pause menu show their buttons again here.")]
        public UnityEvent onClosed = new UnityEvent();

        Vector2Int[] resolutions = new Vector2Int[0];
        bool wired;

        void Awake() => Wire();

        void OnEnable() => Refresh();

        void Wire()
        {
            if (wired) return;
            wired = true;
            Setup(musicSlider, 0f, 1f, v => { GameSettings.MusicVolume = v; RefreshValues(); });
            Setup(sfxSlider, 0f, 1f, v => { GameSettings.SfxVolume = v; RefreshValues(); });
            Setup(sensitivitySlider, GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                  v => { GameSettings.MouseSensitivity = v; RefreshValues(); });
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(v => GameSettings.Fullscreen = v);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(PickResolution);
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        static void Setup(Slider s, float min, float max, UnityAction<float> onChange)
        {
            if (s == null) return;
            s.minValue = min;
            s.maxValue = max;
            s.onValueChanged.AddListener(onChange);
        }

        /// <summary>Shows the panel with the current settings.</summary>
        public void Open()
        {
            Wire();
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>Saves the settings, hides the panel and runs On Closed.</summary>
        public void Close()
        {
            GameSettings.Save();
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        /// <summary>Puts the saved values into every control (without firing their events).</summary>
        public void Refresh()
        {
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            FillResolutions();
            RefreshValues();
        }

        void FillResolutions()
        {
            if (resolutionDropdown == null) return;
            resolutions = GameSettings.AvailableResolutions();
            var saved = GameSettings.Resolution;
            var current = saved.x > 0 ? saved : new Vector2Int(Screen.width, Screen.height);
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            int selected = resolutions.Length - 1;
            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add(resolutions[i].x + " x " + resolutions[i].y);
                if (resolutions[i] == current) selected = i;
            }
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, selected));
            resolutionDropdown.RefreshShownValue();
        }

        void PickResolution(int index)
        {
            if (index < 0 || index >= resolutions.Length) return;
            GameSettings.Resolution = resolutions[index];
        }

        void RefreshValues()
        {
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            if (sensitivityValue != null) sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00") + "x";
        }
    }
}
