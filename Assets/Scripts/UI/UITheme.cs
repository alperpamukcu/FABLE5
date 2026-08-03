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
        private static readonly Dictionary<string, Color> LiquidColors = new Dictionary<string, Color>
        {
            ["vodka"]    = (Color)new Color32(0xE9, 0xEE, 0xF6, 0xFF),   // clear, a whisper of blue
            ["gin"]      = (Color)new Color32(0xEA, 0xF2, 0xE9, 0xFF),   // clear, a whisper of green
            ["soda"]     = (Color)new Color32(0xE6, 0xF2, 0xF3, 0xFF),   // clear fizz
            ["rum"]      = (Color)new Color32(0xC6, 0x7F, 0x35, 0xFF),   // golden amber
            ["bourbon"]  = (Color)new Color32(0xB0, 0x6A, 0x22, 0xFF),   // amber
            ["amaro"]    = (Color)new Color32(0x7A, 0x2C, 0x2A, 0xFF),   // dark red-brown
            ["vermouth"] = (Color)new Color32(0xA9, 0x4E, 0x5C, 0xFF),   // rosé
            ["syrup"]    = (Color)new Color32(0xE3, 0x6F, 0xA0, 0xFF),   // pink
            ["lemon"]    = (Color)new Color32(0xED, 0xD8, 0x66, 0xFF),   // pale citrus
            ["ginger"]   = (Color)new Color32(0xD3, 0x92, 0x3C, 0xFF),   // golden
            ["mint"]     = (Color)new Color32(0xA6, 0xDE, 0x80, 0xFF),   // pale green
            ["olive"]    = (Color)new Color32(0xB7, 0xBE, 0x6A, 0xFF),   // brine
            ["lager"]    = (Color)new Color32(0xE8, 0xB0, 0x3E, 0xFF),   // pale gold
            ["stout"]    = (Color)new Color32(0x2A, 0x18, 0x10, 0xFF),   // near black
            ["pale_ale"] = (Color)new Color32(0xD4, 0x82, 0x24, 0xFF),   // deep copper
            // v5 P10's seven. Tequila and triple sec are clear in the glass whatever their
            // shelf tag says; the mixers carry the colour that actually survives a pour.
            ["tequila"]    = (Color)new Color32(0xEC, 0xF0, 0xDE, 0xFF), // clear, faintly green
            ["triple_sec"] = (Color)new Color32(0xF2, 0xEE, 0xE2, 0xFF), // clear
            ["cola"]       = (Color)new Color32(0x4A, 0x24, 0x14, 0xFF), // dark caramel
            ["tonic"]      = (Color)new Color32(0xE4, 0xF1, 0xF6, 0xFF), // clear, a whisper of blue
            ["energy"]     = (Color)new Color32(0xE7, 0xD8, 0x4A, 0xFF), // acid yellow
            ["orange"]     = (Color)new Color32(0xF2, 0x8E, 0x22, 0xFF), // juice, pulp and all
            ["lime"]       = (Color)new Color32(0xCD, 0xE0, 0x72, 0xFF), // pale green-yellow
            // The P16 wave (2026-07-31): the four bottles the new classics need.
            ["cranberry"]      = (Color)new Color32(0xB4, 0x2C, 0x48, 0xFF), // deep tart red
            ["coffee_liqueur"] = (Color)new Color32(0x38, 0x22, 0x18, 0xFF), // near-black coffee
            ["pineapple"]      = (Color)new Color32(0xEF, 0xD4, 0x52, 0xFF), // pressed sunshine
            ["grenadine"]      = (Color)new Color32(0xC8, 0x1E, 0x3C, 0xFF), // pomegranate ruby
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

        /// <summary>THE drink's colour — one function for every scene (the author,
        /// 2026-08-02: the liquid on the counter and the liquid being poured must read
        /// as the same liquid). Ingredients' true liquid colours, blended by share.</summary>
        public static Color DrinkColor(Shelf shelf, GlassContents glass)
        {
            if (glass == null || glass.IsEmpty) return Cream[3];
            var parts = new System.Collections.Generic.List<(string, IngredientType, float)>();
            foreach (var id in glass.Ingredients)
            {
                var card = shelf != null ? shelf.Find(id)?.Ingredient : null;
                parts.Add((card?.Info?.Style, card?.Type ?? IngredientType.Spirit,
                    (float)glass.RatioOf(id)));
            }
            return BlendLiquid(parts, Cream[3], DrinkAlpha(glass.FillFraction));
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

        /// <summary>Blends the poured ingredients into one liquid colour, weighted by share and
        /// mixed in LINEAR space so a two-part drink reads bright and clean instead of the muddy
        /// mid-grey a straight sRGB average produces (2026-07-23).</summary>
        public static Color BlendLiquid(IEnumerable<(string style, IngredientType type, float weight)> parts,
            Color empty, float alpha)
        {
            float r = 0, g = 0, b = 0, tot = 0;
            foreach (var (style, type, weight) in parts)
            {
                if (weight <= 0) continue;
                Color lin = LiquidColor(style, type).linear;
                r += lin.r * weight; g += lin.g * weight; b += lin.b * weight; tot += weight;
            }
            if (tot <= 0) return empty;
            var mixed = new Color(r / tot, g / tot, b / tot, 1f).gamma;
            return new Color(mixed.r, mixed.g, mixed.b, alpha);
        }

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
