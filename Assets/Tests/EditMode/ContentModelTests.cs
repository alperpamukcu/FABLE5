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
    /// The v5 P10 content model (PLAN_service_depth): categories, carbonation, locked stock
    /// and recipes, style-banded matching, glassware and snacks. The load-bearing promise
    /// pinned here is QUARANTINE — new content exists in the data without changing one thing
    /// about the live game until something unlocks it.
    /// </summary>
    public class ContentModelTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        private static IngredientCard Fizzy(string id = "cola_test") =>
            new IngredientCard(id, "Test Cola", IngredientType.Bubbly, 2,
                QualityTier.HousePour,
                new IngredientInfo("cola", category: IngredientCategories.Mixer, carbonated: true));

        private static IngredientCard Still(string id, string style, IngredientType type) =>
            new IngredientCard(id, id, type, 5,
                QualityTier.HousePour,
                new IngredientInfo(style, category: IngredientCategories.Vodka));

        private static TycoonRun RunWith(params IngredientCard[] cards) =>
            new TycoonRun(new Shelf(cards.Select(c => new ShelfBottle(c, 20)).ToList()),
                RecipeCatalog.CreateDefault(), new RunRng("content-seed"));

        // ── carbonated is built, not shaken (GDD 21 §12) ────────────────────────

        [Test]
        public void CarbonatedNeverEntersTheShaker()
        {
            var run = RunWith(Fizzy(), Still("vodka_t", "vodka", IngredientType.Spirit));

            Assert.Throws<ArgumentException>(() => run.BeginPour("cola_test"),
                "the held pour must refuse fizz");
            Assert.Throws<ArgumentException>(() => run.PourMeasure("cola_test", 0.2),
                "and so must the measured pour");
            Assert.DoesNotThrow(() => run.BeginPour("vodka_t"),
                "a still bottle pours as it always did");
        }

        [Test]
        public void CarbonatedGoesStraightIntoTheServingGlass()
        {
            var run = RunWith(Fizzy());
            double before = run.Shelf.Find("cola_test").Remaining;

            double poured = run.PourAtGlass("cola_test", 0.4);

            Assert.AreEqual(0.4, poured, 1e-9);
            Assert.AreEqual(0.4, run.ServingGlass.VolumeOf("cola_test"), 1e-9);
            Assert.AreEqual(before - 0.4, run.Shelf.Find("cola_test").Remaining, 1e-9,
                "the pour draws real stock");
        }

        [Test]
        public void PourAtGlass_StopsAtTheBrim_AndWastesNoStock()
        {
            var run = RunWith(Fizzy());
            double before = run.Shelf.Find("cola_test").Remaining;

            run.PourAtGlass("cola_test", 5.0);   // far more than the glass holds

            Assert.IsTrue(run.ServingGlass.IsFull, "the glass fills to the brim and no further");
            double drawn = before - run.Shelf.Find("cola_test").Remaining;
            Assert.AreEqual(run.ServingGlass.TotalVolume, drawn, 1e-9,
                "every drop that left the keg is in the glass — nothing evaporated");
        }

        // ── locked recipes are quarantined ──────────────────────────────────────

        [Test]
        public void TheMenuHidesLockedRecipes_TheCatalogueKeepsThem()
        {
            var run = RunWith(Still("vodka_t", "vodka", IngredientType.Spirit));

            Assert.IsTrue(run.AllRecipes.Any(r => r.Locked), "the catalogue carries the starters");
            Assert.IsFalse(run.MenuRecipes.Any(r => r.Locked), "the menu offers none of them");
        }

        [Test]
        public void TheDayOneMenu_NamesAVodkaSoda_AndRefusesAGinSoda()
        {
            // The v5 P16 redesign: the abstract cousins (Spritz et al.) are gone, so a
            // vodka-and-soda IS a Vodka Soda from day one — and pouring gin at the same
            // proportions is NOT one, which is the whole point of style bands.
            var vodka = Still("vodka_t", "vodka", IngredientType.Spirit);
            var gin = Still("gin_t", "gin", IngredientType.Spirit);
            var soda = new IngredientCard("soda_t", "Soda", IngredientType.Bubbly, 1,
                QualityTier.HousePour,
                new IngredientInfo("soda", category: IngredientCategories.Mixer));
            var lookup = new System.Collections.Generic.Dictionary<string, IngredientCard>
                { ["vodka_t"] = vodka, ["gin_t"] = gin, ["soda_t"] = soda };

            var active = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();

            var glass = new GlassContents(1.0);
            glass.Add("vodka_t", 0.40);
            glass.Add("soda_t", 0.55);
            Assert.AreEqual("vodka_soda",
                RatioRecipeMatcher.Match(glass, active, id => lookup[id])?.Recipe.Id,
                "vodka and soda on the open menu is a Vodka Soda, by name");

            var wrongSpirit = new GlassContents(1.0);
            wrongSpirit.Add("gin_t", 0.40);
            wrongSpirit.Add("soda_t", 0.55);
            Assert.IsNull(RatioRecipeMatcher.Match(wrongSpirit, active, id => lookup[id]),
                "gin at the same proportions is a different drink — no abstract cousin catches it");
        }

        // ── style bands ─────────────────────────────────────────────────────────

        [Test]
        public void StyleBands_TellAGinAndTonicFromAVodkaTonic()
        {
            var ginTonic = RecipeCatalog.CreateDefault().Single(r => r.Id == "gin_tonic");
            var gin = new IngredientCard("gin_t", "Gin", IngredientType.Spirit, 6,
                QualityTier.HousePour, new IngredientInfo("gin", category: IngredientCategories.Gin));
            var vodka = Still("vodka_t", "vodka", IngredientType.Spirit);
            var tonic = new IngredientCard("tonic_t", "Tonic", IngredientType.Bubbly, 2,
                QualityTier.HousePour,
                new IngredientInfo("tonic", category: IngredientCategories.Mixer, carbonated: true));
            var lookup = new System.Collections.Generic.Dictionary<string, IngredientCard>
                { ["gin_t"] = gin, ["vodka_t"] = vodka, ["tonic_t"] = tonic };

            var real = new GlassContents(1.0);
            real.Add("gin_t", 0.40);
            real.Add("tonic_t", 0.60);
            Assert.AreEqual("gin_tonic",
                RatioRecipeMatcher.Match(real, new[] { ginTonic }, id => lookup[id])?.Recipe.Id,
                "gin and tonic at the book ratio is a Gin & Tonic");

            var fake = new GlassContents(1.0);
            fake.Add("vodka_t", 0.40);
            fake.Add("tonic_t", 0.60);
            Assert.IsNull(RatioRecipeMatcher.Match(fake, new[] { ginTonic }, id => lookup[id]),
                "vodka at the same proportions is a different drink — the type system could not say so");
        }

        [Test]
        public void ARecipeMayNotMixStyleBandsWithTypeBands()
        {
            Assert.Throws<ArgumentException>(() => new RecipeDefinition(
                "bad", "Bad", 1, 5, 1, 10, 1,
                new[] { new PatternRequirement(1, IngredientType.Spirit) },
                ratioRequirements: new[]
                {
                    new RatioRequirement("gin", 0.4, 0.6),
                    new RatioRequirement(IngredientType.Bubbly, 0.4, 0.6),
                }));
        }

        [Test]
        public void EveryStarterCocktail_AdmitsAValidDrink()
        {
            foreach (var recipe in RecipeCatalog.CreateDefault().Where(r => r.Locked))
            {
                double minSum = recipe.RatioRequirements.Sum(b => b.MinRatio);
                double maxSum = recipe.RatioRequirements.Sum(b => b.MaxRatio);
                Assert.LessOrEqual(minSum, 1.0 + 1e-9, recipe.Id);
                Assert.GreaterOrEqual(maxSum, 1.0 - RatioRecipeMatcher.MaxUnnamedShare - 1e-9,
                    $"{recipe.Id}: bands so low that even a full glass leaves too much unnamed");
                Assert.IsTrue(recipe.RatioRequirements.All(b => b.IsStyleBand),
                    $"{recipe.Id}: starters are style-banded by design");
            }
        }

        // ── the data files ──────────────────────────────────────────────────────

        [Test]
        public void BaseBar_QuarantinesLockedBottles_AndAislesEveryone()
        {
            var deck = DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json"));

            // P10's seven, plus the P16 wave's four (cranberry, coffee liqueur, pineapple,
            // grenadine) — every one released by the recipe that needs it.
            Assert.AreEqual(11, deck.LockedCards.Count, "the quarantined cocktail ingredients");
            Assert.IsFalse(deck.Cards.Any(c => deck.LockedCards.Any(l => l.Id == c.Id)),
                "no locked card leaks into the live deck");
            foreach (var card in deck.Cards.Concat(deck.LockedCards))
                Assert.IsTrue(IngredientCategories.IsKnown(card.Info?.Category),
                    $"{card.Id} must name a known aisle");
            Assert.IsTrue(deck.LockedCards.Where(c => c.Info.Style == "cola" || c.Info.Style == "tonic"
                    || c.Info.Style == "energy").All(c => c.Info.Carbonated),
                "the fizzy mixers say so");
        }

        [Test]
        public void GlasswareFile_ParsesAndValidates()
        {
            var glasses = DataLoader.ParseGlassware(ReadDataFile("glassware/glassware.json"));
            Assert.AreEqual(5, glasses.Count);
            Assert.IsTrue(glasses.Any(g => g.Id == "pint"), "the tap's glass is in the set");

            const string bad = "{\"version\":1,\"glasses\":[{\"id\":\"x\",\"name\":\"X\",\"spriteKey\":\"x\",\"profile\":[0.5,1.4],\"tierPrices\":[10,20]}]}";
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(bad),
                "a profile value over 1 is a silhouette wider than the glass");
        }

        [Test]
        public void SnacksFile_ParsesAndValidates()
        {
            var snacks = DataLoader.ParseSnacks(ReadDataFile("snacks/snacks.json"));
            Assert.AreEqual(4, snacks.Count);

            const string bad = "{\"version\":1,\"snacks\":[{\"id\":\"x\",\"name\":\"X\",\"price\":0,\"stock\":5}]}";
            Assert.Throws<FormatException>(() => DataLoader.ParseSnacks(bad));
        }

        [Test]
        public void UnknownCategory_FailsAtLoad()
        {
            const string json = "{\"deckId\":\"x\",\"name\":\"X\",\"cards\":[{\"id\":\"b\",\"name\":\"B\",\"type\":\"Spirit\",\"flavor\":3,\"style\":\"vodka\",\"tier\":1,\"category\":\"aisle_nine\"}]}";
            var ex = Assert.Throws<FormatException>(() => DataLoader.ParseDeck(json));
            StringAssert.Contains("aisle_nine", ex.Message);
        }
    }
}
