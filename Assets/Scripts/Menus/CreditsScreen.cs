// ============================================================================
// CreditsScreen.cs
// The Credits screen (T46), opened by the Credits button on the main menu and on the Win
// screen. Its text comes from a text file, so it can be changed without touching code:
//     Assets/_Project/Docs/credits.txt
// Lines starting with "# " are shown as section headings (bold, bigger, coloured);
// everything else is shown as it is.
//
// DESIGN RULES: none (T46).
//
// HOW TO USE: the prefab Assets/Prefabs/UI/Credits.prefab is placed (hidden) in the MainMenu
// and Win scenes under "UI". A Button calls CreditsScreen > Open in its On Click list; the
// screen's Back button (or Esc) closes it.
// HOW TO EDIT: the words: edit credits.txt. The look: open the prefab (background, title,
// scroll area, Back button). Heading size and colour are below.
// ============================================================================

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Shows the credits text file in a scrolling panel.</summary>
    public class CreditsScreen : MonoBehaviour
    {
        [Tooltip("The credits text file (Assets/_Project/Docs/credits.txt).")]
        public TextAsset creditsFile;
        [Tooltip("The part that is shown and hidden. Empty = this object's first child.")]
        public GameObject panel;
        [Tooltip("Gets the credits text.")]
        public TMP_Text bodyText;
        [Tooltip("Optional: scrolled back to the top every time the screen opens.")]
        public ScrollRect scroll;
        [Tooltip("Closes the screen.")]
        public Button backButton;

        [Header("Headings (lines starting with \"# \")")]
        [Tooltip("Heading size as a share of the body text size.")]
        public float headingScale = 1.4f;
        public Color headingColor = new Color(0.96f, 0.80f, 0.45f);

        GameObject Panel => panel != null ? panel : (transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject);

        int openedFrame;

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Close);
            if (Panel != gameObject) Panel.SetActive(false);
        }

        /// <summary>Shows the credits.</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            Panel.SetActive(true);
            if (bodyText != null) bodyText.text = Format(creditsFile != null ? creditsFile.text : "credits.txt is missing.");
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            openedFrame = Time.frameCount;
        }

        /// <summary>Hides the credits.</summary>
        public void Close() => Panel.SetActive(false);

        void Update()
        {
            if (!Panel.activeInHierarchy || Time.frameCount == openedFrame) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.IsOpen) Close();
        }

        string Format(string raw)
        {
            var sb = new StringBuilder();
            string colour = ColorUtility.ToHtmlStringRGB(headingColor);
            foreach (var line in raw.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith("# "))
                    sb.Append($"\n<b><size={Mathf.RoundToInt(headingScale * 100f)}%><color=#{colour}>{line.Substring(2).ToUpperInvariant()}</color></size></b>\n");
                else
                    sb.Append(line).Append('\n');
            }
            return sb.ToString().Trim('\n');
        }
    }
}
