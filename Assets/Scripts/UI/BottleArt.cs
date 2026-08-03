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
    /// It splits a bottle into three layers, and the game draws them in this order:
    ///   1. the bottle as the artist drew it;
    ///   2. the drink, inside <see cref="Piece.Fill"/> and nowhere else;
    ///   3. <see cref="Piece.Front"/> — the wall, the neck, the closure, whatever hangs off the
    ///      side, and every label — at FULL strength, so none of it can be tinted by the drink.
    ///
    /// Layer 3 is the whole point. Until 2026-08-02 the third layer was the entire bottle at half
    /// strength, a hack that put the labels roughly back on top while staining them with whatever
    /// was in the bottle; the author had reported the same fault three times. A label is on the
    /// OUTSIDE of the glass, so it is opaque and the drink simply stops behind it.
    /// </summary>
    public static class BottleArt
    {
        /// <summary>Wall inset, in pixels: how far inside the silhouette the liquid starts.</summary>
        private const int Wall = 3;

        /// <summary>A row is "body" if it is at least this wide compared to the widest row.</summary>
        private const float BodyWidth = 0.80f;

        /// <summary>
        /// A colour trapped in this share of the body or less is printed ON the glass.
        /// Glass is the background a label sits on, so a glass tone appears above the label and
        /// again below it and ends up spanning the body; a label's tones are stuck in their band.
        /// Measured across the shelf: glass tones reach 87–98% of the body, label tones 25–56%.
        /// </summary>
        private const float PrintTall = 0.72f;

        /// <summary>
        /// Or this much shorter than the TALLEST tone on the bottle, whatever share of the
        /// body that is. The house syrup's label covers 78% of its body and the fixed share
        /// above could not see it; against the glass around it, which runs the whole cavity,
        /// it is plainly the shorter thing.
        /// </summary>
        private const float PrintShort = 0.65f;

        /// <summary>Colours rarer than this are highlights and dithering, not print.</summary>
        private const int PrintMin = 20;

        /// <summary>
        /// How far across the vessel a colour must reach to be print. A label spans the
        /// bottle; a specular highlight is a short bright block trapped in its own band,
        /// which passes the height test exactly as a label does. Left in, it was drawn at
        /// full strength over the drink and read as a chip of white floating in the whisky.
        /// </summary>
        private const float PrintWide = 0.40f;

        /// <summary>
        /// If "print" claims more than this much of the cavity, the test has failed to find any
        /// glass — a barrel, say, which is wood all the way round. Better to show the drink
        /// everywhere than to hide it behind a bottle-shaped label. This sat at 0.72 when the
        /// shelf's labels topped out around 62% of the cavity; the tier bottles drawn 2026-08-03
        /// carry tall panels that reach 80% of a slim body, and at 0.72 the whole label was
        /// thrown out and tinted by the drink. The kegs, which the cap existed to protect, are
        /// in <see cref="Sealed"/> now.
        /// </summary>
        private const float PrintCap = 0.88f;

        /// <summary>
        /// How much of its own bounding box a print colour must actually cover. A label plate
        /// is a solid block — its colours fill most of the box they span — but the cut glass
        /// and dither shading on the tier bottles put a colour into a few dozen scattered chips
        /// whose box spans half the bottle. Those chips passed both span tests, were promoted
        /// to "print", and floated at full strength over the drink.
        /// </summary>
        private const float PrintDense = 0.40f;

        /// <summary>
        /// Glass with more chroma than this is COLOURED glass, and the drink is not drawn in
        /// its own colour inside it — an amber bottle with red liquid in it reads as a mistake,
        /// not a menu. The liquid becomes a darker shade of the glass itself: enough to say how
        /// much is left, nothing foreign about the hue (the author, 2026-08-03: "renkli
        /// şişelerde renkli sıvı olmamalı").
        /// </summary>
        private const float TintedChroma = 0.24f;

        /// <summary>Or darker than this — smoked and near-black glass shows no colour either.</summary>
        private const float TintedLum = 0.28f;

        /// <summary>
        /// How solid the drink is drawn. A flat opaque block reads as paint, not liquid — the
        /// bottle's own highlights and the shading down its shoulders have to carry through it for
        /// the eye to put the drink BEHIND the glass rather than on it. This used to be 0.85 with
        /// a half-strength sprite laid over the top, which landed near 0.42 of pure colour; the
        /// front layer no longer covers the cavity, so the number says what it means again.
        /// </summary>
        private const float LiquidAlpha = 0.62f;

        /// <summary>A bottle's sprite together with the cavity the drink is drawn into.</summary>
        public readonly struct Piece
        {
            /// <summary>The bottle as drawn — empty glass, cap seated.</summary>
            public readonly Sprite Sprite;

            /// <summary>The cavity, white on nothing, in the same rect as <see cref="Sprite"/>.</summary>
            public readonly Sprite Fill;

            /// <summary>Everything that must stay in front of the drink, in the same rect.</summary>
            public readonly Sprite Front;

            /// <summary>Width over height, for sizing from a height alone.</summary>
            public readonly float Aspect;

            /// <summary>Bottom of the cavity, as a fraction of the rect. 0 = bottom of the sprite.</summary>
            public readonly float FloorY;

            /// <summary>Top of the cavity, as a fraction of the rect.</summary>
            public readonly float RimY;

            /// <summary>The average tone of the glass itself — the cavity with the print left
            /// out. White where the sprite could not be read.</summary>
            public readonly Color GlassColor;

            public Piece(Sprite sprite, Sprite fill, Sprite front, float aspect, float floorY,
                         float rimY, Color glassColor)
            {
                Sprite = sprite;
                Fill = fill;
                Front = front;
                Aspect = aspect;
                FloorY = floorY;
                RimY = rimY;
                GlassColor = glassColor;
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

        /// <summary>What marks the capless art of a style.</summary>
        private const string OpenSuffix = "_open";

        /// <summary>
        /// Vessels the game draws no drink into (the author, 2026-08-02). The juices come
        /// in board cartons and the energy drink in a can — a matte box owes nobody a view
        /// of its contents, which is the point of putting a juice in one — and the cola and
        /// the soda came back from the generator already drawn full. The kegs joined them
        /// 2026-08-03: steel is as opaque as board, and the drink that used to be painted on
        /// them sat on the metal like a decal. The hover card carries what is left in each.
        /// </summary>
        private static readonly HashSet<string> Sealed = new HashSet<string>
        {
            "orange", "lemon", "lime", "pineapple", "cranberry", "energy", "cola", "soda",
            "lager", "pale_ale", "stout"
        };

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
        /// A BRAND's bottle, measured from its own art where it has any. The upper tiers of
        /// each spirit were drawn their own vessels (2026-08-03) and their cavities are their
        /// own too — a decanter's belly is nowhere near where a straight bottle's is, and
        /// measuring the tier-one sprite for all four would have poured the drink into thin
        /// air. A brand with no art of its own falls back to its style, which is correct: the
        /// tier that opens the bar IS the style art.
        /// </summary>
        public static Piece For(IngredientCard card, bool open = false)
        {
            if (card == null) return default;
            string key = (open ? "o:" : "c:") + card.Id;
            if (Cache.TryGetValue(key, out var piece)) return piece;
            var sprite = open ? ItemArt.BottleOpen(card) : ItemArt.Bottle(card);
            piece = Measure(sprite);
            Cache[key] = piece;
            return piece;
        }

        /// <summary>
        /// The art key for a bottle in the hand: the capless shot where the style has one.
        /// The pour stage used to draw the open art but take its layers from the closed
        /// bottle, which under a full-strength front layer would seat the cap back on top
        /// of the bottle it had just opened. One key now decides both.
        /// </summary>
        public static string OpenKey(string style)
        {
            if (string.IsNullOrEmpty(style)) return style;
            return ItemArt.Bottle(style + OpenSuffix) != null ? style + OpenSuffix : style;
        }

        /// <summary>The style a piece of art belongs to — an opened vodka is still vodka,
        /// and it pours the same colour.</summary>
        private static string StyleOf(string artKey) =>
            !string.IsNullOrEmpty(artKey) && artKey.EndsWith(OpenSuffix)
                ? artKey.Substring(0, artKey.Length - OpenSuffix.Length)
                : artKey;

        /// <summary>
        /// Hangs the drink inside a bottle image, between the bottle and its front layer.
        /// Returns null when the style has no art; the caller then has nothing to fill.
        /// </summary>
        /// <summary>The drink inside a BRAND's bottle, measured from that brand's own art.</summary>
        public static Image AddLiquid(RectTransform bottleArt, IngredientCard card, bool open = false)
            => AddLiquid(bottleArt, For(card, open), card?.Info?.Style, card?.Type ?? IngredientType.Spirit);

        public static Image AddLiquid(RectTransform bottleArt, string style, IngredientType type)
            => AddLiquid(bottleArt, For(style), style, type);

        private static Image AddLiquid(RectTransform bottleArt, Piece piece, string style,
                                       IngredientType type)
        {
            if (Sealed.Contains(StyleOf(style))) return null;
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
            img.color = LiquidTint(piece, StyleOf(style), type);

            if (piece.Front == null) return img;

            // A child of the LIQUID, not of the bottle: the pour stage swaps bottles by
            // destroying the liquid alone, and an orphaned front layer would wear the old
            // bottle's face over the new one.
            var front = new GameObject("Front", typeof(RectTransform));
            var frt = (RectTransform)front.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            var fimg = front.AddComponent<Image>();
            fimg.sprite = piece.Front;
            fimg.preserveAspect = true;
            fimg.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// What colour the drink is drawn. Clear glass shows the drink's own colour; coloured
        /// or dark glass shows a deeper shade of ITSELF — the level stays readable as a value
        /// step, and no foreign hue fights the glass (see <see cref="TintedChroma"/>).
        /// </summary>
        private static Color LiquidTint(Piece piece, string style, IngredientType type)
        {
            var g = piece.GlassColor;
            float chroma = Mathf.Max(g.r, Mathf.Max(g.g, g.b)) - Mathf.Min(g.r, Mathf.Min(g.g, g.b));
            float lum = 0.299f * g.r + 0.587f * g.g + 0.114f * g.b;
            if (chroma > TintedChroma || lum < TintedLum)
            {
                var deep = Color.Lerp(g, Color.black, 0.38f);
                return new Color(deep.r, deep.g, deep.b, LiquidAlpha);
            }
            var c = UITheme.LiquidColor(style, type);
            return new Color(c.r, c.g, c.b, LiquidAlpha);
        }

        private static Piece Measure(Sprite bottle)
        {
            if (bottle == null) return default;
            var tex = bottle.texture;
            if (tex == null || !tex.isReadable) return new Piece(bottle, null, null, 1f, 0f, 1f, Color.white);

            var rect = bottle.rect;
            int w = (int)rect.width, h = (int)rect.height;
            if (w <= 0 || h <= 0) return new Piece(bottle, null, null, 1f, 0f, 1f, Color.white);

            var px = tex.GetPixels((int)rect.x, (int)rect.y, w, h);
            var opaque = new bool[w * h];
            for (int i = 0; i < px.Length; i++) opaque[i] = px[i].a >= 0.5f;

            // The vessel is the biggest blob. A lemon wedge or a hanging tag is not the vessel,
            // and letting one into the row widths is what tipped the shoulder off its shelf.
            var vessel = LargestBlob(opaque, w, h);

            var rowWidth = new int[h];
            int widest = 0;
            for (int y = 0; y < h; y++)
            {
                int n = 0;
                for (int x = 0; x < w; x++) if (vessel[y * w + x]) n++;
                rowWidth[y] = n;
                if (n > widest) widest = n;
            }
            if (widest == 0) return new Piece(bottle, null, null, (float)w / h, 0f, 1f, Color.white);

            // Texture rows run UPWARD from the bottom of the sprite, which is the opposite of
            // the way a bottle is described. Reading them as if row 0 were the top put the
            // shoulder on the base and ran the cavity from the body up THROUGH the closure:
            // the drink filled the cap, and its fill line sliced the top off the bottle.
            int bodyLow = -1;                          // the floor of the vessel
            for (int y = 0; y < h; y++)
            {
                if (rowWidth[y] <= 0) continue;
                bodyLow = y;
                break;
            }
            int bodyHigh = -1;                         // the shoulder: above it lie neck and closure
            for (int y = h - 1; y >= 0; y--)
            {
                if (rowWidth[y] < widest * BodyWidth) continue;
                bodyHigh = y;
                break;
            }
            if (bodyLow < 0 || bodyHigh < bodyLow)
                return new Piece(bottle, null, null, (float)w / h, 0f, 1f, Color.white);

            // The cavity is the whole body below the shoulder, walls inset, down to the FLOOR of
            // the vessel. It used to stop at the last row wide enough to count as body, which left
            // the drink hovering as much as 23px above the glass on every round-bottomed bottle.
            // It also runs straight THROUGH the print, because the print is drawn in front of it;
            // cutting labels out of the cavity is what split the drink into disconnected bands.
            var vesselSum = Integral(vessel, w, h);
            int wallArea = (Wall * 2 + 1) * (Wall * 2 + 1);
            var cavity = new bool[w * h];
            int lowest = h, highest = -1, cavityCount = 0;
            for (int y = bodyLow; y <= bodyHigh; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!vessel[y * w + x]) continue;
                    if (Window(vesselSum, w, h, x, y, Wall) < wallArea) continue;
                    cavity[y * w + x] = true;
                    cavityCount++;
                    if (y < lowest) lowest = y;
                    if (y > highest) highest = y;
                }
            }
            if (highest < 0) return new Piece(bottle, null, null, (float)w / h, 0f, 1f, Color.white);

            var print = FindPrint(px, cavity, w, h, bodyLow, bodyHigh, cavityCount);

            var fill = new Color[w * h];
            var face = new Color[w * h];
            var clear = new Color(1f, 1f, 1f, 0f);
            float glassR = 0f, glassG = 0f, glassB = 0f;
            int glassN = 0;
            for (int i = 0; i < fill.Length; i++)
            {
                bool inside = cavity[i] && !print[i];
                fill[i] = cavity[i] ? Color.white : clear;
                // Everything the drink must not touch, at the strength the artist drew it.
                face[i] = opaque[i] && !inside ? px[i] : clear;
                if (!inside) continue;
                glassR += px[i].r; glassG += px[i].g; glassB += px[i].b;
                glassN++;
            }
            var glass = glassN > 0
                ? new Color(glassR / glassN, glassG / glassN, glassB / glassN)
                : Color.white;

            var fillSprite = Bake(fill, w, h);
            var frontSprite = Bake(face, w, h);

            // A pixel's span is [y, y+1), so the cavity reaches the top of its highest row.
            return new Piece(bottle, fillSprite, frontSprite, (float)w / h,
                             lowest / (float)h, (highest + 1) / (float)h, glass);
        }

        /// <summary>
        /// Which cavity pixels are printed on the glass rather than seen through it, told apart
        /// by how far up and down the sprite each colour reaches. See <see cref="PrintTall"/>.
        /// </summary>
        private static bool[] FindPrint(Color[] px, bool[] cavity, int w, int h,
                                        int bodyLow, int bodyHigh, int cavityCount)
        {
            var print = new bool[w * h];
            int bodyHeight = bodyHigh - bodyLow + 1;
            var low = new Dictionary<int, int>();
            var high = new Dictionary<int, int>();
            var left = new Dictionary<int, int>();
            var right = new Dictionary<int, int>();
            var seen = new Dictionary<int, int>();
            int widestCavity = 0;

            for (int y = bodyLow; y <= bodyHigh; y++)
            {
                int rowRun = 0;
                for (int x = 0; x < w; x++)
                {
                    if (!cavity[y * w + x]) continue;
                    rowRun++;
                    int key = Key(px[y * w + x]);
                    seen[key] = seen.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (!low.TryGetValue(key, out var lo) || y < lo) low[key] = y;
                    if (!high.TryGetValue(key, out var hi) || y > hi) high[key] = y;
                    if (!left.TryGetValue(key, out var xl) || x < xl) left[key] = x;
                    if (!right.TryGetValue(key, out var xr) || x > xr) right[key] = x;
                }
                if (rowRun > widestCavity) widestCavity = rowRun;
            }

            int tallest = 0;
            foreach (var pair in seen)
            {
                if (pair.Value < PrintMin) continue;
                int span = high[pair.Key] - low[pair.Key] + 1;
                if (span > tallest) tallest = span;
            }

            var ink = new HashSet<int>();
            foreach (var pair in seen)
            {
                if (pair.Value < PrintMin) continue;
                int key = pair.Key;
                int span = high[key] - low[key] + 1;
                int reach = right[key] - left[key] + 1;
                if (span > bodyHeight * PrintTall && span > tallest * PrintShort) continue;
                if (reach < widestCavity * PrintWide) continue;
                // a plate is a block; a shading tone is a scatter in a plate-sized box
                if (pair.Value < span * reach * PrintDense) continue;
                ink.Add(key);
            }

            int painted = 0;
            for (int i = 0; i < print.Length; i++)
            {
                if (!cavity[i] || !ink.Contains(Key(px[i]))) continue;
                print[i] = true;
                painted++;
            }

            // A plate has no glass holes in it. Where a bottle shares its colour with its own
            // label — the green tequila in its green glass — only the label's border came back,
            // and the middle was left to be painted over. Anything walled in by print on all four
            // sides belongs to the plate.
            var inkLeft = new bool[w * h];
            var inkRight = new bool[w * h];
            var inkAbove = new bool[w * h];
            var inkBelow = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                bool run = false;
                for (int x = 0; x < w; x++) { run |= print[y * w + x]; inkLeft[y * w + x] = run; }
                run = false;
                for (int x = w - 1; x >= 0; x--) { run |= print[y * w + x]; inkRight[y * w + x] = run; }
            }
            for (int x = 0; x < w; x++)
            {
                bool run = false;
                for (int y = 0; y < h; y++) { run |= print[y * w + x]; inkAbove[y * w + x] = run; }
                run = false;
                for (int y = h - 1; y >= 0; y--) { run |= print[y * w + x]; inkBelow[y * w + x] = run; }
            }
            for (int i = 0; i < print.Length; i++)
            {
                if (print[i] || !cavity[i]) continue;
                if (inkLeft[i] && inkRight[i] && inkAbove[i] && inkBelow[i]) { print[i] = true; painted++; }
            }

            if (cavityCount > 0 && painted > cavityCount * PrintCap) return new bool[w * h];
            return print;
        }

        /// <summary>A colour as one integer, so it can key a table. Alpha is not part of it.</summary>
        private static int Key(Color c) =>
            (Mathf.RoundToInt(c.r * 255f) << 16) |
            (Mathf.RoundToInt(c.g * 255f) << 8) |
            Mathf.RoundToInt(c.b * 255f);

        /// <summary>The biggest 4-connected run of set pixels — the vessel, without its garnish.</summary>
        private static bool[] LargestBlob(bool[] flag, int w, int h)
        {
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            var current = new List<int>();
            var best = new List<int>();
            for (int start = 0; start < flag.Length; start++)
            {
                if (!flag[start] || seen[start]) continue;
                current.Clear();
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    current.Add(i);
                    int x = i % w, y = i / w;
                    if (x > 0) Visit(flag, seen, stack, i - 1);
                    if (x < w - 1) Visit(flag, seen, stack, i + 1);
                    if (y > 0) Visit(flag, seen, stack, i - w);
                    if (y < h - 1) Visit(flag, seen, stack, i + w);
                }
                if (current.Count > best.Count)
                {
                    best.Clear();
                    best.AddRange(current);
                }
            }
            var blob = new bool[w * h];
            foreach (int i in best) blob[i] = true;
            return blob;
        }

        private static void Visit(bool[] flag, bool[] seen, Stack<int> stack, int i)
        {
            if (!flag[i] || seen[i]) return;
            seen[i] = true;
            stack.Push(i);
        }

        private static Sprite Bake(Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect);
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
