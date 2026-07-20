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
        public void ExactlyFull_IsNotASpill()
        {
            var glass = Glass();
            glass.Add("gin", 1.0);

            Assert.AreEqual(1.0, glass.FillFraction, 1e-9);
            Assert.IsFalse(glass.IsOverflowing, "the boundary belongs to the player");
        }

        [Test]
        public void OnePourPastTheBrim_Spills()
        {
            var glass = Glass();
            glass.Add("gin", 1.0);
            glass.Add("tonic", 0.01);

            Assert.IsTrue(glass.IsOverflowing);
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
        private static IngredientCard Card(string id, params EmotionCharge[] charges) =>
            new IngredientCard(id, id, IngredientType.Spirit, 5, QualityTier.HousePour, charges);

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
            Assert.IsFalse(glass.IsOverflowing);
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
            var shelf = NewShelf();
            var glass = new GlassContents(1.0);
            shelf.PourInto(glass, "gin", 2.0);

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

    public class PourResolverTests
    {
        private static readonly Dictionary<string, IngredientCard> Cards = new Dictionary<string, IngredientCard>
        {
            ["vodka"] = new IngredientCard("vodka", "Vodka", IngredientType.Spirit, 5, QualityTier.HousePour,
                new[] { new EmotionCharge(Emotion.Excitement, 20), new EmotionCharge(Emotion.Fatigue, -10) }),
            ["lemon"] = new IngredientCard("lemon", "Lemon", IngredientType.Sour, 3, QualityTier.HousePour,
                new[] { new EmotionCharge(Emotion.Anxiety, -20) }),
            ["soda"] = new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1, QualityTier.HousePour,
                new EmotionCharge[0]),
        };

        private static IngredientCard Lookup(string id) => Cards.TryGetValue(id, out var c) ? c : null;

        private static GlassContents Glass(params (string id, double volume)[] pours)
        {
            var glass = new GlassContents(1.0);
            foreach (var (id, volume) in pours) glass.Add(id, volume);
            return glass;
        }

        [Test]
        public void AFullGlassOfOneThing_DeliversItsPrintedCharges()
        {
            var delta = PourResolver.RawCharges(Glass(("vodka", 1.0)), Lookup);

            Assert.AreEqual(20, delta[Emotion.Excitement]);
            Assert.AreEqual(-10, delta[Emotion.Fatigue]);
        }

        [Test]
        public void HalfAGlass_DeliversHalf()
        {
            var delta = PourResolver.RawCharges(Glass(("vodka", 0.5)), Lookup);

            Assert.AreEqual(10, delta[Emotion.Excitement]);
            Assert.AreEqual(-5, delta[Emotion.Fatigue]);
        }

        [Test]
        public void TheDesignExample_SeventyThirty()
        {
            // The mix that drove this whole system: energy without the edge.
            var delta = PourResolver.RawCharges(Glass(("vodka", 0.7), ("lemon", 0.3)), Lookup);

            Assert.AreEqual(14, delta[Emotion.Excitement], "0.7 × 20");
            Assert.AreEqual(-7, delta[Emotion.Fatigue], "0.7 × −10");
            Assert.AreEqual(-6, delta[Emotion.Anxiety], "0.3 × −20");
        }

        [Test]
        public void RoundingHappensOnce_SoExactLandingsStayReachable()
        {
            // Two pours that each round to 3 but together make 7: per-pour rounding would
            // give 6 and put a landing on zero out of reach from 7.
            var cards = new Dictionary<string, IngredientCard>
            {
                ["a"] = new IngredientCard("a", "A", IngredientType.Spirit, 5, QualityTier.HousePour,
                    new[] { new EmotionCharge(Emotion.Sadness, -7) }),
                ["b"] = new IngredientCard("b", "B", IngredientType.Sweet, 5, QualityTier.HousePour,
                    new[] { new EmotionCharge(Emotion.Sadness, -7) }),
            };
            var glass = new GlassContents(1.0);
            glass.Add("a", 0.5);
            glass.Add("b", 0.5);

            var delta = PourResolver.RawCharges(glass, id => cards.TryGetValue(id, out var c) ? c : null);

            Assert.AreEqual(-7, delta[Emotion.Sadness]);
        }

        [Test]
        public void AMixerAddsVolumeWithoutEmotion()
        {
            // The tone ruling made mechanical: a tall glass can be mostly soda, and the
            // length axis is reachable without pouring more spirit.
            var glass = Glass(("vodka", 0.2), ("soda", 0.7));
            var delta = PourResolver.RawCharges(glass, Lookup);

            Assert.AreEqual(0.9, glass.FillFraction, 1e-9, "a long drink");
            Assert.AreEqual(4, delta[Emotion.Excitement], "…carrying only 0.2 of a spirit");
        }

        [Test]
        public void ASpilledGlass_ServesWhatStayedInIt()
        {
            // 1.4 glasses poured, but only one glass's worth crosses the counter: the
            // overflow is on the bar, not in the customer. 20 × 1.0 × 0.5 (no recipe).
            var glass = Glass(("vodka", 1.4));

            Assert.AreEqual(10, PourResolver.Resolve(glass, null, Lookup)[Emotion.Excitement]);
        }

        [Test]
        public void NoRecipe_StillPours_AtHalfStrength()
        {
            var delta = PourResolver.Resolve(Glass(("vodka", 1.0)), null, Lookup);

            Assert.AreEqual(10, delta[Emotion.Excitement], "20 × 0.5");
        }

        [Test]
        public void FillBonus_OnlyPaysInsideTheBand()
        {
            var preference = new FillPreference(GlassLength.Long, Emotion.Anxiety, reward: 8);

            var hit = PourResolver.FillBonus(Glass(("soda", 0.8)), preference, IntentDirection.Extinguish);
            var miss = PourResolver.FillBonus(Glass(("soda", 0.5)), preference, IntentDirection.Extinguish);

            Assert.AreEqual(-8, hit[Emotion.Anxiety]);
            Assert.IsTrue(miss.IsEmpty, "missing the length is never a penalty");
        }

        [Test]
        public void FillBands_CoverTheWholeGlassWithoutGaps()
        {
            var shortDrink = new FillPreference(GlassLength.Short, Emotion.Anger);
            var regular = new FillPreference(GlassLength.Regular, Emotion.Anger);
            var longDrink = new FillPreference(GlassLength.Long, Emotion.Anger);

            Assert.IsTrue(shortDrink.IsSatisfiedBy(0.45));
            Assert.IsTrue(regular.IsSatisfiedBy(0.45), "bands touch rather than leaving a dead zone");
            Assert.IsTrue(regular.IsSatisfiedBy(0.75));
            Assert.IsTrue(longDrink.IsSatisfiedBy(0.75));
            Assert.IsTrue(longDrink.IsSatisfiedBy(1.0));
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
        public void ASpilledGlass_MatchesNothing()
        {
            Assert.IsNull(RatioRecipeMatcher.Match(Glass(("gin", 0.9), ("vermouth", 0.3)), Book, Look));
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
    }

    /// <summary>
    /// Volume-weighted scoring (PLAN_pour_pivot Phase 2 audit). Without this, card Flavor,
    /// the five quality tiers and every enhancement become dead content the moment the deck
    /// goes — so these tests guard the decision that kept them alive.
    /// </summary>
    public class WeightedScoringTests
    {
        private static RecipeDefinition Recipe(int baseFlavor = 0, int baseMult = 1) =>
            new RecipeDefinition("r", "R", 1, baseFlavor, baseMult, 0, 0,
                Array.Empty<PatternRequirement>());

        private static IngredientCard Card(string id, int flavor,
            QualityTier quality = QualityTier.HousePour) =>
            new IngredientCard(id, id, IngredientType.Spirit, flavor, quality);

        [Test]
        public void FlavorScalesWithTheShareOfTheDrink()
        {
            var cards = new[] { Card("gin", 10) };
            var match = new RecipeMatch(Recipe(), cards, new[] { 0.7 });

            var breakdown = ScoringEngine.Score(match, 1);

            Assert.AreEqual(7, breakdown.TotalFlavor, 1e-9, "70% of a Flavor-10 pour");
        }

        [Test]
        public void AnUnweightedMatch_ScoresExactlyAsBefore()
        {
            // Card-era matches pass no weights and must be untouched by the pivot.
            var cards = new[] { Card("gin", 10), Card("tonic", 4) };

            var weighted = ScoringEngine.Score(new RecipeMatch(Recipe(), cards), 1);

            Assert.AreEqual(14, weighted.TotalFlavor, 1e-9);
        }

        [Test]
        public void TopShelfAndInfused_ScaleToo_BecauseTheyAreFlavor()
        {
            var cards = new[] { Card("gin", 0, QualityTier.TopShelf) };
            var match = new RecipeMatch(Recipe(), cards, new[] { 0.5 });

            var breakdown = ScoringEngine.Score(match, 1);

            Assert.AreEqual(15, breakdown.TotalFlavor, 1e-9, "half of Top Shelf's +30");
        }

        [Test]
        public void MultEffectsDoNotScale_TheDrinkEitherHasItOrDoesNot()
        {
            var cards = new[] { Card("rye", 0, QualityTier.BarrelAged) };
            var match = new RecipeMatch(Recipe(baseMult: 1), cards, new[] { 0.5 });

            var breakdown = ScoringEngine.Score(match, 1);

            Assert.AreEqual(9, breakdown.TotalMult, 1e-9, "base 1 + a full +8, not +4");
        }

        [Test]
        public void ADropOfEverything_EarnsNoMultipliers()
        {
            // The exploit this guard exists for: pour a splash of every Barrel-Aged and
            // Signature bottle on the shelf and collect every multiplier for no volume.
            var cards = new[]
            {
                Card("a", 0, QualityTier.BarrelAged),
                Card("b", 0, QualityTier.Signature),
                Card("c", 0, QualityTier.BarrelAged),
            };
            var splash = ScoringEngine.MinShareForMultEffects / 2;
            var match = new RecipeMatch(Recipe(baseMult: 1), cards, new[] { splash, splash, splash });

            var breakdown = ScoringEngine.Score(match, 1);

            Assert.AreEqual(1, breakdown.TotalMult, 1e-9, "nothing was really in the drink");
        }

        [Test]
        public void AtExactlyTheMinimumShare_TheMultiplierCounts()
        {
            var cards = new[] { Card("rye", 0, QualityTier.BarrelAged) };
            var match = new RecipeMatch(Recipe(baseMult: 1), cards,
                new[] { ScoringEngine.MinShareForMultEffects });

            Assert.AreEqual(9, ScoringEngine.Score(match, 1).TotalMult, 1e-9,
                "the boundary belongs to the player, as everywhere else");
        }

        [Test]
        public void ABottleReturnedTo_ScoresOnceAtItsTotalShare()
        {
            // Pouring gin, then tonic, then gin again must not score gin twice — that would
            // double its Mult effects for free.
            var glass = new GlassContents(1.0);
            glass.Add("gin", 0.3);
            glass.Add("tonic", 0.4);
            glass.Add("gin", 0.3);

            var lookup = new Dictionary<string, IngredientCard>
            {
                ["gin"] = Card("gin", 10),
                ["tonic"] = Card("tonic", 5),
            };
            var (cards, weights) = RatioRecipeMatcher.ScoredContents(
                glass, id => lookup.TryGetValue(id, out var c) ? c : null);

            Assert.AreEqual(2, cards.Count);
            Assert.AreEqual(0.6, weights[0], 1e-9, "gin's total share, counted once");
            Assert.AreEqual(0.4, weights[1], 1e-9);
        }
    }
}
