// ============================================================================
// TutorialGate.cs
// The ONE place other systems ask "does the tutorial allow this right now?":
//   - Allows(interactable)  PlayerInteraction_Manager: an E interaction not taught yet gives
//                           no prompt and does nothing (the pump before the pump step ...).
//   - AllowsMenu(page)      GameMenu / MapScreen: Build and Skills, and the map, stay shut
//                           until their step (ReportBlocked shows a toast saying why).
//   - EasyMinigame(basin)   LockBasin_Manager: the guided first valve minigame is easier and
//                           free to fail (EasySettings gives its band and needle speed).
// When no tutorial is running every answer is "yes" / "normal game".
//
// Which feature each interactable belongs to is FeatureOf below: the pump, the tap, lock
// basins, and everything else (valve grates, room doors, notes, room water) = the maze.
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: TutorialDirector fills the hooks; other scripts only call the static methods.
// HOW TO EDIT: the easy minigame numbers are in GameBalance > Tutorial.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Asks the running tutorial what the player may use right now.</summary>
    public static class TutorialGate
    {
        /// <summary>Set by the running tutorial: true if a feature may be used. Null = everything allowed.</summary>
        public static Func<TutorialFeature, bool> FeatureCheck;

        /// <summary>Set by the running tutorial: true if this basin's next minigame is the guided easy one.</summary>
        public static Func<LockBasin_Manager, bool> EasyMinigameCheck;

        /// <summary>Set by the running tutorial: shows why a menu didn't open.</summary>
        public static Action<TutorialFeature> Blocked;

        // Static fields survive between Play sessions when domain reload is off: start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FeatureCheck = null;
            EasyMinigameCheck = null;
            Blocked = null;
        }

        /// <summary>True if a feature may be used now.</summary>
        public static bool Allows(TutorialFeature feature) => FeatureCheck == null || FeatureCheck(feature);

        /// <summary>True if the player may use this interactable now (checked every frame, no messages).</summary>
        public static bool Allows(Interactable_Manager interactable) =>
            FeatureCheck == null || interactable == null || FeatureCheck(FeatureOf(interactable));

        /// <summary>True if this page of the Tab menu (or the map) may open now. Doesn't show anything.</summary>
        public static bool AllowsMenu(MenuPage page) => Allows(FeatureOf(page));

        /// <summary>Shows the "opens later in the tutorial" toast for a page that didn't open.</summary>
        public static void ReportBlocked(MenuPage page) => Blocked?.Invoke(FeatureOf(page));

        /// <summary>True if this basin's minigame should be the guided easy one (no HYD cost, no lockout).</summary>
        public static bool EasyMinigame(LockBasin_Manager basin) => EasyMinigameCheck != null && basin != null && EasyMinigameCheck(basin);

        /// <summary>The easy first minigame: GameBalance > Tutorial band and speed, same hits and misses.</summary>
        public static ValveTimingSettings EasySettings(ValveTimingSettings normal)
        {
            var b = BalanceProvider.Current;
            float band = b != null ? b.tutorialValveBand : 45f;
            float speed = b != null ? b.tutorialValveSpeed : 80f;
            int hits = normal != null ? normal.hitsToWin : (b != null ? b.valveHitsToWin : 3);
            int misses = normal != null ? normal.missesToLose : (b != null ? b.valveMissesToLose : 3);
            // Never harder than the real gate.
            if (normal != null)
            {
                band = Mathf.Max(band, normal.bandWidth);
                speed = Mathf.Min(speed, normal.needleSpeed);
            }
            return new ValveTimingSettings(band, speed, hits, misses);
        }

        /// <summary>Which feature an interactable belongs to.</summary>
        public static TutorialFeature FeatureOf(Interactable_Manager i)
        {
            if (i is HubPump_Manager) return TutorialFeature.Pump;
            if (i is HubTapPoint_Manager) return TutorialFeature.Tap;
            if (i is LockBasin_Manager) return TutorialFeature.Gates;
            return TutorialFeature.Maze;
        }

        /// <summary>Which feature a menu page belongs to.</summary>
        public static TutorialFeature FeatureOf(MenuPage page) =>
            page == MenuPage.Map ? TutorialFeature.Map : TutorialFeature.BuildSkills;
    }
}
