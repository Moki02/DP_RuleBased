// ============================================================================
// PlayerProfile.cs
// The player's name, typed on the Name Entry screen before every New Game.
//   - PlayerProfile.Name is the name of the run being played (Win, GameOver, the intro read it).
//   - The last name typed is kept in PlayerPrefs, so the Name Entry box is pre-filled with it.
//   - The run's name is also written into the save file (SaveData.playerName), so Continue
//     brings it back.
// Name rules (Clean): spaces trimmed at both ends, at most MaxLength characters,
// DefaultName if nothing is left.
//
// DESIGN RULES: none (intro / name entry).
//
// HOW TO USE: read PlayerProfile.Name. Fill("... {name} ...") swaps {name} for the name and
// {NAME} for the name in capitals.
// HOW TO EDIT: the length and default name are the constants below.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The player's name for this run, plus the last name typed (PlayerPrefs).</summary>
    public static class PlayerProfile
    {
        /// <summary>Longest name allowed.</summary>
        public const int MaxLength = 16;
        /// <summary>The name used when the box is left empty.</summary>
        public const string DefaultName = "Survivor";
        /// <summary>PlayerPrefs key holding the last name typed.</summary>
        public const string PrefKey = "salok.playerName";

        static string current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        /// <summary>The name of the run being played (the last name typed if none was set yet, else DefaultName).</summary>
        public static string Name
        {
            get
            {
                if (string.IsNullOrEmpty(current)) current = Clean(LastUsedName);
                return current;
            }
        }

        /// <summary>The last name typed on the Name Entry screen, or "" if none.</summary>
        public static string LastUsedName => PlayerPrefs.GetString(PrefKey, "");

        /// <summary>Cleans <paramref name="raw"/> and makes it this run's name and the remembered name. Returns the clean name.</summary>
        public static string SetName(string raw)
        {
            current = Clean(raw);
            PlayerPrefs.SetString(PrefKey, current);
            PlayerPrefs.Save();
            return current;
        }

        /// <summary>Makes <paramref name="raw"/> this run's name without changing the remembered one (a loaded save).</summary>
        public static void SetCurrent(string raw) => current = Clean(raw);

        /// <summary>The name rules: trim, at most MaxLength characters, DefaultName if empty.</summary>
        public static string Clean(string raw)
        {
            if (raw == null) return DefaultName;
            string s = raw.Trim();
            if (s.Length > MaxLength) s = s.Substring(0, MaxLength).TrimEnd();
            return s.Length == 0 ? DefaultName : s;
        }

        /// <summary>Swaps {name} for <see cref="Name"/> and {NAME} for it in capitals.</summary>
        public static string Fill(string text) => Fill(text, Name);

        /// <summary>Swaps {name} for <paramref name="name"/> and {NAME} for it in capitals.</summary>
        public static string Fill(string text, string name)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text ?? "";
            name = name ?? "";
            return text.Replace("{NAME}", name.ToUpperInvariant()).Replace("{name}", name);
        }
    }
}
