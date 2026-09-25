using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE THREE BAD ENDINGS, AND WHICH CURRENCY EACH IS PAID IN (2026-09-22, the author:
    /// "müşteri kovmak hem para cezası hem de puan cezası olmamalı. Eğer siparişini
    /// yetiştiremediysen gün sonu faturasına ceza gelmeli ama puanı daha az düşürmeli. Kovmak
    /// puanı daha çok düşürmeli ama para kaybetmemelisin. Bu sahte kimlikle kovulması gereken
    /// müşteriler için geçerli değil, o hem + puan sağlamalı hem de + para getirmeli.")
    ///
    /// Before this pass a storm-off and a wrong kick were numerically the same event — both filed
    /// a flat zero, both cost nothing — so the game had one punishment and two ways to earn it.
    /// The ORDERING is what these tests hold, in one place, so a future tuner cannot break the
    /// shape by moving one number: a refused customer is worse than a slow drink, a slow drink is
    /// worse than an honest "we cannot make that", and the door done right is a good night's work.
    /// </summary>
    public sealed class EndingsTests
    {
        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 4000),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 4000),
        });

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[]
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

        private static TycoonRun NewRun(string seed, bool door = true)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                regulars: People());
            if (door) run.Rating.DevSet(BarRank.Granting(Feature.Door).Stars);
            return run;
        }

        private static CustomerVisit FirstWaiting(TycoonRun run)
        {
            for (int i = 0; i < 4000 && run.Phase == TycoonPhase.DayOpen; i++)
            {
                var waiting = run.Floor.Seated.FirstOrDefault(
                    v => v.State == VisitState.Waiting && v.HasOrdered
                         && !ReferenceEquals(v, run.LastCustomer));
                if (waiting != null) return waiting;
                run.Tick(0.25);
            }
            return null;
        }

        // ── the ordering, which is what the whole ruling is about ────────────────────────────

        [Test]
        public void TheThreeEndings_AreOrdered_WorstIsTheDoor()
        {
            // A refused adult files nothing at all; a drink that never came files a little; an
            // honest "we cannot make that" files more than either, because saying so is a service.
            Assert.Less(0.0, CustomerVisit.StormOffSatisfaction,
                "a slow drink is not as bad as refusing them outright");
            Assert.Less(CustomerVisit.StormOffSatisfaction, ServiceJudge.Declined().Satisfaction,
                "and an honest no is better than letting them walk");
            Assert.Greater(CustomerVisit.WrongKickWeight, 1.0,
                "a refused customer weighs more than one seat, which is the only way below zero");
            Assert.AreEqual(1.0, CustomerVisit.RightKickSatisfaction, 1e-9,
                "the door done right is a night's work");
        }

        [Test]
        public void AStormOff_StaysUnderTheLineThatKeepsTomorrowsCrowdBroke()
        {
            // The ceiling is not taste. Above BrokeStars/MaxStars a night where nobody was served
            // stops drawing tomorrow's broke crowd, and the loop loses its floor.
            Assert.Less(CustomerVisit.StormOffSatisfaction, BarRating.BrokeStars / BarRating.MaxStars,
                "a night of walk-outs must still empty the room of money");
        }

        [Test]
        public void ANightNobodyWasServed_StillDrawsABrokeCrowd()
        {
            // The guard on the constant above, played rather than asserted: sit people down and
            // serve nobody, and tomorrow's crowd must still be broke.
            var run = NewRun("endings-broke");
            Assert.IsNotNull(FirstWaiting(run), "somebody sat down");
            for (int i = 0; i < 4000 && !run.Floor.IsComplete; i++) run.Tick(0.5);
            Assert.Greater(run.Floor.Finished.Count, 0, "somebody sat down and left");
            Assert.LessOrEqual(run.Floor.AverageSatisfaction,
                BarRating.BrokeStars / BarRating.MaxStars,
                "a night of storm-offs is still a dreadful night");
        }

        // ── the money half ──────────────────────────────────────────────────────────────────

        [Test]
        public void AStormOff_LandsOnTheNightsBill_AndCostsNothingWhileTheBarIsOpen()
        {
            var run = NewRun("endings-bill");
            Assert.IsNotNull(FirstWaiting(run), "somebody sat down");
            int walked = 0;
            for (int i = 0; i < 4000 && run.Phase == TycoonPhase.DayOpen; i++)
            {
                run.Tick(0.5);
                if (run.Phase != TycoonPhase.DayOpen) break;   // the same tick can close the night
                if (run.DayWalkOuts > walked)
                {
                    walked = run.DayWalkOuts;
                    // The author asked for a line on the BILL, not a hand in the till mid-service.
                    Assert.AreEqual(0, run.DayWalkOutFees,
                        "nothing is taken while the bar is still open");
                }
            }
            Assert.Greater(walked, 0, "somebody left without their drink, while the bar was open");
            Assert.Greater(run.DayWalkOuts, 0);
            Assert.Greater(run.DayWalkOutFees, 0, "and it is on the bill at closing");
            // Half of each drink they never got, never under the floor — so the fee is at least
            // the walk-out count times the floor, and at most half of what a night of the dearest
            // pages on the menu would have sold for.
            var cfg = new TycoonConfig(500);
            Assert.GreaterOrEqual(run.DayWalkOutFees, run.DayWalkOuts * cfg.WalkOutFeeFloor,
                "every missed drink costs at least the floor");
            Assert.IsTrue(run.DayExpenses >= run.DayWalkOutFees, "and it is an expense");

            int fees = run.DayWalkOutFees;
            var result = run.ContinueToNextDay();
            Assert.AreEqual(fees, result.WalkOutFees, "the books keep it");
            Assert.AreEqual(result.Rent + result.Stock + result.Upgrades + result.Fines
                + result.WalkOutFees, result.Expenses);
            Assert.AreEqual(0, run.DayWalkOutFees, "tomorrow starts clean");
            Assert.AreEqual(0, run.DayWalkOuts, "tomorrow starts clean");
        }

        [Test]
        public void AWrongKick_CostsNoMoney_AndWeighsTwoSeats()
        {
            var run = NewRun("endings-kick");
            var adult = FirstWaiting(run);
            Assert.IsNotNull(adult, "somebody sat down");
            adult.InspectId();
            int guard = 0;
            while (adult.Papers.ShouldBeKicked)
            {
                Assert.Less(guard++, 200);
                run.Kick(adult);
                run.Tick(0.01);
                adult = FirstWaiting(run);
                Assert.IsNotNull(adult, "an honest adult sat down");
                adult.InspectId();
            }

            int before = run.Money;
            run.Kick(adult);
            run.Tick(0.01);

            Assert.AreEqual(before, run.Money, "showing somebody the door never costs money");
            Assert.AreEqual(0, run.DayWalkOutFees, "and it is not a missed order either");
            Assert.AreEqual(0.0, adult.Satisfaction, 1e-9);
            Assert.AreEqual(CustomerVisit.WrongKickWeight, adult.RatingWeight, 1e-9,
                "but it weighs two seats in the night's mean");
        }

        [Test]
        public void AWrongKick_DragsTheNightFurtherThanAStormOff()
        {
            // The ruling, measured end to end: the same floor, one refused customer against one
            // customer who was simply never served, and the door must be the worse night.
            var kicked = NewRun("endings-compare-a");
            var adult = FirstWaiting(kicked);
            Assert.IsNotNull(adult, "somebody sat down");
            adult.InspectId();
            int guard = 0;
            while (adult.Papers.ShouldBeKicked)
            {
                Assert.Less(guard++, 200);
                kicked.Kick(adult);
                kicked.Tick(0.01);
                adult = FirstWaiting(kicked);
                Assert.IsNotNull(adult, "an honest adult sat down");
                adult.InspectId();
            }
            kicked.Kick(adult);
            kicked.Tick(0.01);

            var stormed = NewRun("endings-compare-b");
            var waiting = FirstWaiting(stormed);
            Assert.IsNotNull(waiting, "somebody sat down");
            while (waiting.State == VisitState.Waiting && stormed.Phase == TycoonPhase.DayOpen)
                stormed.Tick(1.0);

            Assert.Less(kicked.Floor.AverageSatisfaction, stormed.Floor.AverageSatisfaction,
                "refusing a customer hurts the standing more than being too slow for one");
        }

        // ── and the one ending that pays ────────────────────────────────────────────────────

        [Test]
        public void ARightKick_PaysBothWays_AndIsStillNeitherServedNorWalked()
        {
            var run = NewRun("endings-right");
            run.DevJumpToNight(9);   // the minor share climbs with the calendar (IdPapers)
            CustomerVisit minor = null;
            for (int i = 0; i < 200 && minor == null; i++)
            {
                var v = FirstWaiting(run);
                if (v == null) break;
                v.InspectId();
                if (v.Papers.ShouldBeKicked) minor = v;
                else { run.Kick(v); run.Tick(0.01); }
            }
            Assert.IsNotNull(minor, "the crowd rolled somebody who should not be served");

            int before = run.Money;
            run.Kick(minor);
            run.Tick(0.01);

            Assert.AreEqual(before, run.Money, "the thanks is paid at closing, with the rent");
            Assert.AreEqual(CustomerVisit.RightKickSatisfaction, minor.Satisfaction, 1e-9,
                "+ puan: it files a review, and a good one");
            Assert.IsTrue(run.Floor.FinishedCounted().Contains(minor), "which means it is counted");
            Assert.AreEqual(1.0, minor.RatingWeight, 1e-9, "at one seat, like anybody else");
            Assert.Greater(run.Floor.AverageSatisfaction, 0.0);

            for (int i = 0; i < 4000 && run.Phase == TycoonPhase.DayOpen; i++) run.Tick(0.5);
            Assert.Greater(run.DayBonus, 0, "+ para: the state's thanks, at closing");
            int onTheBooks = run.Floor.Finished.Count(v => !v.OnTheHouse);
            int rightKicks = run.RightKicks;
            var result = run.ContinueToNextDay();
            Assert.GreaterOrEqual(result.RightKicks, 1);
            // The half of the old rule that was always about the SLIP and not about the stars.
            Assert.AreEqual(onTheBooks - rightKicks, result.Served + result.WalkedOut,
                "neither served nor walked");
        }

        [Test]
        public void TheThanks_ClimbsWithTheBar_LikeTheFineItAnswers()
        {
            // At a frozen $5 against a fine of $20 a whole star, doing the right thing paid a
            // twentieth of what doing the wrong thing cost by the time the bar was made.
            int youngThanks = StarEconomy.PriceAt(IdPapers.KickBonus, 0.0);
            int madeThanks = StarEconomy.PriceAt(IdPapers.KickBonus, 4.0);
            Assert.Greater(madeThanks, youngThanks, "the state pays what the room is worth");
        }
    }
}
