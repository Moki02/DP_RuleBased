// ============================================================================
// UpgradeManager.cs
// Owns the Build and Skills state in the scene: what's owned and how much water is in
// Clean Storage (UpgradeLogic does the rules). Applies the effects this scene can show
// and keeps the HUD's Clean Storage numbers (GameResources_Manager) in step.
//
// Effects WIRED in the playtest:
//   M1 Light Sandals   walkSpeed 4.4        -> PlayerStats_Manager walking speed
//   M2 Long Stride     sprintSpeed 7.3      -> PlayerStats_Manager running speed
//   V1 Trail Sense     lookAheadTiles 4     -> MapRevealTracker
//   V2 Landmarks       maxMarkers 8         -> MapRevealTracker
//   V3 Keen Eyes       revealDiagonals true -> MapRevealTracker
//   S2/S3/S6 storage   cleanStorageCap      -> Clean Storage size (UpgradeLogic)
// Effects STORED but not used yet (the future system that will read each one):
//   pumpStrokeInterval, pumpUsesDeepTable          -> HubPump_Manager (hand pump)
//   unlockSource, catchArea, stillRate, solarStillCount, wellRate -> water sources
//   canteenCapacity, heavySpeedMult                 -> canteen / carrying
//   cleanCoverFactor, rawBasinCap                    -> storage and evaporation
//   purifierRate, ripenDays, sodisBatch, muddyPoolRefill -> purifiers
//   drainMultAll, drainMultMove, thirstySprintMult, dehydratedWalkMult, hydPerLiter,
//   wakeHyd, sickDuration, keepCanteenOnCollapse, drinkPourRateMult -> PlayerHydration_Manager
//   gatesOnMap, sectionRewardMult, sectionMappedFrac -> map rewards (sections)
// Any script can read an effect with: UpgradeManager.Instance.GetFloat("walkSpeed", 4f).
//
// DESIGN RULES: R7.1 to R7.4 (Build and Skills), R7.2 (buy at the hub), R7.3 (storage
// limits prices), R7.4 (no refunds).
//
// HOW TO USE: put this on the "Systems" object and drag UpgradeDatabase into "Database"
// (the Build Playtest Scene menu does both). Debug key 7 adds 50 L of Clean Storage.
// HOW TO EDIT: prices and effects are in the CSVs; start water, storage sizes, the debug
// amount and "Allow Buying Anywhere" are in GameBalance.
// ============================================================================

