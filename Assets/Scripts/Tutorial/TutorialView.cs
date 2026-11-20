// ============================================================================
// TutorialView.cs
// Goes on the root of the TUTORIAL PREFAB (Assets/Resources/UI/Tutorial.prefab) and holds the
// links to its parts, so the artists can restyle, move or replace any of them in the prefab:
//   Objective Panel / Text   - the line at the top: what to do now
//   Card Panel               - the instruction card (Card Label, Card Text)
//   Done Badge / Done Text   - the tick that shows for a moment when a step is done
//   Stuck Text               - "Stuck? Press K to skip this step" (only after a long time on a step)
//   Guide Panel / Text       - the hints under the valve minigame during the guided first game
//   Skip Button              - "Skip tutorial", shown only while the pause menu is open
//   Death Panel              - the first collapse screen: Title, Lost / Kept / Next headings
//                              and texts, Continue button
// Every part is optional except the texts the code writes into; a part left empty is skipped.
//
// DESIGN RULES: none (UI).
//
// HOW TO USE: TutorialDirector loads the prefab by itself. If it doesn't exist yet the same
// look is built in code; Salok > Tutorial > Set Up Tutorial (or Tools > The Last Drop > Build
// UI Prefabs) saves that look as the prefab.
// HOW TO EDIT: the look in the prefab (placeholder style: black / white / grey, blue only as the
// water accent). The WORDS are in the Tutorial Texts asset, not here.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Links to the parts of the tutorial prefab.</summary>
    public class TutorialView : MonoBehaviour
    {
        /// <summary>Where the default prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/Tutorial";

        [Header("Objective line (top)")]
        [Tooltip("Shown while a main step is going.")] public GameObject objectivePanel;
        [Tooltip("The objective, e.g. \"Pump water: hold E at the hand pump.\"")] public TMP_Text objectiveText;

        [Header("Instruction card")]
        [Tooltip("Shown while a step or lesson has a card.")] public GameObject cardPanel;
        [Tooltip("The small word above the card (TUTORIAL).")] public TMP_Text cardLabel;
        [Tooltip("The instruction.")] public TMP_Text cardText;
        [Tooltip("\"Stuck? Press K to skip this step.\" Hidden until the player has been on a step a long time.")] public TMP_Text stuckText;

        [Header("Done tick")]
        [Tooltip("Shown for a moment when a step is done.")] public GameObject doneBadge;
        [Tooltip("The word next to the tick.")] public TMP_Text doneText;

        [Header("Minigame guide")]
        [Tooltip("Shown under the valve minigame during the guided first game.")] public GameObject guidePanel;
        public TMP_Text guideText;

        [Header("Pause menu")]
        [Tooltip("\"Skip tutorial\", shown only while the game is paused during the tutorial.")] public Button skipButton;
        public TMP_Text skipButtonLabel;

        [Header("First collapse screen")]
        [Tooltip("The whole screen (should block clicks).")] public GameObject deathPanel;
        public TMP_Text deathTitle;
        public TMP_Text lostHeading, lostText;
        public TMP_Text keptHeading, keptText;
        public TMP_Text nextHeading, nextText;
        public Button deathContinueButton;
        public TMP_Text deathContinueLabel;

        // ------------------------------------------------------------------ Default look

        static readonly Color Panel = new Color(0.04f, 0.04f, 0.04f, 0.88f);
        static readonly Color Grey = new Color(0.65f, 0.65f, 0.65f);
        static readonly Color ButtonGrey = new Color(0.16f, 0.16f, 0.16f);
        static readonly Color Water = new Color32(63, 169, 245, 255); // #3FA9F5, the water accent

        /// <summary>Builds the default black-and-white tutorial screen (used for the prefab, or when there is none).</summary>
        public static TutorialView BuildDefault(Transform parent)
        {
            // Above the HUD (9), the map (60) and menus; under the start card (80).
            var canvas = UiBuilder.CreateCanvas("Tutorial", parent, 70);
            var v = canvas.gameObject.AddComponent<TutorialView>();
            var root = canvas.transform;

            // Objective line: top centre.
            var obj = UiBuilder.Image("Objective Panel", root, Panel).rectTransform;
            UiBuilder.Place(obj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 64f));
            v.objectivePanel = obj.gameObject;
            var objBar = UiBuilder.Image("Accent", obj, Water).rectTransform;
            objBar.anchorMin = new Vector2(0f, 0f);
            objBar.anchorMax = new Vector2(0f, 1f);
            objBar.pivot = new Vector2(0f, 0.5f);
            objBar.offsetMin = Vector2.zero;
            objBar.offsetMax = new Vector2(6f, 0f);
            v.objectiveText = UiBuilder.Text("Objective Text", obj, "", 28f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Fill(v.objectiveText.rectTransform, 24f, 24f, 6f, 6f);

            // Done tick: under the objective.
            var done = UiBuilder.Image("Done Badge", root, Panel).rectTransform;
            UiBuilder.Place(done, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(220f, 52f));
            v.doneBadge = done.gameObject;
            v.doneText = UiBuilder.Text("Done Text", done, "Done", 30f, Water, TextAlignmentOptions.Center);
            v.doneText.fontStyle = FontStyles.Bold;

            // Instruction card: left middle.
            var card = UiBuilder.Image("Card Panel", root, Panel).rectTransform;
            UiBuilder.Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 110f), new Vector2(580f, 210f));
            v.cardPanel = card.gameObject;
            var bar = UiBuilder.Image("Accent", card, Color.white).rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(6f, 0f);
            v.cardLabel = UiBuilder.Text("Card Label", card, "TUTORIAL", 20f, Grey);
            v.cardLabel.fontStyle = FontStyles.Bold;
            UiBuilder.Fill(v.cardLabel.rectTransform, 24f, 18f, 14f, 0f);
            v.cardText = UiBuilder.Text("Card Text", card, "", 26f, Color.white);
            UiBuilder.Fill(v.cardText.rectTransform, 24f, 18f, 44f, 44f);
            v.stuckText = UiBuilder.Text("Stuck Text", card, "", 20f, Grey, TextAlignmentOptions.BottomLeft);
            UiBuilder.Fill(v.stuckText.rectTransform, 24f, 18f, 160f, 12f);

            // Minigame guide: bottom centre, under the valve panel.
            var guide = UiBuilder.Image("Guide Panel", root, Panel).rectTransform;
            UiBuilder.Place(guide, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1100f, 120f));
            v.guidePanel = guide.gameObject;
            v.guideText = UiBuilder.Text("Guide Text", guide, "", 28f, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Fill(v.guideText.rectTransform, 24f, 24f, 10f, 10f);

            // Pause menu: Skip tutorial, bottom centre.
            v.skipButton = UiBuilder.Button("Skip Tutorial Button", root, "Skip tutorial", 28f, ButtonGrey, Color.white, null);
            UiBuilder.Place((RectTransform)v.skipButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 64f));
            v.skipButtonLabel = v.skipButton.GetComponentInChildren<TMP_Text>(true);

            // First collapse screen.
            var death = UiBuilder.Image("Death Panel", root, new Color(0f, 0f, 0f, 0.94f), blocksClicks: true).rectTransform;
            v.deathPanel = death.gameObject;
            v.deathTitle = UiBuilder.Text("Title", death, "YOU COLLAPSED", 64f, Color.white, TextAlignmentOptions.Center);
            v.deathTitle.fontStyle = FontStyles.Bold;
            UiBuilder.Place(v.deathTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 90f));
            Column(death, -480f, "LOST", out v.lostHeading, out v.lostText);
            Column(death, 0f, "KEPT", out v.keptHeading, out v.keptText);
            Column(death, 480f, "NEXT TIME", out v.nextHeading, out v.nextText);
            v.deathContinueButton = UiBuilder.Button("Continue Button", death, "Continue", 32f, ButtonGrey, Color.white, null);
            UiBuilder.Place((RectTransform)v.deathContinueButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(360f, 70f));
            v.deathContinueLabel = v.deathContinueButton.GetComponentInChildren<TMP_Text>(true);

            v.objectivePanel.SetActive(false);
            v.doneBadge.SetActive(false);
            v.cardPanel.SetActive(false);
            v.guidePanel.SetActive(false);
            v.skipButton.gameObject.SetActive(false);
            v.deathPanel.SetActive(false);
            return v;
        }

        static void Column(RectTransform parent, float x, string heading, out TMP_Text head, out TMP_Text body)
        {
            var col = UiBuilder.Image(heading + " Column", parent, new Color(0.11f, 0.11f, 0.11f, 1f)).rectTransform;
            UiBuilder.Place(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 10f), new Vector2(440f, 420f));
            head = UiBuilder.Text("Heading", col, heading, 30f, heading == "KEPT" ? Water : Color.white, TextAlignmentOptions.Top);
            head.fontStyle = FontStyles.Bold;
            UiBuilder.Fill(head.rectTransform, 20f, 20f, 20f, 0f);
            body = UiBuilder.Text("Text", col, "", 25f, Color.white);
            UiBuilder.Fill(body.rectTransform, 28f, 28f, 80f, 24f);
            body.lineSpacing = 20f;
        }
    }
}
