using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The bar-dressing fixtures (2026-08-10): parse rules, and the purchase contract
    /// every buyable in this game signs — books the expense, applies the effect, refunds
    /// the same night, never sells on credit, and respects its gate.
    /// </summary>
    public sealed class FixtureTests
    {
        // ── the catalogue under test ────────────────────────────────────────────

        private static FixtureDefinition Fern(double stars = 0) =>
            new FixtureDefinition("fern_pot", "Potted Fern", "plant_left", 20, stars,
                "A little green.", "fx_fern");

        private static FixtureDefinition Lantern() =>
            new FixtureDefinition("lantern", "Hanging Lantern", "lamp_left", 60, 1.5,
                "Warm.", "fx_lantern", 1f, 0.78f, 0.5f, 0.55f, 70f);

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
        });

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[]
        {
            new RecipeDefinition("neat", "Neat Pour", rank: 1, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[] { new RatioRequirement(IngredientType.Spirit, 0.9, 1.0) },
                minFill: 0.2),
        };

        private static TycoonRun RunAtDayEnd(int money = 200, params FixtureDefinition[] fixtures)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng("fixtures"),
                config: new TycoonConfig(money, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: fixtures);
            run.DevSkipToDayEnd();
            return run;
        }

        // ── parsing ─────────────────────────────────────────────────────────────

        /// <summary>The hooks every catalogue in these tests stands its pieces in. A room
        /// with no slots is a content bug of its own, so every fixture case needs them.</summary>
        private const string Slots = @"""slots"": [
            { ""id"": ""plant_left"", ""x"": 20, ""y"": 129 },
            { ""id"": ""lamp_left"", ""x"": 151, ""y"": 266 },
            { ""id"": ""s1"", ""x"": 0, ""y"": 0 },
            { ""id"": ""s2"", ""x"": 10, ""y"": 0, ""onCounter"": true }],";

        [Test]
        public void ParseFixtures_ReadsTheCatalogue_LightsAndAll()
        {
            var loaded = DataLoader.ParseFixtures(@"{
                ""version"": 1," + Slots + @"
                ""fixtures"": [
                    { ""id"": ""fern"", ""name"": ""Fern"", ""slot"": ""plant_left"",
                      ""price"": 20, ""stars"": 0, ""flavor"": ""green"", ""sprite"": ""fx_fern"" },
                    { ""id"": ""lantern"", ""name"": ""Lantern"", ""slot"": ""lamp_left"",
                      ""price"": 60, ""stars"": 1.5, ""sprite"": ""fx_lantern"",
                      ""lightR"": 1.0, ""lightG"": 0.8, ""lightB"": 0.5,
                      ""lightIntensity"": 0.55, ""lightRadius"": 70 }
                ]}");

            var fixtures = loaded.Fixtures;
            Assert.AreEqual(2, fixtures.Count);
            Assert.IsFalse(fixtures[0].HasLight, "a fern does not shine");
            Assert.IsTrue(fixtures[1].HasLight);
            Assert.AreEqual(70f, fixtures[1].LightRadius);
            Assert.AreEqual(1.5, fixtures[1].Stars);

            // The slots come out of the same file, and they carry where AND how they draw.
            Assert.AreEqual(4, loaded.Slots.Count);
            Assert.AreEqual(129f, loaded.Slots[0].Y);
            Assert.IsFalse(loaded.Slots[2].OnCounter);
            Assert.IsTrue(loaded.Slots[3].OnCounter, "a counter-top slot says so in the data");
        }

        [Test]
        public void AFixture_CannotName_ASlotTheRoomDoesNotHave()
        {
            // The room used to warn about this at RUNTIME, on the night the piece was
            // bought — a fixture sold and never seen. It is a content bug, so it fails at
            // the load like every other content bug.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @"
                ""fixtures"": [
                    { ""id"": ""a"", ""name"": ""A"", ""slot"": ""nowhere"",
                      ""price"": 10, ""sprite"": ""x"" }]}"));
        }

        [Test]
        public void ARoom_WithNoSlots_IsRefused()
        {
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{ ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" }]}"));
        }

        [Test]
        public void ParseFixtures_RefusesContentBugs_AtLoad()
        {
            // The same id twice.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" },
                { ""id"": ""a"", ""name"": ""B"", ""slot"": ""s2"", ""price"": 10, ""sprite"": ""x"" }]}"));
            // Two fixtures fighting over one hook.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" },
                { ""id"": ""b"", ""name"": ""B"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" }]}"));
            // A lamp that shines but has no radius.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""lightIntensity"": 0.5 }]}"));
            // A freebie.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 0, ""sprite"": ""x"" }]}"));
        }

        // ── the purchase contract ───────────────────────────────────────────────

        [Test]
        public void BuyFixture_BooksTheExpense_AndDressesTheRoom()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            int before = run.Money;

            int price = run.BuyFixture("fern_pot");

            Assert.AreEqual(20, price, "the definition prices it");
            Assert.AreEqual(before - 20, run.Money);
            Assert.AreEqual(20, run.DayUpgrades, "the invoice itemises it");
            Assert.IsTrue(run.OwnsFixture("fern_pot"));
            Assert.AreEqual(1, run.OwnedFixtureCount);
            var slip = run.TodaysPurchases[run.TodaysPurchases.Count - 1];
            Assert.AreEqual(TycoonRun.DayPurchase.Kind.Fixture, slip.What);
            Assert.AreEqual("Potted Fern", slip.Name);
        }

        [Test]
        public void AFixture_IsDressing_NotAFitting()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            Assert.IsTrue(run.CanFitTonight, "the night starts with its fitting free");

            run.BuyFixture("fern_pot");

            Assert.IsTrue(run.CanFitTonight,
                "a fern changes what the room looks like, not what the bar IS — " +
                "the night's one fitting must still be spendable");
        }

        [Test]
        public void SameNightRefund_PutsTheFixtureBackOnTheTruck()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            int before = run.Money;
            run.BuyFixture("fern_pot");

            run.RefundToday(run.TodaysPurchases.Count - 1);

            Assert.AreEqual(before, run.Money, "the till is made whole");
            Assert.AreEqual(0, run.DayUpgrades);
            Assert.IsFalse(run.OwnsFixture("fern_pot"));
            // ...and having been refunded, it can be bought again tonight.
            run.BuyFixture("fern_pot");
            Assert.IsTrue(run.OwnsFixture("fern_pot"));
        }

        [Test]
        public void AtDawn_TheFixtureIsFinal_AndBilled()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            run.BuyFixture("fern_pot");

            var result = run.ContinueToNextDay();

            Assert.AreEqual(run.Config.Rent(1) + 20, result.Expenses, "rent + the fern");
            Assert.AreEqual(0, run.TodaysPurchases.Count, "the slip is torn up");
            Assert.IsTrue(run.OwnsFixture("fern_pot"), "the fern stays");
        }

        [Test]
        public void NothingSellsOnCredit_FixturesIncluded()
        {
            // The skip to day end plays a real day — rent lands — so the broke-bar premise
            // is asserted on what the till actually holds THERE, not on the starting money.
            var run = RunAtDayEnd(money: 10, fixtures: Fern());
            int tillAtClose = run.Money;
            Assert.Less(tillAtClose, 20, "the premise: the bar cannot afford the fern");
            int upgradesBefore = run.DayUpgrades;

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("fern_pot"));

            Assert.AreEqual(tillAtClose, run.Money, "a refused buy takes nothing");
            Assert.AreEqual(upgradesBefore, run.DayUpgrades, "and books nothing");
            Assert.IsFalse(run.OwnsFixture("fern_pot"));
        }

        [Test]
        public void TheStarGate_HoldsUntilTheRoomEarnsIt()
        {
            var run = RunAtDayEnd(fixtures: Lantern());
            Assert.Less(run.Rating.Average, 1.5, "a new bar has no stars yet");

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("lantern"),
                "a 1.5-star lantern is not for a no-star room");

            run.Rating.DevSet(1.5);
            run.BuyFixture("lantern");
            Assert.IsTrue(run.OwnsFixture("lantern"));
        }

        [Test]
        public void UnknownAndDoubleBuys_AreRefused()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("chandelier"),
                "the catalogue has no chandelier");

            run.BuyFixture("fern_pot");
            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("fern_pot"),
                "one fern per plant_left");
        }
    }
}
