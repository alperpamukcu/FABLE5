using System;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE SINK IS THE BIN NOW, AND THE SINK IS AN UPGRADE (2026-08-26, the author: "çöp
    /// kutusu yerine lavabo kullanılacak, üst seviye lavabo alındığında dökülen içkilerden
    /// zarar elde edilmeyecek, başlangıç lavabosunda içkiyi çöpe attığında para yiyeceksin").
    ///
    /// The fee itself is old and covered by TycoonRunTests; what is new is that a piece of
    /// DRESSING can now switch it off. That makes the brass basin the first fixture in the
    /// game that changes what the bar can afford to do, so the boundary is worth pinning:
    /// the waiver is a flag on the fixture, it needs the fixture to be OWNED and not merely
    /// listed, and a run with no fixtures at all — which is every bench setup and most of
    /// the older suites — is unchanged.
    /// </summary>
    public sealed class DrainTests
    {
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

        /// <summary>The basin the bar opens with: standing in the room, and it charges.</summary>
        private static FixtureDefinition SteelSink() =>
            new FixtureDefinition("counter_sink", "Steel Sink", "sink", 40, 0,
                "Cold water.", "fx_sink", startsInTheRoom: true, level: 1, isDrain: true);

        /// <summary>The one it can fit later: the same job, no write-off.</summary>
        private static FixtureDefinition BrassSink() =>
            new FixtureDefinition("sink_brass", "Brass Sink", "sink", 85, 0,
                "Neon in the basin.", "fx_sink_gold", level: 2,
                isDrain: true, drainsFree: true);

        private static TycoonRun NewRun(int money, params FixtureDefinition[] fixtures) =>
            new TycoonRun(NewShelf(), Book, new RunRng("drain"),
                config: new TycoonConfig(money, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: fixtures);

        [Test]
        public void TheSteelBasin_WritesTheGoodsOff()
        {
            var run = NewRun(50, SteelSink(), BrassSink());
            Assert.IsFalse(run.WasteIsFree, "the brass one is in the CATALOGUE, not in the bar");

            int before = run.Money;
            run.PourMeasure("gin", 0.5);
            int fee = run.DiscardGlass();

            Assert.AreEqual((int)Math.Ceiling(0.5 * TycoonRun.BinFeePerVolume), fee);
            Assert.AreEqual(before - fee, run.Money);
            Assert.IsTrue(run.Glass.IsEmpty);
        }

        [Test]
        public void TheBrassBasin_CostsNothingToPourAway()
        {
            // Comfortably clear of the fitting AND the night's rent: a bar that closes on
            // the way to tomorrow never reaches the thing under test.
            var run = NewRun(600, SteelSink(), BrassSink());
            run.DevSkipToDayEnd();
            run.BuyFixture("sink_brass");
            run.ContinueToNextDay();

            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase, "the bar has to still be open");
            Assert.IsTrue(run.WasteIsFree, "the upgrade IS 'spilt drink stops costing'");

            int before = run.Money;
            run.PourMeasure("gin", 0.5);
            Assert.AreEqual(0, run.DiscardGlass(), "nothing is written off down the brass one");
            Assert.AreEqual(before, run.Money);
            Assert.IsTrue(run.Glass.IsEmpty, "and the drink is still gone");
        }

        [Test]
        public void ARunWithNoFixtures_StillPaysTheFee()
        {
            // Every bench setup and most of the older suites build a run with no catalogue
            // at all. They must be unchanged, or this upgrade would have quietly handed
            // free waste to every test in the house.
            var run = NewRun(50);
            Assert.IsFalse(run.WasteIsFree);
            run.PourMeasure("gin", 0.5);
            Assert.Greater(run.DiscardGlass(), 0);
        }

        [Test]
        public void AWaiverMustBeOnADrain()
        {
            // A free-drain flag on something that is not a drain is a rule nobody can reach:
            // the fee is charged at the basin, so only a basin may excuse it.
            Assert.Throws<ArgumentException>(() =>
                new FixtureDefinition("fern", "Fern", "plant_left", 20, 0, "Green.", "fx_fern",
                    drainsFree: true));
            Assert.Throws<ArgumentException>(() =>
                new FixtureDefinition("font", "Font", "taps", 35, 0, "Beer.", "fx_tap_single",
                    tapLevel: 1, isDrain: true));
        }

        [Test]
        public void TheEmptyDrain_IsStillFreeOnEitherBasin()
        {
            // The UI resets the vessels through this verb, so an empty pour-away must never
            // be a fine — with or without the upgrade.
            var steel = NewRun(50, SteelSink());
            Assert.AreEqual(0, steel.DiscardGlass());
            Assert.AreEqual(50, steel.Money);
        }
    }
}
