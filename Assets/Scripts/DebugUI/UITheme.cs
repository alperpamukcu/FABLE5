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
