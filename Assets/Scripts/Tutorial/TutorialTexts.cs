// ============================================================================
// TutorialTexts.cs
// Every word the tutorial shows, in ONE editable asset: Assets/Resources/Tutorial/TutorialTexts.asset
// (Salok > Tutorial > Set Up Tutorial makes it). Each entry has:
//   Id         - which step or lesson it belongs to (don't rename; the code looks it up by id)
//   Objective  - the short line at the top of the screen while that step is going
//   Card       - the 1 to 2 sentence instruction card
// Numbers are written as {tokens} (for example {pumpStroke}); the game fills them in from
// GameBalance, so the text never disagrees with the real numbers. The token list is in
// TutorialDirector.BuildTokens.
// <b>bold</b> and <color=#4DA3FF>blue</color> work (TextMeshPro rich text).
//
// DESIGN RULES: none of its own. Plain instructions, no narrator or story voice.
//
// HOW TO USE: nothing; TutorialDirector loads it. If the asset is missing, the defaults
// below are used.
// HOW TO EDIT: select the asset and change the words in the Inspector. To get the default
// words back, right-click the asset's header ("⋮") > Reset To Default Words. A missing id
// (e.g. you deleted an entry) falls back to the default for that id.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The tutorial's words, one entry per step or lesson.</summary>
    [CreateAssetMenu(fileName = "TutorialTexts", menuName = "Salok/Tutorial Texts")]
    public class TutorialTexts : ScriptableObject
    {
        /// <summary>Where the asset lives inside a Resources folder.</summary>
        public const string ResourcePath = "Tutorial/TutorialTexts";

        /// <summary>The words of one step or lesson.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Which step or lesson this is. Don't rename: the code looks it up by this.")]
            public string id;
            [Tooltip("The short line at the top of the screen while this step is going (empty for side lessons).")]
            [TextArea(1, 3)] public string objective;
            [Tooltip("The instruction card (1 to 2 sentences).")]
            [TextArea(2, 6)] public string card;

            public Entry(string id, string objective, string card)
            {
                this.id = id;
                this.objective = objective;
                this.card = card;
            }
        }

        [Header("Fixed words")]
        [Tooltip("The small word above the card.")]
        public string cardLabel = "TUTORIAL";
        [Tooltip("Shown with the tick when a step is done.")]
        public string doneWord = "Done";
        [Tooltip("Shown on the card when the player has been stuck on a step for a while. {key} = the skip key.")]
        public string stuckHint = "Stuck? Press <b>{key}</b> to skip this step.";
        [Tooltip("Toast when the player opens something the tutorial hasn't taught yet. {what} = Build and Skills / the map.")]
        public string lockedToast = "{what} opens later in the tutorial.";
        [Tooltip("The pause menu button that ends the tutorial.")]
        public string skipButtonLabel = "Skip tutorial";

        [Header("First collapse screen")]
        public string deathTitle = "YOU COLLAPSED";
        public string deathLostHeading = "LOST";
        public string deathKeptHeading = "KEPT";
        public string deathNextHeading = "NEXT TIME";
        public string deathContinueLabel = "Continue";

        [Header("Steps and lessons")]
        public List<Entry> entries = Defaults();

        Dictionary<string, Entry> byId;
        static Dictionary<string, Entry> defaultsById;

        /// <summary>The entry for an id: this asset's, else the default, else an empty one.</summary>
        public Entry Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Entry>();
                if (entries != null)
                    foreach (var e in entries)
                        if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;
            }
            if (byId.TryGetValue(id, out var found)) return found;
            if (defaultsById == null)
            {
                defaultsById = new Dictionary<string, Entry>();
                foreach (var e in Defaults()) defaultsById[e.id] = e;
            }
            return defaultsById.TryGetValue(id, out var d) ? d : new Entry(id, "", "");
        }

        void OnValidate() => byId = null; // edits in the Inspector show up straight away

        /// <summary>Loads the asset from Resources, or makes one with the default words.</summary>
        public static TutorialTexts Load()
        {
            var t = Resources.Load<TutorialTexts>(ResourcePath);
            return t != null ? t : CreateInstance<TutorialTexts>();
        }

        [ContextMenu("Reset To Default Words")]
        void ResetToDefaults()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Reset tutorial words");
