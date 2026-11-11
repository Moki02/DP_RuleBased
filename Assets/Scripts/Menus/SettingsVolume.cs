// ============================================================================
// SettingsVolume.cs
// Makes an AudioSource follow the Settings screen's Music or SFX volume.
//
// DESIGN RULES: none.
//
// HOW TO USE: add it next to an AudioSource (music, ambience, a sound effect) and pick
// "Kind". Set the AudioSource's own volume as usual: that is the volume at 100%.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Scales this AudioSource by the player's Music or SFX volume setting.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class SettingsVolume : MonoBehaviour
    {
        /// <summary>Which setting this sound follows.</summary>
        public enum Kind { Music, Sfx }

        [Tooltip("Music follows the Music slider; Sfx follows the SFX slider.")]
        public Kind kind = Kind.Sfx;

        AudioSource source;
        float baseVolume;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
        }

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply()
        {
            if (source == null) return;
            source.volume = baseVolume * (kind == Kind.Music ? GameSettings.MusicVolume : GameSettings.SfxVolume);
        }
    }
}
