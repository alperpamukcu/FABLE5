using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Builds the per-visit customer ID card (GDD 19 §3): exactly one Exact reading, three
    /// Ranges and two Unknowns, shuffled per customer, with bands that widen as the week
    /// gets later and narrow as you get to know someone.
    /// Pure apart from the injected stream — callers pass <c>RunRng.GetStream("read")</c>.
    /// </summary>
    public static class CustomerReadFactory
    {
        public const int ExactCount = 1;
        public const int RangeCount = 3;
        public const int UnknownCount = 2;

        /// <summary>How far a Range band reaches either side of the truth, by night (GDD 19 §3).</summary>
        public static int HalfWidthForNight(int night) =>
            night <= 2 ? 8 : night <= 5 ? 12 : 16;

        /// <summary>Knowing someone tightens the bands you can read on them (GDD 19 §10).</summary>
        public static int HalfWidthFor(int night, Relationship relationship) =>
            Math.Max(2, HalfWidthForNight(night) - 3 * (int)relationship);

        public static CustomerRead Build(EmotionStats truth, int night, SeededRng rng,
            Relationship relationship = Relationship.Stranger,
            DemandLevel archetypeDemand = DemandLevel.Easygoing)
        {
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int halfWidth = HalfWidthFor(night, relationship);
            var tiers = ShuffledTiers(rng);
            var readings = new StatReading[Emotions.Count];

            for (int i = 0; i < readings.Length; i++)
            {
                var emotion = Emotions.All[i];
                switch (tiers[i])
                {
                    case VisibilityTier.Exact:
                        readings[i] = StatReading.Exact(truth[emotion]);
                        break;
                    case VisibilityTier.Range:
                        readings[i] = StatReading.Range(truth[emotion], halfWidth);
                        break;
                    default:
                        readings[i] = StatReading.Unknown;
                        break;
                }
            }

            var intent = RollIntent(truth, rng, out var direction);
            return new CustomerRead(readings, intent, direction,
                Demands.For(night, archetypeDemand), RollFillPreference(intent, rng));
        }

        /// <summary>
        /// The read for a face you have served before (GDD 19 §10). You look at them fresh
        /// tonight — a full tier roll — and whatever you still remember is merged in as a
        /// *floor*, taking the clearer of the two per stat.
        ///
        /// The merge is what makes a regular worth having. An earlier build decayed the
        /// remembered tiers and used them alone, which meant every returning customer got
        /// strictly less legible each week until the whole cast was blank — the opposite of
        /// the design, and a bust rate to match. Knowing someone must only ever help.
        /// </summary>
        public static CustomerRead FromTiers(EmotionStats truth, IReadOnlyList<VisibilityTier> remembered,
            int night, SeededRng rng, Relationship relationship = Relationship.Stranger,
            DemandLevel archetypeDemand = DemandLevel.Easygoing)
        {
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (remembered == null || remembered.Count != Emotions.Count)
                throw new ArgumentException($"Expected {Emotions.Count} tiers.", nameof(remembered));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int halfWidth = HalfWidthFor(night, relationship);
            var tonight = ShuffledTiers(rng);
            var readings = new StatReading[Emotions.Count];

            for (int i = 0; i < readings.Length; i++)
            {
                var emotion = Emotions.All[i];
                var tier = Clearer(tonight[i], remembered[i]);
                readings[i] = tier == VisibilityTier.Exact ? StatReading.Exact(truth[emotion])
                    : tier == VisibilityTier.Range ? StatReading.Range(truth[emotion], halfWidth)
                    : StatReading.Unknown;
            }

            var intent = RollIntent(truth, rng, out var direction);
            return new CustomerRead(readings, intent, direction,
                Demands.For(night, archetypeDemand), RollFillPreference(intent, rng));
        }

        /// <summary>Exact beats Range beats Unknown.</summary>
        private static VisibilityTier Clearer(VisibilityTier a, VisibilityTier b) =>
            (int)a <= (int)b ? a : b;

        /// <summary>
        /// How far a lie strays from the truth (GDD 19 §8, The Liar). Big enough that acting
        /// on it busts, not so big that it is obviously absurd.
        /// </summary>
        public const int LieOffset = 28;

        /// <summary>
        /// Applies a VIP's read rules to a finished card (GDD 19 §8). Blanket overrides come
        /// first, then the lie — so The Liar can plant a false reading on a licence that
        /// Open Book just made fully legible, and Poker Face has nothing left to lie about.
        /// </summary>
        public static CustomerRead ApplyVipRules(CustomerRead read, EmotionStats truth,
            VipRuleSet rules, int night, SeededRng rng, Relationship relationship = Relationship.Stranger)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (rules == null || !rules.HasAnyRule) return read;

            int halfWidth = HalfWidthFor(night, relationship);

            if (rules.ReadOverride != ReadOverride.None)
            {
                var readings = new StatReading[Emotions.Count];
                for (int i = 0; i < readings.Length; i++)
                    readings[i] = rules.ReadOverride == ReadOverride.AllExact
                        ? StatReading.Exact(truth[Emotions.All[i]])
                        : StatReading.Unknown;
                read = new CustomerRead(readings, read.Intent, read.Direction, read.Demand, read.FillPreference);
            }

            if (rules.OneReadingFalse)
            {
                // Only a legible reading can lie; if the licence says nothing, nothing lies.
                var legible = new List<Emotion>();
                foreach (var emotion in Emotions.All)
                    if (read[emotion].Tier != VisibilityTier.Unknown) legible.Add(emotion);

                if (legible.Count > 0)
                {
                    var target = legible[rng.NextInt(legible.Count)];
                    int actual = truth[target];
                    // Push the lie whichever way leaves room on the 0–100 scale.
                    int lied = actual <= 50 ? actual + LieOffset : actual - LieOffset;
                    var reading = read[target].Tier == VisibilityTier.Exact
                        ? StatReading.Exact(lied)
                        : StatReading.Range(lied, halfWidth);
                    read = read.Replacing(target, reading);
                }
            }

            return read;
        }

        /// <summary>The tier bag, Fisher-Yates shuffled on the caller's stream.</summary>
        public static VisibilityTier[] ShuffledTiers(SeededRng rng)
        {
            var bag = new VisibilityTier[Emotions.Count];
            int index = 0;
            for (int i = 0; i < ExactCount; i++) bag[index++] = VisibilityTier.Exact;
            for (int i = 0; i < RangeCount; i++) bag[index++] = VisibilityTier.Range;
            for (int i = 0; i < UnknownCount; i++) bag[index++] = VisibilityTier.Unknown;

            for (int i = bag.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            return bag;
        }

        /// <summary>
        /// What they came here about. Usually the loudest thing they are carrying, sometimes
        /// not — people don't always lead with the real problem. The direction follows the
        /// emotion's natural pull three times in four, and is forced when a stat is already
        /// pinned (you cannot extinguish a 0).
        /// </summary>
        private static Emotion RollIntent(EmotionStats truth, SeededRng rng, out IntentDirection direction)
        {
            var intent = rng.NextInt(100) < 60
                ? truth.Dominant
                : Emotions.All[rng.NextInt(Emotions.Count)];

            int value = truth[intent];
            if (value <= 5) direction = IntentDirection.Fuel;
            else if (value >= 95) direction = IntentDirection.Extinguish;
            else
            {
                bool natural = rng.NextInt(100) < 75;
                var pull = NaturalDirection(intent);
                direction = natural ? pull : Opposite(pull);
            }
            return intent;
        }

        /// <summary>
        /// What they want to be holding (GDD 21 §5). The stat it serves is deliberately not
        /// the intent stat wherever possible: two objectives pointing at the same number
        /// would collapse into one, and the second axis exists to give the player something
        /// else to aim at.
        /// </summary>
        private static FillPreference RollFillPreference(Emotion intent, SeededRng rng)
        {
            var length = (GlassLength)rng.NextInt(3);

            var candidates = new List<Emotion>(Emotions.Count - 1);
            foreach (var emotion in Emotions.All)
                if (emotion != intent) candidates.Add(emotion);

            return new FillPreference(length, candidates[rng.NextInt(candidates.Count)]);
        }

        /// <summary>Excitement is the one people come in wanting more of; the rest they want put down.</summary>
        private static IntentDirection NaturalDirection(Emotion emotion) =>
            emotion == Emotion.Excitement ? IntentDirection.Fuel : IntentDirection.Extinguish;

        private static IntentDirection Opposite(IntentDirection direction) =>
            direction == IntentDirection.Fuel ? IntentDirection.Extinguish : IntentDirection.Fuel;
    }
}
