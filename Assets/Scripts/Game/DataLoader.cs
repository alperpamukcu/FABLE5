using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>A parsed deck file: fresh IngredientCard instances on every parse.</summary>
    public sealed class LoadedDeck
    {
        public string DeckId { get; }
        public string Name { get; }
        public IReadOnlyList<IngredientCard> Cards { get; }

        public LoadedDeck(string deckId, string name, IReadOnlyList<IngredientCard> cards)
        {
            DeckId = deckId;
            Name = name;
            Cards = cards;
        }
    }

    /// <summary>
    /// Parses Assets/Data JSON into pure-core models, validating loudly: these files are
    /// the game's modding/content surface, so a typo must fail at load, not mid-run.
    /// </summary>
    public static class DataLoader
    {
        public static LoadedDeck ParseDeck(string json)
        {
            var dto = FromJson<DeckFileDto>(json, "deck");
            if (dto.cards == null || dto.cards.Count == 0)
                throw new FormatException("Deck file contains no cards.");

            var cards = new List<IngredientCard>(dto.cards.Count);
            foreach (var card in dto.cards)
            {
                if (string.IsNullOrWhiteSpace(card.id))
                    throw new FormatException("Deck file has a card with an empty id.");
                if (card.flavor < 0)
                    throw new FormatException($"Card '{card.id}' has negative flavor.");
                // Branded bottles (GDD 22) carry their identity papers; older files without
                // a style are plain cards and load as before.
                IngredientInfo info = null;
                if (!string.IsNullOrEmpty(card.style))
                {
                    if (card.tier < 1)
                        throw new FormatException($"Bottle '{card.id}' has tier {card.tier}; brands start at 1.");
                    if (card.tier > 1 && card.price <= 0)
                        throw new FormatException($"Bottle '{card.id}' is a market brand but has no price.");
                    info = new IngredientInfo(card.style, card.tier, card.price,
                        card.origin, card.abv, card.blurb);
                }
                cards.Add(new IngredientCard(card.id, card.name, ParseType(card.type, card.id),
                    card.flavor, QualityTier.HousePour, ParseCharges(card.charges, card.id), info));
            }
            return new LoadedDeck(dto.deckId, dto.name, cards);
        }

        public static IReadOnlyList<RecipeDefinition> ParseRecipes(string json)
        {
            var dto = FromJson<RecipesFileDto>(json, "recipes");
            if (dto.recipes == null || dto.recipes.Count == 0)
                throw new FormatException("Recipes file contains no recipes.");

            var recipes = new List<RecipeDefinition>(dto.recipes.Count);
            foreach (var recipe in dto.recipes)
            {
                if (string.IsNullOrWhiteSpace(recipe.id))
                    throw new FormatException("Recipes file has a recipe with an empty id.");
                bool isGroupRecipe = recipe.equalFlavorGroupSize > 0 ||
                                     recipe.ascendingFlavorGroupSize > 0 ||
                                     recipe.sameTypeGroupMin > 0;
                if (!isGroupRecipe && (recipe.requirements == null || recipe.requirements.Count == 0))
                    throw new FormatException($"Recipe '{recipe.id}' has no requirements.");

                var requirements = new List<PatternRequirement>(recipe.requirements?.Count ?? 0);
                foreach (var req in recipe.requirements ?? new List<RequirementDto>())
                {
                    if (req.types == null || req.types.Count == 0)
                        throw new FormatException($"Recipe '{recipe.id}' has a requirement with no types.");
                    var types = new IngredientType[req.types.Count];
                    for (int i = 0; i < req.types.Count; i++)
                        types[i] = ParseType(req.types[i], recipe.id);
                    requirements.Add(new PatternRequirement(req.count, types));
                }

                recipes.Add(new RecipeDefinition(
                    recipe.id, recipe.name, recipe.rank,
                    recipe.baseFlavor, recipe.baseMult, recipe.flavorPerLevel, recipe.multPerLevel,
                    requirements,
                    recipe.exactMixSize, recipe.minMixSize,
                    recipe.allDistinctTypes, recipe.allEqualFlavor, recipe.scoreAllMixCards,
                    recipe.equalFlavorGroupSize, recipe.ascendingFlavorGroupSize,
                    recipe.sameTypeGroupMin,
                    recipe.chargeMultiplier)); // 0 = derive it from baseMult
            }
            return recipes;
        }

        /// <summary>
        /// Customer archetypes (GDD 19 §9). Bands are addressed by emotion name rather than
        /// position so a reordered file can't silently make everyone furious.
        /// </summary>
        public static IReadOnlyList<ArchetypeDefinition> ParseArchetypes(string json)
        {
            var dto = FromJson<ArchetypesFileDto>(json, "archetypes");
            if (dto.archetypes == null || dto.archetypes.Count == 0)
                throw new FormatException("Archetypes file contains no archetypes.");

            var archetypes = new List<ArchetypeDefinition>(dto.archetypes.Count);
            foreach (var archetype in dto.archetypes)
            {
                if (string.IsNullOrWhiteSpace(archetype.id))
                    throw new FormatException("Archetypes file has an archetype with an empty id.");
                if (archetype.bands == null || archetype.bands.Count != Emotions.Count)
                    throw new FormatException(
                        $"Archetype '{archetype.id}' needs exactly {Emotions.Count} bands.");

                var bands = new EmotionBand[Emotions.Count];
                var seen = new bool[Emotions.Count];
                foreach (var band in archetype.bands)
                {
                    var emotion = ParseEnum<Emotion>(band.emotion, archetype.id, "emotion");
                    if (seen[(int)emotion])
                        throw new FormatException(
                            $"Archetype '{archetype.id}' lists {emotion} twice.");
                    if (band.max < band.min)
                        throw new FormatException(
                            $"Archetype '{archetype.id}' has {emotion} max {band.max} below min {band.min}.");
                    bands[(int)emotion] = new EmotionBand(band.min, band.max);
                    seen[(int)emotion] = true;
                }

                int weight = archetype.weight > 0 ? archetype.weight : 1;
                var hometowns = archetype.hometowns;
                var demand = string.IsNullOrEmpty(archetype.demand)
                    ? DemandLevel.Easygoing
                    : ParseEnum<DemandLevel>(archetype.demand, archetype.id, "demand");
                archetypes.Add(new ArchetypeDefinition(
                    archetype.id, archetype.name, bands, archetype.names, weight, demand, hometowns));
            }
            return archetypes;
        }

        /// <summary>
        /// Emotional charges printed on a card (GDD 19 §4). A card with no charges is inert,
        /// which is legal — it just says nothing to anyone.
        /// </summary>
        private static IReadOnlyList<EmotionCharge> ParseCharges(List<ChargeDto> dtos, string context)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<EmotionCharge>();

            var charges = new List<EmotionCharge>(dtos.Count);
            var seen = new HashSet<Emotion>();
            foreach (var dto in dtos)
            {
                var emotion = ParseEnum<Emotion>(dto.emotion, context, "emotion");
                if (!seen.Add(emotion))
                    throw new FormatException($"Card '{context}' charges {emotion} twice.");
                if (dto.amount == 0)
                    throw new FormatException($"Card '{context}' has a zero {emotion} charge.");
                charges.Add(new EmotionCharge(emotion, dto.amount));
            }
            return charges;
        }

        private static T ParseEnum<T>(string raw, string context, string field) where T : struct
        {
            if (Enum.TryParse(raw, ignoreCase: false, out T value)) return value;
            throw new FormatException($"Unknown {field} '{raw}' (in '{context}').");
        }

        private static T FromJson<T>(string json, string label) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException($"Empty {label} JSON.");
            var dto = JsonUtility.FromJson<T>(json);
            if (dto == null)
                throw new FormatException($"Could not parse {label} JSON.");
            return dto;
        }

        private static IngredientType ParseType(string raw, string context)
        {
            if (Enum.TryParse(raw, ignoreCase: false, out IngredientType type)) return type;
            throw new FormatException($"Unknown ingredient type '{raw}' (in '{context}').");
        }

