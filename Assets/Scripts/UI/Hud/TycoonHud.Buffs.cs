using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Buffs: how a buff is SAID, wherever it is shown - a page's character on the menu and the
    // licence, a fitting on the upgrade ladder, a crate in the market (2026-09-23, the author: "Bufflar kısa ve
    // net bir şekilde küçük iconuyla markette ürünlerde gözükmeli aynı şey açık görünen tarifler içinde geçerli").
    //
    // SHARED FILE. The menu (TraitStrip), the market and the fittings all say a buff through the same four
    // things: its MARK, its FIGURE, its WORD, and the one PLATE they stand on (ChromeArt.BuffPlate), keyed by
    // one short stat word - DrinkTrait.StatKey for a page, the fitting's kind for a fitting. A second plate,
    // a second word table or a second icon table is how the menu and the market would start telling the
    // player two different things about one number.
    public sealed partial class TycoonHud
    {
        /// <summary>The fact row's height: a 16 glyph and its 2-unit ring above and below.</summary>
        private const float BuffRowH = 20f;

        /// <summary>A plate holding only the fact: a 2-unit rim, the row, and the 4-unit foot (rim + throw).</summary>
        private const float BuffFactPlateH = 26f;

        /// <summary>
        /// THE FIGURE, signed and per-cent, in this language's order ("+22%", Turkish "+%22"). The sign is the
        /// NUMBER's, never the news's: a buff that takes 20% off the lateness penalty prints -20% on a green plate.
        /// </summary>
        private static string Pct(int pct)
        {
            string key = pct >= 0 ? "buff.pct.up" : "buff.pct.down";
            int n = System.Math.Abs(pct);
            // A table that does not carry the figure yet prints it the English way, never a bracketed key.
            return UIText.TOr(key, null) != null ? UIText.T(key, ("pct", n)) : (pct >= 0 ? "+" : "-") + n + "%";
        }

        /// <summary>
        /// THE MARK for a buff, and whether it is a white mask the caller must tint. The house's three drawn
        /// symbols come back as themselves and untinted (GDD 16: one thing, one drawing; CLAUDE.md: a star or a
        /// heart is never tinted) - the coin for a tip, the medallion for COMFORT, the heart for SERVICE - and
        /// the rest are ChromeArt's buff_&lt;key&gt; masks or an existing mark for a thing that already had one.
        /// Never null for a known key; an unknown one gets the generic rising mark rather than a hole.
        /// </summary>
        private static Sprite BuffIcon(string key, out bool tinted) => BuffIcon(key, null, out tinted);

        /// <summary>
        /// The same, with the mark to fall back on for a key that has none of its own (a caller that knows a
        /// better one than the generic mark - a page's lever mark, say).
        /// </summary>
        private static Sprite BuffIcon(string key, Sprite fallback, out bool tinted)
        {
            tinted = true;
            Sprite art = null;
            switch (key ?? "")
            {
                // The house's own drawings, at their own 16 (the service heart is 12x12: draw it at 1x).
                case "tip":
                    art = ItemArt.Load("coin3d_16");
                    if (art != null) { tinted = false; return art; }
                    return ChromeArt.Mark("tips");
                case "comfort":
                    art = ItemArt.Medal(true, 16f);
                    if (art != null) { tinted = false; return art; }
                    return ChromeArt.Mark("room");
                case "service":
                    art = ItemArt.Heart(true, 16f);
                    if (art != null) { tinted = false; return art; }
                    return ChromeArt.Mark("good");

                // Things that already wore a mark somewhere in the house keep it, so a buff and the control it
                // speeds up look like one thing.
                case "round": art = ChromeArt.Mark("round"); break;
                case "room": art = ChromeArt.Mark("room"); break;
                case "pour_speed": case "kegs": art = ChromeArt.Mark("pour"); break;
                case "shake_speed": art = ChromeArt.Mark("step_shake"); break;
                case "free_drain": case "drains_free": art = ChromeArt.Mark("bin"); break;
                case "glass_rung": art = ChromeArt.Mark("step_glass"); break;
                // A seat is a stool; a better bottle's premium is a price on every drink poured from it.
                case "seats": art = ChromeArt.Mark("buff_stool"); break;
                case "stock_premium": case "premium": art = ChromeArt.Mark("buff_price") ?? ChromeArt.Mark("till"); break;
            }
            if (art == null && !string.IsNullOrEmpty(key)) art = ChromeArt.Mark("buff_" + key);
            if (art == null) art = fallback;
            // The last resort is the market's own rising mark: it says "this helps" without claiming a what.
            return art != null ? art : ChromeArt.Mark("rise");
        }

        /// <summary>
        /// THE WORD for what a buff moves, in capitals. ONE vocabulary: book.trait.stat.&lt;key&gt; - the page's
        /// characters wrote it first and a fitting's kind is keyed the same way - except the two house ratings,
        /// whose words the top bar already says in all twenty-nine tables (book.trait.stat.comfort/service exist
        /// for a caller that reads the table directly, but only in English and Turkish yet). A key nobody wrote a
        /// word for prints itself in capitals rather than a bracketed key.
        /// </summary>
        private static string BuffWord(string key)
        {
            switch (key ?? "")
            {
                case "": return "";
                case "comfort": return UIText.T("build.house.comfort");
                case "service": return UIText.T("build.house.service");
                case "drains_free": key = "free_drain"; break;
                case "premium": key = "stock_premium"; break;
            }
            return UIText.TOr("book.trait.stat." + key, UIText.Caps(key.Replace('_', ' ')));
        }

        /// <summary>
        /// THE FACT, IN ONE ROW (2026-09-23): the buff's mark, the figure, the word for what moves - left to
        /// right, each placed at the MEASURED end of the one before it, so nothing can run under anything else.
        ///
        /// The figure is the loudest thing on the plate (the author: "+%00 göze çarpıcı olmalı, beyaz gövde siyah
        /// çerçeve olabilir"): the house's figure face at 16, in the palette's white, ringed in Night[0] one
        /// font-pixel out on all eight sides (PixelOutline - Unity's Outline stamps only the four diagonals and
        /// chews the stems). The word stands beside it in the plate's brightest step, and steps back to 8 rather
        /// than run past the rim. An empty figure (a flag such as FREE DRAIN) leaves the mark and the word.
        /// </summary>
        /// <param name="right">The x the row must stop at; infinity sizes the word to itself.</param>
        /// <param name="ramp">The plate's ramp (Lime, ViceRed or Cream): the word is its [4].</param>
        /// <param name="rowH">The row's height; the mark and the type are centred in it.</param>
        /// <returns>The x the row ended at.</returns>
        private float BuffFactRow(RectTransform plate, float x, float top, float right, string key, string figure,
            Color[] ramp, bool withWord = true, float rowH = BuffRowH)
        {
            var art = BuffIcon(key, out bool tinted);
            if (art != null)
            {
                // At its own size when that is 16 or less - the service heart is drawn 12x12 - so the mark is
                // always whole pixels, never 1.33x.
                float mw = Mathf.Min(16f, art.rect.width), mh = Mathf.Min(16f, art.rect.height);
                var m = NewRect("Mark", plate);
                Place(m, new Vector2(0, 1), new Vector2(mw, mh),
                    new Vector2(x + Mathf.Floor((16f - mw) * 0.5f), -(top + Mathf.Floor((rowH - mh) * 0.5f))));
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = art;
                mi.preserveAspect = true;
                mi.raycastTarget = false;
                mi.color = tinted ? UITheme.Cream[4] : Color.white;
                x += 16f + 6f;
            }

            if (!string.IsNullOrEmpty(figure))
            {
                var fig = NewText("Figure", plate, _figures, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                fig.horizontalOverflow = HorizontalWrapMode.Overflow;
                fig.raycastTarget = false;
                fig.text = figure;
                var ring = fig.gameObject.AddComponent<PixelOutline>();
                ring.EffectColor = UITheme.Night[0];
                ring.Distance = 2f;          // one font pixel: the figure face is an 8-px grid set at 16
                float fw = Mathf.Ceil(fig.preferredWidth);
                // The box starts two in, so the ring's left edge lands on x and its right edge on x + fw + 4.
                Place(fig.rectTransform, new Vector2(0, 1), new Vector2(fw + 2f, rowH), new Vector2(x + 2f, -top));
                x += fw + 4f;
                if (withWord) x += 8f;
            }
            if (!withWord) return x;

            var word = NewText("Stat", plate, _shop, 16, TextAnchor.MiddleLeft, ramp[4]);
            word.horizontalOverflow = HorizontalWrapMode.Overflow;
            word.raycastTarget = false;
            word.text = BuffWord(key);
            bool open = float.IsInfinity(right);
            // A badge is a smaller thing than a page's strip: under a 20-unit row the word is set at 8 from the
            // start, so the figure stays the loudest thing on it.
            if (rowH < BuffRowH) word.fontSize = LanguageFonts.Size(_shop, 8);
            float avail = open ? Mathf.Ceil(word.preferredWidth) : Mathf.Max(0f, right - x);
            if (word.preferredWidth > avail) word.fontSize = LanguageFonts.Size(_shop, 8);
            if (word.preferredWidth > avail)
            {
                // Never met in English or Turkish at 232 or 296 (measured 2026-09-23); a longer language gets
                // two short lines inside the row rather than a word under the rim.
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
            }
            Place(word.rectTransform, new Vector2(0, 1), new Vector2(avail, rowH), new Vector2(x, -top));
            return x + Mathf.Min(Mathf.Ceil(word.preferredWidth), avail);
        }

        /// <summary>
        /// A BUFF, SAID SHORT (2026-09-23, the author: "Bufflar kısa ve net bir şekilde küçük iconuyla markette
        /// ürünlerde gözükmeli"): the buff's mark, its figure as the loudest thing, and - unless
        /// <paramref name="withWord"/> is false - the word, on THE plate (<see cref="ChromeArt.BuffPlate"/>),
        /// sized to what it holds. Lime for a buff, ViceRed for a nerf, Cream for a buff that is offered or owned
        /// but not working tonight (<paramref name="dim"/> wins over <paramref name="nerf"/>); a caller that wants
        /// it further back puts a CanvasGroup on what this returns.
        ///
        /// <paramref name="figure"/> comes formatted (<see cref="Pct"/>, "+1", "+$2") or empty for a flag. At the
        /// default 22 the plate's inside is exactly one 16 mark tall and the figure's ring lies on the rim; pass
        /// <see cref="BuffFactPlateH"/> (26) for the page's own breathing room. <see cref="Place"/> sets the
        /// anchor as the pivot, so <paramref name="pos"/> is measured from that corner of the host. Nothing on it
        /// takes a raycast, and nothing on it is named "Name" (the PlayMode suite finds market tiles by a child
        /// Text of that name).
        /// </summary>
        private RectTransform BuffBadge(RectTransform host, string key, string figure, bool nerf, Vector2 anchor,
            Vector2 pos, bool withWord = true, bool dim = false, float h = 22f)
        {
            var ramp = dim ? UITheme.Cream : nerf ? UITheme.ViceRed : UITheme.Lime;
            var badge = NewRect("Buff", host);
            var bg = badge.gameObject.AddComponent<Image>();
            bg.sprite = ChromeArt.BuffPlate(ramp);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 0.5f;      // the house's 2x: a 2-unit frame, 4-unit corners, a 4-unit foot
            bg.raycastTarget = false;

            // The inside runs from under the 2-unit rim to over the 4-unit foot.
            const float Pad = 5f;
            float rowH = Mathf.Max(12f, h - 6f);
            float end = BuffFactRow(badge, Pad, 2f, float.PositiveInfinity, key, figure, ramp, withWord, rowH);
            Place(badge, anchor, new Vector2(Mathf.Ceil(end) + Pad, h), pos);
            return badge;
        }
    }
}
