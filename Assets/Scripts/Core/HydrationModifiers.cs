// ============================================================================
// HydrationModifiers.cs
// One place where other systems multiply the player's HYD drain (R2.1: "base drain ×
// drought stage heat × event modifier × skill modifiers. Multiply them all; never add").
//
// A system adds a multiplier once, for example the drought stages:
//
//     HydrationModifiers.Add("stage", ctx => balance.GetStage(day).heatMult);
//
// and PlayerHydration_Manager multiplies every one of them into the drain each frame.
// The context says what the player is doing, so a skill can apply only to walking.
//
// DESIGN RULES: R2.1.
//
// HOW TO USE: Add(key, function) once (in Start); Remove(key) in OnDestroy.
// Using the same key again replaces the old one. Keys in use: "stage" (StageManager),
// EventManager.ModifierKey (Thunderstorm / Heatwave). Skills E1 / E2 are applied by
// PlayerHydration_Manager itself. The F1 debug HUD lists them all (Describe).
// HOW TO EDIT: nothing to tune here; each system's numbers are in GameBalance.
// ============================================================================

using System;
using System.Collections.Generic;

namespace TheLastDrop
{
    /// <summary>What the player is doing this frame, for drain multipliers that depend on it.</summary>
    public struct DrainContext
    {
        /// <summary>Walking or sprinting.</summary>
        public bool Moving;
        /// <summary>Sprinting.</summary>
        public bool Sprinting;
        /// <summary>Holding E on the hand pump.</summary>
        public bool Pumping;
        /// <summary>Inside the hub (shade).</summary>
        public bool InHub;
        /// <summary>Current day (1-based).</summary>
        public int Day;
    }

    /// <summary>Every multiplier on HYD drain, by name.</summary>
    public static class HydrationModifiers
    {
        static readonly Dictionary<string, Func<DrainContext, float>> mods = new Dictionary<string, Func<DrainContext, float>>();

        /// <summary>Adds (or replaces) a drain multiplier.</summary>
        public static void Add(string key, Func<DrainContext, float> multiplier) => mods[key] = multiplier;

        /// <summary>Removes a drain multiplier.</summary>
        public static void Remove(string key) => mods.Remove(key);

        /// <summary>All multipliers multiplied together (1 if there are none).</summary>
        public static float Product(DrainContext ctx)
        {
            float p = 1f;
            foreach (var m in mods.Values) p *= m(ctx);
            return p;
        }

        /// <summary>Each multiplier's current value, for the debug HUD.</summary>
        public static string Describe(DrainContext ctx)
        {
            var parts = new List<string>();
            foreach (var kv in mods) parts.Add($"{kv.Key} ×{kv.Value(ctx):0.##}");
            return string.Join(", ", parts);
        }
    }
}
