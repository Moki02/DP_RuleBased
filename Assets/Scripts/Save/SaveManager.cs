// ============================================================================
// SaveManager.cs
// Saves and loads the game: one save slot, a JSON file at
//     Application.persistentDataPath/thelastdrop_save.json
// (on Windows: %USERPROFILE%/AppData/LocalLow/<company>/<product>/).
//   - Autosave right after every new day starts (NewDay). That comes after the night's
//     zone 2 turn, so a loaded game starts at that dawn with zone 2 already turned right.
//   - F5 saves now, F9 loads (debug keys).
//   - Every system saves its own part (ISaveable); this script only collects them,
//     restores them in order (clock first, player last) and writes the file.
//   - It saves the player's position and facing too, so a load puts them back.
//   - A bad or old-version file is rejected with a clear Console message; nothing breaks.
//
// CONTINUE BUTTON: the project has no main menu yet. When it gets one, enable its
// Continue button only if SaveManager.HasSave is true, and on click call
// SaveManager.LoadAndStart(); it opens the game scene and loads the save.
//
// DESIGN RULES: R11.8.
//
// HOW TO USE: put this on the "Systems" object (the Build Playtest Scene menu does it).
// Tools > The Last Drop > Delete Save removes the file.
// HOW TO EDIT: the file name and the game scene's name are the constants below.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>Saves and loads the one save slot.</summary>
    public class SaveManager : MonoBehaviour, ISaveable
    {
        /// <summary>The save file's name inside Application.persistentDataPath.</summary>
        public const string FileName = "thelastdrop_save.json";
        /// <summary>The scene LoadAndStart opens.</summary>
        public const string GameSceneName = "Playtest_Maze";

        /// <summary>The manager in this scene (set in Awake).</summary>
        public static SaveManager Instance { get; private set; }

        /// <summary>Full path of the save file.</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>True if a save file exists (use it to enable a Continue button).</summary>
        public static bool HasSave => File.Exists(SavePath);

        // Set by LoadAndStart: the next SaveManager to start in the game scene loads the save.
        static bool loadWhenSceneStarts;

        [Tooltip("Save automatically at every dawn (R11.8).")]
        public bool autosaveAtDawn = true;

        GameClock_Manager clock;

        /// <summary>
        /// Main menu's Continue button: opens the game scene and loads the save into it.
        /// (The scene must be in File > Build Profiles > Scene List; the playtest builder adds it.)
        /// </summary>
        public static void LoadAndStart()
        {
            if (!HasSave) { Debug.LogWarning("[SaveManager] Continue: there is no save."); return; }
            loadWhenSceneStarts = true;
            SceneManager.LoadScene(GameSceneName);
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (clock != null) clock.NewDay -= OnNewDay;
        }

        IEnumerator Start()
        {
            clock = GameClock_Manager.Instance;
            if (clock != null) clock.NewDay += OnNewDay;

            if (!loadWhenSceneStarts) yield break;
            loadWhenSceneStarts = false;
            // Wait two frames so every system has finished its own Start (gates, map, menu).
            yield return null;
            yield return null;
            Load();
        }

        void OnNewDay(int day)
        {
            if (!autosaveAtDawn) return;
            if (Save()) Debug.Log($"[SaveManager] Autosaved at the dawn of day {day}.", this);
        }

        // ------------------------------------------------------------------ Save / load

        /// <summary>Saves everything now (F5). Returns false if writing failed.</summary>
        public bool Save()
        {
            var data = new SaveData { savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            foreach (var s in Saveables()) s.Capture(data);
            try
            {
                SaveFile.Write(SavePath, data);
                Debug.Log($"[SaveManager] Saved day {data.day} to {SavePath}", this);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Couldn't save to {SavePath}: {e.Message}", this);
                return false;
            }
        }

        /// <summary>Loads the save (F9). Returns false, with a Console message, if it's missing or bad.</summary>
        public bool Load()
        {
            if (!SaveFile.TryLoad(SavePath, out var data, out string error))
            {
                Debug.LogWarning($"[SaveManager] Didn't load the save: {error}. ({SavePath})", this);
                return false;
            }
            foreach (var s in Saveables().OrderBy(s => s.RestoreOrder)) s.Restore(data);
            Debug.Log($"[SaveManager] Loaded day {data.day} (saved {data.savedAt}).", this);
            return true;
        }

        /// <summary>Deletes the save file (and any half-written temporary file).</summary>
        public static void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(SavePath + ".tmp")) File.Delete(SavePath + ".tmp");
        }

        // Every script in the scene that saves its own part.
        static List<ISaveable> Saveables()
        {
            var list = new List<ISaveable>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is ISaveable s) list.Add(s);
            return list;
        }

        // ------------------------------------------------------------------ The player's part

        /// <summary>The player is put back last, after zone 2 is in place.</summary>
        public int RestoreOrder => RestoreOrders.Player;

        /// <summary>Saves the player's position and facing.</summary>
        public void Capture(SaveData data)
        {
            var p = Player_Manager.instance;
            data.hasPlayer = p != null;
            if (p == null) return;
            data.playerPosition = p.transform.position;
            data.playerYaw = p.transform.eulerAngles.y;
        }

        /// <summary>Puts the player back where they were saved.</summary>
        public void Restore(SaveData data)
        {
            var p = Player_Manager.instance;
            if (p == null || !data.hasPlayer) return;
            var cc = p.characterController;
            bool on = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;   // the CharacterController fights being moved by hand
            p.transform.SetPositionAndRotation(data.playerPosition, Quaternion.Euler(0f, data.playerYaw, 0f));
            if (cc != null) cc.enabled = on;
            if (Player_Camera.instance != null) Player_Camera.instance.TurnWithPlayer(0f); // snap the camera onto the player
        }
    }
}
