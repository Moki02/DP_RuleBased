// ============================================================================
// SurvivalDirector.cs
// Ties the survival loop together in the maze game:
//   (GameBalance > Survival On itself is applied by GameResources_Manager and
//    PlayerHydration_Manager: drain on, canteen runs out.)
//   - pays 0.03 L of unbanked water for every new floor or room tile seen (R8.2a), using
//     MapReveal.TileSeen (not fired by loading a save or by debug key 0, so never paid twice);
//   - banks unbanked water the moment the player walks into the hub, with the +10% Safe
//     Return bonus if it's still daytime (R3.5), and shows "+X L banked"; what doesn't fit
//     goes to the Raw Basin, then the spare jugs (GameResources_Manager.StoreHomeWater);
//   - shows extra water text (unbanked, spare jugs, Sick, drinking) bottom left; HYD, canteen and storage are on the team HUD.
// SurvivalRules (bottom) are small helpers for minigame losses and rewards (R9.3, R8.8).
//
// DESIGN RULES: R2.4, R3.5, R8.2a, R8.8, R9.3.
//
// HOW TO USE: added to the game scene by itself. Nothing to set.
// HOW TO EDIT: GameBalance > Survival loop (tile reward, Safe Return bonus).
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Banking, the tile reward and the water text on the HUD.</summary>
    public class SurvivalDirector : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<SurvivalDirector>();

        [Tooltip("How often the HUD text refreshes, in seconds.")]
        [Min(0.02f)] public float hudInterval = 0.15f;

        /// <summary>The director in this scene (null in old scenes).</summary>
        public static SurvivalDirector Instance { get; private set; }

        GameBalance balance;
        MazeBuilder maze;
        MapRevealTracker tracker;
        MapReveal subscribedReveal;
        bool wasAtHub = true;
        float hudTimer;

        void Awake() => Instance = this;

        void Start()
        {
            balance = BalanceProvider.Current;
            maze = FindAnyObjectByType<MazeBuilder>();
            tracker = FindAnyObjectByType<MapRevealTracker>();
            wasAtHub = PlayerAtHub();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribedReveal != null) subscribedReveal.TileSeen -= OnTileSeen;
        }

        void Update()
        {
            // The map tracker makes its MapReveal in Start, so subscribe once it exists
            // (and move over if it's ever replaced, so a tile is never paid by two of them).
            if (tracker != null && tracker.Reveal != null && tracker.Reveal != subscribedReveal)
            {
                if (subscribedReveal != null) subscribedReveal.TileSeen -= OnTileSeen;
                subscribedReveal = tracker.Reveal;
                subscribedReveal.TileSeen += OnTileSeen;
            }

            if (Time.timeScale > 0f) CheckBanking();

            hudTimer -= Time.unscaledDeltaTime;
            if (hudTimer <= 0f)
            {
                hudTimer = hudInterval;
                RefreshHud();
            }
        }

        // ------------------------------------------------------------------ R8.2a tile reward

        void OnTileSeen(Vector2Int local)
        {
            if (balance == null || maze == null || maze.Grid == null) return;
            var type = maze.Grid.Get(local.x, local.y);
            if (type != TileType.Floor && type != TileType.Room) return;
            var res = GameResources_Manager.instance;
            if (res == null) return;
            res.AddUnbankedWater(balance.tileReward);
            RunStats.AddCollected(balance.tileReward);
        }

        // ------------------------------------------------------------------ R3.5 banking

        bool PlayerAtHub()
        {
            if (UpgradeManager.Instance != null && maze != null) return UpgradeManager.Instance.PlayerAtHub;
            var p = Player_Manager.instance;
            return p != null && p.playerHydration_Manager != null && p.playerHydration_Manager.IsInsideHub();
        }

        void CheckBanking()
        {
            bool atHub = PlayerAtHub();
            if (atHub && !wasAtHub) Bank(true);
            wasAtHub = atHub;
        }

        /// <summary>
        /// Banks the unbanked water now (the player is at the hub). <paramref name="allowBonus"/> = false
        /// skips the Safe Return bonus (used after a collapse: being carried home isn't a safe return).
        /// </summary>
        public void Bank(bool allowBonus)
        {
            var res = GameResources_Manager.instance;
            if (res == null || res.unbankedWater <= 0.0001f) return;

            float carried = res.unbankedWater;
            bool beforeLockdown = allowBonus && (GameClock_Manager.Instance == null || !GameClock_Manager.Instance.IsNight);
            float bonus = beforeLockdown && balance != null ? carried * balance.safeReturnBonus : 0f;
            res.unbankedWater = 0f;
            float toJugs = res.StoreHomeWater(carried + bonus);
            RunStats.AddCollected(bonus); // the carried part was counted when it was earned

            string msg = $"+{carried:0.0} L banked";
            if (bonus > 0f) msg += $"  (+{bonus:0.0} L Safe Return)";
            if (toJugs > 0f) msg += $"  ({toJugs:0.0} L into spare jugs)";
            SimpleHud.Toast(msg, 4f);
        }

        // ------------------------------------------------------------------ HUD text

        void RefreshHud()
        {
            var res = GameResources_Manager.instance;
            var p = Player_Manager.instance;
            if (res == null || p == null) return;
            var hyd = p.playerHydration_Manager;

            // HYD, the canteen, Raw Basin and Clean Storage are shown by the team's Player HUD and
            // Hub HUD, so this text only adds what those don't show.
            SimpleHud.SetLine("drinking", hyd != null && hyd.IsDrinking ? $"Drinking...  canteen {res.FormatWater(res.canteen)}" : null);
            SimpleHud.SetLine("unbanked", res.unbankedWater > 0.0001f ? $"Unbanked +{res.FormatWater(res.unbankedWater)}" : null);

            SimpleHud.SetLine("jugs", res.spareJugs > 0.0001f ? $"Spare jugs {res.FormatWater(res.spareJugs)}" : null);
            SimpleHud.SetLine("sick", hyd != null && hyd.SickSecondsLeft > 0f ? $"SICK {Mathf.CeilToInt(hyd.SickSecondsLeft)} s (drain ×1.5)" : null);
        }

        static string StateName(PlayerHydration_Manager.HydrationState s)
        {
            switch (s)
            {
                case PlayerHydration_Manager.HydrationState.Thirsty: return "Thirsty";
                case PlayerHydration_Manager.HydrationState.Dehydrated: return "Dehydrated";
                case PlayerHydration_Manager.HydrationState.Collapsed: return "Collapsed";
                default: return "Hydrated";
            }
        }
    }

    /// <summary>Small shared rules for minigames (R9.3, R8.8).</summary>
    public static class SurvivalRules
    {
        /// <summary>R9.3: a lost minigame costs HYD (5).</summary>
        public static void MinigameLost()
        {
            var b = BalanceProvider.Current;
            float cost = b != null ? b.minigameFailHyd : 5f;
            var p = Player_Manager.instance;
            if (p != null && p.playerHydration_Manager != null) p.playerHydration_Manager.LoseHydration(cost);
            SimpleHud.Toast($"Valve lost: -{cost:0} HYD", 3f);
        }

        /// <summary>R8.8: a one-time water reward, paid into unbanked water.</summary>
        public static void Reward(float litres, string why)
        {
            if (litres <= 0f) return;
            var res = GameResources_Manager.instance;
            if (res == null) return;
            res.AddUnbankedWater(litres);
            RunStats.AddCollected(litres);
            SimpleHud.Toast($"+{litres:0.0} L unbanked ({why})", 3f);
        }
    }
}
