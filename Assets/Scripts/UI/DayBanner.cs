// ============================================================================
// DayBanner.cs
// At dawn of every new day, the big middle-of-the-screen banner says which day it is and the
// drought stage, e.g. "DAY 4 · DROUGHT" (T44). It uses SimpleHud.Banner, so it looks like
// every other banner (events, stages, collapse).
// On a day that also starts a new drought stage, StageManager shows its stage banner in the
// same frame; this banner comes one frame later and keeps the stage's line under the title,
// so the player gets one banner with both.
// Day 1 has no banner (the game just started; How to Play is on screen).
//
// DESIGN RULES: R1.5 (days), R10.1 (stages), T44.
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// HOW TO EDIT: the look is SimpleHud's banner (UI > Toasts and Banners in the scene). The
// words and the time on screen are below.
// ============================================================================

using System.Collections;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Shows "DAY N · STAGE" at the start of every day.</summary>
    public class DayBanner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<DayBanner>();

        [Tooltip("{0} = day number, {1} = stage name in capitals.")]
        public string titleFormat = "DAY {0} · {1}";
        [Tooltip("Used when the banner font has no middle dot.")]
        public string plainTitleFormat = "DAY {0} - {1}";
        [Tooltip("Seconds on screen.")]
        public float seconds = 4f;

        GameClock_Manager clock;
        int stageAtLastDawn = -1;

        IEnumerator Start()
        {
            yield return null; // the clock is made in its own Start
            clock = GameClock_Manager.Instance;
            if (clock == null) yield break;
            clock.NewDay += OnNewDay;
            stageAtLastDawn = WorldState.StageIndex;
        }

        void OnDestroy()
        {
            if (clock != null) clock.NewDay -= OnNewDay;
        }

        void OnNewDay(int day) => StartCoroutine(ShowNextFrame(day));

        IEnumerator ShowNextFrame(int day)
        {
            yield return null; // after StageManager has moved to the new stage
            var balance = BalanceProvider.Current;
            int stage = WorldState.StageIndex;
            string stageName = balance != null && balance.stages != null && stage >= 0 && stage < balance.stages.Length
                ? balance.stages[stage].displayName.ToUpperInvariant() : "";
            string line = "";
            float time = seconds;
            if (stage != stageAtLastDawn && balance != null)
            {
                // A new stage today: keep its banner line (R10.1).
                if (balance.stageBannerLines != null && stage < balance.stageBannerLines.Length) line = balance.stageBannerLines[stage];
                time = Mathf.Max(seconds, balance.stageBannerSeconds);
            }
            stageAtLastDawn = stage;

            string format = SimpleHud.BannerHasCharacter('·') ? titleFormat : plainTitleFormat;
            string title = string.IsNullOrEmpty(stageName) ? $"DAY {day}" : string.Format(format, day, stageName);
            SimpleHud.Banner(title, line, time);
        }
    }
}
