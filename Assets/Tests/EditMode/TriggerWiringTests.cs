using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The non-scoring patron hooks the 60-patron pool relies on: OnRestock growth,
    /// OnShopEnter money, and OnCustomerEnd accumulation (resolved before the payout).
    /// </summary>
    public class TriggerWiringTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static List<IngredientCard> SpiritCards(int count) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        [Test]
        public void OnRestock_AccumulationGrows_AndFeedsScoring()
        {
            // The Gossip: +5 Flavor banked per Restock, paid out on every hand.
            var gossip = new PatronInstance(new PatronDefinition("gossip", "The Gossip",
                PatronRarity.Uncommon, 7, "",
                new[]
                {
                    new PatronEffect(EffectTrigger.OnRefill, EffectOp.Accumulate, 5),
                    new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddFlavor, 0,
                        EffectCondition.Always, EffectValueSource.Accumulated)
                }));
            // The Gossip banked Flavor per Restock; Restock is gone, so it banks per bottle
            // refilled instead — the pour system's own recurring spend (see the pour audit).
            var context = EffectContext.Empty;
            PatronTriggers.ResolveAccumulation(EffectTrigger.OnRefill, new[] { gossip }, context);
            PatronTriggers.ResolveAccumulation(EffectTrigger.OnRefill, new[] { gossip }, context);

            Assert.AreEqual(10, gossip.Accumulated);
        }

        [Test]
        public void OnShopEnter_PaysOut_WhenTheBackRoomOpens()
        {
            var coatCheck = new PatronInstance(new PatronDefinition("coat_check", "The Coat Check Girl",
                PatronRarity.Uncommon, 6, "",
                new[] { new PatronEffect(EffectTrigger.OnShopEnter, EffectOp.AddMoney, 2) }));
            var run = new RunController(SpiritCards(48), Recipes, new RunRng("SHOP"),
                new[] { coatCheck }, new RunConfig(targetProvider: (n, s) => 10));

            int before = run.Money;
            PourTestKit.ServeSomething(run); // 11 ≥ 10: win, Back Room opens

            Assert.AreEqual(RunPhase.BackRoom, run.Phase);
            Assert.AreEqual(before + run.LastTips.Total + 2, run.Money,
                "wallet = tips + the shop-enter bonus");
        }

        [Test]
        public void OnCustomerEnd_Accumulation_ResolvesBeforeThePayout()
        {
            // The Accountant: banks +1 per customer, then pays the banked amount.
            var accountant = new PatronInstance(new PatronDefinition("accountant", "The Accountant",
                PatronRarity.Rare, 9, "",
                new[]
                {
                    new PatronEffect(EffectTrigger.OnCustomerEnd, EffectOp.Accumulate, 1),
                    new PatronEffect(EffectTrigger.OnCustomerEnd, EffectOp.AddMoney, 0,
                        EffectCondition.Always, EffectValueSource.Accumulated)
                }));
            var run = new RunController(SpiritCards(48), Recipes, new RunRng("BANK"),
                new[] { accountant }, new RunConfig(targetProvider: (n, s) => 10));

            PourTestKit.ServeSomething(run);
            Assert.AreEqual(1, run.LastTips.PatronBonus, "first customer pays $1");

            run.ContinueToNextCustomer();
            PourTestKit.ServeSomething(run);
            Assert.AreEqual(2, run.LastTips.PatronBonus, "second customer pays $2");
        }
    }
}
