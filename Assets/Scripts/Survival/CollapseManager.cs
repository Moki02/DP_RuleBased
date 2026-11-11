// ============================================================================
// CollapseManager.cs
// What happens when HYD hits 0 (R11.1 to R11.5):
//   1. The screen fades to black over 2 s; the game is frozen meanwhile (time scale 0,
//      so the fade counts real time). An open minigame is closed with no penalty.
//   2. The collapse costs: all canteen water (kept with E5 Second Wind) and half the
//      unbanked water (rounded down to 0.1 L).
//   3. If Clean Storage holds less than 2 L, the game ends: "The drought wins." (end screen
//      with the run's stats, Restart / Quit).
//   4. Otherwise the player wakes at the hub, drinks 2 L from Clean Storage and has
//      50 HYD (60 with E3 Water Discipline). The rest of the unbanked water is banked
//      (no Safe Return bonus), and THEN the clock jumps 30 s (the last R11.2 cost; done
//      last so a dawn autosave in those 30 s saves the woken player).
//
// DESIGN RULES: R11.1 to R11.5, R3.5 (banking on waking).
//
// HOW TO USE: added to the game scene by itself (needs a BalanceProvider). Nothing to set.
// It listens to PlayerHydration_Manager.OnCollapsed; PlayerDeath_Manager listens too and
// plays the collapse animation and freezes the game (Game_Manager state Collapsed).
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

            // A minigame left open would still be on screen (and playable) after waking.
            // Close it as a Quit (no penalty), then make sure the game stays frozen: closing it
            // may have switched the state from Minigame back to Exploring.
            if (ValveTimingGame.IsPlaying) ValveTimingGame.Instance.Cancel();
            var gm = Game_Manager.instance;
            if (gm != null && gm.currentGameState != Game_Manager.GameState.Collapsed)
                gm.ChangeGameState(Game_Manager.GameState.Collapsed);

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
            float clockCost = balance != null ? balance.collapseClockPenalty : 30f;

            // 3. Game over? (R11.4)
            float wakeLitres = balance != null ? balance.wakeLiters : 2f;
            float needed = balance != null ? balance.gameOverBelowLiters : 2f;
            if (res == null || res.cleanStorage < needed - 1e-4f)
            {
                // No clock penalty here: moving the clock could fire the dawn autosave and save
                // the game-over state (0 HYD, empty canteen) over the last good save.
                GameOver(needed);
                running = false;
                yield break;
            }

            // 4. Wake at the hub (R11.3).
            res.TakeCleanWater(wakeLitres);
            float wakeHyd = Skills.F("wakeHyd", balance != null ? balance.GetWakeHyd(balance.hydPerLiter) : 50f);
            var death = Player_Manager.instance != null ? Player_Manager.instance.playerDeath_Manager : null;
            GetSpawn(out Vector3 pos, out Quaternion rot);
            if (death != null) death.ReviveAt(pos, rot, wakeHyd);
            else
            {
                // No PlayerDeath_Manager: still refill HYD, or the player would collapse again at once.
                if (hydration != null) hydration.ResetAfterRevive(wakeHyd);
                if (Game_Manager.instance != null) Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Exploring);
            }

            // The rest of the unbanked water is home now (R3.5), but with no Safe Return bonus.
            if (SurvivalDirector.Instance != null) SurvivalDirector.Instance.Bank(false);

            // R11.2: 30 s of clock time. Done AFTER waking on purpose: if those 30 s cross dawn,
            // the dawn autosave (and anything else listening for night or dawn) sees the woken
            // player, not one lying at 0 HYD with an empty canteen.
            // Advance multiplies by the debug clock speed, so divide it back out: always 30 game seconds.
            var clock = GameClock_Manager.Instance;
            if (clock != null && clock.Clock != null)
                clock.Clock.Advance(clockCost / Mathf.Max(0.01f, clock.Clock.SpeedMultiplier));

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

        // R11.4: the end. The game stays frozen (Collapsed state) behind the end screen.
        void GameOver(float neededLitres)
        {
            IsGameOver = true;
            if (Game_Manager.instance != null) Game_Manager.instance.ChangeGameState(Game_Manager.GameState.Collapsed);
            ShowEndScreen("THE DROUGHT WINS", $"You collapsed with less than {neededLitres:0.#} L at home.");
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
