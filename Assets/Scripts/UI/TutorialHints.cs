// ============================================================================
// TutorialHints.cs
// Teaches the game while it's played, instead of one long How to Play screen. Each hint is
// a short card shown ONCE per run, the first time the player meets that thing (the pump,
// the first lock basin, the first evening ...). One card at a time, the rest wait in line.
//
//   - A card waits while a menu, minigame, pause or the start screen is up, then carries on.
//   - A card closes early once the player has done what it teaches (walked, drank, opened
//     the map ...). A hint whose lesson is already done before it shows is skipped.
//   - Hints already shown are saved (SaveData.Tutorial.cs), so Continue doesn't repeat them.
//
// Things the game already announces on its own are NOT repeated here: E prompts on every
// interactable, night countdown toasts, drought stage banners, event banners, collapse banner.
//
// DESIGN RULES: none of its own; it replaces the How to Play text (summary of R1 to R11).
// Plan: Project doc "claude/Tutorial hints plan.md".
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: THE WORDS AND TRIGGERS are in BuildHints below, in the order players usually
// meet them. THE LOOK (position, size, colours) is in the inspector fields; the card is built
// in code. Untick "Show Hints" to switch them all off.
// ============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Shows one-time hints the first time the player meets each mechanic.</summary>
    public class TutorialHints : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<TutorialHints>();

        [Header("Hints")]
        [Tooltip("Untick to switch every hint off.")]
        public bool showHints = true;
        [Tooltip("How long a hint stays up if the player doesn't do what it says (seconds).")]
        [Min(2f)] public float defaultSeconds = 8f;
        [Tooltip("A hint stays at least this long, even once its lesson is done (seconds).")]
        [Min(0.5f)] public float minSeconds = 2.5f;
        [Tooltip("Pause between two hints (seconds).")]
        [Min(0f)] public float gapSeconds = 0.75f;
        [Tooltip("How close (metres) the player must be to an object for its hint.")]
        [Min(1f)] public float nearRange = 5f;

        [Header("Look")]
        [Tooltip("Where the card sits, from the left-middle of the screen (1920 x 1080 canvas).")]
        public Vector2 cardPosition = new Vector2(24f, 120f);
        public Vector2 cardSize = new Vector2(560f, 150f);
        public Color cardColor = new Color(0.06f, 0.05f, 0.04f, 0.88f);
        public Color labelColor = new Color32(63, 169, 245, 255);
        public Color textColor = new Color32(242, 233, 220, 255);
        [Tooltip("The small word above each hint.")]
        public string label = "TIP";

        // ------------------------------------------------------------------ One hint

        class Hint
        {
            public string id;
            public string text;
            public Func<bool> showIf;      // true = ready to show
            public Func<bool> done;       // true = the player has done it (closes early); may be null
            public float seconds;         // 0 = defaultSeconds
            public bool demoToo = true;   // false = not in Demo mode
        }

        readonly List<Hint> hints = new List<Hint>();
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Dictionary<string, float> shownAt = new Dictionary<string, float>(); // game time each hint first showed
        readonly List<Hint> queue = new List<Hint>();

        Hint current;
        float currentFor;   // seconds the current card has been on screen
        float gapLeft;
        float checkTimer;

        // Facts the triggers need, tracked over time.
        Vector3 startPos;
        bool hasStartPos;
        float leftHubAt = -1f;
        bool wasCollapsed;
        int collapses;
        int zone2Turns;
        float lastHydSeen = 100f;

        Zone2Rotator rotator;
        MapScreen mapScreen;
        GameMenu gameMenu;

        // The card
        GameObject cardRoot;
        CanvasGroup cardGroup;
        TMP_Text cardText;

        // ------------------------------------------------------------------ Save (ISaveable)

        public int RestoreOrder => RestoreOrders.Menu + 1;

        public void Capture(SaveData data)
        {
            data.hasTutorial = true;
            data.hintsSeen = new List<string>(seen);
        }

        public void Restore(SaveData data)
        {
            seen.Clear();
            queue.Clear();
            HideCard();
            if (data.hasTutorial && data.hintsSeen != null)
            {
                foreach (var id in data.hintsSeen) seen.Add(id);
            }
            else if (data.day > 1)
            {
                // A save from before hints existed, past day 1: the player knows the basics.
                foreach (var h in hints) seen.Add(h.id);
            }
        }

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            BuildHints();
            BuildCard();
        }

        void Start()
        {
            rotator = FindAnyObjectByType<Zone2Rotator>();
            if (rotator != null) rotator.Zone2Turned += OnZone2Turned;
            mapScreen = FindAnyObjectByType<MapScreen>();
            gameMenu = FindAnyObjectByType<GameMenu>();
        }

        void OnDestroy()
        {
            if (rotator != null) rotator.Zone2Turned -= OnZone2Turned;
        }

        void OnZone2Turned(int from, int to) => zone2Turns++;

        // ------------------------------------------------------------------ THE HINTS

        void BuildHints()
        {
            hints.Clear();

            Add("move", "<b>WASD</b> to move, <b>mouse</b> to look, hold <b>Shift</b> to sprint.",
                showIf: () => true,
                done: () => hasStartPos && Player != null &&
                            (Player.transform.position - startPos).sqrMagnitude > 9f);

            Add("hyd", "Your <b>HYD</b> is your body's water. It drains all the time, faster when you sprint. At 0 you collapse.",
                showIf: () => SecondsSince("move") >= 6f);

            Add("pump", "This hand pump fills the <b>Raw Basin</b>. Hold <b>E</b> to pump. Raw water is dirty: don't drink it.",
                showIf: () => Near<HubPump_Manager>(),
                done: () => Res != null && Res.rawBasin > 0.05f);

            Add("pot", "The <b>boiling pot</b> cleans raw water by itself and moves it into <b>Clean Storage</b>.",
                showIf: () => Res != null && Res.rawBasin > 0.05f);

            Add("tap", "Drink here, or refill your <b>canteen</b> to carry water out. Clean Storage is also the money you buy upgrades with.",
                showIf: () => Near<HubTapPoint_Manager>());

            Add("outside", "Out of the hub's shade you get thirsty twice as fast. Hold <b>Q</b> to drink from your canteen.",
                showIf: () => leftHubAt >= 0f,
                done: () => Hyd != null && Hyd.IsDrinking);

            Add("map", "Press <b>M</b> for your map. It fills in as you explore, and new areas pay out water. <b>F</b> drops a marker.",
                showIf: () => leftHubAt >= 0f && Time.time - leftHubAt >= 20f,
                done: MapOpen);

            Add("basin", "Gates open for water. Hold <b>E</b> to pour your canteen into the <b>lock basin</b>. " +
                         "Poured water stays, so you can fill it over several trips.",
                showIf: () => NearBasin(full: false), seconds: 10f);

            Add("basinFull", "Basin full! Press <b>E</b>, then hit <b>Space</b> while the needle is in the green band. 3 hits opens the gate for good.",
                showIf: () => NearBasin(full: true), seconds: 10f);

            Add("valve", "<b>Valve gates</b> are free shortcuts: no water needed, just win the valve minigame.",
                showIf: () => NearValve());

            Add("unbanked", "Water you earn out here is <b>unbanked</b>. Walk back into the hub to keep it. Home before night: +10%.",
                showIf: () => Res != null && Res.unbankedWater > 0.05f, seconds: 9f);

            Add("room", "<b>Rooms</b> are safe at night. They hide notes, jugs and cisterns. Read notes later in your journal (<b>J</b> on the map).",
                showIf: () => Near<RoomNoteObject>() || Near<RoomWaterSource>(), seconds: 9f);

            Add("night", "Night is coming. Every gate shuts, and the open maze drains you fast. Be in the <b>hub</b> or a <b>room</b>.",
                showIf: () => Clock != null && !Clock.IsNight && Clock.SecondsLeftInPhase <= 75f, seconds: 9f);

            Add("build", "You can afford an upgrade. Press <b>Tab</b> for Build and Skills (only at the hub; the clock stops while it's open).",
                showIf: () => AtHub && CanAffordSomething(),
                done: () => gameMenu != null && gameMenu.IsOpen, seconds: 10f);

            Add("dehydrated", "You're <b>dehydrated</b>: slower, and no sprinting. Drink (<b>Q</b>) or head home.",
                showIf: () => lastHydSeen < 30f && !(Hyd != null && Hyd.IsCollapsed()));

            Add("sick", "Raw water made you <b>sick</b>: you'll drain faster for a while.",
                showIf: () => Hyd != null && Hyd.SickSecondsLeft > 0f);

            Add("collapse", "Collapsing costs water and time. Always keep at least <b>2 L</b> in Clean Storage: collapse with less and the drought wins.",
                showIf: () => collapses > 0 && !(Hyd != null && Hyd.IsCollapsed()), seconds: 10f);

            Add("calamity", "The water table is dropping. By <b>State of Calamity</b> the pump runs dry: build another source or the Deep-Set Cylinder.",
                showIf: () => WorldState.StageIndex >= 2, seconds: 10f);

            Add("zone2", "Zone 2 just <b>turned 90°</b>. It turns every second night. Check your map (<b>M</b>).",
                showIf: () => zone2Turns > 0, done: MapOpen, seconds: 10f, demoToo: false);
        }

        void Add(string id, string text, Func<bool> showIf, Func<bool> done = null, float seconds = 0f, bool demoToo = true)
        {
            hints.Add(new Hint { id = id, text = text, showIf = showIf, done = done, seconds = seconds, demoToo = demoToo });
        }

        // ------------------------------------------------------------------ Each frame

        void Update()
        {
            if (!showHints) { HideCard(); return; }

            bool canShow = CanShow();
            if (canShow) TrackFacts();

            // Look for newly ready hints a few times a second (cheap enough, but no need every frame).
            checkTimer -= Time.unscaledDeltaTime;
            if (canShow && checkTimer <= 0f)
            {
                checkTimer = 0.25f;
                bool demo = BalanceProvider.Current != null && BalanceProvider.Current.isDemoMode;
                foreach (var h in hints)
                {
                    if (seen.Contains(h.id) || queue.Contains(h) || h == current) continue;
                    if (demo && !h.demoToo) continue;
                    if (SafeCheck(h.showIf)) queue.Add(h);
                }
            }

            if (current != null)
            {
                SetCardVisible(canShow);
                if (!canShow) return; // wait behind the menu / minigame / pause
                currentFor += Time.unscaledDeltaTime;
                float limit = current.seconds > 0f ? current.seconds : defaultSeconds;
                bool learnt = current.done != null && SafeCheck(current.done);
                if (currentFor >= limit || (learnt && currentFor >= minSeconds))
                {
                    current = null;
                    HideCard();
                    gapLeft = gapSeconds;
                }
                return;
            }

            if (!canShow || queue.Count == 0) return;
            if (gapLeft > 0f) { gapLeft -= Time.unscaledDeltaTime; return; }

            var next = queue[0];
            queue.RemoveAt(0);
            seen.Add(next.id);
            shownAt[next.id] = Time.time;
            if (next.done != null && SafeCheck(next.done)) return; // already learnt: skip it
            Show(next);
        }

        void TrackFacts()
        {
            var p = Player;
            if (p != null && !hasStartPos) { startPos = p.transform.position; hasStartPos = true; }
            if (leftHubAt < 0f && hasStartPos && UpgradeManager.Instance != null && !AtHub) leftHubAt = Time.time;

            if (p != null && p.playerStats_Manager != null) lastHydSeen = p.playerStats_Manager.player_Hydration;

            bool collapsed = Hyd != null && Hyd.IsCollapsed();
            if (wasCollapsed && !collapsed) collapses++;
            wasCollapsed = collapsed;
        }

        /// <summary>Hints only show during normal play, after the start screen.</summary>
        static bool CanShow()
        {
            var gm = Game_Manager.instance;
            return gm != null && gm.CanControlPlayer() &&
                   !LoadingScreen.IsLoading && !HowToPlay.IsOpen && !MainMenu.IsFrozen;
        }

        // A broken trigger must never break the game: treat it as "not yet".
        static bool SafeCheck(Func<bool> f)
        {
            try { return f != null && f(); }
            catch (Exception e) { Debug.LogWarning("[TutorialHints] A hint check failed: " + e.Message); return false; }
        }

        // ------------------------------------------------------------------ Trigger helpers

        static Player_Manager Player => Player_Manager.instance;
        static PlayerHydration_Manager Hyd => Player != null ? Player.playerHydration_Manager : null;
        static GameResources_Manager Res => GameResources_Manager.instance;
        static GameClockLogic Clock => GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Clock : null;
        static bool AtHub => UpgradeManager.Instance != null && UpgradeManager.Instance.PlayerAtHub;

        float SecondsSince(string id) => shownAt.TryGetValue(id, out var t) ? Time.time - t : -1f;

        bool Near<T>() where T : Interactable_Manager
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is T && i != null && (i.GetInteractionPosition() - p.transform.position).sqrMagnitude <= r2)
                    return true;
            return false;
        }

        bool NearBasin(bool full)
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
            {
                if (!(i is LockBasin_Manager b) || b == null || b.IsPassable) continue;
                if ((b.GetInteractionPosition() - p.transform.position).sqrMagnitude > r2) continue;
                bool isFull = b.basin_Water >= b.basin_MaxWater - 0.01f;
                if (isFull == full) return true;
            }
            return false;
        }

        bool NearValve()
        {
            var p = Player;
            if (p == null) return false;
            float r2 = nearRange * nearRange;
            foreach (var i in Interactable_Manager.allInteractables)
                if (i is ValveGate v && v != null && !v.IsOpen &&
                    (v.GetInteractionPosition() - p.transform.position).sqrMagnitude <= r2)
                    return true;
            return false;
        }

        bool MapOpen() =>
            (mapScreen != null && mapScreen.IsOpen) ||
            (gameMenu != null && gameMenu.IsOpen && gameMenu.Memory.Current == MenuPage.Map);

        static bool CanAffordSomething()
        {
            var um = UpgradeManager.Instance;
            if (um == null || um.Logic == null) return false;
            foreach (var u in um.Logic.All)
                if (u != null && um.Logic.GetCard(u.Id).State == CardState.CanBuy) return true;
            return false;
        }

        // ------------------------------------------------------------------ The card

        void BuildCard()
        {
            // Above the HUD (9-10), under the map (60), menus and the start screen.
            var canvas = UiBuilder.CreateCanvas("TutorialHints", transform, 12);
            cardRoot = canvas.gameObject;
            cardGroup = cardRoot.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = false;

            var bg = UiBuilder.Image("Card", canvas.transform, cardColor).rectTransform;
            UiBuilder.Place(bg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), cardPosition, cardSize);

            var bar = UiBuilder.Image("Accent", bg, labelColor).rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(6f, 0f);

            var tag = UiBuilder.Text("Label", bg, label, 20f, labelColor);
            tag.fontStyle = FontStyles.Bold;
            UiBuilder.Fill(tag.rectTransform, 22f, 16f, 12f, 0f);

            cardText = UiBuilder.Text("Text", bg, "", 26f, textColor);
            UiBuilder.Fill(cardText.rectTransform, 22f, 18f, 40f, 12f);

            HideCard();
        }

        void Show(Hint h)
        {
            current = h;
            currentFor = 0f;
            if (cardText != null) cardText.text = h.text;
            SetCardVisible(true);
        }

        void SetCardVisible(bool visible)
        {
            if (cardRoot != null && cardRoot.activeSelf != visible) cardRoot.SetActive(visible);
            if (cardGroup != null) cardGroup.alpha = visible ? 1f : 0f;
        }

        void HideCard()
        {
            current = null;
            SetCardVisible(false);
        }
    }
}
