// ============================================================================
// SalokHudMenu.cs   (Editor only)
// MENU: Salok > UI > Put HUD text in scene
// Places the SimpleHud prefab (the status lines at the bottom right, like "Stage 1: El Niño
// Advisory" and "Boiling pot", plus toasts, banners and the pop-up panel) into the open scene,
// still linked to the prefab. SimpleHud uses this copy at Play instead of making its own, so
// you can see it in the hierarchy and move things with the Rect Tool.
//   - Move the status lines: select the object linked in SimpleHudView > Status Text.
//   - To use the new spot in every scene, right-click the prefab instance > Prefab > Apply All.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Puts the SimpleHud prefab into the open scene so its layout can be edited.</summary>
    public static class SalokHudMenu
    {
        const string PrefabPath = "Assets/Resources/UI/SimpleHud.prefab";

        [MenuItem("Salok/UI/Put HUD text in scene", priority = 1)]
        public static void PutInScene()
        {
            var existing = Object.FindAnyObjectByType<SimpleHudView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeObject = existing.statusText != null ? existing.statusText.gameObject : existing.gameObject;
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log("[SalokHud] The HUD text is already in this scene; selected its status text.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SalokHud] {PrefabPath} is missing. Run Tools > The Last Drop > Build UI Prefabs first.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Put HUD text in scene");
            var view = go.GetComponentInChildren<SimpleHudView>(true);
            Selection.activeObject = view != null && view.statusText != null ? view.statusText.gameObject : go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[SalokHud] HUD text placed. Move Status Text (bottom right) with the Rect Tool, then save the scene.");
        }
    }
}
