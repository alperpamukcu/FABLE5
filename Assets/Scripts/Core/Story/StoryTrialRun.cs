using System;

namespace LastCall.Core
{
    /// <summary>Where a trial is (GDD 26 §4). It only ever moves forwards.</summary>
    public enum TrialState
    {
        /// <summary>They are talking. Nothing is being asked for yet and no clock is running.</summary>
        Talking,
        /// <summary>The ask is on the wall and the clock is going.</summary>
        Pouring,
        /// <summary>Every drink landed, to the standard.</summary>
        Passed,
        /// <summary>Time, mistakes, or an honest no.</summary>
        Failed,
    }

    /// <summary>
    /// One night's attempt at a <see cref="StoryTrial"/> — the state the post-it is drawn from
    /// (GDD 26 §4). It knows what is being asked for right now, how many have landed, how many
    /// were wrong, and how long is left; it decides nothing about drinks, because judging one
    /// is <see cref="ServiceJudge"/>'s job and the standard is <see cref="TycoonRun"/>'s to
    /// apply with it.
    ///
    /// The clock is NOT kept here. It is the guest's own patience, ticked where every other
    /// customer's is (<see cref="CustomerVisit.Tick"/>) — one clock in one place, so a trial
    /// cannot end at a different moment than the person sitting at the bar does.
    /// </summary>
    public sealed class StoryTrialRun
    {
        public StoryTrial Trial { get; }
        public TrialState State { get; private set; } = TrialState.Talking;

        /// <summary>Drinks that landed to the standard.</summary>
        public int Done { get; private set; }

        /// <summary>Drinks that did not. Over the allowance, the night is over.</summary>
        public int Mistakes { get; private set; }

        public int Total => Trial.Asks.Count;

        /// <summary>How long they have been talking. The rules layer does not trust the UI to
        /// end a conversation: a plate nobody dismisses would hold the clock, and a held clock
        /// means a night that can never close — so the trial starts itself after
        /// <see cref="StoryTrial.TalkingGrace"/>, long past any real dialogue.</summary>
        public double TalkedFor { get; private set; }

        public void Waited(double seconds)
        {
            if (State == TrialState.Talking && seconds > 0) TalkedFor += seconds;
        }

        /// <summary>What they are asking for right now, or null once it is over.</summary>
        public RecipeDefinition Current =>
            State == TrialState.Talking || State == TrialState.Pouring
                ? (Done < Total ? Trial.Asks[Done] : null)
                : null;

        /// <summary>The one after this, for nobody: the post-it shows the current ask only
        /// (§4). It is here for the arc's own bookkeeping and for tests.</summary>
        public bool IsLastAsk => Done == Total - 1;

        public bool IsOver => State == TrialState.Passed || State == TrialState.Failed;

        public StoryTrialRun(StoryTrial trial)
        {
            Trial = trial ?? throw new ArgumentNullException(nameof(trial));
        }

        /// <summary>The talking is done and the clock starts. Called once.</summary>
        public void Begin()
        {
            if (State != TrialState.Talking)
                throw new InvalidOperationException("That trial has already started.");
            State = TrialState.Pouring;
        }

        /// <summary>A drink landed to the standard. The last one passes the whole thing.</summary>
        public void Landed()
        {
            RequirePouring();
            Done++;
            if (Done >= Total) State = TrialState.Passed;
        }

        /// <summary>A drink did not. The ASK STAYS — they still want it — and the cost is a
        /// mistake and the time it takes to make another. Over the allowance, it is over.</summary>
        public void Fumbled()
        {
            RequirePouring();
            Mistakes++;
            if (Mistakes > Trial.AllowedMistakes) State = TrialState.Failed;
        }

        /// <summary>True when the night ended because the bar SAID SO rather than because the
        /// clock or the mistakes ran out. Three failures, three different things to say — and
        /// a guest who waited out their whole clock must not be answered with the line written
        /// for an honest no.</summary>
        public bool ToldNo { get; private set; }

        /// <summary>Time ran out, or the bar said no. Either way the night is spent.</summary>
        public void Fail(bool toldNo = false)
        {
            if (IsOver) return;
            ToldNo = toldNo;
            State = TrialState.Failed;
        }

        private void RequirePouring()
        {
            if (State != TrialState.Pouring)
                throw new InvalidOperationException(
                    State == TrialState.Talking
                        ? "They have not asked for anything yet."
                        : "That trial is already over.");
        }

        public override string ToString() => $"{State} {Done}/{Total} ({Mistakes} wrong)";
    }
}
