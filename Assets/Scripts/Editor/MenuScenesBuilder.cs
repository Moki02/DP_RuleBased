// ============================================================================
// MenuScenesBuilder.cs
// Adds "Tools > The Last Drop > Build Menu Scenes". It makes the menu scenes with a real,
// saved Canvas the artists can edit (every background, text, button, slider is a normal
// GameObject in the Hierarchy), plus the loading screen prefab:
//   Assets/Scenes/MainMenu.unity         - Canvas: title, New Game / Continue / Demo /
//                                          Settings / Quit, and the Settings panel (sliders)
//   Assets/Scenes/Win.unity              - Canvas: title, body text, Play Again / Main Menu / Quit
//   Assets/Scenes/GameOver.unity         - same as Win
//   Assets/Resources/LoadingScreen.prefab - Canvas: background, "LOADING...", bar, percent
// Each button's On Click (and each slider's On Value Changed) is set to the screen script's
// method, so it shows in the Inspector and survives any restyling.
// It also makes the in-game UI prefabs (Build UI Prefabs: menu, cards, map, notes, HUD texts)
// and puts the Scene List in order: MainMenu first (a build opens on it), then the game
// scene (Playtest_Maze), Win and GameOver; other scenes stay after these.
//
// SAFE TO RUN AGAIN: a scene or prefab that already has its Canvas set up is left alone, so
// the artists' work is never overwritten. (An old MainMenu / Win / GameOver made by the first
// version of this command, with no Canvas, is replaced.) To start one over, delete it first.
//
// DESIGN RULES: none (tooling).
//
// HOW TO USE: Tools > The Last Drop > Build Menu Scenes, then open MainMenu and press Play.
// HOW TO EDIT: scene paths are the constants below; the scene names must match SceneFlow.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop.EditorTools
{
    /// <summary>Makes the MainMenu, Win and GameOver scenes and the loading screen prefab, all with editable Canvases.</summary>
    public static class MenuScenesBuilder
    {
        const string Folder = "Assets/Scenes";
        const string MainMenuPath = Folder + "/" + SceneFlow.MainMenuScene + ".unity";
        const string WinPath = Folder + "/" + SceneFlow.WinScene + ".unity";
        const string GameOverPath = Folder + "/" + SceneFlow.GameOverScene + ".unity";
        const string GamePath = Folder + "/" + SceneFlow.GameScene + ".unity";
        const string ResourcesFolder = "Assets/Resources";
        const string LoadingPrefabPath = ResourcesFolder + "/" + LoadingScreen.PrefabResource + ".prefab";

        // The default black-and-white look.
        static readonly Color Black = new Color(0.04f, 0.04f, 0.04f);
        static readonly Color ButtonGrey = new Color(0.16f, 0.16f, 0.16f);
        static readonly Color PanelGrey = new Color(0.10f, 0.10f, 0.10f);
        static readonly Color Muted = new Color(0.65f, 0.65f, 0.65f);

        [MenuItem("Tools/The Last Drop/Build Menu Scenes")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            var made = new List<string>();
            if (NeedsBuild<MenuSceneUI>(MainMenuPath, ui => ui.mainPanel != null)) { MakeMainMenu(); made.Add("MainMenu"); }
            if (NeedsBuild<EndSceneUI>(WinPath, ui => ui.titleText != null)) { MakeEndScene(WinPath, EndSceneUI.Kind.Win, "YOU ESCAPED"); made.Add("Win"); }
            if (NeedsBuild<EndSceneUI>(GameOverPath, ui => ui.titleText != null)) { MakeEndScene(GameOverPath, EndSceneUI.Kind.GameOver, "THE DROUGHT WINS"); made.Add("GameOver"); }
            if (!File.Exists(LoadingPrefabPath)) { MakeLoadingPrefab(); made.Add("LoadingScreen prefab"); }
            foreach (var p in UiPrefabsBuilder.Build()) made.Add(p + " prefab");   // the in-game UI prefabs too

            OrderBuildScenes();
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            Debug.Log(made.Count > 0
                ? $"[MenuScenesBuilder] Made: {string.Join(", ", made)}. The rest already had their Canvas and were left alone. Scene List ordered."
                : "[MenuScenesBuilder] Everything already exists with its Canvas; nothing was changed. Scene List ordered.");
        }

        // True if the scene is missing, or exists but its screen script isn't linked to a Canvas yet.
        static bool NeedsBuild<T>(string path, System.Func<T, bool> isSetUp) where T : Object
        {
            if (!File.Exists(path)) return true;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var ui = Object.FindAnyObjectByType<T>();
            return ui == null || !isSetUp(ui);
        }

        // ------------------------------------------------------------------ Main menu

        static void MakeMainMenu()
        {
            Scene scene = NewMenuScene();
            var ui = new GameObject("MainMenu").AddComponent<MenuSceneUI>();
            var canvas = UiBuilder.CreateCanvas("Canvas", null, 10);
            UiBuilder.Image("Background", canvas.transform, Black, blocksClicks: true);

            // Main screen
            var main = UiBuilder.Rect("Main Panel", canvas.transform);
            var title = UiBuilder.Text("Title", main, "SALOK", 110f, Color.white, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            Centre(title.rectTransform, 0f, 300f, 1200f, 140f);
            var sub = UiBuilder.Text("Subtitle", main, "Survive the drought. Find the way out.", 30f, Muted, TextAlignmentOptions.Center);
            Centre(sub.rectTransform, 0f, 210f, 1200f, 50f);

            float y = 90f;
            MenuButton(main, "New Game", ref y, ui.NewGame);
            ui.continueButton = MenuButton(main, "Continue", ref y, ui.Continue);
            MenuButton(main, "Demo", ref y, ui.Demo);
            MenuButton(main, "Settings", ref y, ui.OpenSettings);
            MenuButton(main, "Quit", ref y, ui.Quit);
            ui.mainPanel = main.gameObject;

            // Settings screen
            var settings = UiBuilder.Image("Settings Panel", canvas.transform, PanelGrey, blocksClicks: true).rectTransform;
            Centre(settings, 0f, 0f, 820f, 560f);
            var sTitle = UiBuilder.Text("Title", settings, "SETTINGS", 52f, Color.white, TextAlignmentOptions.Center);
            sTitle.fontStyle = FontStyles.Bold;
            UiBuilder.Place(sTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 70f));

            ui.musicSlider = SliderRow(settings, "Music Volume", -150f, ui.SetMusicVolume, out ui.musicValue);
            ui.sfxSlider = SliderRow(settings, "SFX Volume", -250f, ui.SetSfxVolume, out ui.sfxValue);
            ui.sensitivitySlider = SliderRow(settings, "Mouse Sensitivity", -350f, ui.SetMouseSensitivity, out ui.sensitivityValue);

            var back = UiBuilder.Button("Back Button", settings, "Back", 30f, ButtonGrey, Color.white, null);
            UiBuilder.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 60f));
            Hook(back, ui.CloseSettings);
            Tint(back);
            ui.settingsPanel = settings.gameObject;
            settings.gameObject.SetActive(false);

            EditorUtility.SetDirty(ui);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        static Button MenuButton(RectTransform parent, string label, ref float y, UnityAction onClick)
        {
            var b = UiBuilder.Button(label + " Button", parent, label, 32f, ButtonGrey, Color.white, null);
            Centre((RectTransform)b.transform, 0f, y, 380f, 64f);
            Hook(b, onClick);
            Tint(b);
            y -= 84f;
            return b;
        }

        // One row: label on the left, slider in the middle, value text on the right.
        static Slider SliderRow(RectTransform parent, string label, float y, UnityAction<float> onChange, out TMP_Text valueText)
        {
            var text = UiBuilder.Text(label + " Label", parent, label, 28f, Color.white, TextAlignmentOptions.Left);
            UiBuilder.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(50f, y), new Vector2(280f, 50f));

            var value = UiBuilder.Text(label + " Value", parent, "100%", 26f, Muted, TextAlignmentOptions.Right);
            UiBuilder.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-50f, y), new Vector2(110f, 50f));
            valueText = value;

            // Unity's default slider with no sprites: grey track, white fill and handle.
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = label + " Slider";
            go.transform.SetParent(parent, false);
            UiBuilder.Place((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(350f, y), new Vector2(290f, 24f));
            foreach (var img in go.GetComponentsInChildren<Image>(true))
                img.color = img.name == "Background" ? new Color(0.3f, 0.3f, 0.3f) : Color.white;

            var slider = go.GetComponent<Slider>();
            UnityEventTools.AddPersistentListener(slider.onValueChanged, onChange);
            return slider;
        }

        // ------------------------------------------------------------------ Win / GameOver

        static void MakeEndScene(string path, EndSceneUI.Kind kind, string defaultTitle)
        {
            Scene scene = NewMenuScene();
            var ui = new GameObject(kind + " Screen").AddComponent<EndSceneUI>();
            ui.kind = kind;
            var canvas = UiBuilder.CreateCanvas("Canvas", null, 10);
            UiBuilder.Image("Background", canvas.transform, Black, blocksClicks: true);

            var title = UiBuilder.Text("Title", canvas.transform, defaultTitle, 84f, Color.white, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            Centre(title.rectTransform, 0f, 330f, 1600f, 120f);
            var body = UiBuilder.Text("Body", canvas.transform, "Day and stats go here.", 30f, new Color(0.75f, 0.75f, 0.75f), TextAlignmentOptions.Top);
            UiBuilder.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 250f), new Vector2(1200f, 420f));
            ui.titleText = title;
            ui.bodyText = body;

            float y = -220f;
            EndButton(canvas.transform, "Play Again", ref y, ui.PlayAgain);
            EndButton(canvas.transform, "Main Menu", ref y, ui.MainMenu);
            EndButton(canvas.transform, "Quit", ref y, ui.Quit);

            EditorUtility.SetDirty(ui);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void EndButton(Transform parent, string label, ref float y, UnityAction onClick)
        {
            var b = UiBuilder.Button(label + " Button", parent, label, 30f, ButtonGrey, Color.white, null);
            Centre((RectTransform)b.transform, 0f, y, 360f, 60f);
            Hook(b, onClick);
            Tint(b);
            y -= 78f;
        }

        // ------------------------------------------------------------------ Loading screen prefab

        static void MakeLoadingPrefab()
        {
            if (!Directory.Exists(ResourcesFolder)) Directory.CreateDirectory(ResourcesFolder);
            var canvas = UiBuilder.CreateCanvas("LoadingScreen", null, 200); // drawn over everything
            var screen = canvas.gameObject.AddComponent<LoadingScreen>();
            LoadingScreen.BuildDefaultLook(canvas.transform, screen);
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, LoadingPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
        }

        // ------------------------------------------------------------------ Helpers

        // An empty scene with a black camera and an EventSystem.
        static Scene NewMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return scene;
        }

        static void Centre(RectTransform rt, float x, float y, float w, float h) =>
            UiBuilder.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));

        // Saved in the scene, so it shows in the Button's On Click list.
        static void Hook(Button b, UnityAction action) => UnityEventTools.AddPersistentListener(b.onClick, action);

        // Hover / press shades for a plain grey button.
        static void Tint(Button b)
        {
            var c = b.colors;
            c.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            c.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            c.colorMultiplier = 1.5f;
            b.colors = c;
        }

        static void OrderBuildScenes()
        {
            var wanted = new List<string> { MainMenuPath, GamePath, WinPath, GameOverPath };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in wanted)
            {
                if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
                else Debug.LogWarning($"[MenuScenesBuilder] {p} doesn't exist yet. For the game scene, run Tools > The Last Drop > Build Playtest Scene, then this again.");
            }
            foreach (var s in EditorBuildSettings.scenes)
                if (!wanted.Contains(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
