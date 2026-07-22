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
                ratioRequirements: bands, minFill: 0.5);

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

        private static CustomerRead ReadOf(Emotion intent, IntentDirection direction)
        {
            var readings = new List<StatReading>();
            for (int i = 0; i < Emotions.Count; i++) readings.Add(StatReading.Unknown);
            return new CustomerRead(readings, intent, direction);
        }

        private static EmotionDelta Delta(Emotion emotion, int amount)
        {
            var delta = new EmotionDelta();
            delta.Add(emotion, amount);
            return delta;
        }

        private static CustomerVisit Visit(int price = 10, double patience = 60,
            CustomerRead read = null) =>
            new CustomerVisit(new DrinkOrder(Spritz(), price), patience, read: read);

        // ── orders & menu (GDD 23 §3) ───────────────────────────────────────────

        [Test]
        public void MenuPrice_IsFourDollarsPlusRank()
        {
            Assert.AreEqual(11, DrinkOrder.MenuPrice(Spritz(rank: 7)));
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
        public void AWrongDrink_PaysNothing_AndTipsNothing()
        {
            var verdict = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Wrong, null);

            Assert.AreEqual(0, verdict.BasePaid, "the wrong drink pays nothing");
            Assert.AreEqual(0, verdict.Tip);
            Assert.AreEqual(0.05, verdict.Satisfaction, 1e-9);
        }

        [Test]
        public void TheGarnishCraft_LiftsSatisfaction_AndGatesTheExtraRound()
        {
            var iced = new DrinkOrder(Spritz(), 6, new[] { Preparations.Ice });

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

            // The garnish moves satisfaction, not the till: the tip is speed only.
            Assert.AreEqual(got.Tip, missed.Tip);
        }

        [Test]
        public void TheSpeedTip_ScalesAndFadesAcrossTheWindow()
        {
            var slow = Visit(patience: 60);
            slow.Tick(30);   // 50% waited — at the window edge, no speed tip

            Assert.AreEqual(0, ServiceJudge.Judge(slow, OrderMatch.Exact, null).Tip);
            Assert.AreEqual(4, ServiceJudge.Judge(Visit(), OrderMatch.Exact, null).Tip,
                "a serve at zero wait earns the full speed tip");
        }

        [Test]
        public void Ambience_LiftsSatisfaction()
        {
            var plain = ServiceJudge.Judge(Visit(), OrderMatch.Close, null);
            var nicer = ServiceJudge.Judge(Visit(), OrderMatch.Close, null,
                WealthTier.Regular, ambienceBonus: 0.1);

            Assert.AreEqual(0.6, plain.Satisfaction, 1e-9, "Close, no wait");
            Assert.AreEqual(0.7, nicer.Satisfaction, 1e-9, "a nicer room pleases the same serve more");
        }

        // ── the extra order (GDD 23 §5) ─────────────────────────────────────────

        /// <summary>An order with one garnish, and a glass that has it — a perfect craft serve.</summary>
        private static DrinkOrder IcedOrder(int price = 10) =>
            new DrinkOrder(Spritz(), price, new[] { Preparations.Ice });

        private static GlassContents IcedGlass()
        {
            var g = Glass(("gin", 0.5), ("soda", 0.5));
            g.AddPreparation(Preparations.Ice);
            return g;
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

            ledger.CloseDay(3, 10, 5, averageSatisfaction: 0.2, tillAfter: 35);
            Assert.AreEqual(WealthTier.Broke, ledger.TomorrowsCrowd);
        }

        // ── the floor (GDD 23 §1) ───────────────────────────────────────────────

        [Test]
        public void TheFloor_SeatsArrivals_UpToTheStools_AndFinishesTheDay()
        {
            var rng = new RunRng("floor-test");
            var day = new BarDay(day: 1, seats: 2, TycoonConfig.Default, rng.GetStream("arrivals"));
            Assert.AreEqual(8, day.CustomersPlanned, "day 1 sends 8 customers");

            CustomerVisit NewVisit() => Visit(patience: 1000);

            // A huge first tick can only fill the two stools — the rest wait at the door.
            day.Tick(10_000, NewVisit);
            Assert.AreEqual(2, day.Seated.Count);
            Assert.AreEqual(2, day.Arrived);

            // Serve everyone as they sit until the day is spent.
            int guard = 0;
            while (!day.IsComplete)
            {
                Assert.Less(guard++, 100, "the day must terminate");
                foreach (var visit in day.Seated)
                    visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, null));
                day.Tick(60, NewVisit);
            }

            Assert.AreEqual(8, day.Arrived);
            Assert.AreEqual(8, day.Finished.Count);
            Assert.AreEqual(0, day.Seated.Count);
            Assert.Greater(day.AverageSatisfaction, 0.5, "everyone got their drink");
        }
    }
}
