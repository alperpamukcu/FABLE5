using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The pour model (GDD 21). Volume is unitless — capacity 1.0 is one full glass — so a
    /// "0.7 pour" is literally 70% of the glass and the ratio maths reads directly.
    /// </summary>
    public class GlassContentsTests
    {
        private static GlassContents Glass(double capacity = 1.0) => new GlassContents(capacity);

        [Test]
        public void AnEmptyGlass_HasNoRatios()
        {
            var glass = Glass();

            Assert.IsTrue(glass.IsEmpty);
            Assert.AreEqual(0, glass.FillFraction);
            Assert.AreEqual(0, glass.RatioOf("gin"), "no division by zero on an empty glass");
        }

        [Test]
        public void DrainingProportionally_KeepsTheRatios()
        {
            var glass = Glass();
            glass.Add("gin", 0.6);
            glass.Add("soda", 0.2);

            double removed = glass.DrainProportional(0.4);   // half the 0.8 glass

            Assert.AreEqual(0.4, removed, 1e-9);
            Assert.AreEqual(0.4, glass.TotalVolume, 1e-9);
            Assert.AreEqual(0.75, glass.RatioOf("gin"), 1e-9, "still 60/20 → 3:1");
            Assert.AreEqual(0.25, glass.RatioOf("soda"), 1e-9);
        }

        [Test]
        public void DrainingEverything_LeavesAnEmptyGlass()
        {
            var glass = Glass();
            glass.Add("gin", 0.5);

            Assert.AreEqual(0.5, glass.DrainProportional(1.0), 1e-9, "you cannot drain more than is there");
            Assert.IsTrue(glass.IsEmpty);
        }

        [Test]
        public void APerfectServePour_MovesTheDrinkWhole()
        {
            var shaker = Glass();
            shaker.Add("gin", 0.6);
            shaker.Add("soda", 0.2);
            var serving = Glass();

            double landed = shaker.TransferInto(serving, shaker.TotalVolume, accuracy: 1.0);

            Assert.AreEqual(0.8, landed, 1e-9);
            Assert.IsTrue(shaker.IsEmpty, "the shaker is spent");
            Assert.AreEqual(0.8, serving.FillFraction, 1e-9);
            Assert.AreEqual(0.75, serving.RatioOf("gin"), 1e-9, "ratios carry across");
        }

        [Test]
        public void ASloppyServePour_SpillsHalfAndUnderfillsTheGlass()
        {
            var shaker = Glass();
            shaker.Add("gin", 0.8);
            var serving = Glass();

            double landed = shaker.TransferInto(serving, 0.8, accuracy: 0.5);

            Assert.AreEqual(0.4, landed, 1e-9, "half missed the rim");
            Assert.IsTrue(shaker.IsEmpty, "the full pour still drained the shaker");
            Assert.AreEqual(0.4, serving.FillFraction, 1e-9, "a thinner drink than you built");
        }

        [Test]
        public void RatiosAreShareOfTheDrink_NotOfTheGlass()
        {
            // Half a glass of 70/30 is still 70/30 — ratio is about the drink, fill is about
            // the glass, and the two axes must not contaminate each other.
            var glass = Glass();
            glass.Add("vodka", 0.35);
            glass.Add("lemon", 0.15);

            Assert.AreEqual(0.5, glass.FillFraction, 1e-9);
            Assert.AreEqual(0.7, glass.RatioOf("vodka"), 1e-9);
            Assert.AreEqual(0.3, glass.RatioOf("lemon"), 1e-9);
        }

        [Test]
        public void TheGlassStopsAtTheBrim()
        {
            // No spills (ruling 2026-07-20): a heavy hand costs precision, not the drink.
            // The glass takes what fits and reports what it took.
            var glass = Glass();
            glass.Add("gin", 0.7);

            Assert.AreEqual(0.3, glass.Add("tonic", 0.6), 1e-9, "only the headroom went in");
            Assert.AreEqual(1.0, glass.FillFraction, 1e-9);
            Assert.AreEqual(0.0, glass.Add("tonic", 0.2), 1e-9, "a full glass takes nothing");
        }

        [Test]
        public void ConsecutivePoursOfOneBottle_MergeIntoOneLayer()
        {
            // Releasing and re-holding the same bottle must not stripe the glass.
            var glass = Glass();
            glass.Add("gin", 0.2);
            glass.Add("gin", 0.2);

            Assert.AreEqual(1, glass.Pours.Count);
            Assert.AreEqual(0.4, glass.Pours[0].Volume, 1e-9);
        }

        [Test]
        public void ReturningToABottle_StartsANewLayer_ButOneTotal()
        {
            var glass = Glass();
            glass.Add("gin", 0.2);
            glass.Add("tonic", 0.2);
            glass.Add("gin", 0.1);

            Assert.AreEqual(3, glass.Pours.Count, "the readout draws the drink being built");
            Assert.AreEqual(0.3, glass.VolumeOf("gin"), 1e-9, "…but the maths sees one gin");
        }

        [Test]
        public void LargerGlassware_MakesTheSamePourALighterDrink()
        {
            var small = Glass(1.0);
            var large = Glass(2.0);
            small.Add("gin", 0.5);
            large.Add("gin", 0.5);

            Assert.AreEqual(0.50, small.FillFraction, 1e-9);
            Assert.AreEqual(0.25, large.FillFraction, 1e-9);
        }
    }

    public class ShelfTests
    {
        private static IngredientCard Card(string id) =>
            new IngredientCard(id, id, IngredientType.Spirit, 5);

        private static Shelf NewShelf(double capacity = 6.0) =>
            new Shelf(new[]
            {
                new ShelfBottle(Card("gin"), capacity),
                new ShelfBottle(Card("tonic"), capacity)
            });

        [Test]
        public void PouringSpendsTheBottle()
        {
            var shelf = NewShelf();
            var glass = new GlassContents(1.0);

            shelf.PourInto(glass, "gin", 0.5);

            Assert.AreEqual(5.5, shelf.Find("gin").Remaining, 1e-9);
            Assert.AreEqual(0.5, glass.VolumeOf("gin"), 1e-9);
        }

        [Test]
        public void RunningDryMidPour_GivesWhatWasLeft_AndIsNotAFailure()
        {
            var shelf = new Shelf(new[] { new ShelfBottle(Card("gin"), capacity: 0.3) });
            var glass = new GlassContents(1.0);

            double poured = shelf.PourInto(glass, "gin", 0.5);

            Assert.AreEqual(0.3, poured, 1e-9, "you get what was left");
            Assert.IsTrue(shelf.Find("gin").IsEmpty);
        }

        [Test]
        public void AFullGlass_DrawsNothingFromTheBottle()
        {
            // The brim cap must not evaporate stock: what the glass cannot take, the
            // bottle keeps.
            var shelf = new Shelf(new[] { new ShelfBottle(Card("gin"), capacity: 6.0) });
            var glass = new GlassContents(1.0);

            shelf.PourInto(glass, "gin", 0.8);
            double second = shelf.PourInto(glass, "gin", 0.5);

            Assert.AreEqual(0.2, second, 1e-9, "only the headroom was drawn");
            Assert.AreEqual(5.0, shelf.Find("gin").Remaining, 1e-9, "the rest stays bottled");
        }

        [Test]
        public void AnEmptyBottle_PoursNothing()
        {
            var shelf = new Shelf(new[] { new ShelfBottle(Card("gin"), capacity: 0.2) });
            var glass = new GlassContents(1.0);
            shelf.PourInto(glass, "gin", 1.0);

            double second = shelf.PourInto(glass, "gin", 0.5);

            Assert.AreEqual(0, second);
            Assert.AreEqual(0.2, glass.TotalVolume, 1e-9);
        }

        [Test]
        public void RefillingRestoresTheShelf()
        {
            var shelf = NewShelf();
            var glass = new GlassContents(1.0);
            shelf.PourInto(glass, "gin", 2.0);

            shelf.RefillAll();

            Assert.AreEqual(6.0, shelf.Find("gin").Remaining, 1e-9);
        }

        [Test]
        public void RefillCost_ChargesOnlyForWhatWasUsed()
        {
            // Two full glasses drain two capacity — the brim cap means one glass can never
            // draw more than one glass's worth in a single pour.
            var shelf = NewShelf();
            shelf.PourInto(new GlassContents(1.0), "gin", 2.0);
            shelf.PourInto(new GlassContents(1.0), "gin", 2.0);

            Assert.AreEqual(4, shelf.RefillCost(pricePerCapacity: 2), "2 capacity used × $2");
        }

        [Test]
        public void UpgradingABottle_RaisesItsCeiling()
        {
            var bottle = new ShelfBottle(Card("gin"), capacity: 6.0, pourRate: 0.5);

            bottle.Upgrade(capacityDelta: 2.0, pourRateDelta: 0.1);

            Assert.AreEqual(2, bottle.Tier);
            Assert.AreEqual(8.0, bottle.Capacity, 1e-9);
            Assert.AreEqual(0.6, bottle.PourRate, 1e-9);
        }

        [Test]
        public void AShelfRejectsDuplicateBottles()
        {
            Assert.Throws<ArgumentException>(() => new Shelf(new[]
            {
                new ShelfBottle(Card("gin")),
                new ShelfBottle(Card("gin"))
            }));
        }
    }

    public class RatioRecipeMatcherTests
    {
        // Real proportions: a Martini is roughly 83% gin to 17% vermouth. The bands have to
        // be authorable *as ratios that sum to 1* — see BandsMustAdmitAValidDrink below for
        // why that is not a detail.
        private static RecipeDefinition Martini() => new RecipeDefinition(
            "martini", "Martini", rank: 10, baseFlavor: 40, baseMult: 4, flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.70, 0.88),
                new RatioRequirement(IngredientType.Sweet, 0.12, 0.30),
            },
            minFill: 0.70);

        private static GlassContents Glass(params (string id, double volume)[] pours)
        {
            var glass = new GlassContents(1.0);
            foreach (var (id, volume) in pours) glass.Add(id, volume);
            return glass;
        }

        // Bands are by type, so the matcher needs to know what each poured id *is*.
        private static readonly Dictionary<string, IngredientCard> Bar = new Dictionary<string, IngredientCard>
        {
            ["gin"] = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6),
            ["vermouth"] = new IngredientCard("vermouth", "Vermouth", IngredientType.Sweet, 4),
            ["bitters"] = new IngredientCard("bitters", "Bitters", IngredientType.Bitter, 3),
            ["cola"] = new IngredientCard("cola", "Cola", IngredientType.Bubbly, 4),
        };

        private static IngredientCard Look(string id) => Bar.TryGetValue(id, out var c) ? c : null;

        private static IReadOnlyList<RecipeDefinition> Book => new[] { Martini() };

        [Test]
        public void APourInsideEveryBand_IsTheDrink()
        {
            // 0.64 + 0.16 fills 80% of the glass at an 80/20 ratio — note the two are not
            // the same number, which is exactly the trap the UI has to keep the player out of.
            var match = RatioRecipeMatcher.Match(Glass(("gin", 0.64), ("vermouth", 0.16)), Book, Look);

            Assert.IsNotNull(match);
            Assert.AreEqual("martini", match.Recipe.Id);
        }

        [Test]
        public void BandEdgesAreInclusive()
        {
            // Exactly 70/30, both ratios sitting on a band edge. A band the player can see
            // must not have invisible slivers cut off its ends.
            var match = RatioRecipeMatcher.Match(Glass(("gin", 0.56), ("vermouth", 0.24)), Book, Look);

            Assert.IsNotNull(match);
        }

        [Test]
        public void TooLittleInTheGlass_IsNotTheDrink()
        {
            // Right proportions, but only a third of a glass — below the recipe's MinFill.
            var match = RatioRecipeMatcher.Match(Glass(("gin", 0.24), ("vermouth", 0.06)), Book, Look);

            Assert.IsNull(match);
        }

        [Test]
        public void ASplashOfSomethingElse_IsTolerated()
        {
            var match = RatioRecipeMatcher.Match(
                Glass(("gin", 0.68), ("vermouth", 0.18), ("bitters", 0.05)), Book, Look);

            Assert.IsNotNull(match, "a 5% stray is a bartender's splash");
        }

        [Test]
        public void TooMuchUnaccountedFor_IsADifferentDrink()
        {
            // Both named ratios sit exactly on a band edge, so only the 18% of cola can
            // reject this — which is the point: the stray tolerance is doing the work.
            var match = RatioRecipeMatcher.Match(
                Glass(("gin", 0.70), ("vermouth", 0.12), ("cola", 0.18)), Book, Look);

            Assert.IsNull(match);
        }

        [Test]
        public void RecipesWhoseRuleIsNotProportional_StayUnpourable()
        {
            // Perfect Serve and Double Perfect list one Spirit slot but really mean "five
            // distinct types" and "…at one Flavor value". Deriving bands from that partial
            // pattern gave them "Spirit 85-100%", so a glass of neat whisky matched Double
            // Perfect — the highest-ranked recipe in the game — for one pour of one bottle.
            var catalog = RecipeCatalog.CreateDefault();

            foreach (var recipe in catalog)
            {
                bool proportional = !recipe.AllDistinctTypes && !recipe.AllEqualFlavor &&
                                    recipe.EqualFlavorGroupSize == 0 &&
                                    recipe.AscendingFlavorGroupSize == 0 &&
                                    recipe.SameTypeGroupMin == 0;
                if (!proportional)
                    CollectionAssert.IsEmpty(recipe.RatioRequirements.ToList(),
                        $"'{recipe.Id}' has a non-proportional rule and must not derive bands");
            }
        }

        [Test]
        public void BandsMustAdmitAValidDrink()
        {
            // Ratios always sum to 1, so bands can be authored that no pour can ever satisfy:
            // gin 55-75% plus vermouth 10-25% is unmatchable with two ingredients, because
            // their ratios must total 1 and the bands only overlap at a single point. This
            // caught a bad Martini during Phase 1 and guards every recipe converted later.
            foreach (var recipe in Book)
            {
                if (recipe.RatioRequirements.Count == 0) continue;

                double minSum = recipe.RatioRequirements.Sum(r => r.MinRatio);
                double maxSum = recipe.RatioRequirements.Sum(r => r.MaxRatio);

                Assert.LessOrEqual(minSum, 1.0 + 1e-9,
                    $"'{recipe.Id}': minimum shares already exceed a full glass");
                Assert.GreaterOrEqual(maxSum + RatioRecipeMatcher.MaxUnnamedShare, 1.0 - 1e-9,
                    $"'{recipe.Id}': maximum shares cannot fill a glass, even with a stray splash");
            }
        }

        [Test]
        public void RecipesWithoutRatios_CannotBeMatchedByPouring()
        {
            // The card-era recipes have no bands yet; they must fail closed rather than
            // matching everything.
            var cardEra = new[] { new RecipeDefinition("neat", "Neat Pour", 1, 5, 1, 0, 0,
                Array.Empty<PatternRequirement>()) };

            Assert.IsNull(RatioRecipeMatcher.Match(Glass(("gin", 1.0)), cardEra));
        }

        // ── top-shelf bands (2026-08-02) ────────────────────────────────────────
        // The author's rule: a quality cocktail cannot be made with cheap spirits.
        // A band may name the lowest brand rung that fills it, and pours from below
        // that rung do not count — the drink is not refused, it simply is not the
        // drink. Tested here rather than through the menu because Core owns it: the
        // sim bot and the tests pour with the same verbs the player does.

        private static IngredientCard Brand(string id, string style, int tier) =>
            new IngredientCard(id, id, IngredientType.Spirit, 6,
                info: new IngredientInfo(style, tier));

        private static readonly Dictionary<string, IngredientCard> TieredBar =
            new Dictionary<string, IngredientCard>
            {
                ["well_gin"] = Brand("well_gin", "gin", 1),
                ["top_gin"] = Brand("top_gin", "gin", 3),
                ["vermouth"] = new IngredientCard("vermouth", "Vermouth", IngredientType.Sweet, 4,
                    info: new IngredientInfo("vermouth")),
            };

        private static IngredientCard LookTiered(string id) =>
            TieredBar.TryGetValue(id, out var c) ? c : null;

        private static IReadOnlyList<RecipeDefinition> TopShelfBook => new[]
        {
            new RecipeDefinition("reserve_martini", "Reserve Martini", 10, 40, 4, 0, 0,
                Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement("gin", 0.70, 0.90, minTier: 3),
                    new RatioRequirement("vermouth", 0.10, 0.30),
                }),
        };

        [Test]
        public void ATopShelfBand_IsFilledByTheGoodBottle()
        {
            var match = RatioRecipeMatcher.Match(
                Glass(("top_gin", 0.80), ("vermouth", 0.20)), TopShelfBook, LookTiered);

            Assert.IsNotNull(match);
            Assert.AreEqual("reserve_martini", match.Recipe.Id);
        }

        [Test]
        public void ATopShelfBand_IsNotFilledByTheWellBottle()
        {
            // Same style, same proportions, cheaper bottle: the gin band sees nothing it
            // may count, so the glass is a martini-shaped drink and no recipe at all.
            Assert.IsNull(RatioRecipeMatcher.Match(
                Glass(("well_gin", 0.80), ("vermouth", 0.20)), TopShelfBook, LookTiered));
        }

        [Test]
        public void CuttingTheGoodBottleWithTheWellOne_BreaksTheDrink()
        {
            // Half and half: only the top-shelf half counts toward the band, which drops
            // it under its minimum — and the well gin becomes a stray the recipe never
            // named. Topping up a Reserve Martini from the cheap bottle is not a shortcut.
            Assert.IsNull(RatioRecipeMatcher.Match(
                Glass(("top_gin", 0.40), ("well_gin", 0.40), ("vermouth", 0.20)),
                TopShelfBook, LookTiered));
        }

        [Test]
        public void OrdinaryBands_StillTakeAnyBottle()
        {
            // The rung is opt-in: a band without one counts every brand of its style,
            // which is every recipe the menu had before this existed.
            var anyGin = new[]
            {
                new RecipeDefinition("house_martini", "House Martini", 9, 20, 2, 0, 0,
                    Array.Empty<PatternRequirement>(),
                    ratioRequirements: new[]
                    {
                        new RatioRequirement("gin", 0.70, 0.90),
                        new RatioRequirement("vermouth", 0.10, 0.30),
                    }),
            };

            Assert.IsNotNull(RatioRecipeMatcher.Match(
                Glass(("well_gin", 0.80), ("vermouth", 0.20)), anyGin, LookTiered));
        }
    }
}
