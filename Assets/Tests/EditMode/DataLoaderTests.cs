using System;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The data files the tycoon loop still loads: the branded shelf and the recipe table.
    /// (Patron/tool/VIP/voucher parsing retired with the card loop in the demolition; those
    /// JSON files stay as cold storage, unparsed.)
    /// </summary>
    public class DataLoaderTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

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
