using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// Beer is pulled, not built (GDD 21 §10): it comes from a keg, never sees the shaker, and
    /// the whole craft is how far the handle was pulled. These cover the pull, the head that
    /// comes with it, what a settling head gives back, and how a pint is judged.
    /// </summary>
    public sealed class DraughtBeerTests
    {
        private static IngredientCard Keg(string id = "beer_test") =>
            new IngredientCard(id, "Test Lager", IngredientType.Beer, 3);

        private static TycoonRun RunWithKeg(out string kegId)
        {
            kegId = "beer_test";
            var shelf = new Shelf(new[]
            {
                new ShelfBottle(Keg()),
                new ShelfBottle(new IngredientCard("spirit_a", "Spirit A", IngredientType.Spirit, 5)),
            });
            return new TycoonRun(shelf, RecipeCatalog.CreateDefault(), new RunRng("beer-seed"));
        }

        // ── the angle of the glass (GDD 21 §10.2) ───────────────────────────────

        [Test]
        public void AGlassHeldUprightCatchesMostlyFroth()
        {
            var upright = TapPour.Flow(0.0, 1.0);
            Assert.Greater(upright.Head / upright.Caught, 0.5,
                "the stream drops the height of the glass and breaks — this is the mistake");
            Assert.AreEqual(0, upright.Spill, 1e-9, "nothing misses a glass that is straight up");
        }

        [Test]
        public void AGlassLeanedOverCatchesAlmostCleanBeer()
        {
            var tilted = TapPour.Flow(TapPour.IdealTilt, 1.0);
            Assert.Less(tilted.Head / tilted.Caught, 0.12,
                "run down the wall, beer keeps its gas");
            Assert.AreEqual(0, tilted.Spill, 1e-9);
        }

        [Test]
        public void TippingItPastTheSpillAngleThrowsBeerOnTheFloor()
        {
            Assert.AreEqual(0, TapPour.Flow(TapPour.SpillTilt, 1.0).Spill, 1e-9,
                "the spill starts past the angle, not at it");
            Assert.Greater(TapPour.Flow(TapPour.SpillTilt + 12, 1.0).Spill, 0);
            Assert.Greater(TapPour.Flow(85, 1.0).Spill, TapPour.Flow(70, 1.0).Spill,
                "the further it goes over, the more runs by the rim");
        }

        [Test]
        public void TheTapRunsAtOneRateWhateverTheAngle()
        {
            Assert.AreEqual(TapPour.Flow(0, 1.0).Total, TapPour.Flow(45, 1.0).Total, 1e-9);
            Assert.AreEqual(TapPour.Flow(45, 1.0).Total, TapPour.Flow(75, 1.0).Total, 1e-9,
                "the angle decides where it lands, never how much comes out");
        }

        [Test]
        public void MostOfTheFoamControlIsNearUpright()
        {
            // Squared falloff: standing the glass up at the end is what raises a head, and
            // the last few degrees are where it arrives.
            double nearUpright = TapPour.Flow(10, 1.0).Head - TapPour.Flow(20, 1.0).Head;
            double nearIdeal = TapPour.Flow(30, 1.0).Head - TapPour.Flow(40, 1.0).Head;
            Assert.Greater(nearUpright, nearIdeal);
        }

        [Test]
        public void NoTimeMeansNoBeer()
        {
            Assert.AreEqual(0, TapPour.Flow(45, 0).Total, 1e-9);
        }

        // ── the head in the glass ───────────────────────────────────────────────

        [Test]
        public void FoamTakesUpRoomInTheGlass()
        {
            var glass = new GlassContents(1.0);
            glass.Add("beer_test", 0.5);
            glass.AddHead(0.3);

            Assert.AreEqual(0.8, glass.FillFraction, 1e-9, "head counts toward how full it looks");
            Assert.AreEqual(0.5, glass.TotalVolume, 1e-9, "but it is not beer");
            Assert.AreEqual(0.2, glass.Headroom, 1e-9);
        }

        [Test]
        public void AGlassFullOfFoamRefusesMoreBeer()
        {
            var glass = new GlassContents(1.0);
            glass.AddHead(1.0);
            Assert.AreEqual(0, glass.Add("beer_test", 0.5), 1e-9,
                "froth to the brim means there is nowhere left to put the beer");
        }

        [Test]
        public void ASettlingHeadGivesBackSomeBeerAndLeavesRoomToTopUp()
        {
            var glass = new GlassContents(1.0);
            glass.Add("beer_test", 0.4);
            glass.AddHead(0.6);
            Assert.AreEqual(1.0, glass.FillFraction, 1e-9);

            glass.CollapseHead(0.6, TapPour.FoamLiquidShare);

            Assert.AreEqual(0, glass.Head, 1e-9);
            Assert.AreEqual(0.4 + 0.6 * TapPour.FoamLiquidShare, glass.TotalVolume, 1e-9,
                "most of a head is air and is lost when it falls");
            Assert.Less(glass.FillFraction, 1.0, "which is what leaves room to top the pint up");
        }

        [Test]
        public void HeadSettlesFastAtFirstAndLingersAtTheEnd()
        {
            double firstSecond = TapPour.Settled(1.0, 1.0);
            double remaining = 1.0 - firstSecond;
            double secondSecond = TapPour.Settled(remaining, 1.0);
            Assert.Greater(firstSecond, secondSecond,
                "exponential decay: the froth drops away, the last skim clings");
        }

        // ── what a good pint is worth ───────────────────────────────────────────

        [Test]
        public void TheGoodHeadBandScoresFull()
        {
            Assert.AreEqual(1.0, TapPour.HeadScore(TapPour.IdealHead), 1e-9);
            Assert.AreEqual(1.0, TapPour.HeadScore(TapPour.GoodHeadMin), 1e-9);
            Assert.AreEqual(1.0, TapPour.HeadScore(TapPour.GoodHeadMax), 1e-9);
        }

        [Test]
        public void FlatBeerAndAGlassOfFrothBothScoreNothing()
        {
            Assert.AreEqual(0.0, TapPour.HeadScore(0.0), 1e-9, "no head at all is a flat pint");
            Assert.AreEqual(0.0, TapPour.HeadScore(TapPour.HeadTolerance), 1e-9);
            Assert.AreEqual(0.0, TapPour.HeadScore(0.9), 1e-9, "a glass of froth is not a pint");
        }

        [Test]
        public void HeadScoreFallsOffSmoothlyOutsideTheBand()
        {
            double justOver = TapPour.HeadScore(TapPour.GoodHeadMax + 0.02);
            double wellOver = TapPour.HeadScore(TapPour.GoodHeadMax + 0.05);
            Assert.Greater(justOver, wellOver);
            Assert.Greater(justOver, 0.0);
            Assert.Less(justOver, 1.0);
        }

        // ── the run's tap verbs ─────────────────────────────────────────────────

        [Test]
        public void PullingFillsTheServingGlassAndNeverTheShaker()
        {
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            run.PourTilted(1.0, 20.0);

            Assert.IsTrue(run.Glass.IsEmpty, "beer must not pass through the shaker");
            Assert.IsFalse(run.ServingGlass.IsEmpty);
            Assert.Greater(run.ServingGlass.Head, 0, "a pull always raises some head");
            Assert.IsTrue(run.ServingGlass.HasPreparation(Preparations.Draught.Id));
        }

        [Test]
        public void OnlyAKegCanBePulled()
        {
            var run = RunWithKeg(out _);
            Assert.Throws<System.ArgumentException>(() => run.BeginPull("spirit_a"));
            Assert.Throws<System.ArgumentException>(() => run.BeginPull("nothing_here"));
        }

        [Test]
        public void ACocktailOnTheGoBlocksTheTap()
        {
            var run = RunWithKeg(out string kegId);
            run.PourMeasure("spirit_a", 0.3);
            Assert.Throws<System.InvalidOperationException>(() => run.BeginPull(kegId));
        }

        [Test]
        public void PullingDrawsFromTheKegAndStopsWhenItRunsDry()
        {
            var shelf = new Shelf(new[] { new ShelfBottle(Keg(), capacity: 0.2) });
            var run = new TycoonRun(shelf, RecipeCatalog.CreateDefault(), new RunRng("dry"));
            run.BeginPull("beer_test");
            for (int i = 0; i < 20; i++) run.PourTilted(0.5, 45.0);

            Assert.IsTrue(shelf.Find("beer_test").IsEmpty, "the keg empties like any bottle");
            Assert.IsNull(run.PullingId, "a dry keg closes its own tap");
        }

        [Test]
        public void TheGlassNeverTakesMoreThanItHolds()
        {
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            for (int i = 0; i < 40; i++) run.PourTilted(0.5, 45.0);

            Assert.LessOrEqual(run.ServingGlass.FillFraction, 1.0 + 1e-9);
        }

        [Test]
        public void SettlingTheHeadIsDrivenByTheRunClock()
        {
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            run.PourTilted(1.0, 0.0);          // held dead upright: all froth
            run.EndPull();
            double headBefore = run.ServingGlass.Head;
            double beerBefore = run.ServingGlass.TotalVolume;

            run.SettleHead(3.0);

            Assert.Less(run.ServingGlass.Head, headBefore, "foam falls while you stand there");
            Assert.Greater(run.ServingGlass.TotalVolume, beerBefore, "some of it comes back as beer");
        }

        [Test]
        public void SpilledBeerLeavesTheKegAndReachesNobody()
        {
            var run = RunWithKeg(out string kegId);
            var keg = run.Shelf.Find(kegId);
            double before = keg.Remaining;

            run.BeginPull(kegId);
            for (int i = 0; i < 10; i++) run.PourTilted(0.1, 80.0);   // held nearly on its side

            Assert.Greater(run.SpilledBeer, 0, "beer poured past the rim is gone");
            Assert.Less(keg.Remaining, before, "and it came out of the keg all the same");
            Assert.Less(run.ServingGlass.FillFraction, 0.5, "very little of it reached the glass");
        }

        [Test]
        public void TheTwoMovementPourIsWhatMakesAGoodPint()
        {
            // Tilted to fill, straightened at the end to raise the head — the pint the mechanic
            // is built around (GDD 21 §10.2).
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            for (int i = 0; i < 34; i++) run.PourTilted(0.05, 45.0);   // fill it leaning over
            for (int i = 0; i < 10; i++) run.PourTilted(0.05, 6.0);    // stand it up at the end

            var g = run.ServingGlass;
            Assert.AreEqual(1.0, TapPour.HeadScore(g.Head / g.Capacity), 1e-9,
                "that is a good pint");
            Assert.Greater(g.FillFraction, 0.85, "and a full one");
        }

        [Test]
        public void PouringItUprightThroughoutServesAGlassOfFroth()
        {
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            for (int i = 0; i < 44; i++) run.PourTilted(0.05, 0.0);

            var g = run.ServingGlass;
            Assert.Greater(g.Head / g.Capacity, TapPour.GoodHeadMax, "far too much head");
            Assert.AreEqual(0.0, TapPour.HeadScore(g.Head / g.Capacity), 1e-9);
        }

        [Test]
        public void PouringItLeaningOverThroughoutServesAFlatOne()
        {
            var run = RunWithKeg(out string kegId);
            run.BeginPull(kegId);
            for (int i = 0; i < 44; i++) run.PourTilted(0.05, 50.0);

            var g = run.ServingGlass;
            Assert.Less(g.Head / g.Capacity, TapPour.GoodHeadMin, "barely a head on it");
            Assert.Less(TapPour.HeadScore(g.Head / g.Capacity), 1.0);
        }

        // ── it reads as a drink ─────────────────────────────────────────────────

        [Test]
        public void AFullGlassOfBeerIdentifiesAsDraught()
        {
            var recipes = RecipeCatalog.CreateDefault();
            var glass = new GlassContents(1.0);
            glass.Add("beer_test", 0.86);
            glass.AddHead(0.14);

            var match = RatioRecipeMatcher.Match(glass, recipes, id => Keg(id));
            Assert.IsNotNull(match?.Recipe);
            Assert.AreEqual("draught", match.Recipe.Id);
        }

        [Test]
        public void HalfAGlassOfBeerIsNotADraught()
        {
            var recipes = RecipeCatalog.CreateDefault();
            var glass = new GlassContents(1.0);
            glass.Add("beer_test", 0.4);

            var match = RatioRecipeMatcher.Match(glass, recipes, id => Keg(id));
            Assert.AreNotEqual("draught", match?.Recipe?.Id,
                "a short pint is not the drink they asked for");
        }

        [Test]
        public void NeatSpiritIsNeverADraughtAndBeerIsNeverANeatPour()
        {
            var recipes = RecipeCatalog.CreateDefault();
            var spirit = new IngredientCard("spirit_a", "Spirit A", IngredientType.Spirit, 5);

            var neat = new GlassContents(1.0);
            neat.Add("spirit_a", 1.0);
            Assert.AreEqual("neat_pour",
                RatioRecipeMatcher.Match(neat, recipes, id => spirit)?.Recipe?.Id);

            var pint = new GlassContents(1.0);
            pint.Add("beer_test", 0.9);
            pint.AddHead(0.1);
            Assert.AreEqual("draught",
                RatioRecipeMatcher.Match(pint, recipes, id => Keg(id))?.Recipe?.Id);
        }

        [Test]
        public void DraughtIsOnTheMenuFromDayOne()
        {
            var pourable = RecipeCatalog.CreateDefault()
                .Where(r => r.RatioRequirements.Count > 0)
                .OrderBy(r => r.Rank)
                .Take(TycoonConfig.Default.OrderPoolSize(1))
                .Select(r => r.Id);
            CollectionAssert.Contains(pourable.ToList(), "draught",
                "beer is the order a new bar can always answer");
        }

        // ── judging the pint ────────────────────────────────────────────────────

        private static CustomerVisit PintDrinker(RecipeDefinition draught) =>
            new CustomerVisit(new DrinkOrder(draught, 5), patienceSeconds: 60, decideSeconds: 0);

        private static GlassContents Pint(double head)
        {
            var glass = new GlassContents(1.0);
            glass.Add("beer_test", 1.0 - head);
            glass.AddHead(head);
            glass.AddPreparation(Preparations.Draught);
            return glass;
        }

        [Test]
        public void AWellPulledPintSatisfiesMoreThanAFrothyOne()
        {
            var draught = RecipeCatalog.CreateDefault().First(r => r.Id == "draught");

            var good = ServiceJudge.Judge(PintDrinker(draught), OrderMatch.Exact, Pint(TapPour.IdealHead));
            var froth = ServiceJudge.Judge(PintDrinker(draught), OrderMatch.Exact, Pint(0.6));

            Assert.Greater(good.Satisfaction, froth.Satisfaction);
            Assert.IsTrue(good.CraftLanded, "a good pull is the pint's craft");
            Assert.IsFalse(froth.CraftLanded);
        }

        [Test]
        public void AGoodPullEarnsTheExtraRound()
        {
            var draught = RecipeCatalog.CreateDefault().First(r => r.Id == "draught");
            var verdict = ServiceJudge.Judge(PintDrinker(draught), OrderMatch.Exact, Pint(TapPour.IdealHead));
            Assert.IsTrue(verdict.OrdersAgain);
        }

        [Test]
        public void ACocktailIsNeverGradedOnAHeadItCannotHave()
        {
            var martini = RecipeCatalog.CreateDefault().First(r => r.Id == "neat_pour");
            var glass = new GlassContents(1.0);
            glass.Add("spirit_a", 1.0);

            var visit = new CustomerVisit(new DrinkOrder(martini, 5), patienceSeconds: 60, decideSeconds: 0);
            var verdict = ServiceJudge.Judge(visit, OrderMatch.Exact, glass);

            Assert.AreEqual(0.9, verdict.Satisfaction, 1e-9,
                "no draught mark means no head term at all");
        }

        // ── the keg is a keg ────────────────────────────────────────────────────

        [Test]
        public void AKegHoldsMoreAndPoursFasterThanABottle()
        {
            var keg = new ShelfBottle(Keg());
            var bottle = new ShelfBottle(new IngredientCard("spirit_a", "Spirit A", IngredientType.Spirit, 5));

            Assert.Greater(keg.Capacity, bottle.Capacity);
            Assert.Greater(keg.PourRate, bottle.PourRate);
            Assert.AreEqual(ShelfBottle.KegCapacity, keg.Capacity, 1e-9);
            Assert.AreEqual(ShelfBottle.BottleCapacity, bottle.Capacity, 1e-9);
        }

        [Test]
        public void AnExplicitCapacityStillWins()
        {
            Assert.AreEqual(3.0, new ShelfBottle(Keg(), capacity: 3.0).Capacity, 1e-9);
        }
    }
}
