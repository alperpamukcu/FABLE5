using System;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The pour's own law (2026-09-11): how much a tipped bottle or tin gives up at a lean.
    /// GDD 24 §2 wrote "more tilt = faster pour" in 2026-07 and nothing built it until
    /// <see cref="BottlePour"/>; the rate lived in the UI as a flat constant from 42 degrees.
    /// These pin the shape — an onset that follows the level, a trickle at the lip, full flow
    /// tipped over — and the two verbs the hand now pours through.
    /// </summary>
    public sealed class BottlePourTests
    {
        // ── the law ─────────────────────────────────────────────────────────────

        [Test]
        public void NothingRunsUnderTheLip_AtAnyLevel()
        {
            for (double fill = 0.05; fill <= 1.0001; fill += 0.05)
            {
                double onset = BottlePour.OnsetDeg(fill);
                Assert.AreEqual(0.0, BottlePour.Share(onset - 0.5, fill), 1e-12,
                    $"a vessel {fill:P0} full ran half a degree under its own lip");
                Assert.Greater(BottlePour.Share(onset + 0.5, fill), 0.0,
                    $"a vessel {fill:P0} full gave nothing half a degree past its lip");
            }
        }

        [Test]
        public void AFullerVessel_TipsEarlier()
        {
            double last = double.MaxValue;
            for (double fill = 0.02; fill <= 1.0001; fill += 0.02)
            {
                double onset = BottlePour.OnsetDeg(fill);
                Assert.Less(onset, last, $"the lip did not come down as the level rose, at {fill:P0}");
                last = onset;
            }
            Assert.AreEqual(BottlePour.OnsetFullDeg, BottlePour.OnsetDeg(1.0), 1e-9);
            Assert.AreEqual(BottlePour.OnsetEmptyDeg, BottlePour.OnsetDeg(0.0), 1e-9);
        }

        [Test]
        public void TippingFurther_NeverPoursLess_AndFullFlowIsReached()
        {
            foreach (double fill in new[] { 1.0, 0.6, 0.25 })
            {
                double onset = BottlePour.OnsetDeg(fill), last = 0;
                for (double t = onset; t <= onset + BottlePour.RampDeg + 20; t += 0.25)
                {
                    double s = BottlePour.Share(t, fill);
                    Assert.GreaterOrEqual(s, last - 1e-12, $"leaning to {t:0.00} deg poured less at {fill:P0}");
                    last = s;
                }
                Assert.AreEqual(1.0, BottlePour.Share(onset + BottlePour.RampDeg, fill), 1e-9,
                    "full flow is reached a ramp past the lip");
            }
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
        /// just past its lip has to take the best part of a second — not a single frame.
        /// </summary>
        [Test]
        public void AFivePercentDash_IsASecondOfSteadyHand_AtTheLip()
        {
            double fullRate = ShelfBottle.BottlePourRate * TycoonConfig.Default.HandPourScale;
            double atTheLip = BottlePour.Volume(BottlePour.OnsetDeg(1.0) + 3.0, 1.0, fullRate, 1.0);
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
            double got = run.PourTick(0.5, 10.0);   // a fresh bottle's lip is 24 degrees
            Assert.AreEqual(0.0, got, 1e-12, "the bottle ran under its own lip");
            Assert.IsTrue(run.IsShaken, "a pour that poured nothing un-mixed the tin");
            Assert.AreEqual("gin_b", run.PouringId, "a lean under the lip ended the pour");
        }

        [Test]
        public void TippedFurther_TheSameMomentPoursMore()
        {
            var lip = BenchRun();
            lip.BeginPour("gin");
            double atTheLip = lip.PourTick(0.5, BottlePour.OnsetFullDeg + 3.0);

            var over = BenchRun();
            over.BeginPour("gin");
            double tippedOver = over.PourTick(0.5, 100.0);

            Assert.Greater(atTheLip, 0.0, "nothing ran just past the lip");
            Assert.Greater(tippedOver, 4.0 * atTheLip,
                "a bottle tipped right over should pour many times what a lean at the lip does");
        }

        [Test]
        public void TheTiltedPour_StillStopsAtTheBrim()
        {
            var run = BenchRun();
            run.BeginPour("gin");
            for (int i = 0; i < 600; i++) run.PourTick(1.0 / 60.0, 110.0);
            Assert.AreEqual(1.0, run.Glass.FillFraction, 1e-9, "the tin runs past its brim");
            run.BeginPour("gin");
            Assert.AreEqual(0.0, run.PourTick(0.5, 110.0), 1e-12, "a full tin took more");
        }

        [Test]
        public void TheHandPour_ReachesForTheDrinksGlass_BeforeTheServe()
        {
            var run = SpritzRun();
            Assert.AreEqual("highball", run.ServingGlassware.Id, "an empty counter holds the default");
            run.BeginPour("gin");
            run.PourTick(1.0, 60.0);
            run.EndPour();
            run.BeginPour("soda");
            run.PourTick(1.0, 60.0);
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
            Assert.AreEqual(0.0, run.PourOutTilted(0.1, 10.0), 1e-12,
                "and a tin held under its lip is not pouring, so there is nothing to refuse");
        }

        [Test]
        public void AFullTinTippedOut_FillsTheGlass_AndEmpties()
        {
            var run = BenchRun();
            run.PourMeasure("gin", 0.4);
            run.PourMeasure("soda", 0.6);
            for (int i = 0; i < 60 * 6 && !run.Glass.IsEmpty; i++) run.PourOutTilted(1.0 / 60.0, 118.0);
            Assert.IsTrue(run.Glass.IsEmpty, "a full tin tipped right over for six seconds did not run dry");
            Assert.AreEqual(1.0, run.ServingGlass.FillFraction, 1e-9, "one tin is one portion");
        }
    }
}
