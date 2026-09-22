using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE LICENCE'S OWN MATERIALS, AND THE FAKES' (2026-09-22, the author's eighth list: "Aynı zamanda sahte
    /// kimlikler de üretilsin ... 1- Kimliğin arkaplanı yıpranmış ve köşeli ise sanki pvs makinesinde bastırılmış
    /// kötü bir kopya. 2- Her şeyi el ile kalemle çizmiş şekilde fontlar el yazısı gibi görsel insan çizimi çöp adam
    /// gibi kalitesiz").
    ///
    /// Everything here is drawn in code (GDD 16: chrome is procedural) and is DETERMINISTIC IN THE PERSON: every
    /// sprite takes a seed hashed off whoever's card it is, so a returning drinker shows the same scuffs, the same
    /// wobble in the same pen line and the same stick man every night. None of it touches a RunRng stream - this is
    /// what a card looks like, never what happens.
    ///
    /// Two grids, on purpose. The card's STOCK is drawn at the licence's own three units a pixel (the genuine paper,
    /// the reprint's worn plastic, the backs); what a PEN draws is drawn at one unit a pixel, because a ballpoint line
    /// is thinner than a printed pixel, and a drawing made on the print's grid reads as print.
    /// </summary>
    public static class IdArt
    {
        /// <summary>Ballpoint blue-black: the drawn card's one ink.</summary>
        public static readonly Color Pen = new Color(0.13f, 0.18f, 0.42f, 1f);

        /// <summary>The licence stock's art-pixel size, in card units.</summary>
        public const float Grid = 3f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ── determinism ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A stable seed from a person's id (FNV-1a), the same on every platform and every night.</summary>
        public static uint Seed(string s)
        {
            uint h = 2166136261u;
            if (s != null)
                foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }

        private static uint Mix(uint a)
        {
            a ^= a >> 16; a *= 0x7feb352du; a ^= a >> 15; a *= 0x846ca68bu; a ^= a >> 16;
            return a;
        }

        /// <summary>A number in [0, 1) that is always the same for this seed and this index.</summary>
        public static float R(uint seed, int i) => (Mix(seed * 0x9E3779B1u + (uint)i * 0x85EBCA77u) & 0xFFFFFF) / 16777216f;

        /// <summary>Value noise on a lattice of <paramref name="cell"/> pixels, smoothed between the knots.</summary>
        private static float Noise(uint seed, float x, float y, float cell)
        {
            float gx = x / cell, gy = y / cell;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
            float fx = gx - x0, fy = gy - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = R(seed, x0 * 7919 + y0 * 104729), b = R(seed, (x0 + 1) * 7919 + y0 * 104729);
            float c = R(seed, x0 * 7919 + (y0 + 1) * 104729), d = R(seed, (x0 + 1) * 7919 + (y0 + 1) * 104729);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static Sprite Make(Color32[] px, int w, int h, Vector4 border = default, string name = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            if (name != null) tex.name = name;
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            if (name != null) s.name = name;
            return s;
        }

        private static Color32 C(Color c) => c;

        private static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        private static void Plot(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (c.a >= 255 || px[i].a == 0) { px[i] = c; return; }
            float a = c.a / 255f;
            var d = px[i];
            px[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a),
                                (byte)(d.b + (c.b - d.b) * a), (byte)Mathf.Max(d.a, c.a));
        }

        /// <summary>A pen stroke from a to b, <paramref name="width"/> pixels wide, its path bowed by the hand.</summary>
        private static void Stroke(Color32[] px, int w, int h, Vector2 a, Vector2 b, Color32 ink, uint seed, int salt,
                                   float wobble = 0.7f, int width = 1)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(2, Mathf.CeilToInt(len * 2f));
            var n = (b - a).normalized;
            var perp = new Vector2(-n.y, n.x);
            float bow = (R(seed, salt) - 0.5f) * wobble * 2.2f;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float off = bow * Mathf.Sin(Mathf.PI * t) + (Noise(seed + (uint)salt, t * len, 0f, 5f) - 0.5f) * wobble;
                var p = Vector2.Lerp(a, b, t) + perp * off;
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                Plot(px, w, h, x, y, ink);
                if (width > 1) { Plot(px, w, h, x + 1, y, ink); Plot(px, w, h, x, y + 1, ink); }
            }
        }

        /// <summary>A ring drawn in one go around (cx, cy), wobbling a little and closing a touch past its start.</summary>
        private static void Loop(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, Color32 ink,
                                 uint seed, int salt, float wobble = 0.8f)
        {
            int steps = Mathf.CeilToInt(Mathf.PI * 2f * Mathf.Max(rx, ry) * 2f);
            float start = R(seed, salt) * Mathf.PI * 2f;
            float over = 0.25f + R(seed, salt + 1) * 0.35f;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float ang = start + t * (Mathf.PI * 2f + over);
                float k = 1f + (Noise(seed + (uint)salt, t * 40f, 3f, 9f) - 0.5f) * wobble * 0.18f;
                int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * rx * k);
                int y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * ry * k);
                Plot(px, w, h, x, y, ink);
            }
        }

        // ── the reprint (Copied) ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A BAD COPY ON THE WRONG STOCK: the licence's cream gone grey, scuffed in patches, a scratch or two across
        /// it, and CORNERS CUT SQUARE - the one thing a card printer at a market stall cannot do is round a corner.
        /// Drawn at the stock's own three units a pixel, whole card at once, so the wear never repeats like a tile.
        /// </summary>
        public static Sprite WornPaper(uint seed, int w, int h)
        {
            string key = "worn:" + seed + ":" + w + "x" + h;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[w * h];
            var stock = new Color32(0xE4, 0xDD, 0xCE, 255);       // the cream, greyed: not the house's stock
            var grime = new Color32(0xB9, 0xB0, 0x9E, 255);
            var rub = new Color32(0xF1, 0xEE, 0xE6, 255);
            var rim = new Color32(0x6E, 0x66, 0x5C, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Noise(seed, x, y, 7f) * 0.6f + Noise(seed + 17u, x, y, 3f) * 0.4f;
                    var c = stock;
                    if (n > 0.66f) c = Lerp(stock, grime, (n - 0.66f) * 3.2f);
                    else if (n < 0.24f) c = Lerp(stock, rub, (0.24f - n) * 4f);
                    // the reprint's coarse dot screen, where the original has a fine stipple
                    if (((x / 2) + (y / 2)) % 3 == 0 && R(seed, x * 131 + y) < 0.35f) c = Lerp(c, grime, 0.35f);
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    // a chipped rim: the edge is broken where the card has been carried
                    if (edge) c = R(seed, 5000 + x * 3 + y * 7) < 0.8f ? rim : Lerp(rim, stock, 0.6f);
                    px[y * w + x] = c;
                }
            // scratches: a few long thin lines across the face
            int scratches = 2 + (int)(R(seed, 90) * 3f);
            for (int s = 0; s < scratches; s++)
            {
                var a = new Vector2(R(seed, 100 + s) * w, R(seed, 110 + s) * h);
                float ang = (R(seed, 120 + s) - 0.5f) * 1.4f;
                float len = 20f + R(seed, 130 + s) * 50f;
                var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                Stroke(px, w, h, a, b, new Color32(0xF8, 0xF6, 0xF0, 200), seed, 140 + s, 0.4f);
            }
            return Cache[key] = Make(px, w, h, default, "id_worn");
        }

        /// <summary>
        /// A picture run through a cheap copier: half the resolution (every 2x2 block averaged), its colours cut to a
        /// few steps and washed towards grey, and the red plate a pixel out of register. For the photograph, the
        /// flag and the band's beach on the COPIED card.
        /// </summary>
        public static Sprite Reprint(Sprite src)
        {
            if (src == null || src.texture == null) return null;
            // keyed on the TEXTURE: every drinker's photograph is a sprite called "face"
            string key = "reprint:" + src.texture.GetInstanceID() + ":" + src.rect;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            Color32[] from;
            try { from = src.texture.GetPixels32(); }
            catch (UnityException) { return src; }      // not readable: the copy is the picture, which is still a tell
            int tw = src.texture.width;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // the 2x2 block this pixel sits in, averaged (alpha-weighted)
                    int bx = x & ~1, by = y & ~1;
                    float sr = 0, sg = 0, sb = 0, sa = 0, n = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int sx = Mathf.Min(w - 1, bx + dx), sy = Mathf.Min(h - 1, by + dy);
                            var c = from[((int)r.y + sy) * tw + (int)r.x + sx];
                            float a = c.a / 255f;
                            sr += c.r * a; sg += c.g * a; sb += c.b * a; sa += a; n++;
                        }
                    if (sa < 0.5f * n) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float rr = sr / sa, gg = sg / sa, b2 = sb / sa;
                    float grey = rr * 0.3f + gg * 0.59f + b2 * 0.11f;
                    rr = Mathf.Lerp(rr, grey, 0.35f); gg = Mathf.Lerp(gg, grey, 0.35f); b2 = Mathf.Lerp(b2, grey, 0.35f);
                    // posterise to five steps a channel, and lift the blacks the way a tired toner does
                    rr = Mathf.Round(rr / 255f * 4f) / 4f * 215f + 30f;
                    gg = Mathf.Round(gg / 255f * 4f) / 4f * 215f + 30f;
                    b2 = Mathf.Round(b2 / 255f * 4f) / 4f * 215f + 30f;
                    px[y * w + x] = new Color32((byte)rr, (byte)gg, (byte)b2, 255);
                }
            // the red plate one pixel to the right
            var shifted = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = w - 1; x > 0; x--)
                {
                    var here = shifted[y * w + x];
                    var left = px[y * w + x - 1];
                    if (here.a == 0 && left.a == 0) continue;
                    byte red = left.a == 0 ? (byte)Mathf.Min(255, here.r + 40) : left.r;
                    shifted[y * w + x] = new Color32(red, here.g, here.b, (byte)Mathf.Max(here.a, left.a));
                }
            return Cache[key] = Make(shifted, w, h, default, "reprint_" + src.name);
        }

        // ── the drawing (Drawn) ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A SCRAP OF NOTEBOOK PAPER, CUT OUT BY HAND: faint blue rules, a red margin, an edge that wanders where the
        /// scissors did, and a thumbprint of grime. At one unit a pixel - the card is the size of the real one, and it
        /// is the only part of the drawn card that pretends to be anything.
        /// </summary>
        public static Sprite ScrapPaper(uint seed, int w, int h)
        {
            string key = "scrap:" + seed + ":" + w + "x" + h;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[w * h];
            var paper = new Color32(0xF6, 0xF1, 0xE2, 255);
            var rule = new Color32(0x86, 0xA7, 0xD8, 255);
            var margin = new Color32(0xE0, 0x7A, 0x80, 255);
            var shade = new Color32(0xDE, 0xD4, 0xBC, 255);
            float marginX = 34f + R(seed, 3) * 6f;
            float ruleOff = R(seed, 4) * 18f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // the scissors: every edge wanders by a few units, slow along its length
                    float inL = x - (2f + Noise(seed, 0f, y, 23f) * 6f);
                    float inR = (w - 1 - x) - (2f + Noise(seed + 1u, 0f, y, 23f) * 6f);
                    float inB = y - (2f + Noise(seed + 2u, x, 0f, 29f) * 6f);
                    float inT = (h - 1 - y) - (2f + Noise(seed + 3u, x, 0f, 29f) * 6f);
                    float inside = Mathf.Min(Mathf.Min(inL, inR), Mathf.Min(inB, inT));
                    if (inside < 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    var c = paper;
                    float n = Noise(seed + 9u, x, y, 31f);
                    if (n > 0.7f) c = Lerp(paper, shade, (n - 0.7f) * 2.2f);
                    // ruled every 18 units, one unit of blue at a third
                    if (Mathf.Abs(((h - 1 - y) + ruleOff) % 18f) < 1f) c = Lerp(c, rule, 0.45f);
                    if (Mathf.Abs(x - marginX) < 1f) c = Lerp(c, margin, 0.55f);
                    if (inside < 1.5f) c = Lerp(c, new Color32(0x9A, 0x8F, 0x78, 255), 0.7f);   // the cut edge
                    px[y * w + x] = c;
                }
            return Cache[key] = Make(px, w, h, default, "id_scrap");
        }

        /// <summary>
        /// A BOX RULED BY HAND: four pen strokes that do not quite meet and do not quite run straight, one unit a
        /// pixel, clear inside. Sized to the field it frames, so it is never stretched.
        /// </summary>
        public static Sprite PenBox(int w, int h, uint seed)
        {
            string key = "penbox:" + w + "x" + h + ":" + seed;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            w = Mathf.Max(8, w); h = Mathf.Max(8, h);
            var px = new Color32[w * h];
            var ink = C(new Color(Pen.r, Pen.g, Pen.b, 0.9f));
            float o = 2f;
            // each side starts a little early or late and overshoots the corner
            Vector2 j(int i) => new Vector2((R(seed, i) - 0.5f) * 3f, (R(seed, i + 50) - 0.5f) * 3f);
            Stroke(px, w, h, new Vector2(o, o) + j(1), new Vector2(w - 1 - o, o) + j(2), ink, seed, 10, 1.0f);
            Stroke(px, w, h, new Vector2(w - 1 - o, o) + j(3), new Vector2(w - 1 - o, h - 1 - o) + j(4), ink, seed, 11, 1.0f);
            Stroke(px, w, h, new Vector2(w - 1 - o, h - 1 - o) + j(5), new Vector2(o, h - 1 - o) + j(6), ink, seed, 12, 1.0f);
            Stroke(px, w, h, new Vector2(o, h - 1 - o) + j(7), new Vector2(o, o) + j(8), ink, seed, 13, 1.0f);
            return Cache[key] = Make(px, w, h, default, "id_penbox");
        }

        /// <summary>
        /// THE PHOTO, DRAWN: a stick man in ballpoint, the way a teenager draws a face on a card they made at a
        /// kitchen table - a wobbly ring of a head, two dots, a line of a mouth, a line of a body and two of arms, and
        /// a scribble of hair that differs with the person. 64x64 at one pen pixel, shown at the photo's 2x.
        /// </summary>
        public static Sprite StickFigure(uint seed)
        {
            string key = "stick:" + seed;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int S = 64;
            var px = new Color32[S * S];
            var ink = C(Pen);
            // y runs UP in the texture: the head near the top
            float cx = 32f + (R(seed, 1) - 0.5f) * 4f, cy = 42f + (R(seed, 2) - 0.5f) * 3f;
            float rx = 11f + R(seed, 3) * 3f, ry = rx * (0.95f + R(seed, 4) * 0.15f);
            Loop(px, S, S, cx, cy, rx, ry, ink, seed, 10);
            // the eyes: dots, or little crosses, or a pair of rings for glasses
            int eyes = (int)(R(seed, 5) * 3f);
            float ex = rx * 0.38f, ey = cy + ry * 0.12f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = cx + side * ex;
                if (eyes == 2) Loop(px, S, S, x, ey, 3f, 3f, ink, seed, 20 + side, 0.4f);
                Plot(px, S, S, Mathf.RoundToInt(x), Mathf.RoundToInt(ey), ink);
                Plot(px, S, S, Mathf.RoundToInt(x), Mathf.RoundToInt(ey) + 1, ink);
                if (eyes == 1) { Plot(px, S, S, Mathf.RoundToInt(x) + 1, Mathf.RoundToInt(ey), ink); }
            }
            if (eyes == 2) Stroke(px, S, S, new Vector2(cx - ex + 3f, ey), new Vector2(cx + ex - 3f, ey), ink, seed, 25, 0.3f);
            // the mouth: a smile, a line or an o
            int mouth = (int)(R(seed, 6) * 3f);
            float my = cy - ry * 0.45f;
            if (mouth == 0)
                for (int i = -4; i <= 4; i++)
                    Plot(px, S, S, Mathf.RoundToInt(cx + i), Mathf.RoundToInt(my + (i * i) * 0.09f), ink);
            else if (mouth == 1) Stroke(px, S, S, new Vector2(cx - 4f, my), new Vector2(cx + 4f, my + 0.8f), ink, seed, 30, 0.5f);
            else Loop(px, S, S, cx, my, 1.8f, 1.6f, ink, seed, 31, 0.2f);
            // the hair: spikes, a scribble of curls, or a cap
            int hair = (int)(R(seed, 7) * 3f);
            if (hair == 0)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, i / 6f);
                    var root = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
                    var tip = root + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (3f + R(seed, 40 + i) * 3f);
                    Stroke(px, S, S, root, tip, ink, seed, 40 + i, 0.4f);
                }
            }
            else if (hair == 1)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Lerp(0.5f, Mathf.PI - 0.5f, i / 5f);
                    Loop(px, S, S, cx + Mathf.Cos(a) * (rx - 1f), cy + Mathf.Sin(a) * (ry - 1f), 2.5f, 2.2f, ink, seed, 50 + i, 0.3f);
                }
            }
            else
            {
                Stroke(px, S, S, new Vector2(cx - rx - 1f, cy + ry * 0.45f), new Vector2(cx + rx + 5f, cy + ry * 0.4f), ink, seed, 60, 0.4f);
                for (int i = -6; i <= 6; i++)
                    Plot(px, S, S, Mathf.RoundToInt(cx + i * rx / 7f), Mathf.RoundToInt(cy + ry * 0.45f + Mathf.Sqrt(Mathf.Max(0f, 36f - i * i)) * ry / 9f), ink);
            }
            // the neck, the body and the arms, cut off by the photo's foot like a real portrait's shoulders
            var neck = new Vector2(cx, cy - ry);
            var hip = new Vector2(cx + (R(seed, 8) - 0.5f) * 3f, -2f);
            Stroke(px, S, S, neck, hip, ink, seed, 70, 0.6f);
            var shoulder = neck + new Vector2(0f, -7f);
            float lift = R(seed, 9) < 0.3f ? 10f : -4f;          // one in three waves
            Stroke(px, S, S, shoulder, shoulder + new Vector2(-16f, lift + (R(seed, 11) - 0.5f) * 4f), ink, seed, 71, 0.7f);
            Stroke(px, S, S, shoulder, shoulder + new Vector2(16f, -4f + (R(seed, 12) - 0.5f) * 4f), ink, seed, 72, 0.7f);
            return Cache[key] = Make(px, S, S, default, "id_stick");
        }

        /// <summary>
        /// A FLAG COLOURED IN: the country's own flag, its colours laid in with a pencil's hatching - every third
        /// diagonal pressed harder, paper showing through in specks - inside a pen outline that wobbles. Same size as
        /// the drawn flag (48x33), so it sits in the same frame.
        /// </summary>
        public static Sprite HandFlag(Sprite flag, uint seed)
        {
            if (flag == null || flag.texture == null) return null;
            string key = "handflag:" + flag.texture.GetInstanceID() + ":" + flag.rect + ":" + seed;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var r = flag.rect;
            int w = (int)r.width, h = (int)r.height;
            Color32[] from;
            try { from = flag.texture.GetPixels32(); }
            catch (UnityException) { return flag; }
            int tw = flag.texture.width;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // the hand does not keep inside the lines: colour is sampled a pixel or so off
                    int sx = Mathf.Clamp(x + Mathf.RoundToInt((Noise(seed, x, y, 6f) - 0.5f) * 3f), 0, w - 1);
                    int sy = Mathf.Clamp(y + Mathf.RoundToInt((Noise(seed + 5u, x, y, 6f) - 0.5f) * 3f), 0, h - 1);
                    var c = from[((int)r.y + sy) * tw + (int)r.x + sx];
                    if (c.a < 128) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float press = ((x + y) % 3 == 0) ? 1f : 0.72f;          // the hatching
                    if (R(seed, x * 977 + y * 31) < 0.14f) press = 0.2f;   // paper through the crayon
                    var paper = new Color32(0xF6, 0xF1, 0xE2, 255);
                    px[y * w + x] = Lerp(paper, c, press);
                }
            var ink = C(Pen);
            Stroke(px, w, h, new Vector2(0, 0), new Vector2(w - 1, 0), ink, seed, 80, 0.8f);
            Stroke(px, w, h, new Vector2(w - 1, 0), new Vector2(w - 1, h - 1), ink, seed, 81, 0.8f);
            Stroke(px, w, h, new Vector2(w - 1, h - 1), new Vector2(0, h - 1), ink, seed, 82, 0.8f);
            Stroke(px, w, h, new Vector2(0, h - 1), new Vector2(0, 0), ink, seed, 83, 0.8f);
            return Cache[key] = Make(px, w, h, default, "handflag_" + flag.name);
        }

        /// <summary>
        /// A PICTOGRAM SKETCHED: the icon's own silhouette traced in pen, and its colours washed in at a third with the
        /// paper showing through - the drawn card's drink and its garnish marks. Still reads as the same thing, which
        /// matters: the order on a fake is still the order.
        /// </summary>
        public static Sprite Sketch(Sprite src, uint seed)
        {
            if (src == null || src.texture == null) return null;
            string key = "sketch:" + src.texture.GetInstanceID() + ":" + src.rect + ":" + (seed & 0xFF);
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            Color32[] from;
            try { from = src.texture.GetPixels32(); }
            catch (UnityException) { return src; }
            int tw = src.texture.width;
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && from[((int)r.y + y) * tw + (int)r.x + x].a > 100;
            var px = new Color32[w * h];
            var ink = C(Pen);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) continue;
                    bool edge = !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1);
                    var c = from[((int)r.y + y) * tw + (int)r.x + x];
                    if (edge) { px[y * w + x] = R(seed, x * 53 + y) < 0.9f ? ink : new Color32(0, 0, 0, 0); continue; }
                    if (R(seed, x * 71 + y * 13) < 0.3f) continue;
                    px[y * w + x] = new Color32(c.r, c.g, c.b, 110);
                }
            return Cache[key] = Make(px, w, h, default, "sketch_" + src.name);
        }

        /// <summary>A five-point star in pen, 12x12: the drawn card's rating mark.</summary>
        public static Sprite PenStar()
        {
            const string Key = "penstar";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 16;
            var px = new Color32[S * S];
            var ink = C(Pen);
            var pts = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f * 2f / 5f;
                pts[i] = new Vector2(7.5f + Mathf.Cos(a) * 6.5f, 7.5f + Mathf.Sin(a) * 6.5f);
            }
            for (int i = 0; i < 5; i++) Stroke(px, S, S, pts[i], pts[(i + 1) % 5], ink, 7u, 90 + i, 0.4f);
            return Cache[Key] = Make(px, S, S, default, "id_penstar");
        }

        /// <summary>A DOOR, 8x8, white for the caller to tint: the visits count's mark (how many times they walked in).</summary>
        public static Sprite DoorMark()
        {
            const string Key = "door";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            string[] rows =
            {
                ".######.",
                ".#....#.",
                ".#....#.",
                ".#...##.",
                ".#....#.",
                ".#....#.",
                ".#....#.",
                "########",
            };
            const int S = 8;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (rows[S - 1 - y][x] == '#') px[y * S + x] = new Color32(255, 255, 255, 255);
            return Cache[Key] = Make(px, S, S, default, "id_door");
        }

        /// <summary>
        /// THE FLAG'S FRAME (2026-09-22, the author: "ülkesinin bayrağının etrafında bayrağın şekline göre çerçeve
        /// olacak"): the flag's own rectangle, a pixel of ink outside a pixel of the card's cream, at the stock's grid.
        /// Nine-sliced, clear in the middle, so it takes any flag the art draws.
        /// </summary>
        public static Sprite FlagFrame()
        {
            const string Key = "flagframe";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 7;
            var px = new Color32[S * S];
            var ink = new Color32(0x1A, 0x10, 0x23, 255);      // Night[1]
            var mat = new Color32(0xF2, 0xE8, 0xD5, 255);      // Cream[4]
            var lit = new Color32(0xFF, 0xFA, 0xEE, 255);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int e = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y));
                    px[y * S + x] = e == 0 ? ink : e == 1 ? (y == S - 2 || x == 1 ? lit : mat) : new Color32(0, 0, 0, 0);
                }
            return Cache[Key] = Make(px, S, S, new Vector4(2, 2, 2, 2), "id_flagframe");
        }

        /// <summary>A field on the printed card: a hairline of the card's ink round a breath of it inside, at one unit a
        /// pixel. Nine-sliced, so every box on the card is the same box.</summary>
        public static Sprite FieldBox()
        {
            const string Key = "fieldbox";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 3;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = (x == 1 && y == 1) ? new Color32(0x4D, 0x29, 0x0D, 16) : new Color32(0x4D, 0x29, 0x0D, 115);
            return Cache[Key] = Make(px, S, S, new Vector4(1, 1, 1, 1), "id_field");
        }

        /// <summary>The photograph's frame: one pixel of ink at the stock's grid, clear inside, so it never covers the face.</summary>
        public static Sprite PhotoFrame()
        {
            const string Key = "photoframe";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 3;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = (x == 1 && y == 1) ? new Color32(0, 0, 0, 0) : new Color32(0x1A, 0x10, 0x23, 255);
            return Cache[Key] = Make(px, S, S, new Vector4(1, 1, 1, 1), "id_photoframe");
        }

        /// <summary>A PUSHPIN, 12x12, white for the caller to tint: a hover that has stopped following says so.</summary>
        public static Sprite PinMark()
        {
            const string Key = "pin";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            string[] rows =
            {
                "....####....",
                "...######...",
                "...######...",
                "....####....",
                "....####....",
                "...######...",
                "..########..",
                "..########..",
                ".....##.....",
                ".....##.....",
                ".....#......",
                ".....#......",
            };
            const int S = 12;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (rows[S - 1 - y][x] == '#') px[y * S + x] = new Color32(255, 255, 255, 255);
            return Cache[Key] = Make(px, S, S, default, "id_pin");
        }

        // ── the backs ───────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE CARD'S BACK, seen as it turns over on its way up from the stool: the house stock with a Miami sunset
        /// seal and a strip of barcode. <paramref name="copied"/> reprints it on the worn stock with square corners,
        /// the way the front is. Drawn at the stock's three units a pixel.
        /// </summary>
        public static Sprite Back(uint seed, int w, int h, bool copied)
        {
            string key = "back:" + (copied ? "c" + seed : "g") + ":" + w + "x" + h;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            Color32[] px;
            if (copied)
            {
                var worn = WornPaper(seed ^ 0x5bd1e995u, w, h);
                px = worn.texture.GetPixels32();
            }
            else
            {
                px = new Color32[w * h];
                var stock = new Color32(0xF2, 0xE8, 0xD5, 255);
                var tint = new Color32(0xEB, 0xE1, 0xCC, 255);
                var rim = new Color32(0x45, 0x3E, 0x38, 255);
                const float Rr = 4f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int cx = Mathf.Min(x, w - 1 - x), cy = Mathf.Min(y, h - 1 - y);
                        bool onRim = Mathf.Min(cx, cy) == 0, clear = false;
                        if (cx < Rr && cy < Rr)
                        {
                            float dx = (Rr - 0.5f) - cx, dy = (Rr - 0.5f) - cy;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            clear = d > Rr; onRim = !clear && d > Rr - 1f;
                        }
                        px[y * w + x] = clear ? new Color32(0, 0, 0, 0) : onRim ? rim : ((x + y) % 6 == 0 ? tint : stock);
                    }
            }
            // the sunset seal: a disc cut by widening bars toward its foot, magenta at the top to amber below
            float sx = w * 0.5f, sy = h * 0.60f, sr = h * 0.26f;
            var top = new Color32(0xE8, 0x4D, 0xA6, 255);
            var foot = new Color32(0xF5, 0xC9, 0x7B, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x - sx, dy = y - sy;
                    if (dx * dx + dy * dy > sr * sr) continue;
                    float t = (dy + sr) / (2f * sr);                       // 0 at the foot, 1 at the top
                    if (t < 0.5f)
                    {
                        float band = (0.5f - t) * 2f;                      // 1 at the foot
                        int period = 4;
                        int gap = Mathf.RoundToInt(band * 2.2f);
                        if (y % period < gap) continue;
                    }
                    var c = Lerp(foot, top, t);
                    if (copied) c = Lerp(c, new Color32(0xB8, 0xB0, 0xA4, 255), 0.45f);
                    px[y * w + x] = c;
                }
            // the barcode strip along the foot
            var bar = copied ? new Color32(0x55, 0x50, 0x4A, 255) : new Color32(0x1A, 0x10, 0x23, 255);
            int bx0 = w / 8, bx1 = w - w / 8, by0 = 5, by1 = 13;
            for (int x = bx0; x < bx1; x++)
            {
                if (R(0xBA5Eu, x) < 0.45f) continue;
                for (int y = by0; y < by1; y++)
                    if (!copied || R(seed, x * 17 + y) > 0.12f) px[y * w + x] = bar;
            }
            return Cache[key] = Make(px, w, h, default, copied ? "id_back_copy" : "id_back");
        }

        /// <summary>The drawn card's back: the scrap's other side with a pen sun doodled on it and nothing else.</summary>
        public static Sprite ScrapBack(uint seed, int w, int h)
        {
            string key = "scrapback:" + seed + ":" + w + "x" + h;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = (Color32[])ScrapPaper(seed ^ 0x27d4eb2du, w, h).texture.GetPixels32().Clone();
            var ink = C(Pen);
            float cx = w * 0.5f, cy = h * 0.58f, r = h * 0.16f;
            Loop(px, w, h, cx, cy, r, r, ink, seed, 200, 1f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f + R(seed, 210 + i) * 0.2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Stroke(px, w, h, new Vector2(cx, cy) + d * (r + 5f), new Vector2(cx, cy) + d * (r + 14f + R(seed, 230 + i) * 6f),
                       ink, seed, 240 + i, 0.8f);
            }
            return Cache[key] = Make(px, w, h, default, "id_scrap_back");
        }
    }
}
