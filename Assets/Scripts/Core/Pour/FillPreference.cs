using System;

namespace LastCall.Core
{
    /// <summary>What kind of drink they want in front of them (GDD 21 §5).</summary>
    public enum GlassLength
    {
        /// <summary>One sharp thing, then out.</summary>
        Short,

        /// <summary>An ordinary drink.</summary>
        Regular,

        /// <summary>Something to nurse, to sit with.</summary>
        Long
    }

    /// <summary>
    /// The second axis: not what the drink does to them, but what they want to be holding
    /// (GDD 21 §5). Landing inside the band moves <see cref="Serves"/>; missing it does
    /// nothing, because punishing both axes at once would make every serve a coin flip.
    ///
    /// **This is about length, never strength.** Volume comes from every ingredient, mixers
    /// included, so a Long drink can be 20% gin and 55% soda. That is what keeps GDD 19's
    /// tone guardrail intact under a mechanic that would otherwise reward pouring more
    /// alcohol — see PLAN_pour_pivot P5.
    /// </summary>
    public readonly struct FillPreference : IEquatable<FillPreference>
    {
        public GlassLength Length { get; }

        /// <summary>The emotion satisfied by getting the length right.</summary>
        public Emotion Serves { get; }

        /// <summary>How far the named emotion moves toward its target when the band is hit.</summary>
        public int Reward { get; }

        public FillPreference(GlassLength length, Emotion serves, int reward = 8)
        {
            Length = length;
            Serves = serves;
            Reward = reward;
        }

        public double MinFill => Bounds(Length).min;
        public double MaxFill => Bounds(Length).max;

        public bool IsSatisfiedBy(double fillFraction) =>
            fillFraction >= MinFill && fillFraction <= MaxFill;

        private static (double min, double max) Bounds(GlassLength length)
        {
            switch (length)
            {
                case GlassLength.Short: return (0.15, 0.45);
                case GlassLength.Regular: return (0.45, 0.75);
                default: return (0.75, 1.00);
            }
        }

        /// <summary>Three letters for tight UI: SHORT / MID / LONG.</summary>
        public string ShortLabel =>
            Length == GlassLength.Short ? "SHORT" : Length == GlassLength.Regular ? "MID" : "LONG";

        public string Label
        {
            get
            {
                switch (Length)
                {
                    case GlassLength.Short: return "something short";
                    case GlassLength.Regular: return "an ordinary drink";
                    default: return "something to nurse";
                }
            }
        }

        public bool Equals(FillPreference other) =>
            Length == other.Length && Serves == other.Serves && Reward == other.Reward;

        public override bool Equals(object obj) => obj is FillPreference other && Equals(other);
        public override int GetHashCode() => ((int)Length * 397 ^ (int)Serves) * 397 ^ Reward;
        public override string ToString() => $"{Label} ({MinFill:P0}–{MaxFill:P0}) → {Serves}";
    }
}
