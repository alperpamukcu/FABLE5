using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>The three GDD 7.1 special tools: Cocktail Umbrella, Tab Ledger, Bottle Opener.</summary>
    public class ToolOpsTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static readonly ToolDefinition Umbrella = new ToolDefinition(
            "cocktail_umbrella", "Cocktail Umbrella", 3, ToolOp.SetQuality, 1, quality: QualityTier.Signature);
        private static readonly ToolDefinition Muddler = new ToolDefinition(
            "muddler", "Muddler", 3, ToolOp.Enhance, 2, Enhancement.Infused);
        private static readonly ToolDefinition Opener = new ToolDefinition(
            "bottle_opener", "Bottle Opener", 3, ToolOp.CreateLastTool, 1);
        private static readonly ToolDefinition Ledger = new ToolDefinition(
            "tab_ledger", "Tab Ledger", 3, ToolOp.DoubleMoney, 1);

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunController NewRun(IReadOnlyList<ToolDefinition> toolPool, int startingMoney)
        {
            var config = new RunConfig(startingMoney: startingMoney, targetProvider: (n, s) => 10);
            return new RunController(SpiritCards(), Recipes, new RunRng("TOOLS"), null, config,
                patronPool: null, toolPool: toolPool);
        }

        /// <summary>Rerolls the current Back Room until the tool shows up, then buys it.</summary>
        private static ToolDefinition Acquire(RunController run, string toolId)
        {
            for (int i = 0; i < 100; i++)
            {
                var offers = run.Shop.Offers.ToList();
                var offer = offers.FirstOrDefault(o =>
                    o.Kind == ShopOfferKind.Tool && o.Tool.Id == toolId && !o.Sold);
                if (offer != null)
                {
                    run.BuyOffer(offers.IndexOf(offer));
                    return offer.Tool;
                }
                run.RerollShop();
            }
            throw new InvalidOperationException($"'{toolId}' never appeared in 100 rerolls.");
        }

        [Test]
        public void CocktailUmbrella_MakesACardSignature()
        {
            var round = new RoundController(new Deck(SpiritCards(9)), Recipes,
                new CustomerOrder("T", 100000));
            var card = round.Rail[0];
            round.ApplyTool(Umbrella, new[] { card });

            Assert.AreEqual(QualityTier.Signature, card.Quality);
            var breakdown = round.Mix(new[] { card });
            Assert.AreEqual((5 + 6) * 1.5, breakdown.FinalScore, 0.001, "Neat Pour x Signature");
        }

        [Test]
        public void RunLevelTools_RejectRailApplication()
        {
            var round = new RoundController(new Deck(SpiritCards(9)), Recipes,
                new CustomerOrder("T", 100000));
            Assert.Throws<InvalidOperationException>(() =>
                round.ApplyTool(Ledger, new[] { round.Rail[0] }));
        }

        [Test]
        public void TabLedger_DoublesMoney_CappedAt20()
        {
            var run = NewRun(new[] { Ledger }, startingMoney: 3000);
            run.Mix(new[] { run.CurrentRound.Rail[0] }); // open the first Back Room
            var ledger = Acquire(run, "tab_ledger");
            run.ContinueToNextCustomer();

            int before = run.Money;
            run.UseTool(ledger, null);

            Assert.AreEqual(before + 20, run.Money, "wallet is way over the cap: +$20 exactly");
            Assert.IsEmpty(run.ToolInventory, "single-use");
        }

        [Test]
        public void BottleOpener_RecreatesTheLastUsedTool()
        {
            var run = NewRun(new[] { Muddler, Opener }, startingMoney: 3000);
            run.Mix(new[] { run.CurrentRound.Rail[0] });
            var muddler = Acquire(run, "muddler");
            var opener = Acquire(run, "bottle_opener");
            run.ContinueToNextCustomer();

            run.UseTool(muddler, run.CurrentRound.Rail.Take(1).ToList());
            run.UseTool(opener, null);

            Assert.AreEqual(1, run.ToolInventory.Count);
            Assert.AreSame(muddler, run.ToolInventory[0], "the opener rebuilt the Muddler");
        }

        [Test]
        public void BottleOpener_ThrowsWithoutToolHistory()
        {
            var run = NewRun(new[] { Opener }, startingMoney: 3000);
            run.Mix(new[] { run.CurrentRound.Rail[0] });
            var opener = Acquire(run, "bottle_opener");
            run.ContinueToNextCustomer();

            Assert.Throws<InvalidOperationException>(() => run.UseTool(opener, null));
            CollectionAssert.Contains(run.ToolInventory.ToList(), opener, "a failed use is not consumed");
        }
    }
}
