// ============================================================================
// HowToPlay.cs
// The How to Play screen. It pops up once the game scene has finished loading (New Game,
// Continue or Demo), freezes the game behind it (clock stopped, player can't move), and the
// player closes it with START (or Enter) to begin. H opens it again at any time while
// exploring.
//
// The words below explain the goal and every system the player needs, in the order they
// meet them. Numbers that live in GameBalance (lock litres, day length, lockdown drain,
// starting water) are read from it, so the text stays right after a retune and in Demo mode.
//
// DESIGN RULES: none of its own; it summarises the Rules tab (R1 to R11, section 13 in Demo).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: THE WORDS are in BuildText below. THE LOOK is a prefab with its own Canvas:
// Assets/Resources/UI/HowToPlay.prefab (see HowToPlayView.cs; Tools > The Last Drop >
// Build UI Prefabs makes it). If it's missing, a plain black-and-white panel is built in code.
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
            scroll = view.scroll;
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
            var kb = Keyboard.current;
            if (kb == null) return;

            if (IsOpen)
            {
                // Ignore the frame it opened on (the H press that opened it).
                if (Time.frameCount > openedFrame &&
                    (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    Close();
                return;
            }

            // H shows it again, only during normal play (not over a menu, minigame or pause).
            var gm = Game_Manager.instance;
            if (kb.hKey.wasPressedThisFrame && gm != null && gm.CanControlPlayer() && !MainMenu.IsFrozen)
                Open();
        }

        // ------------------------------------------------------------------ The words

        const string Blue = "<color=#4DA3FF>";
        const string End = "</color>";

        static string H(string title) => $"\n<size=120%><b>{title}</b></size>\n";

        /// <summary>The full instructions text (TextMeshPro rich text).</summary>
        public static string BuildText(GameBalance b)
        {
            bool demo = b != null && b.isDemoMode;
            string L(int zone) => b != null && b.lockLiters != null && b.lockLiters.Length > 0
                ? b.lockLiters[Mathf.Clamp(zone, 0, b.lockLiters.Length - 1)].ToString("0.#") + " L"
                : (zone == 0 ? "4 L" : zone == 1 ? "10 L" : "18 L");
            float dayLen = b != null ? b.dayLengthSeconds : 240f;
            float night = b != null ? b.nightLockdownSeconds : 60f;
            float lockDrain = b != null ? b.lockdownDrain : 4f;
            float startClean = b != null ? b.startClean : 10f;
            string minutes = ((dayLen + night) / 60f).ToString("0.#");

            var s = new System.Text.StringBuilder();

            s.Append(H("YOUR GOAL"));
            if (demo)
            {
                s.Append($"El Niño has dried out the land and you are trapped in a maze. {Blue}Escape it.{End}\n");
                s.Append($"In this demo, reach the {Blue}outer wall of zone 1{End}, fill one of its exit lock basins with {Blue}{L(1)}{End} of water, " +
                         "win the exit valve minigame, then walk out through the gate.\n");
                s.Append("Days are short in the demo and the drought gets worse every day, so don't waste time.\n");
            }
            else
            {
                s.Append($"El Niño has dried out the land and you are trapped in a maze. {Blue}Escape it, as early as you can.{End}\n");
                s.Append("The maze has 3 rings: your hub in the middle, zone 1 around it, zone 2 around that. Each ring is closed by gates.\n");
                s.Append($"To pass a gate you pour water into its {Blue}lock basin{End}: hub gates need {Blue}{L(0)}{End}, " +
                         $"zone 1 gates {Blue}{L(1)}{End}, zone 2 gates {Blue}{L(2)}{End}. Then you win its valve minigame.\n");
                s.Append($"Zone 2's outer gates are the {Blue}exits{End}. Open one and walk out to win. The fewer days it takes, the better your ending.\n");
            }

            s.Append(H("WATER IS EVERYTHING"));
            s.Append($"Water is your health, your stamina and your money. Every liter you spend is a liter you can't drink.\n");
            s.Append($"• {Blue}Hydration (HYD){End}: your body, 0 to 100. It drains every second, faster when you walk or sprint, and faster as the drought gets worse. At 0 you collapse.\n");
            s.Append("   60+ Hydrated: normal.   30-59 Thirsty: sprinting costs more.   1-29 Dehydrated: you can't sprint and you walk slower.\n");
            s.Append($"• {Blue}Canteen{End}: clean water you carry. Hold Q to drink it (1 L = 25 HYD). You also pour it into lock basins.\n");
            s.Append($"• {Blue}Raw Basin{End} (hub): dirty water from your sources. Not safe to drink: raw water makes you Sick (you drain faster for a while).\n");
            s.Append($"• {Blue}Clean Storage{End} (hub): safe water. You start with {startClean:0.#} L. You drink from it, refill your canteen from it, and buy every upgrade with it.\n");
            s.Append($"• {Blue}Unbanked water{End}: water you earn in the maze (exploring, map rewards, opening gates). It only counts once you carry it home: walk into the hub to bank it. Get home before night for a +10% Safe Return bonus.\n");

            s.Append(H("THE HUB: MAKE WATER"));
            s.Append($"1. Stand at the {Blue}hand pump{End} and hold E. Each stroke puts water in the Raw Basin.\n");
            s.Append($"2. The {Blue}boiling pot{End} cleans raw water on its own and moves it into Clean Storage. No button.\n");
            s.Append($"3. At the {Blue}Clean Storage tap{End}, hold E to drink or to refill your canteen.\n");
            s.Append("4. Inside the hub you're in the shade: you get thirsty half as fast.\n");
            s.Append("Every label above a hub object tells you how much it holds and why it's slow or stopped.\n");

            s.Append(H("BUILD AND SKILLS: SPEND WATER TO GROW"));
            s.Append($"Press {Blue}Tab{End} (or {Blue}B{End}) to open Build and Skills. You can only buy while you're in the hub; the clock stops while this screen is open there.\n");
            s.Append($"• {Blue}Build{End}: new water sources (Rain Catcher, Solar Still, Deep Well), a faster purifier (Biosand Filter), bigger hub storage and a bigger canteen.\n");
            s.Append($"• {Blue}Skills{End}: lose water slower, move faster, see more of the map.\n");
            s.Append("Purchases are permanent. You can't buy anything bigger than your Clean Storage can hold, so upgrade storage first when the tooltip says \"Needs bigger storage\".\n");

            s.Append(H("THE DROUGHT GETS WORSE"));
            s.Append("The drought moves through 4 stages: El Niño Advisory, Dry Spell, Drought, then State of Calamity. Each stage makes you thirstier, makes rain rarer and makes stored water evaporate faster.\n");
            s.Append($"{Blue}Warning:{End} the water table keeps dropping. By State of Calamity the hand pump runs dry. " +
                     "Build another source before then (Rain Catcher, Solar Still, or the Deep-Set Cylinder to keep the pump working).\n");
            s.Append($"Once a day something happens: a {Blue}Thunderstorm{End} (rain fills your Rain Catcher and you drain slower) or a {Blue}Heatwave{End} (you drain much faster). Watch the banner.\n");

            s.Append(H("THE MAZE"));
            s.Append($"• Press {Blue}M{End} for the map. It starts black and fills in as you walk and look around. Newly seen tiles and mapped sections give you water.\n");
            s.Append($"• Press {Blue}F{End} to drop a marker where you stand (F again on it removes it).\n");
            s.Append($"• {Blue}Rooms{End} hide survivor notes, sealed water jugs and cisterns. Notes reveal parts of the map or give hints. Press {Blue}J{End} on the map to read your journal.\n");
            s.Append($"• {Blue}Valve gates{End} are shortcuts. Press E and play the minigame: press {Blue}Space{End} when the needle is in the green band. 3 hits wins, 3 misses loses (5 HYD and a short wait).\n");
            s.Append($"• {Blue}Lock basins{End}: hold E to pour from your canteen. Poured water stays there between trips. When it's full, win the valve minigame and the gate opens for good. " +
                     "If your canteen runs dry, you pour from your own HYD (down to 10).\n");
            if (!demo)
                s.Append($"• {Blue}Zone 2 turns!{End} Every second night it rotates 90° clockwise. The map turns with it and shows the new direction, so check it.\n");

            s.Append(H("NIGHT LOCKDOWN"));
            s.Append($"A day lasts about {minutes} minutes. Each night every gate and door shuts for {night:0} seconds.\n");
            s.Append($"Warnings show at 60, 40, 20, 10 and 5 seconds. If you're caught outside the hub or a room, you lose {Blue}{lockDrain:0.#} HYD every second{End}, which usually means you collapse.\n");
            s.Append($"{Blue}Be home, or inside a room, before night.{End} Opened gates open again in the morning.\n");

            s.Append(H("COLLAPSING"));
            s.Append("If HYD hits 0 you collapse and wake up at the hub. You lose the water in your canteen, half your unbanked water and some time, and you drink 2 L from Clean Storage to wake up.\n");
            s.Append($"{Blue}If Clean Storage has less than 2 L when you wake, the drought wins: game over.{End} Always keep a little water at home.\n");
            if (!demo) s.Append("The game saves itself at the start of every day. Continue on the main menu picks up from there.\n");

            s.Append(H("CONTROLS"));
            s.Append("W A S D  Move          Shift (hold)  Sprint          Mouse  Look around\n");
            s.Append("E (hold)  Interact: pump, tap, pour, notes, gates\n");
            s.Append("Q (hold)  Drink from the canteen\n");
            s.Append("Tab / B  Build and Skills          M  Map          J  Journal (on the map)          F  Map marker\n");
            s.Append("Space  Valve minigame          Esc  Pause          H  Show this screen again\n");

            s.Append(H("TIPS"));
            s.Append("• Plan every trip: how far can you go and still get home with water to spare?\n");
            s.Append("• Upgrade your canteen early. Lock basins take a lot of water and it all comes from what you carry.\n");
            s.Append("• Buy a second water source before the pump dries up.\n");
            s.Append("• Open valve gates: they are shortcuts that make every later trip cheaper.\n");
            s.Append("• Don't drink raw water unless you have to.\n");
            s.Append("• When the night warning shows, turn back.\n");

            return s.ToString();
        }
    }
}
