// ============================================================================
// TutorialOutline.cs
// The pulsing outline the tutorial draws around the object it is teaching (the pump, the
// pot, the hub gates ...). Also pulses HUD elements (the HYD bar and the canteen).
//
// HOW IT WORKS (screen-space, mask based): URP has no outline of its own, and Synty's
// flat-shaded meshes have split normals, so the usual "inverted hull" cracks at the corners.
// Instead, while something is highlighted:
//   1. the highlighted meshes are drawn in plain white into a small black-and-white mask
//      (their silhouette; drawn through walls, so the player can find the object);
//   2. a full-screen pass draws the outline colour on every pixel just OUTSIDE that
//      silhouette, a few pixels wide, blended over the picture.
// The width and colour pulse over time. Nothing is drawn, and nothing costs anything, when
// nothing is highlighted: the pass is only added to the camera while there are targets.
// It needs no Renderer Feature and no change to the URP assets (the pass is added from code
// each frame), works on WebGL2, and needs no Read/Write on the meshes.
// The two shaders are in Assets/Resources/Tutorial (SalokOutlineMask, SalokOutlineComposite).
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: TutorialDirector adds it and calls SetTargets / SetHudTargets / Clear.
// Any script can use it too: TutorialOutline.Instance.SetTargets(listOfGameObjects).
// HOW TO EDIT: colour, widths and pulse speed are the Inspector fields below (on Systems,
// during Play; copy them to keep them).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TheLastDrop
{
    /// <summary>Draws a pulsing outline around highlighted objects and pulses highlighted HUD elements.</summary>
    public class TutorialOutline : MonoBehaviour
    {
        /// <summary>The one in this scene (null if none).</summary>
        public static TutorialOutline Instance { get; private set; }

        [Header("Look")]
        [Tooltip("Outline colour (its alpha is the strongest the outline gets).")]
        public Color color = new Color(1f, 1f, 1f, 0.95f);
        [Tooltip("Outline width at its thinnest, in pixels at 1080p (scaled with the screen height).")]
        [Min(0.5f)] public float minWidth = 2f;
        [Tooltip("Outline width at its widest, in pixels at 1080p.")]
        [Min(0.5f)] public float maxWidth = 5f;
        [Tooltip("Lowest alpha in the pulse (0-1, times the colour's alpha).")]
        [Range(0f, 1f)] public float minAlpha = 0.45f;
        [Tooltip("Pulses per second.")]
        [Min(0.05f)] public float pulsesPerSecond = 0.9f;

        [Header("HUD pulse")]
        [Tooltip("How much a highlighted HUD element grows at the top of each pulse (0.08 = 8%).")]
        [Range(0f, 0.5f)] public float hudPulseScale = 0.08f;

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<int> subMeshes = new List<int>();
        readonly List<RectTransform> hud = new List<RectTransform>();
        readonly List<Vector3> hudScales = new List<Vector3>();
        static readonly List<Renderer> scratch = new List<Renderer>();

        Material maskMaterial, compositeMaterial;
        OutlinePass pass;
        bool shadersMissingLogged;

        /// <summary>True while at least one object is outlined.</summary>
        public bool HasTargets => renderers.Count > 0;

        void Awake()
        {
            Instance = this;
            var maskShader = Resources.Load<Shader>("Tutorial/SalokOutlineMask");
            var compShader = Resources.Load<Shader>("Tutorial/SalokOutlineComposite");
            if (maskShader != null && compShader != null && maskShader.isSupported && compShader.isSupported)
            {
                maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
                compositeMaterial = new Material(compShader) { hideFlags = HideFlags.HideAndDontSave };
                pass = new OutlinePass(this);
            }
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RestoreHud();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (maskMaterial != null) Destroy(maskMaterial);
            if (compositeMaterial != null) Destroy(compositeMaterial);
        }

        // ------------------------------------------------------------------ Targets

        /// <summary>Outlines these objects (and everything under them). Replaces the old targets.</summary>
        public void SetTargets(IList<GameObject> targets)
        {
            renderers.Clear();
            subMeshes.Clear();
            if (targets == null) return;
            for (int t = 0; t < targets.Count; t++)
            {
                var go = targets[t];
                if (go == null) continue;
                scratch.Clear();
                go.GetComponentsInChildren(false, scratch);
                for (int i = 0; i < scratch.Count; i++)
                {
                    var r = scratch[i];
                    // Only meshes: no world-space text labels, particles or lines.
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    if (r.GetComponent<TMPro.TMP_Text>() != null || renderers.Contains(r)) continue;
                    renderers.Add(r);
                    subMeshes.Add(SubMeshCount(r));
                }
            }
            if (renderers.Count > 0 && pass == null && !shadersMissingLogged)
            {
                shadersMissingLogged = true;
                Debug.LogWarning("[TutorialOutline] The outline shaders are missing or not supported " +
                                 "(Assets/Resources/Tutorial/SalokOutline*.shader), so nothing is outlined.", this);
            }
        }

        /// <summary>Pulses these HUD elements (grows and shrinks them). Replaces the old ones.</summary>
        public void SetHudTargets(IList<RectTransform> targets)
        {
            RestoreHud();
            if (targets == null) return;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null || hud.Contains(targets[i])) continue;
                hud.Add(targets[i]);
                hudScales.Add(targets[i].localScale);
            }
        }

        /// <summary>Removes every outline and HUD pulse.</summary>
        public void Clear()
        {
            renderers.Clear();
            subMeshes.Clear();
            RestoreHud();
        }

        void RestoreHud()
        {
            for (int i = 0; i < hud.Count; i++)
                if (hud[i] != null) hud[i].localScale = hudScales[i];
            hud.Clear();
            hudScales.Clear();
        }

        static int SubMeshCount(Renderer r)
        {
            if (r is SkinnedMeshRenderer s) return s.sharedMesh != null ? s.sharedMesh.subMeshCount : 1;
            var f = r.GetComponent<MeshFilter>();
            return f != null && f.sharedMesh != null ? f.sharedMesh.subMeshCount : 1;
        }

        // ------------------------------------------------------------------ Pulse

        /// <summary>0 to 1 and back, pulsesPerSecond times a second (real time, so it pulses in menus too).</summary>
        float Pulse => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsesPerSecond * Mathf.PI * 2f);

        void LateUpdate()
        {
            if (hud.Count == 0) return;
            float s = 1f + hudPulseScale * Pulse;
            for (int i = 0; i < hud.Count; i++)
                if (hud[i] != null) hud[i].localScale = hudScales[i] * s;
        }

        // ------------------------------------------------------------------ Rendering

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (pass == null || renderers.Count == 0) return;
            if (cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;

            float p = Pulse;
            float scale = cam.pixelHeight / 1080f;
            pass.width = Mathf.Max(1f, Mathf.Lerp(minWidth, maxWidth, p) * scale);
            var c = color;
            c.a *= Mathf.Lerp(minAlpha, 1f, p);
            pass.tint = c;
            data.scriptableRenderer.EnqueuePass(pass);
        }

        /// <summary>The URP pass: mask of the highlighted meshes, then the outline over the picture.</summary>
        class OutlinePass : ScriptableRenderPass
        {
            static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
            static readonly int SizeId = Shader.PropertyToID("_MaskSize");

            readonly TutorialOutline owner;
            public float width;
            public Color tint;

            class MaskData
            {
                public List<Renderer> renderers;
                public List<int> subMeshes;
                public Material material;
            }

            class CompositeData
            {
                public TextureHandle mask;
                public Material material;
            }

            public OutlinePass(TutorialOutline owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;

                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.msaaSamples = 1;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm; // R8 isn't a render target on every WebGL device
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SalokOutlineMask", true);

                using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Salok Tutorial Outline Mask", out var data))
                {
                    data.renderers = owner.renderers;
                    data.subMeshes = owner.subMeshes;
                    data.material = owner.maskMaterial;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                        for (int i = 0; i < d.renderers.Count; i++)
                        {
                            var r = d.renderers[i];
                            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                            for (int s = 0; s < d.subMeshes[i]; s++)
                                ctx.cmd.DrawRenderer(r, d.material, s, 0);
                        }
                    });
                }

                var mat = owner.compositeMaterial;
                mat.SetColor(ColorId, tint);
                mat.SetFloat(WidthId, width);
                mat.SetVector(SizeId, new Vector4(desc.width, desc.height, 0f, 0f));

                using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Salok Tutorial Outline", out var data))
                {
                    data.mask = mask;
                    data.material = mat;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.mask, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
                }
            }
        }
    }
}
