using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The hidden information has to stay hidden. Everything the player sees is derived from
    /// <see cref="CustomerRead"/>; the moment a preview or a label reaches past it into
    /// <see cref="RegularState.Stats"/>, blind reads stop being reads and the lucky-read
    /// bonus becomes free money. These tests pin that boundary.
    /// </summary>
    public class ReadIntegrityTests
    {
        private static ArchetypeDefinition Archetype()
        {
            var bands = Emotions.All.Select(_ => new EmotionBand(40, 60)).ToList();
            return new ArchetypeDefinition("test", "Test", bands, new[] { "Sam" });
        }

        private static IngredientCard Card(string id, Emotion emotion, int amount, int flavor = 5) =>
            new IngredientCard(id, id, IngredientType.Spirit, flavor, QualityTier.HousePour,
                new[] { new EmotionCharge(emotion, amount) });

        private static RunController NewRun(string seed = "leak")
        {
            var cards = new List<IngredientCard>();
            for (int i = 0; i < 24; i++) cards.Add(Card($"c{i}", Emotions.All[i % Emotions.Count], -6));
            return new RunController(cards, RecipeCatalog.CreateDefault(), new RunRng(seed),
                archetypes: new[] { Archetype() });
        }

        [Test]
        public void AnUnknownReading_ExposesNoBound()
        {
            // StatReading.Unknown must not carry the truth in its Low/High: it is rendered,
            // logged and previewed, and anything stored on it is one ToString() from leaking.
            var unknown = StatReading.Unknown;

            Assert.AreEqual(EmotionStats.Min, unknown.Low);
            Assert.AreEqual(EmotionStats.Max, unknown.High);
            Assert.AreEqual("??", unknown.ToString());
        }

        [Test]
        public void ARangeReading_NeverPrintsTheExactValue()
        {
            var reading = StatReading.Range(50, 8);

            Assert.AreEqual("42-58", reading.ToString());
            Assert.AreNotEqual(reading.Low, reading.High, "a zero-width range would be an Exact in disguise");
        }

        [Test]
        public void TheCard_ShowsAtMostOneExactValue()
        {
            var truth = new EmotionStats(new[] { 11, 22, 33, 44, 55, 66 });

            for (int i = 0; i < 25; i++)
            {
                var read = CustomerReadFactory.Build(truth, 3, new RunRng($"s{i}").GetStream("read"));
                int exact = Emotions.All.Count(e => read[e].Tier == VisibilityTier.Exact);
                Assert.AreEqual(1, exact, "exactly one stat is ever printed outright");
            }
        }

        [Test]
        public void PreviewCharges_DescribesTheDrink_NotTheCustomer()
        {
            // The delta depends only on the cards played. If it ever varied with the
            // customer's hidden stats, the preview would be a readout of the answer.
            var run = NewRun();
            var round = run.CurrentRound;
            var selection = new[] { round.Rail[0] };

            var first = round.PreviewCharges(selection);
            foreach (var emotion in Emotions.All) round.Customer.Regular.Stats.Set(emotion, 97);
            var second = round.PreviewCharges(selection);

            foreach (var emotion in Emotions.All)
                Assert.AreEqual(first[emotion], second[emotion],
                    $"{emotion} preview moved when only the hidden stats changed");
        }

        [Test]
        public void PreviewCharges_IsNonConsuming()
        {
            var run = NewRun();
            var round = run.CurrentRound;
            var before = round.Customer.Regular.Stats.Clone();
            int mixes = round.MixesRemaining;

            round.PreviewCharges(new[] { round.Rail[0], round.Rail[1] });
            round.PreviewResonance(new[] { round.Rail[0], round.Rail[1] });

            Assert.AreEqual(before, round.Customer.Regular.Stats, "previewing must not move anyone");
            Assert.AreEqual(mixes, round.MixesRemaining);
        }

        [Test]
        public void Chat_NarrowsOnlyTheStatAsked_AndSpendsARestock()
        {
            var run = NewRun();
            var round = run.CurrentRound;
            var target = Emotions.All.First(e => round.Customer.Read[e].Tier == VisibilityTier.Unknown);
            var untouched = Emotions.All.First(e => e != target);
            var before = round.Customer.Read[untouched];
            int restocks = round.RestocksRemaining;
            int chats = round.ChatsRemaining;

            var learned = round.Chat(target);

            Assert.AreNotEqual(VisibilityTier.Unknown, learned.Tier, "asking must reveal something");
            Assert.IsTrue(learned.Contains(round.Customer.Regular.Stats[target]));
            Assert.AreEqual(before, round.Customer.Read[untouched], "only the stat asked about changes");
            Assert.AreEqual(restocks - 1, round.RestocksRemaining);
            Assert.AreEqual(chats - 1, round.ChatsRemaining);
        }

        [Test]
        public void Chat_DoesNotCountAsARestockForPatronConditions()
        {
            // Docs/PLAN_emotion_pivot.md D8: listening is not churning the rail. Patrons that
            // key off restocks_used must not be fed by conversation.
            var run = NewRun();
            var round = run.CurrentRound;
            int restocksUsed = round.RestocksUsed;

            round.Chat(Emotion.Anger);

            Assert.AreEqual(restocksUsed, round.RestocksUsed);
        }

        [Test]
        public void Chat_RunsOut()
        {
            var run = NewRun();
            var round = run.CurrentRound;

            for (int i = 0; i < round.Config.ChatsPerCustomer; i++) round.Chat(Emotion.Anger);

            Assert.AreEqual(0, round.ChatsRemaining);
            Assert.Throws<System.InvalidOperationException>(() => round.Chat(Emotion.Anger));
        }

        /// <summary>A round whose customer is fully controlled, so the verdict is not a coin flip.</summary>
        private static RoundController RoundWith(Emotion intent, IntentDirection direction,
            int trueValue, VisibilityTier intentTier, IReadOnlyList<IngredientCard> deckCards)
        {
            var stats = new EmotionStats();
            stats.Set(intent, trueValue);
            var regular = new RegularState("r", "Sam", "test", stats, stats.Clone());

            var readings = Emotions.All.Select(e => e != intent ? StatReading.Exact(0)
                : intentTier == VisibilityTier.Exact ? StatReading.Exact(trueValue)
                : intentTier == VisibilityTier.Range ? StatReading.Range(trueValue, 8)
                : StatReading.Unknown).ToList();

            var order = new CustomerOrder("Sam", 1e9, regular,
                new CustomerRead(readings, intent, direction));
            return new RoundController(new Deck(deckCards), RecipeCatalog.CreateDefault(), order);
        }

        [Test]
        public void ABust_IsNeverWrittenThroughToTheCustomer()
        {
            // Overshooting must not double as a way to probe where someone actually is.
            // The deck draws off the end, so the card under test goes last to reach the rail.
            var deck = Enumerable.Range(0, 12).Select(i => Card($"f{i}", Emotion.Anger, -4)).ToList();
            deck.Add(Card("overshoot", Emotion.Sadness, -80));

            var round = RoundWith(Emotion.Sadness, IntentDirection.Extinguish, 30,
                VisibilityTier.Exact, deck);
            var before = round.Customer.Regular.Stats.Clone();

            round.Mix(new[] { round.Rail.First(c => c.Id == "overshoot") });

            Assert.IsTrue(round.LastResonance.IsBust, "-80 against 30 must overshoot");
            Assert.AreEqual(BustKind.Overshoot, round.LastResonance.Bust);
            Assert.AreEqual(before, round.Customer.Regular.Stats);
            Assert.AreEqual(0, round.SatisfactionEarned);
        }

        [Test]
        public void ACleanServe_LandsExactly_AndIsWorthTheMost()
        {
            var deck = Enumerable.Range(0, 12).Select(i => Card($"f{i}", Emotion.Anger, -4)).ToList();
            deck.Add(Card("exact", Emotion.Sadness, -30));

            var round = RoundWith(Emotion.Sadness, IntentDirection.Extinguish, 30,
                VisibilityTier.Exact, deck);

            round.Mix(new[] { round.Rail.First(c => c.Id == "exact") });

            Assert.IsTrue(round.LastResonance.CleanServe);
            Assert.AreEqual(0, round.Customer.Regular.Stats[Emotion.Sadness]);
            Assert.AreEqual(3, round.SatisfactionEarned);
        }
    }
}
