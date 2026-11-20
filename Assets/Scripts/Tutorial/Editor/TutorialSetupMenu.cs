// ============================================================================
// TutorialSetupMenu.cs
// Editor menu for the guided tutorial:
//   Salok > Tutorial > Set Up Tutorial
//       Makes whatever is missing, and never overwrites the artists' work:
//       - Assets/Resources/UI/Tutorial.prefab           the tutorial screen (TutorialView)
//       - Assets/Resources/Tutorial/TutorialTexts.asset  every word the tutorial shows
//       - a "Skip tutorial" toggle under the main menu's buttons (MainMenu scene), wired to
//         MenuSceneUI.SetSkipTutorial
//   Salok > Tutorial > Play Tutorial Again
//       Clears the "tutorial done" flag, so the next New Game runs it (same as unticking
//       "Skip tutorial" in the main menu).
//
// DESIGN RULES: none (tooling).
//
// HOW TO USE: run Set Up Tutorial once (with the game scene saved). Then edit the prefab and
// the texts asset like any other asset. Delete one and run the menu again to get the default back.
// HOW TO EDIT: the default look is TutorialView.BuildDefault; the default words are
// TutorialTexts.Defaults.
// ============================================================================

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop.EditorTools
{
    /// <summary>Makes the tutorial's prefab, texts asset and main menu toggle.</summary>
    public static class TutorialSetupMenu
    {
        const string PrefabPath = "Assets/Resources/UI/Tutorial.prefab";
        const string TextsFolder = "Assets/Resources/Tutorial";
        const string TextsPath = TextsFolder + "/TutorialTexts.asset";
        const string MainMenuScene = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Salok/Tutorial/Set Up Tutorial")]
        public static void SetUp()
        {
            var log = new System.Text.StringBuilder("[Tutorial] Set Up Tutorial:");
            log.Append(MakePrefab() ? "\n  made " + PrefabPath : "\n  " + PrefabPath + " already exists (kept)");
            log.Append(MakeTexts() ? "\n  made " + TextsPath : "\n  " + TextsPath + " already exists (kept)");
            log.Append("\n  " + AddMainMenuToggle());
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        [MenuItem("Salok/Tutorial/Play Tutorial Again")]
        public static void PlayAgain()
        {
            TutorialDirector.SkipNextNewGame = false;
            Debug.Log("[Tutorial] The next New Game runs the tutorial (the 'Skip tutorial' flag is cleared).");
        }

        /// <summary>Saves the default tutorial screen as a prefab, if there isn't one. True if made.</summary>
        public static bool MakePrefab()
        {
            if (File.Exists(PrefabPath)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var view = TutorialView.BuildDefault(null);
            var go = view.gameObject;
            go.name = "Tutorial";
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return true;
        }

        /// <summary>Saves the default words as the texts asset, if there isn't one. True if made.</summary>
        public static bool MakeTexts()
        {
            if (File.Exists(TextsPath)) return false;
            Directory.CreateDirectory(TextsFolder);
            var texts = ScriptableObject.CreateInstance<TutorialTexts>();
            AssetDatabase.CreateAsset(texts, TextsPath);
            return true;
        }

        // Adds "Skip tutorial" under the main menu's buttons. Returns what happened.
        static string AddMainMenuToggle()
        {
            if (!File.Exists(MainMenuScene)) return MainMenuScene + " not found (Tools > The Last Drop > Build Menu Scenes makes it); no toggle added.";

            Scene scene = SceneManager.GetSceneByPath(MainMenuScene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Additive);

            string result;
            MenuSceneUI ui = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                ui = root.GetComponentInChildren<MenuSceneUI>(true);
                if (ui != null) break;
            }

            if (ui == null) result = "no MenuSceneUI in the MainMenu scene; no toggle added.";
            else if (ui.skipTutorialToggle != null) result = "the main menu already has its Skip tutorial toggle (kept).";
            else if (ui.mainPanel == null) result = "MenuSceneUI has no Main Panel set; no toggle added.";
            else
            {
                ui.skipTutorialToggle = MakeToggle((RectTransform)ui.mainPanel.transform, ui);
                EditorUtility.SetDirty(ui);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                result = "added a Skip tutorial toggle to the MainMenu scene (Main Panel > Skip Tutorial Toggle).";
            }

            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            return result;
        }

        static Toggle MakeToggle(RectTransform parent, MenuSceneUI ui)
        {
            // Under the lowest button of the main panel.
            float y = 0f;
            bool any = false;
            foreach (var b in parent.GetComponentsInChildren<Button>(true))
            {
                var rt = (RectTransform)b.transform;
                if (rt.parent != parent) continue;
                float bottom = rt.anchoredPosition.y - rt.sizeDelta.y * 0.5f;
                if (!any || bottom < y) { y = bottom; any = true; }
            }
            y = any ? y - 50f : -360f;

            var go = new GameObject("Skip Tutorial Toggle", typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            UiBuilder.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(380f, 44f));

            var box = UiBuilder.Image("Box", rect, new Color(0.16f, 0.16f, 0.16f), blocksClicks: true).rectTransform;
            UiBuilder.Place(box, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(34f, 34f));
            var check = UiBuilder.Image("Checkmark", box, Color.white).rectTransform;
            UiBuilder.Fill(check, 7f, 7f, 7f, 7f);

            var label = UiBuilder.Text("Label", rect, "Skip tutorial", 28f, new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Left);
            UiBuilder.Fill(label.rectTransform, 140f, 0f, 0f, 0f);

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, ui.SetSkipTutorial);
            return toggle;
        }
    }
}
