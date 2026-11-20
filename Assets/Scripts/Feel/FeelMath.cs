// ============================================================================
// FeelMath.cs
// The small sums behind the feel effects, kept in one place so they can be tested:
// how strong the shake is, how the punch bounces, how a counter rolls, how strong the
// low-HYD vignette is and how fast it pulses, and how floating numbers fade.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: called by the Feel scripts. Tests: FeelMathTests (EditMode).
// HOW TO EDIT: the numbers come from FeelSettings; change them there, not here.
// ============================================================================

using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Pure helper maths for the Feel scripts (no scene objects needed).</summary>
    public static class FeelMath
    {
        /// <summary>
        /// Shake strength 0..1 from "trauma" 0..1. Squared, so small knocks stay small and big
        /// hits feel big.
        /// </summary>
        public static float ShakeStrength(float trauma)
        {
            trauma = Mathf.Clamp01(trauma);
            return trauma * trauma;
        }

        /// <summary>Trauma after <paramref name="dt"/> seconds of fading at <paramref name="decayPerSecond"/>.</summary>
        public static float DecayTrauma(float trauma, float decayPerSecond, float dt)
            => Mathf.Max(0f, trauma - decayPerSecond * dt);

        /// <summary>
        /// Punch scale at time <paramref name="t"/> (0..1 of the punch). Starts at 1, jumps up to
        /// <paramref name="peak"/>, wobbles once below 1 and settles back on exactly 1.
        /// </summary>
        public static float PunchScale(float t, float peak)
        {
            if (t <= 0f || t >= 1f) return 1f;
            // A damped sine: one big bounce, one small dip, then still.
            float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * (1f - t);
            return 1f + (peak - 1f) * wave / 0.671f; // 0.671 = top of the wave, so the top is exactly "peak"
        }

        /// <summary>Counter value at time <paramref name="t"/> (0..1) rolling from <paramref name="from"/> to <paramref name="to"/>. Eases out (fast then slow).</summary>
        public static float Roll(float from, float to, float t)
        {
            t = Mathf.Clamp01(t);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            return Mathf.LerpUnclamped(from, to, eased);
        }

        /// <summary>
        /// How strong the low-HYD vignette is (0..1) for HYD as a part of max (0..1).
        /// 0 above <paramref name="startsAt"/>, rising to 1 at 0 HYD.
        /// </summary>
        public static float VignetteStrength(float hydFraction, float startsAt)
        {
            if (startsAt <= 0f) return 0f;
            hydFraction = Mathf.Clamp01(hydFraction);
            if (hydFraction >= startsAt) return 0f;
            return 1f - hydFraction / startsAt;
        }

        /// <summary>Heartbeat speed in beats per minute for a vignette strength 0..1.</summary>
        public static float HeartRate(float strength, float bpmLow, float bpmHigh)
            => Mathf.Lerp(bpmLow, bpmHigh, Mathf.Clamp01(strength));

        /// <summary>
        /// A "lub-dub" heartbeat pulse 0..1 at <paramref name="phase"/> (0..1 of one beat): a
        /// strong beat, a softer second beat, then rest.
        /// </summary>
        public static float HeartPulse(float phase)
        {
            phase -= Mathf.Floor(phase);
            float a = Bump(phase, 0.00f, 0.12f);
            float b = Bump(phase, 0.18f, 0.12f) * 0.6f;
            return Mathf.Max(a, b);
        }

        // A smooth hump that starts at "start" and lasts "width".
        static float Bump(float x, float start, float width)
        {
            float u = (x - start) / width;
            if (u <= 0f || u >= 1f) return 0f;
            return Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>Alpha of a floating number at life <paramref name="t"/> (0..1): fully visible until <paramref name="fadeStart"/>, then fades to 0.</summary>
        public static float FloatAlpha(float t, float fadeStart)
        {
            t = Mathf.Clamp01(t);
            if (t <= fadeStart) return 1f;
            if (fadeStart >= 1f) return 1f;
            return 1f - (t - fadeStart) / (1f - fadeStart);
        }

        /// <summary>Scale of a floating number at life <paramref name="t"/>: pops in at <paramref name="pop"/> and eases to 1 in the first 15%.</summary>
        public static float FloatScale(float t, float pop)
        {
            const float popTime = 0.15f;
            if (t >= popTime) return 1f;
            return Mathf.Lerp(pop, 1f, Mathf.Clamp01(t / popTime));
        }
    }
}
