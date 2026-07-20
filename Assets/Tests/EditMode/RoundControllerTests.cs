using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The customer round under the pour system (GDD 21): hold a bottle, watch the glass,
    /// serve it. Rewritten wholesale from the card-era suite — the round no longer deals a
    /// rail, selects cards or restocks, so almost nothing carried over except the win/close
    /// conditions and the preview guarantees.
    /// </summary>
    public class RoundControllerTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(string id, IngredientType type, int flavor) =>
            new IngredientCard(id, id, type, flavor);

        private static Shelf SpiritShelf(int bottles = 6, int flavor = 6, double capacity = 20) =>
            new Shelf(Enumerable.Range(0, bottles)
                .Select(i => new ShelfBottle(Card($"spirit_{i}", IngredientType.Spirit, flavor), capacity)));

        private static RoundController NewRound(Shelf shelf = null, double target = 1000,
            RoundConfig config = null) =>
            new RoundController(shelf ?? SpiritShelf(), Recipes,
                new CustomerOrder("Test", target), config);

        // ── pouring ──────────────────────────────────────────────────────────────

        [Test]
        public void ARoundStarts_WithAnEmptyGlassAndNothingHeld()
        {
            var round = NewRound();

            Assert.IsTrue(round.Glass.IsEmpty);
            Assert.IsNull(round.PouringId);
            Assert.AreEqual(4, round.DrinksRemaining);
            Assert.AreEqual(RoundPhase.InProgress, round.Phase);
        }

        [Test]
        public void HoldingABottle_MovesVolumeOverTime()
        {
            var round = NewRound();
            var bottle = round.Shelf.Bottles[0];

            round.BeginPour(bottle.Id);
            double poured = round.PourTick(1.0);
            round.EndPour();

            Assert.AreEqual(bottle.PourRate, poured, 1e-9, "one second at the bottle's rate");
            Assert.AreEqual(poured, round.Glass.TotalVolume, 1e-9);
            Assert.IsNull(round.PouringId, "released");
        }

        [Test]
        public void PouringTwoBottles_BuildsARatio()
        {
            var round = NewRound();
            var a = round.Shelf.Bottles[0].Id;
            var b = round.Shelf.Bottles[1].Id;

            round.PourMeasure(a, 0.7);
            round.PourMeasure(b, 0.3);

            Assert.AreEqual(0.7, round.Glass.RatioOf(a), 1e-9);
            Assert.AreEqual(0.3, round.Glass.RatioOf(b), 1e-9);
        }

        [Test]
        public void PouringPastTheBrim_Spills_ButTheGlassStillServes()
        {
            var round = NewRound();

            round.PourMeasure(round.Shelf.Bottles[0].Id, round.Config.GlassCapacity + 0.1);
            Assert.IsTrue(round.Glass.IsOverflowing);

            var breakdown = round.Serve();

            Assert.AreEqual(0, breakdown.FinalScore, "a spill is never a recipe");
            Assert.AreEqual(1, round.Spills, "the counter is wet either way");
            Assert.IsTrue(round.Glass.IsEmpty, "the drink went out, mess and all");
        }

        [Test]
        public void BinningASpill_ClearsTheGlass_AndCountsAgainstYou()
        {
            var round = NewRound();
            round.PourMeasure(round.Shelf.Bottles[0].Id, round.Config.GlassCapacity + 0.1);

            round.Discard();

            Assert.IsTrue(round.Glass.IsEmpty);
            Assert.AreEqual(1, round.Spills);
            Assert.AreEqual(4, round.DrinksRemaining, "a spill costs volume, not the customer's patience");
        }

        [Test]
        public void AnEmptyBottle_StopsTheHeldPour()
        {
            var round = NewRound(SpiritShelf(bottles: 2, capacity: 0.2));
            var bottle = round.Shelf.Bottles[0];

            round.BeginPour(bottle.Id);
            round.PourTick(10.0);

            Assert.IsTrue(bottle.IsEmpty);
            Assert.IsNull(round.PouringId, "nothing left to hold");
            Assert.AreEqual(0.2, round.Glass.TotalVolume, 1e-9, "you get what was left");
        }

        [Test]
        public void PouringSomethingNotOnTheShelf_Throws()
        {
            var round = NewRound();
            Assert.Throws<ArgumentException>(() => round.BeginPour("absinthe"));
        }

        // ── serving ──────────────────────────────────────────────────────────────

        [Test]
        public void ServingScores_ClearsTheGlass_AndSpendsADrink()
        {
            var round = NewRound();
            round.PourMeasure(round.Shelf.Bottles[0].Id, 0.6);

            var breakdown = round.Serve();

            Assert.GreaterOrEqual(breakdown.FinalScore, 0);
            Assert.IsTrue(round.Glass.IsEmpty);
            Assert.AreEqual(3, round.DrinksRemaining);
        }

        [Test]
        public void AnEmptyGlass_CannotBeServed()
        {
            var round = NewRound();
            Assert.Throws<InvalidOperationException>(() => round.Serve());
        }

        [Test]
        public void ReachingTheTarget_WinsImmediately()
        {
            var round = NewRound(target: 1);
            round.PourMeasure(round.Shelf.Bottles[0].Id, 0.6);

            round.Serve();

            Assert.AreEqual(RoundPhase.Won, round.Phase);
        }

        [Test]
        public void RunningOutOfDrinksBelowTarget_ClosesTheVisit()
        {
            var round = NewRound(target: 100000);
            for (int i = 0; i < 4; i++)
            {
                round.PourMeasure(round.Shelf.Bottles[0].Id, 0.4);
                round.Serve();
            }

            Assert.AreEqual(RoundPhase.Closed, round.Phase);
            Assert.AreEqual(0, round.DrinksRemaining);
        }

        [Test]
        public void ActingAfterTheRoundIsOver_Throws()
        {
            var round = NewRound(target: 1);
            round.PourMeasure(round.Shelf.Bottles[0].Id, 0.6);
            round.Serve(); // wins

            Assert.Throws<InvalidOperationException>(() => round.BeginPour(round.Shelf.Bottles[0].Id));
            Assert.Throws<InvalidOperationException>(() => round.Serve());
        }

        // ── previews ─────────────────────────────────────────────────────────────

        [Test]
        public void PreviewScore_MatchesTheServe_WithoutConsuming()
        {
            var round = NewRound();
            round.PourMeasure(round.Shelf.Bottles[0].Id, 0.6);

            var preview = round.PreviewScore();
            Assert.AreEqual(4, round.DrinksRemaining, "previewing spends nothing");
            Assert.IsFalse(round.Glass.IsEmpty, "…and pours nothing away");

            var actual = round.Serve();
            Assert.AreEqual(preview.FinalScore, actual.FinalScore, 1e-9);
        }

        [Test]
        public void RecipeLevels_AreAppliedWhenProvided()
        {
            // A glass of nothing but Spirit is a Neat Pour, whose bands are derived from its
            // "1 Spirit alone" pattern.
            var levels = new Dictionary<string, int> { ["neat_pour"] = 2 };
            var leveled = new RoundController(SpiritShelf(), Recipes,
                new CustomerOrder("Test", 1e9), recipeLevels: levels);
            var plain = new RoundController(SpiritShelf(), Recipes, new CustomerOrder("Test", 1e9));

            leveled.PourMeasure(leveled.Shelf.Bottles[0].Id, 0.6);
            plain.PourMeasure(plain.Shelf.Bottles[0].Id, 0.6);

            Assert.Greater(leveled.Serve().FinalScore, plain.Serve().FinalScore,
                "a levelled recipe scores more for the same pour");
        }

        [Test]
        public void GlasswareCapacity_ChangesWhatAFullPourMeans()
        {
            var small = NewRound(config: new RoundConfig(glassCapacity: 1.0));
            var large = NewRound(config: new RoundConfig(glassCapacity: 2.0));

            small.PourMeasure(small.Shelf.Bottles[0].Id, 1.0);
            large.PourMeasure(large.Shelf.Bottles[0].Id, 1.0);

            Assert.AreEqual(1.0, small.Glass.FillFraction, 1e-9);
            Assert.AreEqual(0.5, large.Glass.FillFraction, 1e-9);
            Assert.IsFalse(small.Glass.IsOverflowing, "exactly full is not a spill");
        }
    }
}
