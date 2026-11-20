// ============================================================================
// TutorialFlow.cs
// The tutorial's rules, in plain C# (no scene needed, so the EditMode tests can run it):
//
//   MAIN STEPS run in order (pump, then Raw Basin, then the pot ...). A step finishes when its
//   "done" check is true (the player did it), or by itself when its "can't be done now" check
//   is true (so a step can never trap the player). Then a "Done" tick shows for a moment and
//   the next step starts. A step marked "read" finishes once its card has been on screen long
//   enough. Each step can UNLOCK features (the pump, the tap, the map ...): anything not yet
//   unlocked stays unavailable (TutorialGate asks Allows()).
//
//   SIDE LESSONS (night, rooms, weather ...) show once each, the first time their trigger is
//   true. They never block anything; while one shows it takes the card, the objective line
//   keeps showing the main step.
//
//   STATES: Off (skipped, already done in an earlier run, or an old save), Running (main steps
//   going), Finished (main steps done; side lessons still show the first time they happen).
//
// DESIGN RULES: none of its own.
//
// HOW TO USE: TutorialDirector builds the steps (Add / AddLesson), then calls Begin and Tick
// every frame. Save with Capture / Restore.
// HOW TO EDIT: the steps themselves are in TutorialDirector.BuildSteps; the words are in the
// Tutorial Texts asset; the timings in GameBalance > Tutorial.
// ============================================================================

using System;
using System.Collections.Generic;

namespace TheLastDrop
{
    /// <summary>Things the tutorial keeps locked until it has taught them.</summary>
    [Flags]
    public enum TutorialFeature
    {
        None = 0,
        /// <summary>The hand pump.</summary>
        Pump = 1,
        /// <summary>The hub tap (drink / refill).</summary>
        Tap = 2,
        /// <summary>Lock basins (pouring, the gate valve).</summary>
        Gates = 4,
        /// <summary>Everything out in the maze: valve grates, room doors, notes, room water.</summary>
        Maze = 8,
        /// <summary>The map (M, and the Map page of the Tab menu).</summary>
        Map = 16,
        /// <summary>The Build and Skills pages of the Tab menu.</summary>
        BuildSkills = 32,
        /// <summary>Everything.</summary>
        All = Pump | Tap | Gates | Maze | Map | BuildSkills
    }

    /// <summary>One main step of the tutorial.</summary>
    public class TutorialStep
    {
        /// <summary>Its name, also the key of its words in the Tutorial Texts asset.</summary>
        public string Id;
        /// <summary>What becomes available when this step starts (and stays available).</summary>
        public TutorialFeature Unlocks;
        /// <summary>True once the player has done it.</summary>
        public Func<bool> IsDone;
        /// <summary>True when it can't be done right now (dry pump, empty storage ...): the step passes by itself.</summary>
        public Func<bool> CannotBeDone;
        /// <summary>True for a "just read this" step: it finishes after Read Seconds of its card on screen.</summary>
        public bool ReadOnly;
        /// <summary>Card time before a "read" step finishes (seconds).</summary>
        public float ReadSeconds;
        /// <summary>No "Done" tick (the goal step: the start card already said it).</summary>
        public bool Silent;
        /// <summary>Called when the step starts (to note where things were, e.g. the Raw Basin level).</summary>
        public Action OnEnter;
    }

    /// <summary>A one-time lesson shown the first time something happens.</summary>
    public class TutorialLesson
    {
        /// <summary>Its name, also the key of its words in the Tutorial Texts asset.</summary>
        public string Id;
        /// <summary>True when the thing happens (the lesson then shows once).</summary>
        public Func<bool> Trigger;
        /// <summary>True once the player has done what it says (closes it early). May be null.</summary>
        public Func<bool> IsDone;
        /// <summary>Seconds it stays on screen (0 = the flow's default).</summary>
        public float Seconds;
    }

    /// <summary>What the tutorial saves.</summary>
    [Serializable]
    public class TutorialSaveState
    {
        public TutorialFlow.Mode mode = TutorialFlow.Mode.Off;
        public string stepId = "";
        public int extraUnlocks;
        public List<string> seen = new List<string>();
    }

    /// <summary>Runs the main steps and the side lessons (plain C#, tested by TutorialFlowTests).</summary>
    public class TutorialFlow
    {
        /// <summary>Off = nothing shows. Running = main steps going. Finished = only side lessons left.</summary>
        public enum Mode { Off, Running, Finished }

        readonly List<TutorialStep> steps = new List<TutorialStep>();
        readonly List<TutorialLesson> lessons = new List<TutorialLesson>();
        readonly HashSet<string> seen = new HashSet<string>();
        TutorialFeature extraUnlocks;

