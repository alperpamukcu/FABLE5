using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE DISHES, READ IN THE LIGHT THEY STAND IN (2026-09-23, the author: "Garnishler hem menü görsellerinde hem
    /// de ana sahnede çok karanlık kalıyorlar").
    ///
    /// The six counter drawings are not dark files — ice, lemon, salt and sugar measure as bright as the bottles the
    /// author signed off. What darkened them was everything drawn ON them: a rail that dimmed itself whenever no drink
    /// stood in front of it, an amber room multiplying what was left, and a menu that inked the colour picture brown
    /// and point-sampled it to a third. Those are fixed where they happen (TycoonHud.Seats, TycoonHud.LitProps). What
    /// is left here is the one thing the files themselves carry: the olive and the mint are drawn in the Night ramp
    /// and read as holes once the room has taken its share.
    ///
    /// <see cref="Lift"/> raises a dish's OWN colours — never the glass it stands in, never the one-pixel ring round
    /// its silhouette — so the drawing language survives (the rim band, the contents, the glass foot, a narrow
    /// palette) and every colour still maps to exactly one colour. <see cref="Mini"/> is the dish at half size for a
    /// line of type, built the way the cellar's bottles are (cellar_box): ring peeled, area-averaged in linear light,
    /// snapped back onto the dish's own palette, one ring put back.
    ///
    /// Both are built once, in memory, from the shipped PNG. Nothing here writes a file: the drawing-language note
    /// says no tone in a dish FILE is ever computed ("hiçbir ton hesapla karıştırılmaz"), and none is — this is a
    /// regrade at runtime with one constant to switch it off.
    /// </summary>
    public static class GarnishArt
    {
        /// <summary>How far a dish's own colours are lifted: each content pixel's HSV value goes to value^LiftGamma,
        /// hue and saturation kept. 1 turns the lift off and every caller gets the PNG itself. 0.70 keeps the olives
        /// purple-black and readable; 0.55 turned them into grey pebbles (compared on 2026-09-23).</summary>
        public const float LiftGamma = 0.70f;

        /// <summary>The glass every dish is drawn in: every colour found in at least four of the six drawings
        /// (measured 2026-09-23). These are the author's shell values and are never lifted — only what is IN the
        /// glass is.</summary>
        private static readonly HashSet<int> GlassShell = new HashSet<int>
        {
            0xF7E8D3, 0x535255, 0xEEC9B8, 0x414049, 0x8F8F95, 0xBFAE9C,
            0x797978, 0xFEFEFE, 0x646568, 0x322536, 0xA78B95, 0x4D305D,
        };

        private static readonly Dictionary<Sprite, Sprite> s_lifted = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> s_minis = new Dictionary<Sprite, Sprite>();
        // What this class has already made. Lift(Lift(x)) is Lift(x): the pass is not idempotent on pixels (a
        // lifted colour would lift again), so it is made idempotent on SPRITES — a picture that came out of here
        // is handed back as it is.
        private static readonly HashSet<Sprite> s_made = new HashSet<Sprite>();

        /// <summary>
        /// The dish with its contents lifted. The source itself when <see cref="LiftGamma"/> is 1, the sprite is null
        /// or its texture is not readable. Same size, pivot, pixels-per-unit and FullRect mesh as the source, so
        /// DishRestY, ItemArt.FootPadding and StepLitProps measure it exactly as they measured the PNG — the alpha is
        /// the PNG's, pixel for pixel. The copy stays readable (ItemArt.OpaqueBounds and HoverGlow's halo read it).
        /// </summary>
        public static Sprite Lift(Sprite src)
        {
            if (src == null || Math.Abs(LiftGamma - 1f) < 1e-6f) return src;
            if (s_made.Contains(src)) return src;
            if (s_lifted.TryGetValue(src, out var hit) && hit != null) return hit;
            var px = Cut(src, out int w, out int h);
            if (px == null) return src;
            var lifted = LiftPixels(px, w, h, LiftGamma);
            var made = Make(lifted, w, h, new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit,
                src.name + "(lift)");
            s_lifted[src] = made;
            s_made.Add(made);
            return made;
        }

        /// <summary>
        /// The dish at HALF size, for a line of type (the menu's habit row: 18 units high). Pass the picture the rest
        /// of the game shows — <c>GarnishCounterArt</c>, already lifted — because the palette the mini snaps to is
        /// the one it is given. The six dishes (35x33) come out 18x17 and are padded to 18x18 with one clear row at
        /// the TOP, so the foot stays on the box's floor and a 1:1 draw lands on whole pixels. The source itself
        /// when its texture is not readable (a full-size picture in a small preserveAspect box still reads).
        /// </summary>
        public static Sprite Mini(Sprite src)
        {
            if (src == null) return null;
            if (s_minis.TryGetValue(src, out var hit) && hit != null) return hit;
            var px = Cut(src, out int w, out int h);
            if (px == null) return src;
            var half = HalfPixels(px, w, h, out int hw, out int hh);
            // Square it from the top: the rows are bottom-up, so the clear rows are appended last.
            int side = Math.Max(hw, hh);
            var padded = new byte[hw * side * 4];
            Buffer.BlockCopy(half, 0, padded, 0, half.Length);
            var made = Make(padded, hw, side, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, src.name + "(mini)");
            s_minis[src] = made;
            s_made.Add(made);
            return made;
        }

        // ── Unity at the edges: a sprite's pixels in, a sprite out ───────────────────────────────────────────────

        /// <summary>The sprite's own rect out of its texture, as RGBA bytes, rows BOTTOM-UP (GetPixels32's order).
        /// Null when the texture cannot be read.</summary>
        private static byte[] Cut(Sprite src, out int w, out int h)
        {
            w = h = 0;
            var tex = src.texture;
            if (tex == null || !tex.isReadable) return null;
            var r = src.rect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
            w = Mathf.RoundToInt(r.width);
            h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) return null;
            Color32[] all;
            try { all = tex.GetPixels32(); }
            catch (UnityException) { return null; }
            int tw = tex.width;
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = all[(y0 + y) * tw + x0 + x];
                    int i = (y * w + x) * 4;
                    px[i] = c.r; px[i + 1] = c.g; px[i + 2] = c.b; px[i + 3] = c.a;
                }
            return px;
        }

        private static Sprite Make(byte[] px, int w, int h, Vector2 pivot, float ppu, string name)
        {
            var cols = new Color32[w * h];
            for (int i = 0; i < cols.Length; i++)
                cols[i] = new Color32(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            tex.SetPixels32(cols);
            tex.Apply(false, false);                         // kept readable: the halo and the foot line read it
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
            sp.name = name;
            return sp;
        }

        // ── the pixels (no Unity type below this line) ───────────────────────────────────────────────────────────
        //
        // Row-major RGBA bytes, rows BOTTOM-UP as GetPixels32 hands them over. Opaque means alpha >= 128, and the
        // RING is every opaque pixel with a clear (or off-canvas) four-neighbour — the one-pixel line the author
        // closes every silhouette with. Rounding is banker's (Math.Round's default), the same rounding the
        // oracle the pass was proved against used.

        /// <summary>
        /// The lift, per opaque pixel: the ring and the glass shell keep the author's values; every other colour is
        /// multiplied by value^(gamma - 1), which raises its HSV value to value^gamma with hue and saturation kept.
        /// Alpha is never touched. gamma 1 returns an exact copy.
        /// </summary>
        internal static byte[] LiftPixels(byte[] rgba, int w, int h, float gamma)
        {
            var outPx = (byte[])rgba.Clone();
            // Read through decimal: 0.70f is 0.699999988 as a double, and the tuning is written as 0.70.
            double g = (double)(decimal)gamma;
            if (g == 1.0) return outPx;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    if (rgba[i + 3] < 128 || IsRing(rgba, w, h, x, y)) continue;
                    int r = rgba[i], gr = rgba[i + 1], b = rgba[i + 2];
                    if (GlassShell.Contains((r << 16) | (gr << 8) | b)) continue;
                    int max = Math.Max(r, Math.Max(gr, b));
                    if (max == 0) continue;
                    double k = Math.Pow(max / 255.0, g - 1.0);
                    outPx[i] = (byte)Math.Min(255.0, Math.Round(r * k));
                    outPx[i + 1] = (byte)Math.Min(255.0, Math.Round(gr * k));
                    outPx[i + 2] = (byte)Math.Min(255.0, Math.Round(b * k));
                }
            return outPx;
        }

        /// <summary>
        /// The half-size drawing (the project's cellar_box recipe at 1/2). Blocks of 2x2 aligned to the canvas's
        /// TOP-LEFT, so a dish's rim lands on the same rows at both sizes. A cell is drawn when at least two of its
        /// four source pixels are opaque and one of those is not ring: the non-ring pixels are averaged in linear
        /// light and snapped to the nearest colour the source already uses. Then one ring, in the colour the source
        /// rings itself with most (#535255 for five dishes, the leaf green for the mint), round what came out.
        /// Out: ((w+1)/2) x ((h+1)/2), rows bottom-up, alpha 0 or 255.
        /// </summary>
        internal static byte[] HalfPixels(byte[] rgba, int w, int h, out int outW, out int outH)
        {
            int W = (w + 1) / 2, H = (h + 1) / 2;
            outW = W; outH = H;
            var opaque = new bool[w * h];
            var ring = new bool[w * h];
            for (int i = 0; i < w * h; i++) opaque[i] = rgba[i * 4 + 3] >= 128;
            var ringCount = new Dictionary<int, int>();
            var palette = new SortedSet<int>();              // ascending (r,g,b): the tie goes to the first
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x;
                    if (!opaque[p]) continue;
                    int rgb = (rgba[p * 4] << 16) | (rgba[p * 4 + 1] << 8) | rgba[p * 4 + 2];
                    palette.Add(rgb);
                    if (!IsRing(rgba, w, h, x, y)) continue;
                    ring[p] = true;
                    ringCount.TryGetValue(rgb, out int n0);
                    ringCount[rgb] = n0 + 1;
                }

            // the ring colour: the most frequent; a tie goes to the darker (Rec.709 luma), then the lower value
            int ringRgb = 0, ringBest = -1;
            double ringLuma = double.MaxValue;
            foreach (var kv in ringCount)
            {
                double luma = Luma709(kv.Key);
                if (kv.Value > ringBest || (kv.Value == ringBest && (luma < ringLuma
                    || (luma == ringLuma && kv.Key < ringRgb))))
                {
                    ringRgb = kv.Key; ringBest = kv.Value; ringLuma = luma;
                }
            }
            var pal = new int[palette.Count];
            palette.CopyTo(pal);

            var cell = new byte[W * H * 4];
            for (int yt = 0; yt < H; yt++)
                for (int bx = 0; bx < W; bx++)
                {
                    int cover = 0, n = 0;
                    double ar = 0, ag = 0, ab = 0;
                    for (int dyt = 0; dyt < 2; dyt++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int sx = 2 * bx + dx, rowTop = 2 * yt + dyt;
                            if (sx >= w || rowTop >= h) continue;
                            int p = (h - 1 - rowTop) * w + sx;
                            if (!opaque[p]) continue;
                            cover++;
                            if (ring[p]) continue;
                            n++;
                            ar += SrgbToLinear(rgba[p * 4]);
                            ag += SrgbToLinear(rgba[p * 4 + 1]);
                            ab += SrgbToLinear(rgba[p * 4 + 2]);
                        }
                    if (cover < 2 || n == 0) continue;
                    int mr = LinearToSrgb(ar / n), mg = LinearToSrgb(ag / n), mb = LinearToSrgb(ab / n);
                    int best = pal[0];
                    long bestD = long.MaxValue;
                    foreach (int c in pal)
                    {
                        long dr = ((c >> 16) & 255) - mr, dg = ((c >> 8) & 255) - mg, db = (c & 255) - mb;
                        long d = dr * dr + dg * dg + db * db;
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    int o = ((H - 1 - yt) * W + bx) * 4;
                    cell[o] = (byte)(best >> 16); cell[o + 1] = (byte)(best >> 8); cell[o + 2] = (byte)best;
                    cell[o + 3] = 255;
                }

            // one ring, judged on the cells before any of them became ring
            var outPx = (byte[])cell.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int o = (y * W + x) * 4;
                    if (cell[o + 3] < 128 || !IsRing(cell, W, H, x, y)) continue;
                    outPx[o] = (byte)(ringRgb >> 16); outPx[o + 1] = (byte)(ringRgb >> 8); outPx[o + 2] = (byte)ringRgb;
                    outPx[o + 3] = 255;
                }
            return outPx;
        }

        /// <summary>An opaque pixel with a clear or off-canvas four-neighbour.</summary>
        private static bool IsRing(byte[] rgba, int w, int h, int x, int y) =>
            !OpaqueAt(rgba, w, h, x + 1, y) || !OpaqueAt(rgba, w, h, x - 1, y)
            || !OpaqueAt(rgba, w, h, x, y + 1) || !OpaqueAt(rgba, w, h, x, y - 1);

        private static bool OpaqueAt(byte[] rgba, int w, int h, int x, int y) =>
            x >= 0 && x < w && y >= 0 && y < h && rgba[(y * w + x) * 4 + 3] >= 128;

        private static double Luma709(int rgb) =>
            0.2126 * ((rgb >> 16) & 255) + 0.7152 * ((rgb >> 8) & 255) + 0.0722 * (rgb & 255);

        /// <summary>The exact sRGB curve, a byte in, linear light out.</summary>
        private static double SrgbToLinear(byte v)
        {
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>...and back, clamped, to a byte.</summary>
        private static int LinearToSrgb(double l)
        {
            l = Math.Max(0.0, Math.Min(1.0, l));
            double c = l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
            return (int)Math.Round(255.0 * c);
        }
    }
}
