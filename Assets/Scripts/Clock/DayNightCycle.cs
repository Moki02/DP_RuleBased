// ============================================================================
// DayNightCycle.cs
// Makes the sky and the light follow the game clock, using the sky dome already in the
// scene (SM_Env_Skydome_01):
//
//   dawn (pink) -> bright day -> orange dusk near the end of daylight (a "lockdown is coming"
//   cue) -> deep blue night with stars during the night lockdown -> dawn.
//
//   - SKY DOME: at Play its material is swapped for a copy using the "Salok/Sky Dome" shader
//     (Resources/Sky), with the same gradient texture, tinted by "Sky Tint" and with stars
//     fading in by "Stars". The artists' Sky_Box_01 material is never changed.
//     The dome follows the camera left/right (not up/down), so its edge is never seen.
//   - SUN: the Directional Light rises in the east, is highest in the south at noon and sets
//     in the west; at night it becomes a dim blue "moonlight" that crosses the sky the same
//     way. Colour and brightness come from "Light Colour" and "Light Intensity".
//   - AMBIENT: the scene's ambient light follows "Ambient" (switched to Trilight at Play).
//
// SKY TIME (the x axis of every gradient and curve below): 0 = dawn, 0.4 = noon,
// 0.8 = night lockdown starts, 1 = next dawn. Daylight is always 0-0.8 and night 0.8-1,
// whatever the real lengths in GameBalance (DayNightMath.SkyTime).
//
// DESIGN RULES: R1.5 (240 s daylight), R8.19 (60 s night lockdown). Look: pending GDD entry
// (sky dome day/night, 2026-11-13).
//
// HOW TO USE: nothing to set up. It adds itself to the game scene (AutoSystems) and finds the
// dome (any object with "Skydome" in its name) and the first Directional Light. Drag others
// into "Dome" / "Sun" if needed.
// HOW TO EDIT: the colours and curves below. This component is added only at Play, so
// Inspector changes made during Play are lost; to keep them, add DayNightCycle to the Systems
// object by hand once and tune it there. TESTING: tick "Override Sky Time" and drag "Sky Time"
// to scrub through the day while playing (or use debug key 6 to speed up the clock).
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastDrop
{
    /// <summary>Sky dome tint, stars, sun/moon and ambient light driven by the game clock.</summary>
    public class DayNightCycle : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => AutoSystems.Register<DayNightCycle>();

        /// <summary>The shader inside a Resources folder.</summary>
        public const string ShaderResource = "Sky/SalokSkyDome";

        [Header("Links (found by themselves when empty)")]
        [Tooltip("The sky dome's renderer. Empty = the first object with \"Skydome\" in its name.")]
        public Renderer dome;
        [Tooltip("The sun. Empty = RenderSettings' sun, else the first Directional Light.")]
        public Light sun;
        [Tooltip("Keep the dome centred on the camera (left/right only), so its edge is never seen.")]
        public bool domeFollowsCamera = true;
        [Tooltip("Drive the scene's ambient light too (switches it to Trilight at Play).")]
        public bool controlAmbient = true;

        [Header("Sky (x = sky time: 0 dawn, 0.4 noon, 0.8 lockdown, 1 dawn)")]
        [Tooltip("Multiplies the dome's gradient. White = the dome as the artists made it.")]
        public Gradient skyTint = DefaultSkyTint();
        [Tooltip("How strong the stars are (0-1).")]
        public AnimationCurve stars = DefaultStars();
        public Color starColour = new Color(1f, 0.97f, 0.9f);
        [Range(0f, 1f)] public float starDensity = 0.06f;
        [Range(0.02f, 0.5f)] public float starSize = 0.16f;
        [Tooltip("More = smaller cells, so more and smaller stars.")]
        [Min(10f)] public float starGrid = 140f;
        [Range(0f, 1f)] public float starTwinkle = 0.35f;

        [Header("Sun and moon")]
        public Gradient lightColour = DefaultLightColour();
        public AnimationCurve lightIntensity = DefaultLightIntensity();
        [Tooltip("Sun height at noon, degrees above the horizon.")]
        [Range(10f, 90f)] public float sunMaxHeight = 65f;
        [Tooltip("Moon height at midnight, degrees above the horizon.")]
        [Range(10f, 90f)] public float moonMaxHeight = 45f;
        [Tooltip("Lowest the sun/moon goes (degrees), so shadows never get endless.")]
        [Range(0f, 30f)] public float lowestHeight = 8f;

        [Header("Ambient")]
        public Gradient ambient = DefaultAmbient();

        [Header("Testing")]
        [Tooltip("Tick to ignore the clock and use Sky Time below.")]
        public bool overrideSkyTime;
        [Range(0f, 1f)] public float skyTime = 0.4f;

        Material skyMaterial;
        bool skyHasTint, skyHasStars;
        string tintProperty;
        float domeHeight;

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int StarAlphaId = Shader.PropertyToID("_StarAlpha");

        // ------------------------------------------------------------------ Setup

        void Start()
        {
            if (dome == null) dome = FindDome();
            if (sun == null) sun = FindSun();
            if (dome == null) Debug.LogWarning("[DayNightCycle] No sky dome found (an object with \"Skydome\" in its name). Only the light will change.", this);
            if (sun == null) Debug.LogWarning("[DayNightCycle] No Directional Light found. Only the sky will change.", this);
            if (dome != null) { SetUpSkyMaterial(); domeHeight = dome.transform.position.y; }
            if (controlAmbient) RenderSettings.ambientMode = AmbientMode.Trilight;
            Apply(CurrentSkyTime());
        }

        static Renderer FindDome()
        {
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.gameObject.name.IndexOf("skydome", System.StringComparison.OrdinalIgnoreCase) >= 0) return r;
            return null;
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        // A copy of the dome's look using the day/night shader. The original material is left alone.
        void SetUpSkyMaterial()
        {
            var original = dome.sharedMaterial;
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader != null && shader.isSupported)
            {
                skyMaterial = new Material(shader) { name = "SkyDome (Day/Night, runtime)" };
                if (original != null)
                {
                    string texName = original.HasProperty("_BaseMap") ? "_BaseMap" : original.HasProperty("_MainTex") ? "_MainTex" : null;
                    if (texName != null)
                    {
                        skyMaterial.SetTexture("_MainTex", original.GetTexture(texName));
                        skyMaterial.SetTextureScale("_MainTex", original.GetTextureScale(texName));
                        skyMaterial.SetTextureOffset("_MainTex", original.GetTextureOffset(texName));
                    }
                }
                skyMaterial.SetColor("_StarColor", starColour);
                skyMaterial.SetFloat("_StarDensity", starDensity);
                skyMaterial.SetFloat("_StarSize", starSize);
                skyMaterial.SetFloat("_StarGrid", starGrid);
                skyMaterial.SetFloat("_StarTwinkle", starTwinkle);
                dome.sharedMaterial = skyMaterial;
                skyHasTint = true;
                skyHasStars = true;
                tintProperty = "_Tint";
            }
            else
            {
                // Fallback: tint a copy of the dome's own material if it has a colour (no stars).
                Debug.LogWarning("[DayNightCycle] The Salok/Sky Dome shader is missing (Resources/Sky). Using the dome's own material, without stars.", this);
                skyMaterial = dome.material;
                tintProperty = skyMaterial.HasProperty("_BaseColor") ? "_BaseColor" : skyMaterial.HasProperty("_Color") ? "_Color" : null;
                skyHasTint = tintProperty != null;
            }
        }

        // ------------------------------------------------------------------ Every frame

        void LateUpdate()
        {
            Apply(CurrentSkyTime());

            if (dome != null && domeFollowsCamera)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 c = cam.transform.position;
                    dome.transform.position = new Vector3(c.x, domeHeight, c.z);
                }
            }
        }

        float CurrentSkyTime()
        {
            if (overrideSkyTime) return skyTime;
            var clock = GameClock_Manager.Instance != null ? GameClock_Manager.Instance.Clock : null;
            if (clock == null) return 0.4f;   // no clock: noon
            float t = DayNightMath.SkyTime(clock.TimeOfDay, clock.DaylightSeconds, clock.NightSeconds);
            skyTime = t;                       // shown in the Inspector while playing
            return t;
        }

        void Apply(float t)
        {
            if (skyMaterial != null)
            {
                if (skyHasTint) skyMaterial.SetColor(tintProperty == "_Tint" ? TintId : Shader.PropertyToID(tintProperty), skyTint.Evaluate(t));
                if (skyHasStars) skyMaterial.SetFloat(StarAlphaId, Mathf.Clamp01(stars.Evaluate(t)));
            }

            if (sun != null)
            {
                Vector3 toSun = DayNightMath.SunDirection(t, sunMaxHeight, moonMaxHeight, lowestHeight);
                sun.transform.rotation = Quaternion.LookRotation(-toSun);
                sun.color = lightColour.Evaluate(t);
                sun.intensity = Mathf.Max(0f, lightIntensity.Evaluate(t));
            }

            if (controlAmbient)
            {
                Color a = ambient.Evaluate(t);
                RenderSettings.ambientSkyColor = a;
                RenderSettings.ambientEquatorColor = a * 0.8f;
                RenderSettings.ambientGroundColor = a * 0.55f;
            }
        }

        // ------------------------------------------------------------------ Defaults

        static Gradient G(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
            g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static Gradient DefaultSkyTint() => G(
            (0.00f, C(1.00f, 0.72f, 0.62f)),   // dawn pink
            (0.10f, C(1.00f, 1.00f, 1.00f)),   // day: the dome as made
            (0.60f, C(1.00f, 1.00f, 1.00f)),
            (0.74f, C(1.00f, 0.62f, 0.40f)),   // dusk orange (lockdown soon)
            (0.80f, C(0.38f, 0.26f, 0.45f)),   // twilight purple as lockdown starts
            (0.85f, C(0.10f, 0.13f, 0.28f)),   // night navy
            (0.95f, C(0.10f, 0.13f, 0.28f)),
            (1.00f, C(1.00f, 0.72f, 0.62f)));  // back to dawn

        static AnimationCurve DefaultStars() => new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.78f, 0f), new Keyframe(0.84f, 1f),
            new Keyframe(0.95f, 1f), new Keyframe(1f, 0f));

        static Gradient DefaultLightColour() => G(
            (0.00f, C(1.00f, 0.62f, 0.45f)),   // dawn
            (0.12f, C(1.00f, 0.96f, 0.88f)),   // day
            (0.62f, C(1.00f, 0.96f, 0.88f)),
            (0.76f, C(1.00f, 0.58f, 0.32f)),   // dusk
            (0.81f, C(0.55f, 0.65f, 1.00f)),   // moonlight blue
            (0.97f, C(0.55f, 0.65f, 1.00f)),
            (1.00f, C(1.00f, 0.62f, 0.45f)));

        static AnimationCurve DefaultLightIntensity() => new AnimationCurve(
            new Keyframe(0f, 0.08f), new Keyframe(0.10f, 1f), new Keyframe(0.66f, 1f),
            new Keyframe(0.78f, 0.35f), new Keyframe(0.80f, 0.05f),   // sun sets, moon takes over
            new Keyframe(0.83f, 0.28f), new Keyframe(0.97f, 0.28f), new Keyframe(1f, 0.08f));

        static Gradient DefaultAmbient() => G(
            (0.00f, C(0.42f, 0.36f, 0.36f)),
            (0.12f, C(0.55f, 0.57f, 0.60f)),
            (0.62f, C(0.55f, 0.57f, 0.60f)),
            (0.76f, C(0.48f, 0.38f, 0.32f)),
            (0.84f, C(0.17f, 0.19f, 0.30f)),   // night: dark but the maze stays readable
            (0.96f, C(0.17f, 0.19f, 0.30f)),
            (1.00f, C(0.42f, 0.36f, 0.36f)));
    }
}
