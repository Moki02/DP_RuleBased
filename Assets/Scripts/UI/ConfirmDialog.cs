// ============================================================================
// ConfirmDialog.cs
// The one "Are you sure?" box with Yes and No, used before anything that can't be undone:
// Quit, Save and quit to menu, New Game over an existing save, and Restart (Play Again).
//
//   ConfirmDialog.Ask("Quit the game?", () => SceneFlow.Quit());
//
// THE LOOK IS A PREFAB WITH ITS OWN CANVAS: Assets/Resources/UI/ConfirmDialog.prefab. A copy
// is placed (hidden) in every scene under "UI > Confirm Dialog"; that copy is used first, so it
// can be edited in the scene. If a scene has none, the prefab is copied in from Resources.
// It draws above everything else (sort order 300) and works while the game is paused (time
// scale 0). Esc or the No button closes it without doing anything.
//
// DESIGN RULES: none of its own (T42).
//
// HOW TO USE: call ConfirmDialog.Ask from code. From a Button's On Click list, use the
// ConfirmActions component instead (it has ready-made NewGame / Restart / Quit methods).
// HOW TO EDIT: open the prefab (or the scene copy). "Message" gets the question; "Yes Button"
// and "No Button" are linked below. The words on the buttons are plain text in the prefab.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>A modal Yes / No box. Call <see cref="Ask"/>.</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        /// <summary>Where the prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/ConfirmDialog";

        [Tooltip("The part that is shown and hidden (the dim background and the box). Empty = this object.")]
        public GameObject panel;
        [Tooltip("Gets the question, e.g. \"Quit the game?\".")]
        public TMP_Text messageText;
        [Tooltip("Runs the action and closes the box.")]
        public Button yesButton;
        [Tooltip("Closes the box without doing anything (Esc does the same).")]
        public Button noButton;

        static ConfirmDialog instance;
        Action onYes, onNo;
        int openedFrame;

        /// <summary>True while the box is on screen.</summary>
        public static bool IsOpen => instance != null && instance.Panel.activeInHierarchy;

        GameObject Panel => panel != null ? panel : gameObject;

        /// <summary>Shows the box with <paramref name="message"/>. Yes runs <paramref name="yes"/>; No (or Esc) runs <paramref name="no"/> if given.</summary>
        public static void Ask(string message, Action yes, Action no = null)
        {
            var d = Get();
            if (d == null) { yes?.Invoke(); return; } // no dialog at all: don't block the player
            d.onYes = yes;
            d.onNo = no;
            if (d.messageText != null) d.messageText.text = message;
            d.Panel.SetActive(true);
            d.openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static ConfirmDialog Get()
        {
            if (instance != null) return instance;
            instance = UiBuilder.FindInScene<ConfirmDialog>();
            if (instance == null)
            {
                var prefab = Resources.Load<GameObject>(PrefabResource);
                if (prefab == null)
                {
                    Debug.LogWarning("[ConfirmDialog] No Confirm Dialog in the scene and no prefab at Resources/" + PrefabResource + ": the action runs without asking.");
                    return null;
                }
                var go = Instantiate(prefab);
                go.name = "Confirm Dialog";
                instance = go.GetComponentInChildren<ConfirmDialog>(true);
            }
            if (instance == null) return null;
            instance.Wire();
            instance.Panel.SetActive(false);
            return instance;
        }

        void Wire()
        {
            if (yesButton != null) { yesButton.onClick.RemoveListener(Yes); yesButton.onClick.AddListener(Yes); }
            if (noButton != null) { noButton.onClick.RemoveListener(No); noButton.onClick.AddListener(No); }
        }

        void Update()
        {
            if (!Panel.activeInHierarchy || Time.frameCount == openedFrame) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) No();
        }

        /// <summary>The Yes button.</summary>
        public void Yes()
        {
            var a = onYes;
            Close();
            a?.Invoke();
        }

        /// <summary>The No button (and Esc).</summary>
        public void No()
        {
            var a = onNo;
            Close();
            a?.Invoke();
        }

        void Close()
        {
            onYes = onNo = null;
            Panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
