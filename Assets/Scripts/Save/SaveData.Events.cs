// ============================================================================
// SaveData.Events.cs
// The daily event's small part of the save (R11.8). Running events are NOT saved (the
// autosave is at dawn, before any event); these fields only stop a second event firing on
// the same day after loading a mid-day save (F5), and bring back a Heatwave that was on.
//
// HOW TO EDIT: add fields here; old saves without them load with these defaults.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    public partial class SaveData
    {
        [Header("Events")]
        [Tooltip("False in saves made before events existed: the day's event is then planned fresh.")]
        public bool hasEvents;
        [Tooltip("The last day whose event has already started (0 = none yet).")]
        public int eventFiredDay;
        [Tooltip("The day a Heatwave was running on (0 = none). It's restored if the save is from that day.")]
        public int heatwaveDay;
    }
}
