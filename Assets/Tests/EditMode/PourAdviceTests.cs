using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// WHAT THE DRINKER SAYS ABOUT THE GLASS (2026-09-04). The note is coaching, so the two
    /// things it must never do are lie about the direction and leak the number — one would
    /// teach the player the wrong pour, the other would hand them the answer the whole
    /// perfect-pour mechanic exists to make them earn.
    /// </summary>
    public sealed class PourAdviceTests
    {
        private static string ReadDataFile(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static IReadOnlyList<IngredientCard> Cards()
        {
            var deck = DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json"));
            return deck.Cards.Concat(deck.LockedCards).ToList();
        }

        private static RecipeDefinition Recipe(string id) =>
            RecipeCatalog.CreateDefault().First(r => r.Id == id);

        /// <summary>
        /// A glass poured at the recipe's PERFECT, then one band nudged by <paramref name="off"/>
        /// with the difference shared out over the others so the glass still fills.
        ///
        /// The perfect and not the ideal, and the first cut of these tests got that wrong:
        /// <see cref="RatioRecipeMatcher.IdealPour"/> is the bands' own settled midpoint and
        /// the perfect is that walked off the box grid, so a glass built on the ideal is
        /// already a few points adrift of what the note measures against — which put the
        /// nudge in a race with a baseline error the test never meant to introduce.
        ///
        /// The nudged band ends up |off| out and every other band |off|/(n−1), so on a
        /// three-band drink the nudge is unambiguously the worst thing in the glass. A
        /// TWO-band drink cannot do that at all: its two shares sum to a glass, so "too much
        /// gin" and "too little tonic" are one fact and either name is right.
        /// </summary>
        private static GlassContents Pour(RecipeDefinition recipe, IReadOnlyList<IngredientCard> cards,
            int band = -1, double off = 0)
        {
            var shares = RatioRecipeMatcher.PerfectPour(recipe).ToArray();
            if (band >= 0)
            {
                shares[band] += off;
                for (int i = 0; i < shares.Length; i++)
                    if (i != band) shares[i] -= off / (shares.Length - 1);
            }
            var glass = new GlassContents(1.0);
            for (int i = 0; i < shares.Length; i++)
            {
                var b = recipe.RatioRequirements[i];
                var card = cards.First(c => b.IsStyleBand
                    ? c.Info?.Style == b.Style && (c.Info?.Tier ?? 1) >= b.MinTier
                    : c.Type == b.Type);
                glass.Add(card.Id, System.Math.Max(0, shares[i]));
            }
            return glass;
        }

        /// <summary>The index of a style's band on a recipe.</summary>
        private static int BandOf(RecipeDefinition recipe, string style)
        {
            for (int i = 0; i < recipe.RatioRequirements.Count; i++)
                if (recipe.RatioRequirements[i].Style == style) return i;
            Assert.Fail(recipe.Id + " has no '" + style + "' band any more");
            return -1;
        }

        private static PourNote NoteFor(RecipeDefinition recipe, GlassContents glass,
            IReadOnlyList<IngredientCard> cards) =>
            PourAdvice.For(recipe, glass, id => cards.FirstOrDefault(c => c.Id == id));

        [Test]
        public void TooMuchOfAnIngredient_AsksForLessOfThatIngredient()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");
            var note = NoteFor(sour, Pour(sour, cards, BandOf(sour, "gin"), +0.09), cards);

            Assert.That(note.Silent, Is.False, "a tenth of the glass out and the drinker said nothing");
            Assert.That(note.Flawless, Is.False);
            Assert.That(note.Ingredient, Is.EqualTo("gin"), "it named the wrong ingredient: " + note.Sentence);
            Assert.That(note.Direction, Is.EqualTo(-1), "too much gin and it asked for more: " + note.Sentence);
            StringAssert.Contains("less gin", note.Sentence);
        }

        [Test]
        public void TooLittleOfAnIngredient_AsksForMoreOfIt()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");
            var note = NoteFor(sour, Pour(sour, cards, BandOf(sour, "gin"), -0.09), cards);

            Assert.That(note.Ingredient, Is.EqualTo("gin"), note.Sentence);
            Assert.That(note.Direction, Is.EqualTo(1), note.Sentence);
            StringAssert.Contains("more gin", note.Sentence);
        }

        /// <summary>
        /// A TWO-BAND DRINK HAS ONE FAULT WITH TWO NAMES, and the note may use either: too
        /// much gin in a G&amp;T IS too little tonic, so this pins the thing that would
        /// actually be a bug — the note pointing the wrong WAY for whichever name it picks.
        /// </summary>
        [Test]
        public void OnATwoPartDrink_EitherNameIsRight_ButNotEitherDirection()
        {
            var cards = Cards();
            var gt = Recipe("gin_tonic");
            var note = NoteFor(gt, Pour(gt, cards, BandOf(gt, "gin"), +0.10), cards);

            Assert.That(note.Ingredient, Is.EqualTo("gin").Or.EqualTo("tonic"), note.Sentence);
            if (note.Ingredient == "gin")
                StringAssert.Contains("less gin", note.Sentence);
            else
                StringAssert.Contains("more tonic", note.Sentence);
        }

        /// <summary>
        /// THE POUR THE JUDGE CALLS PERFECT IS THE POUR THE DRINKER CALLS PERFECT. Two rules
        /// reading one window: if these two ever disagree the game congratulates a player it
        /// did not pay, or coaches one it did.
        /// </summary>
        [Test]
        public void ThePerfectPour_IsCalledFlawless_AndNamesNothing()
        {
            var cards = Cards();
            foreach (var recipe in RecipeCatalog.CreateDefault())
            {
                if (!recipe.HasAuthoredRatios) continue;
                var glass = new GlassContents(1.0);
                var exact = RatioRecipeMatcher.PerfectPour(recipe);
                for (int i = 0; i < exact.Length; i++)
                {
                    var b = recipe.RatioRequirements[i];
                    var card = cards.First(c => b.IsStyleBand
                        ? c.Info?.Style == b.Style && (c.Info?.Tier ?? 1) >= b.MinTier
                        : c.Type == b.Type);
                    glass.Add(card.Id, exact[i]);
                }
                var note = PourAdvice.For(recipe, glass, id => cards.FirstOrDefault(c => c.Id == id));
                Assert.That(note.Flawless, Is.True, recipe.Id + " poured perfectly and was coached: "
                    + note.Sentence);
                Assert.That(note.Ingredient, Is.Empty, recipe.Id + " named an ingredient on a perfect pour");
            }
        }

        /// <summary>A neat pour has no ratios and no head, so nobody lectures anyone.</summary>
        [Test]
        public void ADerivedRecipe_SaysNothingAtAll()
        {
            var cards = Cards();
            var neat = Recipe("neat_pour");
            var glass = new GlassContents(1.0);
            glass.Add(cards.First(c => c.Type == IngredientType.Spirit).Id, 0.8);

            var note = PourAdvice.For(neat, glass, id => cards.FirstOrDefault(c => c.Id == id));
            Assert.That(note.Silent, Is.True, "the neat pour was given ratio notes: " + note.Sentence);
        }

        /// <summary>A pint poured from the tap, its head in and out of the band.</summary>
        private static GlassContents Pint(IReadOnlyList<IngredientCard> cards, double head)
        {
            var glass = new GlassContents(1.0);
            glass.Add(cards.First(c => c.Type == IngredientType.Beer).Id, 0.9 - head);
            glass.AddHead(head);
            glass.AddPreparation(Preparations.Draught);
            return glass;
        }

        /// <summary>
        /// A PINT IS COACHED ON ITS HEAD (GDD 21 §10). Beer takes no ratio bands, so before
        /// this the ticket over a badly pulled pint said DRINKING… and nothing else — on an
        /// early bar, where draught is a real share of the orders.
        /// </summary>
        [Test]
        public void ATooFlatPint_AsksForMoreHead()
        {
            var cards = Cards();
            var note = PourAdvice.For(Recipe("draught"), Pint(cards, 0.01),
                id => cards.FirstOrDefault(c => c.Id == id));

            Assert.That(note.Silent, Is.False, "a flat pint said nothing");
            Assert.That(note.Ingredient, Is.EqualTo("head"));
            Assert.That(note.Direction, Is.EqualTo(1), note.Sentence);
            StringAssert.Contains("more head", note.Sentence);
        }

        [Test]
        public void AnAllFoamPint_AsksForLessHead()
        {
            var cards = Cards();
            var note = PourAdvice.For(Recipe("draught"), Pint(cards, 0.42),
                id => cards.FirstOrDefault(c => c.Id == id));

            Assert.That(note.Ingredient, Is.EqualTo("head"));
            Assert.That(note.Direction, Is.EqualTo(-1), note.Sentence);
            StringAssert.Contains("less head", note.Sentence);
        }

        [Test]
        public void APintInsideTheBand_IsPraised()
        {
            var cards = Cards();
            var note = PourAdvice.For(Recipe("draught"), Pint(cards, 0.14),
                id => cards.FirstOrDefault(c => c.Id == id));

            Assert.That(note.Flawless, Is.True, note.Sentence);
            StringAssert.Contains("Pulled just right", note.Sentence);
        }

        /// <summary>
        /// A GARNISH THAT NEVER ARRIVED IS THE SECOND HALF OF THE SENTENCE (2026-09-04, the
        /// author: "bu garnishleri de sipariş etmiştim eksik kalmış"). It is said after the
        /// pour, and it names what was asked for rather than what came.
        /// </summary>
        [Test]
        public void AMissingGarnish_IsSaidAfterThePour()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");
            var glass = Pour(sour, cards, BandOf(sour, "gin"), +0.09);
            var spec = new ServingSpec(new[] { Preparations.Ice, Preparations.LemonTwist });

            var note = PourAdvice.For(sour, glass, id => cards.FirstOrDefault(c => c.Id == id), spec);

            Assert.That(note.Silent, Is.False);
            StringAssert.Contains("less gin", note.Sentence);
            StringAssert.Contains("the ice and a lemon twist", note.Sentence);
            Assert.That(note.Sentence.IndexOf("gin"), Is.LessThan(note.Sentence.IndexOf("asked")),
                "the garnish was mentioned before the pour: " + note.Sentence);
        }

        /// <summary>What DID arrive is not complained about.</summary>
        [Test]
        public void AGarnishThatArrived_IsNotMentioned()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");
            var glass = Pour(sour, cards, BandOf(sour, "gin"), +0.09);
            glass.AddPreparation(Preparations.Ice);
            var spec = new ServingSpec(new[] { Preparations.Ice, Preparations.LemonTwist });

            var note = PourAdvice.For(sour, glass, id => cards.FirstOrDefault(c => c.Id == id), spec);

            StringAssert.Contains("a lemon twist", note.Sentence);
            StringAssert.DoesNotContain("the ice", note.Sentence);
        }

        /// <summary>
        /// EVERY CUSTOMER SAYS IT, including the ones whose drink has no ratios to coach:
        /// a pint without the ice it was ordered with is still a pint without its ice, and
        /// the pour half staying silent must not silence the whole note.
        /// </summary>
        [Test]
        public void ADrinkWithNoRatiosStillComplainsAboutAMissingGarnish()
        {
            var cards = Cards();
            var draught = Recipe("draught");
            var glass = new GlassContents(1.0);
            glass.Add(cards.First(c => c.Type == IngredientType.Beer).Id, 0.95);
            var spec = new ServingSpec(new[] { Preparations.Ice });

            var note = PourAdvice.For(draught, glass, id => cards.FirstOrDefault(c => c.Id == id), spec);

            Assert.That(note.Silent, Is.False, "the pint said nothing about its missing ice");
            StringAssert.Contains("the ice", note.Sentence);
            Assert.That(note.Ingredient, Is.Empty, "a pint was coached on an ingredient");
        }

        /// <summary>
        /// A PERFECT POUR WITH SOMETHING MISSING IS NOT FLAWLESS. The flag drives the
        /// celebration burst, and congratulating a player who forgot the ice is the game
        /// saying something it does not mean.
        /// </summary>
        [Test]
        public void APerfectPourMissingItsGarnish_IsNotCalledFlawless()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");
            var glass = Pour(sour, cards);          // dead on the perfect
            var spec = new ServingSpec(new[] { Preparations.LemonTwist });

            var note = PourAdvice.For(sour, glass, id => cards.FirstOrDefault(c => c.Id == id), spec);

            Assert.That(note.Flawless, Is.False, "a drink missing its twist was celebrated");
            StringAssert.Contains("a lemon twist", note.Sentence);
            StringAssert.DoesNotContain("Not a thing I would change", note.Sentence);
        }

        /// <summary>
        /// THE SENTENCE CARRIES NO NUMBER. The note is built inside Core, where the perfect
        /// pour is readable, and hands out a word — so this asserts the wall rather than
        /// trusting it: no digit may appear in anything a drinker says.
        /// </summary>
        [Test]
        public void NoNoteEverPrintsAFigure()
        {
            var cards = Cards();
            foreach (var recipe in RecipeCatalog.CreateDefault())
            {
                if (!recipe.HasAuthoredRatios) continue;
                for (int band = 0; band < recipe.RatioRequirements.Count; band++)
                    foreach (double off in new[] { -0.30, -0.14, -0.05, 0.05, 0.14, 0.30 })
                    {
                        var note = NoteFor(recipe, Pour(recipe, cards, band, off), cards);
                        if (note.Silent) continue;
                        Assert.That(note.Sentence.Any(char.IsDigit), Is.False,
                            recipe.Id + " leaked a figure: " + note.Sentence);
                    }
            }
        }

        /// <summary>
        /// The note is about the WORST band, and "worst" is the biggest correction in the
        /// glass. Pour one ingredient far out and another slightly out, and it must name the
        /// far one — a drinker who complains about the smaller of two faults is noise.
        /// </summary>
        [Test]
        public void ItNamesTheBiggestCorrection_NotTheFirstBand()
        {
            var cards = Cards();
            var sour = Recipe("gin_sour");

            // The SMALLEST band pulled the furthest: syrup is a fifth of this drink and gin
            // is over half of it, and the note must still name the syrup. Pouring it out by
            // 0.12 leaves gin and lemon 0.06 out apiece, so the syrup is the biggest single
            // correction in the glass even though it is the least of the drink.
            var note = NoteFor(sour, Pour(sour, cards, BandOf(sour, "syrup"), +0.12), cards);

            Assert.That(note.Ingredient, Is.EqualTo("syrup"), "it coached the wrong fault: " + note.Sentence);
            Assert.That(note.Direction, Is.EqualTo(-1), note.Sentence);
        }
    }
}
