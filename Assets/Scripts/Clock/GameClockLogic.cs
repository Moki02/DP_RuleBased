// ============================================================================
// GameClockLogic.cs
// The day/night clock as plain C# (no MonoBehaviour), so it can be tested.
// A day is daylight followed by the night lockdown: 240 s + 60 s = 300 s by default.
// The day counter starts at 1. At the end of daylight it fires NightStarted(day); at the
// end of the night it moves to the next day and fires NewDay(newDay).
//
// DESIGN RULES: R1.5 (day length), R8.19 (night lockdown length).
//
// HOW TO USE: you don't put this on an object. GameClock_Manager (on the Game Manager
// prefab) owns one and calls Advance() every frame.
// HOW TO EDIT: the lengths are "Day Length Seconds" and "Night Lockdown Seconds" in
// GameBalance. Debug key 6 multiplies the speed by "Debug Clock Speed".
// ============================================================================

using System;

namespace TheLastDrop
{
    /// <summary>Counts days, daylight and night. Fires an event when night starts and when a new day starts.</summary>
    public class GameClockLogic
    {
        /// <summary>Seconds of daylight (dawn to the start of the night lockdown).</summary>
        public float DaylightSeconds { get; }

        /// <summary>Seconds of night lockdown. 0 = no night (old single-zone scenes).</summary>
        public float NightSeconds { get; }

        /// <summary>Seconds in a whole day (daylight + night).</summary>
        public float DayLength => DaylightSeconds + NightSeconds;

        /// <summary>The day number, starting at 1.</summary>
        public int Day { get; private set; } = 1;

        /// <summary>Seconds since this day's dawn (0 up to DayLength).</summary>
        public float TimeOfDay { get; private set; }

        /// <summary>True during the night lockdown.</summary>
        public bool IsNight => NightSeconds > 0f && TimeOfDay >= DaylightSeconds;

        /// <summary>Seconds left until night starts (in daylight) or until dawn (at night).</summary>
        public float SecondsLeftInPhase => IsNight ? DayLength - TimeOfDay : DaylightSeconds - TimeOfDay;

        /// <summary>How fast the clock runs: 1 = real time. Debug key 6 changes it.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>While true, Advance() does nothing (the Build and Skills menu at the hub pauses the clock).</summary>
        public bool Paused { get; set; }

        /// <summary>Fired when the night lockdown starts. The number is the current day.</summary>
        public event Action<int> NightStarted;

        /// <summary>Fired at dawn. The number is the NEW day (2 at the end of day 1).</summary>
        public event Action<int> NewDay;

        /// <summary>Makes a clock at dawn of day 1.</summary>
        /// <param name="daylightSeconds">Seconds of daylight (at least a tiny bit, so the clock can't get stuck).</param>
        /// <param name="nightSeconds">Seconds of night lockdown (0 for no night).</param>
        public GameClockLogic(float daylightSeconds, float nightSeconds)
        {
            DaylightSeconds = Math.Max(0.01f, daylightSeconds);
            NightSeconds = Math.Max(0f, nightSeconds);
        }

        /// <summary>
        /// Moves the clock forward by <paramref name="deltaSeconds"/> real seconds (times SpeedMultiplier).
        /// Every night start and dawn on the way fires its event, in order, even after a long pause.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (Paused || deltaSeconds <= 0f) return;
            float remaining = deltaSeconds * SpeedMultiplier;

            // Walk forward one "boundary" at a time (night start, then dawn) so that no event is
            // skipped if a big step of time crosses several of them at once.
            while (remaining > 0f)
            {
                bool nextIsNight = !IsNight && NightSeconds > 0f;
                float boundary = nextIsNight ? DaylightSeconds : DayLength;
                float untilBoundary = boundary - TimeOfDay;

                if (remaining < untilBoundary)
                {
                    TimeOfDay += remaining;
                    return;
                }

                remaining -= untilBoundary;
                if (nextIsNight)
                {
                    TimeOfDay = DaylightSeconds; // IsNight is now true
                    NightStarted?.Invoke(Day);
                }
                else
                {
                    Day++;
                    TimeOfDay = 0f;
                    NewDay?.Invoke(Day);
                }
            }
        }

        /// <summary>
        /// Jumps straight to a day and time, WITHOUT firing events. Used when loading a save.
        /// </summary>
        public void SetTime(int day, float timeOfDay)
        {
            Day = Math.Max(1, day);
            TimeOfDay = Math.Max(0f, Math.Min(timeOfDay, DayLength - 0.001f));
        }
    }
}
