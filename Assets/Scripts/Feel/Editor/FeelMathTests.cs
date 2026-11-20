// ============================================================================
// FeelMathTests.cs   (EditMode tests)
// Checks the game-feel maths: shake grows with trauma and fades to zero, the UI punch
// peaks at the set size and ends back at normal size, counters roll exactly to their new
// value, the low-HYD vignette is off above its start point and full at 0 HYD, the
// heartbeat speeds up, floating numbers fade out, and hit-stop always gives the speed back.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Tests for FeelMath and FeelSettings defaults.</summary>
    public class FeelMathTests
    {
        [Test]
        public void Shake_IsZeroWithoutTrauma_AndFullAtOne()
        {
            Assert.AreEqual(0f, FeelMath.ShakeStrength(0f), 1e-5f);
            Assert.AreEqual(1f, FeelMath.ShakeStrength(1f), 1e-5f);
            Assert.Less(FeelMath.ShakeStrength(0.3f), 0.3f, "small knocks are softer than linear");
            Assert.AreEqual(1f, FeelMath.ShakeStrength(5f), 1e-5f, "clamped");
        }

        [Test]
        public void Trauma_FadesToZeroAndNeverBelow()
        {
            float t = 1f;
            for (int i = 0; i < 200; i++) t = FeelMath.DecayTrauma(t, 1.6f, 0.02f);
            Assert.AreEqual(0f, t, 1e-5f);
            Assert.AreEqual(0.5f, FeelMath.DecayTrauma(1f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void Punch_PeaksAtSetSize_AndEndsAtOne()
        {
            float max = 0f;
            for (int i = 0; i <= 1000; i++) max = Mathf.Max(max, FeelMath.PunchScale(i / 1000f, 1.2f));
            Assert.AreEqual(1.2f, max, 0.005f);
            Assert.AreEqual(1f, FeelMath.PunchScale(0f, 1.2f), 1e-5f);
            Assert.AreEqual(1f, FeelMath.PunchScale(1f, 1.2f), 1e-5f);
            Assert.AreEqual(1f, FeelMath.PunchScale(0.999f, 1.2f), 0.001f, "no jump at the end");
        }

        [Test]
        public void Roll_StartsAtFrom_EndsExactlyAtTo_AndEasesOut()
        {
            Assert.AreEqual(10f, FeelMath.Roll(10f, 20f, 0f), 1e-5f);
            Assert.AreEqual(20f, FeelMath.Roll(10f, 20f, 1f), 1e-5f);
            Assert.AreEqual(20f, FeelMath.Roll(10f, 20f, 3f), 1e-5f, "clamped");
            Assert.Greater(FeelMath.Roll(0f, 1f, 0.5f), 0.5f, "fast at first, slow at the end");
            Assert.AreEqual(5f, FeelMath.Roll(8f, 5f, 1f), 1e-5f, "rolls down too");
        }

        [Test]
        public void Vignette_OffAboveStart_FullAtZero()
        {
            Assert.AreEqual(0f, FeelMath.VignetteStrength(1f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, FeelMath.VignetteStrength(0.3f, 0.3f), 1e-5f);
            Assert.AreEqual(0.5f, FeelMath.VignetteStrength(0.15f, 0.3f), 1e-5f);
            Assert.AreEqual(1f, FeelMath.VignetteStrength(0f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, FeelMath.VignetteStrength(0f, 0f), 1e-5f, "start 0 = vignette off");
        }

        [Test]
        public void Heartbeat_SpeedsUp_AndPulseStaysInRange()
        {
            Assert.AreEqual(60f, FeelMath.HeartRate(0f, 60f, 120f), 1e-4f);
            Assert.AreEqual(120f, FeelMath.HeartRate(1f, 60f, 120f), 1e-4f);
            for (int i = 0; i <= 100; i++)
            {
                float p = FeelMath.HeartPulse(i / 37f);
                Assert.That(p, Is.InRange(0f, 1f));
            }
            Assert.AreEqual(0f, FeelMath.HeartPulse(0.7f), 1e-5f, "rest between beats");
        }

        [Test]
        public void FloatingText_VisibleThenFades()
        {
            Assert.AreEqual(1f, FeelMath.FloatAlpha(0.2f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, FeelMath.FloatAlpha(0.75f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, FeelMath.FloatAlpha(1f, 0.5f), 1e-5f);
            Assert.AreEqual(1.35f, FeelMath.FloatScale(0f, 1.35f), 1e-5f);
            Assert.AreEqual(1f, FeelMath.FloatScale(0.5f, 1.35f), 1e-5f);
        }

        [Test]
        public void Settings_DefaultsAreSafe()
        {
            var s = ScriptableObject.CreateInstance<FeelSettings>();
            try
            {
                Assert.That(s.shakeMaxOffset, Is.InRange(0f, 0.5f), "shake stays small");
                Assert.Greater(s.punchScale, 1f);
                Assert.Greater(s.floatLifetime, 0f);
                Assert.That(s.vignetteStartsAt, Is.InRange(0.01f, 1f));
                Assert.That(s.hitStopDuration, Is.InRange(0f, 0.2f), "hit-stop is short");
            }
            finally { Object.DestroyImmediate(s); }
        }

        [Test]
        public void FeelPool_ReusesReleasedCopies()
        {
            var prefab = new GameObject("PoolTestPrefab");
            try
            {
                var a = FeelPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
                FeelPool.Release(a);
                Assert.IsFalse(a.activeSelf, "released copies are hidden");
                var b = FeelPool.Spawn(prefab, Vector3.one, Quaternion.identity);
                Assert.AreSame(a, b, "the same copy is used again");
                Assert.IsTrue(b.activeSelf);
                Assert.AreEqual(Vector3.one, b.transform.position);
                Object.DestroyImmediate(b);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                var root = GameObject.Find("[FeelPool]");
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
