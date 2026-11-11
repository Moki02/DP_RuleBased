// ============================================================================
// SaveCodec.cs
// Small helpers that copy the plain-C# game logic into SaveData and back. Kept apart from
// the MonoBehaviours so the save round-trip can be tested without a scene.
//   - Map: revealed tiles are packed one bit per tile (by CSV position) into text, so a
//     95 × 95 map is about 1.5 KB instead of 9,025 true/false values.
//   - Gates: litres and open, by gate index.
//   - Zone 2: the turn count on load comes from the day (it's also saved as a check).
//
// DESIGN RULES: R11.8, R8.28 (map knowledge stays in 0 degree positions).
//
// HOW TO USE: the systems' Capture / Restore methods call these.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Copies game logic into SaveData and back.</summary>
    public static class SaveCodec
    {
        // ------------------------------------------------------------------ Map

        /// <summary>Writes the revealed tiles and markers into the save.</summary>
        public static void CaptureMap(MapReveal map, SaveData data)
        {
            var grid = map.Grid;
            data.mapWidth = grid.Width;
            data.mapHeight = grid.Height;
            var bytes = new byte[(grid.Width * grid.Height + 7) / 8];
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                int i = y * grid.Width + x;
                // Tile i is bit (i % 8) of byte (i / 8).
                if (map.IsRevealedLocal(x, y)) bytes[i >> 3] |= (byte)(1 << (i & 7));
            }
            data.revealedTiles = Convert.ToBase64String(bytes);
            data.markers = new System.Collections.Generic.List<Vector2Int>(map.MarkersLocal);
        }

        /// <summary>
        /// Puts the revealed tiles and markers back (the map is cleared first). Returns false
        /// (and leaves the map black) if the save was made for a different-sized maze.
        /// </summary>
        public static bool RestoreMap(MapReveal map, SaveData data)
        {
            var grid = map.Grid;
            map.Clear();
            if (data.mapWidth != grid.Width || data.mapHeight != grid.Height) return false;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(data.revealedTiles ?? ""); }
            catch (FormatException) { return false; }
            if (bytes.Length < (grid.Width * grid.Height + 7) / 8) return false;

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                int i = y * grid.Width + x;
                if ((bytes[i >> 3] & (1 << (i & 7))) != 0) map.RevealLocal(new Vector2Int(x, y));
            }
            if (data.markers != null)
                foreach (var m in data.markers) map.AddMarkerLocal(m);
            return true;
        }

        // ------------------------------------------------------------------ Zone 2

        /// <summary>Zone 2's turn count for a loaded save, from its day and time (R8.24).</summary>
        public static int Zone2TurnsFor(SaveData data, float daylightSeconds, int daysBetweenTurns)
        {
            bool night = data.timeOfDay >= daylightSeconds;
            return Zone2TurnMath.TurnsForDay(data.day, night, daysBetweenTurns);
        }

        // ------------------------------------------------------------------ Gates

        /// <summary>Adds one gate's state to the save.</summary>
        public static void CaptureGate(int index, GateLockLogic gate, SaveData data)
        {
            data.gates.Add(new GateSave
            {
                index = index,
                litres = gate.Litres,
                open = gate.State == GateLockState.Open,
            });
        }

        /// <summary>The saved state of gate <paramref name="index"/>, or null if the save doesn't have it.</summary>
        public static GateSave FindGate(SaveData data, int index)
        {
            if (data.gates == null) return null;
            foreach (var g in data.gates) if (g.index == index) return g;
            return null;
        }

        // ------------------------------------------------------------------ Menu

        /// <summary>The saved menu page, or Build if the text isn't a page name.</summary>
        public static MenuPage PageFrom(SaveData data) =>
            Enum.TryParse(data.menuLastPage, out MenuPage page) ? page : MenuPage.Build;
    }
}
