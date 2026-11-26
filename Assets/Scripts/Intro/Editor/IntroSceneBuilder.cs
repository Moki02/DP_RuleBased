// ============================================================================
// IntroSceneBuilder.cs   (editor only)
// Salok > Intro > Build Intro Scene
//   Makes, once:
//   - Assets/Resources/Intro/IntroScript.asset with the default script, sounds and timings
//     (never overwritten once it exists: edit it freely);
//   - Assets/Scenes/Intro.unity: camera, a real Canvas (background, beat text, title card,
//     "Hold Enter to skip" hint) and the Intro / Intro Audio objects, all editable in the
//     Hierarchy. An existing Intro scene is left alone unless you confirm a rebuild;
//   - the Intro scene in the Build Profiles scene list, right after MainMenu.
//
// DESIGN RULES: none (intro).
//
// HOW TO USE: run the menu item once. Then edit the scene and the asset by hand.
// HOW TO EDIT: the default words are in DefaultBeats below (only used for a new asset).
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop.EditorTools
{
    /// <summary>Builds the Intro scene and its script asset.</summary>
    public static class IntroSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Intro.unity";
        public const string ScriptPath = "Assets/Resources/Intro/IntroScript.asset";

        const string MonoFont = "Assets/Art/UI/Font/jetbrains-mono.asset";
        const string HeaderFont = "Assets/Art/UI/Font/BarlowCondensed-Bold SDF.asset";
        const string TitleSprite = "Assets/Art/UI/Title Card/salok-logo E.png";

        const string Click = "Assets/Audio/SFX/UI/sfx_ui_click_03.wav";
        const string Heatwave = "Assets/Audio/Ambience/sfx_ambience_heatwave_01.wav";
        const string Tension = "Assets/Audio/Ambience/sfx_ambience_tension_01.wav";
        const string HubDay = "Assets/Audio/Ambience/sfx_ambience_hubday_01.wav";
        const string Siren = "Assets/Audio/SFX/World/sfx_world_lockdown_06.wav";
        const string Collapse = "Assets/Audio/SFX/Player/sfx_player_collapse_06.wav";
        const string GateSlide = "Assets/Audio/SFX/Gates/sfx_gates_gateslide_01.wav";
        const string DoorRise = "Assets/Audio/SFX/Gates/sfx_gates_doorrise_01.wav";

        static readonly Color Background = UiBuilder.Hex("#241B15");
        static readonly Color Body = UiBuilder.Hex("#F5EFE6");

        [MenuItem("Salok/Intro/Build Intro Scene")]
        public static void Build()
        {
            var script = MakeScriptAsset();
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Intro scene", "Assets/Scenes/Intro.unity already exists. Rebuild it from scratch?\n" +
                                             "(Changes made to it by hand are lost. The IntroScript asset is kept.)", "Rebuild", "Keep it"))
            {
                AddToBuild();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene(script);
            AddToBuild();
        }

        /// <summary>Same as Build, without questions (rebuilds the scene; keeps an existing asset). For scripted setup.</summary>
        public static void BuildQuiet()
        {
            var script = MakeScriptAsset();
            BuildScene(script);
            AddToBuild();
        }

        // ------------------------------------------------------------------ Script asset

        static IntroScript MakeScriptAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<IntroScript>(ScriptPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(ScriptPath));
            var s = ScriptableObject.CreateInstance<IntroScript>();
            s.clickClip = Clip(Click);
            s.beats = DefaultBeats();
            AssetDatabase.CreateAsset(s, ScriptPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        static List<IntroBeat> DefaultBeats() => new List<IntroBeat>
        {
            new IntroBeat
            {
                label = "1. Drought",
                text = "The rain stopped coming last year.\nBy May, the well was dry.\nBy June, the carabao were gone.",
                cues = { Cue(IntroCue.Action.StartLoop, Heatwave, 0, 0.55f, 2.5f) }
            },
            new IntroBeat
            {
                label = "2. The water",
                text = "The tap water was contaminated.\nNanay boiled the same water three times.\nThen she told us it was clean.\n" +
                       "We knew it wasn't, but we drank it anyway.",
                cues = { Cue(IntroCue.Action.StopLoop, Heatwave, 0, 0f, 2.5f) }
            },
            new IntroBeat
            {
                label = "3. The campus",
                text = "Every night, Dalisay Data Campus lit up the sky like a city.\nYou could hear it from afar. A low, cold hum.\n" +
                       "It drank more in a day than San Rafael did in a month.\nSo I took a water jug and wire cutters.\n" +
                       "I only meant to take back what was ours.",
                cues = { Cue(IntroCue.Action.StartLoop, Tension, 0, 0.45f, 3f) }
            },
            new IntroBeat
            {
                label = "4. Caught",
                text = "I got as far as the first pipe.\nThen a hand closed over my mouth.\n" +
                       "[LOUDSPEAKER] SUBJECT {NAME} SECURED. TRANSFER TO RESERVOIR C.",
                blackAfter = 2f,
                cues =
                {
                    Cue(IntroCue.Action.OneShot, Siren, 0, 0.6f, 0f),
                    Cue(IntroCue.Action.OneShot, Collapse, 2, 0.8f, 0f),
                    Cue(IntroCue.Action.StopAllLoops, null, 2, 0f, 0.8f),
                }
            },
            new IntroBeat
            {
                label = "5. The cage",
                text = "I woke to the sound of chains.\nRust in my mouth. Iron under my back. The floor, rising.\n" +
                       "Whoever built this cage built it to carry people up.\nI wondered how many had ridden it before me.",
                cues =
                {
                    Cue(IntroCue.Action.OneShot, GateSlide, 0, 0.7f, 0f),
                    Cue(IntroCue.Action.OneShot, DoorRise, 1, 0.7f, 0f),
                }
            },
            new IntroBeat
            {
                label = "6. The reservoir",
                text = "The cage stopped.\nNo guards. No voices.\n" +
                       "Just a dry reservoir, walls taller than anything in San Rafael, and a pump standing in the dust.\n" +
                       "But I knew that beyond the rim, the campus kept humming. Cold. Still drinking.\n" +
                       "Nobody told me how to leave.\nSo I would find out.",
                cues =
                {
                    Cue(IntroCue.Action.StartLoop, Heatwave, 0, 0.5f, 2f),
                    Cue(IntroCue.Action.StartLoop, HubDay, 0, 0.35f, 4f, delay: 1f),
                }
            },
            new IntroBeat
            {
                label = "7. The letter",
                text = "I hope someone still remembers me,\n- {name}",
                speedScale = 0.5f,
                pauseBetweenLines = 1.5f,
            },
        };

        static IntroCue Cue(IntroCue.Action action, string clipPath, int beforeLine, float volume, float fade, float delay = 0f) =>
            new IntroCue
            {
                action = action,
                clip = clipPath != null ? Clip(clipPath) : null,
                beforeLine = beforeLine,
                volume = volume,
                fadeSeconds = fade,
                delay = delay
            };

        static AudioClip Clip(string path)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c == null) Debug.LogWarning("[IntroSceneBuilder] Missing sound: " + path);
            return c;
        }

        // ------------------------------------------------------------------ Scene

        static void BuildScene(IntroScript script)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.cullingMask = 0;

            var canvas = UiBuilder.CreateCanvas("Intro Canvas", null, 0);
            var root = canvas.transform;

            UiBuilder.Image("Background", root, Background, blocksClicks: false);

            var mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFont);
            var header = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeaderFont);

            var text = UiBuilder.Text("Beat Text", root, "", 44f, Body, TextAlignmentOptions.Center);
            if (mono != null) text.font = mono;
            text.lineSpacing = 30f;
            UiBuilder.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720f, 820f));
            var beatGroup = text.gameObject.AddComponent<CanvasGroup>();
            beatGroup.interactable = false;
            beatGroup.blocksRaycasts = false;

            // Title card: the title-card sign sprite (as on the main menu, it has no words on it)
            // with "SALOK" in Barlow Condensed Bold; just the text if the sprite is missing.
            var titleRt = UiBuilder.Rect("Title Card", root);
            var titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();
            titleGroup.blocksRaycasts = false;
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(TitleSprite);
            if (logo != null)
            {
                var img = UiBuilder.Image("Logo", titleRt, Color.white);
                img.sprite = logo;
                img.preserveAspect = true;
                UiBuilder.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 810f));
            }
            var title = UiBuilder.Text("Title", titleRt, "SALOK", 200f, Body, TextAlignmentOptions.Center);
            if (header != null) title.font = header;
            title.characterSpacing = 8f;
            UiBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1000f, 260f));

            // Skip hint, bottom right, with a thin bar that fills while Enter / Esc is held.
            var hintRt = UiBuilder.Rect("Skip Hint", root);
            UiBuilder.Place(hintRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(360f, 44f));
            var hintGroup = hintRt.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            var hintText = UiBuilder.Text("Label", hintRt, "Hold Enter to skip", 24f, new Color(Body.r, Body.g, Body.b, 0.6f),
                                          TextAlignmentOptions.Right);
            if (mono != null) hintText.font = mono;
            var bar = UiBuilder.Image("Hold Bar", hintRt, new Color(Body.r, Body.g, Body.b, 0.15f));
            UiBuilder.Place(bar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -6f), new Vector2(260f, 3f));
            var fill = UiBuilder.Image("Fill", bar.transform, Body);
            fill.sprite = WhiteSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            // Sounds: clicks and one-shots follow SFX, loops follow Music (SettingsVolume).
            var audioGo = new GameObject("Intro Audio", typeof(IntroAudio));
            var introAudio = audioGo.GetComponent<IntroAudio>();
            introAudio.clicks = Source(audioGo.transform, "Clicks", SettingsVolume.Kind.Sfx);
            introAudio.oneShots = Source(audioGo.transform, "One Shots", SettingsVolume.Kind.Sfx);
            introAudio.loops = new[]
            {
                Source(audioGo.transform, "Loop A", SettingsVolume.Kind.Music),
                Source(audioGo.transform, "Loop B", SettingsVolume.Kind.Music),
                Source(audioGo.transform, "Loop C", SettingsVolume.Kind.Music),
            };

            var directorGo = new GameObject("Intro", typeof(IntroDirector));
            var d = directorGo.GetComponent<IntroDirector>();
            d.script = script;
            d.beatText = text;
            d.beatGroup = beatGroup;
            d.titleCard = titleGroup;
            d.skipHint = hintGroup;
            d.skipFill = fill;
            d.introAudio = introAudio;

            titleGroup.alpha = 0f;
            hintGroup.alpha = 0f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[IntroSceneBuilder] Built " + ScenePath);
        }

        static AudioSource Source(Transform parent, string name, SettingsVolume.Kind kind)
        {
            var go = new GameObject(name, typeof(AudioSource), typeof(SettingsVolume));
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = kind == SettingsVolume.Kind.Music;
            s.volume = kind == SettingsVolume.Kind.Music ? 0f : 1f;
            go.GetComponent<SettingsVolume>().kind = kind;
            return s;
        }

        // Filled images need a sprite; Unity's built-in UI sprite.
        static Sprite WhiteSprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ------------------------------------------------------------------ Name Entry (MainMenu scene)

        public const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Salok/Intro/Add Name Entry To Main Menu")]
        public static void AddNameEntryMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string result = AddNameEntry();
            EditorUtility.DisplayDialog("Name Entry", result, "OK");
        }

        /// <summary>
        /// Places the "What is your name?" panel in the MainMenu scene's Canvas, styled as a copy of
        /// the Confirm Dialog (dim backdrop + clipboard box, wood button). Does nothing if one exists.
        /// </summary>
        public static string AddNameEntry()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<NameEntryUI>(FindObjectsInactive.Include) != null)
                return "The MainMenu scene already has a Name Entry panel.";

            var menu = Object.FindAnyObjectByType<MenuSceneUI>(FindObjectsInactive.Include);
            var dialog = Object.FindAnyObjectByType<ConfirmDialog>(FindObjectsInactive.Include);
            if (menu == null || menu.mainPanel == null) return "No MenuSceneUI with a Main Panel in the MainMenu scene.";
            var canvas = menu.mainPanel.GetComponentInParent<Canvas>(true);
            var dialogPanel = dialog != null ? dialog.transform.Find("Panel") : null;

            // The panel: a copy of the Confirm Dialog's backdrop and clipboard box (plain objects, not a prefab).
            GameObject panel;
            if (dialogPanel != null)
            {
                panel = Object.Instantiate(dialogPanel.gameObject, canvas.transform, false);
                foreach (var n in new[] { "Box/Message", "Box/No Button" })
                {
                    var t = panel.transform.Find(n);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
            }
            else
            {
                panel = UiBuilder.Image("Panel", canvas.transform, new Color(0f, 0f, 0f, 0.72f), blocksClicks: true).gameObject;
                var box = UiBuilder.Image("Box", panel.transform, UiBuilder.Hex("#7A4E36"));
                UiBuilder.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));
            }
            panel.name = "Name Entry";
            panel.transform.SetSiblingIndex(menu.mainPanel.transform.GetSiblingIndex() + 1);
            var boxT = panel.transform.Find("Box");

            var title = boxT.Find("Title") != null ? boxT.Find("Title").GetComponent<TMP_Text>()
                                                   : UiBuilder.Text("Title", boxT, "", 48f, UiBuilder.Hex("#2B1C12"), TextAlignmentOptions.Center);
            title.text = "What is your name?";

            // The name box: dark rounded rectangle like the Settings dropdown, JetBrains Mono text.
            var input = MakeInput(boxT);

            // Begin: the Confirm Dialog's Yes button, centred.
            Button begin;
            var yes = boxT.Find("Yes Button");
            if (yes != null)
            {
                yes.name = "Begin Button";
                begin = yes.GetComponent<Button>();
                begin.onClick = new Button.ButtonClickedEvent();
                var rt = (RectTransform)yes;
                rt.anchoredPosition = new Vector2(24f, rt.anchoredPosition.y);
                var label = yes.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "Begin";
            }
            else
            {
                begin = UiBuilder.Button("Begin Button", boxT, "Begin", 32f, UiBuilder.Hex("#EAB308"), UiBuilder.Hex("#241B15"), null);
                UiBuilder.Place((RectTransform)begin.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(24f, 110f), new Vector2(240f, 64f));
            }

            var ui = panel.AddComponent<NameEntryUI>();
            ui.nameInput = input;
            ui.beginButton = begin;
            ui.returnTo = menu.mainPanel;
            menu.nameEntry = ui;
            panel.SetActive(false);

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Added Canvas > Name Entry to the MainMenu scene (hidden until New Game).";
        }

        static TMP_InputField MakeInput(Transform box)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Name Input";
            go.transform.SetParent(box, false);
            var rt = (RectTransform)go.transform;
            UiBuilder.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, -8f), new Vector2(520f, 72f));

            var bg = go.GetComponent<Image>();
            var rect4 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/HUD/Rectangle 4.png");
            if (rect4 != null) { bg.sprite = rect4; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = Background;

            var field = go.GetComponent<TMP_InputField>();
            var mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFont);
            field.characterLimit = PlayerProfile.MaxLength;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = Body;
            field.caretWidth = 3;
            field.selectionColor = new Color(0.92f, 0.70f, 0.03f, 0.45f); // #EAB308
            field.textViewport.offsetMin = new Vector2(20f, 8f);
            field.textViewport.offsetMax = new Vector2(-20f, -8f);

            Style(field.textComponent as TextMeshProUGUI, mono, Body);
            var ph = field.placeholder as TextMeshProUGUI;
            Style(ph, mono, new Color(Body.r, Body.g, Body.b, 0.35f));
            if (ph != null) { ph.text = PlayerProfile.DefaultName; ph.fontStyle = FontStyles.Normal; }
            field.pointSize = 34f;
            return field;
        }

        static void Style(TextMeshProUGUI t, TMP_FontAsset font, Color color)
        {
            if (t == null) return;
            if (font != null) t.font = font;
            t.fontSize = 34f;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
        }

        // ------------------------------------------------------------------ Build list

        /// <summary>Puts the Intro scene in the Build Profiles scene list, right after MainMenu.</summary>
        public static void AddToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            int at = scenes.FindIndex(s => s.path.EndsWith("/MainMenu.unity")) + 1;
            scenes.Insert(Mathf.Clamp(at, 0, scenes.Count), new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[IntroSceneBuilder] Added the Intro scene to the build list.");
        }
    }
}
