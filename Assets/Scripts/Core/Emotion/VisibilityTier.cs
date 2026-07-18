using System;

namespace LastCall.Core
{
    /// <summary>How much of one stat the bartender can actually see this visit (GDD 19 §3).</summary>
    public enum VisibilityTier
    {
        /// <summary>The number is printed on the ID card.</summary>
        Exact,

        /// <summary>A band is shown; the true value sits somewhere inside it.</summary>
        Range,

        /// <summary>Nothing. Reading this one is a guess — and pays a lucky-read bonus.</summary>
        Unknown
    }

    /// <summary>
    /// One row of the customer ID card. For <see cref="VisibilityTier.Exact"/>, Low == High ==
    /// the true value. For Range, the band is clamped to 0–100, so a band's width alone can
    /// leak information near the ends — that is intentional and readable.
    /// </summary>
    public readonly struct StatReading : IEquatable<StatReading>
    {
        public VisibilityTier Tier { get; }
        public int Low { get; }
        public int High { get; }

        private StatReading(VisibilityTier tier, int low, int high)
        {
            Tier = tier;
            Low = low;
            High = high;
        }

        public static StatReading Exact(int value)
        {
            int clamped = EmotionStats.Clamp(value);
            return new StatReading(VisibilityTier.Exact, clamped, clamped);
        }

        public static StatReading Range(int trueValue, int halfWidth)
        {
            if (halfWidth < 0) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            return new StatReading(VisibilityTier.Range,
                EmotionStats.Clamp(trueValue - halfWidth),
                EmotionStats.Clamp(trueValue + halfWidth));
        }

        public static StatReading Unknown { get; } = new StatReading(VisibilityTier.Unknown, EmotionStats.Min, EmotionStats.Max);

        /// <summary>True when the player had no printed information to work from — pays the blind bonus.</summary>
        public bool IsBlind => Tier == VisibilityTier.Unknown;

        /// <summary>
        /// Tightens a reading by one step: Unknown → Range, Range → a narrower Range,
        /// Exact stays Exact. Used by Chat, the Empath patron and Relationship rank (GDD 19 §8/§10).
        /// </summary>
        public StatReading Narrowed(int trueValue, int halfWidth)
        {
            switch (Tier)
            {
                case VisibilityTier.Exact: return this;
                case VisibilityTier.Unknown: return Range(trueValue, halfWidth);
                default:
                    int tightened = Math.Max(0, Math.Min(halfWidth, (High - Low) / 2 - 1));
                    return tightened == 0 ? Exact(trueValue) : Range(trueValue, tightened);
            }
        }

        /// <summary>
        /// Loosens a reading by one step — the between-visit staleness decay (GDD 19 §10):
        /// what you knew last week is not what they are carrying tonight.
        /// </summary>
        public StatReading Decayed(int trueValue, int halfWidth)
        {
            switch (Tier)
            {
                case VisibilityTier.Exact: return Range(trueValue, halfWidth);
                case VisibilityTier.Range: return Unknown;
                default: return Unknown;
            }
        }

        public bool Contains(int value) => value >= Low && value <= High;

        public bool Equals(StatReading other) => Tier == other.Tier && Low == other.Low && High == other.High;
        public override bool Equals(object obj) => obj is StatReading other && Equals(other);
        public override int GetHashCode() => ((int)Tier * 397 ^ Low) * 397 ^ High;

        public override string ToString() =>
            Tier == VisibilityTier.Exact ? Low.ToString()
            : Tier == VisibilityTier.Range ? $"{Low}-{High}"
            : "??";
    }
}
