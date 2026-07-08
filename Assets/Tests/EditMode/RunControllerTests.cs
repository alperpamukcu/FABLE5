using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class RunControllerTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);

        /// <summary>48 uniquely-id'd Spirits of flavor 6: every 1-card mix is a Neat Pour worth 11.</summary>
        private static List<IngredientCard> SpiritCards() =>
            Enumerable.Range(0, 48)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RunConfig EasyConfig(int nights = 8, int startingMoney = 4) =>
            new RunConfig(nights: nights, startingMoney: startingMoney,
                targetProvider: (night, slot) => 10);

        private static RunController NewRun(RunConfig config = null,
            IEnumerable<PatronInstance> patrons = null, string seed = "RUN-TEST") =>
            new RunController(SpiritCards(), Recipes, new RunRng(seed), patrons,
                config ?? EasyConfig());

        private static void WinCurrentCustomer(RunController run) =>
            run.Mix(new[] { run.CurrentRound.Rail[0] }); // Neat Pour 11 >= target 10

        [Test]
        public void TargetTable_MatchesGddTable()
        {
            Assert.AreEqual(300, TargetTable.GreenStake(1, CustomerSlot.CustomerA));
            Assert.AreEqual(3000, TargetTable.GreenStake(3, CustomerSlot.CustomerB));
            Assert.AreEqual(22000, TargetTable.GreenStake(5, CustomerSlot.Vip));
            Assert.AreEqual(100000, TargetTable.GreenStake(8, CustomerSlot.Vip));
            Assert.Throws<ArgumentOutOfRangeException>(() => TargetTable.GreenStake(0, CustomerSlot.CustomerA));
            Assert.Throws<ArgumentOutOfRangeException>(() => TargetTable.GreenStake(9, CustomerSlot.CustomerA));
        }

        [Test]
        public void Run_StartsAtNight1CustomerA_WithGddTarget()
        {
            var run = new RunController(SpiritCards(), Recipes, new RunRng("SEED"));
            Assert.AreEqual(1, run.Night);
            Assert.AreEqual(CustomerSlot.CustomerA, run.Slot);
            Assert.AreEqual(300, run.CurrentRound.Customer.TargetScore);
            Assert.AreEqual(4, run.Money, "starting money");
            Assert.AreEqual(RunPhase.CustomerRound, run.Phase);
        }

        [Test]
        public void WinningACustomer_PaysItemizedTips()
        {
            var run = NewRun(EasyConfig(startingMoney: 4));
            WinCurrentCustomer(run);

            // base 3 + 3 unused mixes + interest floor(4/5)=0
            Assert.AreEqual(RunPhase.BackRoom, run.Phase);
            Assert.AreEqual(3, run.LastTips.Base);
            Assert.AreEqual(3, run.LastTips.UnusedMixBonus);
            Assert.AreEqual(0, run.LastTips.Interest);
            Assert.AreEqual(0, run.LastTips.VipBonus);
            Assert.AreEqual(10, run.Money);
        }

        [Test]
        public void Interest_IsFlooredPerFiveDollars_AndCapped()
        {
            var modest = NewRun(EasyConfig(startingMoney: 7));
            WinCurrentCustomer(modest);
            Assert.AreEqual(1, modest.LastTips.Interest);

            var banked = NewRun(EasyConfig(startingMoney: 27));
            WinCurrentCustomer(banked);
            Assert.AreEqual(5, banked.LastTips.Interest, "capped at +$5 despite 27/5 = 5.4");
        }

        [Test]
        public void VipWin_GrantsBonus_ThenNightAdvances()
        {
            var run = NewRun();
            WinCurrentCustomer(run);              // A
            run.ContinueToNextCustomer();
            WinCurrentCustomer(run);              // B
            Assert.AreEqual(4, run.LastTips.Base, "Customer B pays $4");
            run.ContinueToNextCustomer();
            Assert.AreEqual(CustomerSlot.Vip, run.Slot);

            WinCurrentCustomer(run);              // VIP
            Assert.AreEqual(5, run.LastTips.Base);
            Assert.AreEqual(5, run.LastTips.VipBonus);

            run.ContinueToNextCustomer();
            Assert.AreEqual(2, run.Night);
            Assert.AreEqual(CustomerSlot.CustomerA, run.Slot);
        }

        [Test]
        public void ClearingTheFinalVip_WinsTheRun()
        {
            var run = NewRun(EasyConfig(nights: 2));
            for (int customer = 0; customer < 5; customer++)
            {
                WinCurrentCustomer(run);
                run.ContinueToNextCustomer();
            }

            WinCurrentCustomer(run); // Night 2 VIP
            Assert.AreEqual(RunPhase.RunWon, run.Phase);
            Assert.Throws<InvalidOperationException>(() => WinCurrentCustomer(run));
        }

        [Test]
        public void FailingAnOrder_LosesTheRun()
        {
            var run = NewRun(new RunConfig(targetProvider: (n, s) => 1e9));
            for (int mix = 0; mix < 4; mix++)
                run.Mix(new[] { run.CurrentRound.Rail[0] });

            Assert.AreEqual(RunPhase.RunLost, run.Phase);
            Assert.Throws<InvalidOperationException>(() => run.ContinueToNextCustomer());
        }

        [Test]
        public void RailLeftovers_ReturnToTheDeck_BetweenCustomers()
        {
            var run = NewRun();
            WinCurrentCustomer(run); // 1 card mixed, 7 left on the rail
            run.ContinueToNextCustomer();

            // Full 48-card cabinet reshuffled, 8 dealt to the fresh rail.
            Assert.AreEqual(8, run.CurrentRound.Rail.Count);
            Assert.AreEqual(40, run.CurrentRound.DeckDrawCount);
            Assert.AreEqual(0, run.CurrentRound.DeckDiscardCount);
        }

        [Test]
        public void PatronMoneyEffects_LandInTheTips()
        {
            var cabbie = new PatronInstance(new PatronDefinition("night_cabbie", "The Night Cabbie",
                PatronRarity.Common, 5, "",
                new[] { new PatronEffect(EffectTrigger.OnCustomerEnd, EffectOp.AddMoney, 2) }));

            var run = NewRun(patrons: new[] { cabbie });
            WinCurrentCustomer(run);

            Assert.AreEqual(2, run.LastTips.PatronBonus);
            Assert.AreEqual(12, run.Money); // 4 + 3 + 3 + 2
        }

        [Test]
        public void ContinueToNextCustomer_RequiresBackRoomPhase()
        {
            var run = NewRun();
            Assert.Throws<InvalidOperationException>(() => run.ContinueToNextCustomer());
        }

        [Test]
        public void SameSeed_DealsIdenticalRuns()
        {
            var a = new RunController(SpiritCards(), Recipes, new RunRng("PAIR"));
            var b = new RunController(SpiritCards(), Recipes, new RunRng("PAIR"));

            CollectionAssert.AreEqual(
                a.CurrentRound.Rail.Select(c => c.Id).ToList(),
                b.CurrentRound.Rail.Select(c => c.Id).ToList());
        }
    }
}
