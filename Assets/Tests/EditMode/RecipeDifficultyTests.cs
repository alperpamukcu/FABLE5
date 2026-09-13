using System;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// A drink's difficulty (2026-09-13, the author: "çok malzeme isteyen zaman alan kokteyller
    /// 3. seviye kırmızı zorluk, daha azı turuncu, daha azı yeşil"): read off the pours and the
    /// method, so it can never disagree with the recipe it describes.
    /// </summary>
    public sealed class RecipeDifficultyTests
    {
        private static RecipeDefinition Drink(string id, int pours, PrepMethod prep)
        {
            var bands = Enumerable.Range(0, pours)
                .Select(i => new RatioRequirement("style_" + i, 0.0, 1.0))
                .ToArray();
            return new RecipeDefinition(id, id, rank: 1, baseFlavor: 1, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0, requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: bands, prep: prep);
        }

        [Test]
        public void MorePours_AndAWorkedMethod_MakeADrinkHarder()
        {
            Assert.AreEqual(DrinkDifficulty.Easy, RecipeDifficulty.Of(Drink("a", 2, PrepMethod.Built)));
            Assert.AreEqual(DrinkDifficulty.Easy, RecipeDifficulty.Of(Drink("b", 3, PrepMethod.Built)));
            Assert.AreEqual(DrinkDifficulty.Medium, RecipeDifficulty.Of(Drink("c", 3, PrepMethod.Shaken)));
            Assert.AreEqual(DrinkDifficulty.Medium, RecipeDifficulty.Of(Drink("d", 4, PrepMethod.Built)));
            Assert.AreEqual(DrinkDifficulty.Hard, RecipeDifficulty.Of(Drink("e", 4, PrepMethod.Stirred)));
            Assert.AreEqual(DrinkDifficulty.Hard, RecipeDifficulty.Of(Drink("f", 5, PrepMethod.Built)));
            for (int pours = 1; pours <= 7; pours++)
                Assert.GreaterOrEqual((int)RecipeDifficulty.Of(Drink("s", pours, PrepMethod.Shaken)),
                                      (int)RecipeDifficulty.Of(Drink("b", pours, PrepMethod.Built)),
                    $"working a {pours}-pour drink made it easier than building it");
        }

        [Test]
        public void TheBook_UsesAllThreeSteps_AndNamesTheObviousOnes()
        {
            var book = RecipeCatalog.CreateDefault();
            foreach (DrinkDifficulty step in Enum.GetValues(typeof(DrinkDifficulty)))
                Assert.IsTrue(book.Any(r => RecipeDifficulty.Of(r) == step), $"no drink in the book is {step}");
            RecipeDefinition Named(string id) => book.First(r => r.Id == id);
            Assert.AreEqual(DrinkDifficulty.Easy, RecipeDifficulty.Of(Named("vodka_soda")), "two pours, built");
            Assert.AreEqual(DrinkDifficulty.Medium, RecipeDifficulty.Of(Named("gin_sour")), "three pours, shaken");
            Assert.AreEqual(DrinkDifficulty.Hard, RecipeDifficulty.Of(Named("long_island")), "seven pours, shaken");
            Assert.AreEqual(DrinkDifficulty.Easy, RecipeDifficulty.Of(null), "no drink is no work");
        }
    }
}
