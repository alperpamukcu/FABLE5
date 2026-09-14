using System;
using System.Collections.Generic;
using LastCall.Game;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The pixel faces a language is drawn in (2026-09-14, localization L3; the author approved the
    /// recommended set on the specimen page).
    ///
    /// The house faces — Silkscreen for body text, Malibu Arcade for headings — carry Latin-1, so
    /// English and the other Latin-1 languages keep them untouched and nothing here changes a pixel
    /// of theirs (the look baselines are English). Where a language needs letters those faces do not
    /// draw, a face that does takes over:
    ///
    ///   tr pl cs hu ro   body  Galmuri7   (8 px grid, like Silkscreen)      headings stay Malibu Arcade
    ///   ru uk bg         body  Galmuri9   (10 px: there is no 8 px Cyrillic body face)
    ///   el               body  Galmuri11  (12 px: Galmuri9 lacks the accented capitals Ά Έ Ή Ί Ό)
    ///   vi ko            both  Galmuri7   (Malibu Arcade has no Vietnamese or Hangul)
    ///   ja zh-CN zh-TW   both  Fusion Pixel 12 (8 px kanji and hanzi do not read)
    ///
    /// A pixel face only rasterises cleanly at whole multiples of its own grid (CLAUDE.md), so
    /// <see cref="Size"/> turns the UI's 8/16/24 into that face's multiples: 8 stays 8 on an 8 px
    /// grid, and becomes 10 or 12 on the larger grids; 16 and above become two cells of the grid.
    /// Coverage was measured with fontTools against every character of every table.
    /// </summary>
    public static class LanguageFonts
    {
        private const string Folder = "Fonts/";

        private static readonly Dictionary<string, Font> Loaded = new Dictionary<string, Font>(StringComparer.Ordinal);
        private static readonly Dictionary<Font, int> Grids = new Dictionary<Font, int>();

        /// <summary>The body face for the language being spoken, or <paramref name="house"/> when the
        /// house face already draws it.</summary>
        public static Font Body(Font house) => Face(Faces(Localization.Current.Code).body) ?? house;

        /// <summary>The heading face for the language being spoken, or <paramref name="house"/>.</summary>
        public static Font Display(Font house) => Face(Faces(Localization.Current.Code).display) ?? house;

        /// <summary>The size to draw <paramref name="font"/> at for a UI size of 8, 16 or 24. The
        /// house faces, and any face on an 8 px grid, keep the size they were given.</summary>
        public static int Size(Font font, int size)
        {
            if (font == null || !Grids.TryGetValue(font, out int grid) || grid <= 8) return size;
            // A 12 px face draws the UI's 8 AND 16 at 12 (measured in play 2026-09-14: at 24 its line
            // is 32 tall — the face carries a third of an em of ascent and descent — and a 16 row set
            // at 24 covered the note under it and the crowd line beside it). 24 and up stay 24.
            if (grid >= 12) return size >= 24 ? 2 * grid : grid;
            int cells = Mathf.Clamp(Mathf.RoundToInt(size / 8f), 1, 2);
            return cells * grid;
        }

        private static (string body, string display) Faces(string code)
        {
            switch (code)
            {
                case "tr": case "pl": case "cs": case "hu": case "ro":
                    return ("Galmuri7", null);
                case "ru": case "uk": case "bg":
                    return ("Galmuri9", null);
                case "el":
                    return ("Galmuri11", null);
                case "vi": case "ko":
                    return ("Galmuri7", "Galmuri7");
                case "ja":
                    return ("FusionPixel12-ja", "FusionPixel12-ja");
                case "zh-CN":
                    return ("FusionPixel12-zh_hans", "FusionPixel12-zh_hans");
                case "zh-TW":
                    return ("FusionPixel12-zh_hant", "FusionPixel12-zh_hant");
                default:
                    return (null, null);
            }
        }

        private static int GridOf(string resource) =>
            resource.StartsWith("FusionPixel12", StringComparison.Ordinal) || resource == "Galmuri11" ? 12
            : resource == "Galmuri9" ? 10
            : 8;

        private static Font Face(string resource)
        {
            if (resource == null) return null;
            if (Loaded.TryGetValue(resource, out var font) && font != null) return font;
            font = Resources.Load<Font>(Folder + resource);
            if (font == null)
            {
                Debug.LogError($"LanguageFonts: Resources/{Folder}{resource} is missing; the house face draws instead.");
                return null;
            }
            Loaded[resource] = font;
            Grids[font] = GridOf(resource);
            return font;
        }
    }
}
