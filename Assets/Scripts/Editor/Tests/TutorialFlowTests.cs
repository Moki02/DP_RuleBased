// ============================================================================
// TutorialFlowTests.cs   (EditMode tests)
// Checks the guided tutorial's rules (TutorialFlow), without a scene:
//   - steps run in order and only move on once their action is done;
//   - a step that can't be done passes by itself (no soft-lock); "read" steps need card time;
//   - features unlock with their step, and early unlocks work;
//   - skip step / skip tutorial; stuck offer after Stuck Seconds;
//   - side lessons show once each, never while the card is hidden;
//   - save and resume puts the player back on the same step;
//   - TutorialGate: no tutorial = everything allowed; the easy minigame is never harder.
//   - every step and lesson the director uses has default words.
//
// DESIGN RULES: none of their own.
//
// HOW TO USE: Window > General > Test Runner > EditMode > Run All.
// HOW TO EDIT: nothing to tune.
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace TheLastDrop
{
    /// <summary>Tests for TutorialFlow, TutorialGate and TutorialTexts.</summary>
    public class TutorialFlowTests
    {
        const float Frame = 0.1f;

        // Three steps: A (done when a = true, unlocks the pump), B (read 1 s), C (done when c = true, unlocks the tap).
        bool a, c, cBlocked;
        int aEntered;

        TutorialFlow MakeFlow()
        {
            a = c = cBlocked = false;
            aEntered = 0;
            var f = new TutorialFlow { DoneSeconds = 0.5f, StuckSeconds = 5f, LessonSeconds = 2f, LessonMinSeconds = 0.5f, LessonCheckInterval = 0f };
            f.Add(new TutorialStep { Id = "A", Unlocks = TutorialFeature.Pump, IsDone = () => a, OnEnter = () => aEntered++ });
            f.Add(new TutorialStep { Id = "B", ReadOnly = true, ReadSeconds = 1f });
            f.Add(new TutorialStep { Id = "C", Unlocks = TutorialFeature.Tap, IsDone = () => c, CannotBeDone = () => cBlocked });
            return f;
        }

        static void Run(TutorialFlow f, float seconds, bool visible = true)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Frame) f.Tick(Frame, visible);
        }

        [Test]
        public void Steps_WaitForTheirAction_ThenGoInOrder()
        {
            var f = MakeFlow();
            f.Begin();
            Assert.AreEqual("A", f.Current.Id);
            Assert.AreEqual(1, aEntered, "OnEnter runs when the step starts");

            Run(f, 3f);
            Assert.AreEqual("A", f.Current.Id, "doesn't move on until the action is done");

            a = true;
            f.Tick(Frame, true);
            Assert.IsTrue(f.ShowingDone, "the Done tick shows");
            Run(f, 0.6f);
            Assert.AreEqual("B", f.Current.Id);
        }

        [Test]
        public void ReadStep_NeedsCardTimeOnScreen()
        {
            var f = MakeFlow();
            f.Begin(1);
            Run(f, 3f, visible: false);
            Assert.AreEqual("B", f.Current.Id, "hidden card time doesn't count");
            Run(f, 1.1f);
            Assert.IsTrue(f.ShowingDone);
        }

        [Test]
        public void StepThatCantBeDone_PassesByItself()
        {
            var f = MakeFlow();
            f.Begin(2);
            cBlocked = true;
            f.Tick(Frame, true);
            Assert.IsTrue(f.ShowingDone, "no soft-lock: the step passes");
            Run(f, 0.6f);
            Assert.AreEqual(TutorialFlow.Mode.Finished, f.State);
            Assert.IsNull(f.Current);
        }

        [Test]
        public void Features_UnlockWithTheirStep()
        {
            var f = MakeFlow();
            Assert.IsTrue(f.Allows(TutorialFeature.Map), "not running = everything allowed");
            f.Begin();
            Assert.IsTrue(f.Allows(TutorialFeature.Pump), "A unlocks the pump");
            Assert.IsFalse(f.Allows(TutorialFeature.Tap), "the tap is C's");
            Assert.IsFalse(f.Allows(TutorialFeature.BuildSkills));

            f.Unlock(TutorialFeature.BuildSkills);
            Assert.IsTrue(f.Allows(TutorialFeature.BuildSkills), "early unlock (first dawn)");

            f.Begin(2);
            Assert.IsTrue(f.Allows(TutorialFeature.Tap));
            Assert.IsTrue(f.Allows(TutorialFeature.Pump), "earlier unlocks stay");
        }

        [Test]
        public void Stuck_OfferedAfterStuckSeconds_AndSkipStepMovesOn()
        {
            var f = MakeFlow();
            f.Begin();
            Run(f, 4f);
            Assert.IsFalse(f.IsStuck);
            Run(f, 1.2f);
            Assert.IsTrue(f.IsStuck);
            f.SkipStep();
            Run(f, 0.6f);
            Assert.AreEqual("B", f.Current.Id);
        }

        [Test]
        public void SkipAll_TurnsItOff_AndAllowsEverything()
        {
            var f = MakeFlow();
            f.Begin();
            f.SkipAll();
            Assert.AreEqual(TutorialFlow.Mode.Off, f.State);
            Assert.IsNull(f.Current);
            Assert.IsTrue(f.Allows(TutorialFeature.All));
        }

        [Test]
        public void Lessons_ShowOnce_AndWaitBehindHiddenCard()
        {
            var f = MakeFlow();
            bool night = false;
            int shown = 0;
            f.AddLesson(new TutorialLesson { Id = "night", Trigger = () => night });
            f.LessonStarted += l => shown++;
            f.Begin();

            night = true;
            f.Tick(Frame, false);
            Assert.IsNull(f.CurrentLesson, "not while the card is hidden");
            f.Tick(Frame, true);
            Assert.AreEqual("night", f.CurrentLesson.Id);
            Assert.AreEqual("A", f.Current.Id, "the main step keeps going underneath");

            Run(f, 2.1f);
            Assert.IsNull(f.CurrentLesson, "closes after Lesson Seconds");
            Run(f, 3f);
            Assert.AreEqual(1, shown, "once only");
        }

        [Test]
        public void Lessons_StillShowAfterFinishing_ButNotWhenOff()
        {
            var f = MakeFlow();
            bool rain = true;
            f.AddLesson(new TutorialLesson { Id = "rain", Trigger = () => rain });
            f.Begin(2);
            f.MarkSeen("rain");
            c = true;
            Run(f, 1f);
            Assert.AreEqual(TutorialFlow.Mode.Finished, f.State);
            Assert.IsNull(f.CurrentLesson, "a lesson marked seen never shows");

            var g = MakeFlow();
            g.AddLesson(new TutorialLesson { Id = "rain", Trigger = () => rain });
            g.Tick(Frame, true);
            Assert.IsNull(g.CurrentLesson, "off = no lessons either");
        }

        [Test]
        public void SaveAndResume_PutsThePlayerBackOnTheSameStep()
        {
            var f = MakeFlow();
            f.Begin();
            a = true;
            Run(f, 0.7f);                        // A done, now on B
            f.Unlock(TutorialFeature.BuildSkills);
            f.MarkSeen("dusk");
            var saved = f.Capture();
            Assert.AreEqual("B", saved.stepId);

            var g = MakeFlow();
            g.Restore(saved);
            Assert.AreEqual(TutorialFlow.Mode.Running, g.State);
            Assert.AreEqual("B", g.Current.Id);
            Assert.IsTrue(g.Allows(TutorialFeature.BuildSkills), "early unlocks are saved");
            Assert.IsTrue(g.HasSeen("dusk"), "lessons seen are saved");
        }

        [Test]
        public void Save_DuringDoneTick_ResumesOnTheNextStep()
        {
            var f = MakeFlow();
            f.Begin();
            a = true;
            f.Tick(Frame, true);
            Assert.IsTrue(f.ShowingDone);
            Assert.AreEqual("B", f.Capture().stepId, "a finished step isn't repeated after loading");
        }

        [Test]
        public void Restore_NullOrUnknownStep_IsSafe()
        {
            var f = MakeFlow();
            f.Restore(null);
            Assert.AreEqual(TutorialFlow.Mode.Off, f.State, "an old save: tutorial off");

            f.Restore(new TutorialSaveState { mode = TutorialFlow.Mode.Running, stepId = "renamed-step" });
            Assert.AreEqual("A", f.Current.Id, "an unknown step starts again from the beginning");

            f.Restore(new TutorialSaveState { mode = TutorialFlow.Mode.Finished });
            Assert.AreEqual(TutorialFlow.Mode.Finished, f.State);
            Assert.IsTrue(f.Allows(TutorialFeature.All));
        }

        [Test]
        public void BrokenCheck_NeverBreaksTheGame()
        {
            var f = new TutorialFlow();
            f.Add(new TutorialStep { Id = "X", IsDone = () => throw new System.Exception("boom") });
            f.Begin();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => f.Tick(Frame, true));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual("X", f.Current.Id, "a failing check counts as not done");
        }

        [Test]
        public void Gate_WithoutTutorial_AllowsEverything()
        {
            var old = TutorialGate.FeatureCheck;
            try
            {
                TutorialGate.FeatureCheck = null;
                Assert.IsTrue(TutorialGate.AllowsMenu(MenuPage.Build));
                Assert.IsTrue(TutorialGate.AllowsMenu(MenuPage.Map));
                Assert.IsTrue(TutorialGate.Allows(TutorialFeature.Maze));

                var f = MakeFlow();
                f.Begin();
                TutorialGate.FeatureCheck = f.Allows;
                Assert.IsFalse(TutorialGate.AllowsMenu(MenuPage.Map), "map locked during the first steps");
                Assert.IsFalse(TutorialGate.AllowsMenu(MenuPage.Skills));
            }
            finally { TutorialGate.FeatureCheck = old; }
        }

        [Test]
        public void EasyMinigame_IsNeverHarderThanTheRealGate()
        {
            var normal = new ValveTimingSettings(30f, 120f, 3, 3);
            var easy = TutorialGate.EasySettings(normal);
            Assert.GreaterOrEqual(easy.bandWidth, normal.bandWidth);
            Assert.LessOrEqual(easy.needleSpeed, normal.needleSpeed);
            Assert.AreEqual(normal.hitsToWin, easy.hitsToWin);
            Assert.AreEqual(normal.missesToLose, easy.missesToLose);

            var veryEasy = new ValveTimingSettings(90f, 20f, 3, 3);
            var e2 = TutorialGate.EasySettings(veryEasy);
            Assert.AreEqual(90f, e2.bandWidth, 1e-4f, "a gate already easier keeps its own band");
            Assert.AreEqual(20f, e2.needleSpeed, 1e-4f);
        }

        [Test]
        public void EveryStepAndLesson_HasDefaultWords()
        {
            var texts = ScriptableObject.CreateInstance<TutorialTexts>();
            string[] steps = { "move", "pump", "rawBasin", "pot", "storage", "hud", "tap", "gatePour", "valve", "maze", "map", "return", "build" };
            foreach (var id in steps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(texts.Get(id).objective), id + " has an objective");
                Assert.IsFalse(string.IsNullOrEmpty(texts.Get(id).card), id + " has a card");
            }
            string[] cards = { "dusk", "lockdown", "room", "roomDoor", "sick", "bank", "valveGrate", "zone2", "stage", "calamity",
                               "weather.storm", "weather.heat", "weather.calm", "dehydrated", "valve.play", "valve.miss",
                               "death.lost", "death.kept", "death.next" };
            foreach (var id in cards) Assert.IsFalse(string.IsNullOrEmpty(texts.Get(id).card), id + " has words");
            string[] objectives = { "goal", "pot.empty", "hud.full", "gatePour.night", "valve.night", "maze.go", "maze.night" };
            foreach (var id in objectives) Assert.IsFalse(string.IsNullOrEmpty(texts.Get(id).objective), id + " has an objective");
            Object.DestroyImmediate(texts);
        }
    }
}
