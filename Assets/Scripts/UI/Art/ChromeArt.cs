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
            // WHICH WAY A READING MOVED (2026-08-25). Drawn, because the pixel faces carry
            // no arrow either — PressStart2P has no U+25B2, and the last time a glyph was
            // assumed the night's slip printed five tofu boxes where its stars should have
            // been. Symmetric across the vertical, so the SAME mask turned half a turn is
            // the fall: one drawing, two directions, and they can never disagree in weight.
            ["rise"] = new[]
            {
                "................",
                "................",
                ".......##.......",
                "......####......",
                ".....######.....",
                "....########....",
                "...##########...",
                "..############..",
                "......####......",
                "......####......",
                "......####......",
                "......####......",
                "......####......",
                "................",
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
            // ── the serving spec, said in pictures (2026-08-19) ────────────────────
            // The ticket now prints WHAT they want beside HOW they want it, and how is four
            // things: ice, a twist of peel, a salted rim, a sugared rim. They are read at a
            // glance on a 236-wide card across the room, so each one is a SILHOUETTE that
            // survives at 14 units — no shading, no interior detail, and no two alike in
            // outline. The two rims are the hard pair: both are a glass mouth with something
            // on it, so salt is FEW AND CHUNKY and sugar is MANY AND FINE, which is also
            // what the two things actually look like on a rim.

            // ICE — a cube seen slightly from above, the way every glass in this game draws it.
            ["ice"] = new[]
            {
                "................",
                "................",
                "....########....",
                "...##......##...",
                "..##........##..",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                ".##..........##.",
                "..##........##..",
                "...##......##...",
                "....########....",
                "................",
                "................",
            },
            // TWIST — a curl of peel, cut and let go. A comma with a hook in it.
            // Keyed on the PREPARATION'S OWN ID, like the other three, so the ticket can ask
            // for a mark by the thing it is drawing rather than by a second name that has to
            // be kept in step with it.
            ["lemon_twist"] = new[]
            {
                "................",
                "................",
                ".....######.....",
                "...##......##...",
                "..##........##..",
                "..##........##..",
                "..##.....####...",
                "..##....##......",
                "...##..##.......",
                "....####........",
                ".....##.........",
                "....##..........",
                "...##...........",
                "..##............",
                "................",
                "................",
            },
            // SALT RIM — the glass mouth, and coarse grains standing on it.
            ["salt_rim"] = new[]
            {
                "................",
                "................",
                "..##.......##...",
                ".####.....####..",
                "..##..###..##...",
                "......###.......",
                "................",
                "################",
                "################",
                "..############..",
                "..##........##..",
                "..##........##..",
                "...##......##...",
                "....########....",
                "................",
                "................",
            },
            // SUGAR RIM — the same mouth, and a fine band of it instead of grains.
            ["sugar_rim"] = new[]
            {
                "................",
                "................",
                "..#..#..#..#..#.",
                "................",
                ".#..#..#..#..#..",
                "................",
                "................",
                "################",
                "################",
                "..############..",
                "..##........##..",
                "..##........##..",
                "...##......##...",
                "....########....",
                "................",
                "................",
            },
            // MINIMISE — the 98 window box, and the only one of the three that is a bar.
            ["win_min"] = new[]
            {
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "...##########...",
                "...##########...",
                "................",
                "................",
            },
            // MAXIMISE — a window with its own title bar on it.
            ["win_max"] = new[]
            {
                "................",
                "..############..",
                "..############..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..##........##..",
                "..############..",
                "................",
                "................",
                "................",
            },
            // CLOSE — the X. Two units thick: at 10 units a one-unit X is a smudge.
            ["win_close"] = new[]
            {
                "................",
                ".##..........##.",
                ".###........###.",
                "..###......###..",
                "...###....###...",
                "....###..###....",
                ".....######.....",
                "......####......",
                "......####......",
                ".....######.....",
                "....###..###....",
                "...###....###...",
                "..###......###..",
                ".###........###.",
                ".##..........##.",
                "................",
            },
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

        /// <summary>
        /// A WELL: the recess an instrument's glass sits in, routed INTO the beam rather
        /// than standing on it (2026-08-19, the author: "profesyonel bir UI/UX designer
        /// gibi düşün" — the third cut of the top bar; boxes were refused twice and a
        /// generated plate once). The tell of a recess is that its bevel runs BACKWARDS
        /// from a box's: light falls from above, so the top edge of a cut is the dark one
        /// and the bottom lip is what catches the room. Baked colours, not greyscale —
        /// the floor IS the display glass (the panel's own dark seen through a tint,
        /// nothing near black), and a well is always cut into the one beam.
        ///
        /// 9-sliced with a 5px border: the chamfered corners and the edge rows are the
        /// caps, the floor stretches. One sprite, any width of instrument.
        /// </summary>
        public static Sprite Well()
        {
            const string Key = "well";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int S = 14, Ch = 2;
            var clear = new Color32(0, 0, 0, 0);
            var cut = new Color32(0x0D, 0x08, 0x13, 255);      // Night[0], the sawn edge
            var lip = new Color32(0x36, 0x24, 0x47, 255);      // Night[3], catching light
            var floor = new Color32(8, 14, 19, 255);           // the display glass
            var shade1 = new Color32(4, 7, 10, 255);           // the beam's shadow on it
            var shade2 = new Color32(6, 10, 14, 255);
            var px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = floor;
            // ty counts from the TOP the way the drawing reads; the buffer fills bottom-up.
            void Set(int x, int ty, Color32 c) => px[(S - 1 - ty) * S + x] = c;
            for (int x = 0; x < S; x++)
            {
                Set(x, 1, shade1);
                Set(x, 2, shade2);
                Set(x, 0, x < Ch || x >= S - Ch ? clear : cut);
                Set(x, S - 1, x < Ch || x >= S - Ch ? clear : lip);
            }
            for (int ty = 1; ty < S - 1; ty++)
            {
                bool corner = ty < Ch || ty >= S - Ch;
                Set(0, ty, corner ? clear : cut);
                Set(S - 1, ty, corner ? clear : cut);
            }
            Set(1, 1, cut); Set(S - 2, 1, cut);                // chamfer diagonals
            Set(1, S - 2, lip); Set(S - 2, S - 2, lip);
            return Cache[Key] = Make(px, S, S, new Vector4(5, 5, 5, 5));
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

        // ── the market's own chrome: the 98 key and the storefront's mark ───────────
        //
        // The market reads as a 90s trade site down to its BUTTONS now (2026-08-19, the
        // author: "buton tarzı olarak windows 98 gibi olsun ... internet sitesindeki tüm
        // butonları bu tarza getir"). One face serves them all, greyscale so the tint keeps
        // deciding what a key MEANS while this sprite decides what it IS.

        /// <summary>
        /// THE 98 KEY: a square raised plate with a two-step bevel — outer lit edge over an
        /// inner one, dark twin on the far corner — which is the whole grammar of that era's
        /// chrome and is four flat runs of grey, no gradient anywhere. The DOWN face inverts
        /// the bevel, because a 98 button does not travel when pressed: it turns inside out.
        /// Greyscale by construction like <see cref="Key"/>; tint with the market's paper
        /// for the classic face, or with a state colour for a key that commits something.
        /// </summary>
        public static Sprite Win98Key(bool down = false)
        {
            string key = "win98:key" + (down ? ":down" : "");
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int S = 14;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // Texture rows count up, so y = S-1 is the TOP of the drawn key.
                    bool topL0 = y == S - 1 || x == 0;
                    bool botR0 = y == 0 || x == S - 1;
                    bool topL1 = y == S - 2 || x == 1;
                    bool botR1 = y == 1 || x == S - 2;
                    byte v;
                    if (topL0) v = down ? (byte)90 : (byte)255;        // outer bevel
                    else if (botR0) v = down ? (byte)255 : (byte)90;
                    else if (topL1) v = down ? (byte)150 : (byte)240;  // inner bevel
                    else if (botR1) v = down ? (byte)240 : (byte)150;
                    else v = down ? (byte)208 : (byte)224;             // the face
                    px[y * S + x] = new Color32(v, v, v, 255);
                }
            return Cache[key] = Make(px, S, S, new Vector4(4, 4, 4, 4));
        }

        /// <summary>
        /// THE STOREFRONT'S MARK: a palm on its own island, water going out behind it — the
        /// island the vice fade's sun would set on. Hand-authored like every mark, white for
        /// the caller to tint; on the title bar it stands white on the fade beside the
        /// wordmark. 28x24 and drawn at 28x24: the strip is 40 tall and this is the biggest
        /// drawing that leaves the wordmark its seat.
        /// </summary>
        public static Sprite Isle()
        {
            const string Key = "vice:isle";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            string[] mask =
            {
                "..............###...........",
                "......###...#######.........",
                "....#######..#######........",
                "...####..####.#####.###.....",
                "..###.....#######..####.....",
                ".###......######....####....",
                ".##.......#####......###....",
                ".#.......######.......##....",
                "..........#.###.............",
                "..........#..##.............",
                ".............##.............",
                ".............##.............",
                "............###.............",
                "............##..............",
                "............##..............",
                "...........###..............",
                "...........##...............",
                ".......###########..........",
                ".....###############........",
                "...###################......",
                ".#########################..",
                "............................",
                "..##...###...###...###...##.",
                "............................",
            };
            const int W = 28, H = 24;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[(H - 1 - y) * W + x] = mask[y][x] == '#'
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
            return Cache[Key] = Make(px, W, H, Vector4.zero);
        }

        /// <summary>
        /// A STRIP OF SHADE — flat alpha bands running away from a lit edge, one texel per
        /// band, point-filtered like everything else here.
        ///
        /// A new named object (16 §1), because nothing in the kit fitted: LAMP's glow is a
        /// round falloff for a bulb, and what a shelf needs is the DARK that gathers at the
        /// back of a niche, which is a line, not a point.
        ///
        /// IT IS BLACK, AND THAT IS THE WHOLE POINT (2026-08-19). The first take was white
        /// and laid over the back bar as light; on a canvas an alpha blend toward white does
        /// not light a surface, it FOGS it — the wall came back as grey sheets with the
        /// cabinet's tilework washed out underneath. Shade multiplies the picture instead of
        /// replacing it, so every mark the plate carries survives being put in the dark, and
        /// the lit part of a shelf is simply the part no shade fell on. Light by subtraction
        /// is how a painted surface is lit; adding is how it is erased.
        ///
        /// Eight whole steps and no interpolated pixel — the same licence the vice fade holds
        /// (16 §6.10).
        /// </summary>
        public static Sprite StripShade(bool downward = true)
        {
            string key = "shade:strip" + (downward ? ":down" : ":up");
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            // Clear at the lit edge, deepening away from it.
            byte[] steps = { 0, 14, 27, 45, 70, 104, 150, 210 };
            var px = new Color32[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                // Texture rows count UP, so a shade that DEEPENS DOWNWARD wants its clear
                // band at the top texel, and the flip wants it at the bottom.
                int band = downward ? i : steps.Length - 1 - i;
                px[i] = new Color32(0, 0, 0, steps[band]);
            }
            return Cache[key] = Make(px, 1, steps.Length, Vector4.zero);
        }

        // ── the vice fade, and the wall it hangs on ─────────────────────────────────
        //
        // The market's storefront is a 90s trade site seen on the bar's tablet (2026-08-19).
        // Two things carry that and neither may be a smooth anything: the title bar's run
        // from vice blue to vice pink, and the faint Miami plate behind the aisle.

        /// <summary>
        /// The fade as a STRIP OF FLAT BANDS — `UITheme.ViceFade`, one texel per step, drawn
        /// with point filtering so a 1040-wide title bar comes out as flat runs of 40 with a
        /// hard edge between them (twenty-six bands since 2026-08-19; the band count lives
        /// on the token, this strip just wears it). This is the 98 title bar's own trick and
        /// it is why it is not a gradient (16 §6.10): there is no colour anywhere in it that
        /// is not one of the bands, at any width.
        ///
        /// Point filtering is the load-bearing part. On bilinear this texture IS a gradient,
        /// which is the exact thing being avoided — `Make` sets it, do not change it here.
        /// </summary>
        public static Sprite FadeStrip(bool horizontal = true)
        {
            string key = "vice:fade" + (horizontal ? "h" : "v");
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var bands = UITheme.ViceFade;
            int n = bands.Length;
            var px = new Color32[n];
            // Texel 0 is the left edge of a strip and the BOTTOM of a column, so band 0 —
            // the blue end — lands at the left of a title bar and at the foot of a rail.
            for (int i = 0; i < n; i++) px[i] = bands[i];
            return Cache[key] = Make(px, horizontal ? n : 1, horizontal ? 1 : n, Vector4.zero);
        }

        /// <summary>
        /// THE WALLPAPER. A Miami horizon — a banded sun, a grid running off to it, and two
        /// palms — drawn here in code and never generated. UI chrome is not made by a
        /// generator in this project (see this file's header), and a wallpaper sitting under
        /// the aisle is chrome: it is part of the instrument, not a picture in it.
        ///
        /// It is drawn at HALF the rect it fills and scaled x2, so every mark on it is two
        /// units across and it reads as pixel art rather than as a photograph gone quiet. It
        /// is authored WHITE-on-nothing for the caller to tint and fade, exactly like a mark,
        /// so the page underneath decides how loud it is — and the answer is: barely.
        /// </summary>
        public static Sprite PalmWall(int w, int h)
        {
            string key = $"vice:wall{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            int W = Mathf.Max(8, w / 2), H = Mathf.Max(8, h / 2);
            var px = new Color32[W * H];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            void Put(int x, int y, byte a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                if (px[i].a >= a) return;             // the brightest claim on a pixel wins
                px[i] = new Color32(255, 255, 255, a);
            }

            // THE SUN, low and centred right. Banded rings, not a falloff: five whole steps
            // with a gap between each, which is the one sun this decade ever drew.
            float sunX = W * 0.66f, sunY = H * 0.46f, sunR = Mathf.Min(W, H) * 0.30f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - sunX, dy = y - sunY;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / sunR;
                    if (d > 1f) continue;
                    // The classic cut: the disc is sliced by horizontal gaps that widen as
                    // they fall, so the sun reads as setting even with no sea under it.
                    int fromTop = Mathf.RoundToInt(sunY + sunR) - y;
                    int slice = Mathf.Max(2, 2 + fromTop / 8);
                    if (y < sunY && fromTop % (slice + 3) < 3) continue;
                    Put(x, y, (byte)(200 - Mathf.RoundToInt(d * 90f)));
                }

            // THE GRID, running to the horizon under it. Verticals converge on the sun's
            // foot; horizontals bunch as they go back. Whole units, no perspective maths
            // pretending to be a camera.
            int horizon = Mathf.RoundToInt(sunY - sunR * 0.15f);
            for (int i = -14; i <= 14; i++)
            {
                float spread = i * (W / 10f);
                for (int y = 0; y < horizon; y++)
                {
                    float k = (horizon - y) / (float)Mathf.Max(1, horizon);
                    int x = Mathf.RoundToInt(sunX + spread * k * k);
                    Put(x, y, 110);
                }
            }
            for (int step = 1; step <= 9; step++)
            {
                int y = horizon - Mathf.RoundToInt(horizon * (step * step) / 81f);
                for (int x = 0; x < W; x++) Put(x, y, 110);
            }

            // THE PALMS, one each side, standing off the edges so the aisle's own margins
            // are not fighting them. A trunk that leans and six fronds — the silhouette does
            // all the work at this size, so nothing inside it is drawn.
            void Palm(int baseX, int baseY, int height, int lean)
            {
                int topX = baseX, topY = baseY + height;
                for (int t = 0; t <= height; t++)
                {
                    float k = t / (float)height;
                    int x = baseX + Mathf.RoundToInt(lean * k * k);
                    if (t == height) topX = x;
                    Put(x, baseY + t, 150); Put(x + 1, baseY + t, 150);
                }
                for (int f = 0; f < 6; f++)
                {
                    float ang = Mathf.PI * (0.12f + f * 0.152f);
                    float ln = height * 0.42f;
                    for (int t = 0; t <= Mathf.RoundToInt(ln); t++)
                    {
                        float k = t / ln;
                        int x = topX + Mathf.RoundToInt(Mathf.Cos(ang) * ln * k);
                        // The frond droops: it leaves the crown flat and falls away at the tip.
                        int y = topY + Mathf.RoundToInt(Mathf.Sin(ang) * ln * k * 0.55f - ln * k * k * 0.75f);
                        Put(x, y, 150); Put(x, y - 1, 150);
                    }
                }
            }
            Palm(Mathf.RoundToInt(W * 0.11f), Mathf.RoundToInt(H * 0.05f),
                 Mathf.RoundToInt(H * 0.62f), Mathf.RoundToInt(-W * 0.03f));
            Palm(Mathf.RoundToInt(W * 0.90f), Mathf.RoundToInt(H * 0.02f),
                 Mathf.RoundToInt(H * 0.48f), Mathf.RoundToInt(W * 0.03f));

            return Cache[key] = Make(px, W, H, Vector4.zero);
        }

        // ── the order bubble over a customer's head (2026-08-19) ────────────────────
        //
        // WHY THIS IS DRAWN AND NOT GENERATED, after it was generated. PixelLab drew seven
        // takes and the author picked bub_card's look — a white field with a hot magenta
        // edge — and asked for it without the pixel faults. Held against the harvest, that
        // take could not be repaired: its "canonical plate" reads as a rounded blob with the
        // spout's diagonal bleeding into the bottom rows, and what looked clean on the proof
        // sheet was the 9-slice hiding all of it inside the corners. At this size the object
        // is geometry — a one-unit edge, a two-unit chamfer, a flat fill — and geometry that
        // has to be exact is drawn exact. The generated set stays in the staging folder as
        // what the look was chosen FROM.
        //
        // THE PLATE IS 9-SLICED AND THE TAIL IS NOT. Same split as BackBarArt's bottle card,
        // and for the same reason: the ticket's width is decided by its longest line and its
        // height by how many lines there are, and a spout inside a stretched band smears
        // along it. The tail is its own sprite, placed by code under the middle of whatever
        // width the plate ended up.

        /// <summary>Which state the balloon's edge is speaking (16 §5: light says state) —
        /// only the EDGE moves between tones, so the ticket is always the same object.
        /// Order is the resting magenta; Take is the information cyan of a drink built and
        /// claimable; Drink is the club's own blue (2026-08-19, the author: "içecek içiyorsa
        /// pembe rengi vice mavisi olsun") — this customer is mid-animation and cannot be
        /// talked to, and the ticket's dots say the same thing in the same colour.</summary>
        public enum BubbleTone { Order, Take, Drink }

        /// <summary>
        /// The bubble's fill: the palette's white, never #FFFFFF (14 v3 §3) — and SLIGHTLY
        /// SEE-THROUGH since 2026-08-25 (the author: "baloncukların beyaz kısmı biraz şeffaf
        /// bir beyaz olsun"). Only the fill: the edge, the foot and the spout's slopes stay
        /// solid, so the balloon still has a hard drawn outline and the room only shows
        /// through the paper inside it.
        ///
        /// 0xDB is 86%, which is "biraz" — far enough to see the wall move behind a ticket,
        /// nowhere near far enough to cost the 8px type its contrast.
        /// </summary>
        private static readonly Color32 BubbleFill = new Color32(0xF2, 0xE8, 0xD5, 0xDB);

        /// <summary>The same white, solid. The spout's skirt is drawn in this and nothing
        /// else is: those three rows exist to ERASE the plate's bottom band where the balloon
        /// should be open (see <see cref="BubbleTail"/>), and a see-through eraser erases
        /// nothing — a translucent skirt would show the plate's magenta edge straight through
        /// the mouth of the tail.</summary>
        private static readonly Color32 BubbleSolid = new Color32(0xF2, 0xE8, 0xD5, 0xFF);
        /// <summary>Its edge, and the two steps under it. Magenta[3] is the hot line the
        /// author picked; Magenta[1] is the shade that makes the card sit ON the room rather
        /// than being a hole cut in it, the same trick <see cref="Card"/> plays in grey.</summary>
        private static readonly Color32 BubbleEdge = new Color32(0xE8, 0x4D, 0xA6, 0xFF);
        private static readonly Color32 BubbleFoot = new Color32(0x8F, 0x24, 0x64, 0xFF);
        /// <summary>The edge when the drink is built and this customer can take it. Cyan is
        /// the information ramp and the ticket already lit cyan before it had a drawing
        /// (16 §5).</summary>
        private static readonly Color32 BubbleEdgeLit = new Color32(0x3B, 0xC8, 0xBE, 0xFF);
        private static readonly Color32 BubbleFootLit = new Color32(0x1B, 0x5F, 0x66, 0xFF);
        /// <summary>The edge while they drink: ClubBlue[3] and ClubBlue[1] — the "vice
        /// mavisi" the author asked for is the palette's own club blue, not a new hue.</summary>
        private static readonly Color32 BubbleEdgeDrink = new Color32(0x44, 0x67, 0xCC, 0xFF);
        private static readonly Color32 BubbleFootDrink = new Color32(0x1F, 0x2E, 0x66, 0xFF);

        private static Color32 EdgeOf(BubbleTone tone) =>
            tone == BubbleTone.Take ? BubbleEdgeLit
            : tone == BubbleTone.Drink ? BubbleEdgeDrink : BubbleEdge;
        private static Color32 FootOf(BubbleTone tone) =>
            tone == BubbleTone.Take ? BubbleFootLit
            : tone == BubbleTone.Drink ? BubbleFootDrink : BubbleFoot;

        /// <summary>How thick the coloured edge runs. 2 since 2026-08-19 (the author:
        /// "pembe şeriti kalınlaştırılsın"); the tail's slopes are drawn at the same
        /// weight, or the outline would change thickness where the spout leaves the plate.</summary>
        private const int BubbleEdgeW = 2;

        /// <summary>How deep the plate's border runs. The chamfer is 2 and the edge is 2, so
        /// 5 clears both (and the foot row under them) with room to spare — and 2x5 = 10
        /// means the bubble can be drawn as short as 10 units, which is shorter than any
        /// ticket will ever be.</summary>
        public const int BubbleBorder = 5;

        /// <summary>
        /// THE ORDER BUBBLE'S PLATE. 11x11, 9-sliced at 5, so every size it is ever drawn at
        /// is the four real corners with one-unit runs repeated between them.
        ///
        /// Chamfered by two rather than square or round: a square card on a dark room reads
        /// as a dialog box, and a rounded one needs a radius big enough to see, which is a
        /// border big enough to stop the bubble ever being short. Two units is the room's own
        /// chamfer — the market's plate and key both wear it.
        /// </summary>
        public static Sprite Bubble(BubbleTone tone = BubbleTone.Order)
        {
            string key = "bubble:plate:" + tone;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int S = 11, Cut = 2;
            var edge = EdgeOf(tone);
            var foot = FootOf(tone);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = Mathf.Min(x, S - 1 - x), cy = Mathf.Min(y, S - 1 - y);
                    if (cx + cy < Cut) { px[y * S + x] = new Color32(0, 0, 0, 0); continue; }
                    Color32 c;
                    // The chamfer IS the edge, at BubbleEdgeW deep on the diagonal and the
                    // straights alike.
                    if (cx + cy < Cut + BubbleEdgeW || cx < BubbleEdgeW || cy < BubbleEdgeW)
                        c = edge;
                    else if (y == BubbleEdgeW) c = foot;                  // ONE row of foot:
                    // two was drawn first and, on a white field, a two-unit dark band across
                    // the bottom stops reading as a shadow and starts reading as a second
                    // object. The card only needs to be told it stands on something.
                    else c = BubbleFill;
                    px[y * S + x] = c;
                }
            return Cache[key] = Make(px, S, S,
                new Vector4(BubbleBorder, BubbleBorder, BubbleBorder, BubbleBorder));
        }

        /// <summary>
        /// THE SPOUT, pointing down at the head it belongs to. 11 wide, 9 tall, drawn at the
        /// size it is used and never scaled.
        ///
        /// Its top THREE rows are plain fill with no edge on them. Placed overlapping the
        /// plate's bottom border by those three rows, they erase the plate's own bottom band
        /// (two rows of edge and the foot) exactly where the balloon should be open, and the
        /// two read as one shape instead of as a wedge stuck under a box. InfoTail does this
        /// upside down for the bottle card and the trick is the same one.
        ///
        /// The slopes fall one unit per row so every diagonal lands on a whole pixel — a
        /// slope drawn at any other rate is a staircase with two step heights in it, which is
        /// the single most visible way a pixel drawing goes wrong. They are BubbleEdgeW wide,
        /// the plate's own weight, or the outline would thin where the spout leaves it.
        /// </summary>
        public static Sprite BubbleTail(BubbleTone tone = BubbleTone.Order)
        {
            string key = "bubble:tail:" + tone;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            // H is not free: the cone falls one unit per row from a half-width of Mid to a
            // point, which is Mid + 1 rows, and the skirt sits on top of it. Anything taller
            // leaves an empty row under the tip and the point comes away from the spout.
            const int W = 11, Skirt = BubbleEdgeW + 1, Mid = W / 2, H = Skirt + Mid + 1;
            var edge = EdgeOf(tone);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
            for (int y = 0; y < H; y++)
            {
                // Texture rows count UP; row H-1 is the mouth and row 0 is the tip.
                int fromTop = H - 1 - y;
                bool skirt = fromTop < Skirt;
                int half = skirt ? Mid : Mid - (fromTop - Skirt);
                if (half < 0) continue;
                for (int x = Mid - half; x <= Mid + half; x++)
                {
                    bool slope = !skirt && (x < Mid - half + BubbleEdgeW
                                         || x > Mid + half - BubbleEdgeW);
                    px[y * W + x] = slope ? edge : skirt ? BubbleSolid : BubbleFill;
                }
            }
            // The tip is the row where the half-width reaches nought, and it is already the
            // edge colour: at half = 0 the one pixel is BOTH slopes at once, so the outline
            // closes round the spout without a special case.
            return Cache[key] = Make(px, W, H, Vector4.zero);
        }


        /// <summary>
        /// A PRICE TAG — the card of stock hung on a bottle's neck (2026-08-19, the author:
        /// "fiyatını gösteren yazıyı bir fiyat etiketi içerisine al").
        ///
        /// It is the market's answer to a rule this project keeps coming back to: chrome
        /// comes from the SUBJECT'S OWN WORLD (16 §6, the positive form). A price set as
        /// loose type on a card is a number floating on a page; a price on a tag is a thing
        /// a shop actually has, and it says "this is what it costs" before a digit is read.
        ///
        /// 9-sliced, because "$8" and "+$105" are the same object at two widths. The border
        /// is LOPSIDED on purpose — 11 on the left, 4 everywhere else — so the whole pointed
        /// end with its punch hole is inside the corner region and never stretches. Only the
        /// flat body between the point and the right edge grows.
        ///
        /// Drawn WHITE for the caller to tint, like every other mark here: the tag is Amber
        /// when the price can be paid (money is Amber, 16 §5) and a dead grey when it cannot,
        /// and one drawing serves both.
        /// </summary>
        public static Sprite PriceTag()
        {
            const string Key = "shop:pricetag";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int W = 22, H = 16, Point = 8;
            var px = new Color32[W * H];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // The point: the left `Point` columns close to a nib, two rows a column.
                    int top = H - 1, bot = 0;
                    if (x < Point)
                    {
                        int cut = (Point - x + 1) / 2;
                        top = H - 1 - cut; bot = cut;
                        if (top < bot) continue;
                    }
                    if (y > top || y < bot) continue;
                    // A pixel is the outline if it is on the SHAPE'S boundary — the sloping
                    // rows of the nib, the flat top and bottom of the body, and the two
                    // vertical faces. The first cut left x = 0 out of that on the rows
                    // between the slopes, so the nib's blunt tip had no edge on it and the
                    // tag read as a torn piece of paper.
                    bool edge = y == top || y == bot || x == 0 || x == W - 1;
                    px[y * W + x] = edge ? new Color32(255, 255, 255, 255)
                                         : new Color32(210, 210, 210, 255);
                }
            // THE PUNCH HOLE, where the string goes. It is the one detail that stops the
            // shape reading as an arrow, and it is why the left border has to be 11.
            int hx = 4, hy = H / 2;
            for (int y = hy - 1; y <= hy + 1; y++)
                for (int x = hx - 1; x <= hx + 1; x++)
                    if (System.Math.Abs(x - hx) + System.Math.Abs(y - hy) <= 1)
                        px[y * W + x] = clear;
            return Cache[Key] = Make(px, W, H, new Vector4(11, 4, 4, 4));
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

        /// <summary>
        /// THE INSTRUMENT PLATE: the night boards' face, and the benches' step card.
        ///
        /// It was a GENERATED drawing for two days and the author sent it back — "UI
        /// görselinin çerçevesinde pixel problemleri ve görsel problemler var" — and they
        /// were right, in a way that is worth writing down because it is the house rule
        /// arriving by the back door. The model drew a frame that was a DASHED magenta line
        /// down the left, a solid teal line down the right, and a mixture of the two along
        /// the foot: three different rails on one rectangle, none of them repeating, so
        /// nine-slicing it stretched noise. A frame is chrome, chrome is procedural (14 §3),
        /// and this is the third time this project has learned it.
        ///
        /// What survives from the drawing is its LOOK, which the author liked: a navy field,
        /// a teal capped head with a brass hairline under it, and four brass rivets. What
        /// changes is that every one of those is now placed on a grid — the rails match, the
        /// corners are square, and the middle is one flat colour, so it slices exactly.
        ///
        /// Drawn at 48×48 and sliced (6, 6, 6, 18): the cap and the rivet row live inside
        /// the top border, the foot rivets inside the bottom, and only flat field stretches.
        /// Stand it with pixelsPerUnitMultiplier 0.5 for the house's whole 2× grain.
        /// </summary>
        public static Sprite Instrument()
        {
            const string Key = "instrument";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            const int W = 48, H = 48, Cap = 14;
            var px = new Color32[W * H];
            Color32 field = UITheme.ClubBlue[0];
            Color32 rail = UITheme.Cyan[2];
            Color32 keyline = UITheme.Night[0];
            Color32 capFace = UITheme.Cyan[1];
            Color32 capLit = UITheme.Cyan[3];
            Color32 brass = UITheme.Amber[3];
            Color32 clear = new Color32(0, 0, 0, 0);

            void Set(int x, int ty, Color32 c)
            {
                if (x < 0 || x >= W || ty < 0 || ty >= H) return;
                px[(H - 1 - ty) * W + x] = c;               // ty counts DOWN from the top
            }

            for (int ty = 0; ty < H; ty++)
                for (int x = 0; x < W; x++)
                {
                    // A cut corner, so the plate reads as a machined panel and not a box.
                    if ((x == 0 || x == W - 1) && (ty == 0 || ty == H - 1)) { Set(x, ty, clear); continue; }
                    if (x == 0 || x == W - 1 || ty == 0 || ty == H - 1) { Set(x, ty, keyline); continue; }
                    if (x == 1 || x == W - 2 || ty == 1 || ty == H - 2) { Set(x, ty, rail); continue; }
                    if (ty < Cap) { Set(x, ty, ty == 2 ? capLit : capFace); continue; }
                    if (ty == Cap) { Set(x, ty, brass); continue; }          // the hairline
                    Set(x, ty, field);
                }

            // Four rivets, inset from the field's own corners. 3×3, because a 2×2 rivet at
            // this grain reads as a dead pixel and a 4×4 as a bolt.
            void Rivet(int x0, int y0)
            {
                for (int dy = 0; dy < 3; dy++)
                    for (int dx = 0; dx < 3; dx++)
                        Set(x0 + dx, y0 + dy, dx == 0 || dy == 2 ? (Color32)UITheme.Amber[1] : brass);
            }
            Rivet(4, Cap + 3);
            Rivet(W - 7, Cap + 3);
            Rivet(4, H - 8);
            Rivet(W - 7, H - 8);

            return Cache[Key] = Make(px, W, H, new Vector4(6f, 6f, 6f, 18f));
        }

        /// <summary>
        /// ONE SOLID TEXEL, white, for anything that has to be DRAWN rather than merely
        /// coloured in.
        ///
        /// It exists because of a trap paid for on 2026-08-25: an <see cref="Image"/> with
        /// no sprite ignores <see cref="Image.Type"/> entirely — Filled, Sliced, Tiled, all
        /// of it — and draws one flat quad. So the standing's gauge, built as a Filled image
        /// with no sprite, came back FULL on a bar standing at nought. A fill needs something
        /// to fill.
        /// </summary>
        public static Sprite Solid()
        {
            const string Key = "solid";
            if (Cache.TryGetValue(Key, out var got) && got != null) return got;
            return Cache[Key] = Make(new[] { new Color32(255, 255, 255, 255) }, 1, 1, Vector4.zero);
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
