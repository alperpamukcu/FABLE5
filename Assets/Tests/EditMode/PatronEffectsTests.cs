using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class PatronEffectsTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);

        private static PatronInstance Patron(string name, params PatronEffect[] effects) =>
            new PatronInstance(new PatronDefinition(
                name.ToLowerInvariant().Replace(' ', '_'), name, PatronRarity.Common, 4, "", effects));

        private static ScoreBreakdown Score(IReadOnlyList<PatronInstance> patrons,
            EffectContext ctxOverride, params IngredientCard[] mix)
        {
            var match = RecipeMatcher.Match(mix, Recipes);
            var ctx = ctxOverride ?? new EffectContext(mix, match?.Recipe, 0, 0);
            return ScoringEngine.Score(match, 1, patrons, ctx);
        }

        private static ScoreBreakdown Score(IReadOnlyList<PatronInstance> patrons,
            params IngredientCard[] mix) => Score(patrons, null, mix);

        // ── hand-trigger conditions ──────────────────────────────────────────────

        [Test]
        public void SailorMusa_AddsMult_WhenMixContainsSpirit()
        {
            var musa = Patron("Sailor Musa", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.AddMult, 4, EffectCondition.MixContainsType(IngredientType.Spirit)));

            // Neat Pour: (5+6) x (1+4) = 55
            var result = Score(new[] { musa }, Card(IngredientType.Spirit, 6));
            Assert.AreEqual(55, result.FinalScore);
        }

        [Test]
        public void ThePoet_AddsFlavor_OnlyForThreeCardMixes()
        {
            var poet = Patron("The Poet", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.AddFlavor, 30, EffectCondition.MixSizeEquals(3)));

            // Old Fashioned: (20+13+30) x 2 = 126
            var three = Score(new[] { poet },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(126, three.FinalScore);

            // Spritz (2 cards): unaffected — (10+8) x 2 = 36
            var two = Score(new[] { poet },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2));
            Assert.AreEqual(36, two.FinalScore);
        }

        [Test]
        public void JazzSinger_TriplesMult_OnMartiniAndNegroniOnly()
        {
            var singer = Patron("Jazz Singer", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.MultMult, 3, EffectCondition.RecipeIdIn("martini", "negroni")));

            // Martini: (35+13) x (4x3) = 576
            var martini = Score(new[] { singer },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Spirit, 4), Card(IngredientType.Bitter, 3));
            Assert.AreEqual(576, martini.FinalScore);

            var spritz = Score(new[] { singer },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2));
            Assert.AreEqual(36, spritz.FinalScore, "non-listed recipes are unaffected");
        }

        [Test]
        public void OffDutyCop_DoublesMult_OnlyWithZeroRestocksUsed()
        {
            var cop = Patron("Off-Duty Cop", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.MultMult, 2, EffectCondition.RestocksUsedEquals(0)));
            var mix = new[] { Card(IngredientType.Spirit, 6) };
            var match = RecipeMatcher.Match(mix, Recipes);

            var clean = ScoringEngine.Score(match, 1, new[] { cop }, new EffectContext(mix, match.Recipe, 0, 0));
            var afterRestock = ScoringEngine.Score(match, 1, new[] { cop }, new EffectContext(mix, match.Recipe, 0, 1));

            Assert.AreEqual(22, clean.FinalScore);
            Assert.AreEqual(11, afterRestock.FinalScore);
        }

        // ── per-card triggers ────────────────────────────────────────────────────

        [Test]
        public void AuntPerihan_BoostsScoredSweetCardsOnly()
        {
            var perihan = Patron("Aunt Perihan", new PatronEffect(EffectTrigger.OnCardScored,
                EffectOp.AddFlavor, 15, EffectCondition.CardTypeIs(IngredientType.Sweet)));

            // Old Fashioned + an extra unscored Sweet: pattern keeps the higher sweet (5).
            // (20 + 6+5+3 + 15) x 2 = 98 — the unscored Sweet(3) triggers nothing.
            // (Values 6/3/5/3 on purpose: 4 distinct values would be a Layered Pour, v1.1.)
            var result = Score(new[] { perihan },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 3),
                Card(IngredientType.Sweet, 5), Card(IngredientType.Bitter, 3));
            Assert.AreEqual("old_fashioned", result.Recipe.Id);
            Assert.AreEqual(98, result.FinalScore);
        }

        [Test]
        public void TheChemist_AddsMultPerScoredSourCard()
        {
            var chemist = Patron("The Chemist", new PatronEffect(EffectTrigger.OnCardScored,
                EffectOp.AddMult, 2, EffectCondition.CardTypeIs(IngredientType.Sour)));

            // Fizz: (45+13) x (4+2) = 348
            var result = Score(new[] { chemist },
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 2), Card(IngredientType.Bubbly, 1));
            Assert.AreEqual(348, result.FinalScore);
        }

        [Test]
        public void Twins_RetriggerFirstScoredCard()
        {
            var twins = Patron("Twins at Table 4", new PatronEffect(EffectTrigger.OnCardScored,
                EffectOp.Retrigger, 1, EffectCondition.CardIndexEquals(0)));

            // Neat Pour, spirit 6 scores twice: (5+6+6) x 1 = 17
            var result = Score(new[] { twins }, Card(IngredientType.Spirit, 6));
            Assert.AreEqual(17, result.FinalScore);
            Assert.IsTrue(result.Steps.Any(s => s.Op == EffectOp.Retrigger), "breakdown logs the retrigger");
        }

        // ── ordering & scaling ───────────────────────────────────────────────────

        [Test]
        public void PatronSlotOrder_ChangesTheResult()
        {
            var adder = Patron("Adder", new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 4));
            var doubler = Patron("Doubler", new PatronEffect(EffectTrigger.OnHandScored, EffectOp.MultMult, 2));
            var mix = new[] { Card(IngredientType.Spirit, 6) };

            // 11 x ((1+4)x2) = 110 vs 11 x (1x2+4) = 66
            Assert.AreEqual(110, Score(new[] { adder, doubler }, mix[0]).FinalScore);
            Assert.AreEqual(66, Score(new[] { doubler, adder }, mix[0]).FinalScore);
        }

        [Test]
        public void TheCollector_GrowsWithEachPerfectServe()
        {
            var collector = Patron("The Collector",
                new PatronEffect(EffectTrigger.OnHandScored, EffectOp.Accumulate, 1,
                    EffectCondition.RecipeIdIn("perfect_serve", "double_perfect")),
                new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 0,
                    valueSource: EffectValueSource.Accumulated));
            var patrons = new[] { collector };

            IngredientCard[] Perfect() => new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sour, 4),
                Card(IngredientType.Sweet, 2), Card(IngredientType.Bubbly, 1),
                Card(IngredientType.Garnish, 5)
            };

            // 1st Perfect Serve: base 100x8, +18 card flavor, grows to 1 then +1 => 118 x 9
            Assert.AreEqual(118 * 9, Score(patrons, Perfect()).FinalScore);
            // 2nd: grows to 2 => 118 x 10
            Assert.AreEqual(118 * 10, Score(patrons, Perfect()).FinalScore);
            // A later Neat Pour still benefits: 11 x (1+2) = 33
            Assert.AreEqual(33, Score(patrons, Card(IngredientType.Spirit, 6)).FinalScore);
        }

        [Test]
        public void NoRecipeMix_FiresNoPatronEffects()
        {
            var musa = Patron("Sailor Musa", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.AddMult, 4));
            var result = Score(new[] { musa }, Card(IngredientType.Sour, 4), Card(IngredientType.Sweet, 2));
            Assert.AreEqual(0, result.FinalScore);
            Assert.AreEqual(0, result.Steps.Count);
        }

        // ── round integration ────────────────────────────────────────────────────

        private static Deck SpiritDeck(int count) =>
            new Deck(Enumerable.Range(0, count).Select(_ => Card(IngredientType.Spirit, 6)));

        [Test]
        public void Insomniac_DoublesOnlyTheFirstMixOfTheCustomer()
        {
            var insomniac = Patron("The Insomniac", new PatronEffect(EffectTrigger.OnHandScored,
                EffectOp.MultMult, 2, EffectCondition.MixesUsedEquals(0)));
            var round = new RoundController(SpiritDeck(48), Recipes,
                new CustomerOrder("Test", 100000), patrons: new[] { insomniac });

            var first = round.Mix(new[] { round.Rail[0] });
            var second = round.Mix(new[] { round.Rail[0] });

            Assert.AreEqual(22, first.FinalScore, "first mix: 11 x (1x2)");
            Assert.AreEqual(11, second.FinalScore, "second mix: no bonus");
        }

        [Test]
        public void NightCabbie_PaysOut_WhenCustomerIsSatisfied()
        {
            var cabbie = Patron("The Night Cabbie", new PatronEffect(EffectTrigger.OnCustomerEnd,
                EffectOp.AddMoney, 2));
            var round = new RoundController(SpiritDeck(48), Recipes,
                new CustomerOrder("Test", 10), patrons: new[] { cabbie });

            Assert.AreEqual(0, round.PatronPayout);
            round.Mix(new[] { round.Rail[0] }); // 11 >= 10 => Won

            Assert.AreEqual(RoundPhase.Won, round.Phase);
            Assert.AreEqual(2, round.PatronPayout);
        }

        [Test]
        public void PreviewScore_DoesNotGrowScalingPatrons()
        {
            var collector = Patron("The Collector",
                new PatronEffect(EffectTrigger.OnHandScored, EffectOp.Accumulate, 1,
                    EffectCondition.RecipeIdIn("neat_pour")),
                new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 0,
                    valueSource: EffectValueSource.Accumulated));
            var round = new RoundController(SpiritDeck(48), Recipes,
                new CustomerOrder("Test", 100000), patrons: new[] { collector });
            var selection = new[] { round.Rail[0] };

            round.PreviewScore(selection);
            round.PreviewScore(selection);
            Assert.AreEqual(0, collector.Accumulated, "previews must not mutate run state");

            round.Mix(selection);
            Assert.AreEqual(1, collector.Accumulated, "a real mix does");
        }
    }
}
