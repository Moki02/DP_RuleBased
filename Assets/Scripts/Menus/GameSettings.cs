// ============================================================================
// GameSettings.cs
// The player's settings from the main menu's Settings screen, kept between sessions
// (PlayerPrefs):
//   - Music volume and SFX volume (0-1). There is no audio in the game yet: when the artists
//     add sounds, put a SettingsVolume component next to each AudioSource and pick Music or
//     SFX; it scales that source by the setting (live, also while the game runs).
//   - Mouse sensitivity (0.25×-3×, 1 = the camera's normal speed). Player_Camera reads it.
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
    }
}
