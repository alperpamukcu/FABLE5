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

        /// <summary>
        /// THE HAND (2026-09-22, the author's eighth list: the fake licence "her şeyi el ile kalemle çizmiş şekilde
        /// fontlar el yazısı gibi"). Indie Flower (OFL, Assets/Fonts/OFL-IndieFlower.txt), imported as a hinted raster
        /// so its strokes land on whole pixels like everything else the card prints, for every language it draws -
        /// measured with fontTools against every table: all the Latin ones, Vietnamese and Turkish included. Cyrillic,
        /// Greek and the CJK tables are not in it, and those write in their own body face, which the card jitters.
        /// </summary>
        public static Font Hand(Font fallback)
        {
            switch (Localization.Current.Code)
            {
                case "bg": case "el": case "ja": case "ko": case "ru": case "uk": case "zh-CN": case "zh-TW":
                    return fallback;
                default:
                    return Face("IndieFlower-Regular") ?? fallback;
            }
        }

        /// <summary>Whether <paramref name="font"/> is the hand itself (and so needs no jitter to look written).</summary>
        public static bool IsHand(Font font) => font != null && Loaded.TryGetValue("IndieFlower-Regular", out var hand) && hand == font;

        /// <summary>
        /// THE RECIPE'S TITLE FACE (2026-09-23, the author: "Menüde alkollerin adının yazdığı üst başlığın fontunu
        /// değiş okunaklı değil"). Jersey 15 (OFL, Assets/Fonts/OFL-Jersey15.txt) - the lowercase-and-Turkish pixel
        /// face the crowd spoke for a day in September - at its own grid: 1350 units to the em in steps of 50, so
        /// <see cref="TitlePx"/> 27 is one design pixel to a unit (measured then: 0.1% soft pixels). The heading
        /// face it replaces on the page is an 8x8 monospace cell doubled, every letter the same width and the same
        /// block; this one is proportional, condensed and heavy, so a name reads as a word. It is also narrower:
        /// LONG ISLAND ICED TEA is 204 units here and 320 in the heading face, which ran past its 316 box.
        ///
        /// Latin only. Measured with fontTools against every recipe name in every table: it draws all the Latin
        /// tables except Vietnamese, and no Cyrillic, Greek or CJK - those keep <paramref name="fallback"/>.
        /// </summary>
        public static Font Title(Font fallback)
        {
            switch (Localization.Current.Code)
            {
                case "bg": case "el": case "ja": case "ko": case "ru": case "uk": case "vi": case "zh-CN": case "zh-TW":
                    return fallback;
                default:
                    return Face("Jersey15-Regular") ?? fallback;
            }
        }

        /// <summary>The title face's own size: one of its design pixels to a unit.</summary>
        public const int TitlePx = 27;

        /// <summary>Whether <paramref name="font"/> is the title face (UiAudit lets it stand at its own 27).</summary>
        public static bool IsTitle(Font font) =>
            font != null && Loaded.TryGetValue("Jersey15-Regular", out var title) && title == font;

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

        /// <summary>
        /// THE SMALL LINE ON A PRINTED SLIP (2026-09-22, the author's eighth list: the Turkish receipt "çok büyük
        /// duruyor ... biraz küçült"). A 7 px face has two clean sizes, 8 and 16, and neither is the house face's 16:
        /// its 16 stands taller than Silkscreen's and its 8 reads as specks beside it. So a language on the 7 px face
        /// takes its sibling Galmuri9 at that face's own 10 for the slip's small lines - the same family and letters,
        /// the height the English slip prints. Every other face gives its one-cell size.
        /// </summary>
        public static (Font font, int size) SmallLine(Font body)
        {
            if (body != null && Grids.TryGetValue(body, out int grid) && grid <= 8)
            {
                var nine = Face("Galmuri9");
                if (nine != null) return (nine, 10);
            }
            return (body, Size(body, 8));
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
