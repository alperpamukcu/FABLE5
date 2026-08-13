using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The little picture of a drink that rides on an order tag and the ID card (v5 P13).
    ///
    /// It is drawn here rather than shipped as art, because a drink icon is not a portrait —
    /// it is a statement of what is in the glass, and everything needed to make that statement
    /// is already data: <see cref="GlasswareDefinition.Profile"/> is the silhouette the fluid
    /// solver fills, and <see cref="RecipeDefinition.RatioRequirements"/> is what goes in it.
    /// Drawing from those two means the icon can never drift from the drink, and a recipe added
    /// to recipes.json gets its icon the same day instead of waiting on a generator that cannot
    /// spell. It also keeps the project's rule: chrome is procedural, art is for illustration.
    ///
    /// One texture per recipe, built on first sight and kept.
    /// </summary>
    public static class DrinkIcon
    {
        /// <summary>Icon side in pixels. A whole multiple of 8, like the fonts.</summary>
        public const int Size = 32;

        private const float FillFraction = 0.76f;

        private static readonly Color Outline = new Color32(0x14, 0x0F, 0x14, 0xFF);
        private static readonly Color Wall = new Color32(0xD5, 0xE6, 0xEE, 0xA0);
        private static readonly Color Highlight = new Color32(0xFF, 0xFF, 0xFF, 0x7A);

        /// <summary>
        /// How tall and how wide a glass is drawn. The profile in glassware.json is the taper —
        /// it says a martini is a cone and a highball is a tube, but not that the highball is
        /// the tall one. That is proportion, which is presentation, so it lives here: at 32px a
        /// pint and a rocks have to differ by silhouette alone, because there is no room for a
        /// label and no second colour to spend.
        /// </summary>
        private readonly struct Shape
        {
            public readonly int Half, Floor, Rim;
            public readonly bool Stem;
            public Shape(int half, int floor, int rim, bool stem)
            { Half = half; Floor = floor; Rim = rim; Stem = stem; }
        }

        private static readonly Dictionary<string, Shape> Shapes = new Dictionary<string, Shape>
        {
            // Rims stop at 28 so the garnish, which rides two rows above the mouth, and the
            // outline above that both stay on the canvas.
            ["pint"] = new Shape(9, 2, 28, false),        // tall, heavy, a touch of taper
            ["highball"] = new Shape(7, 2, 28, false),    // the tall thin one
            ["rocks"] = new Shape(10, 4, 22, false),      // squat and wide
            ["martini"] = new Shape(11, 14, 27, true),    // a shallow cone on a stalk
            ["coupe"] = new Shape(10, 15, 26, true),      // shallower still, and round
        };

        private static readonly Shape DefaultShape = new Shape(8, 2, 27, false);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The icon for a recipe, drawn once and cached. Null only for a null recipe.</summary>
        public static Sprite For(RecipeDefinition recipe, IReadOnlyList<GlasswareDefinition> glassware)
        {
            if (recipe == null) return null;
            if (Cache.TryGetValue(recipe.Id, out var cached) && cached != null) return cached;

            var sprite = Draw(recipe, GlassFor(recipe, glassware));
            Cache[recipe.Id] = sprite;
            return sprite;
        }

        /// <summary>
        /// The glass a recipe is served in. Every shipped recipe names one, so this is normally
        /// a lookup; the fallback only covers a recipe authored without a glass, and guesses
        /// the shape it obviously wants — a pint for beer, a rocks for a drink poured alone.
        /// </summary>
        private static GlasswareDefinition GlassFor(
            RecipeDefinition recipe, IReadOnlyList<GlasswareDefinition> glassware)
        {
            if (glassware == null || glassware.Count == 0) return null;

            string wanted = string.IsNullOrEmpty(recipe.GlassId)
                ? (IsDraught(recipe) ? "pint" : recipe.ExactMixSize == 1 ? "rocks" : "highball")
                : recipe.GlassId;
            foreach (var g in glassware)
                if (g.Id == wanted) return g;
            return glassware[0];
        }

        private static bool IsDraught(RecipeDefinition recipe)
        {
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer) return true;
            foreach (var req in recipe.Requirements)
                foreach (var t in req.Types)
                    if (t == IngredientType.Beer) return true;
            return false;
        }

        /// <summary>
        /// What the drink looks like in the glass: its bands blended at their midpoints, which
        /// is the same proportion the judge grades a pour against. A recipe with no bands (the
        /// pre-pour shapes) falls back to its pattern's types, so it still reads as a colour
        /// rather than as a hole.
        /// </summary>
        private static Color LiquidFor(RecipeDefinition recipe)
        {
            var parts = new List<(string, IngredientType, float)>();
            foreach (var band in recipe.RatioRequirements)
                parts.Add((band.Style, band.Type, (float)((band.MinRatio + band.MaxRatio) * 0.5)));
            if (parts.Count == 0)
                foreach (var req in recipe.Requirements)
                    foreach (var t in req.Types)
                        parts.Add((null, t, req.Count / (float)req.Types.Count));

            return UITheme.BlendLiquid(parts, UITheme.Night[2], 1f);
        }

        /// <summary>The garnish riding on the rim, if the recipe asks for one by name.</summary>
        private static bool GarnishFor(RecipeDefinition recipe, out Color colour)
        {
            colour = default;
            foreach (var band in recipe.RatioRequirements)
            {
                if (band.IsStyleBand)
                {
                    switch (band.Style)
                    {
                        case "lemon": case "lime": case "orange": case "mint": case "olive":
                            colour = UITheme.StyleColor(band.Style, IngredientType.Garnish);
                            return true;
                    }
                    continue;
                }
                if (band.Type == IngredientType.Garnish)
                {
                    colour = UITheme.StyleColor(null, IngredientType.Garnish);
                    return true;
                }
            }
            return false;
        }

        private static Sprite Draw(RecipeDefinition recipe, GlasswareDefinition glass)
        {
            var px = new Color[Size * Size];                     // transparent by default
            var solid = new bool[Size * Size];

            IReadOnlyList<double> profile = glass?.Profile;
            Shape shape = glass != null && Shapes.TryGetValue(glass.Id, out var s) ? s : DefaultShape;

            // The foot and the stalk. Both are drawn as glass rather than left hollow — an
            // outline with nothing inside it reads as a hole at this size.
            if (shape.Stem)
            {
                for (int y = 0; y < 2; y++) Fill(px, solid, y, 6, Wall);
                for (int y = 2; y < shape.Floor; y++) Fill(px, solid, y, 1, Wall);
            }

            int span = Mathf.Max(1, shape.Rim - shape.Floor);
            int fillTop = shape.Floor + Mathf.RoundToInt(span * FillFraction);
            Color liquid = LiquidFor(recipe);
            bool draught = IsDraught(recipe);
            Color head = UITheme.HeadColor(draught ? "lager" : null);

            for (int y = shape.Floor; y <= shape.Rim; y++)
            {
                int half = HalfWidth(profile, (y - shape.Floor) / (float)span, shape.Half);
                int left = Centre(half), right = left + half * 2 - 1;
                for (int x = left; x <= right; x++) Mark(solid, x, y);

                if (y <= fillTop)
                {
                    // A pint's head is the top of the beer, not a lid on it (GDD 21 §10).
                    bool foam = draught && y > fillTop - 4;
                    Color body = foam ? head : liquid;
                    if (y == fillTop && !foam) body = Color.Lerp(liquid, Color.white, 0.22f);
                    if (y == shape.Floor) body = Color.Lerp(liquid, Outline, 0.4f);
                    for (int x = left; x <= right; x++) Put(px, x, y, body);
                }
                else
                {
                    // Above the drink the glass is EMPTY, so only its walls are drawn and the
                    // middle stays transparent. Tinting the whole area instead reads as a grey
                    // lid sitting on the drink, which is what the first pass looked like.
                    Put(px, left, y, Wall);
                    Put(px, right, y, Wall);
                }

                // The mouth, and one lit column down the inside of the left wall — between them
                // that is the entire reason a shape this small reads as glass at all.
                if (y == shape.Rim) for (int x = left; x <= right; x++) Put(px, x, y, Wall);
                else if (y > shape.Floor) Put(px, left + 1, y, Highlight);
            }

            if (GarnishFor(recipe, out var garnish))
            {
                int half = HalfWidth(profile, 1f, shape.Half);
                int gx = Centre(half) + half * 2 - 3;
                for (int y = shape.Rim; y <= shape.Rim + 2; y++)
                    for (int x = gx; x < gx + 3; x++)
                    {
                        Put(px, x, y, garnish);
                        Mark(solid, x, y);
                    }
            }

            Trace(px, solid);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
                name = $"icon_{recipe.Id}",
            };
            tex.SetPixels(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = $"icon_{recipe.Id}";
            return sprite;
        }

        /// <summary>Half-width in pixels at <paramref name="t"/> up the glass (0 = floor).</summary>
        private static int HalfWidth(IReadOnlyList<double> profile, float t, int maxHalf)
        {
            if (profile == null || profile.Count == 0) return maxHalf;
            float at = Mathf.Clamp01(t) * (profile.Count - 1);
            int i = Mathf.Min((int)at, profile.Count - 2);
            float w = Mathf.Lerp((float)profile[i], (float)profile[i + 1], at - i);
            return Mathf.Max(1, Mathf.RoundToInt(w * maxHalf));
        }

        private static int Centre(int half) => Size / 2 - half;

        private static void Fill(Color[] px, bool[] solid, int y, int half, Color c)
        {
            for (int x = Centre(half); x < Centre(half) + half * 2; x++)
            {
                Mark(solid, x, y);
                Put(px, x, y, c);
            }
        }

        private static void Mark(bool[] solid, int x, int y)
        {
            if (x >= 0 && x < Size && y >= 0 && y < Size) solid[y * Size + x] = true;
        }

        private static void Put(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) return;
            int i = y * Size + x;
            px[i] = c.a >= 1f ? c : Blend(px[i], c);
        }

        private static Color Blend(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0f) return new Color(0, 0, 0, 0);
            return new Color(
                (over.r * over.a + under.r * under.a * (1f - over.a)) / a,
                (over.g * over.a + under.g * under.a * (1f - over.a)) / a,
                (over.b * over.a + under.b * under.a * (1f - over.a)) / a, a);
        }

        /// <summary>Wraps the silhouette in a one-pixel dark line — the outline the bottles and
        /// the keg carry, so the icon sits in the same set as the rest of the art.</summary>
        private static void Trace(Color[] px, bool[] solid)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (solid[y * Size + x]) continue;
                    if (Neighbour(solid, x - 1, y) || Neighbour(solid, x + 1, y) ||
                        Neighbour(solid, x, y - 1) || Neighbour(solid, x, y + 1))
                        px[y * Size + x] = Outline;
                }
        }

        private static bool Neighbour(bool[] solid, int x, int y) =>
            x >= 0 && x < Size && y >= 0 && y < Size && solid[y * Size + x];
    }
}
