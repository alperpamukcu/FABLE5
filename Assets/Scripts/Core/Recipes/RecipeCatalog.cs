using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The v1.0 recipe table (GDD 02, section 4), built in code so the pure core has no
    /// file dependencies. Assets/Data/recipes/recipes.json mirrors this table for the
    /// data-driven pipeline; a parity test keeps the two in sync once DataLoader lands (M1 wk2).
    /// </summary>
    public static class RecipeCatalog
    {
        public static IReadOnlyList<RecipeDefinition> CreateDefault()
        {
            var s = IngredientType.Spirit;
            var so = IngredientType.Sour;
            var sw = IngredientType.Sweet;
            var bi = IngredientType.Bitter;
            var bu = IngredientType.Bubbly;
            var g = IngredientType.Garnish;

            return new List<RecipeDefinition>
            {
                new RecipeDefinition("neat_pour", "Neat Pour", 1, 5, 1, 10, 1,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 1),

                new RecipeDefinition("spritz", "Spritz", 2, 10, 2, 15, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) }),

                new RecipeDefinition("old_fashioned", "Old Fashioned", 3, 20, 2, 20, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) }),

                new RecipeDefinition("highball", "Highball", 4, 25, 3, 20, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, g) }),

                new RecipeDefinition("sour", "Sour", 5, 30, 3, 25, 2,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) }),

                new RecipeDefinition("martini", "Martini", 6, 35, 4, 25, 2,
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, bi, g) }),

                new RecipeDefinition("fizz", "Fizz", 7, 45, 4, 30, 2,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, bu) }),

                new RecipeDefinition("negroni", "Negroni", 8, 55, 5, 30, 3,
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, bi), new PatternRequirement(1, g) }),

                new RecipeDefinition("tiki", "Tiki", 9, 70, 6, 35, 3,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    minMixSize: 5, scoreAllMixCards: true),

                new RecipeDefinition("perfect_serve", "Perfect Serve", 10, 100, 8, 40, 4,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 5, allDistinctTypes: true, scoreAllMixCards: true),

                new RecipeDefinition("double_perfect", "Double Perfect", 11, 160, 14, 50, 5,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 5, allDistinctTypes: true, allEqualFlavor: true, scoreAllMixCards: true)
            };
        }
    }
}
