using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE MENUS' NEON MARKS (2026-09-29, the author: "Ana menüde kullanılan iconları ... istersen sende üretebilirsin
    /// ama şuanki üretim kalitesiz şuanki altın iconları kullanma"). The door's, the pause's and the settings' keys
    /// wear small signs bent from the SAME tube as the curtain's night sign (ChromeArt.NeonRing): one texel of glass,
    /// a rim either side of it, and two FLAT bands of its light - never a smooth ramp. Each mark is authored below as
    /// its line of glass on a 15x15 grid; the four rings are grown from it by square steps, so the sprite is 21x21 and
    /// is shown at exactly 2x (42 units), and the glass is the same two-unit stroke as the body face's letters beside
    /// it. The rings are WHITE masks and the state is tint alone, the way the curtain's sign runs:
    ///   DARK  unlit glass (Night[3] rim, Night[2] glass) - a key that cannot be pressed
    ///   HALF  the hue's [2] round its [3], no light - a key at rest (the critic's pick: the door stopped being nine
    ///         lit signs competing with the logo, and the strike under the pointer is the "hareketli iconlar" the
    ///         author accepted on 09-27)
    ///   LIT   the hue's [3] round a Cream[4] glass, its [2] light at .30 (and .12 one step further, off a key only)
    ///   INK   an unlit tube lying on a light plate (the pack's amber or lime key): the glass in the word's own ink,
    ///         its rim the plate's ramp[1]. THE ONLY AMBER MARK IS THIS ONE - an amber-lit tube on a dark plate read
    ///         as the brass the author threw out (the critic, 2026-09-29), so there is no amber hue.
    /// Drawn in code, no files: the rows below are the whole of the art (scratchpad icons/skeletons.py drew them;
    /// CONTROLS is a key cap, not the maker's gamepad - the game is played with the mouse and the page lists caps).
    /// </summary>
    public static class NeonIcons
    {
        public const int Grid = 15;
        public const int Pad = ChromeArt.NeonPad;   // 3: the rim and the two bands round the glass
        public const int Size = Grid + 2 * Pad;     // 21 texels, shown at 2x = 42 units

        public enum State { Dark, Half, Lit }

        private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            // a floppy: the cut corner, the shutter, the label
            ["continue"] = new[]
            {
                ".##########....",
                "#...#....#.#...",
                "#...#....#..#..",
                "#...#....#...#.",
                "#...######....#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#...#######...#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#.......#..#",
                ".#############.",
            },
            // a martini glass with its olive (without the olive it read as a funnel)
            ["new_run"] = new[]
            {
                ".#############.",
                "..#.........#..",
                "...#...#...#...",
                "....#.....#....",
                ".....#...#.....",
                "......#.#......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                "....#######....",
            },
            // a cog: eight square teeth round a hub, the axle's bead
            ["settings"] = new[]
            {
                "......###......",
                "..#...#.#...#..",
                ".#.#.##.##.#.#.",
                "..#.........#..",
                "..#.........#..",
                ".##.........##.",
                "#.............#",
                "#......#......#",
                "#.............#",
                ".##.........##.",
                "..#.........#..",
                "..#.........#..",
                ".#.#.##.##.#.#.",
                "..#...#.#...#..",
                "......###......",
            },
            // power: the open ring and its bar
            ["quit"] = new[]
            {
                ".......#.......",
                ".......#.......",
                ".......#.......",
                "...#...#...#...",
                "..#....#....#..",
                "..#....#....#..",
                ".#.....#.....#.",
                ".#.....#.....#.",
                ".#.....#.....#.",
                ".#...........#.",
                ".#...........#.",
                "..#.........#..",
                "..#.........#..",
                "...##.....##...",
                ".....#####.....",
            },
            // a speaker: its box, its cone, two waves
            ["audio"] = new[]
            {
                "...............",
                "......#.....#..",
                ".....##......#.",
                "....#.#.......#",
                "####..#..#....#",
                "#..#..#...#...#",
                "#..#..#...#...#",
                "#..#..#...#...#",
                "#..#..#...#...#",
                "#..#..#...#...#",
                "####..#..#....#",
                "....#.#.......#",
                ".....##......#.",
                "......#.....#..",
                "...............",
            },
            // a globe: the rim, a meridian, the equator
            ["language"] = new[]
            {
                ".....#####.....",
                "...##.#.#.##...",
                "..#..#...#..#..",
                ".#..#.....#..#.",
                ".#..#.....#..#.",
                "#...#.....#...#",
                "#...#.....#...#",
                "###############",
                "#...#.....#...#",
                "#...#.....#...#",
                ".#..#.....#..#.",
                ".#..#.....#..#.",
                "..#..#...#..#..",
                "...##.#.#.##...",
                ".....#####.....",
            },
            // a cup: handles, stem, plinth
            ["achievements"] = new[]
            {
                "...#########...",
                "...#.......#...",
                "####.......####",
                "#..#.......#..#",
                "#..#.......#..#",
                ".#..#.....#..#.",
                "..##.#...#.##..",
                "......#.#......",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                "....#######....",
                "....#.....#....",
                "....#######....",
                "...............",
            },
            // a page: its turned corner, two lines
            ["credits"] = new[]
            {
                "..#######......",
                ".#.......#.....",
                ".#.......##....",
                ".#.......#.#...",
                ".#.......####..",
                ".#..........#..",
                ".#...#####..#..",
                ".#..........#..",
                ".#..........#..",
                ".#...#####..#..",
                ".#..........#..",
                ".#..........#..",
                ".#..........#..",
                ".#..........#..",
                "..##########...",
            },
            // a shopping cart (never the store's own mark)
            ["store"] = new[]
            {
                "...............",
                "...............",
                "###............",
                "...#...........",
                "...############",
                "....#.........#",
                "....#........#.",
                ".....#.......#.",
                ".....#......#..",
                "......#######..",
                "...............",
                "...............",
                "...............",
                ".......#....#..",
                "...............",
            },
            // an arrow turning back
            ["back"] = new[]
            {
                "....#..........",
                "...#...........",
                "..#............",
                ".#.............",
                "###########....",
                ".#.........#...",
                "..#.........#..",
                "...#.........#.",
                "....#........#.",
                ".............#.",
                ".............#.",
                "............#..",
                "....#######....",
                "...............",
                "...............",
            },
            // A KEY CAP (2026-09-29, the critic: "CONTROLS is a gamepad, but the page lists keyboard caps, and the
            // game is played with the mouse"): its top face, the bevels down to the thicker front lip.
            ["controls"] = new[]
            {
                "...............",
                "..###########..",
                ".#...........#.",
                "#..#########..#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#.......#..#",
                "#..#########..#",
                "#.#.........#.#",
                "##...........##",
                "#.............#",
                ".#...........#.",
                "..###########..",
                "...............",
            },
            // a screen on its stand
            ["display"] = new[]
            {
                ".#############.",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                ".#############.",
                ".......#.......",
                ".......#.......",
                ".......#.......",
                "....#######....",
                "...............",
            },
            // play
            ["resume"] = new[]
            {
                "...............",
                "....#..........",
                "....##.........",
                "....#.#........",
                "....#..#.......",
                "....#...#......",
                "....#....#.....",
                "....#.....#....",
                "....#....#.....",
                "....#...#......",
                "....#..#.......",
                "....#.#........",
                "....##.........",
                "....#..........",
                "...............",
            },
            // a house with its door
            ["main_menu"] = new[]
            {
                ".......#.......",
                "......#.#......",
                ".....#...#.....",
                "....#.....#....",
                "...#.......#...",
                "..#.........#..",
                ".#...........#.",
                "#.#.........#.#",
                "..#.........#..",
                "..#.........#..",
                "..#...###...#..",
                "..#...#.#...#..",
                "..#...#.#...#..",
                "..#...#.#...#..",
                "..###########..",
            },
            // a ring running clockwise, its head at the top
            ["reset"] = new[]
            {
                ".......#.......",
                "........#......",
                ".....#####.....",
                "...##...#......",
                "..#....#.......",
                "..#............",
                ".#...........#.",
                ".#...........#.",
                ".#...........#.",
                ".#...........#.",
                ".#...........#.",
                "..#.........#..",
                "..#.........#..",
                "...##.....##...",
                ".....#####.....",
            },
            // a tick
            ["apply"] = new[]
            {
                "...............",
                "...............",
                "...............",
                ".............#.",
                "............#..",
                "...........#...",
                "..........#....",
                ".#.......#.....",
                "..#.....#......",
                "...#...#.......",
                "....#.#........",
                ".....#.........",
                "...............",
                "...............",
                "...............",
            },
            // the door's pointer: an arrow at the key it points to
            ["pointer"] = new[]
            {
                "...............",
                "...............",
                "........#......",
                ".........#.....",
                "..........#....",
                "...........#...",
                "............#..",
                "#############..",
                "............#..",
                "...........#...",
                "..........#....",
                ".........#.....",
                "........#......",
                "...............",
                "...............",
            },
        };

        // ── THE SMALL MARKS (2026-09-29, the settings rebuilt on the kit - BUILD_SPEC §2 "SmallKey") ────────────────
        // The marks on the settings' 34-unit keys - a meter's - and +, the player's transport, a cycle's arrows, a
        // row key's chevron: 9x9 lines of glass by the same rules (one texel of glass, 1:1 diagonals), grown by the
        // same ring into a 15-texel picture shown at 2x = 30 units, so it sits two in from every side of a 34 key. A
        // key shows its rim and the first band only (NeonTube.Show without the outer glow): the second band would
        // cross the key's own rim.

        public const int SmallGrid = 9;
        public const int SmallSize = SmallGrid + 2 * Pad;   // 15 texels, 30 units

        private static readonly Dictionary<string, string[]> SmallLines = BuildSmallLines();

        private static Dictionary<string, string[]> BuildSmallLines()
        {
            var lines = new Dictionary<string, string[]>
            {
                ["minus"] = new[] { ".........", ".........", ".........", ".........", ".#######.", ".........", ".........", ".........", "........." },
                ["plus"] = new[] { ".........", "....#....", "....#....", "....#....", ".#######.", "....#....", "....#....", "....#....", "........." },
                ["left"] = new[] { "......#..", ".....#...", "....#....", "...#.....", "..#......", "...#.....", "....#....", ".....#...", "......#.." },
                ["prev"] = new[] { "#.......#", "#......#.", "#.....#..", "#....#...", "#...#....", "#....#...", "#.....#..", "#......#.", "#.......#" },
                ["hold"] = new[] { ".........", "..#...#..", "..#...#..", "..#...#..", "..#...#..", "..#...#..", "..#...#..", "..#...#..", "........." },
                // the hold's other face, while the record is held: a play mark
                ["play"] = new[] { "..#......", "..##.....", "..#.#....", "..#..#...", "..#...#..", "..#..#...", "..#.#....", "..##.....", "..#......" },
                ["down"] = new[] { ".........", ".........", "#.......#", ".#.....#.", "..#...#..", "...#.#...", "....#....", ".........", "........." },
            };
            lines["right"] = Mirror(lines["left"]);
            lines["next"] = Mirror(lines["prev"]);
            return lines;
        }

        private static string[] Mirror(string[] rows)
        {
            var o = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var c = rows[i].ToCharArray();
                System.Array.Reverse(c);
                o[i] = new string(c);
            }
            return o;
        }

        public static bool HasSmall(string role) => role != null && SmallLines.ContainsKey(role);

        /// <summary>One ring of a small mark as a white mask, 15x15 (the glass, or one to three square steps out).</summary>
        public static Sprite SmallMask(string role, ChromeArt.NeonLayer ring)
        {
            if (!HasSmall(role)) return null;
            string key = "small:" + role + ":" + ring;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            string[] rows = SmallLines[role];
            var line = new bool[SmallSize * SmallSize];
            for (int gy = 0; gy < SmallGrid; gy++)
                for (int gx = 0; gx < SmallGrid; gx++)
                    if (rows[gy][gx] == '#') line[(gy + Pad) * SmallSize + gx + Pad] = true;
            var sprite = ChromeArt.NeonRing(line, SmallSize, SmallSize, ring);
            sprite.name = "neon_small_" + role + "_" + ring;
            return Cache[key] = sprite;
        }

        /// <summary>A small mark's four stacked images, 30x30 units, no raycast.</summary>
        public sealed class SmallView : NeonTube
        {
            public SmallView(RectTransform parent, string name, string role, Vector2 anchor, Vector2 pos)
                : base(parent, name, ring => SmallMask(role, ring), new Vector2(SmallSize * 2, SmallSize * 2), anchor, pos)
            {
            }
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static bool Has(string role) => role != null && Lines.ContainsKey(role);

        /// <summary>One ring of a mark as a white mask, 21x21: the glass itself, or the texels one, two or three
        /// square steps out from it - grown by the curtain's own tube (ChromeArt.NeonRing). Null for a role with no
        /// drawing.</summary>
        public static Sprite Mask(string role, ChromeArt.NeonLayer ring)
        {
            if (!Has(role)) return null;
            string key = role + ":" + ring;
            // a destroyed sprite is not a cache hit (the static cache outlives a play; domain reload is off)
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            string[] rows = Lines[role];
            var line = new bool[Size * Size];                 // top-down, as NeonRing reads it
            for (int gy = 0; gy < Grid; gy++)
                for (int gx = 0; gx < Grid; gx++)
                    if (rows[gy][gx] == '#') line[(gy + Pad) * Size + gx + Pad] = true;
            var sprite = ChromeArt.NeonRing(line, Size, Size, ring);
            sprite.name = "neon_" + role + "_" + ring;
            return Cache[key] = sprite;
        }

        /// <summary>A mark's four stacked images under a parent, 42x42 units, no raycast: the menus' tube
        /// (NeonTube) bent into the mark, so a key's sign and the cabinet's frame run one set of states.</summary>
        public sealed class View : NeonTube
        {
            public View(RectTransform parent, string name, string role, Vector2 anchor, Vector2 pos)
                : base(parent, name, ring => Mask(role, ring), new Vector2(Size * 2, Size * 2), anchor, pos)
            {
            }
        }
    }
}
