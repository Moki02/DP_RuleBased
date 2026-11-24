// ============================================================================
// GameFeelInstaller.cs   (Editor only)
// Adds "Salok > Feel > Install Game Feel": puts the team's picked sounds and the approved
// effects into the game.
//   1. Assets/VFX/GameFeelLibrary.asset: which sound / effect plays at each moment (the
//      final picks from Assets/Audio/CREDITS.md and Docs/Agents/GameFeel_Hookups.md).
//      Empty slots are filled; slots someone already changed are kept.
//   2. Salok_Environment: a "GameFeel" object (GameFeelDirector, AmbienceDirector,
//      UiButtonSounds), PlayerFeel on the player, the low-HYD vignette, CameraShake on the
//      player camera.
//   3. MainMenu / Win / GameOver: a "GameFeel" object with the menu ambience or the
//      Win / Game Over sting, and button sounds.
// SAFE TO RUN AGAIN: it finds what's already there and only adds what's missing.
// Batch mode (Unity closed):
//   Unity.exe -batchmode -quit -projectPath . -executeMethod TheLastDrop.Feel.EditorTools.GameFeelInstaller.InstallBatch
//
// DESIGN RULES: none (tooling). No gameplay values change.
//
// HOW TO USE: Salok > Feel > Install Game Feel (save your open scene first; it opens and
// saves the four scenes, then reopens the one you had).
// HOW TO EDIT: the default picks and volumes are in FillLibrary below; after installing,
// edit the GameFeelLibrary asset instead.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop.Feel.EditorTools
{
    /// <summary>Installs the game-feel sounds and effects into the game scenes.</summary>
    public static class GameFeelInstaller
    {
        public const string LibraryPath = FeelVfxBuilder.VfxFolder + "/GameFeelLibrary.asset";
        const string GameScene = "Assets/Scenes/Salok_Environment.unity";
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string WinScene = "Assets/Scenes/Win.unity";
        const string GameOverScene = "Assets/Scenes/GameOver.unity";
        const string RootName = "GameFeel";
        const string Audio = "Assets/Audio/";

        [MenuItem("Salok/Feel/Install Game Feel")]
        public static void InstallFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = SceneManager.GetActiveScene().path;
            var log = Install();
            if (!string.IsNullOrEmpty(back) && System.IO.File.Exists(back)) EditorSceneManager.OpenScene(back);
            Debug.Log("[GameFeel] Install finished:\n" + string.Join("\n", log));
        }

        /// <summary>For -executeMethod (Unity closed). Exits with code 1 if something important is missing.</summary>
        public static void InstallBatch()
        {
            var log = Install();
            Debug.Log("[GameFeel] Install finished:\n" + string.Join("\n", log));
            bool bad = log.Exists(l => l.StartsWith("MISSING") || l.StartsWith("ERROR"));
            if (Application.isBatchMode) EditorApplication.Exit(bad ? 1 : 0);
        }

        /// <summary>Does the whole install. Returns one line per step.</summary>
        public static List<string> Install()
        {
            var log = new List<string>();
            if (!AssetDatabase.IsValidFolder(FeelVfxBuilder.EffectsFolder))
            {
                log.AddRange(FeelVfxBuilder.BuildAll());
            }
            var lib = LoadOrCreateLibrary(log);
            FillLibrary(lib, log);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            InstallGameScene(lib, log);
            InstallSceneFeel(MenuScene, lib, SceneFeel.Which.MainMenuAmbience, log);
            InstallSceneFeel(WinScene, lib, SceneFeel.Which.WinSting, log);
            InstallSceneFeel(GameOverScene, lib, SceneFeel.Which.GameOverSting, log);
            return log;
        }

        // ================================================================ library

        static GameFeelLibrary LoadOrCreateLibrary(List<string> log)
        {
            var lib = AssetDatabase.LoadAssetAtPath<GameFeelLibrary>(LibraryPath);
            if (lib != null) { log.Add("kept   " + LibraryPath); return lib; }
            lib = ScriptableObject.CreateInstance<GameFeelLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
            log.Add("made   " + LibraryPath);
            return lib;
        }

        // The team's final picks (2026-11-24). Only empty slots are filled.
        static void FillLibrary(GameFeelLibrary l, List<string> log)
        {
            S(l.footstepSandLoop, "SFX/Player/sfx_player_footstep_sand_02", 0.45f, log);
            S(l.footstepStone, "SFX/Player/sfx_player_footstep_stone_07", 0.5f, log);
            S(l.sprintBreath, "SFX/Player/sfx_player_sprintbreath_03", 0.55f, log);
            S(l.drink, "SFX/Player/sfx_player_drink_01", 0.7f, log);
            S(l.canteenFill, "SFX/Player/sfx_player_canteenfill_06", 0.7f, log);
            S(l.collapse, "SFX/Player/sfx_player_collapse_06", 0.9f, log);
            S(l.lowHydHeartbeat, "SFX/Player/sfx_player_heartbeat_03", 0.7f, log);
            S(l.lowHydBreath, "SFX/Player/sfx_player_lowhyd_breath_02", 0.5f, log);
            S(l.cough, "SFX/Player/sfx_player_cough_03", 0.7f, log);

            S(l.pumpLoop, "SFX/Hub/sfx_hub_pumploop_01", 0.8f, log);
            S(l.gush, "SFX/Hub/sfx_hub_gush_05", 0.6f, log);
            S(l.boilLoop, "SFX/Hub/sfx_hub_boil_04", 0.6f, log);
            S(l.pourLoop, "SFX/Hub/sfx_hub_pour_02", 0.6f, log);
            S(l.tapOpen, "SFX/Hub/sfx_hub_tap_06", 0.7f, log);
            S(l.waterBanked, "SFX/UI/sfx_ui_waterbanked_02", 0.8f, log);

            S(l.valveTurn, "SFX/Gates/sfx_gates_valve_03", 0.8f, log);
            S(l.needleTick, "SFX/Gates/sfx_gates_tick_02", 0.45f, log);
            S(l.minigameHit, "SFX/Gates/sfx_gates_hit_01", 0.8f, log);
            S(l.minigameMiss, "SFX/UI/sfx_ui_notenough_05", 0.8f, log);
            S(l.minigameWin, "SFX/Gates/sfx_gates_success_01", 0.85f, log);
            S(l.minigameLose, "SFX/Gates/sfx_gates_fail_03", 0.85f, log);
            S(l.gateOpen, "SFX/Gates/sfx_gates_doorrise_01", 0.9f, log);      // swapped on purpose (team pick)
            S(l.gateSlam, "SFX/Gates/sfx_gates_gateslam_04", 0.9f, log);
            S(l.doorOpen, "SFX/Gates/sfx_gates_gateslide_01", 0.85f, log);    // swapped on purpose (team pick)

            S(l.zone2Turn, "SFX/Maze/sfx_maze_zone2turnmix_01", 0.9f, log);
            S(l.cistern, "SFX/Maze/sfx_maze_cistern_05", 0.7f, log);
            S(l.note, "SFX/Maze/sfx_maze_note_01", 0.7f, log);
            S(l.mapOpen, "SFX/Maze/sfx_maze_mapopen_02", 0.7f, log);
            S(l.marker, "SFX/Maze/sfx_maze_marker_01", 0.7f, log);
            S(l.reward, "SFX/Maze/sfx_maze_reveal_04", 0.8f, log);

            S(l.duskWarning, "SFX/World/sfx_world_duskwarning_08", 0.8f, log);
            S(l.lockdown, "SFX/World/sfx_world_lockdown_06", 0.85f, log);
            S(l.thunder, "SFX/World/sfx_world_thunder_03", 0.9f, log);
            S(l.droughtSting, "Stingers/sfx_stinger_droughtstage_04", 0.85f, log);

            S(l.uiClick, "SFX/UI/sfx_ui_click_03", 0.6f, log);
            S(l.uiHover, "SFX/UI/sfx_ui_hover_03", 0.35f, log);
            S(l.menuOpen, "SFX/UI/sfx_ui_menuopen_02", 0.6f, log);
            S(l.buy, "SFX/UI/sfx_ui_buy_05", 0.8f, log);
            S(l.unlock, "SFX/UI/sfx_ui_unlock_05", 0.8f, log);
            S(l.notEnough, "SFX/UI/sfx_ui_notenough_05", 0.7f, log);
            S(l.winSting, "Stingers/sfx_stinger_win_12", 0.9f, log);
            S(l.gameOverSting, "Stingers/sfx_stinger_gameover_10", 0.9f, log);

            S(l.ambienceHubDay, "Ambience/sfx_ambience_hubday_01", 0.5f, log);
            S(l.ambienceMazeDay, "Ambience/sfx_ambience_mazeday_02", 0.5f, log);
            S(l.ambienceNight, "Ambience/sfx_ambience_night_01", 0.5f, log);
            S(l.ambienceTension, "Ambience/sfx_ambience_tension_01", 0.4f, log);
            S(l.ambienceHeatwave, "Ambience/sfx_ambience_heatwave_01", 0.45f, log);
            S(l.ambienceStorm, "Ambience/sfx_ambience_storm_03", 0.6f, log);
            S(l.ambienceRain, "Ambience/sfx_ambience_rain_01", 0.5f, log);
            S(l.ambienceDawn, "SFX/World/sfx_world_dawn_06", 0.5f, log);
            S(l.ambienceMenu, "Ambience/sfx_ambience_menu_02", 0.6f, log);

            l.vfxFootstepDust = V(l.vfxFootstepDust, "VFX_FootstepDust", log);
            l.vfxWaterSplash = V(l.vfxWaterSplash, "VFX_WaterSplash", log);
            l.vfxSteam = V(l.vfxSteam, "VFX_Steam", log);
            l.vfxPourStream = V(l.vfxPourStream, "VFX_PourStream", log);
            l.vfxWaterSparkle = V(l.vfxWaterSparkle, "VFX_WaterSparkle", log);
            l.vfxDustPuff = V(l.vfxDustPuff, "VFX_DustPuff", log);
            l.vfxZone2Dust = V(l.vfxZone2Dust, "VFX_Zone2Dust", log);
            l.vfxRewardPop = V(l.vfxRewardPop, "VFX_RewardPop", log);
            l.vfxSickFlies = V(l.vfxSickFlies, "VFX_SickFlies", log);
            l.vfxRain = V(l.vfxRain, "VFX_Rain", log);
            l.vfxDustBlowing = V(l.vfxDustBlowing, "VFX_DustBlowing", log);
            l.vfxSandStreaks = V(l.vfxSandStreaks, "VFX_SandStreaks", log);
        }

        static void S(FeelSound slot, string file, float volume, List<string> log)
        {
            if (slot.clip != null) return;   // someone already chose: keep it
            string path = Audio + file + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { log.Add("MISSING sound " + path); return; }
            slot.clip = clip;
            slot.volume = volume;
        }

        static GameObject V(GameObject current, string name, List<string> log)
        {
            if (current != null) return current;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FeelVfxBuilder.EffectPath(name));
            if (go == null) log.Add("MISSING effect " + FeelVfxBuilder.EffectPath(name));
            return go;
        }

        // ================================================================ scenes

        static GameObject Root(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) return go;
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void InstallGameScene(GameFeelLibrary lib, List<string> log)
        {
            if (!System.IO.File.Exists(GameScene)) { log.Add("MISSING scene " + GameScene); return; }
            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            var root = Root(scene);
            Ensure<GameFeelDirector>(root).library = lib;
            Ensure<AmbienceDirector>(root).library = lib;
            Ensure<UiButtonSounds>(root).library = lib;
            log.Add("game   GameFeel object: director, ambience, button sounds");

            // Low-HYD vignette (an editable prefab instance)
            var vignette = Object.FindFirstObjectByType<LowHydVignette>(FindObjectsInactive.Include);
            if (vignette == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeelLabBuilder.VignettePrefabPath);
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    vignette = inst.GetComponent<LowHydVignette>();
                    log.Add("game   added LowHydVignette prefab");
                }
                else log.Add("MISSING prefab " + FeelLabBuilder.VignettePrefabPath);
            }

            // Player feel
            var player = Object.FindFirstObjectByType<Player_Manager>(FindObjectsInactive.Include);
            if (player != null)
            {
                var pf = Ensure<PlayerFeel>(player.gameObject);
                pf.library = lib;
                if (vignette != null) pf.vignette = vignette;
                log.Add("game   PlayerFeel on " + player.name);
            }
            else log.Add("note   no Player_Manager in the scene: GameFeelDirector adds PlayerFeel at runtime");

            // Camera shake on the camera that renders (Player_Camera's camera object)
            var pc = Object.FindFirstObjectByType<Player_Camera>(FindObjectsInactive.Include);
            Camera cam = pc != null && pc.cameraObject != null ? pc.cameraObject : null;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (cam != null) { Ensure<CameraShake>(cam.gameObject); log.Add("game   CameraShake on " + cam.name); }
            else log.Add("note   no camera found: GameFeelDirector adds CameraShake at runtime");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) log.Add("ERROR could not save " + GameScene);
            else log.Add("saved  " + GameScene);
        }

        static void InstallSceneFeel(string path, GameFeelLibrary lib, SceneFeel.Which which, List<string> log)
        {
            if (!System.IO.File.Exists(path)) { log.Add("note   no scene " + path + " (skipped)"); return; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var root = Root(scene);
            var sf = Ensure<SceneFeel>(root);
            sf.library = lib;
            sf.which = which;
            Ensure<UiButtonSounds>(root).library = lib;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) log.Add("ERROR could not save " + path);
            else log.Add("saved  " + path + " (" + which + ", button sounds)");
        }
    }
}
