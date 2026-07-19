using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>The GDD 02 v1.1 additions: House Special, Layered Pour, Straight Booze,
    /// deterministic priority tie-breaks and the Muddling Stick value axis.</summary>
    public class RecipesV11Tests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor, Enhancement enhancement = Enhancement.None)
        {
            var card = new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);
            if (enhancement != Enhancement.None) card.Enhance(enhancement);
            return card;
        }

        [Test]
        public void HouseSpecial_ThreeEqualValues_OfAnyTypes()
        {
            var mix = new[]
            {
                Card(IngredientType.Sour, 7), Card(IngredientType.Sweet, 7),
                Card(IngredientType.Garnish, 7)
            };
            var match = RecipeMatcher.Match(mix, Recipes);

            Assert.AreEqual("house_special", match.Recipe.Id);
            Assert.AreEqual(3, match.ScoredCards.Count);
            // (30 + 7+7+7) × 3 = 153
            Assert.AreEqual(153, ScoringEngine.Score(match).FinalScore);
        }

        [Test]
        public void HouseSpecial_PicksTheHighestQualifyingValue_OthersAreNonPattern()
        {
            var mix = new[]
            {
                Card(IngredientType.Sour, 3), Card(IngredientType.Sweet, 3), Card(IngredientType.Bitter, 3),
                Card(IngredientType.Bubbly, 9), Card(IngredientType.Garnish, 9)
            };
            var match = RecipeMatcher.Match(mix, Recipes);

            Assert.AreEqual("house_special", match.Recipe.Id);
            Assert.IsTrue(match.ScoredCards.All(c => c.Flavor == 3), "9s appear only twice");
            Assert.AreEqual(3, match.ScoredCards.Count, "the two 9s are non-pattern");
        }

        [Test]
        public void LayeredPour_FourDistinctValues_DuplicatesAreNonPattern()
        {
            var mix = new[]
            {
                Card(IngredientType.Sour, 2), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 6), Card(IngredientType.Bubbly, 8),
                Card(IngredientType.Bitter, 4) // duplicate value: non-pattern
            };
            var match = RecipeMatcher.Match(mix, Recipes);

            Assert.AreEqual("layered_pour", match.Recipe.Id);
            Assert.AreEqual(4, match.ScoredCards.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6, 8 },
                match.ScoredCards.Select(c => c.Flavor).ToList());
        }

        [Test]
        public void StraightBooze_FourOfAType_AndPremiumCompletesIt()
        {
            var natural = new[]
            {
                Card(IngredientType.Sweet, 2), Card(IngredientType.Sweet, 4),
                Card(IngredientType.Sweet, 6), Card(IngredientType.Sweet, 8)
            };
            Assert.AreEqual("straight_booze", RecipeMatcher.Match(natural, Recipes).Recipe.Id);

            var withWild = new[]
            {
                Card(IngredientType.Sweet, 2), Card(IngredientType.Sweet, 4),
                Card(IngredientType.Sweet, 6), Card(IngredientType.Sour, 9, Enhancement.Premium)
            };
            var match = RecipeMatcher.Match(withWild, Recipes);
            Assert.AreEqual("straight_booze", match.Recipe.Id);
            Assert.AreEqual(4, match.ScoredCards.Count, "the wild is part of the set");
        }

        // ── deterministic priority tie-breaks ────────────────────────────────────

        [Test]
        public void Priority_MartiniBeatsHouseSpecial()
        {
            // 2 Spirits + Bitter, all value 6: House Special (5) and Martini (7) both match.
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 6),
                Card(IngredientType.Bitter, 6)
            };
            Assert.AreEqual("martini", RecipeMatcher.Match(mix, Recipes).Recipe.Id);
        }

        [Test]
        public void Priority_FizzBeatsLayeredPour()
        {
            // Spirit+Sour+Sweet+Bubbly with 4 distinct values: Fizz (9) over Layered Pour (8).
            var mix = new[]
            {
                Card(IngredientType.Spirit, 2), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 6), Card(IngredientType.Bubbly, 8)
            };
            Assert.AreEqual("fizz", RecipeMatcher.Match(mix, Recipes).Recipe.Id);
        }

        [Test]
        public void Priority_StraightBoozeBeatsHouseSpecial()
        {
            // 4 equal-value Spirits: House Special (5) and Straight Booze (10) both match.
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 6),
                Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 6)
            };
            Assert.AreEqual("straight_booze", RecipeMatcher.Match(mix, Recipes).Recipe.Id);
        }

        [Test]
        public void Priority_DoublePerfectStillBeatsEverything()
        {
            var mix = new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 6),
                Card(IngredientType.Sweet, 6), Card(IngredientType.Bitter, 6),
                Card(IngredientType.Bubbly, 6)
            };
            Assert.AreEqual("double_perfect", RecipeMatcher.Match(mix, Recipes).Recipe.Id);
        }

        // ── Muddling Stick (value axis) ──────────────────────────────────────────

        // The Muddling Stick's round-level rail application went with the rail; it now
        // shifts a shelf bottle's Flavor at the run layer (see BackRoomTests). The value-axis
        // recipes it fed are still covered by the matcher tests above.


        [Test]
        public void CatalogHasFourteen_AndPrioritiesAreUniqueAndComplete()
        {
            Assert.AreEqual(14, Recipes.Count, "GDD 02 v1.1: 14 recipes");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 14).ToList(),
                Recipes.Select(r => r.Rank).ToList(), "priorities 1..14, no gaps or ties");
        }
    }
}
