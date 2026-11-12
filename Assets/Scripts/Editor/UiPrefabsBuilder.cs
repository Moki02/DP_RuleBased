// ============================================================================
// UiPrefabsBuilder.cs
// Adds "Tools > The Last Drop > Build UI Prefabs". It saves the game's built-in screens as
// prefabs with their own Canvas, so the artists can open and edit every background, text,
// button and panel:
//   Assets/Resources/UI/GameMenu.prefab     - Build / Skills / Map menu (GameMenuView)
//   Assets/Resources/UI/UpgradeCard.prefab  - one upgrade card, copied for every upgrade (UpgradeCardUI)
//   Assets/Resources/UI/Map.prefab          - the map screen and side panel (MapView)
//   Assets/Resources/UI/NotePanel.prefab    - survivor notes and the journal (NotePanelView)
//   Assets/Resources/UI/SimpleHud.prefab    - status lines, toasts, banners, fade, pop-up panel (SimpleHudView)
//   Assets/Resources/UI/HowToPlay.prefab    - the How to Play screen shown after loading (HowToPlayView)
// The game loads these by itself. A different prefab can also be dragged into the slots on
// GameMenu ("Menu Prefab", "Card Prefab") and MapScreen ("Map Prefab") on the Systems object.
//
// SAFE TO RUN AGAIN: a prefab that already exists is left alone, so the artists' work is never
// overwritten. To get the default back, delete the prefab and run this again.
// (Build Menu Scenes runs this too.)
//
// DESIGN RULES: none (tooling).
//
// HOW TO USE: Tools > The Last Drop > Build UI Prefabs, then edit the prefabs in Assets/Resources/UI.
// HOW TO EDIT: the default look is in each view script's BuildDefault.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop.EditorTools
{
    /// <summary>Saves the built-in UI screens as editable prefabs in Assets/Resources/UI.</summary>
    public static class UiPrefabsBuilder
    {
        const string Folder = "Assets/Resources/UI";

        [MenuItem("Tools/The Last Drop/Build UI Prefabs")]
        public static void BuildFromMenu()
        {
            var made = Build();
            Debug.Log(made.Count > 0
                ? $"[UiPrefabsBuilder] Made {string.Join(", ", made)} in {Folder}. Open them to edit the look."
                : $"[UiPrefabsBuilder] All UI prefabs already exist in {Folder}; nothing was changed.");
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/GameMenu.prefab");
            if (menu != null) EditorGUIUtility.PingObject(menu);
        }

        /// <summary>Makes every missing UI prefab. Returns the names of the ones made.</summary>
        public static List<string> Build()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            var made = new List<string>();

            // Hidden helper objects that hold the default words and the black-and-white colours.
            var helper = new GameObject("UI Prefab Builder (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var menu = helper.AddComponent<GameMenu>();
                menu.ApplyBlackAndWhite();
                var map = helper.AddComponent<MapScreen>();
                map.ApplyBlackAndWhite();

                Make("GameMenu", made, () => GameMenuView.BuildDefault(null, menu).gameObject);
                Make("UpgradeCard", made, () => UpgradeCardUI.BuildDefault(null, menu).gameObject);
                Make("Map", made, () => MapView.BuildDefault(null, map).gameObject);
                Make("NotePanel", made, () => NotePanelView.BuildDefault(null).gameObject);
                Make("SimpleHud", made, () => SimpleHudView.BuildDefault(null).gameObject);
                Make("HowToPlay", made, () => HowToPlayView.BuildDefault(null).gameObject);
            }
            finally
            {
                Object.DestroyImmediate(helper);
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        // Builds one screen, saves it as a prefab (if it doesn't exist yet) and removes the copy.
        static void Make(string name, List<string> made, System.Func<GameObject> build)
        {
            string path = $"{Folder}/{name}.prefab";
            if (File.Exists(path)) return;
            var go = build();
            go.name = name;
            go.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            made.Add(name);
        }
    }
}
