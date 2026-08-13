using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The moments the host is allowed to speak up on (GDD 26 §10). Deliberately a fixed
    /// vocabulary and not a scripting language: the game keeps a small table of conditions it
    /// can actually observe, the data picks one BY NAME and supplies the words, and a name
    /// nobody implements is refused when the file loads rather than staying silent forever.
    /// </summary>
    public enum StoryCue
    {
        /// <summary>The first door of the run.</summary>
        FirstNight,
        /// <summary>Nobody's card has been read yet.</summary>
        FirstLicence,
        /// <summary>Two spirits in the tin and no verb on them (Core's own MixRequired).</summary>
        TwoSpiritsInTheTin,
        FirstKeg,
        FirstMarket,
        /// <summary>A beat is due this week and the shelf is missing what it will ask for.</summary>
        CannotPourTheAsk,
        /// <summary>The night closed under the rent.</summary>
        RedNight,
        FirstExtraOrder,
    }

    public static class StoryCues
    {
        /// <summary>Reads a cue by the name the data uses. Null for anything else — the
        /// caller decides how loudly to complain, since it knows which file it came from.</summary>
        public static StoryCue? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "first_night": return StoryCue.FirstNight;
                case "first_licence": return StoryCue.FirstLicence;
                case "two_spirits_in_the_tin": return StoryCue.TwoSpiritsInTheTin;
                case "first_keg": return StoryCue.FirstKeg;
                case "first_market": return StoryCue.FirstMarket;
                case "cannot_pour_the_ask": return StoryCue.CannotPourTheAsk;
                case "red_night": return StoryCue.RedNight;
                case "first_extra_order": return StoryCue.FirstExtraOrder;
                default: return null;
            }
        }

        /// <summary>Every name the data may use, for a failure message worth reading.</summary>
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "first_night", "first_licence", "two_spirits_in_the_tin", "first_keg",
            "first_market", "cannot_pour_the_ask", "red_night", "first_extra_order",
        };
    }

    /// <summary>
    /// Something the host says the first time a condition is true (GDD 26 §1b/§10) — the
    /// tutorial, in her voice, instead of a tooltip. Each one fires once per run.
    /// </summary>
    public sealed class StoryLesson
    {
        public string Id { get; }
        public StoryCue Cue { get; }
        public IReadOnlyList<string> Say { get; }

        public StoryLesson(string id, StoryCue cue, IReadOnlyList<string> say)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A lesson needs an id.", nameof(id));
            if (say == null || say.Count == 0)
                throw new ArgumentException($"lesson '{id}' has nothing to say", nameof(say));
            Id = id;
            Cue = cue;
            Say = new List<string>(say);
        }

        public override string ToString() => $"{Id} ({Cue})";
    }
}