using System;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>The scene's Build and Skills state, and the effects of what's owned.</summary>
    public class UpgradeManager : MonoBehaviour, ISaveable
    {
        /// <summary>Restored after zone 2, before the gates.</summary>
        public int RestoreOrder => RestoreOrders.Upgrades;

        /// <summary>Saves owned upgrades (in buy order) and Clean Storage.</summary>
        public void Capture(SaveData data)
        {
            if (Logic == null) return;
            data.ownedUpgrades = new System.Collections.Generic.List<string>(Logic.Owned);
            data.cleanStorage = Logic.Stored;
        }

        /// <summary>Puts owned upgrades and Clean Storage back; their effects apply at once.</summary>
        public void Restore(SaveData data)
        {
            if (Logic == null) return;
            Logic.Restore(data.ownedUpgrades ?? new System.Collections.Generic.List<string>(), data.cleanStorage);
        }

        /// <summary>The manager in this scene (set in Awake).</summary>
        public static UpgradeManager Instance { get; private set; }

        [Tooltip("Every Build item and skill (made by Tools > The Last Drop > Import Build and Skills CSV).")]
        public UpgradeDatabase database;
        [Tooltip("The Maze object, to check whether the player is at the hub. Empty = found by itself.")]
        public MazeBuilder maze;

        /// <summary>The rules and state (null if there's no database).</summary>
        public UpgradeLogic Logic { get; private set; }

        /// <summary>Fired after anything changes (bought, water added, loaded).</summary>
        public event Action Changed;

        GameBalance balance;

        void Awake()
        {
            Instance = this;
            balance = BalanceProvider.Current;
            if (database == null || balance == null)
            {
                Debug.LogError("[UpgradeManager] Needs the UpgradeDatabase and a BalanceProvider with GameBalance.", this);
                enabled = false;
                return;
            }
            float baseCap = balance.cleanStorageTiers != null && balance.cleanStorageTiers.Length > 0
                ? balance.cleanStorageTiers[0].capacity : balance.startClean;
            Logic = new UpgradeLogic(database.ToInfos(), balance.startClean, baseCap, balance.skillCostMultiplier);
            Logic.Changed += OnLogicChanged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            var tracker = FindAnyObjectByType<MapRevealTracker>();
            if (tracker != null) tracker.upgrades = this;
            ApplyEffects();
            SyncResources();
        }

        void OnLogicChanged()
        {
            ApplyEffects();
            SyncResources();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Shortcuts

        /// <summary>An effect value from owned upgrades, or <paramref name="defaultValue"/> (see UpgradeLogic.GetFloat).</summary>
        public float GetFloat(string field, float defaultValue) => Logic != null ? Logic.GetFloat(field, defaultValue) : defaultValue;

        /// <summary>A true/false effect from owned upgrades, or <paramref name="defaultValue"/>.</summary>
        public bool GetBool(string field, bool defaultValue) => Logic != null ? Logic.GetBool(field, defaultValue) : defaultValue;

        /// <summary>True when the player stands in the hub (ring ≤ GameBalance "Hub Max Ring").</summary>
        public bool PlayerAtHub
        {
            get
            {
                var p = Player_Manager.instance;
                if (p == null || maze == null || maze.Grid == null || balance == null) return false;
                return maze.Grid.Ring(maze.WorldToGrid(p.transform.position)) <= balance.hubMaxRing;
            }
        }

        /// <summary>True if buying is allowed where the player stands (at the hub, or anywhere in the playtest).</summary>
        public bool CanBuyHere => balance != null && (balance.allowBuyingAnywhere || PlayerAtHub);

        /// <summary>Tries to buy; logs what happened.</summary>
        public BuyResult Buy(string id)
        {
            if (Logic == null) return BuyResult.UnknownId;
            var result = Logic.Buy(id, PlayerAtHub, balance.allowBuyingAnywhere);
            Debug.Log($"[Upgrades] Buy {id}: {result}. Clean Storage {Logic.Stored:0.#} / {Logic.Cap:0.#} L.", this);
            return result;
        }

        /// <summary>Debug key 7: adds water to Clean Storage, never past its size.</summary>
        public void AddCleanStorage(float litres)
        {
            if (Logic == null) return;
            float added = Logic.AddStored(litres);
            Debug.Log($"[Upgrades] +{added:0.#} L Clean Storage (debug). Now {Logic.Stored:0.#} / {Logic.Cap:0.#} L.", this);
        }

        // ------------------------------------------------------------------ Effects

        void ApplyEffects()
        {
            if (Logic == null || balance == null) return;
            var player = Player_Manager.instance;
            if (player != null && player.playerStats_Manager != null)
            {
                // PlayerLocomotion reads these every frame, so the new speed works at once.
                player.playerStats_Manager.player_WalkingSpeed = Logic.GetFloat("walkSpeed", balance.walkSpeed);
                player.playerStats_Manager.player_RunningSpeed = Logic.GetFloat("sprintSpeed", balance.sprintSpeed);
            }
            // V1, V2, V3: MapRevealTracker asks this manager each time (see its LookAheadTiles etc.).
        }

        // The HUD and the rest of the team's code read Clean Storage from GameResources_Manager,
        // so copy the numbers there. In the playtest this manager is the one that changes them.
        void SyncResources()
        {
            var res = GameResources_Manager.instance;
            if (res == null || Logic == null) return;
            res.cleanStorage = Logic.Stored;
            res.cleanStorage_Max = Logic.Cap;
        }
    }
}
