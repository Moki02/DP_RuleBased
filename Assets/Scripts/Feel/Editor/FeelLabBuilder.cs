// ============================================================================
// FeelLabBuilder.cs   (Editor only)
// Adds two menu items:
//   Salok > Feel > Build FeelLab
//       Makes the VFX (FeelVfxBuilder), the feel prefabs and settings, and the FeelLab
//       scene (Assets/Scenes/FeelLab.unity): a sound board on the left, a row of effects
//       with Play buttons in the middle, and a panel on the right to try every feel
//       component. Nothing in the game scene is touched.
//   Salok > Feel > Refresh Sound Board (missing sounds only / everything)
//       With FeelLab open: makes one button per sound in Assets/Audio (grouped by folder).
//       "Missing sounds only" shows just the options in Assets/Audio/_Candidates (not picked yet).
//       Run it after new sounds are added.
// Files made (in Assets/VFX):
//   Resources/FeelSettings.asset             all the feel tuning numbers
//   Resources/Feel/FloatingText.prefab       "+1.5 L" world text
//   Resources/Feel/LowHydVignette.prefab     low-HYD screen edges (Canvas)
//   Resources/Feel/CollapseFade.prefab       fade to black (Canvas)
//   Resources/Feel/UiHitBurst.prefab         minigame hit ring (UI)
// SAFE TO RUN AGAIN: files and the scene that already exist are kept (only the sound board
// is refreshed), so the artists' changes stay. Delete a file to get the default back.
//
// DESIGN RULES: none (tooling). Nothing here is wired into the game.
//
// HOW TO USE: Salok > Feel > Build FeelLab, then open the scene and press Play.
// HOW TO EDIT: the look of everything is in the prefabs / scene once made. Colours for the
// first build are the constants below (black / white / grey, blue only for water).
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

namespace TheLastDrop.Feel.EditorTools
{
    /// <summary>Builds the feel prefabs and the FeelLab scene, and refreshes its sound board.</summary>
    public static class FeelLabBuilder
    {
        public const string ScenePath = "Assets/Scenes/FeelLab.unity";
        public const string ResourcesFolder = FeelVfxBuilder.VfxFolder + "/Resources";
        public const string FeelResFolder = ResourcesFolder + "/Feel";
        public const string SettingsPath = ResourcesFolder + "/FeelSettings.asset";
        public const string FloatingTextPath = FeelResFolder + "/FloatingText.prefab";
        public const string VignettePrefabPath = FeelResFolder + "/LowHydVignette.prefab";
        public const string FadePrefabPath = FeelResFolder + "/CollapseFade.prefab";
        public const string BurstPrefabPath = FeelResFolder + "/UiHitBurst.prefab";
        const string AudioRoot = "Assets/Audio";

        static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.07f, 0.88f);
        static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        static readonly Color HeaderColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        static readonly Color WaterBlue = new Color(0.25f, 0.6f, 0.95f, 1f);

        // ================================================================ menus

        [MenuItem("Salok/Feel/Build FeelLab")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var log = Build();
            Debug.Log("[FeelLab] Build finished:\n" + string.Join("\n", log));
        }

        [MenuItem("Salok/Feel/Refresh Sound Board (missing sounds only)")]
        public static void RefreshCandidatesFromMenu() => Refresh(true);

        [MenuItem("Salok/Feel/Refresh Sound Board (everything)")]
        public static void RefreshFromMenu() => Refresh(false);

        static void Refresh(bool candidatesOnly)
        {
            var board = Object.FindFirstObjectByType<SoundBoard>();
            if (board == null)
            {
                EditorUtility.DisplayDialog("Refresh Sound Board", "Open Assets/Scenes/FeelLab.unity first (or run Salok > Feel > Build FeelLab).", "OK");
                return;
            }
            int n = FillSoundBoard(board, candidatesOnly);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            EditorSceneManager.SaveScene(board.gameObject.scene);
            Debug.Log("[FeelLab] Sound board refreshed: " + n + " sounds.");
        }

        /// <summary>Builds whatever is missing and saves FeelLab. Returns a log line per item.</summary>
        public static List<string> Build()
        {
            var log = new List<string>();
            bool sceneExists = File.Exists(ScenePath);

            // Work inside FeelLab (or a fresh scene), so temporary objects never touch the game scene.
            var scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            log.AddRange(FeelVfxBuilder.BuildAll());
            BuildFeelPrefabs(log);
            AssetDatabase.SaveAssets();

            if (sceneExists)
            {
                log.Add("kept   " + ScenePath + " (delete it to rebuild the layout)");
            }
            else
            {
                BuildScene(scene);
                log.Add("made   " + ScenePath);
            }

            var board = Object.FindFirstObjectByType<SoundBoard>();
            if (board != null) log.Add("sound board: " + FillSoundBoard(board) + " sounds");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return log;
        }

