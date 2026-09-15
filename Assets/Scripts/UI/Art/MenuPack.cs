using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE AUTHOR'S BUTTON PACK (2026-09-15, the author: "Butonlar içinde bu dosya yolundaki butonları kullan" —
    /// Desktop/konsept art/buton, brought in by Tools/menu_pack.py). Ten 80x96 sheets of thirty 16x16 icon buttons —
    /// grey, orange and green, each resting, lit (a brighter glyph) and pressed (the frame a pixel lower, its shadow
    /// a row thinner). The pause menu and the settings window are built from them, shown at exactly 2x:
    ///   · an ICON KEY is one cell, whole (<see cref="Cell"/>): the meters' - and +, the player's keys, the sound switch
    ///   · a WORDED KEY stands on a cell with its glyph painted out, 9-sliced at 4 (<see cref="Blank"/>), and carries
    ///     one of the pack's glyphs as a white mask at its left (<see cref="Glyph"/>), tinted in the pack's own inks
    /// The pack keeps its own palette — the one written exception to GDD 16's palette-token rule, on the author's
    /// word, the way the flags and the bottles keep theirs. Everything drawn AROUND a key stays in the palette.
    /// Sprites are cut at run time from the sheets and cached the way ChromeArt caches (a dead sprite is not a hit).
    /// </summary>
    public static class MenuPack
    {
        public enum Tone { Grey, Orange, Green }
        public enum State { Rest, Lit, Pressed }

        /// <summary>The pack's cells in sheet order, five a row — and, in a seventh row on the mask sheet only,
        /// the two the player needs that the pack lacks (the song before and after, Tools/menu_pack.py).</summary>
        private static readonly string[] Icons =
        {
            "exit", "tent", "play", "home", "stats",
            "back", "pause", "menu", "music", "gamepad",
            "expand", "sound_on", "sound_off", "cog", "save",
            "restart", "close", "trophy", "mail", "heart_line",
            "check", "trash", "chevron_down", "chevron_up", "heart",
            "plus", "minus", "lock", "unlock", "info",
            "prev", "next",
        };
        private const int CellPx = 16, Cols = 5, PackCells = 30;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Sheets = new Dictionary<string, Texture2D>();

        private static Texture2D Sheet(string name)
        {
            if (Sheets.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Menu/" + name);
            if (t != null) Sheets[name] = t; else Sheets.Remove(name);   // a miss is never cached
            return t;
        }

        /// <summary>Which sheet carries a tone in a state. Green has no lit-and-up sheet, so its lit state is its
        /// resting one (the hover lift still says "under the pointer").</summary>
        private static string SheetName(Tone tone, State state)
        {
            string t = tone.ToString().ToLowerInvariant();
            switch (state)
            {
                case State.Lit: return tone == Tone.Green ? t + "_normal" : t + "_light";
                case State.Pressed: return t + "_light_pressed";
                default: return t + "_normal";
            }
        }

        private static Sprite Cut(Texture2D tex, string key, Rect rect, Vector4 border)
        {
            var s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            return Cache[key] = s;
        }

        /// <summary>One icon button, whole — 16x16, to be shown at 2x. Null for a name the pack has no cell for.</summary>
        public static Sprite Cell(Tone tone, State state, string icon)
        {
            string key = "pack:" + tone + ":" + state + ":" + icon;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            int i = Array.IndexOf(Icons, icon);
            if (i < 0 || i >= PackCells) return null;
            var tex = Sheet("pack_" + SheetName(tone, state));
            if (tex == null) return null;
            int r = i / Cols, c = i % Cols;   // rows count from the top of the PNG; a texture's rows from its foot
            return Cut(tex, key, new Rect(c * CellPx, tex.height - (r + 1) * CellPx, CellPx, CellPx), Vector4.zero);
        }

        /// <summary>The plate a worded key stands on: a cell with no glyph, 9-sliced at 4 so its cut corners, its
        /// highlight and its shadow stay the pack's while the middle takes the word's width.</summary>
        public static Sprite Blank(Tone tone, bool pressed)
        {
            string key = "pack:blank:" + tone + (pressed ? ":down" : ":up");
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var tex = Sheet("pack_" + tone.ToString().ToLowerInvariant() + (pressed ? "_blank_pressed" : "_blank"));
            if (tex == null) return null;
            return Cut(tex, key, new Rect(0, 0, tex.width, tex.height), new Vector4(4, 4, 4, 4));
        }

        /// <summary>A glyph as a white mask (its second tone at half alpha), 16x16, for the caller to tint with
        /// <see cref="Ink"/>; null for an unknown name.</summary>
        public static Sprite Glyph(string icon)
        {
            string key = "pack:glyph:" + icon;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            int i = Array.IndexOf(Icons, icon);
            var tex = Sheet("pack_glyphs");
            if (i < 0 || tex == null) return null;
            int r = i / Cols, c = i % Cols;
            return Cut(tex, key, new Rect(c * CellPx, tex.height - (r + 1) * CellPx, CellPx, CellPx), Vector4.zero);
        }

        /// <summary>The face colour a tone's cells are filled with (Tools/menu_pack.py measured them).</summary>
        public static Color Face(Tone tone) =>
            tone == Tone.Orange ? new Color32(230, 69, 57, 255)
            : tone == Tone.Green ? new Color32(99, 171, 63, 255)
            : new Color32(104, 111, 153, 255);

        /// <summary>The glyph's ink on a tone: the pack's resting tone, or its lit one under the pointer.</summary>
        public static Color Ink(Tone tone, bool lit)
        {
            if (lit) return tone == Tone.Green ? new Color32(255, 238, 131, 255) : new Color32(245, 255, 232, 255);
            return tone == Tone.Grey ? new Color32(223, 224, 232, 255) : new Color32(255, 194, 161, 255);
        }

        /// <summary>The ink a WORD is printed in on a tone: the grey key's resting glyph tone, and on the orange and
        /// green keys their lit tone, which is the one that reads against those faces.</summary>
        public static Color Word(Tone tone) => tone == Tone.Grey ? Ink(tone, false) : Ink(tone, true);
    }
}
