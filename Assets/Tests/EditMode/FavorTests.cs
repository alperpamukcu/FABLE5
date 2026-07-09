using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>Skipping Customer A for a Regular's Favor (GDD 5.2 rulings).</summary>
    public class FavorTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static readonly ToolDefinition Muddler = new ToolDefinition(
            "muddler", "Muddler", 3, ToolOp.Enhance, 2, Enhancement.Infused);

        private static PatronDefinition PatronDef(string id) =>
            new PatronDefinition(id, id, PatronRarity.Common, 4, "",
                new[] { new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 1) });

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunController NewRun(string seed,
            IReadOnlyList<PatronDefinition> patronPool = null,
            IReadOnlyList<ToolDefinition> toolPool = null)
        {
            return new RunController(SpiritCards(), Recipes, new RunRng(seed), null,
                new RunConfig(targetProvider: (n, s) => 10),
                patronPool: patronPool, toolPool: toolPool);
        }

        /// <summary>Finds a seed whose first skip grants the wanted favor (deterministic).</summary>
        private static RunController RunWithFavor(RegularsFavorKind wanted,
            IReadOnlyList<PatronDefinition> patronPool = null,
            IReadOnlyList<ToolDefinition> toolPool = null)
        {
            for (int i = 0; i < 60; i++)
            {
                var run = NewRun($"FAVOR-{i}", patronPool, toolPool);
                if (run.SkipCustomerA() == wanted) return run;
            }
            throw new InvalidOperationException($"No seed produced {wanted} in 60 tries.");
        }

        [Test]
        public void Skip_JumpsToCustomerB_WithNoTipsAndNoShop()
        {
            var run = NewRun("SKIP");
            run.SkipCustomerA();

            Assert.AreEqual(CustomerSlot.CustomerB, run.Slot);
            Assert.AreEqual(RunPhase.CustomerRound, run.Phase);
            Assert.IsNull(run.Shop, "no Back Room on a skip");
            Assert.IsNull(run.LastTips, "no tips on a skip");
            Assert.AreEqual(40, run.CurrentRound.DeckDrawCount, "all 48 cards stay in the run");
        }

        [Test]
        public void Skip_RequiresAnUntouchedCustomerARound()
        {
            var touched = NewRun("SKIP");
            touched.Restock(new[] { touched.CurrentRound.Rail[0] });
            Assert.IsFalse(touched.CanSkipCustomerA);
            Assert.Throws<InvalidOperationException>(() => touched.SkipCustomerA());

            var atB = NewRun("SKIP");
            atB.SkipCustomerA();
            Assert.Throws<InvalidOperationException>(() => atB.SkipCustomerA(), "B can't be skipped");
        }

        [Test]
        public void Favor_IsSeedDeterministic()
        {
            var pool = new[] { PatronDef("a"), PatronDef("b") };
            var first = NewRun("PAIR", pool, new[] { Muddler });
            var second = NewRun("PAIR", pool, new[] { Muddler });

            Assert.AreEqual(first.SkipCustomerA(), second.SkipCustomerA());
            Assert.AreEqual(first.LastFavorText, second.LastFavorText);
        }

        [Test]
        public void FreePatronFavor_SeatsAPatron()
        {
            var run = RunWithFavor(RegularsFavorKind.FreePatron,
                patronPool: new[] { PatronDef("a"), PatronDef("b") });
            Assert.AreEqual(1, run.Patrons.Count);
        }

        [Test]
        public void FreeToolFavor_FillsTheBelt()
        {
            var run = RunWithFavor(RegularsFavorKind.FreeTool, toolPool: new[] { Muddler });
            CollectionAssert.Contains(run.ToolInventory.ToList(), Muddler);
        }

        [Test]
        public void CashFavor_PaysFive()
        {
            // Empty pools: patron and tool favors always fall through toward cash.
            var run = RunWithFavor(RegularsFavorKind.Cash);
            Assert.AreEqual(4 + 5, run.Money, "starting $4 + the $5 favor");
        }

        [Test]
        public void DoubledTipFavor_DoublesTheNextBaseTip_Once()
        {
            var run = RunWithFavor(RegularsFavorKind.DoubledTip);

            run.Mix(new[] { run.CurrentRound.Rail[0] }); // win Customer B
            Assert.AreEqual(8, run.LastTips.Base, "Customer B tip $4 doubled");

            run.ContinueToNextCustomer();
            run.Mix(new[] { run.CurrentRound.Rail[0] }); // win the VIP
            Assert.AreEqual(5, run.LastTips.Base, "one-shot: VIP tip back to normal");
        }
    }
}