#pragma warning disable 0649 // fields assigned by JsonUtility via reflection
        [Serializable]
        private sealed class ChargeDto
        {
            public string emotion;
            public int amount;
        }

        [Serializable]
        private sealed class CardDto
        {
            public string id;
            public string name;
            public string type;
            public int flavor;
            public List<ChargeDto> charges;
            // Brand papers (GDD 22); style empty = plain unbranded card.
            public string style;
            public int tier;
            public int price;
            public string origin;
            public double abv;
            public string blurb;
        }

        [Serializable]
        private sealed class BandDto
        {
            public string emotion;
            public int min;
            public int max;
        }

        [Serializable]
        private sealed class ArchetypeDto
        {
            public string id;
            public string name;
            public int weight;
            public string demand;
            public List<string> names;
            public List<string> hometowns;
            public List<BandDto> bands;
        }

        [Serializable]
        private sealed class ArchetypesFileDto
        {
            public int version;
            public List<ArchetypeDto> archetypes;
        }

        [Serializable]
        private sealed class DeckFileDto
        {
            public string deckId;
            public string name;
            public List<CardDto> cards;
        }

        [Serializable]
        private sealed class RequirementDto
        {
            public List<string> types;
            public int count;
        }

        [Serializable]
        private sealed class RecipeDto
        {
            public string id;
            public string name;
            public int rank;
            public int baseFlavor;
            public int baseMult;
            public int flavorPerLevel;
            public int multPerLevel;
            public List<RequirementDto> requirements;
            public int exactMixSize;
            public int minMixSize;
            public bool allDistinctTypes;
            public bool allEqualFlavor;
            public bool scoreAllMixCards;
            public int equalFlavorGroupSize;
            public int ascendingFlavorGroupSize;
            public int sameTypeGroupMin;
            public double chargeMultiplier;
        }

        [Serializable]
        private sealed class RecipesFileDto
        {
            public int version;
            public List<RecipeDto> recipes;
        }

#pragma warning restore 0649
    }
}
