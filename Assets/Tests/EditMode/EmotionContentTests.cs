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
    /// Guards the emotion content files (GDD 19 §4/§9). These are balance surfaces, so the
    /// tests check the *shape* the design guarantees — bands, coverage, reachability —
    /// rather than pinning individual numbers that are meant to be tuned.
    /// </summary>
    public class EmotionContentTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        private static IReadOnlyList<IngredientCard> Deck() =>
            DataLoader.ParseDeck(ReadDataFile("decks/classic_bar.json")).Cards;

        private static IReadOnlyList<ArchetypeDefinition> Archetypes() =>
            DataLoader.ParseArchetypes(ReadDataFile("customers/archetypes.json"));

        // ---- card charges ------------------------------------------------------------

        [Test]
        public void EveryCard_CarriesAtLeastOneCharge()
        {
            var silent = Deck().Where(c => c.Charges.Count == 0).Select(c => c.Id).Distinct().ToList();

            CollectionAssert.IsEmpty(silent, "every ingredient must say something");
        }

        [Test]
        public void PrimaryCharges_SitInTheirFlavorBand()
        {
            // GDD 19 §4: Flavor 1–3 light (4–8), 4–7 standard (9–15), 8–11 heavy (16–24).
            foreach (var card in Deck())
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
        public void EveryEmotion_CanBeMovedBothWays()
        {
            // A stat nobody can push is a stat the player can never be asked about.
            var deck = Deck();
            foreach (var emotion in Emotions.All)
            {
                var charges = deck.SelectMany(c => c.Charges).Where(c => c.Emotion == emotion).ToList();
                Assert.IsTrue(charges.Any(c => c.Amount < 0), $"nothing extinguishes {emotion}");
                Assert.IsTrue(charges.Any(c => c.Amount > 0), $"nothing fuels {emotion}");
            }
        }

        [Test]
        public void EveryEmotion_HasAFineAdjustment()
        {
            // Exact landings on 0/100 need small charges to exist, or Clean Serve is luck.
            foreach (var emotion in Emotions.All)
            {
                bool fine = Deck().SelectMany(c => c.Charges)
                    .Any(c => c.Emotion == emotion && System.Math.Abs(c.Amount) <= 10);
                Assert.IsTrue(fine, $"{emotion} has no charge small enough to land precisely");
            }
        }

        [Test]
        public void Garnishes_AreSingleChargePrecisionTools()
        {
            foreach (var card in Deck().Where(c => c.Type == IngredientType.Garnish))
                Assert.AreEqual(1, card.Charges.Count,
                    $"{card.Id} should carry exactly one charge so it can fine-tune");
        }

        [Test]
        public void NoCard_ChargesTheSameEmotionTwice()
        {
            foreach (var card in Deck())
            {
                var emotions = card.Charges.Select(c => c.Emotion).ToList();
                CollectionAssert.AllItemsAreUnique(emotions, card.Id);
            }
        }

        // ---- recipes -----------------------------------------------------------------

        [Test]
        public void EveryRecipe_AmplifiesChargesAtLeastAsMuchAsNoRecipe()
        {
            var recipes = DataLoader.ParseRecipes(ReadDataFile("recipes/recipes.json"));

            foreach (var recipe in recipes)
            {
                Assert.GreaterOrEqual(recipe.ChargeMultiplier, 1.0, recipe.Id);
                Assert.LessOrEqual(recipe.ChargeMultiplier, EmotionResolver.MaxChargeMultiplier, recipe.Id);
                Assert.Greater(recipe.ChargeMultiplier, EmotionResolver.NoRecipeMultiplier,
                    $"{recipe.Id} must beat pouring at random");
            }
        }

        // ---- archetypes --------------------------------------------------------------

        [Test]
        public void ArchetypesFile_Parses_AndCoversEveryEmotion()
        {
            var archetypes = Archetypes();

            Assert.IsNotEmpty(archetypes);
            foreach (var archetype in archetypes)
                foreach (var emotion in Emotions.All)
                {
                    var band = archetype[emotion];
                    Assert.LessOrEqual(band.Min, band.Max, $"{archetype.Id}/{emotion}");
                    Assert.GreaterOrEqual(band.Min, 0);
                    Assert.LessOrEqual(band.Max, 100);
                }
        }

        [Test]
        public void EveryArchetype_HasANamePool()
        {
            foreach (var archetype in Archetypes())
                Assert.IsNotEmpty(archetype.NamePool, archetype.Id);
        }

        [Test]
        public void ArchetypeIds_AreUnique()
        {
            CollectionAssert.AllItemsAreUnique(Archetypes().Select(a => a.Id).ToList());
        }

        [Test]
        public void EveryEmotion_IsSomeArchetypesDominant()
        {
            // If no archetype leads with an emotion, that emotion never drives an intent
            // and the charges that move it are dead weight.
            var dominants = new HashSet<Emotion>();
            var rng = new RunRng("coverage").GetStream("customer");

            foreach (var archetype in Archetypes())
                for (int i = 0; i < 40; i++)
                    dominants.Add(archetype.RollBaseline(rng).Dominant);

            foreach (var emotion in Emotions.All)
                Assert.IsTrue(dominants.Contains(emotion), $"no archetype ever leads with {emotion}");
        }

        [Test]
        public void Baselines_LeaveRoomToWorkInBothDirections()
        {
            // A baseline pinned at 0 or 100 makes half the intents unplayable.
            foreach (var archetype in Archetypes())
                foreach (var emotion in Emotions.All)
                {
                    Assert.Greater(archetype[emotion].Max, 0, $"{archetype.Id}/{emotion} can never be extinguished from");
                    Assert.Less(archetype[emotion].Min, 100, $"{archetype.Id}/{emotion} can never be fuelled");
                }
        }
    }
}
