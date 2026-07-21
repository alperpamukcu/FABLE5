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
        public void AWrongDrink_PaysHalf_AndTipsNothing()
        {
            var verdict = ServiceJudge.Judge(Visit(price: 10), OrderMatch.Wrong, null);

            Assert.AreEqual(5, verdict.BasePaid);
            Assert.AreEqual(0, verdict.Tip);
            Assert.AreEqual(0.2, verdict.Satisfaction, 1e-9);
        }

        [Test]
        public void TheMoodTip_NeedsRealMovement_AndCapsAtFive()
        {
            var read = ReadOf(Emotion.Anger, IntentDirection.Extinguish);

            var small = ServiceJudge.Judge(Visit(read: read), OrderMatch.Exact, Delta(Emotion.Anger, -5));
            Assert.IsFalse(small.MoodTipLanded, "−5 is below the tip threshold");

            var landed = ServiceJudge.Judge(Visit(read: read), OrderMatch.Exact, Delta(Emotion.Anger, -8));
            Assert.IsTrue(landed.MoodTipLanded);
            Assert.AreEqual(3 + 1, landed.Tip, "$3 mood + $1 speed at zero wait");

            var big = ServiceJudge.Judge(Visit(read: read), OrderMatch.Exact, Delta(Emotion.Anger, -20));
            Assert.AreEqual(5 + 1, big.Tip, "mood tip caps at $5");
        }

        [Test]
        public void TheSpeedTip_OnlyInsideTheWindow()
        {
            var slow = Visit(patience: 60);
            slow.Tick(30);   // 50% waited

            Assert.AreEqual(0, ServiceJudge.Judge(slow, OrderMatch.Exact, null).Tip);
            Assert.AreEqual(1, ServiceJudge.Judge(Visit(), OrderMatch.Exact, null).Tip);
        }

        // ── the extra order (GDD 23 §5) ─────────────────────────────────────────

        [Test]
        public void APerfectServe_OrdersAnotherRound()
        {
            var read = ReadOf(Emotion.Anger, IntentDirection.Extinguish);
            var visit = Visit(price: 10, patience: 60, read: read);

            var verdict = ServiceJudge.Judge(visit, OrderMatch.Exact, Delta(Emotion.Anger, -10));
            Assert.IsTrue(verdict.OrdersAgain);

            visit.Resolve(verdict, new DrinkOrder(Spritz(), 8));

            Assert.AreEqual(VisitState.Waiting, visit.State, "still on the stool");
            Assert.AreEqual(1, visit.ExtraOrdersTaken);
            Assert.AreEqual(8, visit.Order.Price, "a fresh order is open");
            Assert.AreEqual(60 * CustomerVisit.ExtraOrderPatienceRefill, visit.PatienceLeft, 1e-9);
            Assert.Greater(visit.Paid, 0, "the first round is already paid");
        }

        [Test]
        public void ExtraOrders_CapAtTwo()
        {
            var read = ReadOf(Emotion.Anger, IntentDirection.Extinguish);
            var visit = Visit(read: read);
            var delta = Delta(Emotion.Anger, -10);

            visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, delta), new DrinkOrder(Spritz(), 6));
            visit.Resolve(ServiceJudge.Judge(visit, OrderMatch.Exact, delta), new DrinkOrder(Spritz(), 6));

            var third = ServiceJudge.Judge(visit, OrderMatch.Exact, delta);
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

            ledger.CloseDay(1, income: 5, expenses: 10, averageSatisfaction: 0.5);
            ledger.CloseDay(2, income: 5, expenses: 10, averageSatisfaction: 0.5);
            Assert.IsFalse(ledger.IsBankrupt, "two strikes is a warning");

            ledger.CloseDay(3, income: 5, expenses: 10, averageSatisfaction: 0.5);

            Assert.IsTrue(ledger.IsBankrupt);
        }

        [Test]
        public void OneGoodDay_WipesTheStrikes()
        {
            var ledger = new DayLedger();
            ledger.CloseDay(1, 5, 10, 0.5);
            ledger.CloseDay(2, 5, 10, 0.5);

            ledger.CloseDay(3, 20, 10, 0.5);

            Assert.AreEqual(0, ledger.DebtStrikes, "debt is a spiral you can climb out of");
            Assert.IsFalse(ledger.IsBankrupt);
        }

        [Test]
        public void TheSatisfactionBar_SetsTomorrowsCrowd()
        {
            var ledger = new DayLedger();

            ledger.CloseDay(1, 10, 5, averageSatisfaction: 0.8);
            Assert.AreEqual(WealthTier.HighRoller, ledger.TomorrowsCrowd);

            ledger.CloseDay(2, 10, 5, averageSatisfaction: 0.5);
            Assert.AreEqual(WealthTier.Regular, ledger.TomorrowsCrowd);

            ledger.CloseDay(3, 10, 5, averageSatisfaction: 0.2);
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
