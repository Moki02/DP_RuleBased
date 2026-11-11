// ============================================================================
// WinScreen.cs
// The ending screen for a win. GateManager calls WinScreen.Show(day) when the player wins
// an exit gate's valve (a zone 2 exit lock, or in Demo mode a zone 1 exit = Lock 1).
// It shows, on the same end screen as game over (CollapseManager.ShowEndScreen):
//   - the ending title for that day (GDD ending titles table):
//       day 15 or earlier "Water Keeper", 16 to 22 "Survivor", 23 or later "Barely Made It";
//     in Demo mode always "Demo complete" (R13.4);
//   - "You escaped on day N", plus any secondary goal badges (map 100%, every skill);
//   - the run's stats (RunStats.Summary: day, map %, litres, items bought, collapses, R11.7);
//   - Restart and Quit.
// The game is frozen behind it. Debug key F8 wins at once (to test this screen).
//
// DESIGN RULES: R11.6, R11.7, ending titles table and secondary goals (section 11), R13.4.
//
// HOW TO USE: nothing to place; GateManager calls Show. The title days are in
// GameBalance > Ending screen.
// HOW TO EDIT: the words are below. "Read all 8 survivor notes" isn't shown yet: there is
// no notes system to ask (add it to Badges() when there is one).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Shows the win screen with the ending title, badges and stats.</summary>
    public static class WinScreen
    {
        /// <summary>True once the player has won this run.</summary>
        public static bool HasWon { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => HasWon = false;

        // Debug key F8 (only while GameBalance debug keys are on): win now.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RegisterDebugKey()
        {
            DebugKeys.Register(Key.F8, "win now (test the ending screen)", () =>
            {
                var clock = GameClock_Manager.Instance;
                Show(clock != null ? clock.Day : 1);
            });
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => HasWon = false;
        }

        /// <summary>Shows the win screen for an escape on <paramref name="day"/> and freezes the game.</summary>
        public static void Show(int day)
        {
            if (CollapseManager.Instance != null && CollapseManager.Instance.IsGameOver) return;
            HasWon = true;

            var balance = BalanceProvider.Current;
            string title = balance != null ? balance.GetEndingTitle(day) : "You escaped";
            string line = $"You escaped on day {day}.";
            var badges = Badges();
            if (badges.Count > 0) line += "\nBadges: " + string.Join(", ", badges);

            Debug.Log($"[WinScreen] The player escaped on day {day} (R11.6): \"{title}\".");
            MainMenu.Freeze();
            CollapseManager.ShowEndScreen(title.ToUpperInvariant(), line);
        }

        // Secondary goals (section 11, no gameplay effect). Each is read defensively.
        static List<string> Badges()
        {
            var list = new List<string>();

            var tracker = Object.FindAnyObjectByType<MapRevealTracker>();
            if (tracker != null && tracker.Reveal != null && tracker.Reveal.MapPercent >= 99.95f)
                list.Add("Mapped 100% of the maze");

            var um = UpgradeManager.Instance;
            if (um != null && um.Logic != null)
            {
                int skills = 0, owned = 0;
                foreach (var u in um.Logic.All)
                {
                    if (u == null || u.Tab != UpgradeTab.Skills) continue;
                    skills++;
                    if (um.Logic.Owns(u.Id)) owned++;
                }
                if (skills > 0 && owned == skills) list.Add("Learned every skill");
            }
            return list;
        }
    }
}
