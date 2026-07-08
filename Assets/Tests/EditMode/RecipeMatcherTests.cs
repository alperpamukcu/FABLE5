using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class RecipeMatcherTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor,
            QualityTier quality = QualityTier.HousePour) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor, quality);

        private static RecipeMatch Match(params IngredientCard[] mix) => RecipeMatcher.Match(mix, Recipes);

        [Test]
        public void NeatPour_SingleSpirit()
        {
            var match = Match(Card(IngredientType.Spirit, 6));
            Assert.AreEqual("neat_pour", match.Recipe.Id);
        }

        [Test]
        public void TwoSpiritsAlone_MatchNothing()
        {
            // Neat Pour requires the spirit to be alone and no other recipe fits two bare spirits.
            Assert.IsNull(Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 7)));
        }

        [Test]
        public void SingleNonSpirit_MatchesNothing()
        {
            Assert.IsNull(Match(Card(IngredientType.Sour, 4)));
        }

        [Test]
        public void MixWithoutSpirit_MatchesNothing()
        {
            Assert.IsNull(Match(
                Card(IngredientType.Sour, 3), Card(IngredientType.Sweet, 4),
                Card(IngredientType.Bitter, 5), Card(IngredientType.Bubbly, 6),
                Card(IngredientType.Garnish, 2)));
        }

        [Test]
        public void Spritz_SpiritPlusBubbly()
        {
            var match = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2));
            Assert.AreEqual("spritz", match.Recipe.Id);
            Assert.AreEqual(2, match.ScoredCards.Count);
        }

        [Test]
        public void OldFashioned_SpiritSweetBitter()
        {
            var match = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual("old_fashioned", match.Recipe.Id);
        }

        [Test]
        public void Highball_SpiritBubblyGarnish()
        {
            var match = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2), Card(IngredientType.Garnish, 1));
            Assert.AreEqual("highball", match.Recipe.Id);
        }

        [Test]
        public void Sour_SpiritSourSweet()
        {
            var match = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4), Card(IngredientType.Sweet, 2));
            Assert.AreEqual("sour", match.Recipe.Id);
        }

        [Test]
        public void Martini_TwoSpiritsPlusBitter_OrGarnish()
        {
            var withBitter = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 4), Card(IngredientType.Bitter, 3));
            var withGarnish = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 4), Card(IngredientType.Garnish, 3));
            Assert.AreEqual("martini", withBitter.Recipe.Id);
            Assert.AreEqual("martini", withGarnish.Recipe.Id);
        }

        [Test]
        public void Fizz_BeatsSour()
        {
            var match = Match(
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 2), Card(IngredientType.Bubbly, 1));
            Assert.AreEqual("fizz", match.Recipe.Id);
        }

        [Test]
        public void Negroni_BeatsMartini()
        {
            var match = Match(
                Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 4),
                Card(IngredientType.Bitter, 3), Card(IngredientType.Garnish, 1));
            Assert.AreEqual("negroni", match.Recipe.Id);
        }

        [Test]
        public void Tiki_FifthCardMayDuplicateAType_AndAllFiveScore()
        {
            // The duplicated Sour keeps this from being a Perfect Serve (types not all distinct).
            var match = Match(
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 2), Card(IngredientType.Garnish, 1),
                Card(IngredientType.Sour, 7));
            Assert.AreEqual("tiki", match.Recipe.Id);
            Assert.AreEqual(5, match.ScoredCards.Count);
        }

        [Test]
        public void PerfectServe_FiveDistinctTypesIncludingSpirit()
        {
            var match = Match(
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 2), Card(IngredientType.Bubbly, 1),
                Card(IngredientType.Garnish, 5));
            Assert.AreEqual("perfect_serve", match.Recipe.Id);
            Assert.AreEqual(5, match.ScoredCards.Count);
        }

        [Test]
        public void DoublePerfect_PerfectServeWithEqualFlavors()
        {
            var match = Match(
                Card(IngredientType.Spirit, 5), Card(IngredientType.Sour, 5),
                Card(IngredientType.Sweet, 5), Card(IngredientType.Bubbly, 5),
                Card(IngredientType.Garnish, 5));
            Assert.AreEqual("double_perfect", match.Recipe.Id);
        }

        [Test]
        public void UnmatchedExtraCards_DoNotScore()
        {
            var sour = Card(IngredientType.Sour, 9);
            var match = Match(Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2), sour);
            Assert.AreEqual("spritz", match.Recipe.Id);
            CollectionAssert.DoesNotContain(match.ScoredCards.ToList(), sour);
        }

        [Test]
        public void PatternSlots_PreferHigherFlavorCards()
        {
            var weakSpirit = Card(IngredientType.Spirit, 2);
            var strongSpirit = Card(IngredientType.Spirit, 7);
            var match = Match(weakSpirit, strongSpirit, Card(IngredientType.Bubbly, 3));
            Assert.AreEqual("spritz", match.Recipe.Id);
            CollectionAssert.Contains(match.ScoredCards.ToList(), strongSpirit);
            CollectionAssert.DoesNotContain(match.ScoredCards.ToList(), weakSpirit);
        }

        [Test]
        public void ScoredCards_KeepMixOrder()
        {
            var bubbly = Card(IngredientType.Bubbly, 2);
            var spirit = Card(IngredientType.Spirit, 6);
            var match = Match(bubbly, spirit); // bubbly listed first in the mix
            Assert.AreEqual("spritz", match.Recipe.Id);
            Assert.AreSame(bubbly, match.ScoredCards[0]);
            Assert.AreSame(spirit, match.ScoredCards[1]);
        }
    }
}