        /// <summary>Seconds the "Done" tick shows.</summary>
        public float DoneSeconds = 1.2f;
        /// <summary>Seconds on one step before IsStuck turns true.</summary>
        public float StuckSeconds = 90f;
        /// <summary>Default seconds a side lesson stays.</summary>
        public float LessonSeconds = 9f;
        /// <summary>A side lesson stays at least this long, even once done.</summary>
        public float LessonMinSeconds = 2.5f;
        /// <summary>How often the side lesson triggers are checked (seconds).</summary>
        public float LessonCheckInterval = 0.25f;

        /// <summary>Off, Running or Finished.</summary>
        public Mode State { get; private set; } = Mode.Off;
        /// <summary>Index of the current main step (steps.Count once finished).</summary>
        public int Index { get; private set; }
        /// <summary>The current main step (null when not running).</summary>
        public TutorialStep Current => State == Mode.Running && Index < steps.Count ? steps[Index] : null;
        /// <summary>Seconds the current step has been going.</summary>
        public float StepTime { get; private set; }
        /// <summary>True while the "Done" tick of the step just finished shows.</summary>
        public bool ShowingDone { get; private set; }
        /// <summary>The side lesson on screen (null if none).</summary>
        public TutorialLesson CurrentLesson { get; private set; }
        /// <summary>True when the player has been on this step long enough to be offered a skip.</summary>
        public bool IsStuck => Current != null && !ShowingDone && StepTime >= StuckSeconds;
        /// <summary>The main steps, in order.</summary>
        public IReadOnlyList<TutorialStep> Steps => steps;

        /// <summary>A step started (also after a resume).</summary>
        public event Action<TutorialStep> StepStarted;
        /// <summary>A step was done (the "Done" tick starts).</summary>
        public event Action<TutorialStep> StepCompleted;
        /// <summary>Every main step is done.</summary>
        public event Action Finished;
        /// <summary>A side lesson started.</summary>
        public event Action<TutorialLesson> LessonStarted;
        /// <summary>A side lesson ended.</summary>
        public event Action<TutorialLesson> LessonEnded;

        float doneTimer;
        float lessonTime;
        float lessonCheckTimer;

        // ------------------------------------------------------------------ Building

        /// <summary>Adds a main step at the end.</summary>
        public TutorialStep Add(TutorialStep step)
        {
            steps.Add(step);
            return step;
        }

        /// <summary>Adds a side lesson (checked in the order added).</summary>
        public TutorialLesson AddLesson(TutorialLesson lesson)
        {
            lessons.Add(lesson);
            return lesson;
        }

        /// <summary>Index of a step by its id, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].Id == id) return i;
            return -1;
        }

        // ------------------------------------------------------------------ Starting and stopping

        /// <summary>Starts the main steps from <paramref name="index"/> (0 = the beginning).</summary>
        public void Begin(int index = 0)
        {
            if (steps.Count == 0) { State = Mode.Finished; return; }
            State = Mode.Running;
            EnterStep(Math.Max(0, Math.Min(index, steps.Count - 1)));
        }

        /// <summary>Skip tutorial: everything unlocks, nothing more shows.</summary>
        public void SkipAll()
        {
            bool lessonUp = CurrentLesson != null;
            var lesson = CurrentLesson;
            CurrentLesson = null;
            ShowingDone = false;
            State = Mode.Off;
            if (lessonUp) LessonEnded?.Invoke(lesson);
        }

        /// <summary>Skips the current step only (the stuck offer). It counts as done.</summary>
        public void SkipStep()
        {
            if (Current == null || ShowingDone) return;
            Complete();
        }

        /// <summary>Unlocks a feature early (e.g. Build and Skills at the first dawn).</summary>
        public void Unlock(TutorialFeature feature) => extraUnlocks |= feature;

        /// <summary>Marks a side lesson as already shown (e.g. a main step taught the same thing).</summary>
        public void MarkSeen(string id) { if (!string.IsNullOrEmpty(id)) seen.Add(id); }

        /// <summary>True if that side lesson (or one-time screen) has been shown.</summary>
        public bool HasSeen(string id) => seen.Contains(id);

        // ------------------------------------------------------------------ Locks

        /// <summary>True if the feature may be used now. Everything is allowed unless the main steps are running.</summary>
        public bool Allows(TutorialFeature feature)
        {
            if (State != Mode.Running) return true;
            return (Unlocked & feature) == feature;
        }

        /// <summary>Everything unlocked so far (all steps up to and including the current one, plus early unlocks).</summary>
        public TutorialFeature Unlocked
        {
            get
            {
                if (State != Mode.Running) return TutorialFeature.All;
                var f = extraUnlocks;
                for (int i = 0; i <= Index && i < steps.Count; i++) f |= steps[i].Unlocks;
                return f;
            }
        }

