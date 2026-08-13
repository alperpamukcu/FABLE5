using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What a beat's people say (GDD 26 §7). Core carries the words the way a recipe carries
    /// its name — it never renders one of them — but the beat is ONE object on purpose: lines
    /// filed in a second table keyed by the same string is precisely how the licence and the
    /// guide came to disagree about who was on the stool (2026-08-10), and a beat that has
    /// quietly lost its dialogue is a customer who sits down and says nothing.
    ///
    /// The house rule for the writing is in PLAN_last_call: two lines a beat, one sentence
    /// each. Nothing here enforces it — a rule about prose belongs to the person writing it.
    /// </summary>
    public sealed class StoryLines
    {
        /// <summary>A beat with nothing written yet: the silent phase (S1) runs on this.</summary>
        public static readonly StoryLines Silent = new StoryLines();

        /// <summary>What they sit down and ask for.</summary>
        public IReadOnlyList<string> Ask { get; }

        /// <summary>Said while the bar cannot pour it yet — it names what is missing (GDD 26 §4).</summary>
        public IReadOnlyList<string> Nudge { get; }

        public IReadOnlyList<string> ServedRight { get; }
        public IReadOnlyList<string> ServedWrong { get; }

        /// <summary>The answer to an honest no. Declining costs a night, not the arc.</summary>
        public IReadOnlyList<string> Declined { get; }

        /// <summary>
        /// The host, DAYS EARLY, on the quiet nights before this beat — the one warning the
        /// player gets about what the shelf will need (GDD 26 §4). It exists because the asks
        /// are revealed one at a time: the post-it cannot name what is missing in advance, so
        /// somebody has to, or the weekend is a wall instead of a deadline. It names the
        /// style in the market's own word, not a colour.
        /// </summary>
        public IReadOnlyList<string> HostWarning { get; }

        /// <summary>The host's framing, before the guest speaks and after they leave.</summary>
        public IReadOnlyList<string> HostBefore { get; }
        public IReadOnlyList<string> HostAfter { get; }

        public StoryLines(IReadOnlyList<string> ask = null, IReadOnlyList<string> nudge = null,
            IReadOnlyList<string> servedRight = null, IReadOnlyList<string> servedWrong = null,
            IReadOnlyList<string> declined = null, IReadOnlyList<string> hostBefore = null,
            IReadOnlyList<string> hostAfter = null, IReadOnlyList<string> hostWarning = null)
        {
            HostWarning = hostWarning ?? Array.Empty<string>();
            Ask = ask ?? Array.Empty<string>();
            Nudge = nudge ?? Array.Empty<string>();
            ServedRight = servedRight ?? Array.Empty<string>();
            ServedWrong = servedWrong ?? Array.Empty<string>();
            Declined = declined ?? Array.Empty<string>();
            HostBefore = hostBefore ?? Array.Empty<string>();
            HostAfter = hostAfter ?? Array.Empty<string>();
        }
    }
}
