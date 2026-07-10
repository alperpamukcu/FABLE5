using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>Regular's Favor tags (GDD 5.4 v1.1): grant on skip, queue, auto-consumption.</summary>
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
            IReadOnlyList<ToolDefinition> toolPool = null,
            int startingMoney = 500)
        {
            var config = new RunConfig(nights: 50, startingMoney: startingMoney,
                targetProvider: (n, s) => 10);
            return new RunController(SpiritCards(), Recipes, new RunRng(seed), null, config,
                patronPool: patronPool, toolPool: toolPool);
        }

        /// <summary>Finds a seed whose first skip grants the wanted tag (deterministic).</summary>
        private static RunController RunWithTag(FavorTag wanted,
            IReadOnlyList<PatronDefinition> patronPool = null,
            IReadOnlyList<ToolDefinition> toolPool = null)
        {
            for (int i = 0; i < 120; i++)
            {
                var run = NewRun($"FAVOR-{i}", patronPool, toolPool);
                if (run.SkipCustomerA() == wanted) return run;
            }
            throw new InvalidOperationException($"No seed granted {wanted} in 120 tries.");
        }

        private static void Win(RunController run) => run.Mix(new[] { run.CurrentRound.Rail[0] });

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
        public void Tag_IsSeedDeterministic()
        {
            var pool = new[] { PatronDef("a"), PatronDef("b") };
            var first = NewRun("PAIR", pool, new[] { Muddler });
            var second = NewRun("PAIR", pool, new[] { Muddler });

            Assert.AreEqual(first.SkipCustomerA(), second.SkipCustomerA());
            Assert.AreEqual(first.LastFavorText, second.LastFavorText);
        }

        [Test]
        public void QuickHands_GrantsOneExtraMix_ForTheNextCustomerOnly()
        {
            var run = RunWithTag(FavorTag.QuickHands);
            Assert.AreEqual(5, run.CurrentRound.MixesRemaining, "Customer B has +1 Mix");
            Assert.IsEmpty(run.FavorTags, "consumed on grant of the very next customer");

            Win(run);
            run.ContinueToNextCustomer();
            Assert.AreEqual(4, run.CurrentRound.MixesRemaining, "one-shot");
        }

        [Test]
        public void DoubleTip_DoublesTheNextBaseTip_Once()
        {
            var run = RunWithTag(FavorTag.DoubleTip);

            Win(run); // Customer B: base tip $4 doubled
            Assert.AreEqual(8, run.LastTips.Base);
            Assert.IsEmpty(run.FavorTags);

            run.ContinueToNextCustomer();
            Win(run); // VIP: back to normal
            Assert.AreEqual(5, run.LastTips.Base);
        }

        [Test]
        public void WordOfMouth_SeatsACommonPatron_Immediately()
        {
            var pool = new[] { PatronDef("a"), PatronDef("b") };
            var run = RunWithTag(FavorTag.WordOfMouth, patronPool: pool);

            Assert.AreEqual(1, run.Patrons.Count);
            Assert.AreEqual(PatronRarity.Common, run.Patrons[0].Definition.Rarity);
            Assert.IsEmpty(run.FavorTags, "resolves instantly, never held");
        }

        [Test]
        public void Investor_PaysAfterTheNextVip_Only()
        {
            var run = RunWithTag(FavorTag.Investor);

            Win(run); // Customer B: not a VIP, tag stays
            Assert.AreEqual(0, run.LastTips.FavorBonus);
            CollectionAssert.Contains(run.FavorTags.ToList(), FavorTag.Investor);

            run.ContinueToNextCustomer();
            Win(run); // VIP
            Assert.AreEqual(15, run.LastTips.FavorBonus, "GDD 5.4: +$15 after the next VIP");
            Assert.IsEmpty(run.FavorTags, "one-shot");
        }

        [Test]
        public void LoyalTab_MakesTheNextShopsFirstPatronFree()
        {
            var pool = new[] { PatronDef("a"), PatronDef("b"), PatronDef("c") };
            var run = RunWithTag(FavorTag.LoyalTab, patronPool: pool);

            Win(run); // Back Room opens; the tag is consumed into this shop
            Assert.IsEmpty(run.FavorTags);
            for (int i = 0; i < 50; i++)
            {
                var offer = run.Shop.Offers.FirstOrDefault(o => o.Kind == ShopOfferKind.Patron);
                if (offer != null)
                {
                    Assert.AreEqual(0, offer.Price, "Loyal Tab: the visit's first patron offer is free");
                    return;
                }
                run.RerollShop();
            }
            Assert.Fail("no patron offer appeared in 50 rerolls");
        }

        [Test]
        public void OnTheHouse_MakesTheNextShopsPacksFree()
        {
            var run = RunWithTag(FavorTag.OnTheHouse);
            Win(run);

            Assert.IsTrue(run.Shop.PackOffers.Count > 0);
            Assert.IsTrue(run.Shop.PackOffers.All(p => p.Price == 0), "packs cost $0 this visit");
        }

        [Test]
        public void SpeakeasyKey_ForcesASpeakeasyPack()
        {
            var run = RunWithTag(FavorTag.SpeakeasyKey, toolPool: new[] { Muddler });
            Win(run);

            Assert.IsTrue(run.Shop.PackOffers.Any(p => p.Pack == PackKind.Speakeasy),
                "the next shop is guaranteed to stock a Speakeasy Pack");
        }

        [Test]
        public void TopShelfCellar_UpgradesTheNextCellarPack()
        {
            var run = RunWithTag(FavorTag.TopShelfCellar);

            for (int i = 0; i < 200; i++)
            {
                if (run.Phase != RunPhase.BackRoom) { Win(run); continue; }
                var offers = run.Shop.PackOffers;
                int cellar = -1;
                for (int p = 0; p < offers.Count; p++)
                    if (offers[p].Pack == PackKind.Cellar && !offers[p].Sold) cellar = p;
                if (cellar >= 0)
                {
                    run.BuyPack(cellar);
                    Assert.IsTrue(run.OpenPack.Options.All(
                        o => o.Card.Quality == QualityTier.TopShelf), "all cards Top Shelf");
                    Assert.IsEmpty(run.FavorTags, "consumed by this pack");
                    run.SkipPack();
                    return;
                }
                run.ContinueToNextCustomer();
            }
            Assert.Fail("no Cellar Pack offered in 200 steps");
        }

        [Test]
        public void Tags_NeverDuplicate_WhileHeld()
        {
            // TopShelfCellar survives shops (it only consumes on a Cellar Pack), so a
            // second skip can never grant it again while it is still held.
            for (int i = 0; i < 120; i++)
            {
                var run = NewRun($"DUP-{i}");
                if (run.SkipCustomerA() != FavorTag.TopShelfCellar) continue;

                Win(run); run.ContinueToNextCustomer(); // B done
                Win(run); run.ContinueToNextCustomer(); // VIP done, next Night's A
                var second = run.SkipCustomerA();

                Assert.AreNotEqual(FavorTag.TopShelfCellar, second);
                CollectionAssert.Contains(run.FavorTags.ToList(), FavorTag.TopShelfCellar);
                CollectionAssert.AllItemsAreUnique(run.FavorTags.ToList());
                return;
            }
            Assert.Fail("no seed granted TopShelfCellar first in 120 tries.");
        }
    }
}
