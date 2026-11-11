// ============================================================================
// EndSceneUI.cs
// The Win and GameOver scenes' buttons and text. The screen itself is a normal Canvas saved
// in each scene (Tools > The Last Drop > Build Menu Scenes makes them once), so the artists
// can change every background, text, button, font, colour and position in the Inspector.
//
// What this script does:
//   - fills "Title Text" with the ending title (e.g. "SURVIVOR", "THE DROUGHT WINS") and
//     "Body Text" with the day line and the run's stats, from SceneFlow. (Played on its own,
//     the scene keeps whatever the texts say in the Inspector.)
//   - the buttons call PlayAgain, MainMenu or Quit (set in each Button's On Click list).
//     Play Again starts a new run, or a new Demo run if the last one was a demo.
//
// DESIGN RULES: R11.4 (game over), R11.6 / R11.7 (win screen with stats), R13.4 (Demo complete).
//
// HOW TO USE: on the screen object in the Win or GameOver scene, with the two texts dragged in.
// HOW TO EDIT: everything you see is in the Canvas.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TheLastDrop
{
    /// <summary>Fills the Win / GameOver texts and runs their buttons (the look lives in the scene's Canvas).</summary>
    public class EndSceneUI : MonoBehaviour
    {
        /// <summary>Which end this scene shows.</summary>
        public enum Kind { Win, GameOver }

        [Tooltip("Win = the escape screen. GameOver = the collapse screen.")]
        public Kind kind = Kind.Win;

        [Tooltip("Gets the ending title (\"SURVIVOR\", \"THE DROUGHT WINS\" ...).")]
        public TMP_Text titleText;
        [Tooltip("Gets the day line and the run's stats.")]
        public TMP_Text bodyText;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (titleText != null && !string.IsNullOrEmpty(SceneFlow.EndTitle)) titleText.text = SceneFlow.EndTitle;
            if (bodyText != null && !string.IsNullOrEmpty(SceneFlow.EndBody)) bodyText.text = SceneFlow.EndBody;
        }

        /// <summary>Play Again: a new run (a new demo after a demo).</summary>
        public void PlayAgain() =>
            SceneFlow.StartGame(SceneFlow.EndWasDemo ? SceneFlow.StartMode.Demo : SceneFlow.StartMode.NewGame);

        /// <summary>Back to the main menu.</summary>
        public void MainMenu() => SceneFlow.ToMainMenu();

        /// <summary>Closes the game.</summary>
        public void Quit() => SceneFlow.Quit();
    }
}