        // ------------------------------------------------------------------ Every frame

        /// <summary>
        /// Moves the tutorial on. <paramref name="cardVisible"/> = the card can be seen now (no
        /// pause, minigame, loading or start card in front): "read" steps and side lessons only
        /// count time while it is.
        /// </summary>
        public void Tick(float dt, bool cardVisible)
        {
            if (State == Mode.Off) return;

            if (State == Mode.Running) TickStep(dt, cardVisible);
            TickLessons(dt, cardVisible);
        }

        void TickStep(float dt, bool cardVisible)
        {
            if (ShowingDone)
            {
                doneTimer -= dt;
                if (doneTimer <= 0f) Advance();
                return;
            }

            var step = Current;
            if (step == null) return;
            StepTime += dt;

            if (step.ReadOnly)
            {
                // Only time the player could actually read the card counts.
                if (cardVisible && CurrentLesson == null) readTime += dt;
                if (readTime >= step.ReadSeconds) Complete();
                return;
            }

            if (Check(step.IsDone) || Check(step.CannotBeDone)) Complete();
        }

        float readTime;

        void Complete()
        {
            var step = Current;
            if (step == null) return;
            StepCompleted?.Invoke(step);
            if (step.Silent) { Advance(); return; }
            ShowingDone = true;
            doneTimer = DoneSeconds;
        }

        void Advance()
        {
            ShowingDone = false;
            int next = Index + 1;
            if (next >= steps.Count)
            {
                Index = steps.Count;
                State = Mode.Finished;
                Finished?.Invoke();
                return;
            }
            EnterStep(next);
        }

        void EnterStep(int index)
        {
            Index = index;
            StepTime = 0f;
            readTime = 0f;
            ShowingDone = false;
            var step = steps[index];
            try { step.OnEnter?.Invoke(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[Tutorial] Step {step.Id} start failed: {e.Message}"); }
            StepStarted?.Invoke(step);
            // A step already done when it starts (e.g. after a resume) passes on the next tick.
        }

        void TickLessons(float dt, bool cardVisible)
        {
            if (CurrentLesson != null)
            {
                if (!cardVisible) return; // wait behind the menu / minigame / pause
                lessonTime += dt;
                float limit = CurrentLesson.Seconds > 0f ? CurrentLesson.Seconds : LessonSeconds;
                bool learnt = CurrentLesson.IsDone != null && Check(CurrentLesson.IsDone);
                if (lessonTime >= limit || (learnt && lessonTime >= LessonMinSeconds))
                {
                    var ended = CurrentLesson;
                    CurrentLesson = null;
                    LessonEnded?.Invoke(ended);
                }
                return;
            }

            if (!cardVisible || ShowingDone) return;
            lessonCheckTimer -= dt;
            if (lessonCheckTimer > 0f) return;
            lessonCheckTimer = LessonCheckInterval;

            for (int i = 0; i < lessons.Count; i++)
            {
                var l = lessons[i];
                if (seen.Contains(l.Id) || !Check(l.Trigger)) continue;
                seen.Add(l.Id);
                CurrentLesson = l;
                lessonTime = 0f;
                LessonStarted?.Invoke(l);
                return;
            }
        }

        // A broken check must never break the game: treat it as "not yet".
        static bool Check(Func<bool> f)
        {
            if (f == null) return false;
            try { return f(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Tutorial] A check failed: " + e.Message); return false; }
        }

        // ------------------------------------------------------------------ Saving

        /// <summary>The state to save.</summary>
        public TutorialSaveState Capture()
        {
            var s = new TutorialSaveState
            {
                mode = State,
                // Mid-"Done" counts as the next step, so a load doesn't repeat a finished step.
                stepId = State == Mode.Running && Index < steps.Count
                    ? steps[ShowingDone && Index + 1 < steps.Count ? Index + 1 : Index].Id
                    : "",
                extraUnlocks = (int)extraUnlocks,
                seen = new List<string>(seen)
            };
            return s;
        }

        /// <summary>Puts the saved state back. The saved step starts again from its beginning.</summary>
        public void Restore(TutorialSaveState s)
        {
            seen.Clear();
            CurrentLesson = null;
            ShowingDone = false;
            if (s == null) { State = Mode.Off; return; }
            if (s.seen != null) foreach (var id in s.seen) seen.Add(id);
            extraUnlocks = (TutorialFeature)s.extraUnlocks;

            switch (s.mode)
            {
                case Mode.Running:
                    int i = IndexOf(s.stepId);
                    Begin(i < 0 ? 0 : i);
                    break;
                case Mode.Finished:
                    State = Mode.Finished;
                    Index = steps.Count;
                    break;
                default:
                    State = Mode.Off;
                    break;
            }
        }
    }
}
