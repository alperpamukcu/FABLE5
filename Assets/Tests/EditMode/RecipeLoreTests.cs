using System;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The lore file is pinned to the catalogue in BOTH directions (2026-08-24): every
    /// entry must name a real recipe, and every recipe must have its page's bottom half
    /// — so a new drink cannot ship as a page with a blank foot, and a renamed id
    /// cannot leave an orphan line behind.
    /// </summary>
    public class RecipeLoreTests
    {
        [Test]
        public void TheShippedFile_CoversTheCatalogue_ExactlyOnce()
        {
            var text = Resources.Load<TextAsset>("Data/recipes_lore");
            Assert.IsNotNull(text, "Resources/Data/recipes_lore.json is missing");
            var lore = RecipeLore.Parse(text.text);

            foreach (var r in RecipeCatalog.CreateDefault())
                Assert.IsTrue(lore.ContainsKey(r.Id),
                    $"recipe '{r.Id}' has no lore entry — its page prints a blank foot");
            // Parse already refuses ids outside the catalogue and duplicates, so equal
            // counts close the loop: no orphans, no gaps.
            Assert.AreEqual(RecipeCatalog.CreateDefault().Count, lore.Count);
        }

        [Test]
        public void AnEntryOffTheCatalogue_IsRefused()
        {
            Assert.Throws<FormatException>(() => RecipeLore.Parse(
                @"{ ""entries"": [ { ""id"": ""no_such_drink"", ""origin"": ""X"", ""note"": ""Y"" } ] }"));
        }

        [Test]
        public void ADuplicateEntry_IsRefused()
        {
            Assert.Throws<FormatException>(() => RecipeLore.Parse(
                @"{ ""entries"": [
                    { ""id"": ""daiquiri"", ""origin"": ""A"", ""note"": ""B"" },
                    { ""id"": ""daiquiri"", ""origin"": ""C"", ""note"": ""D"" } ] }"));
        }

        [Test]
        public void AnEmptyNote_IsRefused()
        {
            Assert.Throws<FormatException>(() => RecipeLore.Parse(
                @"{ ""entries"": [ { ""id"": ""daiquiri"", ""origin"": ""A"", ""note"": "" "" } ] }"));
        }
    }
}
