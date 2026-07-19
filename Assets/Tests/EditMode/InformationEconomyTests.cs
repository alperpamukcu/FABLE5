using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The third purchase axis (GDD 19 §8): patrons, tools and VIP rules that buy or deny
    /// clarity rather than points. The invariant across all of them is that they change the
    /// <see cref="CustomerRead"/> and never the customer's actual stats.
    /// </summary>
    public class InformationEconomyTests
    {
        private static EmotionStats Stats(int value = 50)
        {
            var stats = new EmotionStats();
            foreach (var emotion in Emotions.All) stats.Set(emotion, value);
            return stats;
        }

        private static CustomerRead ReadOf(params VisibilityTier[] tiers)
        {
            var readings = new List<StatReading>();
            for (int i = 0; i < Emotions.Count; i++)
                readings.Add(tiers[i] == VisibilityTier.Exact ? StatReading.Exact(50)
                    : tiers[i] == VisibilityTier.Range ? StatReading.Range(50, 8)
                    : StatReading.Unknown);
            return new CustomerRead(readings, Emotion.Sadness, IntentDirection.Extinguish);
        }

        private static SeededRng Rng(string seed = "info") => new RunRng(seed).GetStream("read");

        // ---- picking what to reveal --------------------------------------------------

        [Test]
        public void TheDarkestReading_PrefersUnknownOverRange()
        {
            var read = ReadOf(VisibilityTier.Range, VisibilityTier.Range, VisibilityTier.Unknown,
                VisibilityTier.Exact, VisibilityTier.Exact, VisibilityTier.Exact);

            Assert.IsTrue(read.TryPickDarkest(out var darkest));
            Assert.AreEqual(Emotion.Fatigue, darkest, "the only Unknown wins");
        }

        [Test]
        public void AmongEqualTiers_TheIntentStatWins()
        {
            // Sadness is the intent; both it and Anger are Unknown.
            var read = ReadOf(VisibilityTier.Unknown, VisibilityTier.Unknown, VisibilityTier.Exact,
                VisibilityTier.Exact, VisibilityTier.Exact, VisibilityTier.Exact);

            Assert.IsTrue(read.TryPickDarkest(out var darkest));
            Assert.AreEqual(Emotion.Sadness, darkest, "what they came in about is worth more");
        }

        [Test]
        public void AFullyLegibleCard_HasNothingLeftToReveal()
        {
            var read = ReadOf(Enumerable.Repeat(VisibilityTier.Exact, 6).ToArray());

            Assert.IsFalse(read.TryPickDarkest(out _));
        }

        // ---- VIP read rules ----------------------------------------------------------

        [Test]
        public void PokerFace_BlanksEveryReading()
        {
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllUnknown);
            var read = CustomerReadFactory.Build(Stats(), 1, Rng());

            var masked = CustomerReadFactory.ApplyVipRules(read, Stats(), rules, 1, Rng());

            foreach (var emotion in Emotions.All)
                Assert.AreEqual(VisibilityTier.Unknown, masked[emotion].Tier, emotion.ToString());
        }

        [Test]
        public void OpenBook_PrintsEveryReadingExactly()
        {
            var truth = new EmotionStats(new[] { 11, 22, 33, 44, 55, 66 });
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllExact);
            var read = CustomerReadFactory.Build(truth, 1, Rng());

            var open = CustomerReadFactory.ApplyVipRules(read, truth, rules, 1, Rng());

            foreach (var emotion in Emotions.All)
            {
                Assert.AreEqual(VisibilityTier.Exact, open[emotion].Tier, emotion.ToString());
                Assert.AreEqual(truth[emotion], open[emotion].Low, emotion.ToString());
            }
        }

        [Test]
        public void TheLiar_PlantsExactlyOneFalseReading()
        {
            var truth = Stats(50);
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllExact, oneReadingFalse: true);
            var read = CustomerReadFactory.Build(truth, 1, Rng());

            var lied = CustomerReadFactory.ApplyVipRules(read, truth, rules, 1, Rng());

            int wrong = Emotions.All.Count(e => lied[e].Low != truth[e]);
            Assert.AreEqual(1, wrong, "exactly one reading should be false");
        }

        [Test]
        public void TheLiar_LiesAtTheSameTier_SoItLooksTrustworthy()
        {
            var truth = Stats(50);
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllExact, oneReadingFalse: true);
            var read = CustomerReadFactory.Build(truth, 1, Rng());

            var lied = CustomerReadFactory.ApplyVipRules(read, truth, rules, 1, Rng());

            foreach (var emotion in Emotions.All)
                Assert.AreEqual(VisibilityTier.Exact, lied[emotion].Tier,
                    "a lie that renders differently is not a lie");
        }

        [Test]
        public void TheLiar_HasNothingToLieAboutOnABlankCard()
        {
            // Poker Face + The Liar: blanking wins, and nothing claims to be true.
            var truth = Stats(50);
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllUnknown, oneReadingFalse: true);
            var read = CustomerReadFactory.Build(truth, 1, Rng());

            var result = CustomerReadFactory.ApplyVipRules(read, truth, rules, 1, Rng());

            foreach (var emotion in Emotions.All)
                Assert.AreEqual(VisibilityTier.Unknown, result[emotion].Tier);
        }

        [Test]
        public void ALie_NeverTouchesTheRealStats()
        {
            var truth = Stats(50);
            var before = truth.Clone();
            var rules = new VipRuleSet(null, false, 0, false, ReadOverride.AllExact, oneReadingFalse: true);

            CustomerReadFactory.ApplyVipRules(
                CustomerReadFactory.Build(truth, 1, Rng()), truth, rules, 1, Rng());

            Assert.AreEqual(before, truth, "the ID lies; the person does not change");
        }

        [Test]
        public void NoReadRules_LeavesTheCardUntouched()
        {
            var read = CustomerReadFactory.Build(Stats(), 1, Rng());

            var same = CustomerReadFactory.ApplyVipRules(read, Stats(), VipRuleSet.Empty, 1, Rng());

            Assert.AreSame(read, same);
        }

        // ---- reading patrons and Eavesdrop --------------------------------------------

        private static ArchetypeDefinition Archetype()
        {
            var bands = Emotions.All.Select(_ => new EmotionBand(40, 60)).ToList();
            return new ArchetypeDefinition("test", "Test", bands, new[] { "Sam" });
        }

        private static IngredientCard Card(int i) =>
            new IngredientCard($"c{i}", $"c{i}", IngredientType.Spirit, 5, QualityTier.HousePour,
                new[] { new EmotionCharge(Emotion.Anger, -4) });

        private static RunController NewRun(IEnumerable<PatronInstance> patrons = null,
            IReadOnlyList<ToolDefinition> tools = null, string seed = "info-run")
        {
            var cards = Enumerable.Range(0, 24).Select(Card).ToList();
            return new RunController(cards, RecipeCatalog.CreateDefault(), new RunRng(seed),
                patrons: patrons, toolPool: tools, archetypes: new[] { Archetype() });
        }

        private static int Legibility(CustomerRead read) =>
            Emotions.All.Sum(e => read[e].Tier == VisibilityTier.Exact ? 2
                : read[e].Tier == VisibilityTier.Range ? 1 : 0);

        private static PatronInstance Patron(string id, EffectOp op, double value,
            ConditionKind condition = ConditionKind.Always)
        {
            var effect = new PatronEffect(EffectTrigger.OnCustomerStart, op, value,
                new EffectCondition(condition));
            return new PatronInstance(new PatronDefinition(id, id, PatronRarity.Common, 4, id,
                new[] { effect }));
        }

        [Test]
        public void AReadingPatron_MakesTheCardMoreLegible()
        {
            int plain = Legibility(NewRun().CurrentRound.Customer.Read);
            int helped = Legibility(NewRun(new[] { Patron("p", EffectOp.NarrowReading, 1) })
                .CurrentRound.Customer.Read);

            Assert.Greater(helped, plain);
        }

        [Test]
        public void NarrowReadingTwo_BeatsNarrowReadingOne()
        {
            int one = Legibility(NewRun(new[] { Patron("p", EffectOp.NarrowReading, 1) })
                .CurrentRound.Customer.Read);
            int two = Legibility(NewRun(new[] { Patron("p", EffectOp.NarrowReading, 2) })
                .CurrentRound.Customer.Read);

            Assert.Greater(two, one);
        }

        [Test]
        public void TheEmpath_SharpensTheStatTheyCameInAbout()
        {
            var run = NewRun(new[] { Patron("empath", EffectOp.NarrowIntentReading, 1) });
            var read = run.CurrentRound.Customer.Read;

            Assert.AreNotEqual(VisibilityTier.Unknown, read.IntentReading.Tier,
                "the intent stat must never be left blank when an Empath is at the bar");
        }

        [Test]
        public void RegularsMemory_DoesNothingForAStranger()
        {
            // Every customer is new on the first round, so the condition must gate it off.
            int plain = Legibility(NewRun().CurrentRound.Customer.Read);
            int withMemory = Legibility(
                NewRun(new[] { Patron("mem", EffectOp.NarrowReading, 2, ConditionKind.ReturningCustomer) })
                    .CurrentRound.Customer.Read);

            Assert.AreEqual(plain, withMemory);
        }

        [Test]
        public void ReadingPatrons_NeverTouchTheCustomersStats()
        {
            var plain = NewRun().CurrentRound.Customer.Regular.Stats;
            var helped = NewRun(new[] { Patron("p", EffectOp.NarrowReading, 2) })
                .CurrentRound.Customer.Regular.Stats;

            Assert.AreEqual(plain, helped, "buying information must not move anyone");
        }

        [Test]
        public void Eavesdrop_SharpensTheDarkestReading_AndIsConsumed()
        {
            var eavesdrop = new ToolDefinition("eavesdrop", "Eavesdrop", 4, ToolOp.RevealReading, 1);
            var cards = Enumerable.Range(0, 24).Select(Card).ToList();
            // Trivial target so one Mix satisfies the customer and opens the Back Room.
            var run = new RunController(cards, RecipeCatalog.CreateDefault(), new RunRng("eaves"),
                config: new RunConfig(startingMoney: 3000, targetProvider: (n, s) => 1),
                toolPool: new[] { eavesdrop }, archetypes: new[] { Archetype() });

            PourTestKit.ServeSomething(run);
            var tool = AcquireTool(run, "eavesdrop");
            run.ContinueToNextCustomer();

            int before = Legibility(run.CurrentRound.Customer.Read);
            var statsBefore = run.CurrentRound.Customer.Regular.Stats.Clone();
            run.UseTool(tool, null);

            Assert.Greater(Legibility(run.CurrentRound.Customer.Read), before);
            Assert.AreEqual(statsBefore, run.CurrentRound.Customer.Regular.Stats,
                "overhearing something does not change the person");
            Assert.IsEmpty(run.ToolInventory, "single use");
        }

        private static ToolDefinition AcquireTool(RunController run, string toolId)
        {
            for (int i = 0; i < 100; i++)
            {
                var offers = run.Shop.Offers.ToList();
                var offer = offers.FirstOrDefault(o =>
                    o.Kind == ShopOfferKind.Tool && o.Tool.Id == toolId && !o.Sold);
                if (offer != null)
                {
                    run.BuyOffer(offers.IndexOf(offer));
                    return offer.Tool;
                }
                run.RerollShop();
            }
            throw new System.InvalidOperationException($"'{toolId}' never appeared in 100 rerolls.");
        }
    }
}
