using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>Booster Packs (GDD 7.1) and the two vouchers that ride on them.</summary>
    public class PackTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static readonly ToolDefinition Muddler = new ToolDefinition(
            "muddler", "Muddler", 3, ToolOp.Enhance, 2, Enhancement.Infused);
        private static readonly VoucherDefinition DeepCellar = new VoucherDefinition(
            "deep_cellar", "Deep Cellar", 10, VoucherOp.PackExtraCard, 1);

        private static PatronDefinition PatronDef(string id, PatronRarity rarity, int cost) =>
            new PatronDefinition(id, id, rarity, cost, "",
                new[] { new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 1) });

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunController NewRun(IReadOnlyList<PatronDefinition> patronPool = null,
            IReadOnlyList<ToolDefinition> toolPool = null,
            IReadOnlyList<VoucherDefinition> voucherPool = null,
            string seed = "PACKS")
        {
            var config = new RunConfig(nights: 50, startingMoney: 5000, targetProvider: (n, s) => 10);
            return new RunController(SpiritCards(), Recipes, new RunRng(seed), null, config,
                patronPool: patronPool, toolPool: toolPool, voucherPool: voucherPool);
        }

        /// <summary>Wins customers until the current Back Room offers the wanted pack, then buys it.</summary>
        private static void BuyPackOfKind(RunController run, PackKind kind)
        {
            for (int i = 0; i < 200; i++)
            {
                if (run.Phase != RunPhase.BackRoom)
                {
                    PourTestKit.ServeSomething(run);
                    continue;
                }
                var offers = run.Shop.PackOffers;
                for (int p = 0; p < offers.Count; p++)
                {
                    if (offers[p].Pack == kind && !offers[p].Sold)
                    {
                        run.BuyPack(p);
                        return;
                    }
                }
                run.ContinueToNextCustomer();
            }
            throw new InvalidOperationException($"{kind} never offered.");
        }

        [Test]
        public void CellarPack_AddsThePickedCard_ToTheDeckForGood()
        {
            var run = NewRun();
            BuyPackOfKind(run, PackKind.Cellar);

            Assert.AreEqual(3, run.OpenPack.Options.Count);
            Assert.IsTrue(run.OpenPack.Options.All(o => o.Kind == PackOptionKind.IngredientCard));
            int shelfBefore = run.Shelf.Count;
            run.PickFromPack(0);

            Assert.IsNull(run.OpenPack);
            run.ContinueToNextCustomer();
            // A pack ingredient joins the shelf as a bottle rather than the deck as a card.
            Assert.AreEqual(shelfBefore + 1, run.Shelf.Count, "the pick is on the shelf");
        }

        [Test]
        public void DeepCellar_AddsAFourthCard()
        {
            var run = NewRun(voucherPool: new[] { DeepCellar });
            PourTestKit.ServeSomething(run);
            run.BuyVoucher();
            BuyPackOfKind(run, PackKind.Cellar);

            Assert.AreEqual(4, run.OpenPack.Options.Count);
            run.SkipPack();
        }

        [Test]
        public void DistillerPack_LevelsThePickedRecipe()
        {
            var run = NewRun();
            BuyPackOfKind(run, PackKind.Distiller);

            Assert.AreEqual(2, run.OpenPack.Options.Count);
            var recipe = run.OpenPack.Options[1].Recipe;
            run.PickFromPack(1);

            Assert.AreEqual(2, run.RecipeLevelOf(recipe.Id));
        }

        [Test]
        public void BarKit_AddsThePickedTool()
        {
            var run = NewRun(toolPool: new[] { Muddler });
            BuyPackOfKind(run, PackKind.BarKit);

            run.PickFromPack(0);
            CollectionAssert.Contains(run.ToolInventory.ToList(), Muddler);
        }

        [Test]
        public void RegularsPack_SeatsThePickedPatron()
        {
            var pool = new[] { PatronDef("a", PatronRarity.Common, 4), PatronDef("b", PatronRarity.Common, 4) };
            var run = NewRun(patronPool: pool);
            BuyPackOfKind(run, PackKind.Regulars);

            Assert.AreEqual(2, run.OpenPack.Options.Count, "choose 1 of 2");
            var picked = run.OpenPack.Options[0].Patron;
            run.PickFromPack(0);

            Assert.IsTrue(run.Patrons.Any(p => p.Definition.Id == picked.Id));
        }

        [Test]
        public void SpeakeasyPack_IsTheOnlyHomeOfLegendaries()
        {
            var pool = new[]
            {
                PatronDef("common_a", PatronRarity.Common, 4),
                PatronDef("legend_a", PatronRarity.Legendary, 20)
            };
            var run = NewRun(patronPool: pool);

            // Legendary weight is 0 in normal card slots.
            for (int i = 0; i < 40; i++)
            {
                if (run.Phase != RunPhase.BackRoom) { PourTestKit.ServeSomething(run); continue; }
                Assert.IsFalse(run.Shop.Offers.Any(
                        o => o.Kind == ShopOfferKind.Patron && o.Patron.Rarity == PatronRarity.Legendary),
                    "legendaries only via special means");
                run.ContinueToNextCustomer();
            }

            BuyPackOfKind(run, PackKind.Speakeasy);
            var patronOption = run.OpenPack.Options.First(o => o.Kind == PackOptionKind.Patron);
            Assert.IsTrue(patronOption.Patron.Rarity >= PatronRarity.Rare, "Speakeasy offers rare+");
            run.SkipPack();
        }

        [Test]
        public void OpenPack_BlocksLeaving_AndSecondPacks()
        {
            var run = NewRun();
            BuyPackOfKind(run, PackKind.Cellar);

            Assert.Throws<InvalidOperationException>(() => run.ContinueToNextCustomer());
            Assert.Throws<InvalidOperationException>(() => run.BuyPack(0));
            run.SkipPack();
            Assert.IsNull(run.OpenPack);
        }

        [Test]
        public void NeonSign_MakesRarerPatronsRollMoreOften()
        {
            var candidates = new List<PatronDefinition>
            {
                PatronDef("common_a", PatronRarity.Common, 4),
                PatronDef("rare_a", PatronRarity.Rare, 8)
            };

            int RareShare(int boost)
            {
                var rng = new SeededRng(99);
                int rare = 0;
                for (int i = 0; i < 1000; i++)
                    if (PatronRoll.Weighted(rng, candidates, boost).Rarity == PatronRarity.Rare) rare++;
                return rare;
            }

            int baseline = RareShare(1);   // expected near 10/70
            int boosted = RareShare(2);    // expected near 20/80
            Assert.Greater(boosted, baseline, "Neon Sign doubles non-common weights");
            Assert.That(baseline, Is.InRange(80, 210));
            Assert.That(boosted, Is.InRange(170, 330));
        }

        [Test]
        public void PatronRoll_ReturnsNull_WhenOnlyLegendariesRemain()
        {
            var onlyLegend = new[] { PatronDef("legend_a", PatronRarity.Legendary, 20) };
            Assert.IsNull(PatronRoll.Weighted(new SeededRng(1), onlyLegend, 1));
        }
    }
}
