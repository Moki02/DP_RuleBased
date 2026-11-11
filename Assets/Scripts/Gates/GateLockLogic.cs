// ============================================================================
// GateLockLogic.cs
// The rules of one gate's lock, as plain C# so they can be tested:
//
//   Empty --pour--> Filling --pour until full--> Full --press E--> Minigame
//   Minigame --win--> Open (for good)
//   Minigame --lose--> Full, minigame locked for 10 s (the water stays)
//   Minigame --quit (Esc)--> Full, no penalty
//   At night every gate is shut, even an Open one; at dawn an Open gate opens again.
//
// A valve gate (V tile) has no basin: it starts Full, so it goes straight to the minigame.
//
// DESIGN RULES: R8.8 (valve gates), R8.9 (lock basins), R8.21 / R8.23 (shut at night),
// R9.2 (quit with no penalty), R9.3 (lose: 10 s lockout).
//
// HOW TO USE: LockBasin_Manager owns one of these per gate. You don't put it on an object.
// HOW TO EDIT: litres per gate and the lockout are in GameBalance.
// ============================================================================

using System;

namespace TheLastDrop
{
    /// <summary>Where a gate's lock is in its life.</summary>
    public enum GateLockState
    {
        /// <summary>No water poured yet.</summary>
        Empty,
        /// <summary>Some water poured, not full yet.</summary>
        Filling,
        /// <summary>The basin is full: the valve minigame can be started.</summary>
        Full,
        /// <summary>The valve minigame is being played.</summary>
        Minigame,
        /// <summary>The minigame was won: the gate is open for good (but shut at night).</summary>
        Open
    }

    /// <summary>One gate's lock: water poured, minigame, open or shut.</summary>
    public class GateLockLogic
    {
        /// <summary>Litres needed to fill the basin. 0 for a valve gate (no basin).</summary>
        public float Capacity { get; }

        /// <summary>Litres poured so far. Stays between visits and after a lost minigame.</summary>
        public float Litres { get; private set; }

        /// <summary>The current state.</summary>
        public GateLockState State { get; private set; }

        /// <summary>True during the night lockdown: the gate is shut and the minigame can't start.</summary>
        public bool NightLocked { get; set; }

        /// <summary>Game time (seconds) until which the minigame stays locked after a loss.</summary>
        public float LockoutUntil { get; private set; } = float.NegativeInfinity;

        /// <summary>True if the player can walk through right now (won, and not night).</summary>
        public bool IsPassable => State == GateLockState.Open && !NightLocked;

        /// <summary>0 to 1: how full the basin is (1 for a valve gate).</summary>
        public float Fill => Capacity <= 0f ? 1f : Math.Min(1f, Litres / Capacity);

        /// <summary>Makes a lock with nothing poured.</summary>
        public GateLockLogic(float capacity)
        {
            Capacity = Math.Max(0f, capacity);
            Restore(0f, false);
        }

        /// <summary>True while more water can go in.</summary>
        public bool CanPour => State == GateLockState.Empty || State == GateLockState.Filling;

        /// <summary>
        /// Pours up to <paramref name="litres"/> into the basin. Returns how much actually went in
        /// (less if the basin got full).
        /// </summary>
        public float Pour(float litres)
        {
            if (!CanPour || litres <= 0f) return 0f;
            float added = Math.Min(litres, Capacity - Litres);
            Litres += added;
            UpdateFillState();
            return added;
        }

        /// <summary>True if the minigame may start now (<paramref name="now"/> = game time in seconds).</summary>
        public bool CanStartMinigame(float now) => State == GateLockState.Full && !NightLocked && now >= LockoutUntil;

        /// <summary>Seconds of lockout left (0 if none).</summary>
        public float LockoutLeft(float now) => Math.Max(0f, LockoutUntil - now);

        /// <summary>Starts the minigame if allowed. Returns false if not.</summary>
        public bool StartMinigame(float now)
        {
            if (!CanStartMinigame(now)) return false;
            State = GateLockState.Minigame;
            return true;
        }

        /// <summary>
        /// The minigame ended. Won: open for good. Lost: back to Full and locked for
        /// <paramref name="lockoutSeconds"/>. Quit: back to Full, no penalty.
        /// </summary>
        public void FinishMinigame(MinigameResult result, float now, float lockoutSeconds)
        {
            if (State != GateLockState.Minigame) return;
            switch (result)
            {
                case MinigameResult.Won:
                    State = GateLockState.Open;
                    break;
                case MinigameResult.Lost:
                    State = GateLockState.Full;
                    LockoutUntil = now + lockoutSeconds;
                    break;
                default:
                    State = GateLockState.Full;
                    break;
            }
        }

        /// <summary>Sets the litres and open flag straight away (loading a save, or tests).</summary>
        public void Restore(float litres, bool open)
        {
            Litres = Math.Max(0f, Math.Min(litres, Capacity));
            LockoutUntil = float.NegativeInfinity;
            if (open) State = GateLockState.Open;
            else UpdateFillState();
        }

        void UpdateFillState()
        {
            if (Litres >= Capacity) State = GateLockState.Full;
            else State = Litres > 0f ? GateLockState.Filling : GateLockState.Empty;
        }
    }
}
