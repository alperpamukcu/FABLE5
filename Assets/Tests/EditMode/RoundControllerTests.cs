using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class RoundControllerTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);

        /// <summary>An unshuffled deck of identical-type cards so rail contents are predictable.</summary>
        private static Deck SpiritDeck(int count, int flavor = 6) =>
            new Deck(Enumerable.Range(0, count).Select(_ => Card(IngredientType.Spirit, flavor)));

        private static RoundController NewRound(Deck deck, double target = 1000, RoundConfig config = null) =>
            new RoundController(deck, Recipes, new CustomerOrder("Test", target), config);

        [Test]
        public void Start_FillsRailToConfiguredSize()
        {
            var round = NewRound(SpiritDeck(48));
            Assert.AreEqual(8, round.Rail.Count);
            Assert.AreEqual(4, round.MixesRemaining);
            Assert.AreEqual(3, round.RestocksRemaining);
            Assert.AreEqual(RoundPhase.InProgress, round.Phase);
        }

        [Test]
        public void Mix_Scores_Consumes_AndRefills()
        {
            var round = NewRound(SpiritDeck(48, flavor: 6));
            var selection = new[] { round.Rail[0] };

            var breakdown = round.Mix(selection); // Neat Pour: (5 + 6) x 1 = 11

            Assert.AreEqual(11, breakdown.FinalScore);
            Assert.AreEqual(11, round.AccumulatedScore);
            Assert.AreEqual(3, round.MixesRemaining);
            Assert.AreEqual(8, round.Rail.Count, "rail refills after a non-terminal mix");
            CollectionAssert.DoesNotContain(round.Rail.ToList(), selection[0]);
        }

        [Test]
        public void Mix_ReachingTarget_WinsImmediately()
        {
            var round = NewRound(SpiritDeck(48, flavor: 6), target: 10);
            round.Mix(new[] { round.Rail[0] }); // 11 >= 10

            Assert.AreEqual(RoundPhase.Won, round.Phase);
        }

        [Test]
        public void Mix_ExhaustingMixesBelowTarget_Loses()
        {
            var round = NewRound(SpiritDeck(48, flavor: 6), target: 100000);
            for (int i = 0; i < 4; i++) round.Mix(new[] { round.Rail[0] });

            Assert.AreEqual(RoundPhase.Lost, round.Phase);
            Assert.AreEqual(0, round.MixesRemaining);
        }

        [Test]
        public void Mix_WithNoRecipe_ScoresZero_ButConsumesTheMix()
        {
            var cards = Enumerable.Range(0, 16)
                .Select(i => Card(i % 2 == 0 ? IngredientType.Sour : IngredientType.Sweet, 4));
            var round = NewRound(new Deck(cards));

            var breakdown = round.Mix(new[] { round.Rail[0], round.Rail[1] });

            Assert.AreEqual(0, breakdown.FinalScore);
            Assert.IsNull(breakdown.Recipe);
            Assert.AreEqual(3, round.MixesRemaining);
        }

        [Test]
        public void Mix_WhenDeckRunsDry_RailShrinks()
        {
            var round = NewRound(SpiritDeck(8)); // rail takes all 8, draw pile empty
            var selection = round.Rail.Take(5).ToArray();

            round.Mix(selection);

            Assert.AreEqual(3, round.Rail.Count);
            Assert.AreEqual(RoundPhase.InProgress, round.Phase);
        }

        [Test]
        public void Restock_SwapsCards_AndDecrements()
        {
            var round = NewRound(SpiritDeck(48));
            var selection = round.Rail.Take(3).ToArray();

            round.Restock(selection);

            Assert.AreEqual(8, round.Rail.Count);
            Assert.AreEqual(2, round.RestocksRemaining);
            Assert.AreEqual(4, round.MixesRemaining, "restock must not consume a mix");
            foreach (var card in selection)
                CollectionAssert.DoesNotContain(round.Rail.ToList(), card);
        }

        [Test]
        public void Restock_WithNoneRemaining_Throws()
        {
            var round = NewRound(SpiritDeck(48), config: new RoundConfig(restocksPerCustomer: 0));
            Assert.Throws<InvalidOperationException>(() => round.Restock(new[] { round.Rail[0] }));
        }

        [Test]
        public void ActingAfterRoundOver_Throws()
        {
            var round = NewRound(SpiritDeck(48, flavor: 6), target: 10);
            round.Mix(new[] { round.Rail[0] }); // wins

            Assert.Throws<InvalidOperationException>(() => round.Mix(new[] { round.Rail[0] }));
            Assert.Throws<InvalidOperationException>(() => round.Restock(new[] { round.Rail[0] }));
        }

        [Test]
        public void Selection_Validation()
        {
            var round = NewRound(SpiritDeck(48));
            var stranger = Card(IngredientType.Spirit, 5);

            Assert.Throws<ArgumentException>(() => round.Mix(new IngredientCard[0]), "empty selection");
            Assert.Throws<ArgumentException>(() => round.Mix(round.Rail.Take(6).ToArray()), "more than 5 cards");
            Assert.Throws<ArgumentException>(() => round.Mix(new[] { stranger }), "card not on the rail");
            Assert.Throws<ArgumentException>(() => round.Mix(new[] { round.Rail[0], round.Rail[0] }), "duplicate card");
            Assert.AreEqual(4, round.MixesRemaining, "failed validation must not consume a mix");
        }

        [Test]
        public void PreviewMatch_DoesNotConsumeAnything()
        {
            var round = NewRound(SpiritDeck(48));
            var match = round.PreviewMatch(new[] { round.Rail[0] });

            Assert.AreEqual("neat_pour", match.Recipe.Id);
            Assert.AreEqual(4, round.MixesRemaining);
            Assert.AreEqual(8, round.Rail.Count);
        }

        [Test]
        public void PreviewScore_MatchesActualMixScore_WithoutConsuming()
        {
            var round = NewRound(SpiritDeck(48, flavor: 6));
            var selection = new[] { round.Rail[0] };

            var preview = round.PreviewScore(selection);
            Assert.AreEqual(4, round.MixesRemaining);

            var actual = round.Mix(selection);
            Assert.AreEqual(preview.FinalScore, actual.FinalScore);
        }

        [Test]
        public void RecipeLevels_AreAppliedWhenProvided()
        {
            var levels = new Dictionary<string, int> { ["neat_pour"] = 2 };
            var deck = SpiritDeck(48, flavor: 6);
            var round = new RoundController(deck, Recipes, new CustomerOrder("Test", 1000),
                recipeLevels: levels);

            // Level 2 Neat Pour: base 5+10=15 Flavor, 1+1=2 Mult => (15+6) x 2 = 42.
            var breakdown = round.Mix(new[] { round.Rail[0] });
            Assert.AreEqual(42, breakdown.FinalScore);
        }
    }
}
