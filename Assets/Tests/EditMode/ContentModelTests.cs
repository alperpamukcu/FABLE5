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
                new IngredientInfo("cola", category: IngredientCategories.Mixer, carbonated: true));

        private static IngredientCard Still(string id, string style, IngredientType type) =>
            new IngredientCard(id, id, type, 5,
                new IngredientInfo(style, category: IngredientCategories.Vodka));

        private static TycoonRun RunWith(params IngredientCard[] cards) =>
            new TycoonRun(new Shelf(cards.Select(c => new ShelfBottle(c, 20)).ToList()),
                RecipeCatalog.CreateDefault(), new RunRng("content-seed"));

        // ── the tin takes everything but the keg (GDD 21 §12, overturned 2026-08-14) ──

        [Test]
        public void CarbonatedEntersTheShakerLikeAnythingElse()
        {
            var run = RunWith(Fizzy(), Still("vodka_t", "vodka", IngredientType.Spirit));

            Assert.DoesNotThrow(() => run.BeginPour("cola_test"),
                "the held pour takes fizz now — the tin is where a drink is built");
            run.EndPour();
            Assert.AreEqual(0.2, run.PourMeasure("cola_test", 0.2), 1e-9,
                "and the measured pour lands it in the tin");
            Assert.AreEqual(0.2, run.Glass.VolumeOf("cola_test"), 1e-9);
            Assert.DoesNotThrow(() => run.BeginPour("vodka_t"),
                "a still bottle pours as it always did");
        }

        /// <summary>
        /// The method is the recipe's to name, and a Built drink is not conscripted: the
        /// author's rule of 2026-08-14 is that fizz is mixed "tarife göre", so a highball
        /// leaves the tin on the strength of being built, not of being shaken.
        /// </summary>
        [Test]
        public void ABuiltDrinkLeavesTheTinWithoutBeingWorked()
        {
            // Styled, not just fizzy: vodka_soda's bands are named per STYLE, so a cola in
            // the soda slot matches nothing (GDD 21 §12's style bands).
            var soda = new IngredientCard("soda_klara", "Klara Soda", IngredientType.Bubbly, 2,
                new IngredientInfo("soda", category: IngredientCategories.Mixer, carbonated: true));
            var run = RunWith(soda, Still("vodka_astra", "vodka", IngredientType.Spirit));
            run.PourMeasure("vodka_astra", 0.4);
            run.PourMeasure("soda_klara", 0.6);

            Assert.AreEqual(PrepMethod.Built, run.TinMethod, "the book calls this a Vodka Soda");
            Assert.IsFalse(run.MixRequired, "and a built drink is never worked");
            Assert.IsTrue(run.CanPourOut);
        }

        /// <summary>
        /// The other half of the same rule, and the one the ladder was missing (2026-08-15):
        /// a page that says Stirred holds the tin until the spoon goes in. Black Russian is
        /// the zero-star page that carries it — two heavy liquids, no fizz, nothing to shake —
        /// so this is the verb a new bar actually meets rather than one it reads about.
        /// </summary>
        [Test]
        public void TheFirstStirredPage_HoldsTheTinUntilTheSpoonGoesIn()
        {
            // The shipped page, not a stand-in — the claim is about the drink a new bar can
            // actually buy. Every page but the openers is `locked` (the run filters those out
            // of the matcher until they are bought at a day's end), so it is poured here
            // through a copy that is open: same rank, same bands, same method.
            var shipped = RecipeCatalog.CreateDefault().Single(r => r.Id == "black_russian");
            Assert.AreEqual(PrepMethod.Stirred, shipped.Prep, "the zero-star page that carries the spoon");

            var open = new RecipeDefinition(shipped.Id, shipped.Name, shipped.Rank,
                shipped.BaseFlavor, shipped.BaseMult, shipped.FlavorPerLevel, shipped.MultPerLevel,
                shipped.Requirements, ratioRequirements: shipped.RatioRequirements,
                minFill: shipped.MinFill, prep: shipped.Prep, glassId: shipped.GlassId);

            var kahlua = new IngredientCard("liqueur_kafa", "Kafa", IngredientType.Sweet, 5,
                new IngredientInfo("coffee_liqueur", category: IngredientCategories.Mixer));
            var vodka = Still("vodka_astra", "vodka", IngredientType.Spirit);
            var run = new TycoonRun(
                new Shelf(new[] { new ShelfBottle(kahlua, 20), new ShelfBottle(vodka, 20) }),
                new[] { open }, new RunRng("stir-seed"));
            run.PourMeasure("vodka_astra", 0.65);
            run.PourMeasure("liqueur_kafa", 0.35);

            Assert.AreEqual(PrepMethod.Stirred, run.TinMethod, "the tin reads as a Black Russian");
            Assert.IsTrue(run.MixRequired, "and a stirred drink is worked before it leaves the tin");
            Assert.IsFalse(run.ShakeBlowsTheTin, "there is no fizz in it — shaking is wrong, not an accident");

            run.Stir(1.0);
            Assert.IsTrue(run.CanPourOut, "once it is stirred the tin lets go");
        }

        /// <summary>
        /// Shaking a tin of fizz bursts it (2026-08-14, the author: "gazlı içecekler
        /// çalkalandığında patlayabilir"). The drink is gone, the goods are written off at
        /// the bin's own rate, and the bar is back where it started — the accident is the
        /// answer, not a refusal.
        /// </summary>
        [Test]
        public void ShakingFizzBurstsTheTinAndCostsTheGoods()
        {
            var soda = new IngredientCard("soda_klara", "Klara Soda", IngredientType.Bubbly, 2,
                new IngredientInfo("soda", category: IngredientCategories.Mixer, carbonated: true));
            var run = RunWith(soda, Still("vodka_astra", "vodka", IngredientType.Spirit));
            run.PourMeasure("vodka_astra", 0.4);
            run.PourMeasure("soda_klara", 0.6);
            int before = run.Money;

            Assert.IsTrue(run.ShakeBlowsTheTin, "a Vodka Soda is BUILT — shaking it is an accident");
            run.Shake(1.0);

            Assert.AreEqual(1, run.Blowouts);
            Assert.IsTrue(run.Glass.IsEmpty, "the tin is empty after it goes off");
            Assert.IsFalse(run.IsShaken, "and nothing was shaken — there was no drink to shake");
            Assert.Less(run.Money, before, "the goods are written off, so it is never the cheap way out");
        }

        /// <summary>
        /// THE BOOK OUTRANKS THE BUBBLES. A Gin Fizz and a Long Island are shaken WITH their
        /// fizz in this book, so the rule may not be "carbonated plus a shake is an
        /// accident" — it has to ask the recipe, or those two become unmakeable. Written
        /// against a book of one so it tests the RULE and not which cocktails are unlocked
        /// this week (49 of the 53 shipped recipes are locked out of the matcher).
        /// </summary>
        [Test]
        public void AShakenRecipeMayHoldItsOwnFizz()
        {
            var fizzy = new RecipeDefinition(
                "test_fizz", "Test Fizz", rank: 2, baseFlavor: 10, baseMult: 2,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: System.Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement("vodka", 0.3, 0.5),
                    new RatioRequirement("soda", 0.5, 0.7),
                },
                prep: PrepMethod.Shaken);
            var soda = new IngredientCard("soda_klara", "Klara Soda", IngredientType.Bubbly, 2,
                new IngredientInfo("soda", category: IngredientCategories.Mixer, carbonated: true));
            var vodka = Still("vodka_astra", "vodka", IngredientType.Spirit);
            var run = new TycoonRun(
                new Shelf(new[] { new ShelfBottle(soda, 20), new ShelfBottle(vodka, 20) }),
                new[] { fizzy }, new RunRng("fizz-seed"));
            run.PourMeasure("vodka_astra", 0.4);
            run.PourMeasure("soda_klara", 0.6);

            Assert.AreEqual(PrepMethod.Shaken, run.TinMethod, "the book calls for a shake");
            Assert.IsFalse(run.ShakeBlowsTheTin, "so the tin holds, fizz and all");
            run.Shake(1.0);
            Assert.AreEqual(0, run.Blowouts);
            Assert.IsTrue(run.IsShaken);
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
        public void NothingTheShopCanSayNamesASealedDrink()
        {
            // The market describes a bottle by what it is poured into and a glass line by
            // what it serves. Both walked the whole CATALOGUE until 2026-08-09, so hovering
            // a bottle printed the names of drinks the SEALED crate two tabs away hides
            // behind a star gate. The rule is Core's now, not the text builder's — these
            // three queries are the only lists the shop can reach.
            var run = RunWith(Still("vodka_t", "vodka", IngredientType.Spirit));
            var sealedNames = run.AllRecipes.Where(r => r.Locked).Select(r => r.Name).ToArray();
            Assert.IsNotEmpty(sealedNames, "there is something to leak in the first place");

            var said = run.MenuStyles()
                .SelectMany(style => run.MenuDrinksUsingStyle(style))
                .Concat(new[] { "rocks", "highball", "martini", "coupe", "pint" }
                    .SelectMany(glass => run.MenuDrinksInGlass(glass)))
                .Select(r => r.Name)
                .ToArray();

            Assert.IsNotEmpty(said, "the shop still has something to say about a bottle");
            CollectionAssert.IsEmpty(said.Intersect(sealedNames).ToArray(),
                "no sealed drink is named by anything the shop can print");
            CollectionAssert.IsEmpty(
                run.MenuStyles().Except(run.MenuRecipes
                    .SelectMany(r => r.RatioRequirements)
                    .Where(b => b.IsStyleBand).Select(b => b.Style)).ToArray(),
                "and the filter offers no style the visible menu never calls for");
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
                new IngredientInfo("gin", category: IngredientCategories.Gin));
            var vodka = Still("vodka_t", "vodka", IngredientType.Spirit);
            var tonic = new IngredientCard("tonic_t", "Tonic", IngredientType.Bubbly, 2,
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

            // THE QUARANTINE IS RETIRED (2026-08-14, second half of the lineup pass). All
            // seventeen moved onto the star ladder — `unlockStars`, the rung of the first
            // page that names them — because a bottle the market cannot see is a bottle the
            // SHOP cannot mention, and the mixer board was drawing "Nothing tonight" on a
            // night when six pages on the recipe board wanted six mixers. Held back is fine;
            // hidden is not. The release-on-purchase machinery stays for story stock.
            Assert.AreEqual(0, deck.LockedCards.Count, "nothing is hidden from the board");
            Assert.IsFalse(deck.Cards.Any(c => deck.LockedCards.Any(l => l.Id == c.Id)),
                "no locked card leaks into the live deck");
            foreach (var card in deck.Cards.Concat(deck.LockedCards))
                Assert.IsTrue(IngredientCategories.IsKnown(card.Info?.Category),
                    $"{card.Id} must name a known aisle");
            Assert.IsTrue(deck.LockedCards.Where(c => c.Info.Style == "cola" || c.Info.Style == "tonic"
                    || c.Info.Style == "energy").All(c => c.Info.Carbonated),
                "the fizzy mixers say so");

            // THE GENERAL RULES, pinned after each broke once (audit 2026-08-11):
            // soda_klara and ginger_kicker sat Bubbly-but-unflagged for weeks, and nine
            // priceless bottles were silently priced by the $8+6/tier fallback — above
            // their own tier-2 upgrades.
            foreach (var card in deck.Cards.Concat(deck.LockedCards))
                if (card.Type == IngredientType.Bubbly)
                    Assert.IsTrue(card.Info.Carbonated,
                        $"{card.Id} is Bubbly — it must say carbonated");
            var opening = new HashSet<string> { "vodka_astra", "gin_boothby", "soda_klara",
                "lemon_fresh", "syrup_house", "beer_kestrel" };
            foreach (var card in deck.Cards.Concat(deck.LockedCards))
                if (!opening.Contains(card.Id))
                    Assert.Greater(card.Info.Price, 0,
                        $"{card.Id} reaches the market — it must name its own price");
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
