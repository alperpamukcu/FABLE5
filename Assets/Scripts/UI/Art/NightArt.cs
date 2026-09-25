using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE PALM WALL DIRECTION (2026-09-15, the author, choosing it from three: "Palmiye Duvarı kullanılsın. Arkaplan
    /// için pixelart oluşturulsun eğer beğenilmezse sabit düz desenli bir arkaplan"). The night the menus stand in
    /// front of, the plates the menus and the hover tips stand on, and the marks their keys carry — all drawn here
    /// from the palette, pixel by pixel, because UI chrome is never generated in this project (GDD 14 §2; ChromeArt's
    /// rule). Everything is cached the way ChromeArt caches: a sprite made in play mode dies with it, and a dead
    /// sprite is not a hit.
    ///
    /// The night is drawn at HALF size and shown at exactly 2x, which keeps it pixel art (16 §3), and it is drawn
    /// to the size it is asked for: since the author kept the room as the menus' backdrop ("arkaplan ana ekran
    /// kalsın sadece butonların üstünde olduğu çerçevenin arkaplanı olsun", the same day), the night stands INSIDE
    /// each menu's frame — the pause menu's at 217x247, the settings' at 397x267 — with the same sky, sun, city,
    /// sea, road and palms laid out to whatever box it gets. <see cref="UseFlatBackdrop"/> swaps it for the flat
    /// pattern the author asked for as the fallback, one line.
    /// </summary>
    public static class NightArt
    {
        /// <summary>The author's fallback, now the choice (2026-09-16: "arkaplan olarak daha az göz alan bir arkaplan
        /// seçilsin"): a quiet glass inside the frame instead of the drawn night, which stays one line away.</summary>
        public const bool UseFlatBackdrop = true;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Color32 C(Color c) => c;

        private static Sprite Make(Color32[] px, int w, int h, Vector4 border, FilterMode filter = FilterMode.Point)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>A small deterministic generator: the picture must be the same every time it is drawn (the look
        /// tests compare pixels), and game logic never sees it, so the determinism rule is not the reason — repeatability is.</summary>
        private struct Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / 16777216f; }
            public int Range(int n) => (int)(Next() * n);
        }

        // ── the night ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>What stands inside a menu's frame, drawn at <paramref name="w"/>x<paramref name="h"/> to be
        /// shown at 2x: the night, or the glass when <see cref="UseFlatBackdrop"/>.</summary>
        public static Sprite Picture(int w, int h) => UseFlatBackdrop ? Glass(w, h) : Night(w, h);

        /// <summary>The drawn night at any size: the composition of the first 640x360 wall, its heights scaled to
        /// the box and its palms stood at the same fractions of the width; the small pair of palms only where there
        /// is room for them.</summary>
        public static Sprite Night(int W, int H)
        {
            string key = "night:" + W + "x" + H;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            int Horizon = Mathf.RoundToInt(H * 0.6f);
            float k = H / 360f;                                    // against the wall the night was first drawn at
            var px = new Color32[W * H];
            var rng = new Rng(21);
            void Set(int x, int ty, Color32 c) { if (x >= 0 && x < W && ty >= 0 && ty < H) px[(H - 1 - ty) * W + x] = c; }
            Color32 At(int x, int ty) => x >= 0 && x < W && ty >= 0 && ty < H ? px[(H - 1 - ty) * W + x] : default;
            void Box(int x, int ty, int w, int h, Color32 c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, ty + j, c); }

            // the sky: seven bands from the night down into the sunset
            var sky = new[] { UITheme.Night[0], UITheme.ClubBlue[0], UITheme.ClubBlue[1], UITheme.Night[4], UITheme.Magenta[0], UITheme.Magenta[1], UITheme.Magenta[2] };
            int bandH = Mathf.CeilToInt(Horizon / (float)sky.Length);
            for (int i = 0; i < sky.Length; i++) Box(0, i * bandH, W, bandH, C(sky[i]));

            // stars, denser and brighter high up, none in the glow
            int stars = Mathf.RoundToInt(160f * W * H / (640f * 360f));
            for (int i = 0; i < stars; i++)
            {
                int x = rng.Range(W), ty = rng.Range(Mathf.RoundToInt(Horizon * 0.7f));
                var col = rng.Next() < 0.7f ? C(UITheme.Cream[4]) : C(UITheme.Cyan[4]);
                Set(x, ty, col);
                if (rng.Next() < 0.12f) { Set(x - 1, ty, C(UITheme.Cream[2])); Set(x + 1, ty, C(UITheme.Cream[2])); Set(x, ty - 1, C(UITheme.Cream[2])); Set(x, ty + 1, C(UITheme.Cream[2])); }
            }

            // the halo, then the sun: banded, its lower half cut by the widening dark stripes of the decade's every sunset
            int Cx = W / 2, Rad = Mathf.RoundToInt(H * 0.25f);
            int sunFoot = Horizon - Mathf.Max(2, Mathf.RoundToInt(4f * k));
            foreach (var (rr, col) in new[] { (Mathf.RoundToInt(Rad * 1.56f), UITheme.Magenta[0]), (Mathf.RoundToInt(Rad * 1.31f), UITheme.Magenta[1]) })
                for (int yy = -rr; yy < 0; yy++)
                {
                    int half = Mathf.RoundToInt(Mathf.Sqrt(rr * rr - yy * yy));
                    for (int x = Cx - half; x <= Cx + half; x++) if (At(x, sunFoot + yy).r == C(sky[Mathf.Min(sky.Length - 1, (sunFoot + yy) / bandH)]).r) Set(x, sunFoot + yy, C(col));
                }
            var sun = new[] { UITheme.Amber[4], UITheme.Amber[3], UITheme.Amber[2], UITheme.Magenta[3], UITheme.Magenta[2], UITheme.Magenta[1] };
            for (int yy = -Rad; yy < 0; yy++)
            {
                float t = (yy + Rad) / (float)Rad;                       // 0 at the top of the disc, 1 at its foot
                if (t > 0.45f && (-yy) % 11 < Mathf.RoundToInt((t - 0.45f) * 13f)) continue;
                int half = Mathf.RoundToInt(Mathf.Sqrt(Rad * Rad - yy * yy));
                var col = C(sun[Mathf.Min(sun.Length - 1, Mathf.FloorToInt(t * sun.Length))]);
                Box(Cx - half, sunFoot + yy, half * 2, 1, col);
            }

            // the far city: two rows of towers, the near row darker, windows lit amber and cyan
            for (int row = 0; row < 2; row++)
            {
                int x = -10;
                var body = row == 0 ? C(UITheme.Night[3]) : C(UITheme.Night[1]);
                while (x < W + 20)
                {
                    int w = 12 + rng.Range(5) * 4, h = Mathf.RoundToInt(((row == 0 ? 20 : 45) + rng.Range(row == 0 ? 15 : 30) * 2) * k);
                    Box(x, Horizon - h, w, h, body);
                    if (rng.Next() < 0.3f) Box(x + w / 2 - 1, Horizon - h - 5, 2, 5, body);
                    for (int wy = Horizon - h + 3; wy < Horizon - 3; wy += 4)
                        for (int wx = x + 2; wx < x + w - 2; wx += 4)
                            if (rng.Next() < (row == 0 ? 0.25f : 0.4f))
                                Box(wx, wy, 2, 2, rng.Next() < 0.75f ? C(row == 0 ? UITheme.Amber[2] : UITheme.Amber[3]) : C(UITheme.Cyan[3]));
                    x += w + 2 + rng.Range(3) * 2;
                }
            }

            // the sea: the sun broken into bars that thin as they come nearer, a scatter of lit ripples
            int SeaH = Mathf.Max(8, Mathf.RoundToInt(35f * k));
            Box(0, Horizon, W, SeaH, C(UITheme.ClubBlue[0]));
            for (int y = Horizon; y < Horizon + SeaH; y += 2)
            {
                float t = (y - Horizon) / (float)SeaH;
                int half = Mathf.RoundToInt((30f + t * 70f) * W / 640f);
                if ((y / 2) % 2 == 0) Box(Cx - half, y, half * 2, 1, C(t < 0.4f ? UITheme.Amber[2] : UITheme.Magenta[2]));
                for (int x = 0; x < W; x += 8) if ((x * 7 + y * 3) % 23 < 3) Box(x, y, 3, 1, C(UITheme.ClubBlue[2]));
            }

            // the road: a grid to the foot, its lines cyan on the night
            int floorTop = Horizon + SeaH;
            Box(0, floorTop, W, H - floorTop, C(UITheme.Night[1]));
            for (int q = 0; q < 14; q++) Box(0, floorTop + Mathf.RoundToInt(q * q * 1.1f), W, 1, C(UITheme.Cyan[2]));
            int lane = Mathf.Max(20, Mathf.RoundToInt(75f * W / 640f));
            for (int i = -16; i <= 16; i++)
            {
                int x1 = Cx + i * lane;
                for (float s = 0f; s <= 1f; s += 0.004f) Set(Mathf.RoundToInt(Cx + (x1 - Cx) * s), Mathf.RoundToInt(floorTop + (H - floorTop) * s), C(UITheme.Cyan[2]));
            }
            Box(0, floorTop, W, 2, C(UITheme.Cyan[4]));

            // palms: a leaning trunk of stacked segments, seven sagging fronds with leaflets, coconuts at the crown
            var palms = new List<(int bx, int dir, bool small)> { (Mathf.RoundToInt(W * 0.094f), 1, false), (Mathf.RoundToInt(W * 0.906f), -1, false) };
            if (W >= 400) { palms.Add((Mathf.RoundToInt(W * 0.031f), 1, true)); palms.Add((Mathf.RoundToInt(W * 0.969f), -1, true)); }
            foreach (var (bx, dir, small) in palms)
            {
                int hgt = Mathf.RoundToInt((small ? 120 : 170) * k), lean = Mathf.RoundToInt((small ? 15 : 35) * k);
                var ink = C(UITheme.Night[0]);
                for (float t = 0f; t < 1f; t += 0.02f)
                {
                    int x = Mathf.RoundToInt(bx + dir * lean * t * t), y = Mathf.RoundToInt(floorTop + 15 * k - t * hgt);
                    Box(x - 3 + Mathf.RoundToInt(t * 1.5f), y, 7 - Mathf.RoundToInt(t * 3f), 3, ((int)(t * 50f)) % 2 == 0 ? ink : C(UITheme.Night[1]));
                }
                int tx = bx + dir * lean, ty = Mathf.RoundToInt(floorTop + 15 * k) - hgt;
                for (int f = 0; f < 7; f++)
                {
                    float ang = (-165f + f * 42f) * Mathf.Deg2Rad;
                    int len = Mathf.RoundToInt((small ? 40 : 60) * k);
                    for (int d = 0; d < len; d += 2)
                    {
                        float sag = d * d * 0.01f;
                        int x = Mathf.RoundToInt(tx + Mathf.Cos(ang) * d), y = Mathf.RoundToInt(ty + Mathf.Sin(ang) * d + sag);
                        Box(x - 1, y, 2, 2, ink);
                        if (d % 6 == 0 && d > 6) { int n = Mathf.Max(1, 5 - d / 14); Box(x - n, y + 1, 1, n, ink); Box(x + n, y + 1, 1, n, ink); }
                    }
                }
                for (int q = 0; q < 5; q++) Box(tx - 4 + q * 2, ty + 1 + (q % 2) * 2, 2, 2, ink);
            }

            return Cache[key] = Make(px, W, H, Vector4.zero);
        }

        /// <summary>The glass: the night in four bands, a shade lighter at the top than at the foot, and nothing else
        /// — a surface, not a pattern (2026-09-16, the author, of the gridded tile that stood here for an hour: "Bu
        /// desen built sahnesindeki tezgah desenine benziyor, olmaz"). The scanlines over it are the only texture.</summary>
        private static Sprite Glass(int W, int H)
        {
            string key = "night:glass:" + W + "x" + H;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var bands = new[] { C(UITheme.Night[2]), C(UITheme.Night[1]), C(UITheme.Night[1]), C(UITheme.Night[0]) };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                var c = bands[Mathf.Min(bands.Length - 1, y * bands.Length / H)];   // y counts from the top
                for (int x = 0; x < W; x++) px[(H - 1 - y) * W + x] = c;
            }
            return Cache[key] = Make(px, W, H, Vector4.zero);
        }

        /// <summary>Four rows, the last one dark: tiled over a plate, the scanlines of the direction's every surface.
        /// Tinted by the caller's alpha.</summary>
        public static Sprite Scanlines()
        {
            const string Key = "night:scan";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            var px = new Color32[4];
            px[0] = new Color32(255, 255, 255, 255);   // the bottom row of the tile
            for (int i = 1; i < 4; i++) px[i] = new Color32(255, 255, 255, 0);
            return Cache[Key] = Make(px, 1, 4, Vector4.zero);
        }

        /// <summary>
        /// RAIN ON THE DOOR (2026-09-25, the ESC menu's picture - MenuRain scrolls it): a 64x96 tile of short falling
        /// streaks, one texel wide and three to six long, a brighter head at each foot, a white mask the caller inks
        /// with a token. Seeded, so the same rain falls every time; Repeat, so it tiles as it scrolls.
        /// </summary>
        public static Sprite RainTile()
        {
            const string Key = "night:rain";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int W = 64, H = 96, Streaks = 18;
            var px = new Color32[W * H];
            var rng = new Rng(0xA11CE5u);
            for (int s = 0; s < Streaks; s++)
            {
                int x = rng.Range(W), y = rng.Range(H), len = 3 + rng.Range(4);
                for (int k = 0; k < len; k++)
                {
                    int yy = (y + k) % H;                               // texture rows run upward: y is the streak's foot
                    px[yy * W + x] = new Color32(255, 255, 255, (byte)(k == 0 ? 255 : 150));
                }
            }
            return Cache[Key] = Make(px, W, H, Vector4.zero);
        }

        // ── the plates ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The menu plate: a night glass with a two-unit cyan frame, a club-blue inner line, and its corners
        /// cut. 9-sliced at 8; the caller sizes it.</summary>
        public static Sprite MenuPlate() => Plate("night:plate", true);

        /// <summary>The same frame with nothing inside it, to stand OVER the drawn night (2026-09-15): the two-unit
        /// cyan edge, the cut corners, and the inner line six units in, which reads as the picture's mat.</summary>
        public static Sprite MenuFrame() => Plate("night:frame", false);

        private static Sprite Plate(string key, bool filled)
        {
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int S = 24;
            var px = new Color32[S * S];
            var fill = filled ? C(UITheme.Night[1]) : new Color32(0, 0, 0, 0); if (filled) fill.a = 232;
            var frame = C(UITheme.Cyan[3]);
            var inner = C(UITheme.ClubBlue[3]);
            var cut = new Color32(0, 0, 0, 0);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= S - 2 || y >= S - 2;
                    bool ring = (x == 6 || y == 6 || x == S - 7 || y == S - 7) && x >= 6 && y >= 6 && x <= S - 7 && y <= S - 7;
                    bool corner = (x < 3 && y < 3) || (x < 3 && y >= S - 3) || (x >= S - 3 && y < 3) || (x >= S - 3 && y >= S - 3);
                    px[y * S + x] = corner ? cut : edge ? frame : ring ? inner : fill;
                }
            return Cache[key] = Make(px, S, S, new Vector4(8, 8, 8, 8));
        }

        /// <summary>The hover tip's plate: the same glass and frame, smaller and with no inner line. 9-sliced at 4.</summary>
        public static Sprite TipPlate()
        {
            const string Key = "night:tip";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 12;
            var px = new Color32[S * S];
            var fill = C(UITheme.Night[1]); fill.a = 245;
            var frame = C(UITheme.Cyan[3]);
            var top = C(UITheme.Cyan[2]);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= S - 2 || y >= S - 2;
                    bool underTop = y == S - 3;   // the row under the top edge, one shade darker: the lip
                    px[y * S + x] = edge ? frame : underTop ? top : fill;
                }
            return Cache[Key] = Make(px, S, S, new Vector4(4, 4, 4, 4));
        }

        // ── the marks ─────────────────────────────────────────────────────────────────────────────────────────────
        // 8x8 masks drawn at 2 texels a cell into the 16x16 a key inlays (16 §1: a mark is a drawing, never a glyph).

        private static readonly Dictionary<string, string[]> Masks = new Dictionary<string, string[]>
        {
            ["play"] = new[] { "X.......", "XXX.....", "XXXXX...", "XXXXXXX.", "XXXXX...", "XXX.....", "X.......", "........" },
            ["pause"] = new[] { "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "........" },
            ["next"] = new[] { "X....XX.", "XX...XX.", "XXX..XX.", "XXXX.XX.", "XXX..XX.", "XX...XX.", "X....XX.", "........" },
            ["prev"] = new[] { ".XX....X", ".XX...XX", ".XX..XXX", ".XX.XXXX", ".XX..XXX", ".XX...XX", ".XX....X", "........" },
            ["back"] = new[] { "...X....", "..XX....", ".XXXXXX.", "XXXXXXX.", ".XXXXXX.", "..XX....", "...X....", "........" },
            ["save"] = new[] { "XXXXXXX.", "X.XXX.X.", "X.XXX.X.", "X.....X.", "X.XXX.X.", "X.XXX.X.", "XXXXXXX.", "........" },
            ["door"] = new[] { "XXXXX...", "X...X...", "X...X.X.", "X..XXXXX", "X...X.X.", "X...X...", "XXXXX...", "........" },
            ["note"] = new[] { "...XXXXX", "...X...X", "...X...X", "...X...X", ".XXX.XXX", "XXXXXXXX", ".XX..XX.", "........" },
            ["speaker"] = new[] { "...X....", "..XX..X.", "XXXX...X", "XXXX.X.X", "XXXX...X", "..XX..X.", "...X....", "........" },
            ["redo"] = new[] { ".XXXXX..", "X.....X.", "X.......", "X....XXX", "X.....X.", "X.....X.", ".XXXXX..", "........" },
            ["clock"] = new[] { ".XXXXXX.", "X..X...X", "X..X...X", "X..XXX.X", "X......X", "X......X", ".XXXXXX.", "........" },
            ["moon"] = new[] { "...XXX..", "..X.....", ".X......", ".X......", ".X......", "..X.....", "...XXX..", "........" },
            ["key"] = new[] { "..XXX...", ".X...X..", ".X...X..", "..XXX...", "...X....", "...XXX..", "...X.X..", "........" },
            ["book"] = new[] { "XXX.XXX.", "X.XXX.X.", "X.X.X.X.", "X.X.X.X.", "X.X.X.X.", "X.XXX.X.", "XXX.XXX.", "........" },
            ["bottle"] = new[] { "...XX...", "...XX...", "..XXXX..", ".XXXXXX.", ".XXXXXX.", ".XXXXXX.", ".XXXXXX.", "..XXXX.." },
            ["cash"] = new[] { "........", "XXXXXXXX", "X.XXXX.X", "X.X..X.X", "X.X..X.X", "X.XXXX.X", "XXXXXXXX", "........" },
            ["glass"] = new[] { "XXXXXXX.", ".X...X..", ".X...X..", "..X.X...", "...X....", "...X....", ".XXXXX..", "........" },
        };

        /// <summary>One 16x16 mark, white, for the caller to tint; null for a name with no mask.</summary>
        public static Sprite Mark(string name)
        {
            string key = "night:mark:" + name;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            if (!Masks.TryGetValue(name, out var mask)) return null;
            const int S = 16;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = mask[7 - y / 2][x / 2] == 'X' ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            return Cache[key] = Make(px, S, S, Vector4.zero);
        }
    }
}
