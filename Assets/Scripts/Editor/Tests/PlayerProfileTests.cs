// ============================================================================
// PlayerProfileTests.cs   (EditMode tests)
// Checks the name rules (trim, 16 characters at most, "Survivor" when empty) and the
// {name} / {NAME} replacement used by the intro script.
//
// DESIGN RULES: none (intro / name entry).
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;

namespace TheLastDrop
{
    /// <summary>Tests for PlayerProfile and IntroScript.SplitLines.</summary>
    public class PlayerProfileTests
    {
        [Test]
        public void Clean_TrimsSpaces() => Assert.AreEqual("Maya", PlayerProfile.Clean("   Maya  "));

        [Test]
        public void Clean_EmptyOrBlank_IsDefault()
        {
            Assert.AreEqual(PlayerProfile.DefaultName, PlayerProfile.Clean(""));
            Assert.AreEqual(PlayerProfile.DefaultName, PlayerProfile.Clean("    "));
            Assert.AreEqual(PlayerProfile.DefaultName, PlayerProfile.Clean(null));
            Assert.AreEqual("Survivor", PlayerProfile.DefaultName);
        }

        [Test]
        public void Clean_CutsAt16Characters()
        {
            Assert.AreEqual(16, PlayerProfile.MaxLength);
            Assert.AreEqual("ABCDEFGHIJKLMNOP", PlayerProfile.Clean("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.AreEqual("ABCDEFGHIJKLMNOP", PlayerProfile.Clean("  ABCDEFGHIJKLMNOP  "), "trimmed before cutting");
            Assert.AreEqual("Juan dela", PlayerProfile.Clean("Juan dela       Cruz"), "no trailing space after the cut");
        }

        [Test]
        public void Fill_ReplacesNameAndCapitals()
        {
            Assert.AreEqual("SUBJECT MAYA SECURED.", PlayerProfile.Fill("SUBJECT {NAME} SECURED.", "Maya"));
            Assert.AreEqual("- Maya", PlayerProfile.Fill("- {name}", "Maya"));
            Assert.AreEqual("Maya / MAYA", PlayerProfile.Fill("{name} / {NAME}", "Maya"));
            Assert.AreEqual("No name here.", PlayerProfile.Fill("No name here.", "Maya"));
        }

        [Test]
        public void SplitLines_FlagsLoudspeakerAndFillsName()
        {
            var lines = IntroScript.SplitLines(
                "Then a hand closed over my mouth.\r\n\n[LOUDSPEAKER] SUBJECT {NAME} SECURED.\n- {name}  ", "Maya");
            Assert.AreEqual(3, lines.Count, "empty rows skipped");
            Assert.IsFalse(lines[0].loudspeaker);
            Assert.AreEqual("Then a hand closed over my mouth.", lines[0].text);
            Assert.IsTrue(lines[1].loudspeaker);
            Assert.AreEqual("SUBJECT MAYA SECURED.", lines[1].text);
            Assert.AreEqual("- Maya", lines[2].text);
        }
    }
}
