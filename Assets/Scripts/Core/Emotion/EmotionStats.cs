using System;
using System.Collections.Generic;
using System.Text;

namespace LastCall.Core
{
    /// <summary>
    /// A customer's six emotion values, each clamped 0–100 (GDD 19 §1). Mutable, because a
    /// regular carries one instance across the whole run and drinks move it; every mutation
    /// goes through <see cref="Apply"/> or <see cref="Set"/> so clamping can't be skipped.
    /// </summary>
    public sealed class EmotionStats : IEquatable<EmotionStats>
    {
        public const int Min = 0;
        public const int Max = 100;

        private readonly int[] _values = new int[Emotions.Count];

        public EmotionStats() { }

        public EmotionStats(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != Emotions.Count)
                throw new ArgumentException($"Expected {Emotions.Count} values, got {values.Count}.", nameof(values));
            for (int i = 0; i < _values.Length; i++) _values[i] = Clamp(values[i]);
        }

        public int this[Emotion emotion]
        {
            get => _values[(int)emotion];
        }

        public void Set(Emotion emotion, int value) => _values[(int)emotion] = Clamp(value);

        /// <summary>
        /// Applies a delta in place. Returns the movement that actually happened per stat —
        /// clamping means a −20 against a value of 8 only moves 8, and the judge needs to
        /// know that to tell a clean landing from an overshoot.
        /// </summary>
        public EmotionDelta Apply(EmotionDelta delta)
        {
            var applied = new EmotionDelta();
            if (delta == null) return applied;

            foreach (var emotion in Emotions.All)
            {
                int before = _values[(int)emotion];
                int after = Clamp(before + delta[emotion]);
                _values[(int)emotion] = after;
                applied.Add(emotion, after - before);
            }
            return applied;
        }

        /// <summary>The stat the customer is most loudly carrying; ties break by enum order (GDD 19 §2).</summary>
        public Emotion Dominant
        {
            get
            {
                var dominant = Emotions.All[0];
                int best = _values[0];
                for (int i = 1; i < _values.Length; i++)
                {
                    if (_values[i] > best)
                    {
                        best = _values[i];
                        dominant = Emotions.All[i];
                    }
                }
                return dominant;
            }
        }

        /// <summary>A detached copy — used to snapshot "before" state for the judge.</summary>
        public EmotionStats Clone()
        {
            var copy = new EmotionStats();
            Array.Copy(_values, copy._values, _values.Length);
            return copy;
        }

        /// <summary>What this delta would produce, without touching this instance.</summary>
        public EmotionStats Projected(EmotionDelta delta)
        {
            var copy = Clone();
            copy.Apply(delta);
            return copy;
        }

        public static int Clamp(int value) => value < Min ? Min : value > Max ? Max : value;

        public bool Equals(EmotionStats other)
        {
            if (other == null) return false;
            for (int i = 0; i < _values.Length; i++)
                if (_values[i] != other._values[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as EmotionStats);

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (int value in _values) hash = hash * 31 + value;
            return hash;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (var emotion in Emotions.All)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(emotion).Append('=').Append(_values[(int)emotion]);
            }
            return text.ToString();
        }
    }
}
