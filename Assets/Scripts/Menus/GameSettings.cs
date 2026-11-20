// ============================================================================
// GameSettings.cs
// The player's settings from the main menu's Settings screen, kept between sessions
// (PlayerPrefs):
//   - Music volume and SFX volume (0-1). There is no audio in the game yet: when the artists
//     add sounds, put a SettingsVolume component next to each AudioSource and pick Music or
//     SFX; it scales that source by the setting (live, also while the game runs).
//   - Mouse sensitivity (0.25×-3×, 1 = the camera's normal speed). Player_Camera reads it.
//   - Fullscreen and resolution (T42). Applied when the game starts and whenever they change
//     (not in the editor: there the Game view sets the size).
//
// DESIGN RULES: none (pending UI list: settings menu).
//
// HOW TO USE: read GameSettings.MusicVolume / SfxVolume / MouseSensitivity; set them from UI.
// HOW TO EDIT: defaults and ranges are the constants below.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Saved player settings: music volume, SFX volume and mouse sensitivity.</summary>
    public static class GameSettings
    {
        const string MusicKey = "salok.musicVolume";
        const string SfxKey = "salok.sfxVolume";
        const string SensitivityKey = "salok.mouseSensitivity";

        /// <summary>Lowest and highest mouse sensitivity the slider allows.</summary>
        public const float MinSensitivity = 0.25f, MaxSensitivity = 3f;

        /// <summary>Fired when any setting changes (SettingsVolume listens).</summary>
        public static event Action Changed;

        /// <summary>Music volume, 0-1 (default 0.8).</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        /// <summary>Sound effects volume, 0-1 (default 1).</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        /// <summary>Mouse look speed multiplier (default 1).</summary>
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, 1f);
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); Changed?.Invoke(); }
        }

        /// <summary>Writes the settings to disk (the Settings screen calls this when it closes).</summary>
        public static void Save() => PlayerPrefs.Save();

        // ------------------------------------------------------------------ Display (T42)

        const string FullscreenKey = "salok.fullscreen";
        const string ResWidthKey = "salok.resolutionWidth";
        const string ResHeightKey = "salok.resolutionHeight";

        /// <summary>Fullscreen (borderless) or a window. Default: whatever the player started with.</summary>
        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyDisplay(); Changed?.Invoke(); }
        }

        /// <summary>The saved resolution, or (0, 0) if none was picked yet (the game keeps the current one).</summary>
        public static Vector2Int Resolution
        {
            get => new Vector2Int(PlayerPrefs.GetInt(ResWidthKey, 0), PlayerPrefs.GetInt(ResHeightKey, 0));
            set
            {
                PlayerPrefs.SetInt(ResWidthKey, value.x);
                PlayerPrefs.SetInt(ResHeightKey, value.y);
                ApplyDisplay();
                Changed?.Invoke();
            }
        }

        /// <summary>The screen sizes the player can pick (the monitor's own list, each size once, smallest first).</summary>
        public static Vector2Int[] AvailableResolutions()
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 640 && !list.Contains(size)) list.Add(size);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list.ToArray();
        }

        /// <summary>Applies the saved fullscreen and resolution to the window now.</summary>
        public static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            var res = Resolution;
            int w = res.x > 0 ? res.x : Screen.width;
            int h = res.y > 0 ? res.y : Screen.height;
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(w, h, mode);
#endif
            // In the editor the Game view decides the size, so nothing is changed there.
        }

        // The saved display settings are applied once when the game starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnStartup()
        {
            if (PlayerPrefs.HasKey(FullscreenKey) || PlayerPrefs.HasKey(ResWidthKey)) ApplyDisplay();
        }
    }
}
