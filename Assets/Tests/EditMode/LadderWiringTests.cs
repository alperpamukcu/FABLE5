using System;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE LADDER WIRED INTO THE RUN (PLAN_rank_ladder L0, 2026-09-21): what a rung opens is refused below it by
    /// Core itself, through the same verbs the player and the sim bot use. BarRankTests holds the table; this holds
    /// the run to it.
    /// </summary>
    public sealed class LadderWiringTests
    {
        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 4000),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 4000),
        });

        private static readonly System.Collections.Generic.IReadOnlyList<RecipeDefinition> Book = new[]
        {
            new RecipeDefinition("spritz", "Spritz", rank: 2, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5),
        };

        private static RegularsRegistry People() => new RegularsRegistry(new[]
        {
            new ArchetypeDefinition("after_shift", "Off the Late Shift",
                new[] { "Marguerite", "Dev", "Ola", "Kit", "Rasmus", "Nuray" }, 1,
                new[] { "this side of town" }),
        });

        private static TycoonRun NewRun(double stars, string seed = "ladder", bool people = false)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                regulars: people ? People() : null);
            run.Rating.DevSet(stars);
            return run;
        }

        /// <summary>Every order the night rolls, read off the card as the player would.</summary>
        private static System.Collections.Generic.List<DrinkOrder> OrdersOfTheNight(TycoonRun run, int atMost = 40)
        {
            var seen = new System.Collections.Generic.List<DrinkOrder>();
            var read = new System.Collections.Generic.HashSet<CustomerVisit>();
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen && seen.Count < atMost)
            {
                Assert.Less(guard++, 4000, "the night must end");
                run.Tick(5);
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (v.State != VisitState.Waiting || !v.HasOrdered || read.Contains(v)) continue;
                    v.InspectId();
                    read.Add(v);
                    seen.Add(v.Order);
                    run.DeclineOrder(v);   // nothing is poured: only what was ASKED for is under test
                }
            }
            return seen;
        }

        // ── the rail ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AtTheFoot_NobodyAsksForAnythingOffTheRail_AndTheGlassTakesNone()
        {
            var run = NewRun(0.0);
            Assert.AreEqual(0, run.Rank.Index);
            Assert.IsEmpty(run.PreparationsOpen, "nothing on the rail yet");
            var orders = OrdersOfTheNight(run);
            Assert.IsNotEmpty(orders, "a night with orders in it");
            Assert.IsTrue(orders.All(o => o.Spec.IsPlain), "no ice, no twist, no rim can be asked for below the first rung");

            var fresh = NewRun(0.0, "ladder-glass");
            Assert.IsFalse(fresh.PreparationOpen(Preparations.Ice));
            Assert.Throws<InvalidOperationException>(() => fresh.AddPreparationAtGlass(Preparations.Ice),
                "the rules layer refuses what the room would not have shown");
            Assert.Throws<InvalidOperationException>(() => fresh.AddPreparation(Preparations.Ice),
                "and the shaker's twin refuses the same");
        }

        [Test]
        public void AtHalfAStar_IceAndTheTwistOpen_TheRimsDoNot()
        {
            var run = NewRun(0.5, "ladder-half");
            Assert.AreEqual(1, run.Rank.Index);
            CollectionAssert.AreEqual(new[] { "ice", "lemon_twist" }, run.PreparationsOpen.Select(p => p.Id).ToList());
            var asked = OrdersOfTheNight(run, 60)
                .SelectMany(o => o.Spec.Garnishes).Select(g => g.Id).Distinct().ToList();
            Assert.IsTrue(asked.All(id => id == "ice" || id == "lemon_twist"),
                "the customers ask only for what the rail carries: " + string.Join(", ", asked));

            var glass = NewRun(0.5, "ladder-half-glass");
            glass.AddPreparationAtGlass(Preparations.Ice);
            glass.AddPreparationAtGlass(Preparations.LemonTwist);
            Assert.Throws<InvalidOperationException>(() => glass.AddPreparationAtGlass(Preparations.SaltRim),
                "no salt before the second rung");
        }

        [Test]
        public void AtOneStar_TheWholeRailIsOut()
        {
            var run = NewRun(1.0, "ladder-one");
            Assert.AreEqual(2, run.Rank.Index);
            Assert.AreEqual(4, run.PreparationsOpen.Count);
            run.AddPreparationAtGlass(Preparations.SaltRim);
            run.AddPreparationAtGlass(Preparations.SugarRim);
            Assert.IsTrue(run.ServingGlass.HasPreparation("salt_rim"));
        }

        // ── the door ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BelowTheSecondRung_NobodyIsAMinor_AndTheKickRefuses()
        {
            var run = NewRun(0.5, "ladder-door-shut", people: true);
            Assert.IsFalse(run.Has(Feature.Door));
            int met = 0;
            for (int night = 0; night < 12 && run.Phase == TycoonPhase.DayOpen; night++)
            {
                int guard = 0;
                CustomerVisit someone = null;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 4000, "the night must end");
                    run.Tick(5);
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered || v.IdInspected) continue;
                        v.InspectId();
                        met++;
                        Assert.IsTrue(v.Papers == null || !v.Papers.ShouldBeKicked,
                            "before the door is the bar's, everyone who walks in is honest");
                        if (someone == null)
                        {
                            someone = v;
                            // The verb itself refuses — not "read the card first", the door is simply not yours.
                            var ex = Assert.Throws<InvalidOperationException>(() => run.Kick(v));
                            StringAssert.Contains("not yours", ex.Message);
                        }
                        run.DeclineOrder(v);
                    }
                }
                Assert.AreEqual(0, run.MinorsMet, "the papers stream rolled nobody underage");
                if (run.Phase == TycoonPhase.DayEnd) run.ContinueToNextDay();
            }
            Assert.Greater(met, 20, "enough faces read to mean something");
        }

        [Test]
        public void OnTheSecondRung_TheDoorIsYours()
        {
            var run = NewRun(1.0, "ladder-door-open", people: true);
            Assert.IsTrue(run.Has(Feature.Door));
            // The guards that were always there still stand in their order: read the card first.
            int guard = 0;
            CustomerVisit v = null;
            while (v == null)
            {
                Assert.Less(guard++, 4000, "somebody must walk in");
                run.Tick(5);
                v = run.Floor.Seated.FirstOrDefault(x => x.State == VisitState.Waiting && x.HasOrdered);
            }
            var ex = Assert.Throws<InvalidOperationException>(() => run.Kick(v));
            StringAssert.Contains("Read the card", ex.Message);
        }

        // ── the rank itself ───────────────────────────────────────────────────────────────────────

        [Test]
        public void TheRank_IsAHighWaterMark_ABadNightDoesNotTakeTheSpoonBack()
        {
            var run = NewRun(2.0, "ladder-water");
            Assert.AreEqual(3, run.Rank.Index);
            Assert.IsTrue(run.SpoonUnlocked);
            // An empty night files zero and drags the standing down a step; the rank stays where it was.
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 4000); run.Tick(30); }
            run.ContinueToNextDay();
            Assert.Less(run.Rating.Average, 2.0, "the standing fell");
            Assert.AreEqual(2.0, run.Rating.BestStanding, 1e-9, "the mark did not");
            Assert.AreEqual(3, run.Rank.Index);
            Assert.IsTrue(run.SpoonUnlocked, "what a rung opened stays open");
        }

        [Test]
        public void RankAfterTonight_PreviewsTheClimb_WithoutFilingIt()
        {
            var run = NewRun(0.0, "ladder-preview");
            Assert.AreEqual(0, run.RankAfterTonight.Index, "by day, the preview is the rank");
            // A night that never opened its doors files nothing: the preview cannot promise a rung the books
            // will refuse. (A climbing night is played out in the HUD's own tests; here the contract is that
            // the two read the same number.)
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 4000); run.Tick(30); }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            var preview = run.RankAfterTonight;
            run.ContinueToNextDay();
            Assert.AreEqual(preview.Index, run.Rank.Index, "the bill's preview and the close agree");
        }

        [Test]
        public void DevPreset_MovesTheMarkWithTheStanding()
        {
            var run = NewRun(0.0, "ladder-preset");
            run.Rating.DevSet(2.6);
            Assert.AreEqual(3, run.Rank.Index, "a parked mid bar stands on the third rung");
            run.Rating.DevSet(1.0);
            Assert.AreEqual(3, run.Rank.Index, "parking it lower does not lower the mark");
        }
    }
}
