using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class BackRoomTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static readonly ToolDefinition Muddler =
            new ToolDefinition("muddler", "Muddler", 3, ToolOp.Enhance, 2, Enhancement.Infused);
        private static readonly ToolDefinition Jigger =
            new ToolDefinition("jigger", "Jigger", 3, ToolOp.Enhance, 1, Enhancement.Overproof);
        private static readonly ToolDefinition IcePick =
            new ToolDefinition("ice_pick", "Ice Pick", 3, ToolOp.Destroy, 2);
        private static readonly ToolDefinition BarSpoon =
            new ToolDefinition("bar_spoon", "Bar Spoon", 3, ToolOp.Copy, 1);
        private static readonly ToolDefinition CitrusPress =
            new ToolDefinition("citrus_press", "Citrus Press", 3, ToolOp.ConvertType, 3, convertTo: IngredientType.Sour);

        private static PatronDefinition PatronDef(string id, PatronRarity rarity, int cost) =>
            new PatronDefinition(id, id, rarity, cost, "",
                new[] { new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 1) });

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunController NewRun(int startingMoney = 4,
            IReadOnlyList<PatronDefinition> patronPool = null,
            IReadOnlyList<ToolDefinition> toolPool = null,
            IEnumerable<PatronInstance> patrons = null,
            string seed = "BACKROOM")
        {
            var config = new RunConfig(startingMoney: startingMoney, targetProvider: (n, s) => 10);
            return new RunController(SpiritCards(), Recipes, new RunRng(seed), patrons, config,
                patronPool, toolPool);
        }

        /// <summary>
        /// Serves until the customer is done. Deliberately the looping version: a single serve
        /// is enough only while every bottle still scores, and the tool tests convert one to a
        /// type that matches no recipe.
        /// </summary>
        private static void WinCurrentCustomer(RunController run) =>
            PourTestKit.WinCurrentCustomer(run);

        // ── enhancements in scoring ──────────────────────────────────────────────

        [Test]
        public void Infused_Adds40Flavor_WhenScored()
        {
            var card = new IngredientCard("s", "Spirit", IngredientType.Spirit, 6);
            card.Enhance(Enhancement.Infused);
            var result = ScoringEngine.Score(RecipeMatcher.Match(new[] { card }, Recipes));
            Assert.AreEqual(51, result.FinalScore); // (5+6+40) x 1
        }

        [Test]
        public void Overproof_Adds4Mult_WhenScored()
        {
            var card = new IngredientCard("s", "Spirit", IngredientType.Spirit, 6);
            card.Enhance(Enhancement.Overproof);
            var result = ScoringEngine.Score(RecipeMatcher.Match(new[] { card }, Recipes));
            Assert.AreEqual(55, result.FinalScore); // (5+6) x (1+4)
        }

        // ── tool behaviour on the shelf ──────────────────────────────────────────
        //
        // Tools used to rework individual rail cards. Under the pour system they rework the
        // bottles themselves (GDD 21 §7.1), which makes them permanent upgrades rather than
        // one-shot card edits. Ice Pick and Bar Spoon had no bottle equivalent and were cut —
        // see the casualty list in Docs/PLAN_pour_pivot.md.

        /// <summary>Puts a tool in the inventory the way the run layer expects it to arrive.</summary>
        private static ToolDefinition Acquire(RunController run, ToolDefinition tool)
        {
            for (int i = 0; i < 200; i++)
            {
                var offers = run.Shop.Offers.ToList();
                var offer = offers.FirstOrDefault(o =>
                    o.Kind == ShopOfferKind.Tool && o.Tool.Id == tool.Id && !o.Sold);
                if (offer != null)
                {
                    run.BuyOffer(offers.IndexOf(offer));
                    return offer.Tool;
                }
                run.RerollShop();
            }
            throw new InvalidOperationException($"'{tool.Id}' never appeared in the shop.");
        }

        private static RunController RunWithTool(ToolDefinition tool, out ToolDefinition acquired)
        {
            var run = NewRun(startingMoney: 3000, toolPool: new[] { tool });
            WinCurrentCustomer(run);
            acquired = Acquire(run, tool);
            run.ContinueToNextCustomer();
            return run;
        }

        [Test]
        public void Muddler_InfusesUpToTwoBottles()
        {
            var run = RunWithTool(Muddler, out var muddler);
            var targets = run.Shelf.Bottles.Take(2).Select(b => b.Ingredient).ToArray();

            run.UseTool(muddler, targets);

            Assert.IsTrue(targets.All(c => c.Enhancement == Enhancement.Infused));
        }

        [Test]
        public void CitrusPress_RewritesTheIngredientType()
        {
            var run = RunWithTool(CitrusPress, out var press);
            var target = run.Shelf.Bottles[0].Ingredient;

            run.UseTool(press, new[] { target });

            Assert.AreEqual(IngredientType.Sour, target.Type);
        }

        [Test]
        public void ABottleToolPersists_AcrossCustomers()
        {
            // The point of moving tools onto the shelf: the change sticks, because the bottle
            // is still there next customer instead of being shuffled away.
            var run = RunWithTool(CitrusPress, out var press);
            var target = run.Shelf.Bottles[0].Ingredient;
            run.UseTool(press, new[] { target });

            WinCurrentCustomer(run);
            run.ContinueToNextCustomer();

            Assert.AreEqual(IngredientType.Sour, run.Shelf.Bottles[0].Ingredient.Type);
        }

        [Test]
        public void Tools_ValidateTargets_AndAreSingleUse()
        {
            var run = RunWithTool(Muddler, out var muddler);
            var twoBottles = run.Shelf.Bottles.Take(2).Select(b => b.Ingredient).ToArray();

            Assert.Throws<ArgumentException>(() => run.UseTool(muddler, new IngredientCard[0]),
                "empty selection");

            run.UseTool(muddler, twoBottles);
            Assert.Throws<InvalidOperationException>(() => run.UseTool(muddler, twoBottles),
                "single use");
        }

        // ── the shop ─────────────────────────────────────────────────────────────

        [Test]
        public void FirstShop_GuaranteesACommonPatronAndABook()
        {
            var pool = new[]
            {
                PatronDef("common_a", PatronRarity.Common, 4),
                PatronDef("rare_a", PatronRarity.Rare, 8)
            };
            var run = NewRun(patronPool: pool, toolPool: new[] { Muddler });
            WinCurrentCustomer(run);

            Assert.IsNotNull(run.Shop);
            Assert.AreEqual(ShopOfferKind.Patron, run.Shop.Offers[0].Kind);
            Assert.AreEqual(PatronRarity.Common, run.Shop.Offers[0].Patron.Rarity);
            Assert.AreEqual(ShopOfferKind.Book, run.Shop.Offers[1].Kind);
        }

        [Test]
        public void BuyingAPatron_MovesMoneyAndFillsASlot()
        {
            var pool = new[] { PatronDef("common_a", PatronRarity.Common, 4) };
            var run = NewRun(startingMoney: 10, patronPool: pool);
            WinCurrentCustomer(run); // money: 10 + 3 base + 3 unused + 2 interest = 18

            int moneyBefore = run.Money;
            run.BuyOffer(0);

            Assert.AreEqual(moneyBefore - 4, run.Money);
            Assert.AreEqual(1, run.Patrons.Count);
            Assert.AreEqual("common_a", run.Patrons[0].Definition.Id);
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(0), "already sold");
        }

        [Test]
        public void BuyingABook_RaisesTheRecipeLevel()
        {
            var run = NewRun(startingMoney: 20);
            WinCurrentCustomer(run);

            var bookOffer = run.Shop.Offers.First(o => o.Kind == ShopOfferKind.Book);
            string recipeId = bookOffer.Recipe.Id;
            Assert.AreEqual(1, run.RecipeLevelOf(recipeId));

            run.BuyOffer(run.Shop.Offers.ToList().IndexOf(bookOffer));
            Assert.AreEqual(2, run.RecipeLevelOf(recipeId));
        }

        [Test]
        public void Reroll_ChargesAnEscalatingFee()
        {
            var run = NewRun(startingMoney: 30);
            WinCurrentCustomer(run);

            int moneyBefore = run.Money;
            Assert.AreEqual(5, run.Shop.RerollCost);
            run.RerollShop();
            Assert.AreEqual(moneyBefore - 5, run.Money);
            Assert.AreEqual(6, run.Shop.RerollCost);
            run.RerollShop();
            Assert.AreEqual(moneyBefore - 11, run.Money);
        }

        [Test]
        public void PatronSlots_AreCappedAtFive()
        {
            var seated = Enumerable.Range(0, 5)
                .Select(i => new PatronInstance(PatronDef($"seated_{i}", PatronRarity.Common, 4)))
                .ToList();
            var pool = new[] { PatronDef("newcomer", PatronRarity.Common, 4) };
            var run = NewRun(startingMoney: 30, patronPool: pool, patrons: seated);
            WinCurrentCustomer(run);

            Assert.AreEqual(ShopOfferKind.Patron, run.Shop.Offers[0].Kind);
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(0));
        }

        [Test]
        public void BuyingBeyondYourMeans_Throws()
        {
            var pool = new[] { PatronDef("rare_a", PatronRarity.Rare, 8) };
            var run = NewRun(startingMoney: 0, patronPool: pool);
            WinCurrentCustomer(run); // money: 0 + 3 + 3 = 6 < 8

            Assert.AreEqual(ShopOfferKind.Patron, run.Shop.Offers[0].Kind);
            Assert.Throws<InvalidOperationException>(() => run.BuyOffer(0));
            Assert.AreEqual(6, run.Money, "failed purchase must not charge");
        }

        [Test]
        public void ToolInventory_IsCappedAtTwo()
        {
            var run = NewRun(startingMoney: 100, toolPool: new[] { Muddler });
            WinCurrentCustomer(run);

            int bought = 0;
            for (int guard = 0; guard < 40; guard++)
            {
                var offer = run.Shop.Offers.FirstOrDefault(o => o.Kind == ShopOfferKind.Tool && !o.Sold);
                if (offer == null)
                {
                    run.RerollShop();
                    continue;
                }

                if (bought < 2)
                {
                    run.BuyOffer(run.Shop.Offers.ToList().IndexOf(offer));
                    bought++;
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(
                        () => run.BuyOffer(run.Shop.Offers.ToList().IndexOf(offer)));
                    Assert.AreEqual(2, run.ToolInventory.Count);
                    return;
                }
            }
            Assert.Fail("Shop never offered enough tools within the guard limit.");
        }

        [Test]
        public void BoughtTool_CanBeUsed_Once()
        {
            var run = NewRun(startingMoney: 100, toolPool: new[] { Muddler });
            WinCurrentCustomer(run);

            for (int guard = 0; guard < 40 && run.ToolInventory.Count == 0; guard++)
            {
                var offer = run.Shop.Offers.FirstOrDefault(o => o.Kind == ShopOfferKind.Tool && !o.Sold);
                if (offer == null) run.RerollShop();
                else run.BuyOffer(run.Shop.Offers.ToList().IndexOf(offer));
            }
            Assert.AreEqual(1, run.ToolInventory.Count, "shop should offer a tool quickly");

            run.ContinueToNextCustomer();
            var tool = run.ToolInventory[0];
            var target = run.CurrentRound.Shelf.Bottles[0].Ingredient;
            run.UseTool(tool, new[] { target });

            Assert.AreEqual(Enhancement.Infused, target.Enhancement);
            Assert.AreEqual(0, run.ToolInventory.Count, "single-use");
            Assert.Throws<InvalidOperationException>(() => run.UseTool(tool, new[] { target }));
        }

        [Test]
        public void SellingAPatron_RefundsHalfRoundedUp()
        {
            var patron = new PatronInstance(PatronDef("seated", PatronRarity.Common, 5));
            var run = NewRun(startingMoney: 0, patrons: new[] { patron });

            run.SellPatron(patron);
            Assert.AreEqual(3, run.Money); // ceil(5/2)
            Assert.AreEqual(0, run.Patrons.Count);
            Assert.Throws<InvalidOperationException>(() => run.SellPatron(patron));
        }

        [Test]
        public void ShopOffers_AreSeedDeterministic()
        {
            var pool = new[]
            {
                PatronDef("common_a", PatronRarity.Common, 4),
                PatronDef("common_b", PatronRarity.Common, 5),
                PatronDef("rare_a", PatronRarity.Rare, 8)
            };

            RunController Build() => NewRun(startingMoney: 30, patronPool: pool,
                toolPool: new[] { Muddler, IcePick }, seed: "SHOP-SEED");

            var a = Build();
            var b = Build();
            WinCurrentCustomer(a);
            WinCurrentCustomer(b);
            a.RerollShop();
            b.RerollShop();

            CollectionAssert.AreEqual(
                a.Shop.Offers.Select(o => o.DisplayName).ToList(),
                b.Shop.Offers.Select(o => o.DisplayName).ToList());
        }
    }
}
