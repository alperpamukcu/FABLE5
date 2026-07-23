using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.DebugUI
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
        };

        // ── emotion → ramp (GDD 19 §1); index by [step] ─────────────────────────
        // Each emotion owns one ramp so a stat is identifiable by colour alone, before
        // any text is read. Deliberately overlaps the type ramps: an ingredient's colour
        // hints at what it moves, without promising it (charges are per-card, not per-type).
        public static readonly Dictionary<Emotion, Color[]> EmotionRamp = new Dictionary<Emotion, Color[]>
        {
            [Emotion.Anger] = ViceRed,
            [Emotion.Sadness] = ClubBlue,
            [Emotion.Fatigue] = Amber,
            [Emotion.Excitement] = Cyan,
            [Emotion.Heartbreak] = Magenta,
            [Emotion.Anxiety] = Lime,
        };

        public static Color EmotionFill(Emotion e) => EmotionRamp[e][3];
        public static Color EmotionDim(Emotion e) => EmotionRamp[e][1];

        /// <summary>Three-letter tag for the ID card's stat rows — the card is narrow.</summary>
        public static string EmotionTag(Emotion e)
        {
            switch (e)
            {
                case Emotion.Anger: return "ANG";
                case Emotion.Sadness: return "SAD";
                case Emotion.Fatigue: return "TIR";
                case Emotion.Excitement: return "EXC";
                case Emotion.Heartbreak: return "HRT";
                default: return "ANX";
            }
        }

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
        };

        /// <summary>The colour of the actual liquid for a style; clear spirits read pale.
        /// Falls back to a soft body tone of the ingredient type when the style is unmapped.</summary>
        public static Color LiquidColor(string style, IngredientType fallbackType) =>
            style != null && LiquidColors.TryGetValue(style, out var c) ? c : TypeRamp[fallbackType][3];

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
