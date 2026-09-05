using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE DOOR, WIRED (GDD 28, PLAN_house_and_law H2b, 2026-09-05). DoorTests pin the papers;
    /// these pin what the RUN does with them — the kick, the fine when they get up, the thanks
    /// at close — through the same verbs the bot and the player use.
    /// </summary>
    public sealed class DoorWiringTests
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

        private static TycoonRun NewRun(string seed = "door", bool people = true) =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                regulars: people ? People() : null);

        private static ServiceVerdict Serve(TycoonRun run, CustomerVisit v)
        {
            if (!run.CanMake(v.Order)) { run.DeclineOrder(v); return null; }
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            return run.ServeTo(v);
        }

        /// <summary>The first customer with their mind made up, card unread.</summary>
        private static CustomerVisit FirstWaiting(TycoonRun run)
        {
            int guard = 0;
            while (true)
            {
                Assert.Less(guard++, 500, "somebody must walk in");
                var v = run.Floor.Seated.FirstOrDefault(x => x.State == VisitState.Waiting && x.HasOrdered);
                if (v != null) return v;
                run.Tick(5);
            }
        }

        /// <summary>Plays the night out: every card read, minors shown the door when asked to,
        /// everyone else served or honestly declined.</summary>
        private static void FinishTheNight(TycoonRun run, bool kickMinors)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 3000, "the night must end");
                run.Tick(5);
                TestNight.Clean(run);
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                    v.InspectId();
                    if (kickMinors && v.Papers != null && v.Papers.ShouldBeKicked) run.Kick(v);
                    else Serve(run, v);
                }
            }
        }

        /// <summary>Plays nights from the second on until a card that should be kicked is on
        /// the bar, and hands that visit back with its card read.</summary>
        private static CustomerVisit FindAMinor(TycoonRun run, int maxNights = 60)
        {
            if (run.Day < 2) run.DevJumpToNight(2);
            for (int night = 0; night < maxNights; night++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 3000, "the night must end");
                    run.Tick(5);
                    TestNight.Clean(run);
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                        v.InspectId();
                        if (v.Papers != null && v.Papers.ShouldBeKicked) return v;
                        Serve(run, v);
                    }
                }
                Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "the bar must still be open for business");
                run.ContinueToNextDay();
            }
            Assert.Fail("no minor came in " + maxNights + " nights");
            return null;
        }

        [Test]
        public void ThePapers_ThrowUntilTheCardIsRead_AndSoDoesTheKick()
        {
            var run = NewRun();
            run.DevJumpToNight(2);
            var v = FirstWaiting(run);
            Assert.Throws<InvalidOperationException>(() => { var _ = v.Papers; }, "hidden until read");
            Assert.Throws<InvalidOperationException>(() => run.Kick(v), "no blind kick");
            v.InspectId();
            Assert.DoesNotThrow(() => { var _ = v.Papers; });
            Assert.IsNotNull(v.Papers, "everyone carries papers");
        }

        [Test]
        public void AKickAfterAServedRound_Throws()
        {
            var run = NewRun();
            run.DevJumpToNight(2);
            var v = FirstWaiting(run);
            v.InspectId();
            Serve(run, v);
            Assert.Greater(v.Paid, 0);
            Assert.Throws<InvalidOperationException>(() => run.Kick(v), "the card was your moment");
        }

        [Test]
        public void ARightKick_IsOffTheBooks_LeavesNothing_AndIsThankedAtClose()
        {
            var run = NewRun("door-right");
            var minor = FindAMinor(run);
            int messes = run.Floor.Messes.Count;
            var person = minor.Regular;

            run.Kick(minor);

            Assert.AreEqual(VisitState.Kicked, minor.State);
            Assert.IsTrue(minor.OffTheBooks);
            Assert.IsTrue(person.Barred, "the bounced do not come back");
            Assert.AreEqual(1, run.RightKicks);
            Assert.AreEqual(0, run.WrongKicks);
            run.Tick(0.01);
            Assert.IsFalse(run.Floor.Seated.Contains(minor), "the stool is free at once");
            Assert.AreEqual(messes, run.Floor.Messes.Count, "a kicked visit leaves nothing on the counter");
            Assert.IsFalse(run.Floor.FinishedCounted().Contains(minor), "off the books");
            Assert.IsFalse(minor.DrinkServed);

            FinishTheNight(run, kickMinors: true);
            int rightKicks = run.RightKicks;
            Assert.AreEqual(IdPapers.KickBonus * rightKicks, run.DayBonus, "a well drink per face");
            Assert.AreEqual(run.DaySales + run.DayTips + run.DayBonus, run.DayIncome);
            int counted = run.Floor.FinishedCounted().Count;
            var result = run.ContinueToNextDay();
            Assert.AreEqual(rightKicks, result.RightKicks);
            Assert.AreEqual(IdPapers.KickBonus * rightKicks, result.Bonus);
            Assert.AreEqual(result.Sales + result.Tips + result.Bonus, result.Income);
            Assert.AreEqual(counted, result.Served + result.WalkedOut, "neither served nor walked");
            Assert.AreEqual(0, result.Fines);
            Assert.AreEqual(0, run.DayBonus, "tomorrow starts clean");
        }

        [Test]
        public void AWrongKick_IsAWalkOut_OnTheBooks_AndEarnsNothing()
        {
            var run = NewRun("door-wrong");
            run.DevJumpToNight(2);
            CustomerVisit adult = null;
            int guard = 0;
            while (adult == null)
            {
                Assert.Less(guard++, 200);
                var v = FirstWaiting(run);
                v.InspectId();
                if (!v.Papers.ShouldBeKicked) adult = v; else run.Kick(v);
            }
            var person = adult.Regular;

            run.Kick(adult);

            Assert.AreEqual(VisitState.Kicked, adult.State);
            Assert.IsFalse(adult.OffTheBooks);
            Assert.AreEqual(0.0, adult.Satisfaction, 1e-9);
            Assert.AreEqual(1, run.WrongKicks);
            Assert.IsFalse(person.Barred);
            Assert.AreEqual(1, person.Visits, "the regular remembers a zero");
            run.Tick(0.01);
            Assert.IsTrue(run.Floor.FinishedCounted().Contains(adult), "on the books, as a walk-out");

            FinishTheNight(run, kickMinors: true);
            int walked = run.Floor.FinishedCounted()
                .Count(v => v.State == VisitState.StormedOff || v.State == VisitState.Kicked);
            var result = run.ContinueToNextDay();
            Assert.AreEqual(walked, result.WalkedOut);
            Assert.AreEqual(1, result.WrongKicks);
            // Whatever RIGHT kicks the rest of the night earned, the wrong one earned nothing.
            Assert.AreEqual(IdPapers.KickBonus * result.RightKicks, result.Bonus,
                "no thanks for refusing an adult");
        }

        [Test]
        public void AServedMinor_PaysFirst_ThenIsFined_WhenTheyGetUp()
        {
            var run = NewRun("door-fine");
            var minor = FindAMinor(run);
            int fine = IdPapers.FineFor(run.Rating.Average);
            Assert.Greater(fine, 0);

            var verdict = Serve(run, minor);

            Assert.IsNotNull(verdict, "the bar can make a spritz");
            Assert.IsFalse(verdict.OrdersAgain, "a minor gets no extra round");
            Assert.AreEqual(fine, minor.FineOwed);
            Assert.IsFalse(minor.Fined, "not until they get up");
            Assert.AreEqual(1, run.MinorsServed);
            Assert.AreEqual(0, run.DayFines, "nothing taken at the serve");

            int moneyBefore = run.Money, salesBefore = run.DaySales, tipsBefore = run.DayTips;
            run.Tick(0.01);   // savour is zero: they get up, the tab settles, the law follows

            Assert.IsTrue(minor.Fined);
            Assert.AreEqual(fine, run.DayFines);
            int taken = run.DaySales + run.DayTips - salesBefore - tipsBefore;
            Assert.AreEqual(moneyBefore + taken - fine, run.Money, "paid first, then fined");
            Assert.AreEqual(run.DayRent + run.DayStock + run.DayUpgrades + run.DayFines, run.DayExpenses);

            FinishTheNight(run, kickMinors: true);
            var result = run.ContinueToNextDay();
            Assert.AreEqual(fine, result.Fines);
            Assert.AreEqual(1, result.MinorsServed);
            Assert.AreEqual(result.Rent + result.Stock + result.Upgrades + result.Fines, result.Expenses);
            Assert.AreEqual(0, run.DayFines, "tomorrow starts clean");
        }

        [Test]
        public void ABarredFace_DoesNotComeBack()
        {
            var run = NewRun("door-barred");
            var minor = FindAMinor(run);
            var person = minor.Regular;
            run.Kick(minor);
            Assert.IsTrue(person.Barred);
            // Tonight's own record still holds the kicked visit; it is the nights AFTER that
            // must never seat this person again.
            FinishTheNight(run, kickMinors: true);
            run.ContinueToNextDay();
            for (int night = 0; night < 12; night++)
            {
                FinishTheNight(run, kickMinors: true);
                foreach (var v in run.Floor.Finished)
                    Assert.AreNotSame(person, v.Regular, "the bounced do not come back");
                if (run.Phase != TycoonPhase.DayEnd) break;
                run.ContinueToNextDay();
            }
        }

        [Test]
        public void ARunWithoutPeople_HasNoPapers_AndMeetsNoMinor()
        {
            var run = NewRun("door-nobody", people: false);
            run.DevJumpToNight(9);
            for (int night = 0; night < 3; night++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 3000);
                    run.Tick(5);
                    TestNight.Clean(run);
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                        v.InspectId();
                        Assert.IsNull(v.Papers, "nobody has papers without a person behind them");
                        Serve(run, v);
                    }
                }
                Assert.AreEqual(0, run.MinorsMet);
                var result = run.ContinueToNextDay();
                Assert.AreEqual(0, result.MinorsMet);
                Assert.AreEqual(0, result.Fines);
            }
        }

        [Test]
        public void TheSameSeed_MeetsTheSameMinors()
        {
            var a = Play("door-seed", 8);
            var b = Play("door-seed", 8);
            CollectionAssert.AreEqual(a, b);
            Assert.Greater(a.Sum(), 0, "eight nights from the second meet at least one");
        }

        private static List<int> Play(string seed, int nights)
        {
            var run = NewRun(seed);
            run.DevJumpToNight(2);
            var met = new List<int>();
            for (int night = 0; night < nights; night++)
            {
                FinishTheNight(run, kickMinors: true);
                met.Add(run.MinorsMet);
                if (run.Phase != TycoonPhase.DayEnd) break;
                run.ContinueToNextDay();
            }
            return met;
        }
    }
}
