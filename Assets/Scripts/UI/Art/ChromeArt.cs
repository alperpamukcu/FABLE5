using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The UI's own furniture, drawn in code: the market's listing plate, its keys, and the
    /// small marks that stand in for a word.
    ///
    /// Two standing rules meet here. The first is the house rule that UI CHROME IS NEVER
    /// GENERATED — a plate or a button is a piece of the instrument, and a generator draws a
    /// picture OF one; that is exactly what the author kept reading as slop (2026-08-11: "AI
    /// slop olduğu belli oluyor"). The second is that nothing in this game asks a FONT for a
    /// picture: a star, a tick, an arrow taken from a typeface is a glyph the pixel faces do
    /// not carry, so it arrives as a fallback face at the wrong weight or as an empty box.
    /// Every mark below is authored pixel by pixel at the size it is drawn at.
    ///
    /// The marks are WHITE silhouettes on purpose. The caller tints them with the ink of the
    /// line they belong to, so colour says how the line lands and shape says what it is —
    /// the same split the inspector's buffs use.
    /// </summary>
    public static class ChromeArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ── the marks ───────────────────────────────────────────────────────────────
        // 16 rows of 16, '#' is ink. SIMPLE ON PURPOSE (the author, 2026-08-11: "daha basit
        // kullanışlı iconlar"). The generated set before these was a row of little
        // illustrations — a shaded crate, a register with a keypad — and at 16 pixels an
        // illustration is mud. A mark has one silhouette and it has to survive being read at
        // a glance beside a number.

        private static readonly Dictionary<string, string[]> Masks = new Dictionary<string, string[]>
        {
            // SALES — a cocktail glass, the thing that was sold.
            ["sales"] = new[]
            {
                "................",
                "..############..",
                "..############..",
                "...##########...",
                "....########....",
                ".....######.....",
                "......####......",
                ".......##.......",
                ".......##.......",
                ".......##.......",
                ".......##.......",
                ".......##.......",
                "....########....",
                "....########....",
                "................",
                "................",
            },
            // TIPS — a coin, left on the counter.
            ["tips"] = new[]
            {
                "................",
                ".....######.....",
                "...##########...",
                "..############..",
                ".##############.",
                ".##############.",
                "################",
                "################",
                "################",
                "################",
                ".##############.",
                ".##############.",
                "..############..",
                "...##########...",
                ".....######.....",
                "................",
            },
            // RENT — a door key: the room is somebody else's.
            ["rent"] = new[]
            {
                "................",
                "...########.....",
                "..##########....",
                "..###....###....",
                "..##......##....",
                "..###....###....",
                "..##########....",
                "...########.....",
                "......##........",
                "......##........",
                "......####......",
                "......##........",
                "......####......",
                "......##........",
                "......##........",
                "................",
            },
            // STOCK — a crate, banded corner to corner.
            ["stock"] = new[]
            {
                "................",
                "................",
                "..############..",
                "..############..",
                "..##........##..",
                "..##.##..##.##..",
                "..##..####..##..",
                "..##...##...##..",
                "..##...##...##..",
                "..##..####..##..",
                "..##.##..##.##..",
                "..##........##..",
                "..############..",
                "..############..",
                "................",
                "................",
            },
            // SHOP — a carried bag.
            ["shop"] = new[]
            {
                "................",
                "......####......",
                ".....##..##.....",
                ".....##..##.....",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "..############..",
                "................",
                "................",
            },
            // NET — lines added up and ruled off twice. A balance scale was tried and at 16
            // pixels a scale is three grey smudges; a total is a total.
            ["net"] = new[]
            {
                "................",
                "................",
                "...##########...",
                "...##########...",
                "................",
                "..############..",
                "..############..",
                "................",
                ".##############.",
                ".##############.",
                "................",
                "################",
                "################",
                "################",
                "................",
                "................",
            },
            // TILL — the drawer, standing open.
            ["till"] = new[]
            {
                "................",
                "................",
                ".....######.....",
                ".....##..##.....",
                ".....######.....",
                "................",
                "..############..",
                "..############..",
                "..##........##..",
                "..##.######.##..",
                "..##.######.##..",
                "..##........##..",
                "..############..",
                "..############..",
                "................",
                "................",
            },
            // A STAR, because the game counts in them and no pixel face carries one. This is
            // the silhouette the licence and the standing row already wear, traced off
            // Items/star.png — the bar counts in ONE star, not in two that nearly match.
            ["star"] = new[]
            {
                "................",
                ".......##.......",
                "......####......",
                "......####......",
                "......####......",
                "..############..",
                ".##############.",
                "..############..",
                "...##########...",
                "....########....",
                "....########....",
                "...##########...",
                "...####..####...",
                "....##....##....",
                "................",
                "................",
            },
            // A TICK, for a thing already done.
            ["tick"] = new[]
            {
                "................",
                "................",
                "............###.",
                "...........###..",
                "..........###...",
                ".........###....",
                "..##....###.....",
                "..###..###......",
                "...######.......",
                "....####........",
                ".....##.........",
                "................",
                "................",
                "................",
                "................",
                "................",
            },
            // A COG, for the one control that is not part of the night.
            ["cog"] = new[]
            {
                "................",
                "....##....##....",
                "....##....##....",
                "..##########....",
                "..############..",
                "##...####...####",
                "##..######..####",
                "....##..##......",
                "....##..##......",
                "##..######..####",
                "##...####...####",
                "..############..",
                "..##########....",
                "....##....##....",
                "....##....##....",
                "................",
            },

            // ── the bench's four steps (2026-08-14, the author: "sırayla ne yapılmalı
            // ionlarla anlat"). Drawn at the same 16x16 as every other mark, so the card
            // reads as part of the chrome rather than as four pictures borrowed from
            // somewhere else.

            // POUR — a bottle tipped over, its stream falling.
            ["pour"] = new[]
            {
                "..####..........",
                "..####..........",
                ".######.........",
                "#########.......",
                "###########.....",
                "#############...",
                "###########.....",
                ".#########......",
                "......###.......",
                ".......#........",
                ".......#........",
                "......#.........",
                "......#.........",
                ".....#..........",
                ".....#..........",
                "................",
            },

            // CAP — a lid, seen from the side, coming down onto a rim.
            ["cap"] = new[]
            {
                "................",
                "....########....",
                "...##########...",
                "..############..",
                "..############..",
                "..############..",
                "...##########...",
                "................",
                "................",
                ".....######.....",
                "................",
                "..############..",
                "..#..........#..",
                "..#..........#..",
                "..#..........#..",
                "................",
            },

            // MIX — the tin in motion: a body with speed lines either side of it.
            ["mix"] = new[]
            {
                "................",
                "#....######....#",
                "##...######...##",
                "#....######....#",
                "....########....",
                "#...########...#",
                "##..########..##",
                "#...########...#",
                "....########....",
                "#...########...#",
                "##..########..##",
                "#...########...#",
                "....########....",
                ".....######.....",
                "................",
                "................",
            },

            // GLASS — the way on: an arrow into a waiting glass.
            ["toglass"] = new[]
            {
                "................",
                "......#.........",
                "......##........",
                "..#######.......",
                "..########......",
                "..#######.......",
                "......##........",
                "......#.........",
                "................",
                "...##########...",
                "...##########...",
                "....########....",
                ".....######.....",
                "......####......",
                "................",
                "................",
            },

            // GARNISH — a wedge of citrus, cut face out. The counter's optional step, and
            // the only one of the serve card's three that is not a gate.
            ["garnish"] = new[]
            {
                "................",
                "................",
                ".....#####......",
                "...#########....",
                "..###########...",
                ".#####...#####..",
                ".####..#..####..",
                ".###..###..###..",
                ".##..#####..##..",
                ".##...###...##..",
                ".#############..",
                "..###########...",
                "................",
                "................",
                "................",
                "................",
            },

            // SERVE — the finished glass, moving: a footed glass with the carry arrow
            // beside it. The last step of the counter, which is a walk, not a press.
            ["serve"] = new[]
            {
                "................",
                ".########.......",
                ".########.......",
                ".########.......",
                ".########...#...",
                ".########...##..",
                "..######..#####.",
                "..######..######",
                "..######..#####.",
                "...####.....##..",
                "...####.....#...",
                "....##..........",
                "....##..........",
                ".########.......",
                ".########.......",
                "................",
            },
        };

        /// <summary>One 16x16 mark, white, for the caller to tint. Null for a name that has
        /// no mask — a missing mark leaves a gap, it does not throw.</summary>
        public static Sprite Mark(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = "mark:" + name;
            // A DESTROYED SPRITE IS NOT A CACHE HIT (2026-08-13, measured: the settings key
            // drew the word SETTINGS instead of its cog on every play after the first). These
            // sprites are made at runtime and die with play mode, while the static cache
            // survives it — the project runs with domain reload off — so the second session
            // reads back a corpse. Unity's own == already answers this correctly; it just has
            // to be asked. BottleArt, ItemArt and PrefArt learned it earlier; these had not.
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            if (!Masks.TryGetValue(name, out var mask)) return null;

            const int S = 16;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    // The mask is authored top-down, the way it reads in source; the texture
                    // counts up. Row S-1-y is the same row the eye is on.
                    px[y * S + x] = mask[S - 1 - y][x] == '#'
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
            return Cache[key] = Make(px, S, S, Vector4.zero);
        }

        // ── the marquee lamp ────────────────────────────────────────────────────────
        //
        // The board's week is seven of these on a wire (2026-08-14, the author: "takvim
        // tasarımı kötü, kutu kutu"). It was drawn as a filled 8x8 rect with a bigger rect
        // behind it for the glow, which is a box behind a box — the exact thing that was
        // being complained about. A lamp is ROUND, and the light coming off it falls away.
        //
        // Both are drawn at the size they are used, never scaled: a 16px circle stretched to
        // 10 comes back with a lumpy edge, and the whole board is 8px art.

        /// <summary>The bulb itself: a round lamp, hard-edged, white for the caller to tint.</summary>
        public static Sprite Lamp()
        {
            const string Key = "lamp:bulb";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 16;
            const float C = 7.5f, R = 4.2f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - C, dy = y - C;
                    px[y * S + x] = dx * dx + dy * dy <= R * R
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            return Cache[Key] = Make(px, S, S, Vector4.zero);
        }

        /// <summary>The light coming off a lit one: a round falloff in four steps. Four
        /// steps and not a smooth ramp — a gradient under art drawn at 8px reads as a
        /// smudge, and the rest of this game's light is banded too.</summary>
        public static Sprite LampGlow()
        {
            const string Key = "lamp:glow";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 24;
            const float C = 11.5f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - C, dy = y - C;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = d <= 4.5f ? (byte)150
                        : d <= 6.5f ? (byte)78
                        : d <= 8.5f ? (byte)34
                        : d <= 10.5f ? (byte)12
                        : (byte)0;
                    px[y * S + x] = new Color32(255, 255, 255, a);
                }
            return Cache[Key] = Make(px, S, S, Vector4.zero);
        }

        // ── the plates ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A LISTING PLATE: the card a product stands on in the market. Grey-scale by
        /// construction, because the tile is tinted with the state's own paper — so the
        /// border comes out as a darker shade of whatever colour the listing is wearing and
        /// the card is one material rather than a frame stuck to a fill.
        ///
        /// The shape is the whole argument: chamfered corners (a square card on a square
        /// page is a table cell), one hairline rule around it, and two shaded rows along the
        /// bottom so the card sits ON the page instead of being a hole in it.
        /// </summary>
        public static Sprite Card()
        {
            const string Key = "plate:card";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int W = 24, H = 24, Cut = 2;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cx = Mathf.Min(x, W - 1 - x), cy = Mathf.Min(y, H - 1 - y);
                    if (cx + cy < Cut) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    byte v;
                    if (cx + cy == Cut || cx == 0 || cy == 0) v = 150;         // the rule
                    else if (y <= 2) v = 226;                                  // the card's foot
                    else if (y == H - 2) v = 255;                              // its lit top
                    else v = 250;
                    px[y * W + x] = new Color32(v, v, v, 255);
                }
            return Cache[Key] = Make(px, W, H, new Vector4(8, 8, 8, 8));
        }

        /// <summary>
        /// A KEY — the market's one control, and the shape of every button that commits
        /// something. Tinted by the state, so the same drawing is the green ADD, the amber
        /// PICKED and the grey refusal. It has a THROW: two dark rows along the bottom that
        /// read as the side of a key standing above the page, which is the difference
        /// between a button and a coloured rectangle with a word in it.
        /// </summary>
        public static Sprite Key() => KeySprite("plate:key", false);

        private static Sprite KeySprite(string key, bool down)
        {
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int W = 20, H = 20;
            int throwH = down ? 1 : 3;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cx = Mathf.Min(x, W - 1 - x), cy = Mathf.Min(y, H - 1 - y);
                    if (cx + cy < 1) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    byte v;
                    if (cx == 0 || cy == 0) v = 90;                            // the key's edge
                    else if (y < throwH + 1) v = 150;                          // its throw
                    else if (y == H - 2) v = 255;                              // the lit face
                    else v = 224;
                    px[y * W + x] = new Color32(v, v, v, 255);
                }
            return Cache[key] = Make(px, W, H, new Vector4(6, throwH + 2, 6, 4));
        }

        // ── the pour gauge (2026-08-20) ─────────────────────────────────────────────
        //
        // A SIGHT GLASS — the level tube down the side of a keg. It exists because the first
        // draft of the perfect-pour display was five coloured squares in a row, which is §6.1
        // ("a row of equal boxes... the single loudest tell") and §6.8 ("a dot standing in for
        // an object") at once, and the author said so in his own words: "sadece yan yana duran
        // kutular gibi duruyor". §6.1's fix is the instruction followed here — decide what the
        // surface IS, then put things on it. It is a tube of glass in a brass collar with the
        // measures scratched on it, and what it holds is how much of the drink this bottle is.
        //
        // Three pieces, because a gauge is three things: the TUBE it is read through, the
        // LADDER of liquid inside, and the GLASS with the measures on it. The ladder is a
        // five-texel texture drawn with point filtering — flat runs, hard edges between them,
        // the same construction §6.10 rules legal for the market's fade — so filling it to
        // 60% shows red, orange and yellow whole and nothing of the green.

        /// <summary>
        /// The empty tube: a channel sunk into whatever it is standing on, with a hairline
        /// collar and a lit bottom lip.
        ///
        /// GREY-SCALE BY CONSTRUCTION, the same argument <see cref="Card"/> makes: the gauge
        /// is tinted with its SURFACE's own ink, so the recess comes out as a shade of the
        /// paper it is cut into rather than a black slab laid on top of it. The first draft
        /// was dark glass in Night violet and it was the heaviest thing on a cream recipe
        /// card — which is §6.3 (the screen's subject should be its biggest reading, and the
        /// subject is the drink's name, not its syrup measure).
        /// </summary>
        public static Sprite GaugeTube(int w, int h)
        {
            string key = $"gauge:tube:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            void Set(int x, int ty, byte v)
            {
                if (x < 0 || x >= w || ty < 0 || ty >= h) return;
                px[(h - 1 - ty) * w + x] = new Color32(v, v, v, 255);
            }
            for (int ty = 0; ty < h; ty++)
                for (int x = 0; x < w; x++)
                {
                    bool endCut = (x == 0 || x == w - 1) && (ty == 0 || ty == h - 1);
                    if (endCut) { px[(h - 1 - ty) * w + x] = clear; continue; }   // chamfer
                    if (ty == 0) { Set(x, ty, 96); continue; }        // the sawn top edge
                    if (ty == h - 1) { Set(x, ty, 255); continue; }   // the lip, catching light
                    if (x == 0 || x == w - 1) { Set(x, ty, 96); continue; }
                    Set(x, ty, ty == 1 ? (byte)150 : (byte)196);      // shadow under the rim
                }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        /// <summary>The liquid ladder: one texel per 20-point measure, in the reading's own
        /// colours. Drawn as a filled image, so the level cuts it on a measure line exactly.</summary>
        public static Sprite GaugeLadder(Color[] bands)
        {
            string key = "gauge:ladder";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[bands.Length];
            for (int i = 0; i < bands.Length; i++) px[i] = bands[i];
            return Cache[key] = Make(px, bands.Length, 1, Vector4.zero);
        }

        /// <summary>The glass over the liquid: the measures scratched at every 20 points, and
        /// the shine along the top. Transparent everywhere else, so it reads as one object
        /// with the tube rather than a frame stuck over a fill.</summary>
        public static Sprite GaugeGlass(int w, int h, int steps)
        {
            string key = $"gauge:glass:{w}x{h}x{steps}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var clear = new Color32(0, 0, 0, 0);
            var scratch = new Color32(0, 0, 0, 90);            // a measure, cut into the glass
            var shine = new Color32(255, 255, 255, 26);        // the light along the tube's top
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            void Set(int x, int ty, Color32 c)
            {
                if (x < 0 || x >= w || ty < 0 || ty >= h) return;
                px[(h - 1 - ty) * w + x] = c;
            }
            int inner = w - 2;
            for (int x = 1; x < w - 1; x++) Set(x, 2, shine);
            // The measures are TRUE about the content (§6.9): one at every box boundary, and
            // none at the ends, where the collar already says where the tube stops.
            for (int s = 1; s < steps; s++)
            {
                int x = 1 + inner * s / steps;
                for (int ty = 1; ty < h - 1; ty++) Set(x, ty, scratch);
            }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        private static Sprite Make(Color32[] px, int w, int h, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
