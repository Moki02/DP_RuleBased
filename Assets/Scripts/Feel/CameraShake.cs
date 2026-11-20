// ============================================================================
// CameraShake.cs
// A small screen shake that can be turned off in the settings. It works WITHOUT changing
// Player_Camera: just before the camera draws a frame it nudges the camera a little, and
// right after the frame it puts it back. So the developer's follow / look / wall code never
// sees the shake and can't fight it.
//
// DESIGN RULES: none (feel only). Accessibility: players can switch it off (Enabled).
//
// HOW TO USE:
//   1. Add CameraShake to the object that has the Camera component (in the game that is the
//      "Camera Object" child under Player_Camera's pivot). Nothing else to set up.
//   2. From code: CameraShake.Add(FeelSettings.Active.shakeSmall);   (or any 0..1 amount)
//      Or from a UnityEvent: call ShakeSmall / ShakeMedium / ShakeBig on the component.
//   3. Settings screen toggle (later): CameraShake.Enabled = toggle.isOn;  (saved for next time)
// HOW TO EDIT: strength, speed and how fast it fades are in FeelSettings > Screen shake.
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastDrop.Feel
{
    /// <summary>Trauma-based camera shake applied only while the camera renders.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraShake : MonoBehaviour
    {
        /// <summary>PlayerPrefs key for the on/off setting.</summary>
        public const string PrefKey = "Salok.Feel.ScreenShake";

        static float trauma;

        /// <summary>Is screen shake on? Saved between sessions. Default comes from FeelSettings.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, FeelSettings.Active.shakeOnByDefault ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                if (!value) trauma = 0f;
            }
        }

        /// <summary>Current shake amount 0..1 (fades over time).</summary>
        public static float Trauma => trauma;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => trauma = 0f;

        /// <summary>Adds shake (0..1). Several hits add up, capped at 1. Does nothing if shake is off.</summary>
        public static void Add(float amount)
        {
            if (amount <= 0f || !Enabled) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>Stops any shake straight away.</summary>
        public static void Stop() => trauma = 0f;

        Camera cam;
        Vector3 savedPos;
        Quaternion savedRot;
        bool applied;
        float seed;

        void Awake()
        {
            cam = GetComponent<Camera>();
            seed = Random.value * 100f;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            Restore();
        }

        void Update()
        {
            trauma = FeelMath.DecayTrauma(trauma, FeelSettings.Active.shakeDecayPerSecond, Time.unscaledDeltaTime);
        }

        /// <summary>For UnityEvents: a small shake.</summary>
        public void ShakeSmall() => Add(FeelSettings.Active.shakeSmall);
        /// <summary>For UnityEvents: a medium shake.</summary>
        public void ShakeMedium() => Add(FeelSettings.Active.shakeMedium);
        /// <summary>For UnityEvents: a big shake.</summary>
        public void ShakeBig() => Add(FeelSettings.Active.shakeBig);

        void OnBegin(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != cam || trauma <= 0f) return;
            var s = FeelSettings.Active;
            float k = FeelMath.ShakeStrength(trauma);
            float t = Time.unscaledTime * s.shakeFrequency;

            // Smooth random wobble (Perlin noise) in -1..1 for each direction.
            float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
            float y = Mathf.PerlinNoise(seed + 10f, t) * 2f - 1f;
            float r = Mathf.PerlinNoise(seed + 20f, t) * 2f - 1f;

            var tr = transform;
            savedPos = tr.localPosition;
            savedRot = tr.localRotation;
            applied = true;
            tr.localPosition = savedPos + new Vector3(x, y, 0f) * (s.shakeMaxOffset * k);
            tr.localRotation = savedRot * Quaternion.Euler(0f, 0f, r * s.shakeMaxAngle * k);
        }

        void OnEnd(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering == cam) Restore();
        }

        void Restore()
        {
            if (!applied) return;
            transform.localPosition = savedPos;
            transform.localRotation = savedRot;
            applied = false;
        }
    }
}
