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

        public static IReadOnlyList<PatronDefinition> ParsePatrons(string json)
        {
            var dto = FromJson<PatronsFileDto>(json, "patrons");
            if (dto.patrons == null || dto.patrons.Count == 0)
                throw new FormatException("Patrons file contains no patrons.");

            var patrons = new List<PatronDefinition>(dto.patrons.Count);
            foreach (var patron in dto.patrons)
            {
                if (string.IsNullOrWhiteSpace(patron.id))
                    throw new FormatException("Patrons file has a patron with an empty id.");
                if (patron.effects == null || patron.effects.Count == 0)
                    throw new FormatException($"Patron '{patron.id}' has no effects.");

                var effects = new List<PatronEffect>(patron.effects.Count);
                foreach (var effect in patron.effects)
                {
                    effects.Add(new PatronEffect(
                        ParseEnum<EffectTrigger>(effect.trigger, patron.id, "trigger"),
                        ParseEnum<EffectOp>(effect.op, patron.id, "op"),
                        effect.value,
                        ParseCondition(effect.condition, patron.id),
                        string.IsNullOrEmpty(effect.valueSource)
                            ? EffectValueSource.Constant
                            : ParseEnum<EffectValueSource>(effect.valueSource, patron.id, "valueSource")));
                }

                patrons.Add(new PatronDefinition(
                    patron.id, patron.name,
                    ParseEnum<PatronRarity>(patron.rarity, patron.id, "rarity"),
                    patron.cost, patron.description, effects));
            }
            return patrons;
        }

        public static IReadOnlyList<ToolDefinition> ParseTools(string json)
        {
            var dto = FromJson<ToolsFileDto>(json, "tools");
            if (dto.tools == null || dto.tools.Count == 0)
                throw new FormatException("Tools file contains no tools.");

            var tools = new List<ToolDefinition>(dto.tools.Count);
            foreach (var tool in dto.tools)
            {
                if (string.IsNullOrWhiteSpace(tool.id))
                    throw new FormatException("Tools file has a tool with an empty id.");

                var op = ParseEnum<ToolOp>(tool.op, tool.id, "op");
                var enhancement = string.IsNullOrEmpty(tool.enhancement)
                    ? Enhancement.None
                    : ParseEnum<Enhancement>(tool.enhancement, tool.id, "enhancement");
                IngredientType convertTo = default;
                if (!string.IsNullOrEmpty(tool.convertTo)) convertTo = ParseType(tool.convertTo, tool.id);
                if (op == ToolOp.ConvertType && string.IsNullOrEmpty(tool.convertTo))
                    throw new FormatException($"Tool '{tool.id}' converts type but has no convertTo.");

                tools.Add(new ToolDefinition(tool.id, tool.name, tool.cost, op, tool.maxTargets,
                    enhancement, convertTo, tool.description));
            }
            return tools;
        }

        private static EffectCondition ParseCondition(ConditionDto dto, string context)
        {
            if (dto == null || string.IsNullOrEmpty(dto.kind)) return EffectCondition.Always;

            var kind = ParseEnum<ConditionKind>(dto.kind, context, "condition kind");
            IngredientType type = default;
            if (!string.IsNullOrEmpty(dto.type)) type = ParseType(dto.type, context);
            return new EffectCondition(kind, type, dto.intValue, dto.recipeIds);
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

        [Serializable]
        private sealed class ConditionDto
        {
            public string kind;
            public string type;
            public int intValue;
            public List<string> recipeIds;
        }

        [Serializable]
        private sealed class EffectDto
        {
            public string trigger;
            public string op;
            public double value;
            public string valueSource;
            public ConditionDto condition;
        }

        [Serializable]
        private sealed class PatronDto
        {
            public string id;
            public string name;
            public string rarity;
            public int cost;
            public string description;
            public List<EffectDto> effects;
        }

        [Serializable]
        private sealed class PatronsFileDto
        {
            public int version;
            public List<PatronDto> patrons;
        }

        [Serializable]
        private sealed class ToolDto
        {
            public string id;
            public string name;
            public int cost;
            public string op;
            public string enhancement;
            public string convertTo;
            public int maxTargets;
            public string description;
        }

        [Serializable]
        private sealed class ToolsFileDto
        {
            public int version;
            public List<ToolDto> tools;
        }
#pragma warning restore 0649
    }
}
