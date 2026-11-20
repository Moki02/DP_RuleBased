// ============================================================================
// LowHydVignette.cs
// Darkens (or reddens) the edges of the screen when HYD gets low, and pulses like a
// heartbeat that speeds up as HYD drops. Nothing shows while HYD is fine.
//
// DESIGN RULES: none (feel only; it reads HYD, never changes it).
//
// HOW TO USE: the prefab Assets/VFX/Resources/Feel/LowHydVignette.prefab is a Canvas with
// one full-screen Image. Put a copy in the scene and tell it the HYD:
//     vignette.SetHydration(currentHyd, maxHyd);     (every frame or when HYD changes)
// HOW TO EDIT: the image, its colour and sprite are in the prefab (artists). When it starts
// showing, how dark it gets and the pulse speed are in FeelSettings > Low-HYD vignette.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace TheLastDrop.Feel
{
    /// <summary>Full-screen edge vignette driven by HYD, with a heartbeat pulse.</summary>
    public class LowHydVignette : MonoBehaviour
    {
        [Tooltip("The full-screen vignette image. Its colour's alpha is controlled by this script.")]
        public Graphic image;
        [Tooltip("HYD part of max (0..1) the screen starts at. 1 = full.")]
        [Range(0f, 1f)] public float hydFraction = 1f;

        float phase;

        /// <summary>Current strength 0..1 (0 = invisible).</summary>
        public float Strength { get; private set; }

        /// <summary>Sets HYD as current and max values (for example 12 of 100).</summary>
        public void SetHydration(float current, float max)
            => SetHydrationFraction(max > 0f ? current / max : 0f);

        /// <summary>Sets HYD as a part of max, 0..1.</summary>
        public void SetHydrationFraction(float fraction) => hydFraction = Mathf.Clamp01(fraction);

        void Update()
        {
            if (image == null) return;
            var s = FeelSettings.Active;
            Strength = FeelMath.VignetteStrength(hydFraction, s.vignetteStartsAt);

            float alpha = 0f;
            if (Strength > 0f)
            {
                float bpm = FeelMath.HeartRate(Strength, s.vignetteBpmLow, s.vignetteBpmHigh);
                phase += Time.unscaledDeltaTime * bpm / 60f;
                if (phase > 1000f) phase -= 1000f;
                float pulse = FeelMath.HeartPulse(phase) * s.vignettePulse * Strength;
                alpha = Mathf.Clamp01(Strength * s.vignetteMaxAlpha + pulse);
            }

            var c = image.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                image.color = c;
            }
            bool show = alpha > 0.001f;
            if (image.enabled != show) image.enabled = show;
        }
    }
}
