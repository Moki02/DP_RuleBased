// ============================================================================
// SaveFile.cs
// Turns SaveData into JSON text and back, checks it, and writes it safely:
//   - the file has a "version" number; a file with another number (an old save) is
//     rejected with a clear message instead of loading wrong;
//   - writing goes to a temporary file first and then replaces the real one, so a crash
//     in the middle of saving can't leave a half-written save behind.
// Plain C# (tested), apart from Unity's JsonUtility.
//
// DESIGN RULES: R11.8.
//
// HOW TO USE: SaveManager calls Write(path, data) and TryLoad(path, out data, out error).
// HOW TO EDIT: raise CurrentVersion when SaveData changes in a way old files can't read.
// ============================================================================

using System;
using System.IO;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Reads, checks and safely writes save files.</summary>
    public static class SaveFile
    {
        /// <summary>The save format this build reads and writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>The save as JSON text (sets the version number).</summary>
        public static string ToJson(SaveData data)
        {
            data.version = CurrentVersion;
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// Reads JSON text into SaveData. Returns false, with a reason in <paramref name="error"/>,
        /// for empty or broken text, the wrong version, or impossible values. Never throws.
        /// </summary>
        public static bool TryParse(string json, out SaveData data, out string error)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "the file is empty"; return false; }

            SaveData parsed;
            try { parsed = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { error = "the file isn't valid save data (" + e.Message + ")"; return false; }

            if (parsed == null) { error = "the file isn't valid save data"; return false; }
            if (parsed.version != CurrentVersion)
            {
                error = $"it was made by another version of the game (save version {parsed.version}, this game reads version {CurrentVersion})";
                return false;
            }
            if (parsed.day < 1) { error = $"day {parsed.day} is impossible"; return false; }
            if (parsed.timeOfDay < 0f) { error = "the time of day is negative"; return false; }

            data = parsed;
            error = null;
            return true;
        }

        /// <summary>Writes the save: first to a temporary file, then swaps it in for the real one.</summary>
        public static void Write(string path, SaveData data)
        {
            string temp = path + ".tmp";
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(temp, ToJson(data));
            // Only now, with the new file complete, does the old one get replaced.
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, null); }
                catch (IOException) { File.Delete(path); File.Move(temp, path); }       // some drives can't Replace
                catch (PlatformNotSupportedException) { File.Delete(path); File.Move(temp, path); }
            }
            else File.Move(temp, path);
        }

        /// <summary>Reads and checks a save file. False (with a reason) if it's missing or bad. Never throws.</summary>
        public static bool TryLoad(string path, out SaveData data, out string error)
        {
            data = null;
            if (!File.Exists(path)) { error = "there is no save file"; return false; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { error = "the file can't be read (" + e.Message + ")"; return false; }
            return TryParse(text, out data, out error);
        }
    }
}
