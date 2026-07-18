using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What the bartender can see about one customer this visit: six readings of varying
    /// clarity plus the one thing that is never hidden — what they want done about it
    /// (GDD 19 §3). The true values live in <see cref="RegularState.Stats"/>; this is only
    /// the view, and the view goes stale between visits.
    /// </summary>
    public sealed class CustomerRead
    {
        private readonly StatReading[] _readings = new StatReading[Emotions.Count];

        /// <summary>The stat the customer came here about. Always visible.</summary>
        public Emotion Intent { get; }

        /// <summary>Which way they want it moved. Always visible.</summary>
        public IntentDirection Direction { get; }

        public CustomerRead(IReadOnlyList<StatReading> readings, Emotion intent, IntentDirection direction)
        {
            if (readings == null) throw new ArgumentNullException(nameof(readings));
            if (readings.Count != Emotions.Count)
                throw new ArgumentException($"Expected {Emotions.Count} readings.", nameof(readings));
            for (int i = 0; i < _readings.Length; i++) _readings[i] = readings[i];
            Intent = intent;
            Direction = direction;
        }

        public StatReading this[Emotion emotion] => _readings[(int)emotion];

        /// <summary>The reading for the stat that actually matters this visit.</summary>
        public StatReading IntentReading => _readings[(int)Intent];

        /// <summary>True when the player is aiming at a stat they cannot see — the lucky-read case.</summary>
        public bool IsBlindRead => IntentReading.IsBlind;

        /// <summary>The value the intent is aiming at: 0 for Extinguish, 100 for Fuel.</summary>
        public int TargetValue => Emotions.TargetValue(Direction);

        /// <summary>Tightens one reading — Chat, the Empath patron, Eavesdrop (GDD 19 §8).</summary>
        public CustomerRead Narrowing(Emotion emotion, int trueValue, int halfWidth)
        {
            var next = new StatReading[Emotions.Count];
            Array.Copy(_readings, next, next.Length);
            next[(int)emotion] = _readings[(int)emotion].Narrowed(trueValue, halfWidth);
            return new CustomerRead(next, Intent, Direction);
        }

        public override string ToString()
        {
            var parts = new List<string>(Emotions.Count);
            foreach (var emotion in Emotions.All) parts.Add($"{emotion}:{_readings[(int)emotion]}");
            return $"{Direction} {Intent} | {string.Join(" ", parts)}";
        }
    }
}
