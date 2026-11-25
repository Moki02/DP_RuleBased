// ============================================================================
// HubSceneBuilder.cs
// Makes the "HubObjects" object (HubSceneObjects) with every hub model placed: all Raw
// Basin and Clean Storage tiers, the spare jugs, the water sources, the Biosand Filter and
// their labels. Used by the editor menu Salok > Build > 2. Place hub objects in scene (so
// they are saved in the scene), and at Play only when a game scene has no HubObjects yet.
//
// Models come from Assets/Resources/HubModels (same names as before, see HubPlaceholders).
// In the editor they are placed as prefab instances, so an artist's change still shows.
// The Clean Storage tiers go on the team's Clean Storage tap prefab: Tapayan first, the
// prefab's own two drums as the Plastic Drum, then the Ferrocement Tank and the
// Underground Cistern. Colliders on the models are turned off (the base stays walkable).
//
// DESIGN RULES: R3.5 (spare jugs), R5.3 (each tier visible).
// HOW TO EDIT: tiles are in GameBalance > Hub placeholders / Water sources (hub). After the
// objects are in the scene, move them by hand; the menu only rebuilds when you ask it to.
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheLastDrop
{
    /// <summary>Builds the hub's scene objects (editor menu, or Play as a fallback).</summary>
    public static class HubSceneBuilder
    {
        const string MaterialFolder = "HubModels/Materials/";
        static readonly Color GaugeFrameColour = new Color(0.15f, 0.15f, 0.15f);
        static readonly Color GaugeFillColour = new Color(0.2f, 0.5f, 0.95f);

        /// <summary>Makes a new HubObjects in the active scene. Needs the maze grid loaded.</summary>
        public static HubSceneObjects Build(MazeBuilder maze, GameBalance b)
        {
            var root = new GameObject(HubSceneObjects.RootName).transform;
            var hub = root.gameObject.AddComponent<HubSceneObjects>();
            float labelH = b.sourceLabelHeight, font = b.sourceLabelFontSize;

            // Raw Basin: three tiers on one tile.
            var raw = Station(root, maze, "RawBasin", b.rawBasinTile);
            hub.rawBasinTiers = new[]
            {
                ModelTier(raw, "Basin", "RawBasin_Basin"),
                ModelTier(raw, "Bigger Basin", "RawBasin_BiggerBasin"),
                ModelTier(raw, "Concrete Basin", "RawBasin_ConcreteBasin"),
            };
            hub.rawBasinLabel = SourceVisuals.Label(raw, labelH - 0.8f, font);

            // Clean Storage: on the tap prefab (it keeps the drink / refill points and its name text).
            var tap = Object.FindAnyObjectByType<HubTap_Manager>(FindObjectsInactive.Include);
            if (tap != null) BuildCleanStorage(hub, tap.transform);
            else Debug.LogWarning("[HubSceneBuilder] No Clean Storage (HubTap_Manager) in the scene: its tiers were not made.");

            // Spare jugs: rows of 4, 0.5 m apart, all hidden until there are spare litres.
            var jugs = Station(root, maze, "SpareJugs", b.spareJugsTile);
            var list = new List<GameObject>();
            for (int i = 0; i < Mathf.Max(1, b.maxSpareJugModels); i++)
            {
                var holder = new GameObject($"Jug{i + 1}").transform;
                holder.SetParent(jugs, false);
                holder.localPosition = new Vector3((i % 4 - 1.5f) * 0.5f, 0f, (i / 4) * 0.5f);
                Model(holder, "SpareJug");
                holder.gameObject.SetActive(false);
                list.Add(holder.gameObject);
            }
            hub.spareJugs = list.ToArray();
            hub.spareJugsLabel = SourceVisuals.Label(jugs, 1.8f, font);

            // Water sources: every one placed, hidden until bought.
            var rain = Station(root, maze, "RainCatcher", b.rainCatcherTile);
            hub.rainCatcherTiers = new[]
            {
                ModelTier(rain, "Normal", "RainCatcher_Normal"),
                ModelTier(rain, "Wider", "RainCatcher_Wider"),
                ModelTier(rain, "Bigger", "RainCatcher_Bigger"),
            };
            hub.rainCatcherLabel = SourceVisuals.Label(rain, labelH, font);

            var still = Station(root, maze, "SolarStill", b.solarStillTile);
            var s1 = Model(still, "SolarStill", "SolarStill1");
            var s2 = Model(still, "SolarStill", "SolarStill2");
            if (s2 != null) s2.transform.localPosition = new Vector3(1.6f, 0f, 0f);
            hub.solarStills = new[] { s1, s2 };
            hub.solarStillLabel = SourceVisuals.Label(still, labelH, font);

            var well = Station(root, maze, "DeepWell", b.deepWellTile);
            hub.deepWell = Model(well, "DeepWell");
            hub.deepWellLabel = SourceVisuals.Label(well, labelH, font);

            // Biosand Filter: beside the boiling pot.
            var pot = Object.FindAnyObjectByType<HubPurifier_Manager>(FindObjectsInactive.Include);
            if (pot != null)
            {
                var holder = new GameObject("BiosandFilter").transform;
                holder.SetParent(root, false);
                holder.position = pot.transform.position + pot.transform.right * b.biosandSideOffset;
                Model(holder, "BiosandFilter");
                hub.biosandFilter = holder.gameObject;
            }

            // Start as a new game would: lowest tiers, nothing bought.
            HubSceneObjects.ShowTier(hub.rawBasinTiers, "Basin");
            HubSceneObjects.ShowTier(hub.cleanStorageTiers, "Tapayan");
            HubSceneObjects.ShowTier(hub.rainCatcherTiers, null);
            foreach (var go in new[] { s1, s2, hub.deepWell, hub.biosandFilter })
                if (go != null) go.SetActive(false);
            foreach (var l in new[] { hub.rainCatcherLabel, hub.solarStillLabel, hub.deepWellLabel })
                HubSceneObjects.ShowLabel(l, false);
            return hub;
        }

        // ------------------------------------------------------------------ Clean Storage

        static void BuildCleanStorage(HubSceneObjects hub, Transform tap)
        {
            // The prefab's own drum meshes. Each drum ("Drinking Storage", "Refill Storage") also
            // holds its tap point and name text, which must stay on, so only its mesh parts
            // (Storage_Drum, _Lid, _Stand, ...) belong to the Plastic Drum tier.
            var drums = new List<GameObject>();
            foreach (Transform child in tap)
            {
                if (child.name == "StorageTiers") continue;
                if (child.GetComponentInChildren<HubTapPoint_Manager>(true) != null)
                {
                    foreach (Transform part in child)
                        if (IsMeshPart(part)) drums.Add(part.gameObject);
                }
                else if (IsMeshPart(child)) drums.Add(child.gameObject);
            }

            // An unscaled holder, so the tap prefab's scale can't stretch the models.
            var old = tap.Find("StorageTiers");
            if (old != null) DestroyAny(old.gameObject);
            var holder = new GameObject("StorageTiers").transform;
            holder.SetParent(tap, false);
            var ls = tap.lossyScale;
            holder.localScale = new Vector3(1f / NonZero(ls.x), 1f / NonZero(ls.y), 1f / NonZero(ls.z));

            hub.cleanStorageTiers = new[]
            {
                ModelTier(holder, "Tapayan", "CleanTank_Tapayan"),
                new HubSceneObjects.Tier { name = "Plastic Drum", objects = drums.ToArray() },
                ModelTier(holder, "Ferrocement Tank", "CleanTank_FerrocementTank"),
                ModelTier(holder, "Underground Cistern", "CleanTank_UndergroundCistern"),
            };

            // Level gauge beside it: dark frame, blue fill that rises. Real material assets
            // (not the primitive's default one, which shows pink in a URP build).
            var gauge = new GameObject("LevelGauge").transform;
            gauge.SetParent(holder, false);
            gauge.localPosition = new Vector3(1.1f, 0f, 0f);
            SourceVisuals.Part(gauge, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f),
                new Vector3(0.18f, 1.5f, 0.18f), GaugeFrameColour, GaugeMaterial("M_HubGaugeFrame", GaugeFrameColour));
            var fill = SourceVisuals.Part(gauge, "Fill", PrimitiveType.Cube, new Vector3(0f, 0.375f, 0f),
                new Vector3(0.2f, 0.75f, 0.2f), GaugeFillColour, GaugeMaterial("M_HubGaugeFill", GaugeFillColour));
            hub.cleanGaugeFill = fill.transform;
        }

        // A model part: has a mesh, and no text or tap point anywhere under it.
        static bool IsMeshPart(Transform t) =>
            t.GetComponentInChildren<MeshRenderer>(true) != null &&
            t.GetComponentInChildren<TMP_Text>(true) == null &&
            t.GetComponentInChildren<HubTapPoint_Manager>(true) == null;

        static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

        // A material asset in Resources/HubModels/Materials (made in the editor if missing).
        static Material GaugeMaterial(string name, Color colour)
        {
            var mat = Resources.Load<Material>(MaterialFolder + name);
#if UNITY_EDITOR
            if (mat == null && !Application.isPlaying)
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                var shader = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
                else mat.color = colour;
                const string dir = "Assets/Resources/HubModels/Materials";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources/HubModels", "Materials");
                AssetDatabase.CreateAsset(mat, $"{dir}/{name}.mat");
            }
#endif
            return mat;
        }

        // ------------------------------------------------------------------ Helpers

        static HubSceneObjects.Tier ModelTier(Transform parent, string tierName, string model)
        {
            var go = Model(parent, model, tierName.Replace(" ", ""));
            return new HubSceneObjects.Tier { name = tierName, objects = go != null ? new[] { go } : new GameObject[0] };
        }

        // A parent object standing on a hub tile (offset from the centre in tiles).
        static Transform Station(Transform root, MazeBuilder maze, string name, Vector2Int offset)
        {
            var c = maze.Grid.Centre;
            int x = c.x + offset.x, y = c.y + offset.y;
            if (!maze.Grid.IsWalkable(x, y))
                Debug.LogWarning($"[HubSceneBuilder] {name}: tile ({x},{y}) is a wall. Move it by hand or change its tile in GameBalance.");
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = maze.GridToWorld(x, y);
            return t;
        }

        /// <summary>
        /// The Resources/HubModels prefab <paramref name="model"/> under <paramref name="parent"/>
        /// (a prefab instance in the editor), colliders off. Null (with a warning) if there is none.
        /// </summary>
        public static GameObject Model(Transform parent, string model, string objectName = null)
        {
            var prefab = Resources.Load<GameObject>(HubPlaceholders.ModelFolder + model);
            if (prefab == null)
            {
                Debug.LogWarning($"[HubSceneBuilder] No model Resources/{HubPlaceholders.ModelFolder}{model}. " +
                                 "Run Salok > Build > 1. Set up finished models.");
                return null;
            }
            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            else
#endif
                go = Object.Instantiate(prefab, parent, false);
            go.name = objectName ?? model;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            return go;
        }

        static void DestroyAny(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
