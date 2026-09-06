using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>The star economy (2026-09-06): a thing costs the stage it belongs to, a night
    /// pays the stage the bar has reached. Pure pins on the rule, then the run's own doors.</summary>
    public sealed class StarEconomyTests
    {
        [Test]
        public void TheStage_IsTheWholeStar()
        {
            Assert.AreEqual(1.0, StarEconomy.TierMultiplier(0.0), 1e-9);
            Assert.AreEqual(1.0, StarEconomy.TierMultiplier(0.99), 1e-9, "not yet one star");
            Assert.AreEqual(2.0, StarEconomy.TierMultiplier(1.0), 1e-9);
            Assert.AreEqual(3.0, StarEconomy.TierMultiplier(2.5), 1e-9);
            Assert.AreEqual(6.0, StarEconomy.TierMultiplier(5.0), 1e-9);
            Assert.AreEqual(6.0, StarEconomy.TierMultiplier(9.0), 1e-9, "held to the five the standing reaches");
            Assert.AreEqual(1.0, StarEconomy.TierMultiplier(-1.0), 1e-9, "and never under one");
            Assert.AreEqual(1.0, StarEconomy.TierMultiplier(double.NaN), 1e-9);
        }

        [Test]
        public void APrice_IsTheSheetTimesTheStage_ToTheDollar()
        {
            Assert.AreEqual(40, StarEconomy.PriceAt(40, 0.0));
            Assert.AreEqual(80, StarEconomy.PriceAt(40, 1.0));
            Assert.AreEqual(120, StarEconomy.PriceAt(40, 2.0));
            Assert.AreEqual(9, StarEconomy.PriceAt(9, 0.5));
        }

        // ── through the run ──────────────────────────────────────────────────────

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 40),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
        });

        private static RecipeDefinition Page(string id, int rank) => new RecipeDefinition(
            id, id, rank: rank, baseFlavor: 6, baseMult: 1, flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5);

        private static TycoonRun NewRun(params FixtureDefinition[] fixtures) =>
            new TycoonRun(NewShelf(), new[] { Page("spritz", 2) }, new RunRng("stage"),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0, weeklyJobs: false),
                fixtures: fixtures);

        [Test]
        public void AFitting_CostsItsOwnStage()
        {
            var cheap = new FixtureDefinition("candle", "Candle", "counter_end", 30, 0, "A flame.", "fx_candle", comfort: 0.2);
            var dear = new FixtureDefinition("neon", "Neon", "wall_right", 75, 2.5, "Pink.", "fx_neon", comfort: 0.2);
            var run = NewRun(cheap, dear);
            Assert.AreEqual(30, run.FixturePrice(cheap), "stage zero: the sheet");
            Assert.AreEqual(225, run.FixturePrice(dear), "a two-and-a-half-star piece is on the third stage: three times");

            run.DevSkipToDayEnd();
            int before = run.Money;
            Assert.AreEqual(30, run.BuyFixture("candle"));
            Assert.AreEqual(before - 30, run.Money);
            Assert.AreEqual(30, run.TodaysPurchases[0].Price, "the slip carries what was paid");
        }

        [Test]
        public void ARecipe_CostsItsGatesStage()
        {
            var run = NewRun();
            var starter = Page("a", 3);
            var oneStar = Page("b", 10);
            Assert.AreEqual(0.0, run.RecipeStarGate(starter), 1e-9);
            Assert.AreEqual(1.0, run.RecipeStarGate(oneStar), 1e-9);
            Assert.AreEqual(Math.Max(9, 5 + (5 * (3 - 2)) / 2), run.RecipePrice(starter), "stage zero: the curve");
            Assert.AreEqual(2 * Math.Max(9, 5 + (5 * (10 - 2)) / 2), run.RecipePrice(oneStar), "one star: twice the curve");
        }

        [Test]
        public void ADrinkAndTheRent_PayTheBarsStage()
        {
            var zero = NewRun();
            var made = NewRun();
            made.Rating.DevSet(2.0);
            int guard = 0;
            while (zero.Floor.Seated.Count == 0 || made.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 200);
                zero.Tick(5); made.Tick(5);
            }
            var a = zero.Floor.Seated[0]; var b = made.Floor.Seated[0];
            guard = 0;
            while (!a.HasOrdered || !b.HasOrdered) { Assert.Less(guard++, 200); zero.Tick(1); made.Tick(1); }
            a.InspectId(); b.InspectId();
            Assert.AreEqual(a.Order.Wanted.Id, b.Order.Wanted.Id, "the same page, on the same seed");
            Assert.AreEqual(3 * a.Order.Price, b.Order.Price, "a two-star bar's drinker pays three times");

            zero.DevSkipToDayEnd(); made.DevSkipToDayEnd();
            Assert.AreEqual(3 * zero.DayRent, made.DayRent, "and its landlord asks three times");
        }
    }
}