#endif
            entries = Defaults();
            byId = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // ================================================================== The default words

        /// <summary>The default words, in the order the player usually meets them.</summary>
        public static List<Entry> Defaults() => new List<Entry>
        {
            // ---------------- Main steps
            new Entry("goal", "Escape the maze. Every gate opens only for water.", ""),
            new Entry("move", "Move around.",
                "<b>WASD</b> to move, <b>mouse</b> to look, hold <b>Shift</b> to sprint."),
            new Entry("pump", "Pump water: hold <b>E</b> at the hand pump.",
                "The hand pump fills the <b>Raw Basin</b>. Stand next to it and hold <b>E</b>. Each stroke adds {pumpStroke} L."),
            new Entry("rawBasin", "This is the Raw Basin.",
                "The <b>Raw Basin</b> holds the water you pump. Raw water is dirty: drinking it makes you sick."),
            new Entry("pot", "Watch the boiling pot clean your water.",
                "The <b>boiling pot</b> cleans raw water by itself ({boilRate} L/s) and moves it into Clean Storage. Nothing to press."),
            new Entry("pot.empty", "Pump more water: the pot has nothing to boil.", ""),
            new Entry("storage", "This is Clean Storage.",
                "<b>Clean Storage</b> is your water, and also your money: Build items and skills cost Clean Storage water."),
            new Entry("hud", "Drink from your canteen: hold <b>Q</b>.",
                "The <b>HYD</b> bar is your body's water. It always drains (faster outside and when you sprint); at 0 you collapse. " +
                "Hold <b>Q</b> to drink from your <b>canteen</b>."),
            new Entry("hud.full", "Drink from your canteen (hold <b>Q</b>) once your HYD drops a little.", ""),
            new Entry("tap", "Refill your canteen: hold <b>E</b> at the Refill tap.",
                "The tap takes water from Clean Storage: <b>Drink</b> fills you, <b>Refill</b> fills your canteen. Always leave with a full canteen."),
            new Entry("gatePour", "Open a hub gate: pour water into its lock basin (hold <b>E</b>).",
                "Every gate opens only for water. A hub gate's basin takes {hubLock} L and your canteen holds {canteenMax} L: " +
                "pour, refill at the tap, come back. Poured water stays in the basin."),
            new Entry("gatePour.night", "Gates are shut for the night. Keep making water; the gate lesson goes on at dawn.", ""),
            new Entry("valve", "The basin is full: press <b>E</b> to turn the valve.",
                "A full basin unlocks the valve. Win the valve minigame and the gate opens for good."),
            new Entry("valve.night", "Gates are shut for the night. The valve lesson goes on at dawn.", ""),
            new Entry("valve.play", "",
                "The needle swings across the dial. Press <b>Space</b> while it is inside the <color=#4DA3FF><b>green band</b></color>. " +
                "{hits} hits opens the gate. Misses cost nothing this time; <b>Esc</b> gives up."),
            new Entry("valve.miss", "", "Missed. Wait until the needle is inside the band, then press <b>Space</b>."),
            new Entry("valve.hit", "", "Hit! Keep going."),
            new Entry("maze", "Explore the maze. Be back at the hub before the lockdown.",
                "Outside the hub's shade you get thirsty twice as fast. Hold <b>Q</b> to drink from your canteen when you need to."),
            new Entry("maze.go", "Go through the gate you opened.", ""),
            new Entry("maze.night", "Lockdown: stay in the hub until dawn, then go through the gate you opened.", ""),
            new Entry("map", "Open your map: press <b>M</b>.",
                "The map fills in as you explore, and exploring pays water: {tileReward} L per new tile, {sectionReward} L per mapped section, " +
                "+{milestones} L at 25 / 50 / 75 / 100% of the maze. Press <b>F</b> to place a marker ({markers} at most)."),
            new Entry("return", "Go back to the hub to bank what you earned.",
                "Water you earn out here is <b>unbanked</b>: walk back into the hub to keep it. Home before night: +{safeBonus}%."),
            new Entry("build", "Open Build and Skills: press <b>Tab</b>.",
                "Spend Clean Storage on <b>Build</b> items and <b>Skills</b>. Tab opens them (only at the hub); the clock stops while they're open."),

            // ---------------- Side lessons (shown once, the first time it happens)
            new Entry("dusk", "",
                "Night is coming. At lockdown every gate shuts and the open maze drains {lockdownDrain} HYD per second. Be in the <b>hub</b> or a <b>room</b>."),
            new Entry("lockdown", "",
                "Lockdown: every gate stays shut until dawn ({nightSeconds} s). The hub and rooms are safe, and you can keep pumping."),
            new Entry("room", "",
                "<b>Rooms</b> are safe at night. Each has a note (press <b>E</b>; read it again in the journal, <b>J</b> on the map) " +
                "and may have a cistern or a water jug to fill your canteen."),
            new Entry("roomDoor", "",
                "Room doors open with the valve minigame. Losing costs nothing here: just try again."),
            new Entry("sick", "",
                "Raw water made you <b>sick</b>: you drain ×{sickMult} for {sickSeconds} s. Drink clean water only."),
            new Entry("bank", "",
                "Water banked: walking into the hub moves what you carry into Clean Storage. Home before night: +{safeBonus}%."),
            new Entry("valveGrate", "",
                "<b>Valve grates</b> need no water, only the valve minigame. They are shortcuts; a loss costs {failHyd} HYD."),
            new Entry("zone2", "",
                "Zone 2 just turned 90°. It turns every {zone2Days} nights. Check your map (<b>M</b>)."),
            new Entry("stage", "",
                "The drought got worse: <b>{stageName}</b>. You drain faster and the pump gives less."),
            new Entry("calamity", "",
                "<b>State of Calamity</b>: the hand pump has run dry. Build another water source, or the Deep-Set Cylinder."),
            new Entry("weather.storm", "",
                "<b>Thunderstorm</b>: while it rains your drain is halved, the Rain Catcher fills, and your canteen refills in the open."),
            new Entry("weather.heat", "",
                "<b>Heatwave</b>: you drain ×{heatMult} until dawn, and stored water evaporates faster tonight."),
            new Entry("weather.calm", "",
                "<b>Calm day</b>: no weather today. Every day has one event: Calm, Thunderstorm or Heatwave."),
            new Entry("dehydrated", "",
                "You're <b>dehydrated</b>: slower, and no sprinting. Drink (<b>Q</b>) or head home."),

            // ---------------- First collapse screen
            new Entry("death.lost", "",
                "Your canteen water ({lostCanteen} L)\nHalf your unbanked water ({lostUnbanked} L)\n{clockLost} s of the day"),
            new Entry("death.kept", "",
                "Clean Storage at home ({storageLeft} L after the {wakeLitres} L you drank on waking)\n" +
                "The unbanked water you still carried, now banked ({banked} L)\nEverything you built and mapped"),
            new Entry("death.next", "",
                "Drink (<b>Q</b>) before HYD gets low. Turn back when your canteen is half empty. " +
                "Keep at least {gameOverLitres} L in Clean Storage: collapsing with less ends the game."),
        };
    }
}