        // ================================================================ prefabs

        static void BuildFeelPrefabs(List<string> log)
        {
            FeelVfxBuilder.EnsureFolder(FeelResFolder);

            if (!File.Exists(SettingsPath))
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FeelSettings>(), SettingsPath);
                log.Add("made   " + SettingsPath);
            }
            else log.Add("kept   " + SettingsPath);

            MakePrefab(FloatingTextPath, log, BuildFloatingText);
            MakePrefab(VignettePrefabPath, log, BuildVignette);
            MakePrefab(FadePrefabPath, log, BuildFade);
            MakePrefab(BurstPrefabPath, log, BuildBurst);
        }

        static void MakePrefab(string path, List<string> log, System.Func<GameObject> build)
        {
            if (File.Exists(path)) { log.Add("kept   " + path); return; }
            var go = build();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            log.Add("made   " + path);
        }

        static GameObject BuildFloatingText()
        {
            var go = new GameObject("FloatingText");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "+1.5 L";
            tmp.fontSize = 2.5f; // team pick 2026-11-21: half the first size
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);

            // Outline as a saved material (so the prefab doesn't point at a temporary one).
            if (tmp.font != null)
            {
                string matPath = FeelVfxBuilder.MaterialsFolder + "/M_FloatingText_Outline.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(tmp.font.material) { name = "M_FloatingText_Outline" };
                    mat.EnableKeyword("OUTLINE_ON");
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                tmp.fontSharedMaterial = mat;
            }
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<FloatingText>();
            return go;
        }

        static GameObject BuildVignette()
        {
            var canvas = UiBuilder.CreateCanvas("LowHydVignette", null, 900);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>()); // never blocks clicks
            var img = UiBuilder.Image("Vignette", canvas.transform, new Color(0.3f, 0.02f, 0f, 0f));
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FeelVfxBuilder.VignettePath);
            img.enabled = false;
            var v = canvas.gameObject.AddComponent<LowHydVignette>();
            v.image = img;
            return canvas.gameObject;
        }

        static GameObject BuildFade()
        {
            var canvas = UiBuilder.CreateCanvas("CollapseFade", null, 1000);
            canvas.gameObject.AddComponent<CanvasGroup>();
            UiBuilder.Image("Black", canvas.transform, Color.black, blocksClicks: true);
            var fade = canvas.gameObject.AddComponent<CollapseFade>();
            canvas.GetComponent<CanvasGroup>().alpha = 0f;
            fade.name = "CollapseFade";
            return canvas.gameObject;
        }

        static GameObject BuildBurst()
        {
            var go = new GameObject("UiHitBurst", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(120f, 120f);
            var ring = UiBuilder.Image("Ring", go.transform, WaterBlue);
            ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FeelVfxBuilder.RingPath);
            var flash = UiBuilder.Image("Flash", go.transform, new Color(1f, 1f, 1f, 0.9f));
            flash.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FeelVfxBuilder.DotPath);
            UiBuilder.Fill(flash.rectTransform, 30f, 30f, 30f, 30f);
            go.AddComponent<UiBurst>();
            return go;
        }

        // ================================================================ scene

        static void BuildScene(Scene scene)
        {
            // Camera (made by NewScene) gets the shake. Placed to see the effect rows.
            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 5.5f, -12.5f), Quaternion.Euler(18f, 0f, 0f));
            cam.gameObject.AddComponent<CameraShake>();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (sand)";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "Dummy Player";
            dummy.transform.position = new Vector3(0f, 1f, -4.5f);
            var anchor = new GameObject("Float Anchor").transform;
            anchor.SetParent(dummy.transform, false);
            anchor.localPosition = new Vector3(0f, 1.4f, 0f);

            var stations = BuildStations();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var vignette = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefabPath), scene);
            var fade = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FadePrefabPath), scene);

            var canvas = UiBuilder.CreateCanvas("FeelLab Canvas", null, 10);
            BuildSoundBoardPanel(canvas.transform);
            BuildVfxBar(canvas.transform, stations);
            BuildFeelPanel(canvas.transform, anchor, vignette.GetComponent<LowHydVignette>(), fade.GetComponent<CollapseFade>());

            var title = UiBuilder.Text("Title", canvas.transform, "FEELLAB  ·  listen, watch, choose. Nothing here is wired into the game.", 22, HeaderColor, TextAlignmentOptions.Center);
            UiBuilder.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(980f, 40f));
        }

        static Material GroundMaterial()
        {
            string path = FeelVfxBuilder.MaterialsFolder + "/M_FeelLab_Ground.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_FeelLab_Ground" };
            m.SetColor("_BaseColor", new Color(0.78f, 0.66f, 0.47f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        struct Station { public VfxStation station; public string label; }

        static List<Station> BuildStations()
        {
            var list = new List<(string name, string label, float shake, float height)>();
            foreach (var s in FeelVfxBuilder.Specs) list.Add((s.name, s.label, s.shake, s.name == "VFX_Rain" ? 0f : 0.25f));
            list.Add((FeelVfxBuilder.PourStreamName, "Pour stream", 0f, 2.2f));
            list.Add((FeelVfxBuilder.RewardPopName, "Reward pop (map)", 0f, 0.6f));

            var parent = new GameObject("VFX Stations").transform;
            var result = new List<Station>();
            int perRow = 7;
            for (int i = 0; i < list.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, list.Count - row * perRow);
                float x = (col - (inRow - 1) * 0.5f) * 2.1f;
                float z = row * 3.5f;

                var go = new GameObject("Station - " + list[i].label);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, 0f, z);

                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedestal.name = "Pedestal";
                pedestal.transform.SetParent(go.transform, false);
                pedestal.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                pedestal.transform.localScale = new Vector3(1f, 0.2f, 1f);
                Object.DestroyImmediate(pedestal.GetComponent<Collider>());

                var spawn = new GameObject("Spawn Point").transform;
                spawn.SetParent(go.transform, false);
                spawn.localPosition = new Vector3(0f, list[i].height, 0f);

                var label = new GameObject("Label").AddComponent<TextMeshPro>();
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.35f, -0.75f);
                label.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
                label.fontSize = 2f;
                label.alignment = TextAlignmentOptions.Center;
                label.text = list[i].label;
                label.color = Color.white;

                var st = go.AddComponent<VfxStation>();
                st.effect = AssetDatabase.LoadAssetAtPath<GameObject>(FeelVfxBuilder.EffectPath(list[i].name));
                st.spawnPoint = spawn;
                st.shake = list[i].shake;
                result.Add(new Station { station = st, label = list[i].label });
            }
            return result;
        }

        // ---------------------------------------------------------------- sound board (left)

        static void BuildSoundBoardPanel(Transform canvas)
        {
            var panel = UiBuilder.Image("SoundBoard", canvas, PanelColor, blocksClicks: true);
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(0f, 0f); rt.offsetMax = new Vector2(470f, 0f);
            var board = panel.gameObject.AddComponent<SoundBoard>();
            board.oneShotSource = panel.gameObject.AddComponent<AudioSource>();
            board.oneShotSource.playOnAwake = false;

            var title = UiBuilder.Text("Title", rt, "SOUND BOARD", 28, Color.white);
            UiBuilder.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(440f, 40f));

            board.nowPlaying = UiBuilder.Text("Now Playing", rt, "Click a sound. Loops start / stop.", 16, HeaderColor);
            UiBuilder.Place(board.nowPlaying.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -54f), new Vector2(440f, 26f));

            var volLabel = UiBuilder.Text("Volume Label", rt, "Volume", 16, Color.white);
            UiBuilder.Place(volLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -88f), new Vector2(80f, 26f));
            var slider = MakeSlider(rt, 1f);
            UiBuilder.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -92f), new Vector2(200f, 20f));
            board.volumeSlider = slider;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, new UnityAction<float>(board.SetVolume));

            var stop = MakeButton(rt, "Stop all", 16, new UnityAction(board.StopAll));
            UiBuilder.Place((RectTransform)stop.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(316f, -84f), new Vector2(138f, 34f));

            // Scroll list for the buttons.
            var scrollGo = DefaultControls.CreateScrollView(UiResources());
            scrollGo.name = "Sound List";
            scrollGo.transform.SetParent(rt, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(8f, 8f); srt.offsetMax = new Vector2(-8f, -130f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            if (scroll.horizontalScrollbar != null) Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            var content = scroll.content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            board.content = content;
        }

        /// <summary>
        /// Clears the board and makes one button per AudioClip in Assets/Audio. With
        /// <paramref name="candidatesOnly"/>, only the options still waiting for a pick
        /// (Assets/Audio/_Candidates) are shown. Returns the count.
        /// </summary>
        public static int FillSoundBoard(SoundBoard board, bool candidatesOnly = false)
        {
            if (board.content == null) return 0;
            for (int i = board.content.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(board.content.GetChild(i).gameObject);

            var groups = new SortedDictionary<string, List<AudioClip>>();
            if (AssetDatabase.IsValidFolder(AudioRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("/_Inbox/")) continue;
                    bool isCandidate = path.Contains("/_Candidates/");
                    if (candidatesOnly && !isCandidate) continue;
                    string folder = Path.GetDirectoryName(path).Replace('\\', '/').Substring(AudioRoot.Length).TrimStart('/');
                    if (folder.StartsWith("_Candidates/")) folder = folder.Substring("_Candidates/".Length);
                    if (folder.StartsWith("SFX/")) folder = folder.Substring(4);
                    if (isCandidate && !candidatesOnly) folder += " (options)";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    if (!groups.TryGetValue(folder, out var l)) groups[folder] = l = new List<AudioClip>();
                    l.Add(clip);
                }
            }

            var buttons = new List<SoundBoardButton>();
            if (groups.Count == 0)
            {
                var empty = UiBuilder.Text("Empty", board.content, "No sounds yet. Put WAVs in Assets/Audio, then run Salok > Feel > Refresh Sound Board.", 16, HeaderColor);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            }
            foreach (var g in groups)
            {
                var header = UiBuilder.Text("Header - " + g.Key, board.content, g.Key.ToUpperInvariant(), 18, HeaderColor);
                header.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

                var grid = new GameObject("Group - " + g.Key, typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
                grid.transform.SetParent(board.content, false);
                grid.cellSize = new Vector2(198f, 34f);
                grid.spacing = new Vector2(6f, 6f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;

                g.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (var clip in g.Value)
                {
                    bool loop = g.Key.StartsWith("Ambience") || clip.length > 8f;
                    var b = MakeButton(grid.transform, ShortName(clip.name) + (loop ? "  (loop)" : ""), 14, null);
                    b.name = clip.name;
                    var sbb = b.gameObject.AddComponent<SoundBoardButton>();
                    sbb.clip = clip;
                    sbb.loop = loop;
                    sbb.background = b.GetComponent<Image>();
                    sbb.label = b.GetComponentInChildren<TMP_Text>();
                    UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(sbb.Play));
                    buttons.Add(sbb);
                }
            }
            board.buttons = buttons.ToArray();
            EditorUtility.SetDirty(board);
            return buttons.Count;
        }

        // "sfx_player_footstep_sand_01" -> "footstep_sand_01"
        static string ShortName(string clip)
        {
            string n = clip.StartsWith("sfx_") ? clip.Substring(4) : clip;
            int cut = n.IndexOf('_');
            return cut > 0 && cut < n.Length - 1 ? n.Substring(cut + 1) : n;
        }

        // ---------------------------------------------------------------- VFX bar (bottom middle)

        static void BuildVfxBar(Transform canvas, List<Station> stations)
        {
            var bar = UiBuilder.Image("VFX Buttons", canvas, PanelColor, blocksClicks: true);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(480f, 8f); rt.offsetMax = new Vector2(-480f, 140f);

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.transform.SetParent(rt, false);
            UiBuilder.Fill((RectTransform)grid.transform, 10f, 10f, 10f, 10f);
            grid.cellSize = new Vector2(176f, 34f);
            grid.spacing = new Vector2(6f, 6f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            foreach (var s in stations)
                MakeButton(grid.transform, s.label, 14, new UnityAction(s.station.Play));
        }

        // ---------------------------------------------------------------- feel panel (right)

        static void BuildFeelPanel(Transform canvas, Transform floatAnchor, LowHydVignette vignette, CollapseFade fade)
        {
            var panel = UiBuilder.Image("Feel Panel", canvas, PanelColor, blocksClicks: true);
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-470f, 0f); rt.offsetMax = new Vector2(0f, 0f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var demo = panel.gameObject.AddComponent<FeelLabDemo>();
            demo.floatAnchor = floatAnchor;
            demo.vignette = vignette;
            demo.collapseFade = fade;

            Header(rt, "FEEL", 28, Color.white);

            Header(rt, "Floating numbers", 16, HeaderColor);
            Row(rt, ("+1.5 L", new UnityAction(demo.FloatWater)), ("-2 HYD", new UnityAction(demo.FloatHyd)), ("FULL", new UnityAction(demo.FloatFull)));

            Header(rt, "UI punch + number roll-up", 16, HeaderColor);
            var bar = UiBuilder.Image("Water Bar", rt, new Color(0.25f, 0.25f, 0.25f));
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            var fill = UiBuilder.Image("Fill", bar.transform, WaterBlue);
            fill.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            demo.punchTarget = bar.gameObject.AddComponent<UiPunch>();

            var counter = UiBuilder.Text("Counter", rt, "12.0 L", 30, Color.white, TextAlignmentOptions.Center);
            counter.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            counter.gameObject.AddComponent<UiPunch>();
            demo.counter = counter.gameObject.AddComponent<NumberRoll>();
            Row(rt, ("Punch bar", new UnityAction(demo.Punch)), ("Counter +2.5", new UnityAction(demo.AddToCounter)), ("Counter -1", new UnityAction(demo.TakeFromCounter)));

            Header(rt, "Screen shake", 16, HeaderColor);
            Row(rt, ("Small", new UnityAction(demo.ShakeSmall)), ("Medium", new UnityAction(demo.ShakeMedium)), ("Big", new UnityAction(demo.ShakeBig)));
            var toggle = MakeButton(rt, "Shake: ON", 16, new UnityAction(demo.ToggleShake));
            toggle.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            demo.shakeLabel = toggle.GetComponentInChildren<TMP_Text>();

            Header(rt, "Low-HYD vignette (HYD slider)", 16, HeaderColor);
            var hyd = MakeSlider(rt, 1f);
            hyd.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            demo.hydSlider = hyd;
            UnityEventTools.AddPersistentListener(hyd.onValueChanged, new UnityAction<float>(demo.SetHyd));

            Header(rt, "Collapse fade", 16, HeaderColor);
            var collapse = MakeButton(rt, "Collapse", 16, new UnityAction(demo.Collapse));
            collapse.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            Header(rt, "Minigame hit (flash, hit-stop, burst)", 16, HeaderColor);
            var dialHolder = new GameObject("Dial Holder", typeof(RectTransform)).GetComponent<RectTransform>();
            dialHolder.SetParent(rt, false);
            dialHolder.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;
            var dial = UiBuilder.Image("Dial", dialHolder, new Color(0.5f, 0.5f, 0.5f));
            dial.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FeelVfxBuilder.RingPath);
            UiBuilder.Place(dial.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            var needle = UiBuilder.Image("Needle", dial.transform, Color.white);
            UiBuilder.Place(needle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(6f, 52f));
            needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -30f);

            var hit = panel.gameObject.AddComponent<HitFeedback>();
            hit.flashTarget = needle;
            hit.burstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BurstPrefabPath);
            hit.burstParent = dial.rectTransform;
            hit.punchOnHit = dial.gameObject.AddComponent<UiPunch>();
            demo.hitFeedback = hit;
            Row(rt, ("Green hit", new UnityAction(demo.Hit)), ("Miss", new UnityAction(demo.Miss)));
        }

        // ---------------------------------------------------------------- UI helpers

        static void Header(Transform parent, string text, float size, Color color)
        {
            var t = UiBuilder.Text("Header - " + text, parent, text, size, color);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = size + 10f;
        }

        static void Row(Transform parent, params (string label, UnityAction action)[] buttons)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<HorizontalLayoutGroup>();
            row.transform.SetParent(parent, false);
            row.spacing = 6f;
            row.childControlHeight = true;
            row.childControlWidth = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            foreach (var b in buttons) MakeButton(row.transform, b.label, 15, b.action);
        }

        /// <summary>A grey button whose click is saved in the scene (visible in the Button's On Click list).</summary>
        static Button MakeButton(Transform parent, string label, float fontSize, UnityAction persistent)
        {
            var b = UiBuilder.Button(label, parent, label, fontSize, ButtonColor, Color.white, null);
            var colors = b.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            b.colors = colors;
            var text = b.GetComponentInChildren<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(6f, 0f, 6f, 0f);
            if (persistent != null) UnityEventTools.AddPersistentListener(b.onClick, persistent);
            return b;
        }

        static Slider MakeSlider(Transform parent, float value)
        {
            var go = DefaultControls.CreateSlider(UiResources());
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            var fill = s.fillRect != null ? s.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = WaterBlue;
            return s;
        }

        static DefaultControls.Resources UiResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        };
    }
}
