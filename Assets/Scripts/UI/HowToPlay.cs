// ============================================================================
// HowToPlay.cs
// The start card. It pops up once the game scene has finished loading (New Game, Continue
// or Demo), freezes the game behind it (clock stopped, player can't move), and the player
// closes it with START (or Enter) to begin.
//
// 2026-11-19: cut down to the story and the goal only. Every mechanic is now taught in play,
// the first time the player meets it (TutorialHints.cs), so there is no long rules screen
// and H no longer reopens it. The old full text is in the GDD's Rules tab.
//
// DESIGN RULES: none of its own (the goal, R1).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: THE WORDS are in BuildText below (Title is the card's heading). THE LOOK is a
// prefab with its own Canvas: Assets/Resources/UI/HowToPlay.prefab (see HowToPlayView.cs;
// Tools > The Last Drop > Build UI Prefabs makes it). If it's missing, a plain
// black-and-white panel is built in code. The title and the key hint are set from here, so
// an old prefab still says the right thing.
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Shows the How to Play instructions after loading; closing them starts play.</summary>
    public class HowToPlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<HowToPlay>();

        /// <summary>Where the default prefab lives inside a Resources folder.</summary>
        public const string PrefabResource = "UI/HowToPlay";

        /// <summary>True while the instructions are on screen.</summary>
        public static bool IsOpen { get; private set; }

        GameObject root;
        TMP_Text bodyText;
        UnityEngine.UI.ScrollRect scroll;
        int openedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsOpen = false;

        void Awake()
        {
            var view = UiBuilder.InstantiateView<HowToPlayView>(null, PrefabResource, transform);
            if (view == null) view = HowToPlayView.BuildDefault(transform);
            root = view.panel != null ? view.panel : view.gameObject;
            bodyText = view.bodyText;
            if (bodyText != null) bodyText.alignment = TextAlignmentOptions.Top; // a short card reads better centred
            scroll = view.scroll;
            if (view.titleText != null) view.titleText.text = Title;
            if (view.hintText != null) view.hintText.text = "Enter   Start";
            if (view.startButton != null) view.startButton.onClick.AddListener(Close);
            root.SetActive(false);
        }

        IEnumerator Start()
        {
            // Wait for the loading screen to finish and every system to start (Continue loads
            // its save 3 frames in), then show the instructions over the ready game.
            while (LoadingScreen.IsLoading) yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            Open();
        }

        void OnDestroy()
        {
            IsOpen = false;
        }

        // ------------------------------------------------------------------ Open / close

        /// <summary>Shows the instructions and freezes the game behind them.</summary>
        public void Open()
        {
            if (IsOpen) return;
            if (bodyText != null) bodyText.text = BuildText(BalanceProvider.Current);
            root.SetActive(true);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f; // back to the top
            IsOpen = true;
            openedFrame = Time.frameCount;
            MainMenu.Freeze(); // clock stopped, cursor free, Esc / Tab / M / B do nothing
        }

        /// <summary>Hides the instructions and gives control to the player.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            IsOpen = false;
            MainMenu.Unfreeze();
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // Ignore the frame it opened on.
            if (Time.frameCount > openedFrame &&
                (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Close();
        }

        // ------------------------------------------------------------------ The words

        /// <summary>The card's heading.</summary>
        public const string Title = "SALOK";

        const string Blue = "<color=#4DA3FF>";
        const string End = "</color>";

        /// <summary>The start card text (TextMeshPro rich text): story and goal only.</summary>
        public static string BuildText(GameBalance b)
        {
            bool demo = b != null && b.isDemoMode;
            var s = new System.Text.StringBuilder();
            s.Append("\n<size=115%>El Niño has dried out the land, and you are trapped in a maze.</size>\n\n");
            if (demo)
                s.Append($"Reach the {Blue}outer wall of zone 1{End} and open one of its exit gates. Every gate opens only for water.\n\n");
            else
                s.Append($"Every gate opens only for water. {Blue}Find your way out, as early as you can.{End}\n\n");
            s.Append("Water is your life, your stamina and your money. Spend it wisely.\n");
            return s.ToString();
        }
    }
}
