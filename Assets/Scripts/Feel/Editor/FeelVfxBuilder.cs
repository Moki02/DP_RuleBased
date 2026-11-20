// ============================================================================
// FeelVfxBuilder.cs   (Editor only)
// Makes the game-feel effects in Assets/VFX:
//   Effects/VFX_*.prefab      copies of Synty FX, made cheap for the web build (fewer
//                             particles, no lights, no shadows, unlit URP materials), plus a
//                             few new ones (pour stream). Synty's own files are never changed.
//   Materials/M_VFX_*.mat     URP "Particles/Unlit" copies of the Synty particle materials
//                             that used old Built-in shaders (keeps Synty's textures).
//   Textures/                 the vignette and ring images for the UI overlays.
// Used by FeelLabBuilder (Salok > Feel > Build FeelLab). Safe to run again: anything that
// already exists is left alone, so artists' changes are kept. Delete a file to rebuild it.
//
// DESIGN RULES: none (tooling). Web build: Particle System only (no VFX Graph), Max
// Particles set on every system, no lights or soft particles.
//
// HOW TO USE: Salok > Feel > Build FeelLab.
// HOW TO EDIT: the list of effects is Specs below (source prefab, particle cap, one-shot or
// loop, burst size, tint).
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastDrop.Feel.EditorTools
{
    /// <summary>Builds the VFX prefabs, materials and textures in Assets/VFX.</summary>
    public static class FeelVfxBuilder
    {
        public const string VfxFolder = "Assets/VFX";
        public const string EffectsFolder = VfxFolder + "/Effects";
        public const string MaterialsFolder = VfxFolder + "/Materials";
        public const string TexturesFolder = VfxFolder + "/Textures";
        public const string VignettePath = TexturesFolder + "/T_Vignette.png";
        public const string RingPath = TexturesFolder + "/T_Ring.png";
        public const string DotPath = TexturesFolder + "/T_SoftDot.png";

        const string Synty = "Assets/Prefabs/";
        static readonly Color Water = new Color(0.45f, 0.78f, 1f);

        /// <summary>One Synty effect to copy and how to change it.</summary>
        public class Spec
        {
            public string name, source, label;
            public int maxParticles;
            public bool loop;           // false = one-shot (played by FeelPool, returns when done)
            public int burst;           // one-shots: particles in the single burst (0 = keep emission)
            public float duration;      // one-shots: system duration (0 = keep)
            public Color? tint;
            public float scale = 1f;
            public float shake;         // FeelLab only: shake when played
        }

        /// <summary>Every effect copied from Synty.</summary>
        public static readonly Spec[] Specs =
        {
            new Spec { name = "VFX_Rain", label = "Rain (storm)", source = "Generic_Polygon/FX/FX_Rain_01", maxParticles = 800, loop = true },
            new Spec { name = "VFX_SandStreaks", label = "Blowing sand", source = "Egypt_Polygon/FX/FX_Sand_Streaks_01", maxParticles = 60, loop = true },
            new Spec { name = "VFX_DustBlowing", label = "Blowing dust (heatwave)", source = "Wasteland_Polygon/FX/FX_Dust_Blowing_01", maxParticles = 150, loop = true, tint = new Color(1f, 0.85f, 0.65f) },
            new Spec { name = "VFX_DustPuff", label = "Gate / door dust puff", source = "Dungeon_Polygon/FX/FX_Dust_Large_01_Soft", maxParticles = 40, burst = 25, duration = 1f, tint = new Color(0.85f, 0.75f, 0.6f), shake = 0.25f },
            new Spec { name = "VFX_Zone2Dust", label = "Zone 2 turning dust", source = "Wasteland_Polygon/FX/FX_Dust_Storm_01", maxParticles = 150, duration = 3f, tint = new Color(0.9f, 0.8f, 0.65f), shake = 0.7f },
            new Spec { name = "VFX_FootstepDust", label = "Footstep dust", source = "Wasteland_Polygon/FX/FX_Wheel_Dust_02", maxParticles = 12, burst = 5, duration = 0.3f, scale = 0.5f, tint = new Color(0.9f, 0.8f, 0.65f) },
            new Spec { name = "VFX_WaterSplash", label = "Water splash", source = "Generic_Polygon/FX/FX_Waterfall_Foam_01", maxParticles = 40, burst = 20, duration = 0.5f, tint = new Color(0.8f, 0.92f, 1f) },
            new Spec { name = "VFX_Steam", label = "Boiling steam", source = "Wasteland_Polygon/FX/FX_Smoke_Small_01", maxParticles = 40, loop = true, tint = new Color(1f, 1f, 1f, 0.6f) },
            new Spec { name = "VFX_WaterSparkle", label = "Water banked sparkle", source = "Dungeon_Polygon/FX/FX_Sparkles_01", maxParticles = 30, burst = 20, duration = 0.8f, tint = Water },
            new Spec { name = "VFX_RewardRing", label = "Reward pop (ring)", source = "Dungeon_Polygon/FX/FX_Ring_01", maxParticles = 4, burst = 1, duration = 0.8f, tint = Water },
            new Spec { name = "VFX_SickFlies", label = "Sick (flies)", source = "Wasteland_Polygon/FX/FX_Flies_01", maxParticles = 12, loop = true, tint = new Color(0.7f, 0.9f, 0.4f) },
        };

        /// <summary>Names of the effects built from scratch (not Synty copies).</summary>
        public const string PourStreamName = "VFX_PourStream";
        public const string RewardPopName = "VFX_RewardPop";

        /// <summary>Builds everything that is missing. Returns a line per thing made or skipped.</summary>
        public static List<string> BuildAll()
        {
            var log = new List<string>();
            EnsureFolder(EffectsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);

            MakeTexture(VignettePath, 256, (x, y) =>
            {
                // 0 in the middle, rising towards the edges and corners.
                float dx = x - 0.5f, dy = y - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
            }, log);
            MakeTexture(RingPath, 128, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f; // 0 centre, 1 edge
                return Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.12f);
            }, log);
            MakeTexture(DotPath, 64, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
                return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
            }, log);

            foreach (var spec in Specs) CopySynty(spec, log);
            MakePourStream(log);
            MakeRewardPop(log);
            return log;
        }

        /// <summary>Path of an effect prefab by name.</summary>
        public static string EffectPath(string name) => EffectsFolder + "/" + name + ".prefab";

        // ---------------------------------------------------------------- Synty copies

        static void CopySynty(Spec spec, List<string> log)
        {
            string dst = EffectPath(spec.name);
            if (File.Exists(dst)) { log.Add("kept   " + dst); return; }
            string src = Synty + spec.source + ".prefab";
            if (!File.Exists(src)) { log.Add("MISSING Synty source " + src); return; }

            // Opened in its own hidden scene, so no open scene is touched.
            var root = PrefabUtility.LoadPrefabContents(src);
            try
            {
                root.name = spec.name;
                root.transform.localPosition = Vector3.zero;
                root.transform.localScale *= spec.scale;
                MakeCheap(root, spec);
                if (root.GetComponent<PooledEffect>() == null) root.AddComponent<PooledEffect>();
                PrefabUtility.SaveAsPrefabAsset(root, dst);
                log.Add("made   " + dst + "  (from " + src + ")");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MakeCheap(GameObject root, Spec spec)
        {
            foreach (var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (spec.maxParticles > 0 && main.maxParticles > spec.maxParticles) main.maxParticles = spec.maxParticles;
                main.playOnAwake = spec.loop;
                main.stopAction = ParticleSystemStopAction.None;
                if (!spec.loop)
                {
                    main.loop = false;
                    if (spec.duration > 0f) main.duration = spec.duration;
                    if (spec.burst > 0)
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = 0f;
                        emission.rateOverDistance = 0f;
                        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.burst) });
                    }
                }
                if (spec.tint.HasValue) main.startColor = Tint(main.startColor, spec.tint.Value);

                var lights = ps.lights;
                lights.enabled = false;

                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = ToUrpUnlit(mats[i]);
                    r.sharedMaterials = mats;
                }
            }
        }

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color tint)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(g.color * tint);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(g.colorMin * tint, g.colorMax * tint);
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Tint(g.gradient, tint));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Tint(g.gradientMin, tint), Tint(g.gradientMax, tint));
                default:
                    return new ParticleSystem.MinMaxGradient(tint);
            }
        }

        static Gradient Tint(Gradient g, Color tint)
        {
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color *= tint;
            var alphas = g.alphaKeys;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= tint.a;
            var copy = new Gradient { mode = g.mode };
            copy.SetKeys(keys, alphas);
            return copy;
        }

        /// <summary>
        /// Returns a URP Particles/Unlit version of a material that uses an old Built-in
        /// particle shader (made once in Assets/VFX/Materials). URP / Shader Graph materials are
        /// returned unchanged.
        /// </summary>
        public static Material ToUrpUnlit(Material src)
        {
            if (src == null || src.shader == null) return src;
            string shaderPath = AssetDatabase.GetAssetPath(src.shader);
            bool builtIn = string.IsNullOrEmpty(shaderPath) || shaderPath.StartsWith("Resources/");
            if (!builtIn || src.shader.name.StartsWith("Universal Render Pipeline")) return src;

            string path = MaterialsFolder + "/M_VFX_" + src.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return src;
            var m = new Material(shader) { name = "M_VFX_" + src.name };

            Texture tex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            m.SetTexture("_BaseMap", tex);

            Color c = Color.white;
            if (src.HasProperty("_TintColor"))
            {
                // Old particle shaders double the tint (grey 0.5 = normal).
                c = src.GetColor("_TintColor") * 2f;
                c.r = Mathf.Min(c.r, 1f); c.g = Mathf.Min(c.g, 1f); c.b = Mathf.Min(c.b, 1f); c.a = Mathf.Min(c.a, 1f);
            }
            else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
            m.SetColor("_BaseColor", c);

            bool additive = src.shader.name.Contains("Additive") ||
                            (src.HasProperty("_DstBlend") && Mathf.RoundToInt(src.GetFloat("_DstBlend")) == (int)BlendMode.One);
            m.SetFloat("_Surface", 1f);                // transparent
            m.SetFloat("_Blend", additive ? 2f : 0f);  // 2 = additive, 0 = alpha
            m.SetFloat("_SoftParticlesEnabled", 0f);   // web build: no soft particles
            m.SetFloat("_CameraFadingEnabled", 0f);
            m.SetFloat("_DistortionEnabled", 0f);
            BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);

            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- new effects

        static Material UnlitMaterial(string name, Texture tex, Color color, bool additive)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SoftParticlesEnabled", 0f);
            BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // A thin falling stream of water: stretched droplets pulled down by gravity.
        static void MakePourStream(List<string> log)
        {
            string dst = EffectPath(PourStreamName);
            if (File.Exists(dst)) { log.Add("kept   " + dst); return; }

            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
            var mat = UnlitMaterial("M_VFX_PourStream", dot, new Color(0.7f, 0.88f, 1f, 0.8f), false);

            var go = new GameObject(PourStreamName);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // emit straight down
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = true;
            main.maxParticles = 80;
            main.startLifetime = 0.55f;
            main.startSpeed = 1.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 70f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 2f;
            shape.radius = 0.02f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            r.velocityScale = 0.05f;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;

            go.AddComponent<PooledEffect>();
            PrefabUtility.SaveAsPrefabAsset(go, dst);
            Object.DestroyImmediate(go);
            log.Add("made   " + dst);
        }

        // Reward pop: the blue ring plus the water sparkle together.
        static void MakeRewardPop(List<string> log)
        {
            string dst = EffectPath(RewardPopName);
            if (File.Exists(dst)) { log.Add("kept   " + dst); return; }
            var ring = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath("VFX_RewardRing"));
            var sparkle = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath("VFX_WaterSparkle"));
            if (ring == null || sparkle == null) { log.Add("SKIPPED " + dst + " (ring or sparkle missing)"); return; }

            var go = new GameObject(RewardPopName);
            foreach (var part in new[] { ring, sparkle })
            {
                var child = Object.Instantiate(part, go.transform, false); // plain copy, not linked
                child.name = part.name;
                child.transform.localPosition = Vector3.zero;
                var tag = child.GetComponent<PooledEffect>();
                if (tag != null) Object.DestroyImmediate(tag); // the parent's PooledEffect handles both
            }
            go.AddComponent<PooledEffect>();
            PrefabUtility.SaveAsPrefabAsset(go, dst);
            Object.DestroyImmediate(go);
            log.Add("made   " + dst);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Saves a white texture whose alpha is alpha(x, y) (0..1 coords), imported as a UI sprite.</summary>
        static void MakeTexture(string path, int size, System.Func<float, float, float> alpha, List<string> log)
        {
            if (File.Exists(path)) { log.Add("kept   " + path); return; }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = size;
            imp.SaveAndReimport();
            log.Add("made   " + path);
        }

        /// <summary>Makes a folder (and its parents) if missing.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
