// ============================================================================
// PlaytestSceneBuilder.cs   (Editor only)
// Adds the menu "Tools > The Last Drop > Build Playtest Scene". It creates, or refreshes,
// Assets/Scenes/Playtest_Maze.unity:
//   Maze      - the 2-layer maze (MazeBuilder, zone 2 under Zone2Pivot) and Zone2Rotator
//   Player    - the team's player, camera, HUD and pause menu, at the spawn tile (47, 47)
//   Systems   - BalanceProvider (GameBalance), the Game Manager prefab, NightLockdown,
//               MapRevealTracker, MapScreen, ValveTimingGame, GateManager, EscapePanel,
//               UpgradeManager, GameMenu, SaveManager, DebugHud and DebugKeys
//   HUDCanvas      - the team's Player HUD and Player Pause prefabs (they have no Canvas of their own)
//   MinigameCanvas - the artists' ValveTimingPanel prefab
//   plus a light and an EventSystem.
// It also: imports the Build and Skills CSV the first time, ticks "Infinite Canteen",
// ticks GameBalance "Allow Buying Anywhere", and adds the scene to the build's scene list.
//
// DESIGN RULES: R8.24 to R8.28 (zone 2 under its own pivot so it can turn). Water does
// nothing in this scene (the player's "Hydration Off" box is ticked).
//
// HOW TO USE: run the menu. If the scene already exists it is opened and refreshed:
// the maze is rebuilt, the player goes back to the spawn, and missing pieces are added.
// Anything else you put in the scene is left alone.
// HOW TO EDIT: asset paths are the constants at the top. Numbers (tile size, wall height,
// ring limits) live in GameBalance, never here.
// ============================================================================

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastDrop
{
    /// <summary>Builds or refreshes the Playtest_Maze scene from the menu.</summary>
    public static class PlaytestSceneBuilder
    {
        /// <summary>Where the playtest scene is saved.</summary>
        public const string ScenePath = "Assets/Scenes/Playtest_Maze.unity";
        /// <summary>The full-game balance asset the scene uses.</summary>
        public const string BalancePath = "Assets/Scripts/Data/GameBalance.asset";
        /// <summary>The maze CSV the game builds from (zone 2 at 0 degrees).</summary>
        public const string MazeCsvPath = "Assets/_Project/Data/Maze/Maze2Layer_0deg.csv";

        const string GameManagerPrefab = "Assets/Prefabs/Game Prefab/Manager/Game Manager.prefab";
        const string PlayerPrefab = "Assets/Prefabs/Game Prefab/PlaceHolder/Models/Player.prefab";
        const string CameraPrefab = "Assets/Prefabs/Game Prefab/PlaceHolder/Models/Player Camera (Left & Right).prefab";
        const string HudPrefab = "Assets/Prefabs/Game Prefab/UI/Player HUD.prefab";
        const string PausePrefab = "Assets/Prefabs/Game Prefab/UI/Player Pause.prefab";
        const string ValvePanelPrefab = "Assets/Prefabs/UI/ValveTimingPanel.prefab";
        const string LockBasinPrefab = "Assets/Prefabs/Game Prefab/PlaceHolder/Models/Lock Basin.prefab";

        /// <summary>Name of the object that holds the playtest managers.</summary>
        public const string SystemsName = "Systems";
        /// <summary>Name of the object with the MazeBuilder.</summary>
        public const string MazeName = "Maze";

        /// <summary>Menu entry: creates or refreshes the playtest scene and saves it.</summary>
        [MenuItem("Tools/The Last Drop/Build Playtest Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(BalancePath) || !File.Exists(MazeCsvPath))
            {
                EditorUtility.DisplayDialog("Build Playtest Scene",
                    $"Missing asset:\n{(File.Exists(BalancePath) ? MazeCsvPath : BalancePath)}", "OK");
                return;
            }

            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets AFTER opening the scene: opening a scene unloads assets nothing
            // uses, and an unloaded GameBalance would count as "missing" when the maze is built.
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(BalancePath);
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(MazeCsvPath);

            EnsureLight(scene);
            var systems = EnsureSystems(scene, balance);
            var gameManager = EnsurePrefab(scene, GameManagerPrefab, systems.transform);
            var builder = EnsureMaze(scene, balance, csv);
            var player = EnsurePrefab(scene, PlayerPrefab, null);
            var cam = EnsurePrefab(scene, CameraPrefab, null);
            // The team's HUD and pause menu are plain UI panels without their own Canvas, so they
            // only show (and the Resume button only clicks) when they sit inside one.
            var hudCanvas = EnsureCanvas(scene, "HUDCanvas", 10);
            var hud = EnsurePrefab(scene, HudPrefab, hudCanvas);
            var pause = EnsurePrefab(scene, PausePrefab, hudCanvas);
            if (hud != null && hud.transform.parent != hudCanvas) hud.transform.SetParent(hudCanvas, false);
            if (pause != null && pause.transform.parent != hudCanvas) pause.transform.SetParent(hudCanvas, false);
            if (pause != null) pause.transform.SetAsLastSibling(); // pause menu drawn above the HUD
            EnsureEventSystem(scene);

            if (gameManager == null || player == null || cam == null)
            {
                EditorUtility.DisplayDialog("Build Playtest Scene", "A team prefab is missing; see the Console.", "OK");
                return;
            }

            PlacePlayer(builder, player, cam);
            WireScene(builder, gameManager, player, hud, pause);
            AddPhase2(systems, builder);
            AddPhase3(systems, builder);
            AddPhase4(scene, systems, builder, gameManager, balance);
            AddPhase5(systems, builder, balance);
            GetOrAdd<SaveManager>(systems); // Phase 6: saving
            GetOrAdd<DebugHud>(systems);    // Phase 7: F1 debug HUD

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildScenes(ScenePath);
            Debug.Log($"[PlaytestSceneBuilder] Saved {ScenePath}. Press Play to walk the maze.");
        }

        // ------------------------------------------------------------------ Pieces

        static void EnsureLight(Scene scene)
        {
            if (FindRoot(scene, "Directional Light") != null) return;
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            // A low afternoon sun from the south-west, so maze walls cast readable shadows.
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static GameObject EnsureSystems(Scene scene, GameBalance balance)
        {
            var go = FindRoot(scene, SystemsName) ?? new GameObject(SystemsName);
            var provider = GetOrAdd<BalanceProvider>(go);
            provider.balance = balance;
            return go;
        }

        static MazeBuilder EnsureMaze(Scene scene, GameBalance balance, TextAsset csv)
        {
            var go = FindRoot(scene, MazeName) ?? new GameObject(MazeName);
            var builder = GetOrAdd<MazeBuilder>(go);
            builder.layoutCsv = csv;
            builder.balance = balance;
            builder.splitForZone2Rotation = true;
            builder.centerOnTransform = true;

            // Put the builder on the centre tile's world position. With "Center On Transform",
            // tile (x, y) then lands at (x × tile size, 0, (last row − y) × tile size): on the
            // 95 map that is (188, 0, 188), and tile (0, 94) sits on the world origin.
            var grid = MazeGrid.FromCsv(csv.text, csv.name);
            float ts = balance.cellSize;
            go.transform.SetPositionAndRotation(
                new Vector3(grid.CentreX * ts, 0f, (grid.Height - 1 - grid.CentreY) * ts),
                Quaternion.identity);
            go.transform.localScale = Vector3.one;

            builder.Build();
            return builder;
        }

        static void EnsureEventSystem(Scene scene)
        {
            if (FindRoot(scene, "EventSystem") != null) return;
            // Needed so the pause menu's Resume button (and later the menus) can be clicked.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static void PlacePlayer(MazeBuilder builder, GameObject player, GameObject cam)
        {
            Vector3 spawn = builder.PlayerSpawn != null ? builder.PlayerSpawn.position : builder.transform.position;
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            cam.transform.SetPositionAndRotation(spawn, Quaternion.identity);

            // Water does nothing in this scene.
            var hydration = player.GetComponent<PlayerHydration_Manager>();
            if (hydration != null)
            {
                hydration.hydrationOff = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(hydration);
            }
        }

        // Makes the same scene links TestingScene has, so the team's prefabs work together.
        static void WireScene(MazeBuilder builder, GameObject gameManager, GameObject player, GameObject hud, GameObject pause)
        {
            var gm = gameManager.GetComponent<Game_Manager>();

            // Pause menu: Game_Manager shows it, and its Resume button calls Game_Manager.ResumeGame.
            if (gm != null && pause != null)
            {
                gm.pauseMenu = pause;
                PrefabUtility.RecordPrefabInstancePropertyModifications(gm);
                foreach (var button in pause.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentMethodName(i) != nameof(Game_Manager.ResumeGame)) continue;
                        UnityEventTools.RegisterPersistentListener(button.onClick, i, gm.ResumeGame);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                    }
                }
            }

            if (hud != null)
            {
                // "Interaction Text" shows "Hold [E] ..." prompts; "Game Clock" shows the day.
                var interaction = player.GetComponent<PlayerInteraction_Manager>();
                var interactionText = FindText(hud, "Interaction Text");
                if (interaction != null && interactionText != null)
                {
                    interaction.interactionText = interactionText;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
                }

                var clock = gameManager.GetComponent<GameClock_Manager>();
                var clockText = FindText(hud, "Game Clock");
                if (clock != null && clockText != null)
                {
                    var so = new SerializedObject(clock);
                    so.FindProperty("clockText").objectReferenceValue = clockText; // private field, so set it this way
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // The hub's trigger box (made by the MazeBuilder over the S tiles) tells the
            // hydration script when the player is "inside the hub".
            var hydration = player.GetComponent<PlayerHydration_Manager>();
            if (hydration != null)
            {
                foreach (var area in builder.Areas)
                {
                    if (area == null || area.kind != MazeArea.Kind.Base) continue;
                    hydration.hubArea = area.GetComponent<BoxCollider>();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hydration);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Phase 2: clock and zone 2

        // Zone2Rotator goes on the Maze object; the night lockdown and debug keys on Systems.
        static void AddPhase2(GameObject systems, MazeBuilder builder)
        {
            var rotator = GetOrAdd<Zone2Rotator>(builder.gameObject);
            rotator.pivot = builder.Zone2Pivot;

            var lockdown = GetOrAdd<NightLockdown>(systems);
            lockdown.maze = builder;

            var keys = GetOrAdd<DebugKeys>(systems);
            keys.zone2Rotator = rotator;
        }

        // ------------------------------------------------------------------ Phase 3: map

        // The map tracker and the map screen both live on Systems. The screen builds its own UI at Play.
        static void AddPhase3(GameObject systems, MazeBuilder builder)
        {
            var tracker = GetOrAdd<MapRevealTracker>(systems);
            tracker.maze = builder;

            var screen = GetOrAdd<MapScreen>(systems);
            screen.tracker = tracker;
            screen.maze = builder;
            screen.rotator = builder.GetComponent<Zone2Rotator>();

            var keys = GetOrAdd<DebugKeys>(systems);
            keys.mapTracker = tracker;
        }

        // ------------------------------------------------------------------ Phase 4: gates and minigame

        static void AddPhase4(Scene scene, GameObject systems, MazeBuilder builder, GameObject gameManager, GameBalance balance)
        {
            // Infinite canteen: pouring into basins never runs out (water does nothing in this scene).
            var resources = gameManager.GetComponent<GameResources_Manager>();
            if (resources != null)
            {
                resources.infiniteCanteen = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(resources);
            }

            // The Valve Timing panel (the artists' prefab) on its own canvas, run by a ValveTimingGame
            // on Systems (which is always active; the panel is switched off between games).
            var minigameCanvas = EnsureCanvas(scene, "MinigameCanvas", 70);
            var panel = EnsurePrefab(scene, ValvePanelPrefab, minigameCanvas);
            var game = GetOrAdd<ValveTimingGame>(systems);
            game.balance = balance;
            if (panel != null)
            {
                game.panel = panel;
                game.needle = Find<RectTransform>(panel, "Needle");
                game.band = Find<Image>(panel, "Band");
                game.valveWheel = Find<RectTransform>(panel, "ValveWheel");
                game.missFlash = Find<Image>(panel, "MissFlash");
                game.counterText = Find<TMP_Text>(panel, "Counter");
                game.hitPips = ChildImages(panel, "HitPips");
                game.missPips = ChildImages(panel, "MissPips");
                panel.SetActive(false);
            }

            var escape = GetOrAdd<EscapePanel>(systems);
            var gates = GetOrAdd<GateManager>(systems);
            gates.maze = builder;
            gates.lockBasinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LockBasinPrefab);
            gates.mapScreen = systems.GetComponent<MapScreen>();
            gates.escapePanel = escape;
        }

        // ------------------------------------------------------------------ Phase 5: Build and Skills

        static void AddPhase5(GameObject systems, MazeBuilder builder, GameBalance balance)
        {
            // Import the CSVs the first time (later, run the import menu after editing them).
            var db = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(UpgradeCsvImporter.DatabasePath);
            if (db == null)
            {
                var problems = new System.Collections.Generic.List<string>();
                db = UpgradeCsvImporter.Import(problems);
                foreach (var p in problems) Debug.LogWarning("[PlaytestSceneBuilder] CSV: " + p);
            }

            var upgrades = GetOrAdd<UpgradeManager>(systems);
            upgrades.database = db;
            upgrades.maze = builder;
            GetOrAdd<MapRevealTracker>(systems).upgrades = upgrades;

            var menu = GetOrAdd<GameMenu>(systems);
            menu.upgrades = upgrades;
            menu.mapScreen = systems.GetComponent<MapScreen>();

            // The playtest lets you buy anywhere (the rule is: only at the hub, R7.2).
            if (!balance.allowBuyingAnywhere)
            {
                balance.allowBuyingAnywhere = true;
                EditorUtility.SetDirty(balance);
                AssetDatabase.SaveAssets();
                Debug.Log("[PlaytestSceneBuilder] GameBalance: 'Allow Buying Anywhere' turned on for the playtest.");
            }
        }

        static T Find<T>(GameObject root, string objName) where T : Component
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objName && t.TryGetComponent(out T c)) return c;
            Debug.LogWarning($"[PlaytestSceneBuilder] '{objName}' not found in {root.name}.");
            return null;
        }

        // The Image on each direct child of the named object (the pips), in order.
        static Image[] ChildImages(GameObject root, string holderName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != holderName) continue;
                var list = new System.Collections.Generic.List<Image>();
                foreach (Transform child in t)
                    if (child.TryGetComponent(out Image img)) list.Add(img);
                return list.ToArray();
            }
            return new Image[0];
        }

        // ------------------------------------------------------------------ Helpers

        // A screen-overlay canvas at the scene root, scaled from 1920 × 1080 like the artists'
        // design. Higher sort orders are drawn on top (HUD 10, menus 60-65, minigame 70).
        static Transform EnsureCanvas(Scene scene, string canvasName, int sortOrder)
        {
            var go = FindRoot(scene, canvasName);
            if (go != null) return go.transform;
            go = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuilder.ReferenceWidth, UiBuilder.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        // SaveManager.LoadAndStart opens the scene by name, which only works for scenes in the
        // build's scene list, so the playtest scene is added to it (once, at the end).
        static void AddToBuildScenes(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[PlaytestSceneBuilder] Added {path} to the build's scene list (needed by SaveManager.LoadAndStart).");
        }

        // Unity's GetComponent can return a "fake null" in the editor, which the ?? operator
        // doesn't see as null, so this uses TryGetComponent instead.
        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

        static GameObject FindRoot(Scene scene, string objName)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == objName) return go;
            return null;
        }

        /// <summary>Finds the prefab's instance in the scene (anywhere), or adds one.</summary>
        static GameObject EnsurePrefab(Scene scene, string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[PlaytestSceneBuilder] Prefab not found: {path}");
                return null;
            }

            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) &&
                        PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == prefab)
                        return t.gameObject;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static TMP_Text FindText(GameObject root, string objName)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.gameObject.name == objName) return t;
            return null;
        }
    }
}
