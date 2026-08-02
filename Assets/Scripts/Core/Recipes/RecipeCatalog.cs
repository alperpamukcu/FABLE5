using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The drink menu (v5 P16 redesign, 2026-07-31), built in code so the pure core has no
    /// file dependencies. Assets/Data/recipes/recipes.json mirrors this table; a parity test
    /// keeps the two in sync.
    ///
    /// **Every cocktail is style-banded.** The old abstract table (Spritz = "some spirit with
    /// some fizz") was the card game's language, and it stopped being true the day the bottles
    /// became brands: whether the glass holds vodka or gin IS the drink. The author's ruling
    /// (2026-07-31): the type system stays as the shelf's taxonomy — aisles, icons, the judge's
    /// vocabulary — but the recipes speak styles. Exactly two orders stay type-based ON
    /// PURPOSE, because they are brand-agnostic in the fiction too: a Draught is whatever is
    /// on tap, and a Neat Pour is "pour me something".
    ///
    /// Four difficulty tiers, priced and gated by rank (see TycoonRun.RecipeStarGate):
    ///   starter  ranks 1–8   — two parts, generous bands; 1–3 open from day one
    ///   mid      ranks 9–14  — three parts or a shake; 3.0★ (a bar not underwater)
    ///   hard     ranks 15–21 — shaken sours, four bands, tighter; 3.5★
    ///   veryhard ranks 22+   — stirred precision, thirds, five-plus bands; 4.0★
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
                // ── open from day one: makeable from the opening shelf ──────────────
                // Beer is the bar's simple order (GDD 21 §10): one thing, pulled, no shaker.
                new RecipeDefinition("draught", "Draught", 1, 5, 1, 10, 1,
                    new[] { new PatternRequirement(1, be) },
                    exactMixSize: 1, minFill: 0.75, glassId: "pint"),

                // The other brand-agnostic order: "pour me something". Type-based on purpose.
                new RecipeDefinition("neat_pour", "Neat Pour", 1, 5, 1, 10, 1,
                    new[] { new PatternRequirement(1, s) },
                    exactMixSize: 1, glassId: "rocks"),

                Cocktail("vodka_soda", "Vodka Soda", 2, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: false,
                    Band("vodka", .30, .50), Band("soda", .50, .70)),
                Cocktail("gin_sour", "Gin Sour", 3, 20, 2, 15, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: false,
                    Band("gin", .45, .65), Band("lemon", .20, .40), Band("syrup", .10, .30)),

                // ── starter buys (ranks 4–8): two parts, generous bands, no gate ────
                Cocktail("gin_tonic", "Gin & Tonic", 4, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("gin", .30, .50), Band("tonic", .50, .70)),
                Cocktail("whiskey_cola", "Whiskey & Cola", 5, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("bourbon", .30, .50), Band("cola", .50, .70)),
                Cocktail("screwdriver", "Screwdriver", 6, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so) },
                    locked: true,
                    Band("vodka", .30, .50), Band("orange", .50, .70)),
                Cocktail("black_russian", "Black Russian", 8, 20, 2, 15, 1, PrepMethod.Built, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("vodka", .55, .75), Band("coffee_liqueur", .25, .45)),
                Cocktail("vodka_bull", "Vodka Bull", 7, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("vodka", .30, .55), Band("energy", .45, .70)),

                // ── mid (ranks 9–14): a third part, or the first shake ──────────────
                Cocktail("cuba_libre", "Cuba Libre", 9, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, so) },
                    locked: true,
                    Band("rum", .25, .45), Band("cola", .45, .70), Band("lime", .02, .15)),
                Cocktail("whiskey_ginger", "Whiskey Ginger", 10, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("bourbon", .30, .50), Band("ginger", .50, .70)),
                Cocktail("moscow_mule", "Moscow Mule", 11, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, so) },
                    locked: true,
                    Band("vodka", .30, .50), Band("ginger", .40, .60), Band("lime", .05, .20)),
                Cocktail("gimlet", "Gimlet", 12, 25, 2, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("gin", .55, .75), Band("lime", .15, .35), Band("syrup", .08, .25)),
                Cocktail("sex_on_beach", "Sex on the Beach", 13, 25, 2, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, so) },
                    locked: true,
                    Band("vodka", .25, .45), Band("orange", .25, .45), Band("cranberry", .25, .45)),
                Cocktail("vodka_sour", "Vodka Sour", 14, 25, 2, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("vodka", .45, .65), Band("lemon", .20, .40), Band("syrup", .10, .30)),
                // Grenadine, not syrup: the red sinking through the orange IS the sunrise,
                // and it was the only job the bar's grenadine had (2026-08-02).
                Cocktail("tequila_sunrise", "Tequila Sunrise", 13, 25, 2, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("tequila", .30, .50), Band("orange", .38, .62), Band("grenadine", .05, .20)),

                // ── hard (ranks 15–21): the shaken sours, tighter bands ─────────────
                Cocktail("whiskey_sour", "Whiskey Sour", 15, 25, 2, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("bourbon", .45, .65), Band("lemon", .20, .40), Band("syrup", .10, .30)),
                Cocktail("daiquiri", "Daiquiri", 16, 25, 2, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("rum", .45, .65), Band("lime", .15, .35), Band("syrup", .10, .30)),
                Cocktail("gin_fizz", "Gin Fizz", 17, 25, 2, 20, 1, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("gin", .35, .55), Band("lemon", .15, .35), Band("syrup", .05, .25), Band("soda", .15, .35)),
                Cocktail("kamikaze", "Kamikaze", 18, 30, 3, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, so) },
                    locked: true,
                    Band("vodka", .40, .60), Band("triple_sec", .15, .35), Band("lime", .15, .35)),
                Cocktail("margarita", "Margarita", 19, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, so) },
                    locked: true,
                    Band("tequila", .40, .60), Band("triple_sec", .15, .35), Band("lime", .15, .35)),
                Cocktail("cosmopolitan", "Cosmopolitan", 19, 30, 3, 20, 1, PrepMethod.Shaken, "martini",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(2, so) },
                    locked: true,
                    Band("vodka", .35, .55), Band("triple_sec", .12, .28), Band("lime", .08, .22),
                    Band("cranberry", .15, .35)),
                Cocktail("sidecar", "Bourbon Sidecar", 20, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, so) },
                    locked: true,
                    Band("bourbon", .40, .60), Band("triple_sec", .20, .40), Band("lemon", .15, .35)),
                // The shaken wave (2026-07-31, the author: the shaker must be USED): four more
                // worked drinks, so the tin is most of the mid and hard tiers rather than a
                // corner of them.
                Cocktail("rum_punch", "Rum Punch", 15, 25, 2, 20, 1, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("rum", .35, .55), Band("orange", .20, .40), Band("lime", .08, .25), Band("syrup", .05, .20)),
                Cocktail("white_lady", "White Lady", 20, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, so) },
                    locked: true,
                    Band("gin", .40, .60), Band("triple_sec", .20, .40), Band("lemon", .15, .35)),
                Cocktail("southside", "Southside", 21, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    locked: true,
                    Band("gin", .45, .65), Band("lime", .15, .35), Band("syrup", .08, .25), Band("mint", .03, .15)),

                // ── very hard (ranks 22+): stirred precision, thirds, the long build ─
                Cocktail("dry_martini", "Dry Martini", 22, 30, 3, 20, 1, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("gin", .70, .90), Band("vermouth", .10, .30)),
                Cocktail("dirty_martini", "Dirty Martini", 23, 30, 3, 20, 1, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    locked: true,
                    Band("gin", .60, .85), Band("vermouth", .08, .28), Band("olive", .05, .20)),
                Cocktail("manhattan", "Manhattan", 24, 35, 3, 25, 2, PrepMethod.Stirred, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    locked: true,
                    Band("bourbon", .55, .75), Band("vermouth", .20, .40), Band("amaro", .03, .12)),
                // Equal thirds — the steadiest hand on the menu asks for the narrowest bands.
                Cocktail("negroni", "Negroni", 25, 35, 3, 25, 2, PrepMethod.Stirred, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    locked: true,
                    Band("gin", .28, .42), Band("vermouth", .28, .42), Band("amaro", .28, .42)),
                Cocktail("old_fashioned", "Old Fashioned", 26, 35, 3, 25, 2, PrepMethod.Stirred, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    locked: true,
                    Band("bourbon", .70, .88), Band("syrup", .06, .20), Band("amaro", .03, .12)),
                Cocktail("espresso_martini", "Espresso Martini", 23, 35, 3, 25, 2, PrepMethod.Shaken, "martini",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, sw) },
                    locked: true,
                    Band("vodka", .40, .60), Band("coffee_liqueur", .22, .42), Band("syrup", .05, .18)),
                Cocktail("mai_tai", "Mai Tai", 24, 35, 3, 25, 2, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(2, so) },
                    locked: true,
                    Band("rum", .30, .50), Band("triple_sec", .08, .22), Band("lime", .08, .22),
                    Band("pineapple", .15, .35), Band("syrup", .04, .14)),
                Cocktail("mojito", "Mojito", 27, 30, 3, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so),
                            new PatternRequirement(1, sw), new PatternRequirement(1, bu),
                            new PatternRequirement(1, g) },
                    locked: true,
                    Band("rum", .28, .48), Band("lime", .10, .28), Band("syrup", .06, .22),
                    Band("soda", .20, .45), Band("mint", .03, .15)),
                // Seven bands, five of them spirits at a ten-point window each: the capstone.
                Cocktail("long_island", "Long Island", 28, 40, 4, 25, 2, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(5, s), new PatternRequirement(1, so), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("vodka", .08, .18), Band("gin", .08, .18), Band("rum", .08, .18),
                    Band("tequila", .08, .18), Band("triple_sec", .08, .18),
                    Band("lemon", .08, .20), Band("cola", .15, .30)),

                // ── the 2026-08-02 expansion ────────────────────────────────────────
                // Written against the shelf that already exists: no drink here asks for a
                // bottle the bar would buy to serve it alone, and every one of them puts
                // its base spirit in the lead. It rescues the quiet stock — grenadine had
                // no recipe at all, pineapple and tonic had one each — and it fills the
                // hole under tequila, which carried three drinks while vodka carried
                // eleven despite being the priciest line on the shelf.

                // Starter: two parts, no gate. The tonic and the cranberry finally have
                // somewhere to go on day one.
                Cocktail("vodka_tonic", "Vodka Tonic", 8, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("vodka", .30, .50), Band("tonic", .50, .70)),
                Cocktail("cape_codder", "Cape Codder", 8, 15, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so) },
                    locked: true,
                    Band("vodka", .30, .50), Band("cranberry", .50, .70)),

                // Mid: the mule family completed, and tequila's first drinks of its own.
                Cocktail("tequila_mule", "Tequila Mule", 14, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, so) },
                    locked: true,
                    Band("tequila", .30, .50), Band("ginger", .40, .60), Band("lime", .05, .20)),
                Cocktail("paloma", "Paloma", 14, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, bu) },
                    locked: true,
                    Band("tequila", .28, .45), Band("lime", .06, .20), Band("soda", .40, .62)),
                Cocktail("dark_stormy", "Dark 'n' Stormy", 14, 20, 2, 15, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, bu), new PatternRequirement(1, so) },
                    locked: true,
                    Band("rum", .30, .50), Band("ginger", .40, .60), Band("lime", .05, .20)),
                Cocktail("sea_breeze", "Sea Breeze", 14, 25, 2, 20, 1, PrepMethod.Built, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, so) },
                    locked: true,
                    Band("vodka", .25, .45), Band("cranberry", .25, .45), Band("pineapple", .25, .45)),
                Cocktail("tequila_sour", "Tequila Sour", 14, 25, 2, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("tequila", .45, .65), Band("lemon", .20, .40), Band("syrup", .10, .30)),

                // Hard: the mint and the pineapple earn their shelf space here.
                Cocktail("matador", "Matador", 21, 25, 2, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, so) },
                    locked: true,
                    Band("tequila", .35, .55), Band("pineapple", .30, .50), Band("lime", .08, .22)),
                Cocktail("mint_julep", "Mint Julep", 21, 30, 3, 20, 1, PrepMethod.Built, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    locked: true,
                    Band("bourbon", .68, .88), Band("syrup", .08, .24), Band("mint", .03, .15)),
                Cocktail("whiskey_smash", "Whiskey Smash", 21, 30, 3, 20, 1, PrepMethod.Shaken, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw), new PatternRequirement(1, g) },
                    locked: true,
                    Band("bourbon", .45, .65), Band("lemon", .15, .35), Band("syrup", .08, .25), Band("mint", .03, .15)),
                Cocktail("lemon_drop", "Lemon Drop", 21, 30, 3, 20, 1, PrepMethod.Shaken, "martini",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(2, sw) },
                    locked: true,
                    Band("vodka", .40, .60), Band("lemon", .18, .35), Band("triple_sec", .10, .25),
                    Band("syrup", .05, .18)),
                Cocktail("pink_lady", "Pink Lady", 21, 30, 3, 20, 1, PrepMethod.Shaken, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("gin", .50, .70), Band("lemon", .15, .32), Band("grenadine", .08, .25)),
                Cocktail("bahama_mama", "Bahama Mama", 21, 30, 3, 20, 1, PrepMethod.Shaken, "highball",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, so), new PatternRequirement(1, sw) },
                    locked: true,
                    Band("rum", .30, .50), Band("pineapple", .25, .45), Band("coffee_liqueur", .10, .25),
                    Band("lemon", .05, .18)),

                // Very hard — and the first drinks that ask for a BRAND rather than a
                // style. A Martinez poured from the well gin is a gin drink that missed:
                // the band counts only what came out of a good bottle, so it never fills
                // and the glass reads as nothing in particular. The author's rule
                // (2026-08-02): you cannot make a quality cocktail with cheap spirits.
                Cocktail("martinez", "Martinez", 28, 35, 3, 25, 2, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(2, sw) },
                    locked: true,
                    TopShelf("gin", .50, .70, 2), Band("vermouth", .22, .42), Band("triple_sec", .05, .18)),
                Cocktail("boulevardier", "Boulevardier", 28, 35, 3, 25, 2, PrepMethod.Stirred, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    locked: true,
                    TopShelf("bourbon", .28, .42, 2), Band("vermouth", .28, .42), Band("amaro", .28, .42)),
                Cocktail("rosita", "Rosita", 28, 35, 3, 25, 2, PrepMethod.Stirred, "rocks",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(1, sw), new PatternRequirement(1, bi) },
                    locked: true,
                    TopShelf("tequila", .28, .42, 2), Band("vermouth", .28, .42), Band("amaro", .28, .42)),
                Cocktail("el_presidente", "El Presidente", 28, 35, 3, 25, 2, PrepMethod.Stirred, "coupe",
                    new[] { new PatternRequirement(1, s), new PatternRequirement(3, sw) },
                    locked: true,
                    TopShelf("rum", .45, .65, 2), Band("vermouth", .20, .38), Band("triple_sec", .06, .18),
                    Band("grenadine", .03, .12)),
                // The capstone: the only drink that wants the TOP shelf, and it wants two.
                Cocktail("vesper", "Vesper", 29, 40, 4, 25, 2, PrepMethod.Stirred, "martini",
                    new[] { new PatternRequirement(2, s), new PatternRequirement(1, sw) },
                    locked: true,
                    TopShelf("gin", .50, .70, 3), TopShelf("vodka", .15, .32, 2),
                    Band("vermouth", .06, .18)),
            };
        }

        private static RatioRequirement Band(string style, double min, double max) =>
            new RatioRequirement(style, min, max);

        /// <summary>A band only a bottle at <paramref name="minTier"/> or better can fill.</summary>
        private static RatioRequirement TopShelf(string style, double min, double max, int minTier) =>
            new RatioRequirement(style, min, max, minTier);

        private static RecipeDefinition Cocktail(string id, string name, int rank,
            int baseFlavor, int baseMult, int flavorPerLevel, int multPerLevel,
            PrepMethod prep, string glassId,
            PatternRequirement[] requirements, bool locked, params RatioRequirement[] ratios) =>
            new RecipeDefinition(id, name, rank, baseFlavor, baseMult, flavorPerLevel, multPerLevel,
                requirements, ratioRequirements: ratios, locked: locked, prep: prep, glassId: glassId);
    }
}
