// ============================================================================
// EscapePanel.cs
// The "You escaped on day N" panel. It appears when the player wins the valve minigame of
// one of zone 2's 8 exit gates, which is the game's win. In the playtest the Continue
// button just closes it so testing can go on.
//
// DESIGN RULES: R11.6 (opening an exit valve wins the game).
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// GateManager calls Show(day). It builds its own canvas when the game starts.
// HOW TO EDIT: colours and words are in the Inspector.
// ============================================================================

using TMPro;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Shows "You escaped on day N" with a Continue button.</summary>
    public class EscapePanel : MonoBehaviour
    {
        [Header("Text (what the player reads)")]
        [Tooltip("Big heading.")] public string title = "YOU ESCAPED!";
        [Tooltip("{0} = the day number.")] public string bodyFormat = "You escaped on day {0}.";
        [Tooltip("Button label.")] public string continueLabel = "Continue";

        [Header("Colours")]
        [Tooltip("Dims the game behind the panel.")] public Color dimColor = new Color(0f, 0f, 0f, 0.7f);
        [Tooltip("Panel background.")] public Color panelColor = new Color32(36, 31, 26, 255);
        [Tooltip("Heading colour.")] public Color titleColor = new Color32(63, 169, 245, 255);
        [Tooltip("Body text colour.")] public Color textColor = new Color32(242, 233, 220, 255);
        [Tooltip("Button colour.")] public Color buttonColor = new Color32(63, 169, 245, 255);
        [Tooltip("Button text colour.")] public Color buttonTextColor = new Color32(27, 23, 19, 255);

        /// <summary>True while the panel is on screen.</summary>
        public bool IsOpen => root != null && root.activeSelf;

        GameObject root;
        TextMeshProUGUI body;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas("EscapeCanvas", transform, 90);
            root = canvas.gameObject;
            UiBuilder.Image("Dim", root.transform, dimColor, blocksClicks: true);

            var panel = UiBuilder.Image("Panel", root.transform, panelColor, blocksClicks: true).rectTransform;
            UiBuilder.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 360f));

            var heading = UiBuilder.Text("Title", panel, title, 56f, titleColor, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            UiBuilder.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(660f, 80f));

            body = UiBuilder.Text("Body", panel, "", 32f, textColor, TextAlignmentOptions.Center);
            UiBuilder.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(660f, 60f));

            var button = UiBuilder.Button("Continue", panel, continueLabel, 30f, buttonColor, buttonTextColor, Hide);
            UiBuilder.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(260f, 64f));

            root.SetActive(false);
        }

        /// <summary>Shows the panel for an escape on <paramref name="day"/>.</summary>
        public void Show(int day)
        {
            body.text = string.Format(bodyFormat, day);
            root.SetActive(true);
            MenuState.Enter(); // free the cursor so Continue can be clicked
            Debug.Log($"[EscapePanel] The player escaped on day {day} (R11.6).", this);
        }

        /// <summary>Closes the panel and gives control back (the Continue button).</summary>
        public void Hide()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            MenuState.Exit();
        }
    }
}
