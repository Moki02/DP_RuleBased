// ============================================================================
// MenuMemoryTests.cs   (EditMode tests)
// Checks what the Tab menu remembers and what Tab, B, M and Esc do:
//   open Build, switch to Skills, close, Tab reopens Skills;
//   then M opens Map, close, Tab reopens Map.
//
// DESIGN RULES: R7.1.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;

namespace TheLastDrop
{
    /// <summary>Tests for MenuMemory.</summary>
    public class MenuMemoryTests
    {
        [Test]
        public void FirstTab_OpensBuild()
        {
            var m = new MenuMemory();
            m.Press(MenuKey.Tab);
            Assert.IsTrue(m.IsOpen);
            Assert.AreEqual(MenuPage.Build, m.Current);
            m.Press(MenuKey.Tab);
            Assert.IsFalse(m.IsOpen, "Tab again closes");
        }

        [Test]
        public void Memory_SequenceFromTheBrief()
        {
            var m = new MenuMemory();
            m.Press(MenuKey.B);                    // open Build
            Assert.AreEqual(MenuPage.Build, m.Current);
            m.SwitchTo(MenuPage.Skills);           // switch to Skills
            m.Press(MenuKey.Esc);                  // close
            Assert.IsFalse(m.IsOpen);
            m.Press(MenuKey.Tab);                  // Tab reopens Skills
            Assert.IsTrue(m.IsOpen);
            Assert.AreEqual(MenuPage.Skills, m.Current);

            m.Press(MenuKey.M);                    // M opens Map
            Assert.AreEqual(MenuPage.Map, m.Current);
            m.Press(MenuKey.Tab);                  // close
            Assert.IsFalse(m.IsOpen);
            m.Press(MenuKey.Tab);                  // Tab reopens Map
            Assert.AreEqual(MenuPage.Map, m.Current);
            Assert.IsTrue(m.IsOpen);
        }

        [Test]
        public void SameKeyAgain_Closes_OtherKeySwitches()
        {
            var m = new MenuMemory();
            m.Press(MenuKey.M);
            Assert.AreEqual(MenuPage.Map, m.Current);
            m.Press(MenuKey.B);
            Assert.IsTrue(m.IsOpen, "B on the Map page goes to Build");
            Assert.AreEqual(MenuPage.Build, m.Current);
            m.Press(MenuKey.B);
            Assert.IsFalse(m.IsOpen, "B on the Build page closes");
            m.Press(MenuKey.M);
            m.Press(MenuKey.M);
            Assert.IsFalse(m.IsOpen, "M on the Map page closes");
        }

        [Test]
        public void Esc_WhileClosed_DoesNothing()
        {
            var m = new MenuMemory();
            Assert.IsFalse(m.Press(MenuKey.Esc));
            Assert.IsFalse(m.IsOpen);
        }

        [Test]
        public void SelectedCards_RememberedPerPage()
        {
            var m = new MenuMemory();
            m.Select(UpgradeTab.Build, "W4");
            m.Select(UpgradeTab.Skills, "V3");
            Assert.AreEqual("W4", m.SelectedFor(UpgradeTab.Build));
            Assert.AreEqual("V3", m.SelectedFor(UpgradeTab.Skills));
        }
    }
}
