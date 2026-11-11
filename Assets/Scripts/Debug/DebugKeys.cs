// ============================================================================
// DebugKeys.cs
// Every debug key in the game goes through this one script, so they can all be
// switched off with one checkbox: GameBalance > Mode > "Enable Debug Keys" (team rule).
//
//   Key | Does
//   ----+------------------------------------------------------------------
//    0  | Reveal the whole map
//    5  | Force zone 2 to turn now
//    6  | Clock x "Debug Clock Speed" (4); press again for normal speed
//    7  | +50 L Clean Storage (never past its size)
//    F1 | Debug HUD on/off (also the minigame's t / band / speed line)
//    F5 | Save now
//    F9 | Load the save
//   Added by other systems with Register (they work only while debug keys are on too):
//    4  | Toggle rain (Sources/WaterSources.cs)
//    8  | Next drought stage (Drought/StageManager.cs)
//    9  | Next event: Thunderstorm > Heatwave > Calm (Events/EventManager.cs)
//    F8 | Win now, to test the ending screen (Menus/WinScreen.cs)
//
// Other systems add their own keys without editing this file:
//     DebugKeys.Register(Key.Digit8, "next drought stage", () => ...);   // in Start
//     DebugKeys.Unregister(Key.Digit8);                                   // in OnDestroy
// Pick a key nobody uses yet (check this table): registering a used key replaces it.
//
// DESIGN RULES: none (testing only).
//
// HOW TO USE: put this on the "Systems" object. It finds the systems it needs by itself.
// The Build Playtest Scene menu adds it for you. Untick "Enable Debug Keys" on GameBalance
// for builds that players get: every key above then does nothing and the debug HUD hides.
// HOW TO EDIT: the clock speed is "Debug Clock Speed" and the litres are
// "Debug Clean Storage Liters", both in GameBalance.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDrop
{
    /// <summary>Reads the debug keys and calls the matching system. Off when GameBalance says debug keys are off.</summary>
    public class DebugKeys : MonoBehaviour
    {
        [Tooltip("Zone 2's rotator (on the Maze object). Empty = found by itself.")]
        public Zone2Rotator zone2Rotator;

        [Tooltip("The map tracker (on Systems). Empty = found by itself.")]
        public MapRevealTracker mapTracker;

        /// <summary>True while debug info is shown (F1). The debug HUD and the minigame's debug line read this.</summary>
        public static bool ShowDebugInfo { get; set; }

        GameBalance balance;

        static readonly Dictionary<Key, (string label, Action action)> extraKeys = new Dictionary<Key, (string, Action)>();

        /// <summary>Adds a debug key (replaces any earlier one on the same key). Works only while debug keys are on.</summary>
        public static void Register(Key key, string label, Action action) => extraKeys[key] = (label, action);

        /// <summary>Removes a debug key added with Register.</summary>
        public static void Unregister(Key key) => extraKeys.Remove(key);

        void Start()
        {
            balance = BalanceProvider.Current;
            if (zone2Rotator == null) zone2Rotator = FindAnyObjectByType<Zone2Rotator>();
            if (mapTracker == null) mapTracker = FindAnyObjectByType<MapRevealTracker>();
            if (KeysOn)
                Debug.Log("[DebugKeys] Debug keys are ON: 0 reveal map, 5 turn zone 2, 6 fast clock, " +
                          "7 +Clean Storage, F1 debug HUD, F5 save, F9 load.", this);
        }

        /// <summary>True when GameBalance allows debug keys.</summary>
        public bool KeysOn => balance != null && balance.enableDebugKeys;

        void Update()
        {
            if (!KeysOn)
            {
                ShowDebugInfo = false; // debug keys off = no debug HUD either
                return;
            }
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit0Key.wasPressedThisFrame)
            {
                if (mapTracker != null) mapTracker.RevealAll();
                else Missing("0", nameof(MapRevealTracker));
            }

            if (kb.digit5Key.wasPressedThisFrame)
            {
                if (zone2Rotator != null) zone2Rotator.TurnOnce("debug key 5");
                else Missing("5", nameof(Zone2Rotator));
            }

            if (kb.digit6Key.wasPressedThisFrame)
            {
                if (GameClock_Manager.Instance != null) GameClock_Manager.Instance.ToggleDebugSpeed();
                else Missing("6", nameof(GameClock_Manager));
            }

            if (kb.digit7Key.wasPressedThisFrame)
            {
                if (UpgradeManager.Instance != null) UpgradeManager.Instance.AddCleanStorage(balance.debugCleanStorageLiters);
                else Missing("7", nameof(UpgradeManager));
            }

            if (kb.f1Key.wasPressedThisFrame)
            {
                ShowDebugInfo = !ShowDebugInfo;
                Debug.Log($"[DebugKeys] F1: debug HUD {(ShowDebugInfo ? "on" : "off")}.", this);
            }

            if (kb.f5Key.wasPressedThisFrame)
            {
                if (SaveManager.Instance != null) SaveManager.Instance.Save();
                else Missing("F5", nameof(SaveManager));
            }

            if (kb.f9Key.wasPressedThisFrame)
            {
                if (SaveManager.Instance != null) SaveManager.Instance.Load();
                else Missing("F9", nameof(SaveManager));
            }

            // Keys other systems registered (8 drought stage, 9 event, ...). Copy first: an action may register keys.
            foreach (var kv in new List<KeyValuePair<Key, (string label, Action action)>>(extraKeys))
            {
                if (!kb[kv.Key].wasPressedThisFrame) continue;
                Debug.Log($"[DebugKeys] {kv.Key}: {kv.Value.label}", this);
                kv.Value.action?.Invoke();
            }
        }

        void Missing(string key, string what) =>
            Debug.LogWarning($"[DebugKeys] {key}: there is no {what} in this scene.", this);
    }
}
