// ============================================================================
// CollapseManager.cs
// What happens when HYD hits 0 (R11.1 to R11.5):
//   1. The screen fades to black over 2 s; the game is frozen meanwhile.
//   2. The collapse costs: all canteen water (kept with E5 Second Wind), half the
//      unbanked water (rounded down to 0.1 L), and 30 s of clock time.
//   3. If Clean Storage holds less than 2 L, the game ends: "The drought wins."
//   4. Otherwise the player wakes at the hub, drinks 2 L from Clean Storage and has
//      50 HYD (60 with E3 Water Discipline).
//
// DESIGN RULES: R11.1 to R11.5.
//
// HOW TO USE: added to the game scene by itself (needs a BalanceProvider). Nothing to set.
// HOW TO EDIT: GameBalance > Collapse and game over, and Clock > Collapse Clock Penalty.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastDrop
{
    /// <summary>Runs collapses, waking up at the hub, and game over.</summary>
    public class CollapseManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<CollapseManager>();

        /// <summary>The manager in this scene (null in old scenes).</summary>
        public static CollapseManager Instance { get; private set; }

        /// <summary>True once the game is over.</summary>
        public bool IsGameOver { get; private set; }

        GameBalance balance;
        PlayerHydration_Manager hydration;
        bool running;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (hydration != null) hydration.OnCollapsed -= OnCollapsed;
        }

        void Start()
        {
            balance = BalanceProvider.Current;
            var p = Player_Manager.instance;
            hydration = p != null ? p.playerHydration_Manager : FindAnyObjectByType<PlayerHydration_Manager>();
            if (hydration != null) hydration.OnCollapsed += OnCollapsed;
        }

        void OnCollapsed()
        {
            if (running || IsGameOver) return;
            StartCoroutine(CollapseRoutine());
        }

        IEnumerator CollapseRoutine()
        {
            running = true;
            RunStats.AddCollapse();

            // 1. Fade to black (real time: the game is frozen by the Collapsed state).
            float fadeTime = balance != null ? balance.collapseFadeSeconds : 2f;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SimpleHud.SetFade(t / fadeTime);
                yield return null;
            }
            SimpleHud.SetFade(1f);

            // 2. The costs (R11.2).
            var res = GameResources_Manager.instance;
            float lostCanteen = 0f, lostUnbanked = 0f;
            if (res != null)
            {
                if (!Skills.B("keepCanteenOnCollapse", false))
                {
                    lostCanteen = res.canteen;
                    res.canteen = 0f;
                }
                float lossShare = balance != null ? balance.collapseUnbankedLoss : 0.5f;
                lostUnbanked = Mathf.Floor(res.unbankedWater * lossShare * 10f) / 10f;
                res.unbankedWater = Mathf.Max(0f, res.unbankedWater - lostUnbanked);
            }
            var clock = GameClock_Manager.Instance;
            float clockCost = balance != null ? balance.collapseClockPenalty : 30f;
            // Advance multiplies by the debug clock speed, so divide it back out: always 30 game seconds.
            if (clock != null && clock.Clock != null)
                clock.Clock.Advance(clockCost / Mathf.Max(0.01f, clock.Clock.SpeedMultiplier));

            // 3. Game over? (R11.4)
            float wakeLitres = balance != null ? balance.wakeLiters : 2f;
            float needed = balance != null ? balance.gameOverBelowLiters : 2f;
            if (res == null || res.cleanStorage < needed - 1e-4f)
            {
                GameOver();
                running = false;
                yield break;
            }

            // 4. Wake at the hub (R11.3).
            res.TakeCleanWater(wakeLitres);
            float wakeHyd = Skills.F("wakeHyd", balance != null ? balance.GetWakeHyd(balance.hydPerLiter) : 50f);
            var death = Player_Manager.instance != null ? Player_Manager.instance.playerDeath_Manager : null;
            GetSpawn(out Vector3 pos, out Quaternion rot);
            if (death != null) death.ReviveAt(pos, rot, wakeHyd);
            else if (Game_Manager.instance != null) Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Exploring);

            SimpleHud.SetFade(0f);
            string kept = Skills.B("keepCanteenOnCollapse", false) ? "canteen kept (Second Wind)" : $"-{lostCanteen:0.0} L canteen";
            SimpleHud.Banner("YOU COLLAPSED", $"{kept}, -{lostUnbanked:0.0} L unbanked, -{clockCost:0} s. You drank {wakeLitres:0.#} L at home.", 5f);
            running = false;
        }

        // Where the player wakes: the maze's spawn, or the old respawn point.
        void GetSpawn(out Vector3 pos, out Quaternion rot)
        {
            var maze = FindAnyObjectByType<MazeBuilder>();
            if (maze != null && maze.PlayerSpawn != null)
            {
                pos = maze.PlayerSpawn.position;
                rot = maze.PlayerSpawn.rotation;
                return;
            }
            var death = Player_Manager.instance != null ? Player_Manager.instance.playerDeath_Manager : null;
            if (death != null && death.respawnPoint != null)
            {
                pos = death.respawnPoint.position;
                rot = death.respawnPoint.rotation;
                return;
            }
            var p = Player_Manager.instance;
            pos = p != null ? p.transform.position : Vector3.zero;
            rot = p != null ? p.transform.rotation : Quaternion.identity;
        }

        void GameOver()
        {
            IsGameOver = true;
            if (Game_Manager.instance != null) Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Collapsed);
            ShowEndScreen("THE DROUGHT WINS", "You collapsed with less than 2 L at home.");
        }

        /// <summary>Shows an end screen (game over or win) with the run's stats and Restart / Quit.</summary>
        public static void ShowEndScreen(string title, string firstLine)
        {
            SimpleHud.SetFade(0f);
            string body = (string.IsNullOrEmpty(firstLine) ? "" : firstLine + "\n\n") + RunStats.Summary();
            SimpleHud.ShowPanel(title, body,
                ("Restart", new System.Action(Restart)),
                ("Quit", new System.Action(Quit)));
        }

        /// <summary>Starts a new run: deletes the save (not in Demo mode) and reloads the scene, skipping the title screen.</summary>
        public static void Restart()
        {
            // A Demo run doesn't touch the full game's save; the next run skips the title screen.
            if (!BalanceProvider.UseDemo) SaveManager.DeleteSave();
            MainMenu.SkipMenuOnce = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Quits the game (stops Play mode in the editor).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
