// ============================================================================
// CompassTests.cs   (EditMode tests)
// Checks the compass maths: North is world +Z and East is +X, marks to your right are
// positive, the 0/360 wrap works, the right labels sit on the 45s, and marks fade out only
// near the ends of the strip.
//
// DESIGN RULES: none yet (pending GDD entry: HUD compass).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for Compass.</summary>
    public class CompassTests
    {
        [Test]
        public void Heading_PlusZIsNorth_PlusXIsEast()
        {
            Assert.AreEqual(0f, Compass.HeadingFromDirection(Vector3.forward), 0.01f);
            Assert.AreEqual(90f, Compass.HeadingFromDirection(Vector3.right), 0.01f);
            Assert.AreEqual(180f, Compass.HeadingFromDirection(Vector3.back), 0.01f);
            Assert.AreEqual(270f, Compass.HeadingFromDirection(Vector3.left), 0.01f);
        }

        [Test]
        public void Heading_IgnoresLookingUpOrDown()
        {
            Assert.AreEqual(45f, Compass.HeadingFromDirection(new Vector3(1f, -3f, 1f)), 0.01f);
            Assert.AreEqual(0f, Compass.HeadingFromDirection(Vector3.up), 0.01f, "straight up has no heading: 0");
        }

        [Test]
        public void FacingEast_NorthIsOnTheLeft_SouthOnTheRight()
        {
            Assert.AreEqual(-90f, Compass.DegreesFromCenter(90f, 0f), 0.01f);
            Assert.AreEqual(90f, Compass.DegreesFromCenter(90f, 180f), 0.01f);
            Assert.AreEqual(0f, Compass.DegreesFromCenter(90f, 90f), 0.01f);
        }

        [Test]
        public void Wraps_Across_0_And_360()
        {
            Assert.AreEqual(10f, Compass.DegreesFromCenter(350f, 0f), 0.01f, "facing 350, N is just to the right");
            Assert.AreEqual(-15f, Compass.DegreesFromCenter(5f, 350f), 0.01f, "facing 5, the 350 tick is just to the left");
        }

        [Test]
        public void Labels_On_The_45s_Only()
        {
            Assert.AreEqual("N", Compass.LabelFor(0f));
            Assert.AreEqual("NE", Compass.LabelFor(45f));
            Assert.AreEqual("E", Compass.LabelFor(90f));
            Assert.AreEqual("SW", Compass.LabelFor(225f));
            Assert.AreEqual("NW", Compass.LabelFor(315f));
            Assert.AreEqual("N", Compass.LabelFor(360f));
            Assert.IsNull(Compass.LabelFor(15f));
            Assert.IsNull(Compass.LabelFor(330f));
        }

        [Test]
        public void EdgeFade_FullInTheMiddle_ZeroAtTheEnds()
        {
            Assert.AreEqual(1f, Compass.EdgeAlpha(0f, 0.2f), 0.001f);
            Assert.AreEqual(1f, Compass.EdgeAlpha(0.8f, 0.2f), 0.001f);
            Assert.AreEqual(1f, Compass.EdgeAlpha(-0.8f, 0.2f), 0.001f);
            Assert.AreEqual(0.5f, Compass.EdgeAlpha(0.9f, 0.2f), 0.001f);
            Assert.AreEqual(0f, Compass.EdgeAlpha(1f, 0.2f), 0.001f);
            Assert.AreEqual(0f, Compass.EdgeAlpha(1.4f, 0.2f), 0.001f);
            Assert.AreEqual(1f, Compass.EdgeAlpha(0.99f, 0f), 0.001f, "no fade: hard cut");
        }
    }
}
