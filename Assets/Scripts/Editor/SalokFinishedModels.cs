// ============================================================================
// SalokFinishedModels.cs   (Editor only)
// Puts the artists' finished models (Assets/Art/Models/Finished Model) into the game.
//
// MENU: Salok > Build > 1. Set up finished models
//   - Makes a prefab in Assets/Resources/HubModels for each model, under the name the game
//     already looks for (HubPlaceholders, WaterSources, BiosandFilter):
//       RawBasin.fbx            -> RawBasin_Basin           (Raw Basin, 15 L)
//       Basin_Bigger.fbx        -> RawBasin_BiggerBasin     (S5)
//       Basin_Concrete.fbx      -> RawBasin_ConcreteBasin   (S8)
//       Storage_Tapayan.fbx     -> CleanTank_Tapayan
//       Storage_Ferrocement.fbx -> CleanTank_FerrocementTank
//       Storage_Underground.fbx -> CleanTank_UndergroundCistern
//       WaterJug.fbx            -> SpareJug
//       Raincatcher_Normal / _Wider / _Bigger.fbx -> RainCatcher_Normal / _Wider / _Bigger
//       SolarStill.fbx, DeepWell.fbx, BiosandFilter.fbx -> SolarStill, DeepWell, BiosandFilter
//     (There is no Plastic Drum model yet: that tier keeps its grey box.)
//     Each prefab stands the model on the floor (pivot at the bottom middle). The size is the
//     artists' own (1 unit = 1 m); the Console lists every model's size so odd ones are easy to spot.
//   - Lock Basin: adds LockBasin.fbx to the Lock Basin prefab the gates use
//     (Assets/Prefabs/Game Prefab/PlaceHolder/Models/Lock Basin.prefab) as a child called
//     "FinishedModel", lined up with the old cylinder, and hides the cylinder. Its colliders and
//     UI are not touched.
//   Running it again remakes the HubModels prefabs (open them to fine-tune position / scale
//   after the last run) and leaves the Lock Basin alone if it already has its model.
//
// NOT DONE HERE: Valve.fbx (no game object uses a separate valve model yet).
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Turns the finished FBX models into the prefabs the game loads.</summary>
    public static class SalokFinishedModels
    {
        const string ModelDir = "Assets/Art/Models/Finished Model/";
        const string OutDir = "Assets/Resources/HubModels";
        const string LockBasinPrefab = "Assets/Prefabs/Game Prefab/PlaceHolder/Models/Lock Basin.prefab";

        static readonly (string fbx, string prefab)[] Map =
        {
            ("RawBasin", "RawBasin_Basin"),
            ("Basin_Bigger", "RawBasin_BiggerBasin"),
            ("Basin_Concrete", "RawBasin_ConcreteBasin"),
            ("Storage_Tapayan", "CleanTank_Tapayan"),
            ("Storage_Ferrocement", "CleanTank_FerrocementTank"),
            ("Storage_Underground", "CleanTank_UndergroundCistern"),
            ("WaterJug", "SpareJug"),
            ("Raincatcher_Normal", "RainCatcher_Normal"),
            ("Raincatcher_Wider", "RainCatcher_Wider"),
            ("Raincatcher_Bigger", "RainCatcher_Bigger"),
            ("SolarStill", "SolarStill"),
            ("DeepWell", "DeepWell"),
            ("BiosandFilter", "BiosandFilter"),
        };

        [MenuItem("Salok/Build/1. Set up finished models", priority = 1)]
        public static void SetUp()
        {
            EnsureFolder(OutDir);
            int made = 0;
            foreach (var (fbx, prefabName) in Map)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + fbx + ".fbx");
                if (model == null) { Debug.LogWarning($"[FinishedModels] Missing {ModelDir}{fbx}.fbx"); continue; }

                var root = new GameObject(prefabName);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = fbx;
                var b = Bounds(inst);
                if (b.size != Vector3.zero)
                    inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);   // stand it on the floor, centred
                foreach (var col in root.GetComponentsInChildren<Collider>(true)) col.enabled = false;

                PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/{prefabName}.prefab");
                Object.DestroyImmediate(root);
                Debug.Log($"[FinishedModels] {prefabName} <- {fbx}.fbx  size {b.size.x:0.##} x {b.size.y:0.##} x {b.size.z:0.##} m");
                made++;
            }

            bool basin = AddLockBasinModel();
            AssetDatabase.SaveAssets();
            Debug.Log($"[FinishedModels] {made} hub / source prefabs in {OutDir}." +
                      (basin ? " Lock Basin prefab now uses LockBasin.fbx." : "") +
                      " Press Play to see them in the hub (sources show once built).");
        }

        static bool AddLockBasinModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "LockBasin.fbx");
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(LockBasinPrefab) == null)
            {
                Debug.LogWarning("[FinishedModels] LockBasin.fbx or the Lock Basin prefab is missing; skipped.");
                return false;
            }
            var root = PrefabUtility.LoadPrefabContents(LockBasinPrefab);
            try
            {
                if (root.transform.Find("FinishedModel") != null)
                {
                    Debug.Log("[FinishedModels] Lock Basin already has its FinishedModel; left as it is.");
                    return false;
                }
                // The old look: mesh renderers that aren't part of the basin's UI (canvas).
                Bounds old = default;
                bool any = false;
                var hide = new System.Collections.Generic.List<MeshRenderer>();
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.GetComponentInParent<Canvas>(true) != null) continue;
                    if (!any) { old = r.bounds; any = true; } else old.Encapsulate(r.bounds);
                    hide.Add(r);
                }
                foreach (var r in hide) r.enabled = false;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "FinishedModel";
                foreach (var col in inst.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                var b = Bounds(inst);
                if (any && b.size != Vector3.zero)
                    inst.transform.position += new Vector3(old.center.x - b.center.x, old.min.y - b.min.y, old.center.z - b.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, LockBasinPrefab);
                Debug.Log($"[FinishedModels] Lock Basin: model size {b.size.x:0.##} x {b.size.y:0.##} x {b.size.z:0.##} m" +
                          (any ? $" (old cylinder {old.size.x:0.##} x {old.size.y:0.##} x {old.size.z:0.##} m)." : "."));
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;   // world space (the prefab roots made here sit at the origin, unrotated)
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
