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

        private static void WinCurrentCustomer(RunController run) =>
            run.Mix(new[] { run.CurrentRound.Rail[0] });

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

        // ── tool behavior on the rail ────────────────────────────────────────────

        [Test]
        public void Muddler_InfusesUpToTwoCards()
        {
            var run = NewRun();
            var targets = run.CurrentRound.Rail.Take(2).ToArray();
            run.CurrentRound.ApplyTool(Muddler, targets);

            Assert.IsTrue(targets.All(c => c.Enhancement == Enhancement.Infused));
        }

        [Test]
        public void IcePick_DestroysCardsForTheRestOfTheRun()
        {
            var run = NewRun();
            run.CurrentRound.ApplyTool(IcePick, run.CurrentRound.Rail.Take(2).ToArray());
            Assert.AreEqual(6, run.CurrentRound.Rail.Count);

            WinCurrentCustomer(run);
            run.ContinueToNextCustomer();

            // 46 cards remain in the run: 8 on the new rail, 38 in the draw pile.
            Assert.AreEqual(8, run.CurrentRound.Rail.Count);
            Assert.AreEqual(38, run.CurrentRound.DeckDrawCount);
        }

        [Test]
        public void BarSpoon_CopyBecomesAPermanentDeckCard()
        {
            var run = NewRun();
            var original = run.CurrentRound.Rail[0];
            run.CurrentRound.ApplyTool(BarSpoon, new[] { original });

            Assert.AreEqual(9, run.CurrentRound.Rail.Count);
            var copy = run.CurrentRound.Rail[1];
            Assert.AreEqual(original.Id, copy.Id);
            Assert.AreNotEqual(original.InstanceId, copy.InstanceId);

            WinCurrentCustomer(run);
            run.ContinueToNextCustomer();
            Assert.AreEqual(41, run.CurrentRound.DeckDrawCount, "49 cards total, 8 dealt");
        }

        [Test]
        public void CitrusPress_RewritesTheIngredientType()
        {
            var run = NewRun();
            var target = run.CurrentRound.Rail[0];
            run.CurrentRound.ApplyTool(CitrusPress, new[] { target });

            Assert.AreEqual(IngredientType.Sour, target.Type);
            Assert.IsNull(run.CurrentRound.PreviewMatch(new[] { target }), "a lone Sour matches no recipe");
        }

        [Test]
        public void Tools_ValidateTargets_AndAreSingleUse()
        {
            var run = NewRun();
            var round = run.CurrentRound;

            Assert.Throws<ArgumentException>(() =>
                round.ApplyTool(Jigger, round.Rail.Take(2).ToArray()), "Jigger targets max 1");
            Assert.Throws<ArgumentException>(() =>
                round.ApplyTool(Muddler, new IngredientCard[0]), "empty selection");
            Assert.Throws<InvalidOperationException>(() =>
                run.UseTool(Muddler, round.Rail.Take(1).ToArray()), "not in inventory");
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
            var target = run.CurrentRound.Rail[0];
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
