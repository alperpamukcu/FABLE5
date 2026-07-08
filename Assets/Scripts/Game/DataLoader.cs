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
                cards.Add(new IngredientCard(card.id, card.name, ParseType(card.type, card.id), card.flavor));
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
                if (recipe.requirements == null || recipe.requirements.Count == 0)
                    throw new FormatException($"Recipe '{recipe.id}' has no requirements.");

                var requirements = new List<PatternRequirement>(recipe.requirements.Count);
                foreach (var req in recipe.requirements)
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
                    recipe.allDistinctTypes, recipe.allEqualFlavor, recipe.scoreAllMixCards));
            }
            return recipes;
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
        private sealed class CardDto
        {
            public string id;
            public string name;
            public string type;
            public int flavor;
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
