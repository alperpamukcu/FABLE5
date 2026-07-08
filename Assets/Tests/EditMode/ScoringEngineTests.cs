using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class ScoringEngineTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor,
            QualityTier quality = QualityTier.HousePour) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor, quality);

        private static ScoreBreakdown Score(int level, params IngredientCard[] mix) =>
            ScoringEngine.Score(RecipeMatcher.Match(mix, Recipes), level);

        [Test]
        public void OldFashioned_Level1_GoldenValue()
        {
            // Base 20 Flavor x2 Mult, cards add 6+4+3 => (20+13) x 2 = 66.
            var result = Score(1,
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(33, result.TotalFlavor);
            Assert.AreEqual(2, result.TotalMult);
            Assert.AreEqual(66, result.FinalScore);
        }

        [Test]
        public void OldFashioned_Level2_UsesLeveledBase()
        {
            // Level 2: base 20+20=40 Flavor, 2+1=3 Mult => (40+13) x 3 = 159.
            var result = Score(2,
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(159, result.FinalScore);
        }

        [Test]
        public void NoRecipe_ScoresZero()
        {
            var result = Score(1, Card(IngredientType.Sour, 4));
            Assert.IsNull(result.Recipe);
            Assert.AreEqual(0, result.FinalScore);
        }

        [Test]
        public void UnmatchedExtraCard_AddsNoFlavor()
        {
            // Spritz base 10x2; only spirit 5 and bubbly 2 score: (10+7) x 2 = 34.
            var result = Score(1,
                Card(IngredientType.Spirit, 5), Card(IngredientType.Bubbly, 2), Card(IngredientType.Sour, 9));
            Assert.AreEqual("spritz", result.Recipe.Id);
            Assert.AreEqual(34, result.FinalScore);
        }

        [Test]
        public void TopShelf_Adds30Flavor()
        {
            var result = Score(1,
                Card(IngredientType.Spirit, 6, QualityTier.TopShelf),
                Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual((33 + 30) * 2, result.FinalScore);
        }

        [Test]
        public void BarrelAged_Adds8Mult()
        {
            var result = Score(1,
                Card(IngredientType.Spirit, 6, QualityTier.BarrelAged),
                Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(33 * 10, result.FinalScore);
        }

        [Test]
        public void Signature_Multiplies_Mult()
        {
            var result = Score(1,
                Card(IngredientType.Spirit, 6, QualityTier.Signature),
                Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(33 * 3, result.FinalScore); // 2 x 1.5 = 3
        }

        [Test]
        public void Bootleg_CardScoresZeroFlavor()
        {
            var result = Score(1,
                Card(IngredientType.Spirit, 6, QualityTier.Bootleg),
                Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual((20 + 4 + 3) * 2, result.FinalScore);
        }

        [Test]
        public void DoublePerfect_GoldenValue()
        {
            // Base 160x14, five cards of flavor 5 => (160+25) x 14 = 2590.
            var result = Score(1,
                Card(IngredientType.Spirit, 5), Card(IngredientType.Sour, 5),
                Card(IngredientType.Sweet, 5), Card(IngredientType.Bubbly, 5),
                Card(IngredientType.Garnish, 5));
            Assert.AreEqual("double_perfect", result.Recipe.Id);
            Assert.AreEqual(2590, result.FinalScore);
        }

        [Test]
        public void Breakdown_StepsReplayTheFormula()
        {
            var result = Score(1,
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            // 1 base step + 3 card steps; last step's running totals equal the final totals.
            Assert.AreEqual(4, result.Steps.Count);
            var last = result.Steps[result.Steps.Count - 1];
            Assert.AreEqual(result.TotalFlavor, last.FlavorAfter);
            Assert.AreEqual(result.TotalMult, last.MultAfter);
        }
    }
}
