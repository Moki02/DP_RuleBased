// ============================================================================
// SoundBoard.cs
// The FeelLab sound board: plays one-shot sounds for the buttons, shows what is playing,
// and has "Stop all" and a volume slider. For listening and choosing only; nothing here is
// used by the game.
//
// DESIGN RULES: none (FeelLab tool only).
//
// HOW TO USE: open Assets/Scenes/FeelLab.unity and press Play, then click buttons.
// New sounds in Assets/Audio? Run Salok > Feel > Refresh Sound Board with FeelLab open.
// HOW TO EDIT: the board's look is ordinary UI in the FeelLab scene (Canvas > SoundBoard).
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Plays sounds for the FeelLab sound board buttons.</summary>
    public class SoundBoard : MonoBehaviour
    {
        static SoundBoard instance;
        static float masterVolume = 1f;

        [Tooltip("Plays the one-shot sounds (2D).")]
        public AudioSource oneShotSource;
        [Tooltip("Text that shows the last sound played (optional).")]
        public TMP_Text nowPlaying;
        [Tooltip("Volume slider 0..1 (optional). Its On Value Changed should call SoundBoard.SetVolume.")]
        public Slider volumeSlider;
        [Tooltip("All buttons on the board (filled in by Refresh Sound Board).")]
        public SoundBoardButton[] buttons = new SoundBoardButton[0];
        [Tooltip("Where Refresh Sound Board puts the headers and buttons (the scroll list content).")]
        public RectTransform content;

        /// <summary>Board volume 0..1.</summary>
        public static float MasterVolume => masterVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; masterVolume = 1f; }

        void Awake()
        {
            instance = this;
            if (oneShotSource == null) oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(masterVolume);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Plays a one-shot sound through the board.</summary>
        public static void PlayOneShot(AudioClip clip, float volume, SoundBoardButton from)
        {
            if (instance == null || clip == null) return;
            instance.oneShotSource.PlayOneShot(clip, volume * masterVolume);
            ShowNowPlaying(from);
        }

        /// <summary>Writes the clip name and length in the "now playing" text.</summary>
        public static void ShowNowPlaying(SoundBoardButton from)
        {
            if (instance == null || instance.nowPlaying == null || from == null || from.clip == null) return;
            var c = from.clip;
            instance.nowPlaying.text = c.name + "   " + c.length.ToString("0.0") + " s" +
                                       (c.channels == 1 ? "   mono" : "   stereo") + (from.loop ? "   (loop)" : "");
        }

        /// <summary>Stops every sound and loop on the board.</summary>
        public void StopAll()
        {
            oneShotSource.Stop();
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].Stop();
        }

        /// <summary>Sets the board volume (0..1).</summary>
        public void SetVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].RefreshVolume();
        }
    }
}
