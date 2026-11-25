// ============================================================================
// HubSceneMenu.cs
// Puts the hub's objects and the gate basins into the open game scene, so they are in the
// hierarchy (and can be moved by hand) instead of being made when Play is pressed:
//
//   Salok > Build > 2. Place hub objects in scene
//       Makes "HubObjects": every Raw Basin / Clean Storage tier, the Clean Storage level
//       gauge, the spare jugs, Rain Catcher / Solar Still / Deep Well, the Biosand Filter
//       and their labels (see HubSceneBuilder). Old runtime-only "HubPlaceholders" leftovers
//       are not touched. Running it again REPLACES HubObjects (hand moves are lost).
//   Salok > Build > 3. Place gate basins in scene
//       Makes all 16 lock basins under Systems/LockBasins, standing on the floor.
//       Running it again REPLACES them.
//
// Run 1. Set up finished models first, so Resources/HubModels has the models.
// Save the scene afterwards (the menus mark it dirty; Ctrl+S).
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Editor menus that place the hub objects and gate basins in the open scene.</summary>
    public static class HubSceneMenu
    {
        [MenuItem("Salok/Build/2. Place hub objects in scene", priority = 2)]
        public static void PlaceHubObjects()
        {
            var maze = Object.FindAnyObjectByType<MazeBuilder>();
            var provider = Object.FindAnyObjectByType<BalanceProvider>();
            if (maze == null || provider == null || provider.balance == null)
            {
                EditorUtility.DisplayDialog("Place hub objects", "Open the game scene first (it needs the Maze and a BalanceProvider with GameBalance).", "OK");
                return;
            }
            if (maze.Grid == null) maze.LoadGrid();
            if (maze.Grid == null)
            {
                EditorUtility.DisplayDialog("Place hub objects", "The maze has no layout CSV.", "OK");
                return;
            }

            var old = Object.FindAnyObjectByType<HubSceneObjects>(FindObjectsInactive.Include);
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("Place hub objects",
                        "HubObjects is already in the scene. Replace it? Any hand-moved hub objects go back to their default tiles.",
                        "Replace", "Cancel"))
                    return;
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var hub = HubSceneBuilder.Build(maze, provider.balance);
            Undo.RegisterCreatedObjectUndo(hub.gameObject, "Place hub objects");
            var tap = Object.FindAnyObjectByType<HubTap_Manager>(FindObjectsInactive.Include);
            if (tap != null) EditorUtility.SetDirty(tap.gameObject);
            Selection.activeGameObject = hub.gameObject;
            EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
            Debug.Log("[HubSceneMenu] Hub objects placed under HubObjects. Save the scene.", hub);
        }

        [MenuItem("Salok/Build/3. Place gate basins in scene", priority = 3)]
        public static void PlaceGateBasins()
        {
            var gates = Object.FindAnyObjectByType<GateManager>(FindObjectsInactive.Include);
            if (gates == null)
            {
                EditorUtility.DisplayDialog("Place gate basins", "Open the game scene first (it needs the GateManager on Systems).", "OK");
                return;
            }
            if (gates.transform.Find(GateManager.BasinsParentName) != null &&
                !EditorUtility.DisplayDialog("Place gate basins",
                    "The gate basins are already in the scene. Replace them? Hand moves are lost.", "Replace", "Cancel"))
                return;

            gates.PlaceBasinsInScene();
            var parent = gates.transform.Find(GateManager.BasinsParentName);
            if (parent != null)
            {
                Undo.RegisterCreatedObjectUndo(parent.gameObject, "Place gate basins");
                Selection.activeGameObject = parent.gameObject;
                Debug.Log($"[HubSceneMenu] {parent.childCount} gate basins placed under {gates.name}/{GateManager.BasinsParentName}. Save the scene.", parent);
            }
            EditorSceneManager.MarkSceneDirty(gates.gameObject.scene);
        }
    }
}
