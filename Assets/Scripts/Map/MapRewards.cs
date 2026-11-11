// ============================================================================
// MapRewards.cs
// Pays water for mapping: a one-time reward when a map section is mapped (R8.2) and when
// the whole maze passes 25 / 50 / 75 / 100% (R8.3). All rewards go into unbanked water,
// like every other maze reward, so you must carry them home.
//
// HOW IT WORKS: it listens to MapReveal.TileSeen, which fires only when the PLAYER sees a
// tile for the first time. After each new tile it checks that tile's section and the
// milestones. Loading a save and debug key 0 (reveal all) don't fire TileSeen, so they
// never pay. Which rewards were paid is saved, so nothing ever pays twice (R8.28).
// A survivor note that reveals tiles (R8.17) calls CheckTiles() itself; it only checks
// when the note revealed tiles that were still black.
// SurvivalDirector listens to the same TileSeen event and pays the separate 0.03 L per new
// tile (R8.2a). The two rewards add up on purpose (R8.2a: "R8.2 and R8.3 still pay on top").
//
// ASSUMPTION: sections are slices by angle (see MazeSections.cs); the CSV has no section codes.
//
// DESIGN RULES:
//   R8.2  Section mapped at 75% of its floor (V5 Cartographer: 60%, effect "sectionMappedFrac").
//         Reward 2 L in zone 1, 3 L in zone 2 (sectionReward), × 1.5 with V4 Surveyor
//         (effect "sectionRewardMult").
//   R8.3  Map 25% +10 L, 50% +15 L, 75% +20 L, 100% +30 L (milestonePercents / milestoneRewards).
//   R8.28 Mapping rewards never pay twice.
//
// HOW TO USE: nothing. It adds itself to the game scene (AutoSystems) and finds the map.
// HOW TO EDIT: every number is in GameBalance ("Maze and mapping", "Rewards", "Maze content").
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>One-time water rewards for mapped sections and whole-map milestones.</summary>
    public class MapRewards : MonoBehaviour, ISaveable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<MapRewards>();

        /// <summary>The one in this scene (null if there is none).</summary>
        public static MapRewards Instance { get; private set; }

        /// <summary>The map sections (null until the map exists).</summary>
        public MazeSections Sections { get; private set; }

        /// <summary>How many sections have paid their reward.</summary>
        public int SectionsMappedCount
        {
            get { int n = 0; if (sectionPaid != null) foreach (bool b in sectionPaid) if (b) n++; return n; }
        }

        GameBalance balance;
        MapRevealTracker tracker;
        MapReveal reveal;            // the MapReveal we listen to
        bool[] sectionPaid;
        bool[] milestonePaid;
        SaveData pendingRestore;     // a save that arrived before the map existed

        // ------------------------------------------------------------------ Setup

        void Start()
        {
            Instance = this;
            balance = BalanceProvider.Current;
            tracker = FindAnyObjectByType<MapRevealTracker>();
        }

        void OnDestroy()
        {
            if (reveal != null) reveal.TileSeen -= OnTileSeen;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // The map tracker makes its MapReveal in its Start, so connect once it exists.
            if (reveal == null) TryConnect();
        }

        bool TryConnect()
        {
            if (reveal != null) return true;
            if (balance == null) balance = BalanceProvider.Current;
            if (tracker == null) tracker = FindAnyObjectByType<MapRevealTracker>();
            if (balance == null || tracker == null || tracker.Reveal == null) return false;

            reveal = tracker.Reveal;
            reveal.TileSeen += OnTileSeen;
            Sections = new MazeSections(reveal.Grid, balance.zone1SectionCount, balance.zone2SectionCount);
            sectionPaid = new bool[Sections.Count];
            milestonePaid = new bool[balance.milestonePercents != null ? balance.milestonePercents.Length : 0];

            if (pendingRestore != null)
            {
                var data = pendingRestore;
                pendingRestore = null;
                Restore(data);
            }
            return true;
        }

        // ------------------------------------------------------------------ Checking

        void OnTileSeen(Vector2Int local)
        {
            CheckSection(Sections.SectionAt(local));
            CheckMilestones();
        }

        /// <summary>
        /// Checks the sections of these CSV (local) tiles and the milestones. A survivor note calls
        /// this after revealing tiles, and only when at least one of them was new.
        /// </summary>
        public void CheckTiles(IEnumerable<Vector2Int> localTiles)
        {
            if (!TryConnect()) return;
            var done = new HashSet<int>();
            foreach (var t in localTiles)
            {
                int s = Sections.SectionAt(t);
                if (done.Add(s)) CheckSection(s);
            }
            CheckMilestones();
        }

        /// <summary>R8.2 (and V5): share of a section's floor that counts as mapped.</summary>
        public float MappedThreshold => Skills.F("sectionMappedFrac", balance != null ? balance.sectionMappedPct : 0.75f);

        void CheckSection(int s)
        {
            if (s < 0 || sectionPaid == null || s >= sectionPaid.Length || sectionPaid[s]) return;
            if (Sections.TileCount(s) == 0) return;
            if (Sections.RevealedFraction(s, reveal) < MappedThreshold) return;

            sectionPaid[s] = true;
            int zoneIndex = Sections.PartOf(s) == MazePart.Zone2 ? 1 : 0;
            float baseReward = balance.sectionReward != null && zoneIndex < balance.sectionReward.Length
                ? balance.sectionReward[zoneIndex] : 0f;
            float litres = baseReward * Skills.F("sectionRewardMult", 1f);   // V4 Surveyor × 1.5
            Debug.Log($"[MapRewards] Section {Sections.NameOf(s)} (zone {zoneIndex + 1}) mapped: +{litres:0.0} L (R8.2).", this);
            SurvivalRules.Reward(litres, $"section {Sections.NameOf(s)} mapped");
        }

        void CheckMilestones()
        {
            if (milestonePaid == null || balance.milestonePercents == null) return;
            float percent = reveal.MapPercent;
            for (int i = 0; i < milestonePaid.Length; i++)
            {
                if (milestonePaid[i]) continue;
                // milestonePercents are 0-1; MapPercent is 0-100. The small margin lets 100% pay at exactly 100.
                if (percent < balance.milestonePercents[i] * 100f - 0.0001f) continue;
                milestonePaid[i] = true;
                float litres = balance.milestoneRewards != null && i < balance.milestoneRewards.Length
                    ? balance.milestoneRewards[i] : 0f;
                Debug.Log($"[MapRewards] Map {balance.milestonePercents[i] * 100f:0}% reached: +{litres:0.0} L (R8.3).", this);
                SurvivalRules.Reward(litres, $"map {balance.milestonePercents[i] * 100f:0}%");
            }
        }

        // ------------------------------------------------------------------ Saving

        /// <summary>Restored after the map (RestoreOrders.Map = 50).</summary>
        public int RestoreOrder => RestoreOrders.Map + 5;

        /// <summary>Saves which sections and milestones already paid.</summary>
        public void Capture(SaveData data)
        {
            if (sectionPaid == null) return;
            data.hasMapRewards = true;
            data.sectionsMapped.Clear();
            for (int i = 0; i < sectionPaid.Length; i++) if (sectionPaid[i]) data.sectionsMapped.Add(i);
            data.milestonesPaid.Clear();
            for (int i = 0; i < milestonePaid.Length; i++) if (milestonePaid[i]) data.milestonesPaid.Add(i);
        }

        /// <summary>Puts back which rewards were paid. An older save without this part: nothing paid yet.</summary>
        public void Restore(SaveData data)
        {
            if (!TryConnect()) { pendingRestore = data; return; }
            System.Array.Clear(sectionPaid, 0, sectionPaid.Length);
            System.Array.Clear(milestonePaid, 0, milestonePaid.Length);
            if (!data.hasMapRewards) return;
            foreach (int i in data.sectionsMapped) if (i >= 0 && i < sectionPaid.Length) sectionPaid[i] = true;
            foreach (int i in data.milestonesPaid) if (i >= 0 && i < milestonePaid.Length) milestonePaid[i] = true;
        }
    }
}
