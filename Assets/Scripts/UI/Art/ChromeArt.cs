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
            // A FINE (GDD 28 §7): the law's own shield, barred across.
            ["fine"] = new[]
            {
                "................",
                "..############..",
                "..############..",
                "..##........##..",
                "..##.######.##..",
                "..##.######.##..",
                "..##........##..",
                "..############..",
                "..############..",
                "...##......##...",
                "....##....##....",
                ".....##..##.....",
                "......####......",
                ".......##.......",
                "................",
                "................",
            },
            // THE STATE'S THANKS: a rosette with its two ribbons.
            ["thanks"] = new[]
            {
                "................",
                ".....######.....",
                "....########....",
                "...##########...",
                "...###....###...",
                "...###....###...",
                "...##########...",
                "....########....",
                ".....######.....",
                ".....##..##.....",
                "....##....##....",
                "....##....##....",
                "...##......##...",
                "...##......##...",
                "................",
                "................",
            },
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
            // CASH — the dollar itself, DRAWN (2026-09-04, the author: "faturada $ iconu ...
            // kullanılmalı"). Every figure on the night's slip was carrying a typed $ out of
            // the body face, which at 24 is one more narrow glyph in a row of narrow glyphs:
            // the eye had to read the number to find out it was money. This is the same 16px
            // stroke the marks beside it are drawn in, so a figure now OPENS with a mark the
            // way the labels do — and the money is the thing seen first, which is a receipt's
            // whole job. Two units thick everywhere, stem through the S, because a one-unit
            // dollar at this size is the smudge the generated set was thrown out for.
            // It is drawn 14 ROWS TALL inside its 16, not the 10 the label marks opposite it
            // use: this one stands beside TYPE and has to match a cap, where they stand
            // beside a word and have to match a line. Measured against both faces — the
            // slip's regular at 24 and the heavy at 16 — whose capitals are about 14 units.
            ["cash"] = new[]
            {
                "................",
                ".......##.......",
                "....########....",
                "...##..##..##...",
                "...##..##.......",
                "...##..##.......",
                "...##..##.......",
                "....######......",
                "......######....",
                ".......##..##...",
                ".......##..##...",
                ".......##..##...",
                "...##..##..##...",
                "....########....",
                ".......##.......",
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
            // A WASTE BIN, for the key that throws a drink away. Lid, handle, body and two
            // staves — the least that still reads as a bin at 16 px, and the same
            // silhouette everyone already has in their head for "discard".
            ["bin"] = new[]
            {
                "................",
                "......####......",
                "......####......",
                "..############..",
                "..############..",
                "................",
                ".###.######.###.",
                ".###.######.###.",
                ".###.######.###.",
                ".###.######.###.",
                ".###.######.###.",
                ".###.######.###.",
                ".###.######.###.",
                ".##############.",
                ".##############.",
                "................",
            },
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

        // ── the reaction faces (2026-09-04) ─────────────────────────────────────────
        //
        // THE THREE FACES A DRINK EARNS (the author: "kötü, fena değil, güzel/mükemmel
        // için 3 adet emoji/icon"). What a customer gives back once they have tasted it,
        // thrown up behind them a few at a time by ReactionMotes.
        //
        // Fourteen by fourteen and drawn at ONE STAGE UNIT PER PIXEL, never scaled off it:
        // at this size half a pixel of drift shuts an eye. The disc is shared on purpose —
        // the three read as one family, only the MOUTH moves, and the caller's tint says
        // which way it went.
        //
        // INKED, NOT PUNCHED (measured in play, 2026-09-04). The first cut was a white
        // silhouette with the eyes and mouth as HOLES, like the marks above: over the
        // sunset wall a sour face tinted ViceRed lost its features to the wall showing
        // through them and its edge to a wall of the same value. A face carries its own
        // ink now — a ring around it and dark features inside — so it reads on any wall
        // the room throws behind it, which is the same reason the till's figures are
        // ringed twice.

        private static readonly Color32 FaceInk = new Color32(0x0D, 0x08, 0x13, 0xFF);

        /// <summary>Ink cells per mood, (col, row) from the TOP-LEFT of the 14x14 face:
        /// two eyes shared by all three, then the mouth that tells them apart.</summary>
        private static readonly Dictionary<string, (int X, int Y)[]> FaceMouths =
            new Dictionary<string, (int X, int Y)[]>
        {
            // BAD — the corners drop.
            ["bad"] = new[] { (5, 8), (6, 8), (7, 8), (8, 8), (4, 9), (9, 9) },
            // FAIR — a flat line: it was a drink, and it was fine.
            ["fair"] = new[] { (4, 9), (5, 9), (6, 9), (7, 9), (8, 9), (9, 9) },
            // GOOD — the same mouth, the other way up.
            ["good"] = new[] { (4, 8), (9, 8), (5, 9), (6, 9), (7, 9), (8, 9) },
        };

        private static readonly (int X, int Y)[] FaceEyes =
            { (4, 5), (4, 6), (9, 5), (9, 6) };

        /// <summary>One 14x14 reaction face: a white disc for the caller to tint, ringed
        /// and featured in ink. Null for a mood with no mouth.</summary>
        public static Sprite Face(string mood)
        {
            if (string.IsNullOrEmpty(mood)) return null;
            string key = "face:" + mood;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            if (!FaceMouths.TryGetValue(mood, out var mouth)) return null;

            const int S = 14;
            const float C = 6.5f, Body = 5.7f, Ring = 6.7f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - C, dy = y - C;
                    float d2 = dx * dx + dy * dy;
                    px[y * S + x] = d2 <= Body * Body
                        ? new Color32(255, 255, 255, 255)
                        : d2 <= Ring * Ring ? FaceInk : new Color32(255, 255, 255, 0);
                }
            // The features are authored top-down, the way they read in source; the texture
            // counts up, so row S-1-y is the row the eye is on.
            void Ink(int x, int y)
            {
                if (x >= 0 && x < S && y >= 0 && y < S) px[(S - 1 - y) * S + x] = FaceInk;
            }
            foreach (var e in FaceEyes) Ink(e.X, e.Y);
            foreach (var m in mouth) Ink(m.X, m.Y);
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

        // ── the standing gauge (2026-09-04) ─────────────────────────────────────────
        //
        // The author: "Sahnedeki doluluk barı değişmeli." What stood on both benches was a
        // 44×300 rectangle of near-black with a one-pixel neon hairline round it and flat
        // blocks of colour stacked inside — a bar chart, and the only bar chart in the game.
        // The horizontal work meter had already been through this and came out of it by
        // using the house's own instrument (GaugeTube + Solid + GaugeGlass); the standing
        // column never was.
        //
        // So it is the same instrument, stood up: a steel body with a dark bore, a brass
        // ring at its foot and the Instrument plate's teal cap at its head. It is drawn 1:1
        // — 44 sprite pixels in a 44-unit rect — because that is the grain the work meter
        // sits at and the two are read on the same screen.
        //
        // Two sprites rather than one, exactly as the horizontal gauge does it: the BODY is
        // opaque and draws behind the drink, the GLASS is transparent and draws over it, so
        // the measures stay scratched into the tube whether the tube is empty or full.

        /// <summary>Where the tube's bore starts and stops, in sprite rows from each end.
        /// The bench insets its liquid by these, so a level of 1 fills to the collar.</summary>
        public const int GaugeHead = 13, GaugeFoot = 9;

        /// <summary>The empty standing gauge: steel body, dark bore, brass foot, teal cap.</summary>
        public static Sprite GaugeColumn(int w, int h)
        {
            string key = $"gauge:col:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            void Set(int x, int ty, Color32 c)
            {
                if (x < 0 || x >= w || ty < 0 || ty >= h) return;
                px[(h - 1 - ty) * w + x] = c;                   // ty counts DOWN from the top
            }

            Color32 keyline = UITheme.Night[0];
            Color32 lit = UITheme.Graphite[3];                  // light comes from the left
            Color32 shade = UITheme.Graphite[1];
            Color32 bore = UITheme.Night[0];
            for (int ty = 0; ty < h; ty++)
                for (int x = 0; x < w; x++)
                {
                    if ((x == 0 || x == w - 1) && (ty == 0 || ty == h - 1)) { Set(x, ty, clear); continue; }
                    if (x == 0 || x == w - 1 || ty == 0 || ty == h - 1) { Set(x, ty, keyline); continue; }
                    if (x == 1) { Set(x, ty, lit); continue; }
                    if (x == w - 2) { Set(x, ty, shade); continue; }
                    Set(x, ty, bore);
                }

            // The head: the cap, then the brass hairline under it. The bench writes what
            // the column measures across this in the pixel face.
            for (int ty = 1; ty < GaugeHead - 2; ty++)
                for (int x = 2; x < w - 2; x++)
                    Set(x, ty, ty == 2 ? (Color32)UITheme.Cyan[3] : (Color32)UITheme.Cyan[1]);
            for (int x = 2; x < w - 2; x++)
            {
                Set(x, GaugeHead - 2, UITheme.Amber[3]);
                Set(x, GaugeHead - 1, UITheme.Amber[1]);
            }

            // The foot: the ring the tube stands on, and the plinth under it.
            int floor = h - GaugeFoot;
            for (int x = 2; x < w - 2; x++)
            {
                Set(x, floor, UITheme.Amber[3]);
                Set(x, floor + 1, UITheme.Amber[1]);
            }
            for (int ty = floor + 2; ty < h - 1; ty++)
                for (int x = 2; x < w - 2; x++)
                    Set(x, ty, ty == floor + 2 ? (Color32)UITheme.Graphite[2] : (Color32)UITheme.Graphite[1]);

            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        /// <summary>What draws OVER the drink: a measure scratched at every tenth, a long
        /// one at the half, and the light down the near wall.</summary>
        public static Sprite GaugeColumnGlass(int w, int h)
        {
            string key = $"gauge:colglass:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[w * h];
            void Set(int x, int ty, Color32 c)
            {
                if (x < 0 || x >= w || ty < 0 || ty >= h) return;
                px[(h - 1 - ty) * w + x] = c;
            }
            int top = GaugeHead, bot = h - GaugeFoot, inner = bot - top;
            for (int ty = top; ty < bot; ty++)
            {
                Set(3, ty, new Color32(242, 232, 213, 26));     // the shine, two in
                Set(4, ty, new Color32(242, 232, 213, 14));
                Set(w - 4, ty, new Color32(13, 8, 19, 70));     // and the far wall's shadow
            }
            for (int s = 1; s < 10; s++)
            {
                int ty = bot - Mathf.RoundToInt(inner * s / 10f);
                bool half = s == 5;
                int run = half ? w - 5 : 7;
                for (int x = 2; x < 2 + run; x++)
                    Set(x, ty, half ? new Color32(242, 232, 213, 76) : new Color32(201, 188, 168, 52));
                if (!half) for (int x = w - 5; x < w - 2; x++) Set(x, ty, new Color32(201, 188, 168, 52));
            }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        // ── the fill gauge, as the shaker itself (2026-09-04) ───────────────────────
        //
        // The standing instrument above was the first answer and it was sent back too:
        // "Doluluk barı için küçük bir shaker görseli içerisinde doluluk barı olacak,
        // doluluk barı için yaratılan shaker görselinde sadece shakerin dış hatları
        // olacak içerisi boş olacak."
        //
        // Which is the better idea, and for the reason the whole game is built on: the
        // reading takes the SHAPE OF THE THING IT READS, so nobody has to be told what
        // the column measures. A cylinder with a brass foot is an instrument you must
        // learn; a shaker with drink in it is not.
        //
        // The outline is neither drawn by hand nor generated — it is TRACED off the
        // shaker the game already uses, so the gauge and the prop on the bench are the
        // same object: same shoulders, same collar, same taper. If that art is ever
        // replaced, its gauge changes with it and no one has to remember to redraw this.

        /// <summary>Half of the fill gauge: the shaker in outline, one pixel wide and
        /// hollow. Draw <see cref="ShakerGaugeCavity"/>'s contents behind it.</summary>
        public static Sprite ShakerOutline(int w, int h)
        {
            string key = $"shakergauge:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var span = ShakerSpans(w, h);
            var px = new Color32[w * h];
            Color32 ink = UITheme.Cream[2];
            for (int y = 0; y < h; y++)
            {
                int a = span[y].x, b = span[y].y;
                int pa = y > 0 ? span[y - 1].x : a, pb = y > 0 ? span[y - 1].y : b;
                int na = y < h - 1 ? span[y + 1].x : a, nb = y < h - 1 ? span[y + 1].y : b;
                for (int x = a; x <= b; x++)
                    if (x == a || x == b || y == 0 || y == h - 1
                        || x < pa || x > pb || x < na || x > nb)
                        px[(h - 1 - y) * w + x] = ink;
            }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        /// <summary>The same silhouette FILLED — the stencil the contents are cut to, so
        /// a band can never hang over the tin's shoulder. White, because a Mask reads
        /// alpha and nothing else.</summary>
        public static Sprite ShakerSolid(int w, int h)
        {
            string key = $"shakersolid:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var span = ShakerSpans(w, h);
            var px = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < h; y++)
                for (int x = span[y].x; x <= span[y].y; x++)
                    px[(h - 1 - y) * w + x] = white;
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        /// <summary>Where the drink lives inside that outline, as fractions of the
        /// sprite's height from the TOP. Not the whole silhouette — a drink does not fill
        /// the cap; these are the tin's own collar and floor.</summary>
        public static readonly Vector2 ShakerGaugeCavity = new Vector2(0.335f, 0.985f);

        /// <summary>The silhouette's left and right edge per row, at any size. Measured
        /// off <c>Items/shaker.png</c> once and cached; falls back to a plain column if
        /// the art is missing or unreadable, so a gauge always draws.</summary>
        private static Vector2Int[] ShakerSpans(int w, int h)
        {
            string key = $"shakerspans:{w}x{h}";
            if (SpanCache.TryGetValue(key, out var hit)) return hit;
            var rows = ShakerRows();
            var outp = new Vector2Int[h];
            for (int y = 0; y < h; y++)
            {
                if (rows == null || rows.Length == 0) { outp[y] = new Vector2Int(0, w - 1); continue; }
                var r = rows[Mathf.Min(rows.Length - 1, y * rows.Length / h)];
                int a = Mathf.RoundToInt(r.x * (w - 1) / (float)Mathf.Max(1, SpanWidth - 1));
                int b = Mathf.RoundToInt(r.y * (w - 1) / (float)Mathf.Max(1, SpanWidth - 1));
                outp[y] = new Vector2Int(Mathf.Clamp(a, 0, w - 1), Mathf.Clamp(b, 0, w - 1));
            }
            return SpanCache[key] = outp;
        }

        private static readonly Dictionary<string, Vector2Int[]> SpanCache =
            new Dictionary<string, Vector2Int[]>();
        private static Vector2Int[] _shakerRows;
        private static int SpanWidth = 1;

        private static Vector2Int[] ShakerRows()
        {
            if (_shakerRows != null) return _shakerRows;
            var sprite = ItemArt.Load("shaker");
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable)
                return _shakerRows = new Vector2Int[0];
            var tex = sprite.texture;
            var pixels = tex.GetPixels32();
            int tw = tex.width, th = tex.height;
            var found = new List<Vector2Int>();
            int minX = int.MaxValue, maxX = int.MinValue;
            for (int ty = th - 1; ty >= 0; ty--)          // top row first: y-down like the sprite reads
            {
                int a = int.MaxValue, b = int.MinValue;
                for (int x = 0; x < tw; x++)
                    if (pixels[ty * tw + x].a >= 128) { if (x < a) a = x; if (x > b) b = x; }
                if (a > b) { if (found.Count > 0) break; else continue; }
                found.Add(new Vector2Int(a, b));
                if (a < minX) minX = a;
                if (b > maxX) maxX = b;
            }
            if (found.Count == 0) return _shakerRows = new Vector2Int[0];
            SpanWidth = maxX - minX + 1;
            for (int i = 0; i < found.Count; i++)
                found[i] = new Vector2Int(found[i].x - minX, found[i].y - minX);
            return _shakerRows = found.ToArray();
        }

        // ── the bench's counter grain (2026-09-04) ──────────────────────────────────
        //
        // The bench top was one flat #1F1924 with a sheen band across it, which is what a
        // ZOOM of a flat sprite honestly is, and it read as one.
        //
        // MARBLE WAS THE FIRST ANSWER AND IT WAS SENT BACK: "Mermer hissiyatını
        // beğenmedim başka bir desen kullanalım. Gerçek sahnedeki tezgahın renginde
        // sadece yakından daha detaylı gözükebilecek göz yormayacak bir desen gerekiyor."
        // The lesson is worth keeping, because it is not about marble: veining is made of
        // BIG SHAPES, and a big shape on a surface reads from across the room whether it
        // is wanted or not. What a working counter wants is the opposite — an even grain
        // that is one flat colour at a glance and only becomes detail when leaned into.
        //
        // So every pattern here lives inside a one-step contrast budget around the slab's
        // own colour, and none of them draws a shape bigger than a few pixels. They are
        // hashed rather than dithered, because a regular dither at 4× is a visible dot
        // screen. <see cref="CounterGrain"/> is the default; the rest stay switchable
        // because "quieter"/"busier" is a taste call the author makes by eye, not a
        // rewrite.
        //
        // Procedural, because chrome is (14 §3) and because a surface this big cannot be a
        // shipped picture without being stretched: the band's height changes by 121 units
        // between a bench opened over the cellar and one that is not, and a stretched pixel
        // surface stops having pixels. Drawn once, tiled, at a quarter of its pixels per
        // unit so one art pixel lands as four — the same 4× the bench's own zoom stands at.

        /// <summary>
        /// A RECESS CUT INTO THE COUNTER — the step card's ground.
        ///
        /// The card wore the invoice boards' navy plate, and the author asked for the
        /// opposite: "masa arkaplanına gömülü hissi". A plate and a recess are drawn the
        /// same way round in every respect but one, and that one is everything — a raised
        /// thing is lit along its TOP and shaded along its bottom, and a sunk thing is
        /// shaded along its top and lit along its bottom, because the light is coming from
        /// the same place either way. So this is the counter's own colour with the
        /// shading inverted, and the eye reads "hole" without being told.
        ///
        /// Sliced (6,6,6,6) with a flat middle, so it stretches to any card.
        /// </summary>
        public static Sprite Inlay(int w, int h)
        {
            string key = $"inlay:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[w * h];
            Color32 floor = Hex(0x1A141F);      // a shade under the slab: it is a hole
            Color32 dark = Hex(0x120D16);       // the cut face the light misses
            Color32 lip = Hex(0x2E2739);        // ...and the one it climbs back out over
            Color32 ink = UITheme.Night[0];

            for (int ty = 0; ty < h; ty++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c;
                    if ((x == 0 || x == w - 1) && (ty == 0 || ty == h - 1)) c = ink;
                    else if (ty <= 1 || x <= 1) c = ty == 0 || x == 0 ? ink : dark;
                    else if (ty >= h - 2 || x >= w - 2) c = ty == h - 1 || x == w - 1 ? ink : lip;
                    else c = floor;
                    px[(h - 1 - ty) * w + x] = c;
                }
            return Cache[key] = Make(px, w, h, new Vector4(6f, 6f, 6f, 6f));
        }

        /// <summary>
        /// THE BAR NAPKIN the spoon rests on (2026-09-04, the author: "kaşık ise bir
        /// peçetenin üstünde durmalı").
        ///
        /// A tool lying straight on the counter reads as dropped; on a folded napkin it
        /// reads as SET DOWN, which is the difference between a bench and a mess. It is
        /// also the quietest possible way to give the spoon a place of its own — no rail,
        /// no holder, nothing else to draw.
        ///
        /// Paper, so: a soft cream square turned a few degrees off square, one fold line,
        /// and a deliberately RAGGED edge — a perfectly straight paper edge at this grain
        /// looks like a tile. The ragged step is hashed off the pixel's own position, so
        /// it is the same napkin every time the bench opens.
        /// </summary>
        public static Sprite Napkin(int w, int h)
        {
            string key = $"napkin:{w}x{h}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var px = new Color32[w * h];
            // PAPER, and paper is pale. Cream[1]/[2] came out as a grey blob on the
            // counter — it read as a stain rather than a napkin. Two steps up the ramp
            // puts it where a bar napkin actually sits against dark stone.
            Color32 face = UITheme.Cream[3];
            Color32 lit = UITheme.Cream[4];
            Color32 fold = UITheme.Cream[2];
            Color32 edge = UITheme.Cream[1];       // its own thickness, where it meets the counter

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // A rounded-off square with a hashed edge: inset grows at the corners.
                    float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h - 0.5f;
                    float d = Mathf.Abs(u) * 1.02f + Mathf.Abs(v) * 0.92f;
                    float ragged = 0.455f + Hash(x / 2, y / 2, 61) * 0.020f;
                    if (d > ragged) continue;
                    Color32 c = face;
                    if (d > ragged - 0.020f) c = edge;             // the paper's own shadow
                    else if (v < -0.16f) c = lit;                  // the light catches the top
                    else if (Mathf.Abs(v - 0.06f) < 0.012f) c = fold;   // one fold
                    px[(h - 1 - y) * w + x] = c;
                }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        /// <summary>Which grain the counter wears. SLATE by default: it is the one whose
        /// detail is ARCHITECTURE — the top is laid in panels, with a hairline seam where
        /// they meet — so leaning in finds structure rather than more noise.</summary>
        public enum CounterGrain { Slate, Speck, Brushed, Weave, Terrazzo }

        /// <summary>The counter's surface. Tile it; never stretch it.</summary>
        public static Sprite Counter(int w, int h, CounterGrain grain = CounterGrain.Slate)
        {
            string key = $"counter:{w}x{h}:{grain}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;

            Color32 slab = Hex(0x1F1924);        // BenchSlab, sampled off counter.png
            Color32 up1 = Hex(0x231D29);         // one step up — the whole contrast budget
            Color32 up2 = Hex(0x27212E);
            Color32 dn1 = Hex(0x1B1520);
            Color32 dn2 = Hex(0x18121C);

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float g = Hash(x, y, 29);
                    Color32 c = slab;
                    switch (grain)
                    {
                        case CounterGrain.Slate:
                            // Panels, and a hairline where two of them meet.
                            int tx = x % 40, ty = y % 30;
                            if (ty == 0) c = Hash(x, y, 41) > 0.25f ? dn2 : dn1;
                            else if (ty == 1) c = Hash(x, y, 41) > 0.55f ? up1 : slab;
                            else if (tx == 0) c = Hash(x, y, 41) > 0.25f ? dn2 : dn1;
                            else if (Hash(x, y, 41) > 0.95f) c = up1;
                            break;
                        case CounterGrain.Speck:
                            // Polished stone chip: fine grit, no direction at all.
                            if (g > 0.965f) c = up2;
                            else if (g > 0.90f) c = up1;
                            else if (g < 0.055f) c = dn1;
                            break;
                        case CounterGrain.Brushed:
                            // Wiped down a million times: long faint scratches, no shapes.
                            float row = Hash(0, y, 3);
                            float streak = Noise(u, v, 96, 24, 11);
                            if (row > 0.86f && streak > 0.58f && Hash(x, y, 17) > 0.35f) c = up1;
                            else if (row < 0.16f && streak < 0.42f && Hash(x, y, 17) > 0.35f) c = dn1;
                            break;
                        case CounterGrain.Weave:
                            if ((x + y) % 4 == 0 && Hash(x, y, 53) > 0.55f) c = up1;
                            else if ((x - y) % 4 == 0 && Hash(x, y, 53) > 0.75f) c = dn1;
                            break;
                        case CounterGrain.Terrazzo:
                            float cell = Noise(u, v, 80, 60, 71);
                            float fine = Hash(x, y, 73);
                            if (cell > 0.80f && fine > 0.30f) c = cell > 0.90f ? up2 : up1;
                            else if (cell < 0.19f && fine > 0.30f) c = dn1;
                            break;
                    }
                    px[(h - 1 - y) * w + x] = c;
                }
            }
            return Cache[key] = Make(px, w, h, Vector4.zero);
        }

        // ── the bin's push-button (2026-09-04) ──────────────────────────────────────
        //
        // The author, with a picture of a glossy green arcade button: "Çöp kutusu için 3
        // boyutlu basılınca içeri göçen yuvarlak buton istiyorum. Örnekteki gibi renkleri
        // farklı, animasyonlu."
        //
        // So the bin stops being a picture of a bin and becomes the CONTROL it always was
        // — which is also what its own code always claimed it to be ("you click the
        // object, not a button plate around it", AddBinButton). The whole illusion lives
        // in the difference between two plates: UP stands <see cref="ButtonThrow"/> pixels
        // proud of its socket and casts its own shadow on the socket floor; DOWN is flush,
        // the shadow gone and the gloss shrunk and slid down the dome. A cap that only
        // changed colour on press would read as a light coming on, not as a key moving.

        /// <summary>How far the cap stands out of its socket, in sprite pixels.</summary>
        public const int ButtonThrow = 4;

        /// <summary>The border a <see cref="KeyCap"/> is sliced on, so one drawing serves
        /// any width the label needs. Only flat field stretches.</summary>
        public static readonly Vector4 KeyCapBorder = new Vector4(8f, 14f, 8f, 10f);

        /// <summary>
        /// THE SAME PRESS, AS A WIDE KEY (2026-09-04, take 2). The round dome was drawn
        /// first and sent back: "boyut olarak daha büyük ve dikdörtgen bir buton olsun
        /// '(çöp ikonu) Çöp' yazsın üstünde." A 64px disc is small for the one control
        /// that throws a drink away, and a disc has nowhere to put a word.
        ///
        /// What survives is the part that worked — a cap standing proud of a socket and
        /// dropping into it, with its own cast shadow going as it lands. That travel is
        /// the whole illusion, so it is spent generously: six pixels on a 52-tall key,
        /// not the one pixel a subtle version would use.
        ///
        /// Nine-sliced (<see cref="KeyCapBorder"/>), because "ÇÖP" and a longer word are
        /// the same object at two widths — the house rule this project keeps relearning.
        /// The mark and the word are drawn by the caller ON the cap, so they can move
        /// with it and stay live text rather than baked pixels.
        /// </summary>
        public static Sprite KeyCap(Color[] ramp, bool down, string id)
        {
            string key = $"keycap:{id}:{(down ? "dn" : "up")}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int W = 64, H = 52, Throw = 6;
            var px = new Color32[W * H];
            Color32 ink = UITheme.Night[0];

            void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                        px[(H - 1 - y) * W + x] = c;                 // y counts DOWN
            }

            Rect(0, 0, W - 1, H - 1, ink);                            // the socket
            Rect(1, 1, W - 2, H - 2, UITheme.Graphite[1]);
            Rect(2, 2, W - 3, H - 3, UITheme.Graphite[0]);
            foreach (int x in new[] { 0, W - 1 })                     // squared corners
                foreach (int y in new[] { 0, H - 1 })
                    px[(H - 1 - y) * W + x] = new Color32(0, 0, 0, 0);

            int lift = down ? 0 : Throw;
            int cy0 = 4 + (Throw - lift), cy1 = H - 6 - lift;
            Rect(3, cy0 - 1, W - 4, cy1 + 1, ramp[0]);                // the cap's dark edge
            Rect(3, cy0, W - 4, cy1, ramp[2]);                        // its face
            Rect(4, cy1 - 2, W - 5, cy1, ramp[1]);                    // shaded at the foot
            Rect(4, cy0, W - 5, cy0 + 1, ramp[3]);                    // lit along the top
            if (!down)
            {
                Rect(3, cy1 + 2, W - 4, H - 4, UITheme.Graphite[0]);  // the well below it
                Rect(3, cy1 + 2, W - 4, cy1 + 3, ink);                // and its cast shadow
            }
            return Cache[key] = Make(px, W, H, KeyCapBorder);
        }

        /// <summary>How far the cap's face sits below the key's top edge, up and pressed —
        /// the caller moves the label by the difference so the writing travels with the
        /// cap instead of floating over it.</summary>
        public const int KeyCapFaceUp = 4, KeyCapFaceDown = 10;

        /// <summary>One arcade push-button, 32×32, in a named ramp — up or pressed.</summary>
        public static Sprite PushButton(Color[] ramp, bool down, string id)
        {
            string key = $"push:{id}:{(down ? "dn" : "up")}";
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            const int S = 32;
            var px = new Color32[S * S];
            Color32 ink = UITheme.Night[0];

            void Disc(float ccx, float ccy, float r, Color32 c)
            {
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int hits = 0;
                        for (int s = 0; s < 4; s++)
                        {
                            float sx = x + ((s & 1) == 0 ? 0.25f : 0.75f);
                            float sy = y + ((s & 2) == 0 ? 0.25f : 0.75f);
                            if ((sx - ccx) * (sx - ccx) + (sy - ccy) * (sy - ccy) <= r * r) hits++;
                        }
                        if (hits >= 2) px[(S - 1 - y) * S + x] = c;
                    }
            }

            float cx = S / 2f, cy = S / 2f, rim = S / 2f - 1f;
            Disc(cx, cy, rim, ink);                                  // the socket
            Disc(cx, cy, rim - 1f, UITheme.Graphite[1]);
            Disc(cx, cy + 0.6f, rim - 2f, UITheme.Graphite[0]);

            int lift = down ? 0 : ButtonThrow;
            float ccyy = cy - lift + 1f, crad = rim - 3f;
            if (!down) Disc(cx, ccyy + lift + 1f, crad, ink);        // the cap's own shadow
            Disc(cx, ccyy, crad, ramp[1]);
            Disc(cx, ccyy - 0.5f, crad - 1f, ramp[2]);
            Disc(cx, ccyy - 1f, crad - 2f, ramp[3]);
            float gloss = down ? crad * 0.26f : crad * 0.42f;
            Disc(cx - crad * 0.30f, ccyy - crad * 0.34f + (down ? 1.5f : 0f), gloss, ramp[4]);

            // The bin, moulded into the cap in its own deepest step — a lid with a handle,
            // a body and one stave, which is the least that still reads at this size.
            Color32 mark = ramp[0];
            void Set(int x, int y)
            {
                if (x >= 0 && x < S && y >= 0 && y < S) px[(S - 1 - y) * S + x] = mark;
            }
            int mx = (int)cx, y0 = (int)ccyy - 4;
            for (int x = -4; x <= 4; x++) Set(mx + x, y0 + 1);
            for (int x = -1; x <= 1; x++) Set(mx + x, y0);
            for (int y = y0 + 3; y < y0 + 9; y++) { Set(mx - 3, y); Set(mx, y); Set(mx + 3, y); }
            for (int x = -3; x <= 3; x++) Set(mx + x, y0 + 8);

            return Cache[key] = Make(px, S, S, Vector4.zero);
        }

        private static Color32 Hex(int v) =>
            new Color32((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255), 255);

        /// <summary>An integer hash in 0..1. Deterministic across platforms — no
        /// System.Random, no float seeds (CLAUDE.md).</summary>
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Value noise on a WRAPPED lattice, so the tile joins itself on all four
        /// sides and can be repeated without a seam.</summary>
        private static float Noise(float u, float v, int gw, int gh, int seed)
        {
            float fx = u * gw, fy = v * gh;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
            x0 = ((x0 % gw) + gw) % gw;
            y0 = ((y0 % gh) + gh) % gh;
            int x1 = (x0 + 1) % gw, y1 = (y0 + 1) % gh;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
            float c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            float top = a + (b - a) * tx;
            float bottom = c + (d - c) * tx;
            return top + (bottom - top) * ty;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

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
