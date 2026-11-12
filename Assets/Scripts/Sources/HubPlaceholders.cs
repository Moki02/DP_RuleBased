// ============================================================================
// HubPlaceholders.cs
// Gray-box stand-ins for the hub objects that had no model yet, so the whole game can be
// played and read before the artists' models arrive:
//
//   Raw Basin           an open box whose water rises with the litres in it. Grows with
//                       its tier: Basin 15 L, Bigger Basin 40 L (S5), Concrete Basin 100 L (S8).
//   Clean Storage tank  one model per tier, with a gauge beside it: Tapayan 25 L,
//                       Plastic Drum 50 L (S2), Ferrocement Tank 150 L (S3),
//                       Underground Cistern 300 L (S6). The team's Clean Storage tap
//                       prefab stays where it is; this only shows WHICH tank you own (R5.3).
//   Spare jugs          one jug per few litres by the hub door, only while there are any (R3.5).
//
// Each has a floating label (R4.6 spirit: the player never guesses). Parts have no
// colliders, so the base stays walkable. Everything else in the hub already exists:
// pump, boiling pot and tap (Build Playtest Scene), Rain Catcher / Solar Still / Deep Well
// (WaterSources), Biosand Filter (BiosandFilter), the 4 hub lock basins (GateManager).
//
// DESIGN RULES: R3.5 (spare jugs), R5.1 (an upgrade replaces the container, water moves
// over: the amounts come from GameResources_Manager), R5.3 (each tier visible).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems).
// ARTISTS: to swap a gray box for a real model, put a prefab in
//   Assets/Resources/HubModels/ named exactly
//   RawBasin_Basin, RawBasin_BiggerBasin, RawBasin_ConcreteBasin,
//   CleanTank_Tapayan, CleanTank_PlasticDrum, CleanTank_FerrocementTank,
//   CleanTank_UndergroundCistern, SpareJug
// and it is used instead (pivot on the floor, 1 unit = 1 m). The label stays.
// HOW TO EDIT: tiles and jug numbers are in GameBalance > Hub placeholders.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Placeholder Raw Basin, Clean Storage tank and spare jugs in the hub, with labels.</summary>
    public class HubPlaceholders : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<HubPlaceholders>();

        /// <summary>Folder under Resources where artists can drop replacement models.</summary>
        public const string ModelFolder = "HubModels/";

        static readonly Color Water = new Color(0.2f, 0.5f, 0.95f);
        static readonly Color Stone = new Color(0.55f, 0.52f, 0.48f);
        static readonly Color GaugeFrame = new Color(0.15f, 0.15f, 0.15f);

        GameBalance balance;
        GameResources_Manager res;
        MazeBuilder maze;
        UpgradeManager upgrades;

        Transform root;
        bool dirty = true;

        // Raw Basin
        string rawTier;
        FacingLabel rawLabel;
        Transform rawWater;      // scaled in Y with the fill
        float rawInnerHeight;

        // Clean Storage tank
        string cleanTier;
        FacingLabel cleanLabel;
        Transform cleanGaugeFill;

        // Spare jugs
        Transform jugRoot;
        FacingLabel jugLabel;
        readonly List<GameObject> jugs = new List<GameObject>();

        // ------------------------------------------------------------------ Lifecycle

        void Start()
        {
            balance = BalanceProvider.Current;
            res = GameResources_Manager.instance;
            maze = FindAnyObjectByType<MazeBuilder>();
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
            if (maze == null) maze = FindAnyObjectByType<MazeBuilder>();
            if (balance == null || res == null || maze == null || maze.Grid == null) return;

            if (root == null)
            {
                root = new GameObject("HubPlaceholders").transform;
                dirty = true;
            }
            if (dirty) { dirty = false; RebuildIfTierChanged(); }

            UpdateRawBasin();
            UpdateCleanTank();
            UpdateSpareJugs();
        }

        // ------------------------------------------------------------------ Tiers

        static string RawTierName() =>
            Skills.Owns("S8") ? "Concrete Basin" : Skills.Owns("S5") ? "Bigger Basin" : "Basin";

        static string CleanTierName() =>
            Skills.Owns("S6") ? "Underground Cistern" :
            Skills.Owns("S3") ? "Ferrocement Tank" :
            Skills.Owns("S2") ? "Plastic Drum" : "Tapayan";

        void RebuildIfTierChanged()
        {
            string raw = RawTierName();
            if (raw != rawTier) { rawTier = raw; BuildRawBasin(raw); }

            string clean = CleanTierName();
            if (clean != cleanTier) { cleanTier = clean; BuildCleanTank(clean); }

            if (jugRoot == null) BuildJugSpot();
        }

        // ------------------------------------------------------------------ Raw Basin

        void BuildRawBasin(string tier)
        {
            var old = root.Find("RawBasin");
            if (old != null) Destroy(old.gameObject);
            var s = Station("RawBasin", balance.rawBasinTile);
            rawWater = null;

            // Bigger tiers are wider: 15 L -> 1.2 m, 40 L -> 1.8 m, 100 L -> 2.6 m across.
            float side = tier == "Concrete Basin" ? 2.6f : tier == "Bigger Basin" ? 1.8f : 1.2f;
            float h = 0.6f, wall = 0.1f;
            rawInnerHeight = h - wall;

            if (!TryArtistModel(s, "RawBasin_" + tier.Replace(" ", "")))
            {
                Color c = tier == "Concrete Basin" ? new Color(0.7f, 0.7f, 0.7f) : Stone;
                SourceVisuals.Part(s, "Floor", PrimitiveType.Cube, new Vector3(0f, wall / 2f, 0f), new Vector3(side, wall, side), c);
                float half = side / 2f - wall / 2f, wy = h / 2f;
                SourceVisuals.Part(s, "WallN", PrimitiveType.Cube, new Vector3(0f, wy, half), new Vector3(side, h, wall), c);
                SourceVisuals.Part(s, "WallS", PrimitiveType.Cube, new Vector3(0f, wy, -half), new Vector3(side, h, wall), c);
                SourceVisuals.Part(s, "WallE", PrimitiveType.Cube, new Vector3(half, wy, 0f), new Vector3(wall, h, side - 2 * wall), c);
                SourceVisuals.Part(s, "WallW", PrimitiveType.Cube, new Vector3(-half, wy, 0f), new Vector3(wall, h, side - 2 * wall), c);

                // Brown-ish water: it's raw (unsafe) water.
                var w = SourceVisuals.Part(s, "Water", PrimitiveType.Cube, new Vector3(0f, wall, 0f),
                    new Vector3(side - 2 * wall, 1f, side - 2 * wall), new Color(0.45f, 0.38f, 0.25f));
                rawWater = w.transform;
            }
            rawLabel = SourceVisuals.Label(s, balance.sourceLabelHeight - 0.8f, balance.sourceLabelFontSize);
        }

        void UpdateRawBasin()
        {
            if (rawLabel == null) return;
            float amount = res.rawBasin, max = Mathf.Max(0.01f, res.rawBasin_Max);
            float fill = Mathf.Clamp01(amount / max);

            if (rawWater != null)
            {
                // Grow up from the basin floor; hidden when empty.
                float hgt = Mathf.Max(0.001f, fill * rawInnerHeight);
                var sc = rawWater.localScale;
                rawWater.localScale = new Vector3(sc.x, hgt, sc.z);
                var p = rawWater.localPosition;
                rawWater.localPosition = new Vector3(p.x, 0.1f + hgt / 2f, p.z);
                rawWater.gameObject.SetActive(amount > 0.0001f);
            }

            string full = amount >= max - 0.0001f ? "  FULL" : "";
            rawLabel.Text = $"Raw Basin ({rawTier}){full}\n{res.FormatWater(amount)} / {res.FormatWater(max)}";
        }

        // ------------------------------------------------------------------ Clean Storage tank

        void BuildCleanTank(string tier)
        {
            var old = root.Find("CleanTank");
            if (old != null) Destroy(old.gameObject);
            var s = Station("CleanTank", balance.cleanTankTile);
            cleanGaugeFill = null;

            float gaugeX;
            if (!TryArtistModel(s, "CleanTank_" + tier.Replace(" ", "")))
            {
                switch (tier)
                {
                    case "Plastic Drum": // covered, blue
                        SourceVisuals.Part(s, "Drum", PrimitiveType.Cylinder, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 0.65f, 1f), new Color(0.15f, 0.35f, 0.75f));
                        SourceVisuals.Part(s, "Lid", PrimitiveType.Cylinder, new Vector3(0f, 1.32f, 0f), new Vector3(1.02f, 0.02f, 1.02f), new Color(0.1f, 0.25f, 0.55f));
                        gaugeX = 0.9f;
                        break;
                    case "Ferrocement Tank": // big, grey, covered
                        SourceVisuals.Part(s, "Tank", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.9f, 2f), new Color(0.62f, 0.62f, 0.6f));
                        SourceVisuals.Part(s, "Roof", PrimitiveType.Cylinder, new Vector3(0f, 1.85f, 0f), new Vector3(2.05f, 0.05f, 2.05f), new Color(0.45f, 0.45f, 0.43f));
                        gaugeX = 1.4f;
                        break;
                    case "Underground Cistern": // only a slab and a hatch above ground
                        SourceVisuals.Part(s, "Slab", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(2.6f, 0.2f, 2.6f), new Color(0.6f, 0.6f, 0.58f));
                        SourceVisuals.Part(s, "Hatch", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.8f, 0.1f, 0.8f), new Color(0.3f, 0.3f, 0.32f));
                        gaugeX = 1.7f;
                        break;
                    default: // Tapayan: clay jar, open top
                        SourceVisuals.Part(s, "Jar", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 1f, 0.9f), new Color(0.62f, 0.36f, 0.2f));
                        SourceVisuals.Part(s, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(0.45f, 0.1f, 0.45f), new Color(0.55f, 0.3f, 0.17f));
                        gaugeX = 0.8f;
                        break;
                }
            }
            else gaugeX = 1.5f;

            // A level gauge beside the tank: dark frame, blue fill that rises.
            SourceVisuals.Part(s, "GaugeFrame", PrimitiveType.Cube, new Vector3(gaugeX, 0.75f, 0f), new Vector3(0.18f, 1.5f, 0.18f), GaugeFrame);
            var fill = SourceVisuals.Part(s, "GaugeFill", PrimitiveType.Cube, new Vector3(gaugeX, 0f, 0f), new Vector3(0.2f, 1f, 0.2f), Water);
            cleanGaugeFill = fill.transform;

            cleanLabel = SourceVisuals.Label(s, balance.sourceLabelHeight - 0.5f, balance.sourceLabelFontSize);
        }

        void UpdateCleanTank()
        {
            if (cleanLabel == null) return;
            float amount = res.cleanStorage, max = Mathf.Max(0.01f, res.cleanStorage_Max);
            float fill = Mathf.Clamp01(amount / max);

            if (cleanGaugeFill != null)
            {
                float hgt = Mathf.Max(0.001f, fill * 1.5f);
                var p = cleanGaugeFill.localPosition;
                cleanGaugeFill.localScale = new Vector3(0.2f, hgt, 0.2f);
                cleanGaugeFill.localPosition = new Vector3(p.x, hgt / 2f, p.z);
                cleanGaugeFill.gameObject.SetActive(amount > 0.0001f);
            }

            string full = amount >= max - 0.0001f ? "  FULL" : "";
            cleanLabel.Text = $"{cleanTier} (Clean Storage){full}\n{res.FormatWater(amount)} / {res.FormatWater(max)}";
        }

        // ------------------------------------------------------------------ Spare jugs

        void BuildJugSpot()
        {
            var s = Station("SpareJugs", balance.spareJugsTile);
            jugRoot = s;
            jugLabel = SourceVisuals.Label(s, 1.8f, balance.sourceLabelFontSize);
        }

        void UpdateSpareJugs()
        {
            if (jugRoot == null) return;
            float litres = res.spareJugs;
            int want = litres > 0.0001f
                ? Mathf.Clamp(Mathf.CeilToInt(litres / balance.litresPerSpareJug), 1, balance.maxSpareJugModels)
                : 0;

            while (jugs.Count < want) jugs.Add(MakeJug(jugs.Count));
            while (jugs.Count > want)
            {
                Destroy(jugs[jugs.Count - 1]);
                jugs.RemoveAt(jugs.Count - 1);
            }

            jugLabel.Text = want > 0 ? $"Spare jugs: {res.FormatWater(litres)}\n(pours into Clean Storage when there's room)" : "";
        }

        GameObject MakeJug(int i)
        {
            // Rows of 4, 0.5 m apart, centred on the tile.
            float x = (i % 4 - 1.5f) * 0.5f, z = (i / 4) * 0.5f;
            var holder = new GameObject($"Jug{i + 1}").transform;
            holder.SetParent(jugRoot, false);
            holder.localPosition = new Vector3(x, 0f, z);
            if (!TryArtistModel(holder, "SpareJug"))
            {
                SourceVisuals.Part(holder, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.25f, 0.35f), new Color(0.85f, 0.85f, 0.8f));
                SourceVisuals.Part(holder, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.53f, 0f), new Vector3(0.12f, 0.04f, 0.12f), Water);
            }
            return holder.gameObject;
        }

        // ------------------------------------------------------------------ Helpers

        // A parent object standing on a hub tile (offset from the centre in tiles).
        Transform Station(string name, Vector2Int offset)
        {
            var c = maze.Grid.Centre;
            int x = c.x + offset.x, y = c.y + offset.y;
            if (!maze.Grid.IsWalkable(x, y))
                Debug.LogWarning($"[HubPlaceholders] {name}: tile ({x},{y}) is a wall. Change it in GameBalance > Hub placeholders.", this);
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = maze.GridToWorld(x, y);
            return t;
        }

        // Uses an artist's prefab from Resources/HubModels if there is one. Its colliders are
        // kept off so the base stays walkable, like the gray boxes.
        static bool TryArtistModel(Transform parent, string modelName)
        {
            var prefab = Resources.Load<GameObject>(ModelFolder + modelName);
            if (prefab == null) return false;
            var go = Instantiate(prefab, parent, false);
            go.name = modelName;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            return true;
        }
    }
}
