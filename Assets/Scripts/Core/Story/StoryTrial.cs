using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What the story's customer actually wants (GDD 26 §4, the author's rework 2026-08-13):
    /// not one drink but a RUN of them, against one clock, to a standard nothing else in this
    /// game asks for. An inspector's visit, in the shape of a service.
    ///
    /// The standard is the design and is not a data field: **exactly the drink, made exactly
    /// the way the book says, garnished exactly as asked**. The one part that is forgiving is
    /// the fill, because a glass poured to nine tenths is a poured glass and nobody counts the
    /// last millimetre — that threshold is <see cref="MinFill"/>, and it is the only softness
    /// in here on purpose.
    ///
    /// Content, like the beat that owns it: built once, shared, never mutated. What a run is
    /// doing with it lives in <see cref="StoryTrialRun"/>.
    /// </summary>
    public sealed class StoryTrial
    {
        /// <summary>Nine tenths of the glass. Below it the drink is short, above it nobody is
        /// counting — the ordinary service expects 0.80, so a trial is stricter here too, but
        /// it does not ask for the brim.</summary>
        public const double DefaultMinFill = 0.90;

        /// <summary>Seconds of talking after which the trial starts itself. A backstop, not a
        /// mechanic: real dialogue ends in a fraction of this, but a clock that can be held
        /// forever is a night that can never close, and Core does not trust the UI with that
        /// (the house rule). Generous enough that no player ever meets it reading.</summary>
        public const double TalkingGrace = 120.0;

        /// <summary>The drinks, IN ORDER. They are revealed one at a time (§4): the player
        /// sees the one they are making, and what comes after it is the guest's to say.</summary>
        public IReadOnlyList<RecipeDefinition> Asks { get; }

        /// <summary>One clock for the whole trial, started when the talking stops. Not a
        /// patience — a deadline, and the difference is the point.</summary>
        public double Seconds { get; }

        /// <summary>How full the glass has to come out. The forgiving part.</summary>
        public double MinFill { get; }

        /// <summary>How many drinks may be wrong before the visit is a failure. Zero is the
        /// inspector's standard; the early beats are written kinder than that. A refused drink
        /// does not remove the ask — it costs a mistake and the time to make another.</summary>
        public int AllowedMistakes { get; }

        public StoryTrial(IReadOnlyList<RecipeDefinition> asks, double seconds,
            double minFill = DefaultMinFill, int allowedMistakes = 0)
        {
            if (asks == null) throw new ArgumentNullException(nameof(asks));
            if (asks.Count == 0)
                throw new ArgumentException("A trial with nothing to pour is not a trial.", nameof(asks));
            foreach (var ask in asks)
                if (ask == null) throw new ArgumentException("A trial asks for a drink that does not exist.",
                    nameof(asks));
            if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds),
                "A trial needs time to be a trial.");
            if (minFill <= 0 || minFill > 1) throw new ArgumentOutOfRangeException(nameof(minFill));
            if (allowedMistakes < 0) throw new ArgumentOutOfRangeException(nameof(allowedMistakes));

            Asks = new List<RecipeDefinition>(asks);
            Seconds = seconds;
            MinFill = minFill;
            AllowedMistakes = allowedMistakes;
        }

        /// <summary>The drink the whole visit is remembered by — the first one asked for.
        /// What Ece warns about a week early, and what the beat is named for.</summary>
        public RecipeDefinition Headline => Asks[0];

        public override string ToString() =>
            $"{Asks.Count} drinks in {Seconds:0}s, {AllowedMistakes} mistakes allowed";
    }
}
