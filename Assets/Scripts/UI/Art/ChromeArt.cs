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
        };

        /// <summary>One 16x16 mark, white, for the caller to tint. Null for a name that has
        /// no mask — a missing mark leaves a gap, it does not throw.</summary>
        public static Sprite Mark(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = "mark:" + name;
            if (Cache.TryGetValue(key, out var got)) return got;
            if (!Masks.TryGetValue(name, out var mask)) return Cache[key] = null;

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
            if (Cache.TryGetValue(Key, out var got)) return got;
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
            if (Cache.TryGetValue(key, out var got)) return got;
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
