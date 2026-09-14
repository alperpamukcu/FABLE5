using System;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The pour's own law: how much a tipped bottle or tin gives up at a lean. Since 2026-09-13
    /// (the author: "şişeyi 90 dereceden sonra ne kadar yatırıyorsa o kadar hızlı dolsun, tam 90
    /// derece şişe ise en hızlı şekilde; yere paralelleşiyorsa yavaşlasın, paralelleştiğinde
    /// dursun") nothing runs until the vessel is past level, the flow grows with the lean to its
    /// fullest neck-down, and it slows and stops again as the vessel comes back level. These pin
    /// that shape and the verbs the hand pours through.
    /// </summary>
    public sealed class BottlePourTests
    {
        // ── the law ─────────────────────────────────────────────────────────────

        [Test]
        public void NothingRuns_UntilTheVesselIsPastLevel_AtAnyFill()
        {
            for (double fill = 0.05; fill <= 1.0001; fill += 0.05)
            {
                Assert.AreEqual(0.0, BottlePour.Share(30, fill), 1e-12, $"{fill:P0} full ran at 30 degrees");
                Assert.AreEqual(0.0, BottlePour.Share(BottlePour.OnsetDeg, fill), 1e-12,
                    $"{fill:P0} full ran lying exactly level");
                Assert.Greater(BottlePour.Share(BottlePour.OnsetDeg + 1.0, fill), 0.0,
                    $"{fill:P0} full gave nothing a degree past level");
            }
        }

        [Test]
        public void TheLevelInTheVessel_NoLongerMovesTheLip()
        {
            for (double t = 0; t <= 360; t += 5)
                Assert.AreEqual(BottlePour.Share(t, 1.0), BottlePour.Share(t, 0.15), 1e-12,
                    $"a full and a nearly empty vessel poured differently at {t} degrees");
        }

        [Test]
        public void TippingTowardsStraightDown_NeverPoursLess_AndIsFastestStraightDown()
        {
            double last = 0;
            for (double t = BottlePour.OnsetDeg; t <= BottlePour.FullDeg; t += 0.25)
            {
                double s = BottlePour.Share(t, 0.7);
                Assert.GreaterOrEqual(s, last - 1e-12, $"leaning on to {t:0.00} degrees poured less");
                last = s;
            }
            Assert.AreEqual(1.0, BottlePour.Share(BottlePour.FullDeg, 0.7), 1e-12, "neck down is full flow");
            // A TRICKLE FIRST (2026-09-14): the share is the square of the way from level to neck-down.
            Assert.AreEqual(0.25, BottlePour.Share(135, 0.7), 1e-12, "half way from level to neck down is a quarter of the flow");
            Assert.Less(BottlePour.Share(BottlePour.OnsetDeg + 15.0, 0.7), 0.03, "fifteen degrees past level is still a trickle");
        }

        [Test]
        public void PastStraightDown_TheFlowSlows_AndStopsLyingLevelAgain()
        {
            double last = 1;
            for (double t = BottlePour.FullDeg; t <= 270; t += 0.25)
            {
                double s = BottlePour.Share(t, 0.7);
                Assert.LessOrEqual(s, last + 1e-12, $"coming back towards level at {t:0.00} poured more");
                last = s;
            }
            Assert.AreEqual(0.0, BottlePour.Share(270, 0.7), 1e-12, "lying level the other way it stops");
            for (double d = 0; d <= 90; d += 7.5)
                Assert.AreEqual(BottlePour.Share(180 - d, 0.7), BottlePour.Share(180 + d, 0.7), 1e-12,
                    "the same lean either side of straight down pours the same");
        }

        [Test]
        public void AnEmptyVessel_GivesNothing_HoweverFarItIsTipped()
        {
            Assert.AreEqual(0.0, BottlePour.Share(180, 0.0), 1e-12);
            Assert.AreEqual(0.0, BottlePour.Volume(180, 0.0, 1.0, 1.0), 1e-12);
        }

        /// <summary>
        /// A small share must still be a thing a hand can do. The ratio boxes are twenty points
        /// wide with a five-point floor, so a 5% dash of something poured from a fresh bottle held
        /// just past level has to take the best part of a second — not a single frame.
        /// </summary>
        [Test]
        public void AFivePercentDash_IsASecondOfSteadyHand_JustPastLevel()
        {
            double fullRate = ShelfBottle.BottlePourRate * TycoonConfig.Default.HandPourScale;
            double atTheLip = BottlePour.Volume(BottlePour.OnsetDeg + 3.0, 1.0, fullRate, 1.0);
            Assert.GreaterOrEqual(0.05 / atTheLip, 0.9,
                $"a 5% dash takes only {0.05 / atTheLip:0.00} s at the lip — too fast to steer");
        }

        /// <summary>
        /// Even at full flow one slow frame may not carry a pour through the judge's precision
        /// window: at 30 fps the most a frame can add is half of it.
        /// </summary>
        [Test]
        public void OneFrame_AtFullFlow_StaysInsideHalfThePrecisionWindow()
        {
            double handFull = ShelfBottle.BottlePourRate * TycoonConfig.Default.HandPourScale;
            Assert.LessOrEqual(handFull / 30.0, ServiceJudge.PerfectWindow / 2.0 + 1e-12);
        }

        [Test]
        public void TheLaw_IsPure()
        {
            Assert.AreEqual(0.0, BottlePour.Volume(90, 0.8, 0.5, 0.0), 1e-12, "no time, no pour");
            Assert.AreEqual(0.0, BottlePour.Volume(90, 0.8, 0.5, -1.0), 1e-12);
            double a = BottlePour.Volume(63.7, 0.41, 0.33, 1.0 / 60.0);
            double b = BottlePour.Volume(63.7, 0.41, 0.33, 1.0 / 60.0);
            Assert.AreEqual(a, b, 0.0, "the same lean, level and clock give the same volume, bit for bit");
        }

        /// <summary>The lift's steps (2026-09-14, the author: "1-1-2-3-5-8 gibi artarak"): equal bands of the
        /// lift past level, their flows growing as Fibonacci does, from a trickle to full flow.</summary>
        [Test]
        public void TheLift_StepsUpLikeFibonacci_FromATrickleToFullFlow()
        {
            Assert.AreEqual(0.0, BottlePour.LiftShare(0.0, 1.0), 1e-12, "at the pouring angle nothing runs yet");
            var steps = new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34 };
            Assert.AreEqual(steps.Length, BottlePour.StepCount);
            for (int i = 0; i < steps.Length; i++)
            {
                double mid = (i + 0.5) / steps.Length;
                Assert.AreEqual(i + 1, BottlePour.LiftStep(mid), $"the middle of band {i + 1}");
                Assert.AreEqual(steps[i] / 34.0, BottlePour.LiftShare(mid, 0.7), 1e-12, $"band {i + 1}'s flow");
            }
            Assert.AreEqual(1.0, BottlePour.LiftShare(1.0, 0.7), 1e-12, "the top of the lift is full flow");
            Assert.AreEqual(1.0, BottlePour.LiftShare(3.0, 0.7), 1e-12, "past the top it stays full");
            Assert.AreEqual(0.0, BottlePour.LiftShare(0.5, 0.0), 1e-12, "an empty vessel gives nothing");
        }

        // ── the verbs ───────────────────────────────────────────────────────────

        private static IngredientCard Booze(string id, string category) =>
            new IngredientCard(id, id, IngredientType.Spirit, 6,
                info: new IngredientInfo(style: category, category: category));

        private static TycoonConfig Config => new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0);

        private static TycoonRun BenchRun() => new TycoonRun(new Shelf(new[]
            {
                new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
                new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
            }), RecipeCatalog.CreateDefault(), new RunRng("bottle-pour"), config: Config);

        /// <summary>Two real spirits under a book that names NO build of them — the mandatory
        /// mix follows the recipe the tin matches when there is one (a Built drink needs no mix),
        /// so the two-spirit rule is only in force for a tin the bar cannot name. The full
        /// catalogue names gin with a liqueur as a Built drink; this book (a lone Spritz) does not.</summary>
        private static TycoonRun MixRun() => new TycoonRun(new Shelf(new[]
            {
                new ShelfBottle(Booze("gin_b", "gin"), capacity: 20),
                new ShelfBottle(Booze("vermouth_b", "liqueur"), capacity: 20),
            }), new[] { Spritz() }, new RunRng("bottle-pour-mix"), config: Config);

        private static RecipeDefinition Spritz() => new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5, glassId: "coupe");

        private static GlasswareDefinition Glass(string id, double capacity) =>
            new GlasswareDefinition(id, id, new[] { 1.0, 1.0 }, new[] { 10, 20, 30, 45, 65 }, capacity);

        private static TycoonRun SpritzRun()
        {
            var spritz = Spritz();
            return new TycoonRun(new Shelf(new[]
                {
                    new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
                    new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
                }), new[] { spritz }, new RunRng("bottle-pour-glass"), config: Config,
                glassware: new List<GlasswareDefinition> { Glass("highball", 1.0), Glass("coupe", 0.55) });
        }

        [Test]
        public void ALeanUnderTheLip_PoursNothing_AndLeavesTheShakeAlone()
        {
            var run = MixRun();
            run.PourMeasure("gin_b", 0.4);
            run.PourMeasure("vermouth_b", 0.3);
            run.Shake(1.0);
            run.BeginPour("gin_b");
            double got = run.PourTick(0.5, 80.0);   // nothing runs until the bottle is past level
            Assert.AreEqual(0.0, got, 1e-12, "the bottle ran under its own lip");
            Assert.IsTrue(run.IsShaken, "a pour that poured nothing un-mixed the tin");
            Assert.AreEqual("gin_b", run.PouringId, "a lean under the lip ended the pour");
        }

        [Test]
        public void TippedFurther_TheSameMomentPoursMore()
        {
            var lip = BenchRun();
            lip.BeginPour("gin");
            double atTheLip = lip.PourTick(0.5, BottlePour.OnsetDeg + 3.0);

            var over = BenchRun();
            over.BeginPour("gin");
            double tippedOver = over.PourTick(0.5, 170.0);

            Assert.Greater(atTheLip, 0.0, "nothing ran just past the lip");
            Assert.Greater(tippedOver, 4.0 * atTheLip,
                "a bottle tipped right over should pour many times what a lean at the lip does");
        }

        [Test]
        public void TheTiltedPour_StillStopsAtTheBrim()
        {
            var run = BenchRun();
            run.BeginPour("gin");
            for (int i = 0; i < 600; i++) run.PourTick(1.0 / 60.0, 180.0);
            Assert.AreEqual(1.0, run.Glass.FillFraction, 1e-9, "the tin runs past its brim");
            run.BeginPour("gin");
            Assert.AreEqual(0.0, run.PourTick(0.5, 180.0), 1e-12, "a full tin took more");
        }

        [Test]
        public void ByTheLift_TheFirstStepIsAPercentASecond_AndTheTopIsThirtyFourOfIt()
        {
            var low = BenchRun();
            low.BeginPour("gin");
            double trickle = low.PourTickLift(1.0, 0.05);
            var high = BenchRun();
            high.BeginPour("gin");
            double full = high.PourTickLift(1.0, 1.0);
            Assert.That(trickle, Is.InRange(0.008, 0.012), "the first step should be about a percent of the tin a second");
            Assert.AreEqual(34.0 * trickle, full, 1e-9, "the top step is thirty-four of the first");
            var none = BenchRun();
            none.BeginPour("gin");
            Assert.AreEqual(0.0, none.PourTickLift(1.0, 0.0), 1e-12, "at the pouring angle nothing runs");
            Assert.AreEqual("gin", none.PouringId, "a lift that runs nothing ended the pour");
        }

        [Test]
        public void ByTheLift_AFullTin_FillsTheGlass_AndEmpties()
        {
            var run = BenchRun();
            run.PourMeasure("gin", 0.4);
            run.PourMeasure("soda", 0.6);
            for (int i = 0; i < 60 * 6 && !run.Glass.IsEmpty; i++) run.PourOutLift(1.0 / 60.0, 1.0);
            Assert.IsTrue(run.Glass.IsEmpty, "a full tin lifted to the top for six seconds did not run dry");
            Assert.AreEqual(1.0, run.ServingGlass.FillFraction, 1e-9, "one tin is one portion");
        }

        [Test]
        public void TheHandPour_ReachesForTheDrinksGlass_BeforeTheServe()
        {
            var run = SpritzRun();
            Assert.AreEqual("highball", run.ServingGlassware.Id, "an empty counter holds the default");
            run.BeginPour("gin");
            run.PourTick(1.0, 180.0);   // neck down: a full second of full flow each
            run.EndPour();
            run.BeginPour("soda");
            run.PourTick(1.0, 180.0);
            run.EndPour();
            Assert.IsTrue(run.ServingGlass.IsEmpty, "nothing has been poured out of the tin yet");
            Assert.AreEqual("coupe", run.ServingGlassware.Id,
                "the tin names a Spritz, so the coupe should already be standing — as the measured pour does");
        }

        [Test]
        public void TheTinTippedOut_RefusesAnUnmixedTwoSpiritBuild()
        {
            var run = MixRun();
            run.PourMeasure("gin_b", 0.4);
            run.PourMeasure("vermouth_b", 0.3);
            Assert.IsTrue(run.MixRequired, "the setup: an unnamed two-spirit tin must want a mix");
            Assert.Throws<InvalidOperationException>(() => run.PourOutTilted(0.1, 100.0),
                "two spirits may not leave the tin unmixed, however far it is tipped");
            Assert.AreEqual(0.0, run.PourOutTilted(0.1, 60.0), 1e-12,
                "and a tin not yet past level is not pouring, so there is nothing to refuse");
        }

        [Test]
        public void AFullTinTippedOut_FillsTheGlass_AndEmpties()
        {
            var run = BenchRun();
            run.PourMeasure("gin", 0.4);
            run.PourMeasure("soda", 0.6);
            for (int i = 0; i < 60 * 6 && !run.Glass.IsEmpty; i++) run.PourOutTilted(1.0 / 60.0, 180.0);
            Assert.IsTrue(run.Glass.IsEmpty, "a full tin held neck-down for six seconds did not run dry");
            Assert.AreEqual(1.0, run.ServingGlass.FillFraction, 1e-9, "one tin is one portion");
        }
    }
}
