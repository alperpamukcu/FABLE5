using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The late enhancements (GDD 3.3 rulings). Premium's wildcard matching still applies to
    /// the card-era <see cref="RecipeMatcher"/>, which survives unused until Phase 5 authors
    /// ratio bands for the recipe table.
    /// </summary>
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

        // Frozen's 1-in-4 shatter, Doubled's minted copy and Golden's per-rail-card payout
        // were all card-*instance* behaviour: they destroyed, duplicated or counted specific
        // cards in a hand. Shelf bottles are not instances and there is no rail to hold
        // anything at customer end, so those three are casualties of the pour pivot — see the
        // list in Docs/PLAN_pour_pivot.md. Frozen's x2 Mult survives and is covered above.
    }
}
