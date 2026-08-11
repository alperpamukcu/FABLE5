using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The tycoon heart (GDD 23, PLAN_tycoon_pivot P1): orders and menu prices, patience
    /// and storm-offs, the service verdict's money, the extra-order rule, the three-red-days
    /// bankruptcy, and the bar floor's seat row. Every number here mirrors a GDD 23 table.
    /// </summary>
    public class TycoonCoreTests
    {
        // ── scaffolding ─────────────────────────────────────────────────────────

        private static RecipeDefinition BandRecipe(string id, int rank,
            params RatioRequirement[] bands) =>
            new RecipeDefinition(id, id, rank, baseFlavor: 10, baseMult: 2,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                // BUILT, since the method rule (2026-08-11): a gin-and-soda spritz is a
                // built drink, and Built recipes are the "either, or neither" mix class —
                // these suites test the economy, not the spoon.
                ratioRequirements: bands, minFill: 0.5, prep: PrepMethod.Built);

        private static RecipeDefinition Spritz(int rank = 2) => BandRecipe("spritz", rank,
            new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
            new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7));

        private static readonly Dictionary<string, IngredientCard> Bar =
            new Dictionary<string, IngredientCard>
            {
                ["gin"] = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6),
                ["soda"] = new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1),
                ["lemon"] = new IngredientCard("lemon", "Lemon", IngredientType.Sour, 3),
            };

        private static IngredientCard Look(string id) => Bar.TryGetValue(id, out var c) ? c : null;

        private static GlassContents Glass(params (string id, double volume)[] pours)
        {
            var glass = new GlassContents(1.0);
            foreach (var (id, volume) in pours) glass.Add(id, volume);
            return glass;
        }

        private static CustomerVisit Visit(int price = 10, double patience = 60) =>
            new CustomerVisit(new DrinkOrder(Spritz(), price), patience);

        // ── orders & menu (GDD 23 §3) ───────────────────────────────────────────

        [Test]
        public void MenuPrice_IsDeliberatelyLow_SoTheTipIsTheEarner()
        {
            // v5 P11: was 4 + rank. Halving the ladder is the point -- a correct-but-careless
            // serve used to earn nearly as much as a perfect one.
            Assert.AreEqual(7, DrinkOrder.MenuPrice(Spritz(rank: 7)));
            Assert.AreEqual(4, DrinkOrder.MenuPrice(Spritz(rank: 1)));
            Assert.Less(DrinkOrder.MenuPrice(Spritz(rank: 7)), 4 + 7, "lower than the old shape");
        }

        [Test]
        public void OrderRoll_DrawsFromTheLowestRanks_AndGrowsWithTheDay()
        {
            var recipes = Enumerable.Range(1, 10)
                .Select(rank => BandRecipe($"r{rank}", rank,
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7)))
                .Cast<RecipeDefinition>()
                .ToList();
            recipes.Add(new RecipeDefinition("unpourable", "Unpourable", 1, 5, 1, 0, 0,
                Array.Empty<PatternRequirement>()));

            var rng = new RunRng("orders-test").GetStream("orders");
            for (int i = 0; i < 40; i++)
            {
                var order = DrinkOrder.Roll(recipes, day: 1, TycoonConfig.Default, rng);
                Assert.LessOrEqual(order.Wanted.Rank, 4, "day 1 pool is the 4 lowest ranks");
                Assert.AreNotEqual("unpourable", order.Wanted.Id, "you cannot order what cannot be made");
            }
        }

        // ── the service verdict (GDD 23 §4) ─────────────────────────────────────

        [Test]
        public void ServingTheNamedRecipe_IsExact()
        {
            var glass = Glass(("gin", 0.35), ("soda", 0.35));
            var served = RatioRecipeMatcher.Match(glass, new[] { Spritz() }, Look);
            var order = new DrinkOrder(Spritz(), 6);

            Assert.AreEqual(OrderMatch.Exact, ServiceJudge.Compare(order, served, glass, Look));
        }

        [Test]
        public void TheRightFamily_IsClose_TheWrongOne_IsWrong()
        {
            var order = new DrinkOrder(Spritz(), 6);

            var neatGin = Glass(("gin", 0.9));
            Assert.AreEqual(OrderMatch.Close,
                ServiceJudge.Compare(order, null, neatGin, Look),
                "a straight spirit shares the spritz's dominant type");

            var lemonWater = Glass(("lemon", 0.9));
            Assert.AreEqual(OrderMatch.Wrong,
                ServiceJudge.Compare(order, null, lemonWater, Look));
        }

        [Test]
        public void AWrongDrink_PaysForWhatIsActuallyInTheGlass()
        {
            // v5 P11 / C1 -- this REPLACES AWrongDrink_PaysNothing_AndTipsNothing. The notes:
            // "Only the Base Price of the delivered drink is earned." A poured drink handed to
            // the wrong person is still a drink; the service failed, not the pour.
            var delivered = new RecipeMatch(Spritz(rank: 4), null);
            var full = Glass(("gin", 0.5), ("soda", 0.4));

            var verdict = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Wrong, full,
                served: delivered);

            Assert.AreEqual(DrinkOrder.MenuPrice(Spritz(rank: 4)), verdict.BasePaid,
                "they pay for the thing they were handed, not the thing they asked for");
            Assert.AreEqual(0, verdict.Tip, "nobody tips for the wrong drink");
            Assert.LessOrEqual(verdict.Satisfaction, 0.1, "and it sours them all the same");
        }

        [Test]
        public void AGlassThatIsNoDrinkAtAll_PaysNothing()
        {
            var full = Glass(("gin", 0.5), ("soda", 0.4));
            var verdict = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Wrong, full, served: null);

            Assert.AreEqual(0, verdict.BasePaid, "an unidentifiable glass is worth nothing");
        }

        [Test]
        public void ASeverelyUnderfilledGlass_IsRefused()
        {
            // v5 P11: the notes' "if the drink is severely underfilled, the customer may refuse
            // to pay". Below the floor nothing else is even weighed.
            var dribble = Glass(("gin", 0.15), ("soda", 0.10));   // a quarter of a glass
            var verdict = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Exact, dribble);

            Assert.AreEqual(OrderMatch.Refused, verdict.Match);
            Assert.AreEqual(0, verdict.Total, "they will not pay for a quarter of a drink");
            Assert.IsFalse(verdict.OrdersAgain);
        }

        [Test]
        public void FillCloseness_ScalesTheReward()
        {
            var brim = Glass(("gin", 0.5), ("soda", 0.45));
            var thin = Glass(("gin", 0.3), ("soda", 0.25));

            var good = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Exact, brim);
            var poor = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Exact, thin);

            Assert.AreEqual(1.0, good.FillScore, 1e-9, "a full glass meets the expectation");
            Assert.Less(poor.FillScore, good.FillScore);
            Assert.Less(poor.Tip, good.Tip, "a thin pour is paid for but tipped less");
            Assert.AreEqual(good.BasePaid, poor.BasePaid, "the base price is not at stake");
        }

        [Test]
        public void TheGarnishCraft_LiftsSatisfaction_AndGatesTheExtraRound()
        {
            var iced = new DrinkOrder(Spritz(), 6, new ServingSpec(new[] { Preparations.Ice }));

            var served = Glass(("gin", 0.5), ("soda", 0.5));
            served.AddPreparation(Preparations.Ice);
            var got = ServiceJudge.Judge(new CustomerVisit(iced, 60), OrderMatch.Exact, served);
            Assert.IsTrue(got.CraftLanded, "they wanted ice and got it");
            Assert.IsTrue(got.OrdersAgain);

            var plainGlass = Glass(("gin", 0.5), ("soda", 0.5));   // no ice
            var missed = ServiceJudge.Judge(new CustomerVisit(iced, 60), OrderMatch.Exact, plainGlass);
            Assert.IsFalse(missed.CraftLanded, "no ice — the craft was missed");
            Assert.Less(missed.Satisfaction, got.Satisfaction, "missing the garnish sours them");
            Assert.IsFalse(missed.OrdersAgain, "a missed garnish loses the extra round");

            // v5 P11: the spec now moves the TILL too. Missing what they asked for costs tip
            // -- it used to cost nothing at all, which made the craft read worth no money.
            Assert.Less(missed.Tip, got.Tip, "the missed garnish costs tip, not payment");
            Assert.AreEqual(got.BasePaid, missed.BasePaid, "but the drink is still paid for");
        }

        [Test]
        public void PatienceScalesTheTip_Continuously_NotAtACliff()
        {
            // v5 P11: the tip used to hit zero at half patience and stop mattering there. It
            // now fades the whole way down, so every second of someone's wait is worth money.
            var brim = Glass(("gin", 0.5), ("soda", 0.45));
            int TipAfter(double waited)
            {
                var v = Visit(price: 10, patience: 60);
                if (waited > 0) v.Tick(waited);
                return ServiceJudge.Judge(v, OrderMatch.Exact, brim).Tip;
            }

            int fresh = TipAfter(0), half = TipAfter(30), late = TipAfter(54);
            Assert.AreEqual(10, fresh, "a perfect serve doubles a $10 drink");
            Assert.Greater(fresh, half, "waiting costs tip");
            Assert.Greater(half, 0, "and half-patience still earns something -- no cliff");
            Assert.Greater(half, late, "and it keeps fading past the old window");
        }

        [Test]
        public void Ambience_LiftsSatisfaction()
        {
            var brim = Glass(("gin", 0.5), ("soda", 0.45));
            var plain = ServiceJudge.Judge(Visit(), OrderMatch.Close, brim);
            var nicer = ServiceJudge.Judge(Visit(), OrderMatch.Close, brim,
                WealthTier.Regular, ambienceBonus: 0.1);

            Assert.AreEqual(0.1, nicer.Satisfaction - plain.Satisfaction, 1e-9,
                "a nicer room pleases the same serve exactly that much more");
        }

        // ── the extra order (GDD 23 §5) ─────────────────────────────────────────

        /// <summary>An order with one garnish, and a glass that has it — a perfect craft serve.</summary>
        private static DrinkOrder IcedOrder(int price = 10) =>
            new DrinkOrder(Spritz(), price, new ServingSpec(new[] { Preparations.Ice }));

        private static GlassContents IcedGlass()
        {
            var g = Glass(("gin", 0.5), ("soda", 0.5));
            g.AddPreparation(Preparations.Ice);
            return g;
        }

        // ── two waits, not one (2026-08-02) ──────────────────────────────────────
        // The author's rule: being kept waiting to ORDER and being kept waiting for the
        // DRINK are different insults, on different clocks, and taking the order starts
        // the second one from full.

        [Test]
        public void BeingIgnoredWhileReadyToOrder_RunsItsOwnClockOut()
        {
            var visit = new CustomerVisit(IcedOrder(), patienceSeconds: 60,
                orderPatienceSeconds: 20);

            visit.Tick(19);
            Assert.AreEqual(VisitState.Waiting, visit.State, "still there at 19 of 20");
            Assert.AreEqual(60, visit.PatienceLeft, 1e-9, "the drink clock has not started");

            visit.Tick(2);
            Assert.AreEqual(VisitState.StormedOff, visit.State,
                "nobody came to ask — that is a walk-out even with nothing poured");
        }

        [Test]
        public void TakingTheOrder_StartsTheDrinkWaitFromFull()
        {
            var visit = new CustomerVisit(IcedOrder(), patienceSeconds: 60,
                orderPatienceSeconds: 20);

            visit.Tick(15);                       // fifteen seconds of being ignored
            visit.InspectId();                    // …then somebody finally asks

            Assert.AreEqual(60, visit.PatienceLeft, 1e-9,
                "the wait for the drink begins here, at full — not with what the asking left");
            Assert.IsFalse(visit.AwaitingOrderTaking);

            visit.Tick(59);
            Assert.AreEqual(VisitState.Waiting, visit.State);
            visit.Tick(2);
            Assert.AreEqual(VisitState.StormedOff, visit.State, "and it runs out on its own");
        }

        [Test]
        public void TheTwoWaits_AreDifferentLengths()
        {
            // Config, not a visit: the two curves must never collapse into one number, or
            // splitting them changes nothing a player can feel.
            var config = new TycoonConfig();
            for (int day = 1; day <= 30; day++)
                Assert.AreNotEqual(config.PatienceSeconds(day), config.OrderPatienceSeconds(day),
                    $"day {day}");
        }

        [Test]
        public void AnExtraRound_DoesNotWaitToBeAsked()
        {
            // Served blind, earns another round, and only then is the card read. The refill
            // Resolve set is that round's clock; reading the card must not replace it, and
            // the asking clock must not storm off a customer who is already drinking with you.
            var visit = new CustomerVisit(IcedOrder(10), patienceSeconds: 60,
                orderPatienceSeconds: 20);
            visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass()), IcedOrder(8));

            visit.InspectId();
            Assert.AreEqual(60 * CustomerVisit.ExtraOrderPatienceRefill, visit.PatienceLeft, 1e-9);

            visit.Tick(21);
            Assert.AreEqual(VisitState.Waiting, visit.State, "the asking clock is spent, not live");
        }

        [Test]
        public void APerfectServe_OrdersAnotherRound()
        {
            var visit = new CustomerVisit(IcedOrder(10), 60);

            var verdict = ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass());
            Assert.IsTrue(verdict.OrdersAgain);

            visit.Resolve(verdict, IcedOrder(8));

            Assert.AreEqual(VisitState.Waiting, visit.State, "still on the stool");
            Assert.AreEqual(1, visit.ExtraOrdersTaken);
            visit.InspectId();
            Assert.AreEqual(8, visit.Order.Price, "a fresh order is open");
            Assert.AreEqual(60 * CustomerVisit.ExtraOrderPatienceRefill, visit.PatienceLeft, 1e-9);
            Assert.Greater(visit.Paid, 0, "the first round is already paid");
        }

        [Test]
        public void ExtraOrders_CapAtTwo()
        {
            var visit = new CustomerVisit(IcedOrder(), 60);

            visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass()), IcedOrder(6));
            visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass()), IcedOrder(6));

            var third = ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass());
            Assert.IsFalse(third.OrdersAgain, "two extra rounds is the house limit");

            visit.Resolve(third);
            Assert.AreEqual(VisitState.Served, visit.State);
        }

        // ── patience (GDD 23 §2) ────────────────────────────────────────────────

        [Test]
        public void PatienceRunningOut_IsAStormOff()
        {
            var visit = Visit(patience: 10);
            visit.Tick(9);
            Assert.AreEqual(VisitState.Waiting, visit.State);

            visit.Tick(2);

            Assert.AreEqual(VisitState.StormedOff, visit.State);
            Assert.AreEqual(0, visit.Paid, "no payment for no drink");
            Assert.AreEqual(0, visit.Satisfaction, 1e-9);
            Assert.Throws<InvalidOperationException>(
                () => visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, null)),
                "you cannot serve someone who already left");
        }

        // ── deciding & savouring (GDD 23 §2, 2026-07-23) ────────────────────────

        [Test]
        public void WhileDeciding_TheyHaveNotOrdered_AndPatienceHolds_ThenTicks()
        {
            var visit = new CustomerVisit(new DrinkOrder(Spritz(), 6),
                patienceSeconds: 20, decideSeconds: 5);

            Assert.IsFalse(visit.HasOrdered, "just sat down — still reading the menu");
            visit.Tick(3);
            Assert.IsFalse(visit.HasOrdered, "still deciding after 3 of 5 seconds");
            Assert.AreEqual(20, visit.PatienceLeft, 1e-9, "thinking does not spend patience");
            Assert.AreEqual(0.0, visit.WaitFraction, 1e-9);

            visit.Tick(4);   // crosses the 5s decision by 2s → 2s of real waiting

            Assert.IsTrue(visit.HasOrdered, "mind made up, the order is on the bar");
            Assert.AreEqual(18, visit.PatienceLeft, 1e-9, "only the overspill past deciding ticks");
            Assert.AreEqual(0.1, visit.WaitFraction, 1e-9, "the wait clock starts at the order");
        }

        [Test]
        public void AServedCustomer_NursesTheDrink_ThenGetsUpToLeave()
        {
            var visit = new CustomerVisit(IcedOrder(10), 60);
            var verdict = ServiceJudge.Judge(visit, OrderMatch.Exact, IcedGlass());

            visit.Resolve(verdict, nextOrder: null, savorSeconds: 5);
            Assert.AreEqual(VisitState.Drinking, visit.State, "served, now nursing the drink");
            Assert.Greater(visit.Paid, 0, "paid at the serve, not at the leaving");

            visit.Tick(3);
            Assert.AreEqual(VisitState.Drinking, visit.State, "still sipping");

            visit.Tick(3);
            Assert.AreEqual(VisitState.Served, visit.State, "drink finished — up and out next tick");
        }

        [Test]
        public void ADrinkingCustomer_KeepsTheStool_UntilTheyFinish()
        {
            var rng = new RunRng("savor-floor");
            var day = new BarDay(day: 1, seats: 2, TycoonConfig.Default, rng.GetStream("arrivals"));
            CustomerVisit NewVisit() => Visit(patience: 1000);

            day.Tick(10_000, NewVisit);   // fills both stools
            Assert.AreEqual(2, day.Seated.Count);

            var drinker = day.Seated[0];
            drinker.Resolve(ServiceJudge.Judge(drinker, OrderMatch.Exact, null), savorSeconds: 5);
            Assert.AreEqual(VisitState.Drinking, drinker.State);

            day.Tick(2, NewVisit);
            Assert.IsTrue(day.Seated.Contains(drinker), "still nursing it — the stool stays taken");
            Assert.AreEqual(0, day.Finished.Count, "a drinker is not a finished visit yet");

            day.Tick(5, NewVisit);   // past the savour
            Assert.IsFalse(day.Seated.Contains(drinker), "finished the drink and left");
            Assert.AreEqual(1, day.Finished.Count);
        }

        // ── the ledger (GDD 23 §6–§7) ───────────────────────────────────────────

        [Test]
        public void ThreeConsecutiveRedDays_CloseTheBar()
        {
            var ledger = new DayLedger();

            ledger.CloseDay(1, income: 5, expenses: 10, averageSatisfaction: 0.5, tillAfter: -5);
            ledger.CloseDay(2, income: 5, expenses: 10, averageSatisfaction: 0.5, tillAfter: -10);
            Assert.IsFalse(ledger.IsBankrupt, "two strikes is a warning");

            ledger.CloseDay(3, income: 5, expenses: 10, averageSatisfaction: 0.5, tillAfter: -15);

            Assert.IsTrue(ledger.IsBankrupt);
        }

        [Test]
        public void OneDayBackAboveWater_WipesTheStrikes()
        {
            var ledger = new DayLedger();
            ledger.CloseDay(1, 5, 10, 0.5, tillAfter: -5);
            ledger.CloseDay(2, 5, 10, 0.5, tillAfter: -10);

            ledger.CloseDay(3, 20, 10, 0.5, tillAfter: 0);

            Assert.AreEqual(0, ledger.DebtStrikes, "debt is a spiral you can climb out of");
            Assert.IsFalse(ledger.IsBankrupt);
        }

        [Test]
        public void ALosingDay_WithMoneyInTheTill_IsNotAStrike()
        {
            // "In debt" means in debt: a rich bar eats a bad night without the clock
            // starting. The strike watches the till, not the day's net.
            var ledger = new DayLedger();

            ledger.CloseDay(1, income: 5, expenses: 50, averageSatisfaction: 0.5, tillAfter: 200);

            Assert.AreEqual(0, ledger.DebtStrikes);
        }

        [Test]
        public void TheSatisfactionBar_SetsTomorrowsCrowd()
        {
            var ledger = new DayLedger();

            ledger.CloseDay(1, 10, 5, averageSatisfaction: 0.8, tillAfter: 25);
            Assert.AreEqual(WealthTier.HighRoller, ledger.TomorrowsCrowd);

            ledger.CloseDay(2, 10, 5, averageSatisfaction: 0.5, tillAfter: 30);
            Assert.AreEqual(WealthTier.Regular, ledger.TomorrowsCrowd);

            // Broke sank with the zero-start standing rework (2026-08-02): only a bar the
            // room scores under 1.5 stars (satisfaction 0.125) draws the broke crowd.
            ledger.CloseDay(3, 10, 5, averageSatisfaction: 0.1, tillAfter: 35);
            Assert.AreEqual(WealthTier.Broke, ledger.TomorrowsCrowd);
        }

        // ── the floor (GDD 23 §1) ───────────────────────────────────────────────

        [Test]
        public void TheFloor_SeatsArrivals_UpToTheStools_AndClosesAtClosingTime()
        {
            var rng = new RunRng("floor-test");
            var day = new BarDay(day: 1, seats: 2, TycoonConfig.Default, rng.GetStream("arrivals"));

            CustomerVisit NewVisit() => Visit(patience: 1000);

            // A huge first tick can only fill the two stools — the rest wait at the door.
            // (v5 P12: it also runs the clock straight past closing, so nobody else arrives.)
            day.Tick(10_000, NewVisit);
            Assert.AreEqual(2, day.Seated.Count);
            Assert.AreEqual(2, day.Arrived);
            Assert.IsTrue(day.IsClosingTime, "the shift is over — the door is shut");
            Assert.IsFalse(day.IsComplete, "but two people are still sitting there");

            // Closing time does not throw anyone out mid-drink: the night ends when the last
            // stool empties.
            foreach (var visit in day.Seated)
                visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, null));
            day.Tick(1, NewVisit);

            Assert.IsTrue(day.IsComplete);
            Assert.AreEqual(2, day.Finished.Count);
            Assert.AreEqual(0, day.Seated.Count);
            Assert.Greater(day.AverageSatisfaction, 0.5, "everyone got their drink");
        }

        [Test]
        public void AnOpenNight_SeatsAsManyAsTheStoolsCanTurnOver()
        {
            // v5 P12 / C4: there is no quota. Serving fast frees stools, and a free stool is
            // someone new through the door — so throughput is the player's, not the design's.
            IReadOnlyList<CustomerVisit> None = null;
            int ServedIn(double serviceSeconds)
            {
                var rng = new RunRng("open-night");
                var day = new BarDay(day: 1, seats: 2, TycoonConfig.Default, rng.GetStream("arrivals"));
                double sinceServe = 0;
                while (!day.IsComplete)
                {
                    day.Tick(1.0, () => Visit(patience: 1000));
                    sinceServe += 1.0;
                    if (sinceServe < serviceSeconds) continue;
                    sinceServe = 0;
                    if (day.Seated.Count > 0)
                        day.Seated[0].Resolve(ServiceJudge.Judge(day.Seated[0], OrderMatch.Exact, null));
                }
                return day.Finished.Count;
            }

            int brisk = ServedIn(4.0), slow = ServedIn(20.0);
            Assert.Greater(brisk, slow,
                "the faster the bar turns a stool over, the more people it gets through");
        }
    }
}
