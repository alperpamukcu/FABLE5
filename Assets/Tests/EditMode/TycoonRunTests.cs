using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The P2 gate (PLAN_tycoon_pivot): a full seeded day plays headless through
    /// <see cref="TycoonRun"/> — arrivals, pours, serves, the invoice, the strike logic —
    /// with nothing but Core.
    /// </summary>
    public class TycoonRunTests
    {
        private static RecipeDefinition Spritz() => new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5);

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[] { Spritz() };

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
        });

        private static TycoonRun NewRun(string seed = "day-one") =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed));

        /// <summary>Serves every seated customer an exact Spritz until the day closes.</summary>
        private static void PlayDayServingEveryone(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 600, "the day must terminate");
                run.Tick(5);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    if (visit.State != VisitState.Waiting) continue;
                    run.PourMeasure("gin", 0.35);
                    run.PourMeasure("soda", 0.35);
                    run.ServeTo(visit);
                }
            }
        }

        [Test]
        public void AFullDay_PlaysHeadless_AndPaysTheBills()
        {
            var run = NewRun();

            PlayDayServingEveryone(run);

            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            Assert.AreEqual(8, run.Floor.Finished.Count, "day 1 sends 8 customers");
            // 8 exact serves at $6 (rank 2 → $4+$2) + $1 speed tip each, minus $20 rent.
            Assert.AreEqual(20 + 8 * 7 - 20, run.Money);

            var result = run.ContinueToNextDay();

            Assert.AreEqual(8 * 7, result.Income);
            Assert.AreEqual(20, result.Expenses, "rent is the only expense today");
            Assert.AreEqual(0, run.Ledger.DebtStrikes, "a green day");
            Assert.AreEqual(2, run.Day);
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);
        }

        [Test]
        public void AGoodDay_DrawsAWealthierCrowd_WhoPayMore()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);
            run.ContinueToNextDay();

            Assert.AreEqual(WealthTier.HighRoller, run.CrowdToday,
                "everyone served fast and exact — word gets around");

            int guard = 0;
            while (run.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 100);
                run.Tick(5);
            }

            Assert.AreEqual(8, run.Floor.Seated[0].Order.Price,
                "the $6 spritz sells for $8 to high rollers (×1.25)");
        }

        [Test]
        public void ServingNobody_UntilTheTillRunsDry_ClosesTheBar()
        {
            // $20 starting money vs $20 day-1 rent: the till goes under on day 2 with no
            // income, and three underwater closes later the doors are shut.
            var run = NewRun("bankrupt");

            for (int day = 0; day < 4; day++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 500, "storm-offs must clear the floor");
                    run.Tick(50);
                }
                run.ContinueToNextDay();
                if (run.Phase == TycoonPhase.Closed) break;
            }

            Assert.AreEqual(TycoonPhase.Closed, run.Phase);
            Assert.IsTrue(run.Ledger.IsBankrupt);
            Assert.AreEqual(3, run.Ledger.DebtStrikes);
        }

        [Test]
        public void TheWrongDrink_PaysHalf_AndSoursTheRoom()
        {
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 100);
                run.Tick(5);
            }
            var visit = run.Floor.Seated[0];

            run.PourMeasure("soda", 0.7);   // pure soda against a spritz order
            var verdict = run.ServeTo(visit);

            Assert.AreEqual(OrderMatch.Wrong, verdict.Match);
            Assert.AreEqual(3, verdict.BasePaid, "half of the $6 spritz");
            Assert.AreEqual(0, verdict.Tip);
            Assert.LessOrEqual(verdict.Satisfaction, 0.2);
        }

        [Test]
        public void Refills_LandOnTheInvoice()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);

            // 8 drinks × 0.7 volume from two bottles = 5.6 capacity → $17 at $3/capacity.
            int cost = run.RefillShelf();
            Assert.AreEqual(17, cost);

            var result = run.ContinueToNextDay();
            Assert.AreEqual(20 + 17, result.Expenses, "rent + the refill");
        }

        [Test]
        public void Shaking_RecordsThePreparation()
        {
            var run = NewRun();
            run.PourMeasure("gin", 0.4);

            run.Shake();

            Assert.IsTrue(run.IsShaken);
            Assert.IsTrue(run.Glass.HasPreparation("shaken"));
        }

        [Test]
        public void ASloppyServePour_CostsTheRecipe()
        {
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];   // day 1 orders a Spritz

            // An exact spritz built in the shaker, then poured badly: 60% misses the rim,
            // so the serving glass lands under the recipe's MinFill and the drink no longer
            // reads as a Spritz. The aim is the skill; spilling has a price.
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(0.7, accuracy: 0.4);

            Assert.Less(run.ServingGlass.FillFraction, 0.5, "a spilled pour under-fills the glass");
            Assert.IsTrue(run.Glass.IsEmpty, "the shaker is spent either way");

            var verdict = run.ServeTo(visit);
            Assert.AreNotEqual(OrderMatch.Exact, verdict.Match, "the spill lost the recipe");
        }

        [Test]
        public void TheDayClock_AndThePourClock_AreIndependent()
        {
            // Holding a bottle while the floor runs must not double-charge time anywhere:
            // the floor ticks patience, the pour ticks volume, and serving one seat leaves
            // the others waiting untouched.
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count < 2)
            {
                Assert.Less(guard++, 300);
                run.Tick(5);
            }

            var first = run.Floor.Seated[0];
            var second = run.Floor.Seated[1];
            double secondPatienceBefore = second.PatienceLeft;

            run.BeginPour("gin");
            run.PourTick(1.0);
            run.EndPour();
            run.PourMeasure("soda", run.Glass.VolumeOf("gin"));
            run.ServeTo(first);

            Assert.AreEqual(VisitState.Waiting, second.State);
            Assert.AreEqual(secondPatienceBefore, second.PatienceLeft, 1e-9,
                "pouring costs the pourer time, not the waiter patience — only Tick does that");
        }
    }
}
