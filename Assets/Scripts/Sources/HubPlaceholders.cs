// ============================================================================
// HubPlaceholders.cs
// Runs the hub's storage objects that are placed in the scene (HubSceneObjects):
//
//   Raw Basin       shows the owned tier: Basin 15 L, Bigger Basin 40 L (S5),
//                   Concrete Basin 100 L (S8), with a "litres / size" label.
//   Clean Storage   ONE container, on the team's Clean Storage tap prefab, shown as the
//                   owned tier: Tapayan 25 L, Plastic Drum 50 L (S2, the prefab's own drums),
//                   Ferrocement Tank 150 L (S3), Underground Cistern 300 L (S6) (R5.3).
//                   Its level gauge rises with the water; the tap's name text shows the litres.
//   Spare jugs      one jug per few litres by the hub door, only while there are any (R3.5).
//
// Nothing is made at Play: all tiers are already in the scene and only the owned one is
// turned on. A scene with no HubObjects gets one made at Play, with a warning.
//
// DESIGN RULES: R3.5 (spare jugs), R5.1 (an upgrade replaces the container, water moves
// over: the amounts come from GameResources_Manager), R5.3 (each tier visible).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems). Place the
// objects with Salok > Build > 2. Place hub objects in scene, then move them by hand.
// ARTISTS: models come from Assets/Resources/HubModels/ (names in HubSceneBuilder):
//   RawBasin_Basin, RawBasin_BiggerBasin, RawBasin_ConcreteBasin,
//   CleanTank_Tapayan, CleanTank_FerrocementTank, CleanTank_UndergroundCistern, SpareJug
// (the Plastic Drum is the Clean Storage prefab's own drums). Salok > Build > 1. Set up
// finished models makes these from Assets/Art/Models/Finished Model.
// HOW TO EDIT: tiles and jug numbers are in GameBalance > Hub placeholders.
// ============================================================================

using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Shows the owned Raw Basin / Clean Storage tier, the gauge, the spare jugs and their labels.</summary>
    public class HubPlaceholders : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<HubPlaceholders>();

        /// <summary>Folder under Resources where the hub models are.</summary>
        public const string ModelFolder = "HubModels/";

        GameBalance balance;
        GameResources_Manager res;
        MazeBuilder maze;
        UpgradeManager upgrades;
        HubSceneObjects hub;
        bool dirty = true;

        // ------------------------------------------------------------------ Lifecycle

        void Start()
        {
            balance = BalanceProvider.Current;
            res = GameResources_Manager.instance;
            upgrades = UpgradeManager.Instance;
            if (upgrades != null) upgrades.Changed += OnUpgradesChanged;
        }

        void OnDestroy()
        {
            if (upgrades != null) upgrades.Changed -= OnUpgradesChanged;
        }

        // Bought something or loaded a save: check if a tier changed.
        void OnUpgradesChanged() => dirty = true;

        void Update()
        {
            if (balance == null) balance = BalanceProvider.Current;
            if (res == null) res = GameResources_Manager.instance;
            if (balance == null || res == null) return;
            if (hub == null && !FindOrMakeHub()) return;

            if (dirty)
            {
                dirty = false;
                HubSceneObjects.ShowTier(hub.rawBasinTiers, RawTierName());
                HubSceneObjects.ShowTier(hub.cleanStorageTiers, CleanTierName());
            }

            UpdateRawBasin();
            UpdateCleanGauge();
            UpdateSpareJugs();
        }

        // The scene's HubObjects; if there are none, make them (old scenes) once the maze is ready.
        bool FindOrMakeHub()
        {
            hub = HubSceneObjects.Instance;
            if (hub != null) { dirty = true; return true; }
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (maze == null || maze.Grid == null) return false;
            Debug.LogWarning("[HubPlaceholders] This scene has no HubObjects, so they were made at Play. " +
                             "Run Salok > Build > 2. Place hub objects in scene to keep them in the hierarchy.", this);
            hub = HubSceneBuilder.Build(maze, balance);
            dirty = true;
            return true;
        }

        // ------------------------------------------------------------------ Tiers

        static string RawTierName() =>
            Skills.Owns("S8") ? "Concrete Basin" : Skills.Owns("S5") ? "Bigger Basin" : "Basin";

        static string CleanTierName() =>
            Skills.Owns("S6") ? "Underground Cistern" :
            Skills.Owns("S3") ? "Ferrocement Tank" :
            Skills.Owns("S2") ? "Plastic Drum" : "Tapayan";

        // ------------------------------------------------------------------ Updates

        void UpdateRawBasin()
        {
            if (hub.rawBasinLabel == null) return;
            float amount = res.rawBasin, max = Mathf.Max(0.01f, res.rawBasin_Max);
            string full = amount >= max - 0.0001f ? "  FULL" : "";
            hub.rawBasinLabel.Text = $"Raw Basin\n{res.FormatWater(amount)} / {res.FormatWater(max)}{full}";
        }

        void UpdateCleanGauge()
        {
            var fill = hub.cleanGaugeFill;
            if (fill == null) return;
            float amount = res.cleanStorage, max = Mathf.Max(0.01f, res.cleanStorage_Max);
            float h = Mathf.Max(0.001f, Mathf.Clamp01(amount / max) * hub.cleanGaugeHeight);
            var s = fill.localScale;
            if (!Mathf.Approximately(s.y, h))
            {
                fill.localScale = new Vector3(s.x, h, s.z);
                var p = fill.localPosition;
                fill.localPosition = new Vector3(p.x, h / 2f, p.z);
            }
            bool on = amount > 0.0001f;
            if (fill.gameObject.activeSelf != on) fill.gameObject.SetActive(on);
        }

        void UpdateSpareJugs()
        {
            float litres = res.spareJugs;
            int want = litres > 0.0001f ? Mathf.Max(1, Mathf.CeilToInt(litres / balance.litresPerSpareJug)) : 0;
            var jugs = hub.spareJugs;
            if (jugs != null)
                for (int i = 0; i < jugs.Length; i++)
                    if (jugs[i] != null && jugs[i].activeSelf != (i < want)) jugs[i].SetActive(i < want);

            if (hub.spareJugsLabel != null)
                hub.spareJugsLabel.Text = want > 0 ? $"Spare jugs\n{res.FormatWater(litres)}" : "";
        }

        /// <summary>
        /// An artist's model from Resources/HubModels under <paramref name="parent"/> (colliders off),
        /// or null if there is none.
        /// </summary>
        public static GameObject ArtistModel(Transform parent, string modelName) =>
            Resources.Load<GameObject>(ModelFolder + modelName) != null ? HubSceneBuilder.Model(parent, modelName) : null;
    }
}
