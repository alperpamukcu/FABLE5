using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// Tier assignment, staleness and relationship (GDD 19 §3/§10). Determinism matters as
    /// much as the rules here: a seed must reproduce who walked in and what you could read.
    /// </summary>
    public class CustomerReadTests
    {
        private static EmotionStats Stats(int value = 50)
        {
            var stats = new EmotionStats();
            foreach (var emotion in Emotions.All) stats.Set(emotion, value);
            return stats;
        }

        private static SeededRng Rng(string seed = "read-test") =>
            new RunRng(seed).GetStream("read");

        [Test]
        public void EveryCard_HasOneExact_ThreeRanges_AndTwoUnknowns()
        {
            var read = CustomerReadFactory.Build(Stats(), night: 1, Rng());

            int exact = 0, range = 0, unknown = 0;
            foreach (var emotion in Emotions.All)
            {
                switch (read[emotion].Tier)
                {
                    case VisibilityTier.Exact: exact++; break;
                    case VisibilityTier.Range: range++; break;
                    default: unknown++; break;
                }
            }

            Assert.AreEqual(CustomerReadFactory.ExactCount, exact);
            Assert.AreEqual(CustomerReadFactory.RangeCount, range);
            Assert.AreEqual(CustomerReadFactory.UnknownCount, unknown);
        }

        [Test]
        public void RangeBands_WidenAsTheWeekGetsLater()
        {
            Assert.AreEqual(8, CustomerReadFactory.HalfWidthForNight(1));
            Assert.AreEqual(12, CustomerReadFactory.HalfWidthForNight(4));
            Assert.AreEqual(16, CustomerReadFactory.HalfWidthForNight(8));
        }

        [Test]
        public void KnowingSomeone_TightensTheBands()
        {
            int stranger = CustomerReadFactory.HalfWidthFor(8, Relationship.Stranger);
            int confidant = CustomerReadFactory.HalfWidthFor(8, Relationship.Confidant);

            Assert.Less(confidant, stranger);
            Assert.GreaterOrEqual(confidant, 2, "never collapses to a free Exact");
        }

        [Test]
        public void ARangeReading_AlwaysContainsTheTruth()
        {
            var truth = Stats(37);
            var read = CustomerReadFactory.Build(truth, night: 5, Rng());

            foreach (var emotion in Emotions.All)
            {
                if (read[emotion].Tier != VisibilityTier.Range) continue;
                Assert.IsTrue(read[emotion].Contains(truth[emotion]),
                    $"{emotion} band {read[emotion]} must contain {truth[emotion]}");
            }
        }

        [Test]
        public void SameSeed_ReproducesTheSameCard()
        {
            var a = CustomerReadFactory.Build(Stats(), 3, Rng("seed-a"));
            var b = CustomerReadFactory.Build(Stats(), 3, Rng("seed-a"));

            Assert.AreEqual(a.Intent, b.Intent);
            Assert.AreEqual(a.Direction, b.Direction);
            foreach (var emotion in Emotions.All)
                Assert.AreEqual(a[emotion], b[emotion], emotion.ToString());
        }

        [Test]
        public void Intent_IsNeverImpossible()
        {
            // A stat already pinned at 0 cannot be asked to go lower.
            var pinned = new EmotionStats();
            for (int i = 0; i < 40; i++)
            {
                var read = CustomerReadFactory.Build(pinned, 1, Rng($"pinned-{i}"));
                Assert.AreEqual(IntentDirection.Fuel, read.Direction,
                    "everything is at 0, so every intent must point up");
            }
        }

        [Test]
        public void Narrowing_TightensOneReadingOnly()
        {
            var truth = Stats(44);
            var read = new CustomerRead(
                new List<StatReading>
                {
                    StatReading.Unknown, StatReading.Unknown, StatReading.Unknown,
                    StatReading.Unknown, StatReading.Unknown, StatReading.Unknown
                },
                Emotion.Sadness, IntentDirection.Extinguish);

            var learned = read.Narrowing(Emotion.Sadness, truth[Emotion.Sadness], 8);

            Assert.AreEqual(VisibilityTier.Range, learned[Emotion.Sadness].Tier);
            Assert.IsTrue(learned[Emotion.Sadness].Contains(44));
            Assert.AreEqual(VisibilityTier.Unknown, learned[Emotion.Anger].Tier, "untouched");
        }

        [Test]
        public void ReadingsDecay_OneStepAtATime()
        {
            Assert.AreEqual(VisibilityTier.Range, StatReading.Exact(50).Decayed(50, 8).Tier);
            Assert.AreEqual(VisibilityTier.Unknown, StatReading.Range(50, 8).Decayed(50, 8).Tier);
            Assert.AreEqual(VisibilityTier.Unknown, StatReading.Unknown.Decayed(50, 8).Tier);
        }
    }

    public class RegularsRegistryTests
    {
        private static ArchetypeDefinition Archetype(string id = "regular", int weight = 1)
        {
            var bands = new List<EmotionBand>();
            foreach (var _ in Emotions.All) bands.Add(new EmotionBand(30, 60));
            return new ArchetypeDefinition(id, id, bands, new[] { "Sam", "Rae" }, weight);
        }

        private static RegularsRegistry Registry(int returnChance = 55) =>
            new RegularsRegistry(new[] { Archetype() }, returnChance);

        private static SeededRng Rng(string seed = "reg") => new RunRng(seed).GetStream("customer");

        [Test]
        public void TheFirstCustomer_IsAlwaysAStranger()
        {
            var registry = Registry(returnChance: 100);

            var first = registry.RollNext(Rng());

            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(Relationship.Stranger, first.Relationship);
        }

        [Test]
        public void WithZeroReturnChance_EveryCustomerIsNew()
        {
            var registry = Registry(returnChance: 0);
            var rng = Rng();

            for (int i = 0; i < 5; i++) registry.RollNext(rng);

            Assert.AreEqual(5, registry.Count);
        }

        [Test]
        public void WithFullReturnChance_TheSameFacesComeBack()
        {
            var registry = Registry(returnChance: 100);
            var rng = Rng();

            for (int i = 0; i < 5; i++) registry.RollNext(rng);

            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void BaselinesRoll_InsideTheArchetypesBands()
        {
            var registry = Registry(returnChance: 0);
            var rng = Rng();

            for (int i = 0; i < 10; i++)
            {
                var regular = registry.RollNext(rng);
                foreach (var emotion in Emotions.All)
                {
                    Assert.GreaterOrEqual(regular.Baseline[emotion], 30);
                    Assert.LessOrEqual(regular.Baseline[emotion], 60);
                }
            }
        }

        [Test]
        public void SameSeed_ReproducesTheSameCast()
        {
            var a = Registry();
            var b = Registry();
            var rngA = Rng("cast");
            var rngB = Rng("cast");

            for (int i = 0; i < 8; i++)
            {
                var one = a.RollNext(rngA);
                var two = b.RollNext(rngB);
                Assert.AreEqual(one.Id, two.Id);
                Assert.AreEqual(one.Stats, two.Stats);
            }
        }

        [Test]
        public void SatisfyingSomeone_MovesTheRelationshipAlong()
        {
            var registry = Registry(returnChance: 0);
            var regular = registry.RollNext(Rng());

            Assert.AreEqual(Relationship.Stranger, regular.Relationship);
            regular.RecordVisit(2);
            Assert.AreEqual(Relationship.Familiar, regular.Relationship);
            regular.RecordVisit(2);
            regular.RecordVisit(2);
            Assert.AreEqual(Relationship.Regular, regular.Relationship);
        }

        [Test]
        public void AnUnsatisfyingVisit_StillCounts_ButEarnsNoStanding()
        {
            var registry = Registry(returnChance: 0);
            var regular = registry.RollNext(Rng());

            regular.RecordVisit(0);

            Assert.AreEqual(1, regular.Visits);
            Assert.AreEqual(Relationship.Stranger, regular.Relationship);
        }

        [Test]
        public void Drift_PullsBackTowardWhoTheyUsuallyAre()
        {
            var registry = Registry(returnChance: 0);
            var regular = registry.RollNext(Rng());
            foreach (var emotion in Emotions.All) regular.Stats.Set(emotion, 100);

            int before = regular.Stats[Emotion.Sadness];
            registry.DriftAll(new RunRng("drift-seed").GetStream("drift"));

            Assert.Less(regular.Stats[Emotion.Sadness], before,
                "a stat pushed far above baseline must come back down");
        }

        [Test]
        public void Drift_StalesWhatTheBartenderKnew()
        {
            var registry = Registry(returnChance: 0);
            var regular = registry.RollNext(Rng());
            regular.RememberTiers(new[]
            {
                VisibilityTier.Exact, VisibilityTier.Range, VisibilityTier.Range,
                VisibilityTier.Range, VisibilityTier.Unknown, VisibilityTier.Unknown
            });

            registry.DriftAll(new RunRng("s").GetStream("drift"));

            Assert.AreEqual(VisibilityTier.Range, regular.KnownTiers[0], "Exact decays to Range");
            Assert.AreEqual(VisibilityTier.Unknown, regular.KnownTiers[1], "Range decays to Unknown");
        }

        [Test]
        public void Drift_NeverLeavesTheLegalRange()
        {
            var registry = Registry(returnChance: 0);
            var rng = new RunRng("bounds").GetStream("drift");
            for (int i = 0; i < 5; i++) registry.RollNext(Rng($"r{i}"));

            for (int week = 0; week < 20; week++)
            {
                registry.DriftAll(rng);
                foreach (var regular in registry.All)
                    foreach (var emotion in Emotions.All)
                    {
                        Assert.GreaterOrEqual(regular.Stats[emotion], 0);
                        Assert.LessOrEqual(regular.Stats[emotion], 100);
                    }
            }
        }
    }

    public class WeekQuotaTests
    {
        [Test]
        public void MeetingTheQuotaExactly_Passes()
        {
            var quota = new WeekQuota(1, 6);
            quota.Add(6);

            Assert.IsTrue(quota.Met);
            Assert.AreEqual(0, quota.Remaining);
        }

        [Test]
        public void OneShortOfTheQuota_Fails()
        {
            var quota = new WeekQuota(1, 6);
            quota.Add(5);

            Assert.IsFalse(quota.Met);
            Assert.AreEqual(1, quota.Remaining);
        }

        [Test]
        public void TheCurveRises_AndKeepsRisingPastTheTable()
        {
            Assert.Less(QuotaTable.Standard(1), QuotaTable.Standard(2));
            Assert.Less(QuotaTable.Standard(3), QuotaTable.Standard(4));
            Assert.Less(QuotaTable.Standard(4), QuotaTable.Standard(9), "endless keeps climbing");
        }

        [Test]
        public void WeeksGroupNights_AndCloseOnTheLastOne()
        {
            Assert.AreEqual(1, QuotaTable.WeekOfNight(1));
            Assert.AreEqual(1, QuotaTable.WeekOfNight(QuotaTable.NightsPerWeek));
            Assert.AreEqual(2, QuotaTable.WeekOfNight(QuotaTable.NightsPerWeek + 1));

            Assert.IsFalse(QuotaTable.IsWeekEnd(1));
            Assert.IsTrue(QuotaTable.IsWeekEnd(QuotaTable.NightsPerWeek));
        }
    }
}
