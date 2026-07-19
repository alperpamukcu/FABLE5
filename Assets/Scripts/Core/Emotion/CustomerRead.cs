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

        /// <summary>
        /// How hard they are to please (GDD 20 §2.1). Always visible — hidden difficulty is
        /// unfair, visible difficulty is tension.
        /// </summary>
        public DemandLevel Demand { get; }

        /// <summary>
        /// What kind of drink they want to be holding (GDD 21 §5). Always visible, like the
        /// intent — it is a second thing to aim at, not a second thing to guess.
        /// </summary>
        public FillPreference FillPreference { get; }

        public CustomerRead(IReadOnlyList<StatReading> readings, Emotion intent,
            IntentDirection direction, DemandLevel demand = DemandLevel.Easygoing,
            FillPreference fillPreference = default)
        {
            if (readings == null) throw new ArgumentNullException(nameof(readings));
            if (readings.Count != Emotions.Count)
                throw new ArgumentException($"Expected {Emotions.Count} readings.", nameof(readings));
            for (int i = 0; i < _readings.Length; i++) _readings[i] = readings[i];
            Intent = intent;
            Direction = direction;
            Demand = demand;
            FillPreference = fillPreference;
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
            return new CustomerRead(next, Intent, Direction, Demand, FillPreference);
        }

        /// <summary>
        /// Replaces one reading outright — used to plant a false one (GDD 19 §8, The Liar).
        /// The tier is unchanged, so a lie looks exactly as trustworthy as the truth.
        /// </summary>
        public CustomerRead Replacing(Emotion emotion, StatReading reading)
        {
            var next = new StatReading[Emotions.Count];
            Array.Copy(_readings, next, next.Length);
            next[(int)emotion] = reading;
            return new CustomerRead(next, Intent, Direction, Demand, FillPreference);
        }

        /// <summary>
        /// The stat the bartender is most in the dark about, for effects that narrow "one
        /// reading" without naming it. Unknown beats Range, and the intent stat wins ties —
        /// information about what they actually came in for is always worth more.
        /// Returns false when every reading is already Exact.
        /// </summary>
        public bool TryPickDarkest(out Emotion darkest)
        {
            darkest = Intent;
            int best = -1;

            foreach (var emotion in Emotions.All)
            {
                var tier = _readings[(int)emotion].Tier;
                if (tier == VisibilityTier.Exact) continue;

                // Unknown (2) outranks Range (1); +1 more for the stat that matters tonight.
                int score = (tier == VisibilityTier.Unknown ? 2 : 1) * 2 + (emotion == Intent ? 1 : 0);
                if (score > best)
                {
                    best = score;
                    darkest = emotion;
                }
            }
            return best >= 0;
        }

        public override string ToString()
        {
            var parts = new List<string>(Emotions.Count);
            foreach (var emotion in Emotions.All) parts.Add($"{emotion}:{_readings[(int)emotion]}");
            return $"{Direction} {Intent} | {string.Join(" ", parts)}";
        }
    }
}
