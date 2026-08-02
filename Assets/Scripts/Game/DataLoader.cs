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

        /// <summary>
        /// Cards marked locked in the file (v5 P10): future stock, quarantined at the parse.
        /// They never reach <see cref="Cards"/>, so the shelf, the market catalogue and the
        /// simulator's tier-1 sweep all stay exactly as they were the day before the content
        /// existed — unlocking is a purchase, not a load.
        /// </summary>
        public IReadOnlyList<IngredientCard> LockedCards { get; }

        public LoadedDeck(string deckId, string name, IReadOnlyList<IngredientCard> cards,
            IReadOnlyList<IngredientCard> lockedCards = null)
        {
            DeckId = deckId;
            Name = name;
            Cards = cards;
            LockedCards = lockedCards ?? System.Array.Empty<IngredientCard>();
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
            var locked = new List<IngredientCard>();
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
                    // Every branded bottle says which aisle it lives on (v5 P10).
                    if (!IngredientCategories.IsKnown(card.category))
                        throw new FormatException(
                            $"Bottle '{card.id}' has unknown category '{card.category}'.");
                    info = new IngredientInfo(card.style, card.tier, card.price,
                        card.origin, card.abv, card.blurb, card.category, card.carbonated);
                }
                var parsed = new IngredientCard(card.id, card.name, ParseType(card.type, card.id),
                    card.flavor, QualityTier.HousePour, info);
                if (card.locked) locked.Add(parsed);
                else cards.Add(parsed);
            }
            return new LoadedDeck(dto.deckId, dto.name, cards, locked);
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

                // Hand-authored style bands (v5 P10): "a Gin & Tonic is 30–50% gin". When the
                // file gives none, bands derive from the type pattern as before.
                List<RatioRequirement> ratios = null;
                if (recipe.ratios != null && recipe.ratios.Count > 0)
                {
                    ratios = new List<RatioRequirement>(recipe.ratios.Count);
                    foreach (var band in recipe.ratios)
                    {
                        if (string.IsNullOrWhiteSpace(band.style))
                            throw new FormatException($"Recipe '{recipe.id}' has a ratio band with no style.");
                        if (band.max < band.min || band.min < 0 || band.max > 1)
                            throw new FormatException(
                                $"Recipe '{recipe.id}' has a bad {band.style} band {band.min}–{band.max}.");
                        if (band.minTier < 0)
                            throw new FormatException(
                                $"Recipe '{recipe.id}' has a negative minTier on its {band.style} band.");
                        // 0 and 1 both mean "any bottle of the style": JsonUtility cannot express
                        // an absent int, so the file leaves the field off for ordinary bands.
                        ratios.Add(new RatioRequirement(band.style, band.min, band.max,
                            Math.Max(1, band.minTier)));
                    }
                }

                recipes.Add(new RecipeDefinition(
                    recipe.id, recipe.name, recipe.rank,
                    recipe.baseFlavor, recipe.baseMult, recipe.flavorPerLevel, recipe.multPerLevel,
                    requirements,
                    recipe.exactMixSize, recipe.minMixSize,
                    recipe.allDistinctTypes, recipe.allEqualFlavor, recipe.scoreAllMixCards,
                    recipe.equalFlavorGroupSize, recipe.ascendingFlavorGroupSize,
                    recipe.sameTypeGroupMin,
                    ratioRequirements: ratios, // null = derive from the type pattern
                    minFill: recipe.minFill,
                    locked: recipe.locked,
                    prep: ParsePrep(recipe.prepMethod, recipe.id),
                    glassId: recipe.glassId,
                    icon: recipe.icon));
            }
            return recipes;
        }

        /// <summary>The serving glasses (v5 P10): silhouette, sprite, upgrade prices.</summary>
        public static IReadOnlyList<GlasswareDefinition> ParseGlassware(string json)
        {
            var dto = FromJson<GlasswareFileDto>(json, "glassware");
            if (dto.glasses == null || dto.glasses.Count == 0)
                throw new FormatException("Glassware file contains no glasses.");

            var seen = new HashSet<string>();
            var glasses = new List<GlasswareDefinition>(dto.glasses.Count);
            foreach (var glass in dto.glasses)
            {
                if (!seen.Add(glass.id ?? ""))
                    throw new FormatException($"Glassware file lists '{glass.id}' twice.");
                try
                {
                    glasses.Add(new GlasswareDefinition(glass.id, glass.name, glass.spriteKey,
                        glass.profile?.ToArray(), glass.tierPrices?.ToArray(), glass.capacity));
                }
                catch (ArgumentException e)
                {
                    throw new FormatException($"Glassware '{glass.id}': {e.Message}");
                }
            }
            return glasses;
        }

        /// <summary>The snack bowls (v5 P10).</summary>
        public static IReadOnlyList<SnackDefinition> ParseSnacks(string json)
        {
            var dto = FromJson<SnacksFileDto>(json, "snacks");
            if (dto.snacks == null || dto.snacks.Count == 0)
                throw new FormatException("Snacks file contains no snacks.");

            var seen = new HashSet<string>();
            var snacks = new List<SnackDefinition>(dto.snacks.Count);
            foreach (var snack in dto.snacks)
            {
                if (!seen.Add(snack.id ?? ""))
                    throw new FormatException($"Snacks file lists '{snack.id}' twice.");
                try
                {
                    snacks.Add(new SnackDefinition(snack.id, snack.name, snack.price, snack.stock));
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Snack '{snack.id}': {e.Message}");
                }
            }
            return snacks;
        }

        private static PrepMethod ParsePrep(string raw, string context)
        {
            if (string.IsNullOrEmpty(raw)) return PrepMethod.Shaken;
            return ParseEnum<PrepMethod>(raw, context, "prepMethod");
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
                int weight = archetype.weight > 0 ? archetype.weight : 1;
                var hometowns = archetype.hometowns;
                var demand = string.IsNullOrEmpty(archetype.demand)
                    ? DemandLevel.Easygoing
                    : ParseEnum<DemandLevel>(archetype.demand, archetype.id, "demand");
                archetypes.Add(new ArchetypeDefinition(
                    archetype.id, archetype.name, archetype.names, weight, demand, hometowns));
            }
            return archetypes;
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
            // v5 P10 content model.
            public string category;
            public bool carbonated;
            public bool locked;
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
            public double minFill;
            // v5 P10 content model.
            public bool locked;
            public string prepMethod;
            public string glassId;
            public string icon;
            public List<RatioDto> ratios;
        }

        [Serializable]
        private sealed class RatioDto
        {
            public string style;
            public double min;
            public double max;
            /// <summary>Lowest brand rung that fills this band; absent/0 = any bottle.</summary>
            public int minTier;
        }

        [Serializable]
        private sealed class RecipesFileDto
        {
            public int version;
            public List<RecipeDto> recipes;
        }

        [Serializable]
        private sealed class GlassDto
        {
            public string id;
            public string name;
            public string spriteKey;
            public List<double> profile;
            public List<int> tierPrices;
            public double capacity;
        }

        [Serializable]
        private sealed class GlasswareFileDto
        {
            public int version;
            public List<GlassDto> glasses;
        }

        [Serializable]
        private sealed class SnackDto
        {
            public string id;
            public string name;
            public int price;
            public int stock;
        }

        [Serializable]
        private sealed class SnacksFileDto
        {
            public int version;
            public List<SnackDto> snacks;
        }

#pragma warning restore 0649
    }
}
