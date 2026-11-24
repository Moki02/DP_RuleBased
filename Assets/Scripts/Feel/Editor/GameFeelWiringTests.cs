// ============================================================================
// GameFeelWiringTests.cs   (EditMode tests)
// Checks the game-feel wiring helpers: an empty sound slot stays silent, a loop fades in
// while its moment is happening and fades back out to silence, and the installed library
// (if it exists) has every team pick filled in.
//
// DESIGN RULES: none (feel only).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop.Feel
{
    /// <summary>Tests for FeelSound, FeelLoop and the installed GameFeelLibrary.</summary>
    public class GameFeelWiringTests
    {
        [Test]
        public void EmptySlot_IsSilent()
        {
            var s = new FeelSound();
            Assert.IsFalse(s.IsSet);
            s.clip = AudioClip.Create("t", 4410, 1, 44100, false);
            Assert.IsTrue(s.IsSet);
            s.volume = 0f;
            Assert.IsFalse(s.IsSet, "volume 0 = silent");
            Object.DestroyImmediate(s.clip);
        }

        [Test]
        public void Loop_FadesInWhileOn_AndOutWhenOff()
        {
            var go = new GameObject("LoopTest");
            var clip = AudioClip.Create("t", 44100, 1, 44100, false);
            try
            {
                var loop = new FeelLoop(go, new FeelSound { clip = clip, volume = 1f }) { fadeIn = 1f, fadeOut = 0.5f };
                loop.Update(true, 0.5f);
                Assert.AreEqual(0.5f, loop.Fade, 1e-4f, "half way after half the fade-in");
                loop.Update(true, 1f);
                Assert.AreEqual(1f, loop.Fade, 1e-4f);
                loop.Update(false, 0.25f);
                Assert.AreEqual(0.5f, loop.Fade, 1e-4f, "fades out at its own speed");
                loop.Update(false, 1f);
                Assert.AreEqual(0f, loop.Fade, 1e-4f);
                Assert.IsFalse(loop.Source.isPlaying, "stops when silent");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Loop_WithEmptySlot_DoesNothing()
        {
            var go = new GameObject("LoopTest");
            try
            {
                var loop = new FeelLoop(go, new FeelSound());
                loop.Update(true, 1f);
                Assert.AreEqual(0f, loop.Fade, 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void InstalledLibrary_HasEveryPick()
        {
            var lib = AssetDatabase.LoadAssetAtPath<GameFeelLibrary>("Assets/VFX/GameFeelLibrary.asset");
            if (lib == null) Assert.Ignore("Run Salok > Feel > Install Game Feel first.");
            int empty = 0;
            string missing = "";
            foreach (var f in typeof(GameFeelLibrary).GetFields())
            {
                if (f.FieldType == typeof(FeelSound))
                {
                    var s = (FeelSound)f.GetValue(lib);
                    if (s == null || s.clip == null) { empty++; missing += f.Name + " "; }
                }
                else if (f.FieldType == typeof(GameObject) && (GameObject)f.GetValue(lib) == null)
                {
                    empty++; missing += f.Name + " ";
                }
            }
            Assert.AreEqual(0, empty, "Empty slots: " + missing);
        }
    }
}
