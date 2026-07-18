using System;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The emotion model itself (GDD 19 §1–§4): clamping, dominance, charge summation and
    /// the single-rounding rule that keeps exact landings reachable.
    /// </summary>
    public class EmotionStatsTests
    {
        private static EmotionStats Stats(int anger = 0, int sadness = 0, int fatigue = 0,
            int excitement = 0, int heartbreak = 0, int anxiety = 0) =>
            new EmotionStats(new[] { anger, sadness, fatigue, excitement, heartbreak, anxiety });

        [Test]
        public void Values_ClampToZeroAndHundred()
        {
            var stats = Stats(anger: 150, sadness: -20);

            Assert.AreEqual(100, stats[Emotion.Anger]);
            Assert.AreEqual(0, stats[Emotion.Sadness]);
        }

        [Test]
        public void Dominant_IsTheHighestValue()
        {
            Assert.AreEqual(Emotion.Heartbreak, Stats(anger: 40, heartbreak: 70).Dominant);
        }

        [Test]
        public void Dominant_TiesBreakByDeclarationOrder()
        {
            // Anger is declared first, so a tie must resolve to it — the rule is public even
            // though the values are not (GDD 19 §2).
            Assert.AreEqual(Emotion.Anger, Stats(anger: 60, anxiety: 60).Dominant);
        }

        [Test]
        public void Apply_ReturnsTheMovementThatActuallyHappened()
        {
            var stats = Stats(sadness: 8);
            var delta = new EmotionDelta();
            delta.Add(Emotion.Sadness, -20);

            var applied = stats.Apply(delta);

            Assert.AreEqual(0, stats[Emotion.Sadness], "clamped at the floor");
            Assert.AreEqual(-8, applied[Emotion.Sadness], "only 8 units were actually available");
        }

        [Test]
        public void Projected_LeavesTheOriginalAlone()
        {
            var stats = Stats(anger: 50);
            var delta = new EmotionDelta();
            delta.Add(Emotion.Anger, -10);

            var projected = stats.Projected(delta);

            Assert.AreEqual(50, stats[Emotion.Anger]);
            Assert.AreEqual(40, projected[Emotion.Anger]);
        }
    }

    public class EmotionResolverTests
    {
        private static IngredientCard Card(params EmotionCharge[] charges) =>
            new IngredientCard("c", "Card", IngredientType.Spirit, 5,
                QualityTier.HousePour, charges);

        private static RecipeDefinition Recipe(int baseMult) =>
            new RecipeDefinition("r", "Recipe", 1, 10, baseMult, 0, 0, Array.Empty<PatternRequirement>());

        private static RecipeMatch Match(RecipeDefinition recipe, params IngredientCard[] cards) =>
            new RecipeMatch(recipe, cards);

        [Test]
        public void Charges_SumAcrossTheSelection()
        {
            var a = Card(new EmotionCharge(Emotion.Sadness, -6));
            var b = Card(new EmotionCharge(Emotion.Sadness, -4), new EmotionCharge(Emotion.Anger, 3));

            var raw = EmotionResolver.RawCharges(new[] { a, b });

            Assert.AreEqual(-10, raw[Emotion.Sadness]);
            Assert.AreEqual(3, raw[Emotion.Anger]);
        }

        [Test]
        public void ChargeMultiplier_DerivesFromTheRecipesBaseMult()
        {
            Assert.AreEqual(1.0, RecipeDefinition.ChargeMultiplierFor(1), 1e-9);
            Assert.AreEqual(2.0, RecipeDefinition.ChargeMultiplierFor(6), 1e-9);
            Assert.AreEqual(3.0, RecipeDefinition.ChargeMultiplierFor(20), 1e-9, "capped at ×3");
        }

        [Test]
        public void NoRecipe_StillPours_AtHalfStrength()
        {
            var card = Card(new EmotionCharge(Emotion.Sadness, -20));

            var delta = EmotionResolver.Resolve(new[] { card }, null);

            Assert.AreEqual(-10, delta[Emotion.Sadness]);
        }

        [Test]
        public void Rounding_HappensOnceAtTheEnd_SoExactLandingsStayReachable()
        {
            // Two cards at -5 under a ×1.5 recipe: per-card rounding would give -8 (2 × -7.5
            // rounded), a single rounding gives -15. Only the latter can land on 0 from 15.
            var recipe = new RecipeDefinition("r", "R", 1, 10, 1, 0, 0,
                Array.Empty<PatternRequirement>(), chargeMultiplier: 1.5);
            var cards = new[]
            {
                Card(new EmotionCharge(Emotion.Sadness, -5)),
                Card(new EmotionCharge(Emotion.Sadness, -5))
            };

            var delta = EmotionResolver.Resolve(cards, Match(recipe, cards));

            Assert.AreEqual(-15, delta[Emotion.Sadness]);
        }

        [Test]
        public void EmptySelection_ProducesNothing()
        {
            Assert.IsTrue(EmotionResolver.Resolve(Array.Empty<IngredientCard>(), null).IsEmpty);
            Assert.IsTrue(EmotionResolver.Resolve(null, null).IsEmpty);
        }

        [Test]
        public void CardsWithoutCharges_AreInert()
        {
            var plain = new IngredientCard("p", "Plain", IngredientType.Spirit, 5);

            Assert.IsTrue(EmotionResolver.Resolve(new[] { plain }, null).IsEmpty);
        }

        [Test]
        public void Clone_CarriesTheCharges()
        {
            var card = Card(new EmotionCharge(Emotion.Anxiety, 7));

            var clone = card.Clone();

            Assert.AreEqual(1, clone.Charges.Count);
            Assert.AreEqual(new EmotionCharge(Emotion.Anxiety, 7), clone.Charges[0]);
        }

        [Test]
        public void ConvertType_LeavesChargesAlone()
        {
            // Charges are the card's identity, not its type (GDD 19 §4).
            var card = Card(new EmotionCharge(Emotion.Anger, -9));

            card.ConvertType(IngredientType.Sour);

            Assert.AreEqual(-9, card.Charges[0].Amount);
        }
    }
}
