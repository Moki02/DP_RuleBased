// ============================================================================
// SceneFeel.cs
// One sound for a whole scene: the main menu ambience (loops), or the Win / Game Over
// sting (plays once when the scene opens).
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: on the "GameFeel" object in MainMenu, Win and GameOver (Salok > Feel > Install
// Game Feel sets which sound). Works while the game is frozen (time scale 0).
// HOW TO EDIT: pick the sound in "Which"; clips and volume in GameFeelLibrary.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Plays a scene's ambience loop or opening sting.</summary>
    public class SceneFeel : MonoBehaviour
    {
        /// <summary>Which sound this scene plays.</summary>
        public enum Which { MainMenuAmbience, WinSting, GameOverSting }

        [Tooltip("Which sounds to use.")]
        public GameFeelLibrary library;
        [Tooltip("What this scene plays.")]
        public Which which = Which.MainMenuAmbience;
        [Tooltip("Seconds to wait before the sting (lets the scene appear first).")]
        [Min(0f)] public float delay = 0.2f;

        FeelLoop loop;
        float wait;
        bool played;

        void Start()
        {
            if (library == null) { enabled = false; return; }
            if (which == Which.MainMenuAmbience)
                loop = new FeelLoop(gameObject, library.ambienceMenu, music: true) { fadeIn = 2f, fadeOut = 1f };
            wait = delay;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (loop != null) { loop.Update(true, dt); return; }
            if (played) return;
            wait -= dt;
            if (wait > 0f) return;
            played = true;
            FeelAudio.Play(which == Which.WinSting ? library.winSting : library.gameOverSting);
        }
    }
}
