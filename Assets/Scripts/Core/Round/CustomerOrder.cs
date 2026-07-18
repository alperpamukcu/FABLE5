using System;

namespace LastCall.Core
{
    /// <summary>
    /// One person at the bar, for one visit. Two layers sit here: the craft order (reach
    /// <see cref="TargetScore"/> before Mixes run out) and, after the pivot, the person —
    /// what they are carrying (<see cref="Regular"/>), how much of it you can see
    /// (<see cref="Read"/>) and what they want done about it.
    ///
    /// This is a *view*: the truth lives in <see cref="RegularState"/> and persists across
    /// the run. A new CustomerOrder is built every visit; the person is not.
    /// </summary>
    public sealed class CustomerOrder
    {
        public string Name { get; }
        public double TargetScore { get; }

        /// <summary>VIP rule shown on the order ticket (GDD 6); empty for regulars.</summary>
        public string RuleText { get; }

        /// <summary>The persistent person behind this visit; null in bench/legacy setups.</summary>
        public RegularState Regular { get; }

        /// <summary>What the ID card shows tonight; null when there is no emotional layer.</summary>
        public CustomerRead Read { get; private set; }

        /// <summary>
        /// Replaces the ID card after the bartender learns something — Chat, Eavesdrop, the
        /// Empath patron (GDD 19 §8). The person hasn't changed; the reading of them has.
        /// </summary>
        public void Learn(CustomerRead read)
        {
            if (read != null) Read = read;
        }

        /// <summary>True when this order carries the emotion layer (GDD 19).</summary>
        public bool HasEmotion => Regular != null && Read != null;

        public CustomerOrder(string name, double targetScore, string ruleText = null)
            : this(name, targetScore, null, null, ruleText) { }

        public CustomerOrder(string name, double targetScore,
            RegularState regular, CustomerRead read, string ruleText = null)
        {
            if (targetScore <= 0) throw new ArgumentOutOfRangeException(nameof(targetScore));
            Name = string.IsNullOrWhiteSpace(name) ? (regular?.Name ?? "Customer") : name;
            TargetScore = targetScore;
            RuleText = ruleText ?? string.Empty;
            Regular = regular;
            Read = read;
        }
    }
}
