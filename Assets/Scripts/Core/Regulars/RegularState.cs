using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One named person, remembered for the whole run (GDD 19 §10). Their stats are the
    /// truth behind every <see cref="CustomerRead"/>; drinks move them, time pulls them back
    /// toward who they usually are, and what the bartender learned about them goes stale.
    /// </summary>
    public sealed class RegularState
    {
        private readonly VisibilityTier[] _knownTiers = new VisibilityTier[Emotions.Count];

        public string Id { get; }
        public string Name { get; }
        public string ArchetypeId { get; }

        /// <summary>Live values — what they are carrying right now.</summary>
        public EmotionStats Stats { get; }

        /// <summary>Who they are when nothing has happened lately; drift pulls back to this.</summary>
        public EmotionStats Baseline { get; }

        public int Visits { get; private set; }
        public int SatisfiedCount { get; private set; }
        public Relationship Relationship { get; private set; } = Relationship.Stranger;

        /// <summary>Total satisfaction this person has contributed to the week's quota.</summary>
        public int SatisfactionEarned { get; private set; }

        /// <summary>What the bartender currently knows how to read on them; decays between visits.</summary>
        public IReadOnlyList<VisibilityTier> KnownTiers => _knownTiers;

        public RegularState(string id, string name, string archetypeId,
            EmotionStats stats, EmotionStats baseline)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Regular id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            ArchetypeId = archetypeId ?? string.Empty;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Baseline = baseline ?? stats.Clone();
            for (int i = 0; i < _knownTiers.Length; i++) _knownTiers[i] = VisibilityTier.Unknown;
        }

        /// <summary>Records the tiers this visit's ID card was built from, so they can decay later.</summary>
        public void RememberTiers(IReadOnlyList<VisibilityTier> tiers)
        {
            if (tiers == null || tiers.Count != _knownTiers.Length) return;
            for (int i = 0; i < _knownTiers.Length; i++) _knownTiers[i] = tiers[i];
        }

        /// <summary>Closes out a visit. Satisfaction is what the serve earned (GDD 19 §10).</summary>
        public void RecordVisit(int satisfaction)
        {
            Visits++;
            SatisfactionEarned += Math.Max(0, satisfaction);
            if (satisfaction > 0) SatisfiedCount++;
            Relationship = Relationships.ForSatisfiedVisits(SatisfiedCount);
        }

        /// <summary>
        /// Between-visit movement (GDD 19 §10): 35% of the way back to baseline, plus a small
        /// jitter, because life keeps happening while they are not at your bar. Known tiers
        /// slide one step toward Unknown — you knew them last week, not tonight.
        /// </summary>
        public void Drift(SeededRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            foreach (var emotion in Emotions.All)
            {
                int value = Stats[emotion];
                int pull = (int)Math.Round((Baseline[emotion] - value) * 0.35, MidpointRounding.AwayFromZero);
                int jitter = rng.NextInt(-5, 6);
                Stats.Set(emotion, value + pull + jitter);
            }

            for (int i = 0; i < _knownTiers.Length; i++)
                _knownTiers[i] = _knownTiers[i] == VisibilityTier.Exact
                    ? VisibilityTier.Range
                    : VisibilityTier.Unknown;
        }

        public override string ToString() => $"{Name} ({Relationship}, {Visits} visits) {Stats}";
    }
}
