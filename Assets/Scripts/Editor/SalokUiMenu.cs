// ============================================================================
// SalokUiMenu.cs
// Adds "Tools > Salok > Select All UI": selects the "UI" object of the open scene(s) and
// unfolds it in the Hierarchy, so every screen can be found fast. Each scene keeps all of its
// UI under that one object, one child per screen (HUD, Pause, Map, Confirm Dialog ...).
// Screens that start hidden are there too, switched off: tick them on to edit, and switch
// them off again before saving (the game shows them itself).
//
// DESIGN RULES: none (tooling, T0).
//
// HOW TO USE: Tools > Salok > Select All UI.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop.EditorTools
{
    /// <summary>Tools > Salok > Select All UI.</summary>
    public static class SalokUiMenu
    {
        /// <summary>The name of the object that holds every UI screen in a scene.</summary>
        public const string UiRootName = "UI";

        [MenuItem("Tools/Salok/Select All UI")]
        public static void SelectAllUi()
        {
            var found = new List<Object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == UiRootName) found.Add(root);
            }

            if (found.Count == 0)
            {
                // A scene without the "UI" object: select its canvases instead.
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.isRootCanvas) found.Add(c.gameObject);
                Debug.LogWarning(found.Count > 0
                    ? "[Salok] This scene has no \"UI\" object; its canvases are selected instead."
                    : "[Salok] This scene has no \"UI\" object and no Canvas.");
            }

            Selection.objects = found.ToArray();
            if (found.Count > 0)
            {
                EditorGUIUtility.PingObject(found[0]);
                EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                foreach (var o in found) ExpandInHierarchy((GameObject)o);
            }
        }

        // Unfolds the object one level in the Hierarchy so its screens show.
        static void ExpandInHierarchy(GameObject go)
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (type == null) return;
            var window = EditorWindow.GetWindow(type);
            var method = type.GetMethod("SetExpanded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                                        null, new[] { typeof(int), typeof(bool) }, null);
            method?.Invoke(window, new object[] { go.GetInstanceID(), true });
        }
    }
}
