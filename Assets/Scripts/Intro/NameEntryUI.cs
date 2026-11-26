// ============================================================================
// NameEntryUI.cs
// The "What is your name?" panel shown on every New Game from the main menu, before the
// intro. The panel is a normal part of the MainMenu scene's Canvas (Salok > Intro > Add Name
// Entry To Main Menu places it once), so every element can be changed in the Hierarchy /
// Inspector.
//   - The input box is pre-filled with the last name used.
//   - Begin (or Enter in the box) cleans the name (trim, 16 characters, "Survivor" if empty),
//     saves it (PlayerProfile) and starts the new game with the intro.
//   - Esc closes the panel and shows the main buttons again.
//
// DESIGN RULES: none (name entry).
//
// HOW TO USE: on the Name Entry panel, with the fields below dragged in. MenuSceneUI.NewGame
// opens it. Begin's On Click is set by the script (no need to add it in the Inspector).
// HOW TO EDIT: the look is in the scene. The length limit is PlayerProfile.MaxLength.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>The Name Entry panel: a name box and a Begin button.</summary>
    public class NameEntryUI : MonoBehaviour
    {
        [Tooltip("The name box.")]
        public TMP_InputField nameInput;
        [Tooltip("Starts the new game with the typed name.")]
        public Button beginButton;
        [Tooltip("Shown again when the panel is closed with Esc (the main menu's buttons). Optional.")]
        public GameObject returnTo;

        bool wired, started;

        void Awake() => Wire();

        void Wire()
        {
            if (wired) return;
            wired = true;
            if (nameInput != null)
            {
                nameInput.characterLimit = PlayerProfile.MaxLength;
                nameInput.lineType = TMP_InputField.LineType.SingleLine;
                nameInput.onSubmit.AddListener(_ => Begin());
            }
            if (beginButton != null) beginButton.onClick.AddListener(Begin);
        }

        /// <summary>Shows the panel with the last name used, ready to type.</summary>
        public void Open()
        {
            Wire();
            started = false;
            if (returnTo != null) returnTo.SetActive(false);
            gameObject.SetActive(true);
            if (nameInput != null)
            {
                nameInput.SetTextWithoutNotify(PlayerProfile.LastUsedName);
                nameInput.Select();
                nameInput.ActivateInputField();
                nameInput.MoveTextEnd(false);
            }
        }

        /// <summary>Hides the panel and shows the main buttons again.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
            if (returnTo != null) returnTo.SetActive(true);
        }

        /// <summary>Saves the name and starts the new game with the intro.</summary>
        public void Begin()
        {
            if (started) return; // Enter and the button in the same frame
            started = true;
            PlayerProfile.SetName(nameInput != null ? nameInput.text : "");
            SceneFlow.StartNewGameWithIntro();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (!started && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }
    }
}
