using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// Where the liquid goes inside a shelf bottle, measured off the bottle's own pixels.
    ///
    /// The bottles used to be drawn with their contents painted in at a fixed height, so a bottle
    /// looked the same whether it was full or nearly dry. They were reshot empty (2026-07-31), and
    /// the level became the game's to draw — which means the game has to know where the glass
    /// cavity is. Rather than hand-tune nineteen rectangles that would go stale the next time a
    /// bottle is reshot, this reads the sprite: <see cref="GlassArt"/>'s bargain, from the other
    /// side. GlassArt draws a shape and reports its interior; BottleArt is handed a shape and works
    /// the interior out.
    ///
    /// Three tests decide whether a pixel is cavity, and each one throws away a different thing:
    ///   * eroded from the silhouette — drops the wall and the outline, so liquid never touches
    ///     the black edge;
    ///   * the body band — drops the neck, the cap and anything perched on the shoulder, so a
    ///     nearly-full bottle does not fill its own cork;
    ///   * saturated *in company* — drops labels, crests and wax tops, which are on the OUTSIDE of
    ///     the glass and must stay in front of the drink. The company matters: a label is a large
    ///     block of colour, while a lone saturated pixel is a reflection in the glass and is
    ///     cavity. Testing each pixel alone speckled the drink; testing the neighbourhood does not.
    /// </summary>
    public static class BottleArt
    {
        /// <summary>Wall inset, in pixels: how far inside the silhouette the liquid starts.</summary>
        private const int Wall = 3;

        /// <summary>A row is "body" if it is at least this wide compared to the widest row.</summary>
        private const float BodyWidth = 0.80f;

        /// <summary>Chroma at or above this (0–255) counts as a printed colour, not glass.</summary>
        private const int Saturated = 40;

        /// <summary>Half-width of the neighbourhood the company test looks at.</summary>
        private const int Company = 6;

        /// <summary>Fraction of that neighbourhood that must be saturated to read as a label.</summary>
        private const float LabelDensity = 0.43f;

        /// <summary>
        /// How solid the drink is drawn. A flat opaque block reads as paint, not liquid — the
        /// bottle's own highlights and the shading down its shoulders have to carry through it for
        /// the eye to put the drink BEHIND the glass rather than on it.
        /// </summary>
        private const float LiquidAlpha = 0.70f;

        /// <summary>A bottle's sprite together with the cavity the drink is drawn into.</summary>
        public readonly struct Piece
        {
            /// <summary>The bottle as drawn — empty glass, cap seated.</summary>
            public readonly Sprite Sprite;

            /// <summary>The cavity, white on nothing, in the same rect as <see cref="Sprite"/>.</summary>
            public readonly Sprite Fill;

            /// <summary>Width over height, for sizing from a height alone.</summary>
            public readonly float Aspect;

            /// <summary>Bottom of the cavity, as a fraction of the rect. 0 = bottom of the sprite.</summary>
            public readonly float FloorY;

            /// <summary>Top of the cavity, as a fraction of the rect.</summary>
            public readonly float RimY;

            public Piece(Sprite sprite, Sprite fill, float aspect, float floorY, float rimY)
            {
                Sprite = sprite;
                Fill = fill;
                Aspect = aspect;
                FloorY = floorY;
                RimY = rimY;
            }

            public bool Exists => Sprite != null;

            /// <summary>
            /// The <see cref="Image.fillAmount"/> that leaves the bottle <paramref name="fraction"/>
            /// full. A vertical fill measures the whole rect, so the cavity's own span is mapped
            /// into it — otherwise "half a bottle" would mean half the picture, cap included.
            /// </summary>
            public float FillAmount(float fraction) =>
                FloorY + (RimY - FloorY) * Mathf.Clamp01(fraction);
        }

        private static readonly Dictionary<string, Piece> Cache = new Dictionary<string, Piece>();

        /// <summary>The bottle for a shelf style ("vodka", "gin", …), measured once and kept.</summary>
        public static Piece For(string style)
        {
            if (string.IsNullOrEmpty(style)) return default;
            if (Cache.TryGetValue(style, out var piece)) return piece;
            piece = Measure(ItemArt.Bottle(style));
            Cache[style] = piece;
            return piece;
        }

        /// <summary>
        /// Hangs the drink inside a bottle image. The liquid goes IN FRONT of the glass because the
        /// glass is painted opaque — but the cavity mask has holes where the labels are, so the
        /// label still reads as being on the outside, which is where it is.
        /// Returns null when the style has no art; the caller then has nothing to fill.
        /// </summary>
        public static Image AddLiquid(RectTransform bottleArt, string style, IngredientType type)
        {
            var piece = For(style);
            if (!piece.Exists || piece.Fill == null) return null;

            var go = new GameObject("Liquid", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(bottleArt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite = piece.Fill;
            img.preserveAspect = true;          // lands exactly where the bottle does: same rect, same aspect
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Vertical;
            img.fillOrigin = (int)Image.OriginVertical.Bottom;
            img.fillAmount = 0f;
            var c = UITheme.LiquidColor(style, type);
            img.color = new Color(c.r, c.g, c.b, LiquidAlpha);
            return img;
        }

        private static Piece Measure(Sprite bottle)
        {
            if (bottle == null) return default;
            var tex = bottle.texture;
            if (tex == null || !tex.isReadable) return new Piece(bottle, null, 1f, 0f, 1f);

            var rect = bottle.rect;
            int w = (int)rect.width, h = (int)rect.height;
            if (w <= 0 || h <= 0) return new Piece(bottle, null, 1f, 0f, 1f);

            var px = tex.GetPixels((int)rect.x, (int)rect.y, w, h);
            var opaque = new bool[w * h];
            var saturated = new bool[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                opaque[i] = c.a >= 0.5f;
                if (!opaque[i]) continue;
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                saturated[i] = (max - min) * 255f >= Saturated;
            }

            // Summed-area tables turn both neighbourhood tests into four lookups, which keeps the
            // whole shelf's worth of measuring inside one frame at load.
            var opaqueSum = Integral(opaque, w, h);
            var labelSum = Integral(saturated, w, h);

            int widest = 0;
            var rowWidth = new int[h];
            for (int y = 0; y < h; y++)
            {
                int n = 0;
                for (int x = 0; x < w; x++) if (opaque[y * w + x]) n++;
                rowWidth[y] = n;
                if (n > widest) widest = n;
            }
            if (widest == 0) return new Piece(bottle, null, (float)w / h, 0f, 1f);

            int bodyLow = h, bodyHigh = -1;
            for (int y = 0; y < h; y++)
            {
                if (rowWidth[y] < widest * BodyWidth) continue;
                if (y < bodyLow) bodyLow = y;
                if (y > bodyHigh) bodyHigh = y;
            }

            int wallSpan = Wall * 2 + 1;
            int wallArea = wallSpan * wallSpan;
            int companySpan = Company * 2 + 1;
            int labelArea = companySpan * companySpan;
            int labelCut = Mathf.RoundToInt(labelArea * LabelDensity);

            var fill = new Color[w * h];
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < fill.Length; i++) fill[i] = clear;

            int lowest = h, highest = -1;
            for (int y = bodyLow; y <= bodyHigh; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!opaque[y * w + x]) continue;
                    if (Window(opaqueSum, w, h, x, y, Wall) < wallArea) continue;
                    if (Window(labelSum, w, h, x, y, Company) >= labelCut) continue;
                    fill[y * w + x] = Color.white;
                    if (y < lowest) lowest = y;
                    if (y > highest) highest = y;
                }
            }
            if (highest < 0) return new Piece(bottle, null, (float)w / h, 0f, 1f);

            var mask = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            mask.SetPixels(fill);
            mask.Apply();
            var fillSprite = Sprite.Create(mask, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                                           SpriteMeshType.FullRect);

            // A pixel's span is [y, y+1), so the cavity reaches the top of its highest row.
            return new Piece(bottle, fillSprite, (float)w / h, lowest / (float)h, (highest + 1) / (float)h);
        }

        /// <summary>Summed-area table, one row and column of zeroes taller and wider than the mask.</summary>
        private static int[] Integral(bool[] flag, int w, int h)
        {
            var sum = new int[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                int rowRun = 0;
                for (int x = 0; x < w; x++)
                {
                    if (flag[y * w + x]) rowRun++;
                    sum[(y + 1) * (w + 1) + x + 1] = sum[y * (w + 1) + x + 1] + rowRun;
                }
            }
            return sum;
        }

        /// <summary>How many flagged pixels sit in the square of the given radius around (x, y).</summary>
        private static int Window(int[] sum, int w, int h, int x, int y, int radius)
        {
            int x0 = Mathf.Clamp(x - radius, 0, w), x1 = Mathf.Clamp(x + radius + 1, 0, w);
            int y0 = Mathf.Clamp(y - radius, 0, h), y1 = Mathf.Clamp(y + radius + 1, 0, h);
            return sum[y1 * (w + 1) + x1] - sum[y0 * (w + 1) + x1]
                 - sum[y1 * (w + 1) + x0] + sum[y0 * (w + 1) + x0];
        }
    }
}
