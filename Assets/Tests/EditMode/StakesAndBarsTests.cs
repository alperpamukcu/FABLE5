using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>Stakes 1–4 (GDD 5.3, cumulative) and the three launch Bars (GDD 9).</summary>
    public class StakesAndBarsTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static PatronDefinition PatronDef(string id, PatronRarity rarity, int cost) =>
            new PatronDefinition(id, id, rarity, cost, "",
                new[] { new PatronEffect(EffectTrigger.OnHandScored, EffectOp.AddMult, 1) });

        private static readonly RunConfig Flat100 = new RunConfig(targetProvider: (n, s) => 100);

        // ── stakes ───────────────────────────────────────────────────────────────

        [Test]
        public void GreenStake_IsTheBaseline()
        {
            var config = StakeTable.Apply(Flat100, 1);
            Assert.AreEqual(5, config.VipDefeatBonus);
            Assert.AreEqual(3, config.RoundConfig.RestocksPerCustomer);
            Assert.AreEqual(100, config.TargetProvider(1, CustomerSlot.CustomerA));
        }

        [Test]
        public void AmberStake_RemovesTheVipBonus()
        {
            var config = StakeTable.Apply(Flat100, 2);
            Assert.AreEqual(0, config.VipDefeatBonus);
            Assert.AreEqual(3, config.RoundConfig.RestocksPerCustomer, "Silver not active yet");
            Assert.AreEqual(100, config.TargetProvider(1, CustomerSlot.CustomerA), "Copper not active yet");
        }

        [Test]
        public void CopperStake_Scales_EarlyNightTargets_AndStacksAmber()
        {
            var config = StakeTable.Apply(Flat100, 3);
            Assert.AreEqual(125, config.TargetProvider(1, CustomerSlot.CustomerA));
            Assert.AreEqual(125, config.TargetProvider(2, CustomerSlot.Vip));
            Assert.AreEqual(100, config.TargetProvider(3, CustomerSlot.CustomerA), "Night 3+ unscaled");
            Assert.AreEqual(0, config.VipDefeatBonus, "stakes stack cumulatively");
        }

        [Test]
        public void SilverStake_CutsARestock_AndStacksEverything()
        {
            var config = StakeTable.Apply(Flat100, 4);
            Assert.AreEqual(2, config.RoundConfig.RestocksPerCustomer);
            Assert.AreEqual(0, config.VipDefeatBonus);
            Assert.AreEqual(125, config.TargetProvider(1, CustomerSlot.CustomerB));

            var run = new RunController(SpiritCards(), Recipes, new RunRng("STAKE"), config: config);
            Assert.AreEqual(2, run.CurrentRound.RestocksRemaining);
        }

        // ── bars ─────────────────────────────────────────────────────────────────

        [Test]
        public void Speakeasy_SeatsARandomRarePatron_AndCostsMoney()
        {
            var bars = BarCatalog.CreateDefault();
            var pool = new[]
            {
                PatronDef("common_a", PatronRarity.Common, 4),
                PatronDef("rare_a", PatronRarity.Rare, 8),
                PatronDef("rare_b", PatronRarity.Rare, 9)
            };

            RunController NewRun() => new RunController(SpiritCards(), Recipes, new RunRng("BAR"),
                config: new RunConfig(startingMoney: 10, targetProvider: (n, s) => 10),
                patronPool: pool, bar: BarCatalog.Find(bars, "speakeasy"));

            var run = NewRun();
            Assert.AreEqual(8, run.Money, "-$2 starting money");
            Assert.AreEqual(1, run.Patrons.Count);
            Assert.AreEqual(PatronRarity.Rare, run.Patrons[0].Definition.Rarity);
            Assert.AreEqual(run.Patrons[0].Definition.Id, NewRun().Patrons[0].Definition.Id,
                "same seed, same rare");
        }

        [Test]
        public void TikiHut_AddsGarnish_AndPreLevelsTiki()
        {
            var bars = BarCatalog.CreateDefault();
            var run = new RunController(SpiritCards(), Recipes, new RunRng("BAR"),
                config: new RunConfig(targetProvider: (n, s) => 10),
                bar: BarCatalog.Find(bars, "tiki_hut"));

            Assert.AreEqual(44, run.CurrentRound.DeckDrawCount, "52 cards total, 8 dealt");
            Assert.AreEqual(2, run.RecipeLevelOf("tiki"));
            Assert.AreEqual(1, run.RecipeLevelOf("martini"), "only Tiki is pre-leveled");
        }

        [Test]
        public void BarLookup_FallsBackToClassic_AndIgnoresCase()
        {
            var bars = BarCatalog.CreateDefault();
            Assert.AreEqual("classic", BarCatalog.Find(bars, null).Id);
            Assert.AreEqual("classic", BarCatalog.Find(bars, "no_such_bar").Id);
            Assert.AreEqual("tiki_hut", BarCatalog.Find(bars, "TIKI_HUT").Id);
        }
    }
}
