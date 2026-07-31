using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The v1.1 14-recipe table (GDD 02, section 4), built in code so the pure core has no
    /// file dependencies. Assets/Data/recipes/recipes.json mirrors this table for the
    /// data-driven pipeline; a parity test keeps the two in sync. Rank = the table's
    /// priority column: every matching pattern is considered, highest priority wins.
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
            var be = IngredientType.Beer;

            return new List<RecipeDefinition>
            {
                // Beer is the bar's simple order (GDD 21 §10): one thing, pulled, no shaker.
                // It shares Neat Pour's rank because it is the same kind of drink — the one
                // you can put in front of someone in four seconds — and so it is on the menu
                // from day one and priced at the bottom of it.
                new RecipeDefinition("draught", "Draught", 1, 5, 1, 10, 1,
                    new[] { new PatternRequirement(1, be) },
                    exactMixSize: 1, minFill: 0.75, glassId: "pint"),

                new RecipeDefinition("neat_pour", "Neat Pour", 1, 5, 1, 10, 1,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 1, glassId: "rocks"),

                new RecipeDefinition("spritz", "Spritz", 2, 10, 2, 15, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    glassId: "highball"),

                new RecipeDefinition("old_fashioned", "Old Fashioned", 3, 20, 2, 20, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    glassId: "rocks"),

                new RecipeDefinition("highball", "Highball", 4, 25, 3, 20, 1,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, g) },
                    glassId: "highball"),

                new RecipeDefinition("house_special", "House Special", 5, 30, 3, 25, 2,
                    null, equalFlavorGroupSize: 3, glassId: "coupe"),

                new RecipeDefinition("sour", "Sour", 6, 30, 3, 25, 2,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    glassId: "rocks"),

                new RecipeDefinition("martini", "Martini", 7, 35, 4, 25, 2,
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, bi, g) },
                    glassId: "martini"),

                new RecipeDefinition("layered_pour", "Layered Pour", 8, 40, 4, 30, 2,
                    null, ascendingFlavorGroupSize: 4, glassId: "highball"),

                new RecipeDefinition("fizz", "Fizz", 9, 45, 4, 30, 2,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, bu) },
                    glassId: "highball"),

                new RecipeDefinition("straight_booze", "Straight Booze", 10, 50, 5, 30, 3,
                    null, sameTypeGroupMin: 4, glassId: "rocks"),

                new RecipeDefinition("negroni", "Negroni", 11, 55, 5, 30, 3,
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, bi), new PatternRequirement(1, g) },
                    glassId: "rocks"),

                new RecipeDefinition("tiki", "Tiki", 12, 70, 6, 35, 3,
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    minMixSize: 5, scoreAllMixCards: true, glassId: "highball"),

                new RecipeDefinition("perfect_serve", "Perfect Serve", 13, 100, 8, 40, 4,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 5, allDistinctTypes: true, scoreAllMixCards: true, glassId: "coupe"),

                new RecipeDefinition("double_perfect", "Double Perfect", 14, 160, 14, 50, 5,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 5, allDistinctTypes: true, allEqualFlavor: true, scoreAllMixCards: true, glassId: "martini"),

                // ── the starter cocktails (v5 P10) — LOCKED until the shop sells them ────
                // Style-banded, so a Gin & Tonic is gin and tonic rather than "some spirit
                // with some fizz". All quarantined by Locked: they neither roll as orders nor
                // match a pour until unlocked, which is what keeps the sim identical the day
                // the content lands. Ranks 15+ so a real one outranks its abstract cousin
                // (a true G&T must not read as a mere Spritz) once it is live.
                Cocktail("gin_tonic", "Gin & Tonic", 15, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    Band("gin", .30, .50), Band("tonic", .50, .70)),
                Cocktail("vodka_soda", "Vodka Soda", 16, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    Band("vodka", .30, .50), Band("soda", .50, .70)),
                Cocktail("whiskey_cola", "Whiskey & Cola", 17, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    Band("bourbon", .30, .50), Band("cola", .50, .70)),
                Cocktail("cuba_libre", "Cuba Libre", 18, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, so) },
                    Band("rum", .25, .45), Band("cola", .45, .70), Band("lime", .02, .15)),
                Cocktail("screwdriver", "Screwdriver", 19, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so) },
                    Band("vodka", .30, .50), Band("orange", .50, .70)),
                Cocktail("vodka_bull", "Vodka Bull", 20, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    Band("vodka", .30, .55), Band("energy", .45, .70)),
                Cocktail("whiskey_sour", "Whiskey Sour", 21, 25, 2, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    Band("bourbon", .45, .65), Band("lemon", .20, .40), Band("syrup", .10, .30)),
                Cocktail("gin_fizz", "Gin Fizz", 22, 25, 2, 20, 1, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, bu) },
                    Band("gin", .35, .55), Band("lemon", .15, .35), Band("syrup", .05, .25), Band("soda", .15, .35)),
                Cocktail("daiquiri", "Daiquiri", 23, 25, 2, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    Band("rum", .45, .65), Band("lime", .15, .35), Band("syrup", .10, .30)),
                Cocktail("margarita", "Margarita", 24, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, so) },
                    Band("tequila", .40, .60), Band("triple_sec", .15, .35), Band("lime", .15, .35)),
                Cocktail("dry_martini", "Dry Martini", 25, 30, 3, 20, 1, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, sw) },
                    Band("gin", .70, .90), Band("vermouth", .10, .30)),
                Cocktail("dirty_martini", "Dirty Martini", 26, 30, 3, 20, 1, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    Band("gin", .60, .85), Band("vermouth", .08, .28), Band("olive", .05, .20)),

                // Wave 2 (v5 P16): two more on the same model — new content is new data.
                // Both BUILT, because their fizz and juice go in at the glass (C8).
                Cocktail("mojito", "Mojito", 27, 30, 3, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so),
                            new PatternRequirement(1, sw), new PatternRequirement(1, bu),
                            new PatternRequirement(1, g) },
                    Band("rum", .28, .48), Band("lime", .10, .28), Band("syrup", .06, .22),
                    Band("soda", .20, .45), Band("mint", .03, .15)),
                Cocktail("tequila_sunrise", "Tequila Sunrise", 28, 25, 2, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    Band("tequila", .30, .50), Band("orange", .38, .62), Band("syrup", .05, .20)),
            };
        }

        private static RatioRequirement Band(string style, double min, double max) =>
            new RatioRequirement(style, min, max);

        private static RecipeDefinition Cocktail(string id, string name, int rank,
            int baseFlavor, int baseMult, int flavorPerLevel, int multPerLevel,
            PrepMethod prep, string glassId,
            PatternRequirement[] requirements, params RatioRequirement[] ratios) =>
            new RecipeDefinition(id, name, rank, baseFlavor, baseMult, flavorPerLevel, multPerLevel,
                requirements, ratioRequirements: ratios, locked: true, prep: prep, glassId: glassId);
    }
}
