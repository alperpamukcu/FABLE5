using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>Vouchers (GDD 7.4): permanent run upgrades sold in a dedicated shop slot.</summary>
    public class VoucherTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static readonly VoucherDefinition HappyHour = new VoucherDefinition(
            "happy_hour", "Happy Hour", 10, VoucherOp.ExtraRestock, 1);
        private static readonly VoucherDefinition DoubleShift = new VoucherDefinition(
            "double_shift", "Double Shift", 10, VoucherOp.ExtraMix, 1);
        private static readonly VoucherDefinition WiderRail = new VoucherDefinition(
            "wider_rail", "Wider Rail", 10, VoucherOp.ExtraRail, 1);
        private static readonly VoucherDefinition LoyalClientele = new VoucherDefinition(
            "loyal_clientele", "Loyal Clientele", 10, VoucherOp.PatronDiscount, 2);

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunController NewRun(IReadOnlyList<VoucherDefinition> voucherPool,
            int startingMoney = 20, IReadOnlyList<PatronDefinition> patronPool = null)
        {
            var config = new RunConfig(startingMoney: startingMoney, targetProvider: (n, s) => 10);
            return new RunController(SpiritCards(), Recipes, new RunRng("VOUCHER"), null, config,
                patronPool: patronPool, voucherPool: voucherPool);
        }

        private static void WinAndOpenShop(RunController run) =>
            PourTestKit.ServeSomething(run);

        [Test]
        public void DoubleShift_GrantsAnExtraDrink_EveryCustomer()
        {
            var run = NewRun(new[] { DoubleShift });
            WinAndOpenShop(run);
            run.BuyVoucher();
            run.ContinueToNextCustomer();

            Assert.AreEqual(5, run.CurrentRound.DrinksRemaining);
            CollectionAssert.Contains(run.Vouchers.ToList(), DoubleShift);
        }

        // Happy Hour (+1 Restock) and Wider Rail (+1 rail slot) were removed with the deck —
        // see the casualty list in Docs/PLAN_pour_pivot.md. Buying a voucher still costs $10,
        // which is what those two tests were incidentally covering, so that is asserted here.
        [Test]
        public void BuyingAVoucher_CostsTen()
        {
            var run = NewRun(new[] { DoubleShift });
            WinAndOpenShop(run);
            int moneyBefore = run.Money;

            run.BuyVoucher();

            Assert.AreEqual(moneyBefore - 10, run.Money);
        }

        [Test]
        public void LoyalClientele_DiscountsPatrons_FromTheNextShop()
        {
            var patronPool = new[]
            {
                new PatronDefinition("common_a", "Common A", PatronRarity.Common, 4, "",
                    new[] { new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 1) })
            };
            var run = NewRun(new[] { LoyalClientele }, startingMoney: 500, patronPool: patronPool);
            WinAndOpenShop(run);
            run.BuyVoucher();
            run.ContinueToNextCustomer();
            WinAndOpenShop(run);

            for (int i = 0; i < 50; i++)
            {
                var offer = run.Shop.Offers.FirstOrDefault(o => o.Kind == ShopOfferKind.Patron);
                if (offer != null)
                {
                    Assert.AreEqual(2, offer.Price, "cost 4 patron discounted by $2");
                    return;
                }
                run.RerollShop();
            }
            Assert.Fail("no patron offer appeared in 50 rerolls");
        }

        [Test]
        public void OwnedVouchers_AreNeverOfferedAgain()
        {
            var run = NewRun(new[] { WiderRail });
            WinAndOpenShop(run);
            Assert.IsNotNull(run.Shop.VoucherOffer);
            run.BuyVoucher();
            run.ContinueToNextCustomer();
            WinAndOpenShop(run);

            Assert.IsNull(run.Shop.VoucherOffer, "the only voucher is owned; the slot is empty");
        }

        [Test]
        public void BuyVoucher_ValidatesMoneyAndDoubleBuys()
        {
            var run = NewRun(new[] { WiderRail }, startingMoney: 0);
            WinAndOpenShop(run); // wallet: 3 base + 3 unused mixes = $6 < $10
            Assert.Throws<InvalidOperationException>(() => run.BuyVoucher(), "not enough money");

            var funded = NewRun(new[] { WiderRail });
            WinAndOpenShop(funded);
            funded.BuyVoucher();
            Assert.Throws<InvalidOperationException>(() => funded.BuyVoucher(), "already sold");
        }
    }
}
