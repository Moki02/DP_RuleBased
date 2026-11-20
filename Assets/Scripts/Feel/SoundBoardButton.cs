// ============================================================================
// SoundBoardButton.cs
// One button on the FeelLab sound board. Click it to hear its sound. Loops (ambience,
// boiling, rain) start on the first click and stop on the second.
//
// DESIGN RULES: none (FeelLab tool only; not used in the game).
//
// HOW TO USE: made by Salok > Feel > Refresh Sound Board, one per file in Assets/Audio.
// You can also add one by hand: put it on a Button, drag in a clip, and add Play() to the
// Button's On Click list.
// HOW TO EDIT: tick "Loop" to make it a start/stop button. Volume is 0..1.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Plays (or toggles) one sound on the FeelLab sound board.</summary>
    public class SoundBoardButton : MonoBehaviour
    {
        [Tooltip("The sound this button plays.")]
        public AudioClip clip;
        [Tooltip("Loop: first click starts it, second click stops it (ambience, boiling, rain).")]
        public bool loop;
        [Tooltip("Volume 0..1.")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Background image that lights up while a loop is playing (optional).")]
        public Image background;
        [Tooltip("Colour of the background while the loop plays.")]
        public Color playingColor = new Color(0.25f, 0.55f, 0.85f);
        [Tooltip("Text that shows the file name and length (optional).")]
        public TMP_Text label;

        AudioSource loopSource;
        Color idleColor;

        /// <summary>Is this button's loop playing?</summary>
        public bool IsPlaying => loopSource != null && loopSource.isPlaying;

        void Awake()
        {
            if (background != null) idleColor = background.color;
        }

        /// <summary>Plays the sound (or starts / stops the loop).</summary>
        public void Play()
        {
            if (clip == null) return;
            if (!loop)
            {
                SoundBoard.PlayOneShot(clip, volume, this);
                return;
            }
            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
                loopSource.spatialBlend = 0f;
            }
            if (loopSource.isPlaying) Stop();
            else
            {
                loopSource.clip = clip;
                loopSource.volume = volume * SoundBoard.MasterVolume;
                loopSource.Play();
                if (background != null) background.color = playingColor;
                SoundBoard.ShowNowPlaying(this);
            }
        }

        /// <summary>Stops the loop if it is playing.</summary>
        public void Stop()
        {
            if (loopSource != null) loopSource.Stop();
            if (background != null) background.color = idleColor;
        }

        /// <summary>Keeps a playing loop in step with the board's volume slider.</summary>
        public void RefreshVolume()
        {
            if (loopSource != null) loopSource.volume = volume * SoundBoard.MasterVolume;
        }
    }
}
