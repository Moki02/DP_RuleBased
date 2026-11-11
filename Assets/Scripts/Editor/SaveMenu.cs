// ============================================================================
// SaveMenu.cs   (Editor only)
// Adds "Tools > The Last Drop > Delete Save" (deletes the one save slot) and
// "Tools > The Last Drop > Show Save File" (opens its folder).
//
// DESIGN RULES: R11.8.
//
// HOW TO USE: run the menu. Works in and out of Play mode.
// HOW TO EDIT: nothing to tune; the path is SaveManager.SavePath.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Editor menu items for the save file.</summary>
    public static class SaveMenu
    {
        /// <summary>Deletes the save after asking.</summary>
        [MenuItem("Tools/The Last Drop/Delete Save")]
        public static void DeleteSave()
        {
            if (!SaveManager.HasSave)
            {
                EditorUtility.DisplayDialog("Delete Save", "There is no save file.\n\n" + SaveManager.SavePath, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete Save", "Delete the save file? This can't be undone.\n\n" + SaveManager.SavePath, "Delete", "Cancel"))
                return;
            SaveManager.DeleteSave();
            Debug.Log("[SaveMenu] Save deleted: " + SaveManager.SavePath);
        }

        /// <summary>Opens the folder that holds the save file.</summary>
        [MenuItem("Tools/The Last Drop/Show Save File")]
        public static void ShowSaveFile()
        {
            string dir = Path.GetDirectoryName(SaveManager.SavePath);
            if (SaveManager.HasSave) EditorUtility.RevealInFinder(SaveManager.SavePath);
            else if (Directory.Exists(dir)) EditorUtility.RevealInFinder(dir);
            else Debug.Log("[SaveMenu] No save folder yet: " + dir);
        }
    }
}
