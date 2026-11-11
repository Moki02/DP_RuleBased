// ============================================================================
// SurvivalDirector.cs
// Ties the survival loop together in the maze game:
//   - turns survival on (hydration drains, the canteen runs out) when GameBalance says so;
//   - pays 0.03 L of unbanked water for every new floor or room tile seen (R8.2a);
//   - banks unbanked water the moment the player walks into the hub, with the +10% Safe
//     Return bonus if it's still daytime (R3.5), and shows "+X L banked";
//   - shows the water numbers as plain text on the left of the screen.
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

        GameBalance balance;
        MazeBuilder maze;
        MapRevealTracker tracker;
        MapReveal subscribedReveal;
        bool wasAtHub = true;
        float hudTimer;

        void Start()
        {
            balance = BalanceProvider.Current;
            maze = FindAnyObjectByType<MazeBuilder>();
            tracker = FindAnyObjectByType<MapRevealTracker>();
            wasAtHub = PlayerAtHub();
        }

        void OnDestroy()
        {
            if (subscribedReveal != null) subscribedReveal.TileSeen -= OnTileSeen;
        }

        void Update()
        {
            // The map tracker makes its MapReveal in Start, so subscribe once it exists.
            if (subscribedReveal == null && tracker != null && tracker.Reveal != null)
            {
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
            if (atHub && !wasAtHub) Bank();
            wasAtHub = atHub;
        }

        void Bank()
        {
            var res = GameResources_Manager.instance;
            if (res == null || res.unbankedWater <= 0.0001f) return;

            float carried = res.unbankedWater;
            bool beforeLockdown = GameClock_Manager.Instance == null || !GameClock_Manager.Instance.IsNight;
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
            var stats = p.playerStats_Manager;

            if (hyd != null && stats != null && !hyd.hydrationOff)
                SimpleHud.SetLine("hyd", $"HYD {Mathf.CeilToInt(stats.player_Hydration)} / {stats.player_MaxHydration:0}  ({StateName(hyd.currentHydrationState)})");
            else
                SimpleHud.SetLine("hyd", null);

            SimpleHud.SetLine("canteen", $"Canteen {res.FormatWater(res.canteen)} / {res.canteen_Max:0.#} L" + (hyd != null && hyd.IsDrinking ? "  drinking..." : ""));
            SimpleHud.SetLine("unbanked", res.unbankedWater > 0.0001f ? $"Unbanked +{res.FormatWater(res.unbankedWater)}" : null);

            bool atHub = PlayerAtHub();
            SimpleHud.SetLine("clean", atHub ? $"Clean Storage {res.FormatWater(res.cleanStorage)} / {res.cleanStorage_Max:0} L" : null);
            SimpleHud.SetLine("raw", atHub ? $"Raw Basin {res.FormatWater(res.rawBasin)} / {res.rawBasin_Max:0} L" : null);
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
