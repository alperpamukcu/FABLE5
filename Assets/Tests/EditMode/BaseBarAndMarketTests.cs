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
    /// The branded base bar (GDD 22): a small curated shelf where every bottle is knowable,
    /// plus the brand catalogue the end-of-night market sells from. Same philosophy as the
    /// other content suites — pin the shape the design guarantees, not tunable numbers.
    /// </summary>
    public class BaseBarContentTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        private static IReadOnlyList<IngredientCard> All() =>
            DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json")).Cards;

        private static IReadOnlyList<IngredientCard> Starting() =>
            All().Where(c => c.Info == null || c.Info.Tier <= 1).ToList();

        [Test]
        public void TheStartingShelf_IsSmallEnoughToKnowByHeart()
        {
            // The whole point of the base bar: the 46-bottle wall was unreadable.
            var starting = Starting();
            Assert.LessOrEqual(starting.Count, 12);
            Assert.GreaterOrEqual(starting.Count, 8);
        }

        [Test]
        public void EveryBottle_CarriesItsIdentityPapers()
        {
            foreach (var card in All())
            {
                Assert.IsNotNull(card.Info, card.Id);
                Assert.IsNotEmpty(card.Info.Style, card.Id);
                Assert.IsNotEmpty(card.Info.Origin, $"{card.Id} has no origin");
                Assert.IsNotEmpty(card.Info.Blurb, $"{card.Id} has no blurb");
            }
        }

        [Test]
        public void MixersAndGarnishes_CarryNoAlcohol()
        {
            // Tone guardrail bookkeeping: the fill axis must be reachable with zero-ABV
            // volume, so the mixers must actually be zero-ABV.
            foreach (var card in All().Where(c =>
                         c.Type == IngredientType.Bubbly || c.Type == IngredientType.Sour ||
                         c.Type == IngredientType.Garnish ||
                         (c.Type == IngredientType.Sweet && c.Info.Style == "syrup")))
                Assert.AreEqual(0, card.Info.Abv, card.Id);
        }

        [Test]
        public void EveryEmotion_CanBeMovedBothWays_OnTheStartingShelf()
        {
            var charges = Starting().SelectMany(c => c.Charges).ToList();
            foreach (var emotion in Emotions.All)
            {
                Assert.IsTrue(charges.Any(c => c.Emotion == emotion && c.Amount < 0),
                    $"nothing extinguishes {emotion}");
                Assert.IsTrue(charges.Any(c => c.Emotion == emotion && c.Amount > 0),
                    $"nothing fuels {emotion}");
            }
        }

        [Test]
        public void PrimaryCharges_SitInTheirFlavorBand()
        {
            // GDD 19 s4: Flavor 1-3 light (4-8), 4-7 standard (9-15), 8-11 heavy (16-24).
            // Ported from the retired classic-bar suite; the rule outlives the deck.
            foreach (var card in All())
            {
                int primary = card.Charges.Max(c => System.Math.Abs(c.Amount));
                var (low, high) = card.Flavor <= 3 ? (4, 8)
                    : card.Flavor <= 7 ? (9, 15)
                    : (16, 24);
                Assert.GreaterOrEqual(primary, low, $"{card.Id} (Flavor {card.Flavor})");
                Assert.LessOrEqual(primary, high, $"{card.Id} (Flavor {card.Flavor})");
            }
        }

        [Test]
        public void StartingStyles_AreUnique()
        {
            // One bottle per style on the opening shelf, or the market's "replace your vodka"
            // upgrade would be ambiguous about which vodka.
            var styles = Starting().Select(c => c.Info.Style).ToList();
            CollectionAssert.AllItemsAreUnique(styles);
        }

        [Test]
        public void EveryMarketBrand_UpgradesAStyleTheShelfStocks()
        {
            var startingStyles = new HashSet<string>(Starting().Select(c => c.Info.Style));
            foreach (var brand in All().Where(c => c.Info.Tier > 1))
            {
                Assert.IsTrue(startingStyles.Contains(brand.Info.Style),
                    $"{brand.Id} upgrades '{brand.Info.Style}', which nothing stocks");
                Assert.Greater(brand.Info.Price, 0, brand.Id);
            }
        }

        [Test]
        public void TheStartingShelf_CoversTheRecipeTable()
        {
            // The derived ratio bands are by type, so the shelf needs every type present.
            var types = Starting().Select(c => c.Type).Distinct().ToList();
            foreach (IngredientType type in System.Enum.GetValues(typeof(IngredientType)))
                CollectionAssert.Contains(types, type);
        }
    }

    /// <summary>
    /// The end-of-night brand market (GDD 22 §4), now driven by the tycoon day loop: offers
    /// roll when the day closes, and buying swaps a bottle in place, full, so the shelf keeps
    /// its muscle memory.
    /// </summary>
    public class MarketTests
    {
        private static IngredientCard Bottle(string id, string style, int tier, int price = 0,
            int charge = -10) =>
            new IngredientCard(id, id, IngredientType.Spirit, 5, QualityTier.HousePour,
                new[] { new EmotionCharge(Emotion.Anger, charge) },
                new IngredientInfo(style, tier, price, "somewhere", 40, "test"));

        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static TycoonRun NewRun(IReadOnlyList<IngredientCard> catalogue)
        {
            var shelf = new Shelf(new[]
            {
                new ShelfBottle(Bottle("vodka_a", "vodka", 1)),
                new ShelfBottle(Bottle("gin_a", "gin", 1)),
            });
            // Rich enough that no-income test days can still shop (purchases need cash).
            return new TycoonRun(shelf, Recipes, new RunRng("MKT"),
                config: new TycoonConfig(startingMoney: 100), brandCatalogue: catalogue);
        }

        /// <summary>Fast-forwards an unserved day: everyone storms off and the day closes.</summary>
        private static void RunDayToClose(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                if (guard++ > 500) throw new System.Exception("the day never closed");
                run.Tick(60);
            }
        }

        [Test]
        public void TheMarket_FillsWhenTheDayCloses()
        {
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6) });

            Assert.IsEmpty(run.MarketOffers, "no deliveries mid-day");
            RunDayToClose(run);
            Assert.AreEqual(1, run.MarketOffers.Count, "deliveries come at closing time");
        }

        [Test]
        public void BuyingABrand_SwapsTheBottleInPlace_Full()
        {
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6, charge: -14) });
            // Drain some vodka first, so "arrives full" is observable.
            run.PourMeasure("vodka_a", 0.8);
            run.DiscardGlass();
            RunDayToClose(run);

            int money = run.Money;
            int index = run.Shelf.Bottles.ToList().FindIndex(b => b.Id == "vodka_a");
            run.BuyBrand(0);

            Assert.AreEqual(money - 6, run.Money);
            Assert.IsNull(run.Shelf.Find("vodka_a"), "the old brand went back to the distributor");
            var upgraded = run.Shelf.Find("vodka_b");
            Assert.IsNotNull(upgraded);
            Assert.AreEqual(upgraded.Capacity, upgraded.Remaining, 1e-9, "the new brand arrives full");
            Assert.AreEqual(index, run.Shelf.Bottles.ToList().FindIndex(b => b.Id == "vodka_b"),
                "muscle memory: the vodka lives where the vodka lived");
        }

        [Test]
        public void AnOwnedTier_NoLongerAppearsOnTheMarket()
        {
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6) });
            RunDayToClose(run);
            run.BuyBrand(0);
            run.ContinueToNextDay();
            RunDayToClose(run);

            Assert.IsEmpty(run.MarketOffers, "tier 2 is stocked; nothing better exists");
        }
    }

    public class PreparationTests
    {
        [Test]
        public void PreparationsRecord_AndDeduplicate()
        {
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);

            Assert.AreEqual(1, glass.PreparationSteps.Count);
            Assert.IsTrue(glass.HasPreparation("ice"));
        }

        [Test]
        public void ShakenAndStirred_ShareOneSlot()
        {
            // A drink is shaken or stirred, never both; the later choice wins.
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Shaken);
            glass.AddPreparation(Preparations.Stirred);

            Assert.IsFalse(glass.HasPreparation("shaken"));
            Assert.IsTrue(glass.HasPreparation("stirred"));
        }

        [Test]
        public void ClearingTheGlass_ClearsThePreparations()
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", 0.5);
            glass.AddPreparation(Preparations.SaltRim);

            glass.Clear();

            Assert.IsEmpty(glass.PreparationSteps);
        }
    }

    public class LicenceDataTests
    {
        private static ArchetypeDefinition Archetype(params string[] hometowns)
        {
            var bands = Emotions.All.Select(_ => new EmotionBand(40, 60)).ToList();
            return new ArchetypeDefinition("test", "Test", bands, new[] { "Sam" },
                hometowns: hometowns.Length > 0 ? hometowns : null);
        }

        [Test]
        public void EveryRegular_GetsAnAdultAge_AndAHometownFromThePool()
        {
            var registry = new RegularsRegistry(new[] { Archetype("Eastport", "Milltown") }, 0);
            var rng = new RunRng("licence").GetStream("customer");

            for (int i = 0; i < 20; i++)
            {
                var regular = registry.RollNext(rng);
                Assert.GreaterOrEqual(regular.Age, 21, "nobody underage in the bar");
                Assert.Less(regular.Age, 68);
                CollectionAssert.Contains(new[] { "Eastport", "Milltown" }, regular.Hometown);
            }
        }

        [Test]
        public void LicenceDetails_AreSeedDeterministic()
        {
            RegularState Roll(string seed)
            {
                var registry = new RegularsRegistry(new[] { Archetype("Eastport", "Milltown") }, 0);
                return registry.RollNext(new RunRng(seed).GetStream("customer"));
            }

            var a = Roll("PAIR");
            var b = Roll("PAIR");
            Assert.AreEqual(a.Age, b.Age);
            Assert.AreEqual(a.Hometown, b.Hometown);
        }
    }
}
