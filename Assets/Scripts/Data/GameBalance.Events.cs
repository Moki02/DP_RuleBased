// ============================================================================
// GameBalance.Events.cs
// More GameBalance values: the extra numbers the daily El Niño events need (section 10).
// The main event numbers (roll window, storm length, heatwave multipliers, heat index,
// scripted Demo events) are already in GameBalance.cs under "Events"; this file only adds
// what the GDD leaves to the developer.
// This is the same GameBalance asset; the class is split over several files ("partial").
//
// DESIGN RULES: R10.4 (roll window), R10.6 (banner), Thunderstorm row of "What each event does".
//
// HOW TO EDIT: select the GameBalance asset; these appear under "Events (extra)".
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    public partial class GameBalance
    {
        [Header("Events (extra)")]
        [Tooltip("OFF (GDD R10.4 as written): the event can start any time between Event Window Min and Max, " +
                 "even in the night lockdown (240-260 s in the full game). " +
                 "ON: the window is cut at the end of daylight, so events never start at night.")]
        public bool eventsOnlyInDaylight = false;

        [Tooltip("Seconds an event banner stays in the middle of the screen (R10.6).")]
        [Min(0.5f)] public float eventBannerSeconds = 4f;

        [Tooltip("While it rains the sun's light is multiplied by this (0.6 = 40% darker). 1 = no change.")]
        [Range(0.05f, 1f)] public float stormLightMult = 0.6f;
    }
}
