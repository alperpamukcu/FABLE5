using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// v2 design tokens (14_art_bible v2 §3, 16_ui_style_guide v2 §1). The locked 40-colour
    /// palette as 8 ramps × 5 steps, the ingredient type→ramp mapping (§5), a 4px spacing
    /// grid, and the number-colour roles (Money=Amber, Flavor=Cyan, Mult=Magenta — sacred).
    /// Shading = move along a ramp; never off-ramp. Every UI/scene colour comes from here.
    /// </summary>
    public static class UITheme
    {
        // ── the 40-colour palette (ramp[step], step 0 = darkest) ────────────────
        public static readonly Color[] Night = Ramp(0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160);
        public static readonly Color[] Magenta = Ramp(0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6);
        public static readonly Color[] Cyan = Ramp(0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3);
        public static readonly Color[] Amber = Ramp(0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B);
        public static readonly Color[] ViceRed = Ramp(0x3D1220, 0x6E1B32, 0xA62B44, 0xD9455C, 0xF27D8A);
        public static readonly Color[] ClubBlue = Ramp(0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0);
        public static readonly Color[] Lime = Ramp(0x16331B, 0x2A5926, 0x479938, 0x6FCC4B, 0xA8F077);
        public static readonly Color[] Cream = Ramp(0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5);
        // Beer's own ramp (GDD 21 §10): darker and browner than the spirits' amber, so a tap
        // key is never mistaken for a bottle key at a glance.
        public static readonly Color[] Malt = Ramp(0x3A2410, 0x6B4416, 0x9E6A1D, 0xC98F2B, 0xE6B959);

        // ── semantic roles ──────────────────────────────────────────────────────
        public static Color TextPrimary => Cream[4];      // Cream 5 on dark
        public static Color TextSecondary => Cream[3];
        public static Color TextOnAmber => Night[2];      // Night 3 on amber fills
        public static Color PrimaryAction => Amber[3];    // Amber 4, one per screen
        public static Color Selection => Cyan[3];         // Cyan 4 glow
        public static Color VipHeat => Magenta[3];        // Magenta 4
        public static Color Scrim => new Color(Night[0].r, Night[0].g, Night[0].b, 0.70f); // #0D0813 @70%

        // Sacred number colours (16 §2) — never reused elsewhere.
        public static Color Money => Amber[3];
        public static Color Flavor => Cyan[3];
        public static Color Mult => Magenta[3];

        // ── ingredient type → ramp (14 v2 §5); index by [step] ──────────────────
        public static readonly Dictionary<IngredientType, Color[]> TypeRamp = new Dictionary<IngredientType, Color[]>
        {
            [IngredientType.Spirit] = Amber,
            [IngredientType.Sour] = Lime,
            [IngredientType.Sweet] = Magenta,
            [IngredientType.Bitter] = ViceRed,
            [IngredientType.Bubbly] = Cyan,
            [IngredientType.Garnish] = Cream,
            [IngredientType.Beer] = Malt,
        };

        // ── emotion → ramp (GDD 19 §1); index by [step] ─────────────────────────
        // ── bottle style → signature colour (GDD 22 §1) ─────────────────────────
        // Each drink style owns one colour so the shelf reads at a glance: the vodka tag is
        // always ice-blue, the bourbon tag always amber, whatever the brand. Data-driven
        // styles fall back to the ingredient-type ramp when unmapped.
        private static readonly Dictionary<string, Color> StyleColors = new Dictionary<string, Color>
        {
            ["vodka"] = Ramp(0x6E93F0)[0],
            ["gin"] = Ramp(0x6FCC4B)[0],
            ["rum"] = Ramp(0xF27D8A)[0],
            ["bourbon"] = Ramp(0xE8A33D)[0],
            ["amaro"] = Ramp(0x4467CC)[0],
            ["vermouth"] = Ramp(0xC23283)[0],
            ["syrup"] = Ramp(0xFF7DC6)[0],
            ["lemon"] = Ramp(0xF5C97B)[0],
            ["ginger"] = Ramp(0xC9822B)[0],
            ["soda"] = Ramp(0x7DF0E3)[0],
            ["mint"] = Ramp(0xA8F077)[0],
            ["olive"] = Ramp(0x479938)[0],
            ["lager"] = Ramp(0xE6B959)[0],
            ["stout"] = Ramp(0x4A2E1C)[0],
            ["pale_ale"] = Ramp(0xD98A2B)[0],
            // v5 P10's seven new styles. Each is pushed away from the hue its aisle-mates
            // already own — tequila off bourbon's amber, lime off gin's green, orange off
            // lemon's yellow — because these are read side by side on one shelf.
            ["tequila"] = Ramp(0xC7D64A)[0],
            ["triple_sec"] = Ramp(0xF0913D)[0],
            ["cola"] = Ramp(0x8A4A2E)[0],
            ["tonic"] = Ramp(0xBFE7F0)[0],
            ["energy"] = Ramp(0xD84FE0)[0],
            ["orange"] = Ramp(0xFF9A3C)[0],
            ["lime"] = Ramp(0x8FD44A)[0],
            ["cranberry"]      = (Color)new Color32(0xC8, 0x50, 0x66, 0xFF),
            ["coffee_liqueur"] = (Color)new Color32(0x6E, 0x4A, 0x36, 0xFF),
            ["pineapple"]      = (Color)new Color32(0xE2, 0xC8, 0x5A, 0xFF),
            ["grenadine"]      = (Color)new Color32(0xD4, 0x4A, 0x5E, 0xFF),
        };

        /// <summary>The style's signature colour; falls back to the type ramp.</summary>
        public static Color StyleColor(string style, IngredientType fallbackType) =>
            style != null && StyleColors.TryGetValue(style, out var c) ? c : TypeRamp[fallbackType][4];

        // ── bottle style → LIQUID colour (2026-07-23) ───────────────────────────
        // The shelf-tag StyleColor is a vivid identity hue (vodka ice-blue, gin green) for
        // reading the rail at a glance — but that is wrong for the drink itself: real vodka,
        // gin and soda are near-clear, and pouring them as saturated blue/green made every
        // mix read wrong. These are the colours the *liquid* actually is; clear spirits are
        // barely tinted, the rest carry their true tone. Mixed gamma-correctly in BlendLiquid.
        // ── the table, rewritten 2026-08-04 ────────────────────────────────────────
        //
        // These are TRANSMISSION colours, not reflection colours. They are drawn at 0.52-0.86
        // alpha over a dark bar with light behind them, so what each one has to be is the
        // colour that gets THROUGH the drink — not the colour a bottle shows on a shelf. That
        // single shift is what fixes the dark end: cola, coffee liqueur and amaro were painted
        // as they look on a shelf, so at a splash they composited to within a few units of the
        // background and read as holes in the glass rather than as drinks.
        //
        // Separation is by VALUE first and hue second, because at 40px through semi-
        // transparency hue collapses and value survives. The warm family is an explicit ladder
        // — ginger, lager, orange, pale ale, rum, bourbon, cranberry, amaro, cola, vermouth,
        // grenadine, coffee, stout — and hue only splits same-value pairs (vermouth's plum
        // against cola's russet, pineapple's gold against energy's chartreuse). Where the old
        // table had four bottles inside one narrow amber band and two identical reds, no two
        // neighbours now share both axes.
        //
        // The near-clears stay near-clear and are allowed to converge, because they genuinely
        // do — a vodka and a soda in a glass ARE the same picture, and the gauge names them in
        // words. They start faintly WARM so VisibleLiquid's cool cast lands them neutral
        // instead of blue, which is what made a vodka read as a blue drink.
        //
        // What was most wrong, and why it mattered: syrup was #E36FA0, the shelf tag's
        // bubblegum pink leaking into the liquid table, and since syrup is in most of the
        // sours it was dragging a third of the menu pink (it is the bottle in the author's
        // screenshot). Simple syrup is clear-to-honey: it adds thickness, not colour.
        private static readonly Dictionary<string, Color> LiquidColors = new Dictionary<string, Color>
        {
            // clear spirits and mixers — near-white, each with a whisper of its own hue
            ["vodka"]      = (Color)new Color32(0xF1, 0xEF, 0xEA, 0xFF), // water; the zero point
            ["soda"]       = (Color)new Color32(0xE9, 0xF2, 0xF4, 0xFF), // aerated water, a shade cooler
            ["tonic"]      = (Color)new Color32(0xD8, 0xEA, 0xFA, 0xFF), // quinine really does glow cold
            ["gin"]        = (Color)new Color32(0xDC, 0xEE, 0xD4, 0xFF), // juniper's faint green
            ["tequila"]    = (Color)new Color32(0xE3, 0xEA, 0xBE, 0xFF), // blanco's agave straw
            ["triple_sec"] = (Color)new Color32(0xF6, 0xE0, 0xAA, 0xFF), // orange peel, the warm clear
            ["syrup"]      = (Color)new Color32(0xF0, 0xDF, 0xA8, 0xFF), // 2:1 bar syrup: pale honey
            // the yellows and greens, pushed apart — three of these used to be one colour
            ["lemon"]      = (Color)new Color32(0xED, 0xE8, 0x8A, 0xFF), // cloudy, green-leaning
            ["pineapple"]  = (Color)new Color32(0xF4, 0xD0, 0x50, 0xFF), // pressed, warm and rich
            ["energy"]     = (Color)new Color32(0xCD, 0xE4, 0x36, 0xFF), // nothing in nature is this
            ["lime"]       = (Color)new Color32(0xBF, 0xD6, 0x6A, 0xFF), // cordial-cloudy, a step down
            ["mint"]       = (Color)new Color32(0x93, 0xD1, 0x68, 0xFF), // a true leaf green
            ["olive"]      = (Color)new Color32(0xA8, 0xAE, 0x64, 0xFF), // brine: murky salt water
            // the ambers, spaced by value so the four of them stop being one band
            ["ginger"]     = (Color)new Color32(0xE2, 0xBE, 0x72, 0xFF), // ginger beer is hazy straw
            ["lager"]      = (Color)new Color32(0xE7, 0xAF, 0x3C, 0xFF), // clear golden straw
            ["orange"]     = (Color)new Color32(0xF6, 0x9B, 0x26, 0xFF), // pulp and all
            ["pale_ale"]   = (Color)new Color32(0xD1, 0x85, 0x25, 0xFF), // copper, darker than lager
            ["rum"]        = (Color)new Color32(0xCA, 0x82, 0x32, 0xFF), // gold rum's honey amber
            ["bourbon"]    = (Color)new Color32(0xB3, 0x5F, 0x21, 0xFF), // barrel: darker and redder
            // the reds and the darks, lifted to their BACKLIT values
            ["cranberry"]  = (Color)new Color32(0xCF, 0x31, 0x59, 0xFF), // jewel crimson, glows lit
            ["amaro"]      = (Color)new Color32(0xCA, 0x34, 0x26, 0xFF), // aperitivo scarlet, not oxblood
            ["vermouth"]   = (Color)new Color32(0x92, 0x30, 0x33, 0xFF), // rosso: mahogany, not rosé
            ["grenadine"]  = (Color)new Color32(0xA9, 0x12, 0x34, 0xFF), // a syrup: must SINK through juice
            ["cola"]       = (Color)new Color32(0x86, 0x37, 0x15, 0xFF), // held to the light it is russet
            ["coffee_liqueur"] = (Color)new Color32(0x5B, 0x2E, 0x17, 0xFF), // treacle mocha
            ["stout"]      = (Color)new Color32(0x4C, 0x23, 0x14, 0xFF), // the floor; its head is the contrast
        };

        /// <summary>The head on a draught (GDD 21 §10). Off-white, and creamier on the dark
        /// beers, so the foam reads as part of the same pint rather than a white bar on top.</summary>
        public static Color HeadColor(string style) =>
            style == "stout" ? (Color)new Color32(0xE4, 0xD2, 0xB4, 0xFF)
                             : (Color)new Color32(0xF7, 0xF0, 0xDE, 0xFF);

        /// <summary>
        /// The palest a liquid may be drawn before it stops being visible, and what it is pulled
        /// toward. Vodka, gin, soda and tonic are near-white in <see cref="LiquidColors"/> because
        /// that is what they are — but a near-white drink behind pale glass, or in a glass over a
        /// lit bar, is nothing at all: the player could not tell a half-full bottle of vodka from
        /// an empty one. They are given the least colour that separates them, and it is blue
        /// because that is what water already reads as under bar light, and because it is the one
        /// cast that cannot be mistaken for a juice, a beer or a foam head.
        /// </summary>
        /// <remarks>
        /// The cast was 0.55 until 2026-08-03, and at that strength a vodka read as a light
        /// BLUE drink rather than a clear one — the author's report, and fairly: the tint was
        /// carrying the whole job of making a clear spirit visible. It does not have to any
        /// more. The drink is genuinely translucent now (see <see cref="DrinkAlpha"/>), which
        /// is what "clear" actually looks like, and its edge catches the light in its own
        /// colour, so a glass of vodka reads as a glass with something in it without being
        /// painted blue. The cast stays, at a third of the strength, for the case it was
        /// written for: a pale drink against a pale glass on a lit bar.
        /// </remarks>
        private const float ClearAbove = 0.80f, ClearFull = 0.96f, ClearCast = 0.20f;
        private static readonly Color ClearTint = (Color)new Color32(0x86, 0xC2, 0xE4, 0xFF);

        /// <summary>
        /// A liquid you can see. Anything with real colour of its own is returned untouched;
        /// only the near-clear ones are tinted, and the paler they are the more they take.
        /// </summary>
        /// <summary>
        /// How solid a drink is drawn, against how much of it there is. A splash in the bottom
        /// of a glass is a thin film you see the far wall through; a full glass is depth, and
        /// depth is what absorbs light. Drawing both at one alpha is what made every liquid
        /// read as matte paint (the author, 2026-08-03), and it is also the honest answer to
        /// "the colour should change with how much went in": it does, in the two ways a real
        /// drink's does — the MIX moves with each ingredient's share, and the DEPTH moves with
        /// the level.
        /// </summary>
        private const float ThinDrink = 0.52f, DeepDrink = 0.86f;

        public static float DrinkAlpha(double fillFraction) =>
            Mathf.Lerp(ThinDrink, DeepDrink, Mathf.Sqrt(Mathf.Clamp01((float)fillFraction)));

        /// <summary>
        /// What an EMPTY vessel's liquid is. It has to be something — callers tint sprites and
        /// materials with it unconditionally — but it must not be the most solid value in the
        /// game, which is exactly what it was: Cream[3] came straight out of the palette with
        /// alpha 1, skipping <see cref="DrinkAlpha"/> entirely and landing at the fluid's 0.97
        /// ceiling. A tin that read empty was drawn more opaque than a brimful real drink, so
        /// the one frame where a stale colour survived looked like a slab of paint rather than
        /// like nothing (the author's House Syrup screenshot, 2026-08-03).
        /// </summary>
        public static Color Nothing =>
            new Color(Cream[3].r, Cream[3].g, Cream[3].b, 0.10f);

        /// <summary>THE drink's colour — one function for every scene (the author,
        /// 2026-08-02: the liquid on the counter and the liquid being poured must read
        /// as the same liquid). Ingredients' true liquid colours, blended by share.</summary>
        public static Color DrinkColor(Shelf shelf, GlassContents glass)
        {
            if (glass == null || glass.IsEmpty) return Nothing;
            var parts = new System.Collections.Generic.List<(string, IngredientType, float)>();
            foreach (var id in glass.Ingredients)
            {
                var card = shelf != null ? shelf.Find(id)?.Ingredient : null;
                parts.Add((card?.Info?.Style, card?.Type ?? IngredientType.Spirit,
                    (float)glass.RatioOf(id)));
            }
            return BlendLiquid(parts, Nothing, DrinkAlpha(glass.FillFraction));
        }

        public static Color VisibleLiquid(Color c)
        {
            float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            if (luma <= ClearAbove) return c;
            float t = Mathf.InverseLerp(ClearAbove, ClearFull, luma);
            var cast = Color.Lerp(c, ClearTint, t * t * (3f - 2f * t) * ClearCast);
            return new Color(cast.r, cast.g, cast.b, c.a);
        }

        /// <summary>The colour of the actual liquid for a style; clear spirits read pale — but
        /// never so pale they vanish (see <see cref="VisibleLiquid"/>).
        /// Falls back to a soft body tone of the ingredient type when the style is unmapped.</summary>
        public static Color LiquidColor(string style, IngredientType fallbackType) =>
            VisibleLiquid(style != null && LiquidColors.TryGetValue(style, out var c)
                ? c : TypeRamp[fallbackType][3]);

        /// <summary>
        /// How much COLOUR a liquid actually carries — its pigment, as opposed to its share.
        ///
        /// A clear spirit is not white paint; it is a filter that passes nearly everything
        /// through. Averaging it into a mix as if it were pigment is what made a Black Russian
        /// compute to a pale blue-grey and a rum-and-coke to a mid grey: the vodka's near-white
        /// was outvoting the coffee liqueur's brown, when physically it should only have
        /// diluted it. Distance from white, taken as the larger of "how dark" and "how
        /// chromatic", so a bright orange juice counts as fully pigmented while a bright vodka
        /// counts as almost nothing.
        /// </summary>
        private static float Pigment(Color c)
        {
            float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            float chroma = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return Mathf.Clamp01(Mathf.Max(1f - luma, chroma));
        }

        /// <summary>Blends the poured ingredients into one liquid colour, weighted by share AND
        /// by pigment (see <see cref="Pigment"/>), and mixed in LINEAR space so a two-part drink
        /// reads bright and clean instead of the muddy mid-grey a straight sRGB average
        /// produces (2026-07-23).</summary>
        public static Color BlendLiquid(IEnumerable<(string style, IngredientType type, float weight)> parts,
            Color empty, float alpha)
        {
            float r = 0, g = 0, b = 0, tot = 0;
            // A first pass in plain shares, kept as the answer for a glass holding nothing but
            // clear spirits — there the pigment weights all collapse toward zero and the ratio
            // between them stops meaning anything, so the honest answer is the plain average,
            // which for a glass of clear drinks is correctly near-clear.
            float pr = 0, pg = 0, pb = 0, ptot = 0;
            foreach (var (style, type, weight) in parts)
            {
                if (weight <= 0) continue;
                Color srgb = LiquidColor(style, type);
                Color lin = srgb.linear;
                float w = weight * Pigment(srgb);
                r += lin.r * w; g += lin.g * w; b += lin.b * w; tot += w;
                pr += lin.r * weight; pg += lin.g * weight; pb += lin.b * weight; ptot += weight;
            }
            if (tot <= 0.004f && ptot > 0)
            {
                var clear = new Color(pr / ptot, pg / ptot, pb / ptot, 1f).gamma;
                return new Color(clear.r, clear.g, clear.b, alpha);
            }
            // The empty path honours the caller's alpha too. It used to return the sentinel
            // untouched, which meant this one exit opted out of the transparency law every
            // other exit obeys — and since the sentinel came from the palette at alpha 1, the
            // "no drink" case was drawn more solidly than any drink.
            if (tot <= 0) return new Color(empty.r, empty.g, empty.b, Mathf.Min(empty.a, alpha));
            var mixed = new Color(r / tot, g / tot, b / tot, 1f).gamma;
            return new Color(mixed.r, mixed.g, mixed.b, alpha);
        }

        /// <summary>
        /// Ink that can be read on a given fill. Both pour gauges printed their percentages in
        /// Night[0] on a segment filled with the drink's own colour, which is right for the
        /// pale drinks and invisible on stout, coffee liqueur, cola and amaro — and the gauge
        /// is the stage's load-bearing readout, the number the recipe bands are graded on.
        /// </summary>
        public static Color InkOn(Color fill) =>
            0.2126f * fill.r + 0.7152f * fill.g + 0.0722f * fill.b > 0.45f ? Night[0] : Cream[4];

        /// <summary>Body/fill colour for an ingredient type (ramp step 3).</summary>
        public static Color TypeFill(IngredientType t) => TypeRamp[t][3];

        /// <summary>Darkest step of a type's ramp — used for the 1px outline (§3).</summary>
        public static Color TypeOutline(IngredientType t) => TypeRamp[t][0];

        // ── spacing grid (16 §1) ─────────────────────────────────────────────────
        public const int Grid = 4;
        public static float Snap(float v) => Mathf.Round(v / Grid) * Grid;
        public static Vector2 Snap(Vector2 v) => new Vector2(Snap(v.x), Snap(v.y));

        private static Color[] Ramp(params int[] hexes)
        {
            var r = new Color[hexes.Length];
            for (int i = 0; i < hexes.Length; i++) r[i] = Hex(hexes[i]);
            return r;
        }

        public static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
