using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One emotional charge printed on an ingredient card (GDD 19 §4). Signed:
    /// negative pulls the stat toward 0, positive pushes it toward 100. Charges are
    /// per-card identity — a Tool that rewrites a card's Type leaves them alone.
    /// </summary>
    public readonly struct EmotionCharge : IEquatable<EmotionCharge>
    {
        public Emotion Emotion { get; }
        public int Amount { get; }

        public EmotionCharge(Emotion emotion, int amount)
        {
            Emotion = emotion;
            Amount = amount;
        }

        public bool Equals(EmotionCharge other) => Emotion == other.Emotion && Amount == other.Amount;
        public override bool Equals(object obj) => obj is EmotionCharge other && Equals(other);
        public override int GetHashCode() => ((int)Emotion * 397) ^ Amount;
        public override string ToString() => $"{Emotion} {Amount:+#;-#;0}";
    }

    /// <summary>
    /// The summed movement one Mix applies to a customer, already multiplied and rounded
    /// (GDD 19 §5). Produced by <see cref="EmotionResolver"/>; consumed by
    /// <see cref="ResonanceJudge"/> and, if the serve is not a bust, by the customer's stats.
    /// </summary>
    public sealed class EmotionDelta
    {
        private readonly int[] _values = new int[Emotions.Count];

        public int this[Emotion emotion] => _values[(int)emotion];

        /// <summary>
        /// A fresh zero delta. Deliberately not a cached singleton — deltas are mutable, and
        /// a shared "empty" would be one stray <see cref="Add"/> away from corrupting
        /// every code path that returns it.
        /// </summary>
        public static EmotionDelta Empty => new EmotionDelta();

        public EmotionDelta() { }

        public EmotionDelta(IReadOnlyList<EmotionCharge> charges)
        {
            if (charges == null) return;
            foreach (var charge in charges) _values[(int)charge.Emotion] += charge.Amount;
        }

        public void Add(Emotion emotion, int amount) => _values[(int)emotion] += amount;

        public void Add(EmotionDelta other)
        {
            if (other == null) return;
            for (int i = 0; i < _values.Length; i++) _values[i] += other._values[i];
        }

        /// <summary>Scales every component and rounds once, so exact landings stay reachable (GDD 19 §5).</summary>
        public EmotionDelta Scaled(double multiplier)
        {
            var scaled = new EmotionDelta();
            for (int i = 0; i < _values.Length; i++)
                scaled._values[i] = (int)Math.Round(_values[i] * multiplier, MidpointRounding.AwayFromZero);
            return scaled;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (int value in _values) if (value != 0) return false;
                return true;
            }
        }
    }
}
