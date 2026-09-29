using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE MENUS' FURNITURE (2026-09-29, the author: "Ayarlar menüsünü/esc/dil vs. arkaplanıyla her şeyiyle tekrardan
    /// diğer sahneleri ürettiğine uygun bir tasarımda sıfırdan tekrar oluştur"). The pause, the credits and the settings
    /// stop being a generated picture (menu_esc_bg), a marquee picture (menu_header) and a blue skin, and become the
    /// week's own fittings, drawn in code: the CLOSED sign's plate (Night glass, a Graphite rim with its lit top row,
    /// fixings), crowned with the cellar's stepped deco niche, and ringed with the curtain's neon tube (ChromeArt.NeonRing,
    /// the same four rings, tinted by state). Every piece is drawn at HALF the size it is shown at and shown at exactly
    /// 2x, so every edge is two units and lands on the house's grid; every texel is a ramp step (the palette gate, GDD
    /// 16 §0) - the light is the tube's, laid in flat bands by tint, never a smooth ramp.
    ///
    /// The pieces (scratchpad menus/system/kit.py drew them first; BUILD_SPEC §2):
    ///   CABINET   a plate crowned on three stepped shoulders - one sprite a screen
    ///   RECESS    a panel let into the plate - Night[1] (a WELL: Night[0]), top and left in shadow, the sill lit
    ///   KEY PLATE a sign key's plate: a Graphite rim, a lit top row, the face - 9-sliced, so one sprite any width
    ///   FRAME     the magenta tube round a cabinet's body, broken at the top under the crown
    ///   NEON LINE a straight tube, across or down (the credits' scroll)
    ///   ENAMEL    the cellar's cream name plate (the credits' section heads)
    ///   FAN       the cellar sill's deco fan, small, for the door's ledge
    /// </summary>
    public static class MenuArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static bool Cached(string key, out Sprite sprite) => Cache.TryGetValue(key, out sprite) && sprite != null;

        // ── the cabinet ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE CABINET, <paramref name="w"/> texels wide: a body <paramref name="bodyH"/> tall, crowned in the middle by a
        /// raised header <paramref name="crownW"/> x <paramref name="crownH"/> standing on three stepped shoulders (four
        /// texels a step, the cellar's feature niche). Night[2] glass, a one-texel Graphite[3] rim all round, a lit
        /// Graphite[4] row under every TOP edge - the crown, each step and the body's shoulders catch the room's light -
        /// and a fixing near each of the body's corners (a Graphite[2] screw head lit at its top-left). Transparent
        /// outside the outline. Shown at exactly 2x; the sprite is (bodyH + crownH) tall.
        /// </summary>
        public static Sprite Cabinet(int w, int bodyH, int crownW, int crownH)
        {
            string key = $"menu:cabinet:{w}:{bodyH}:{crownW}:{crownH}";
            if (Cached(key, out var got)) return got;
            int H = bodyH + crownH;
            var m = new bool[w * H];                       // top-down, the way the drawing reads
            for (int y = crownH; y < H; y++)
                for (int x = 0; x < w; x++) m[y * w + x] = true;
            if (crownW > 0 && crownH > 0)
            {
                int cx0 = (w - crownW) / 2;
                for (int y = 0; y < crownH; y++)
                    for (int x = cx0; x < cx0 + crownW; x++) m[y * w + x] = true;
                // the shoulders: three stairs down each side of the crown's foot, outwards
                const int Steps = 3, Step = 4;
                for (int k = 1; k <= Steps; k++)
                {
                    int top = Mathf.Clamp(Mathf.RoundToInt(crownH * k / (float)(Steps + 1)), 0, crownH);
                    for (int y = top; y < crownH; y++)
                    {
                        for (int x = Mathf.Max(0, cx0 - k * Step); x < cx0; x++) m[y * w + x] = true;
                        for (int x = cx0 + crownW; x < Mathf.Min(w, cx0 + crownW + k * Step); x++) m[y * w + x] = true;
                    }
                }
            }
            bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < H && m[y * w + x];
            // the rim: every texel of the outline with a four-way neighbour outside it
            var rim = new bool[w * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < w; x++)
                    rim[y * w + x] = In(x, y) && !(In(x - 1, y) && In(x + 1, y) && In(x, y - 1) && In(x, y + 1));

            Color32 face = UITheme.Night[2], edge = UITheme.Graphite[3], lit = UITheme.Graphite[4],
                    screw = UITheme.Graphite[2], clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!m[i]) { px[i] = clear; continue; }
                    if (rim[i]) { px[i] = edge; continue; }
                    // lit: the texel under a TOP rim (a rim with nothing above it)
                    bool underTop = y > 0 && rim[i - w] && !In(x, y - 2);
                    px[i] = underTop ? lit : face;
                }
            // the fixings, two in from the body's corners
            foreach (int sx in new[] { 3, w - 5 })
                foreach (int sy in new[] { crownH + 3, H - 5 })
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            px[(sy + dy) * w + sx + dx] = dx == 0 && dy == 0 ? lit : screw;
            return Cache[key] = Make(Flip(px, w, H), w, H, Vector4.zero, "menu_cabinet");
        }

        // ── the recess and the well ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A RECESS let into a plate, 9-sliced (one texel of border, shown at 2x): its <paramref name="face"/> (Night[1];
        /// Night[0] for a WELL an instrument sits in), the top and left edges in shadow (Night[0]), the right a step up
        /// (Night[2]), the sill catching the light (Night[3]) - a cut's bevel runs backwards from a box's.
        /// </summary>
        public static Sprite Recess(Color face)
        {
            Color32 f = face;
            string key = $"menu:recess:{f.r}:{f.g}:{f.b}";
            if (Cached(key, out var got)) return got;
            Color32 shade = UITheme.Night[0], side = UITheme.Night[2], sill = UITheme.Night[3];
            var rows = new[]
            {
                new[] { shade, shade, shade },
                new[] { shade, f, side },
                new[] { sill, sill, sill },
            };
            return Cache[key] = Make(Flip(Rows(rows), 3, 3), 3, 3, new Vector4(1, 1, 1, 1), "menu_recess");
        }

        // ── the sign key's plate ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A SIGN KEY'S PLATE, 9-sliced at 2x: a one-texel <paramref name="rim"/> all round, a <paramref name="lit"/> row
        /// under the top edge (the room's light on it; pass the face again for a key pressed down or dead), and the
        /// <paramref name="face"/>. The terrazzo lies over the face as its own tiled layer (SignKey).
        /// </summary>
        public static Sprite KeyPlate(Color face, Color rim, Color lit)
        {
            Color32 f = face, r = rim, l = lit;
            string key = $"menu:key:{f.r}.{f.g}.{f.b}:{r.r}.{r.g}.{r.b}:{l.r}.{l.g}.{l.b}";
            if (Cached(key, out var got)) return got;
            var rows = new[]
            {
                new[] { r, r, r },
                new[] { r, l, r },
                new[] { r, f, r },
                new[] { r, r, r },
            };
            // left 1, bottom 1, right 1, top 2: the rim and the lit row are caps, the face stretches
            return Cache[key] = Make(Flip(Rows(rows), 3, 4), 3, 4, new Vector4(1, 1, 1, 2), "menu_key");
        }

        // ── the tubes ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE CABINET'S TUBE: ChromeArt.NeonPath's line (a one-texel rectangle, <paramref name="w"/> x
        /// <paramref name="h"/> at the rim, its corners chamfered) broken at the TOP centre for <paramref name="gap"/>
        /// texels, where the crown stands - the way the curtain's sign breaks its tube for the week plate. One ring of it
        /// (ChromeArt.NeonRing), a white mask for the tint; the sprite is ChromeArt.NeonPad bigger on every side.
        /// </summary>
        public static Sprite FrameTube(int w, int h, int chamfer, int gap, ChromeArt.NeonLayer layer)
        {
            string key = $"menu:frame:{w}x{h}:{chamfer}:{gap}:{layer}";
            if (Cached(key, out var got)) return got;
            int P = ChromeArt.NeonPad, W = w + 2 * P, H = h + 2 * P;
            var line = new bool[W * H];   // top-down
            int l = P + 1, t = P + 1, r = P + w - 2, b = P + h - 2, c = chamfer;
            for (int x = l + c; x <= r - c; x++) { line[t * W + x] = true; line[b * W + x] = true; }
            for (int y = t + c; y <= b - c; y++) { line[y * W + l] = true; line[y * W + r] = true; }
            for (int i = 0; i <= c; i++)
            {
                line[(t + c - i) * W + l + i] = true; line[(t + c - i) * W + r - i] = true;
                line[(b - c + i) * W + l + i] = true; line[(b - c + i) * W + r - i] = true;
            }
            int mid = P + w / 2, half = gap / 2;
            for (int x = mid - half; x < mid + half; x++) if (x >= 0 && x < W) line[t * W + x] = false;
            var sprite = ChromeArt.NeonRing(line, W, H, layer);
            sprite.name = "menu_frame_" + layer;
            return Cache[key] = sprite;
        }

        /// <summary>A STRAIGHT TUBE <paramref name="len"/> texels long at the rim, across or (<paramref name="down"/>)
        /// down: ChromeArt.NeonBar's line turned as asked - one texel of glass with the rim round it and over its ends.</summary>
        public static Sprite NeonLine(int len, bool down, ChromeArt.NeonLayer layer)
        {
            if (!down) return ChromeArt.NeonBar(len, layer);
            string key = $"menu:line:{len}:{layer}";
            if (Cached(key, out var got)) return got;
            int P = ChromeArt.NeonPad, W = 1 + 2 * P, H = len + 2 * P;
            var line = new bool[W * H];
            for (int y = P + 1; y <= P + len - 2; y++) line[y * W + P] = true;
            var sprite = ChromeArt.NeonRing(line, W, H, layer);
            sprite.name = "menu_line_" + layer;
            return Cache[key] = sprite;
        }

        /// <summary>A BEAD: one texel of glass grown into its four rings (7x7, shown at 2x = 14 units) - the settings'
        /// mark for the language being spoken now, lit cyan at a list cell's right end.</summary>
        public static Sprite Bead(ChromeArt.NeonLayer layer)
        {
            string key = $"menu:bead:{layer}";
            if (Cached(key, out var got)) return got;
            int P = ChromeArt.NeonPad, S = 1 + 2 * P;
            var line = new bool[S * S];
            line[P * S + P] = true;
            var sprite = ChromeArt.NeonRing(line, S, S, layer);
            sprite.name = "menu_bead_" + layer;
            return Cache[key] = sprite;
        }

        // ── the settings' instruments ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A CHOICE WELL (2026-09-29, BUILD_SPEC §2 "Choice"): the Night[0] slot a setting's options are cut into, a
        /// one-texel Graphite[2] rim all round - 9-sliced at 2x, so one sprite any width. The segments, their Graphite
        /// seams and the chosen one's cyan enamel are laid on it as flat token rects (TycoonHud.Settings).
        /// </summary>
        public static Sprite ChoiceWell()
        {
            const string key = "menu:choice";
            if (Cached(key, out var got)) return got;
            Color32 r = UITheme.Graphite[2], f = UITheme.Night[0];
            var rows = new[]
            {
                new[] { r, r, r },
                new[] { r, f, r },
                new[] { r, r, r },
            };
            return Cache[key] = Make(Flip(Rows(rows), 3, 3), 3, 3, new Vector4(1, 1, 1, 1), "menu_choice");
        }

        /// <summary>
        /// THE CLAMP that holds a meter's tube at its value (BUILD_SPEC §2 "Meter"): a Graphite block 4 x 10 texels, lit
        /// along its top (Graphite[4]), its left a step up (Graphite[3]), its right and foot in shade (Graphite[1]), a
        /// Graphite[1] slot across its middle where the tube runs through.
        /// </summary>
        public static Sprite Clamp()
        {
            const string key = "menu:clamp";
            if (Cached(key, out var got)) return got;
            const int w = 4, h = 10;
            Color32 body = UITheme.Graphite[2], top = UITheme.Graphite[4], left = UITheme.Graphite[3], shade = UITheme.Graphite[1];
            var px = new Color32[w * h];                   // top-down
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = body;
                    if (x == 0) c = left;
                    if (x == w - 1) c = shade;
                    if (y == 0) c = top;
                    if (y == h - 1) c = shade;
                    if ((y == 4 || y == 5) && (x == 1 || x == 2)) c = shade;
                    px[y * w + x] = c;
                }
            return Cache[key] = Make(Flip(px, w, h), w, h, Vector4.zero, "menu_clamp");
        }

        // ── the small fittings ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE ENAMEL NAME PLATE (the cellar's, 2026-09-28): a Night[0] frame, the upper half Cream[4] and the lower
        /// Cream[3] - a plate that catches the light across its top - 12 texels tall and 9-sliced across, so it is as
        /// wide as its word. The word on it is Night[1].
        /// </summary>
        public static Sprite Enamel()
        {
            const string key = "menu:enamel";
            if (Cached(key, out var got)) return got;
            Color32 frame = UITheme.Night[0], hi = UITheme.Cream[4], lo = UITheme.Cream[3];
            var rows = new Color32[12][];
            for (int y = 0; y < 12; y++)
            {
                Color32 c = y == 0 || y == 11 ? frame : y < 6 ? hi : lo;
                rows[y] = new[] { frame, c, frame };
            }
            return Cache[key] = Make(Flip(Rows(rows), 3, 12), 3, 12, new Vector4(1, 0, 1, 0), "menu_enamel");
        }

        /// <summary>
        /// THE DECO FAN the cellar's sill carries at its middle, small: a half-disc of five rays, Magenta[2] and
        /// Magenta[0] by turns, on a Graphite[3] rim, a Graphite hub at its foot - no light of its own. 26 x 14 texels;
        /// its flat foot is its bottom row, stood on the door's ledge.
        /// </summary>
        public static Sprite Fan()
        {
            const string key = "menu:fan";
            if (Cached(key, out var got)) return got;
            const int R = 12, W = 2 * R + 2, H = R + 2;
            Color32 rayA = UITheme.Magenta[2], rayB = UITheme.Magenta[0], edge = UITheme.Graphite[3],
                    hub = UITheme.Graphite[2], clear = new Color32(0, 0, 0, 0);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - (R + 0.5f), dy = (R + 1) - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = clear;
                    if (d <= R && dy >= 0f)
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        c = (Mathf.FloorToInt(ang / 36f) & 1) == 0 ? rayA : rayB;
                        if (d > R - 1) c = edge;
                        if (d <= 3f) c = d > 2f ? edge : hub;
                    }
                    px[y * W + x] = c;
                }
            return Cache[key] = Make(Flip(px, W, H), W, H, Vector4.zero, "menu_fan");
        }

        // ── plumbing ────────────────────────────────────────────────────────────────────────────────────────────────

        private static Color32[] Rows(Color32[][] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = rows[y][x];
            return px;
        }

        /// <summary>Written top-down; a texture counts up.</summary>
        private static Color32[] Flip(Color32[] px, int w, int h)
        {
            var flipped = new Color32[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
            return flipped;
        }

        private static Sprite Make(Color32[] px, int w, int h, Vector4 border, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }
    }
}
