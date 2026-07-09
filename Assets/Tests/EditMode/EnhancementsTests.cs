using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>The four late enhancements (GDD 3.3 rulings): Premium, Frozen, Doubled, Golden.</summary>
    public class EnhancementsTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor, Enhancement enhancement = Enhancement.None)
        {
            var card = new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);
            if (enhancement != Enhancement.None) card.Enhance(enhancement);
            return card;
        }

        private static List<IngredientCard> SpiritCards(int count) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        // ── Premium (wild for matching only) ─────────────────────────────────────

        [Test]
        public void Premium_FillsAMissingType_AndTheBestRankWins()
        {
            // Spirit + Sweet + wild: the wild becomes Sour, upgrading Old Fashioned to Sour.
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6),
                Card(IngredientType.Sweet, 4),
                Card(IngredientType.Garnish, 3, Enhancement.Premium)
            };

            var match = RecipeMatcher.Match(mix, Recipes);

            Assert.AreEqual("sour", match.Recipe.Id);
            CollectionAssert.Contains(match.ScoredCards.ToList(), mix[2]);
        }

        [Test]
        public void Premium_CountsAsAnUnusedType_InDistinctTypeRecipes()
        {
            // Printed types Spirit/Sour/Sweet/Bitter + a wild duplicate Sour: the wild
            // claims the missing fifth type, so Perfect Serve still forms.
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6),
                Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 4),
                Card(IngredientType.Bitter, 4),
                Card(IngredientType.Sour, 2, Enhancement.Premium)
            };

            var match = RecipeMatcher.Match(mix, Recipes);

            Assert.AreEqual("perfect_serve", match.Recipe.Id);
        }

        [Test]
        public void Premium_KeepsItsPrintedType_ForPatronEffects()
        {
            var gardener = new PatronInstance(new PatronDefinition("gardener", "The Gardener",
                PatronRarity.Common, 4, "",
                new[] { new PatronEffect(EffectTrigger.OnCardScored, EffectOp.AddMult, 2,
                    EffectCondition.CardTypeIs(IngredientType.Garnish)) }));
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6),
                Card(IngredientType.Sweet, 4),
                Card(IngredientType.Garnish, 3, Enhancement.Premium)
            };

            var match = RecipeMatcher.Match(mix, Recipes);
            var breakdown = ScoringEngine.Score(match, 1, new[] { gardener },
                new EffectContext(mix, match.Recipe, 0, 0));

            // Sour: (30 + 6 + 4 + 3) × (3 + 2 from the Garnish trigger) — wild in the
            // pattern, Garnish on the ticket.
            Assert.AreEqual("sour", match.Recipe.Id);
            Assert.AreEqual(215, breakdown.FinalScore);
        }

        // ── Frozen (×2 Mult, 1-in-4 shatter) ─────────────────────────────────────

        [Test]
        public void Frozen_DoublesMult_WhenScored()
        {
            var mix = new[] { Card(IngredientType.Spirit, 6, Enhancement.Frozen) };
            var breakdown = ScoringEngine.Score(RecipeMatcher.Match(mix, Recipes));

            // Neat Pour: (5 + 6) × (1 × 2) = 22.
            Assert.AreEqual(22, breakdown.FinalScore);
            Assert.IsTrue(breakdown.Steps.Any(s => s.Op == EffectOp.MultMult && s.Source.Contains("Frozen")));
        }

        [Test]
        public void FrozenShatter_IsSeedDeterministic_AndDestroysTheCard()
        {
            bool ShatterOutcome(ulong seed)
            {
                var round = new RoundController(new Deck(SpiritCards(9)), Recipes,
                    new CustomerOrder("T", 100000), shatterRng: new SeededRng(seed));
                var frozen = round.Rail[0];
                frozen.Enhance(Enhancement.Frozen);
                round.Mix(new[] { frozen });

                bool shattered = round.LastShatteredCards.Count == 1;
                // A shattered card leaves the run for good; otherwise it discards normally.
                Assert.AreEqual(shattered ? 0 : 1, round.DeckDiscardCount);
                return shattered;
            }

            int shatters = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                if (ShatterOutcome(seed)) shatters++;

            Assert.That(shatters, Is.InRange(1, 39), "roughly 1-in-4 must shatter across seeds");
            Assert.AreEqual(ShatterOutcome(7), ShatterOutcome(7), "same seed, same outcome");
        }

        [Test]
        public void Frozen_NeverShatters_WithoutARunRng()
        {
            var round = new RoundController(new Deck(SpiritCards(9)), Recipes,
                new CustomerOrder("T", 100000));
            round.Rail[0].Enhance(Enhancement.Frozen);
            round.Mix(new[] { round.Rail[0] });

            Assert.IsEmpty(round.LastShatteredCards);
        }

        // ── Doubled (permanent plain copy on score) ──────────────────────────────

        [Test]
        public void Doubled_MintsAPlainPermanentCopy_WhenScored()
        {
            var round = new RoundController(new Deck(SpiritCards(9)), Recipes,
                new CustomerOrder("T", 100000));
            var doubled = round.Rail[0];
            doubled.Enhance(Enhancement.Doubled);
            round.Mix(new[] { doubled });

            Assert.AreEqual(1, round.LastDoubledCopies.Count);
            var copy = round.LastDoubledCopies[0];
            Assert.AreEqual(doubled.Id, copy.Id);
            Assert.AreNotEqual(doubled.InstanceId, copy.InstanceId);
            Assert.AreEqual(Enhancement.None, copy.Enhancement, "copies must not re-copy");
            Assert.AreEqual(2, round.DeckDiscardCount, "original + copy both land in the deck");
        }

        [Test]
        public void VoidedMix_TriggersNoShatterOrDouble()
        {
            var rules = new VipRuleSet(null, true, 0, false); // only the first Mix counts
            var round = new RoundController(new Deck(SpiritCards(10)), Recipes,
                new CustomerOrder("T", 100000), vipRules: rules);
            var doubled = round.Rail[0];
            doubled.Enhance(Enhancement.Doubled);

            round.Mix(new[] { round.Rail[1] });
            var voided = round.Mix(new[] { doubled });

            Assert.IsTrue(voided.IsVoided);
            Assert.IsEmpty(round.LastDoubledCopies);
            Assert.AreEqual(2, round.DeckDiscardCount, "both cards discard, no copy is minted");
        }

        // ── Golden ($3 per card held on the rail at customer end) ────────────────

        [Test]
        public void Golden_PaysPerRailCard_WhenTheCustomerIsSatisfied()
        {
            var run = new RunController(SpiritCards(48), Recipes, new RunRng("GOLD"), null,
                new RunConfig(targetProvider: (n, s) => 10));
            run.CurrentRound.Rail[1].Enhance(Enhancement.Golden);
            run.CurrentRound.Rail[2].Enhance(Enhancement.Golden);

            run.Mix(new[] { run.CurrentRound.Rail[0] }); // Neat Pour 11 ≥ 10 wins

            Assert.AreEqual(6, run.LastTips.GoldenBonus, "two Golden cards on the rail pay $3 each");
            Assert.IsTrue(run.LastTips.Total >= 6);
        }
    }
}
