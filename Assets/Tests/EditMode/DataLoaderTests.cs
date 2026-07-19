using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    public class DataLoaderTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        // The classic_bar deck retired with the pour pivot's base bar (GDD 22); its
        // content guarantees live in BaseBarContentTests now.

        [Test]
        public void RecipesJson_MatchesRecipeCatalog()
        {
            var fromJson = DataLoader.ParseRecipes(ReadDataFile("recipes/recipes.json"))
                .OrderBy(r => r.Rank).ToList();
            var fromCatalog = RecipeCatalog.CreateDefault()
                .OrderBy(r => r.Rank).ToList();

            Assert.AreEqual(fromCatalog.Count, fromJson.Count);
            for (int i = 0; i < fromCatalog.Count; i++)
            {
                var expected = fromCatalog[i];
                var actual = fromJson[i];
                Assert.AreEqual(expected.Id, actual.Id);
                Assert.AreEqual(expected.Name, actual.Name, expected.Id);
                Assert.AreEqual(expected.Rank, actual.Rank, expected.Id);
                Assert.AreEqual(expected.BaseFlavor, actual.BaseFlavor, expected.Id);
                Assert.AreEqual(expected.BaseMult, actual.BaseMult, expected.Id);
                Assert.AreEqual(expected.FlavorPerLevel, actual.FlavorPerLevel, expected.Id);
                Assert.AreEqual(expected.MultPerLevel, actual.MultPerLevel, expected.Id);
                Assert.AreEqual(expected.ExactMixSize, actual.ExactMixSize, expected.Id);
                Assert.AreEqual(expected.MinMixSize, actual.MinMixSize, expected.Id);
                Assert.AreEqual(expected.AllDistinctTypes, actual.AllDistinctTypes, expected.Id);
                Assert.AreEqual(expected.AllEqualFlavor, actual.AllEqualFlavor, expected.Id);
                Assert.AreEqual(expected.ScoreAllMixCards, actual.ScoreAllMixCards, expected.Id);
                Assert.AreEqual(expected.EqualFlavorGroupSize, actual.EqualFlavorGroupSize, expected.Id);
                Assert.AreEqual(expected.AscendingFlavorGroupSize, actual.AscendingFlavorGroupSize, expected.Id);
                Assert.AreEqual(expected.SameTypeGroupMin, actual.SameTypeGroupMin, expected.Id);

                Assert.AreEqual(expected.Requirements.Count, actual.Requirements.Count, expected.Id);
                for (int r = 0; r < expected.Requirements.Count; r++)
                {
                    Assert.AreEqual(expected.Requirements[r].Count, actual.Requirements[r].Count, expected.Id);
                    CollectionAssert.AreEqual(
                        expected.Requirements[r].Types.ToList(),
                        actual.Requirements[r].Types.ToList(),
                        expected.Id);
                }
            }
        }

        [Test]
        public void LoadedData_PlaysARound_EndToEnd()
        {
            // Same wiring GameBootstrap does in play mode, minus the MonoBehaviour.
            var loaded = DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(ReadDataFile("recipes/recipes.json"));
            var starting = loaded.Cards.Where(c => c.Info == null || c.Info.Tier <= 1).ToList();
            var shelf = PourTestKit.NewShelf(starting);

            var round = new RoundController(shelf, recipes, new CustomerOrder("Smoke", 1));
            round.PourMeasure(shelf.Bottles[0].Id, 0.5);
            var breakdown = round.Serve();

            Assert.AreEqual(12, shelf.Count, "the whole base bar stands ready");
            Assert.GreaterOrEqual(breakdown.FinalScore, 0);
            Assert.AreEqual(3, round.DrinksRemaining);
        }

        [Test]
        public void PatronsJson_LoadsTheFullSixtyPool()
        {
            var patrons = DataLoader.ParsePatrons(ReadDataFile("patrons/patrons.json"));

            // 60 at GDD M3 + the 4 information patrons the emotion pivot adds (GDD 19 §8).
            Assert.AreEqual(64, patrons.Count);
            CollectionAssert.AllItemsAreUnique(patrons.Select(p => p.Id).ToList());
            CollectionAssert.AllItemsAreUnique(patrons.Select(p => p.Name).ToList());
            Assert.IsTrue(patrons.All(p => p.Effects.Count >= 1));

            var byRarity = patrons.GroupBy(p => p.Rarity).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(25, byRarity[PatronRarity.Common]);
            Assert.AreEqual(22, byRarity[PatronRarity.Uncommon]);
            Assert.AreEqual(13, byRarity[PatronRarity.Rare]);
            Assert.AreEqual(4, byRarity[PatronRarity.Legendary]);

            // Every RecipeIdIn condition must reference a real recipe.
            var recipeIds = new HashSet<string>(RecipeCatalog.CreateDefault().Select(r => r.Id));
            foreach (var patron in patrons)
                foreach (var effect in patron.Effects)
                    foreach (var id in effect.Condition.RecipeIds)
                        Assert.IsTrue(recipeIds.Contains(id), $"{patron.Id} references unknown recipe '{id}'");

            var singer = patrons.Single(p => p.Id == "jazz_singer");
            Assert.AreEqual(PatronRarity.Rare, singer.Rarity);
            CollectionAssert.Contains(singer.Effects[0].Condition.RecipeIds.ToList(), "martini");

            var collector = patrons.Single(p => p.Id == "the_collector");
            Assert.IsTrue(collector.Effects.Any(e => e.Op == EffectOp.Accumulate));
            Assert.IsTrue(collector.Effects.Any(e => e.ValueSource == EffectValueSource.Accumulated));

            var cabbie = patrons.Single(p => p.Id == "night_cabbie");
            Assert.AreEqual(EffectTrigger.OnCustomerEnd, cabbie.Effects[0].Trigger);
            Assert.AreEqual(EffectOp.AddMoney, cabbie.Effects[0].Op);

            // GDD 7.1 price bands per rarity
            foreach (var patron in patrons)
            {
                (int min, int max) band;
                switch (patron.Rarity)
                {
                    case PatronRarity.Common: band = (4, 5); break;
                    case PatronRarity.Uncommon: band = (6, 7); break;
                    case PatronRarity.Rare: band = (8, 9); break;
                    default: band = (20, 20); break;
                }
                Assert.That(patron.Cost, Is.InRange(band.min, band.max), patron.Id);
            }
        }

        [Test]
        public void ToolsJson_LoadsTheFullSixteenPool()
        {
            var tools = DataLoader.ParseTools(ReadDataFile("tools/tools.json"));

            // 15 at M3 + Muddling Stick (GDD 02 v1.1) + Eavesdrop (GDD 19 §8).
            Assert.AreEqual(17, tools.Count);
            var stick = tools.Single(t => t.Id == "muddling_stick");
            Assert.AreEqual(ToolOp.ShiftValue, stick.Op);
            Assert.AreEqual(1, stick.ShiftAmount);
            CollectionAssert.AllItemsAreUnique(tools.Select(t => t.Id).ToList());
            Assert.IsTrue(tools.All(t => t.Cost == 3), "GDD 7.1: Tools cost $3");

            var icePick = tools.Single(t => t.Id == "ice_pick");
            Assert.AreEqual(ToolOp.Destroy, icePick.Op);
            Assert.AreEqual(2, icePick.MaxTargets);

            var muddler = tools.Single(t => t.Id == "muddler");
            Assert.AreEqual(Enhancement.Infused, muddler.Enhancement);

            var press = tools.Single(t => t.Id == "citrus_press");
            Assert.AreEqual(ToolOp.ConvertType, press.Op);
            Assert.AreEqual(IngredientType.Sour, press.ConvertTo);

            // The M3 enhancement tools cover all four late enhancements.
            Assert.AreEqual(Enhancement.Premium, tools.Single(t => t.Id == "coupe_glass").Enhancement);
            Assert.AreEqual(Enhancement.Frozen, tools.Single(t => t.Id == "ice_tray").Enhancement);
            Assert.AreEqual(Enhancement.Doubled, tools.Single(t => t.Id == "double_strainer").Enhancement);
            Assert.AreEqual(Enhancement.Golden, tools.Single(t => t.Id == "gold_rim").Enhancement);

            // The three GDD 7.1 specials.
            Assert.AreEqual(QualityTier.Signature, tools.Single(t => t.Id == "cocktail_umbrella").Quality);
            Assert.AreEqual(ToolOp.CreateLastTool, tools.Single(t => t.Id == "bottle_opener").Op);
            Assert.AreEqual(ToolOp.DoubleMoney, tools.Single(t => t.Id == "tab_ledger").Op);
        }

        [Test]
        public void VipsJson_LoadsTheFullTwentyPool()
        {
            var vips = DataLoader.ParseVips(ReadDataFile("vips/vips.json"));

            // 20 at GDD M3 + the 3 read-rule VIPs the emotion pivot adds (GDD 19 §8).
            Assert.AreEqual(23, vips.Count);
            CollectionAssert.AllItemsAreUnique(vips.Select(v => v.Id).ToList());
            CollectionAssert.AllItemsAreUnique(vips.Select(v => v.Name).ToList());

            var critic = vips.Single(v => v.Id == "critic");
            Assert.IsTrue(critic.FinaleOnly, "The Critic is a Night 8 finisher");
            Assert.IsTrue(critic.Rules.Any(r => r.Kind == VipRuleKind.TargetScale && r.DoubleValue == 1.5));
            Assert.IsTrue(critic.Rules.Any(r => r.Kind == VipRuleKind.DebuffRandomType));

            // GDD 5.5 (v1.1): Night 8 is always The Critic; the gentle pool is exactly
            // Teetotaler, Allergic, Health Inspector, Purist.
            Assert.AreEqual(1, vips.Count(v => v.FinaleOnly), "The Critic is the only finale VIP");
            CollectionAssert.AreEquivalent(
                new[] { "teetotaler", "allergic", "health_inspector", "purist" },
                vips.Where(v => v.Gentle).Select(v => v.Id).ToList());

            var teetotaler = vips.Single(v => v.Id == "teetotaler");
            Assert.AreEqual(VipRuleKind.DebuffType, teetotaler.Rules[0].Kind);
            Assert.AreEqual(IngredientType.Spirit, teetotaler.Rules[0].Type);
        }

        [Test]
        public void VouchersJson_LoadsTheLaunchSeven()
        {
            var vouchers = DataLoader.ParseVouchers(ReadDataFile("vouchers/vouchers.json"));

            Assert.AreEqual(7, vouchers.Count, "GDD 7.4 v1.1: six launch vouchers + Bouncer");
            CollectionAssert.AllItemsAreUnique(vouchers.Select(v => v.Id).ToList());
            Assert.IsTrue(vouchers.All(v => v.Cost == 10), "GDD 7.4: vouchers cost $10");
            Assert.AreEqual(VoucherOp.RarePatronBoost, vouchers.Single(v => v.Id == "neon_sign").Op);
            Assert.AreEqual(VoucherOp.PackExtraCard, vouchers.Single(v => v.Id == "deep_cellar").Op);
            Assert.AreEqual(VoucherOp.RerollVip, vouchers.Single(v => v.Id == "bouncer").Op);

            Assert.AreEqual(VoucherOp.ExtraRestock, vouchers.Single(v => v.Id == "happy_hour").Op);
            Assert.AreEqual(VoucherOp.ExtraMix, vouchers.Single(v => v.Id == "double_shift").Op);
            Assert.AreEqual(VoucherOp.ExtraRail, vouchers.Single(v => v.Id == "wider_rail").Op);
            var loyal = vouchers.Single(v => v.Id == "loyal_clientele");
            Assert.AreEqual(VoucherOp.PatronDiscount, loyal.Op);
            Assert.AreEqual(2, loyal.IntValue);
        }

        [Test]
        public void ParsePatrons_UnknownTrigger_Throws()
        {
            const string json = "{\"version\":1,\"patrons\":[{\"id\":\"bad\",\"name\":\"Bad\",\"rarity\":\"Common\",\"cost\":4,\"description\":\"\",\"effects\":[{\"trigger\":\"OnFullMoon\",\"op\":\"AddMult\",\"value\":1,\"valueSource\":\"Constant\",\"condition\":{\"kind\":\"Always\",\"type\":\"\",\"intValue\":0,\"recipeIds\":[]}}]}]}";
            var ex = Assert.Throws<FormatException>(() => DataLoader.ParsePatrons(json));
            StringAssert.Contains("OnFullMoon", ex.Message);
        }

        [Test]
        public void ParseDeck_UnknownType_Throws()
        {
            const string json = "{\"deckId\":\"x\",\"name\":\"X\",\"cards\":[{\"id\":\"bad\",\"name\":\"Bad\",\"type\":\"Umami\",\"flavor\":3}]}";
            var ex = Assert.Throws<FormatException>(() => DataLoader.ParseDeck(json));
            StringAssert.Contains("Umami", ex.Message);
        }

        [Test]
        public void ParseRecipes_EmptyRequirements_Throws()
        {
            const string json = "{\"version\":1,\"recipes\":[{\"id\":\"broken\",\"name\":\"Broken\",\"rank\":1,\"baseFlavor\":5,\"baseMult\":1,\"flavorPerLevel\":1,\"multPerLevel\":1,\"requirements\":[]}]}";
            Assert.Throws<FormatException>(() => DataLoader.ParseRecipes(json));
        }
    }
}
