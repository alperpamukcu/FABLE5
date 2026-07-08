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

        [Test]
        public void ClassicBar_Has48Cards_WithGddTypeCounts()
        {
            var deck = DataLoader.ParseDeck(ReadDataFile("decks/classic_bar.json"));

            Assert.AreEqual("classic_bar", deck.DeckId);
            Assert.AreEqual(48, deck.Cards.Count);

            var counts = deck.Cards.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(12, counts[IngredientType.Spirit]);
            Assert.AreEqual(8, counts[IngredientType.Sour]);
            Assert.AreEqual(8, counts[IngredientType.Sweet]);
            Assert.AreEqual(6, counts[IngredientType.Bitter]);
            Assert.AreEqual(8, counts[IngredientType.Bubbly]);
            Assert.AreEqual(6, counts[IngredientType.Garnish]);
            Assert.IsTrue(deck.Cards.All(c => c.Flavor >= 1 && c.Flavor <= 11), "starter flavors are 1–11");
        }

        [Test]
        public void ClassicBar_DoublePerfect_IsAchievable()
        {
            // Some flavor value must exist on the Spirit type plus at least 4 other distinct types.
            var deck = DataLoader.ParseDeck(ReadDataFile("decks/classic_bar.json"));

            bool achievable = deck.Cards
                .GroupBy(c => c.Flavor)
                .Any(g =>
                {
                    var types = g.Select(c => c.Type).Distinct().ToList();
                    return types.Contains(IngredientType.Spirit) && types.Count >= 5;
                });

            Assert.IsTrue(achievable);
        }

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
            var loaded = DataLoader.ParseDeck(ReadDataFile("decks/classic_bar.json"));
            var recipes = DataLoader.ParseRecipes(ReadDataFile("recipes/recipes.json"));
            var deck = new Deck(loaded.Cards);
            deck.Shuffle(new RunRng("SMOKE").GetStream("deck"));

            var round = new RoundController(deck, recipes, new CustomerOrder("Smoke", 1));
            var breakdown = round.Mix(new[] { round.Rail[0] });

            Assert.AreEqual(8, round.Config.RailSize);
            Assert.GreaterOrEqual(breakdown.FinalScore, 0);
            Assert.AreEqual(3, round.MixesRemaining);
        }

        [Test]
        public void PatronsJson_LoadsAllTenStarters()
        {
            var patrons = DataLoader.ParsePatrons(ReadDataFile("patrons/patrons.json"));

            Assert.AreEqual(10, patrons.Count);
            CollectionAssert.AllItemsAreUnique(patrons.Select(p => p.Id).ToList());
            Assert.IsTrue(patrons.All(p => p.Effects.Count >= 1));

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
