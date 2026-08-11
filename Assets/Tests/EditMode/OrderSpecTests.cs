using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The v5 P11 order model (PLAN_service_depth): a drink plus how it is to be served, and
    /// a payment matrix where the base price is low and the tip is the whole reward for doing
    /// the job well. Also the two new ways a serve can end — refused, and declined.
    /// </summary>
    public class OrderSpecTests
    {
        private static RecipeDefinition Shakeable(string id = "sour", int rank = 6) =>
            new RecipeDefinition(id, id, rank, 30, 3, 25, 2,
                new[]
                {
                    new PatternRequirement(1, IngredientType.Spirit),
                    new PatternRequirement(1, IngredientType.Sour),
                },
                prep: PrepMethod.Shaken);

        private static RecipeDefinition Draught() =>
            new RecipeDefinition("draught", "Draught", 1, 5, 1, 10, 1,
                new[] { new PatternRequirement(1, IngredientType.Beer) },
                exactMixSize: 1, minFill: 0.75);

        private static RecipeDefinition Built() =>
            new RecipeDefinition("gt", "G&T", 15, 15, 2, 15, 1,
                new[] { new PatternRequirement(1, IngredientType.Spirit) },
                ratioRequirements: new[]
                {
                    new RatioRequirement("gin", 0.3, 0.5),
                    new RatioRequirement("tonic", 0.5, 0.7),
                },
                prep: PrepMethod.Built);

        private static GlassContents Full()
        {
            var g = new GlassContents(1.0);
            g.Add("gin", 0.5);
            g.Add("lemon", 0.45);
            return g;
        }

        // ── the spec rolls only what the recipe can honour ──────────────────────

        [Test]
        public void APintIsNeverAskedForAGarnish_OrAShake()
        {
            var rng = new RunRng("pint-spec").GetStream("orders");
            var draught = Draught();

            for (int i = 0; i < 400; i++)
            {
                var spec = ServingSpec.Roll(draught, rng);
                Assert.IsEmpty(spec.Garnishes, "a pint takes no garnish");
                Assert.IsTrue(spec.IsPlain,
                    "and with the fill demand retired (2026-08-02) a pint's spec is always plain");
            }
        }

        // ── the method is the recipe's, and the judge reads it (2026-08-11) ─────
        // "Extra shaken" — a customer whim rolled at 25% — retired with this: how a
        // drink is worked belongs to the BOOK, and the judge grades the delivered
        // glass against RecipeDefinition.Prep.

        private static RecipeDefinition Stirred(string id = "martini") =>
            new RecipeDefinition(id, id, rank: 16, baseFlavor: 30, baseMult: 3,
                flavorPerLevel: 25, multPerLevel: 2,
                requirements: new[] { new PatternRequirement(1, IngredientType.Spirit) },
                prep: PrepMethod.Stirred);

        [Test]
        public void AShakenRecipe_ServedUnshaken_PaysForItOutOfTheTip()
        {
            var order = new DrinkOrder(Shakeable(), 10);
            var shaken = Full(); shaken.AddPreparation(Preparations.Shaken);
            var still = Full();

            var right = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact, shaken);
            var wrongWay = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact, still);

            Assert.AreEqual(right.BasePaid, wrongWay.BasePaid, "still the drink; still paid");
            Assert.Less(wrongWay.Tip, right.Tip, "but the book said SHAKE, and nobody did");
        }

        [Test]
        public void AStirredRecipe_Shaken_IsBruised_AndScoresTheSameAsUnmixed()
        {
            var order = new DrinkOrder(Stirred(), 10);
            var bruised = Full(); bruised.AddPreparation(Preparations.Shaken);
            var still = Full();

            var b = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact, bruised);
            var s = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact, still);
            Assert.AreEqual(s.Tip, b.Tip, "a shaken Martini is bruised — the wrong mix earns no more than none");

            var stirred = Full(); stirred.AddPreparation(Preparations.Stirred);
            var st = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact, stirred);
            Assert.Greater(st.Tip, b.Tip, "the spoon is what the book asked for");
        }

        [Test]
        public void ABuiltRecipe_DoesNotCareHowItWasMixed()
        {
            Assert.AreEqual(1.0, ServiceJudge.MethodScore(Built(), Full()), 1e-9);
            var stirred = Full(); stirred.AddPreparation(Preparations.Stirred);
            Assert.AreEqual(1.0, ServiceJudge.MethodScore(Built(), stirred), 1e-9,
                "the tin-built black russian's mandatory stir costs nothing");
        }

        // ── the spec is graded ──────────────────────────────────────────────────

        [Test]
        public void EveryPartOfTheSpec_IsGradedIndependently()
        {
            var spec = new ServingSpec(new[] { Preparations.Ice, Preparations.LemonTwist });
            Assert.AreEqual(2, spec.RequestCount);

            GlassContents Short()
            {
                var g = new GlassContents(1.0);
                g.Add("gin", 0.45);
                g.Add("lemon", 0.40);
                return g;
            }

            var none = Short();
            Assert.AreEqual(0.0, spec.Delivered(none), 1e-9, "nothing asked for was done");

            var iced = Short();
            iced.AddPreparation(Preparations.Ice);
            Assert.AreEqual(1.0 / 2.0, spec.Delivered(iced), 1e-9);

            var both = Short();
            both.AddPreparation(Preparations.Ice);
            both.AddPreparation(Preparations.LemonTwist);
            Assert.AreEqual(1.0, spec.Delivered(both), 1e-9);
        }

        [Test]
        public void APlainOrder_CannotBeGotWrong()
        {
            Assert.AreEqual(1.0, ServingSpec.Plain.Delivered(new GlassContents(1.0)), 1e-9);
        }

        // ── payment matrix ──────────────────────────────────────────────────────

        [Test]
        public void APlainDrinkPouredPlainly_EarnsNoExtraRound()
        {
            // The craft gate is unchanged from v4: the extra round is the reward for reading
            // what someone wanted DOING to the drink and nailing it. Scoring it off the raw
            // spec score would hand every plain order a free round.
            var verdict = ServiceJudge.Judge(
                new CustomerVisit(new DrinkOrder(Shakeable(), 10), 60), OrderMatch.Exact, Full());
            Assert.IsFalse(verdict.OrdersAgain);
            Assert.IsFalse(verdict.CraftLanded);
            Assert.AreEqual(1.0, verdict.SpecScore, 1e-9, "asking for nothing cannot be got wrong");
        }

        [Test]
        public void APerfectServe_DoublesTheDrink_AndACarelessOneDoesNot()
        {
            var order = new DrinkOrder(Shakeable(), 10,
                new ServingSpec(new[] { Preparations.Ice }));

            var perfectGlass = Full();
            perfectGlass.AddPreparation(Preparations.Ice);
            perfectGlass.AddPreparation(Preparations.Shaken);   // the book's own method, since 2026-08-11
            var perfect = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact,
                perfectGlass);

            var careless = ServiceJudge.Judge(new CustomerVisit(order, 60), OrderMatch.Exact,
                Full());   // no ice

            Assert.AreEqual(10, perfect.BasePaid);
            Assert.AreEqual(10, perfect.Tip, "spec + fill + speed all full: the tip equals the base");
            Assert.AreEqual(10, careless.BasePaid, "still the right drink, still paid");
            Assert.Less(careless.Tip, perfect.Tip, "but carelessness is paid for out of the tip");
        }

        [Test]
        public void ABrokeCrowd_NeverTips()
        {
            var glass = Full();
            var verdict = ServiceJudge.Judge(new CustomerVisit(new DrinkOrder(Shakeable(), 10), 60),
                OrderMatch.Exact, glass, WealthTier.Broke);
            Assert.AreEqual(0, verdict.Tip);
            Assert.AreEqual(10, verdict.BasePaid, "they still pay for the drink");
        }

        // ── who gets to order twice ─────────────────────────────────────────────

        [Test]
        public void AFirstTimer_OrdersOnce_AReturningRegularMayOrderAgain()
        {
            // An asked-for spec, delivered: the craft gate is satisfied for both, so the only
            // thing separating them is whether they have been in before.
            var order = new DrinkOrder(Shakeable(), 10, new ServingSpec(new[] { Preparations.Ice }));
            var glass = Full();
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Shaken);

            var newcomer = new RegularState("r1", "Newcomer", "arch");
            var known = new RegularState("r2", "Known", "arch");
            known.RecordVisit(3);   // been in before

            Assert.AreEqual(0, newcomer.Visits);
            Assert.GreaterOrEqual(known.Visits, 1);

            var first = ServiceJudge.Judge(new CustomerVisit(order, 60, newcomer), OrderMatch.Exact, glass);
            var repeat = ServiceJudge.Judge(new CustomerVisit(order, 60, known), OrderMatch.Exact, glass);

            Assert.IsFalse(first.OrdersAgain, "a first-timer orders once (v5 P11)");
            Assert.IsTrue(repeat.OrdersAgain, "someone who has been in before may order again");
        }

        [Test]
        public void AnAnonymousCrowd_KeepsTheOldBehaviour()
        {
            // No emotion layer means nobody is remembered, so "returning" has no meaning and
            // the extra order stays available -- the opt-in rule (CLAUDE.md) holds.
            var iced = Full();
            iced.AddPreparation(Preparations.Ice);
            iced.AddPreparation(Preparations.Shaken);
            var verdict = ServiceJudge.Judge(
                new CustomerVisit(new DrinkOrder(Shakeable(), 10,
                    new ServingSpec(new[] { Preparations.Ice })), 60), OrderMatch.Exact, iced);
            Assert.IsTrue(verdict.OrdersAgain);
        }

        // ── declining an order (C2) ─────────────────────────────────────────────

        [Test]
        public void DecliningAnOrder_PaysNothing_ButBeatsAStormOff()
        {
            var declined = ServiceJudge.Declined();
            Assert.AreEqual(OrderMatch.Declined, declined.Match);
            Assert.AreEqual(0, declined.Total);
            Assert.Greater(declined.Satisfaction, 0.0,
                "being told straight is not the same as being ignored -- a storm-off scores 0");
        }

        [Test]
        public void CanMake_ReadsTheShelf_NotTheMenu()
        {
            var gin = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6);
            var lemon = new IngredientCard("lemon", "Lemon", IngredientType.Sour, 3);
            var shelf = new Shelf(new[] { new ShelfBottle(gin, 20), new ShelfBottle(lemon, 20) });
            var run = new TycoonRun(shelf, RecipeCatalog.CreateDefault(), new RunRng("stock"));
            var order = new DrinkOrder(Shakeable(), 10);

            Assert.IsTrue(run.CanMake(order), "both bands have stock");

            shelf.Find("lemon").Draw(20);   // the sour runs dry
            Assert.IsTrue(shelf.Find("lemon").IsEmpty);
            Assert.IsFalse(run.CanMake(order), "an order the bar cannot answer is visible as such");
        }

        // ── style-banded orders have no "near enough" ───────────────────────────

        [Test]
        public void AStyleBandedOrder_HasNoFamily_SoOnlyTheExactDrinkWillDo()
        {
            var gin = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6,
                QualityTier.HousePour, new IngredientInfo("gin", category: IngredientCategories.Gin));
            var lookup = new Dictionary<string, IngredientCard> { ["gin"] = gin };

            var neatGin = new GlassContents(1.0);
            neatGin.Add("gin", 0.9);

            var order = new DrinkOrder(Built(), 10);
            Assert.AreEqual(OrderMatch.Wrong,
                ServiceJudge.Compare(order, null, neatGin, id => lookup[id]),
                "a G&T is specified down to its bottles; neat gin is not nearly one");
        }
    }
}
