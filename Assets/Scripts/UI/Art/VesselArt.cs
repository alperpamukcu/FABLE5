using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// What a vessel's art actually IS, measured off its own pixels: where the drawing sits on
    /// the sheet it was saved on, how big it really is, and — the reason this exists — WHERE
    /// ITS MOUTH IS.
    ///
    /// Both pour stages used to take those on faith. A bottle was drawn into a fixed rect with
    /// preserveAspect and the stream was emitted from <c>BottleH * 0.78</c>, which is the top
    /// centre of that rect — right only for art that fills its sheet edge to edge. The v3
    /// masters happen to; the juice cartons the author drew on 2026-08-11 do not. They are
    /// 96×168 sheets with the carton standing small inside them (the orange one is 46×88 of it,
    /// a quarter of the sheet), so the carton came out a third shorter than the bottle beside
    /// it, floated above the table it was standing on, and poured from a point in mid-air some
    /// 80px above its own cap. Four cartons of the same juice-carton size drew at four
    /// different sizes, decided by nothing but how much air each sheet was saved with.
    ///
    /// So nothing is assumed here. The drawing's box and its spout are read from the alpha:
    /// the MOUTH is the middle of the widest run of pixels in the TOP row of the silhouette.
    /// That is the cap on every bottle in the set and the spout on every carton — the one place
    /// a vessel is open at the top is the place liquid leaves it — and it costs one pass over a
    /// small sprite, once, cached.
    ///
    /// Two laws come out of it, and both scenes obey the same ones:
    ///  • A vessel STANDS at the height its scene offers, measured on the DRAWING, feet on the
    ///    line and middle over the mark. Sheet padding stops meaning anything.
    ///  • A vessel wider than <see cref="MaxAspect"/> is fitted by its WIDTH instead, so a
    ///    squat carton drawn as tall as a spirit bottle does not stand there like a wardrobe.
    ///
    /// The tin, the cap and the serving glass are deliberately NOT put through this. Their
    /// sheets are aligned to each other on purpose — <c>tin_open</c> carries 73px of air above
    /// the tin so the separately drawn <c>shaker_cap</c> lands exactly on its neck — and
    /// re-standing them by their drawings would take that alignment apart to fix nothing.
    /// </summary>
    public static class VesselArt
    {
        /// <summary>
        /// The widest a vessel may be drawn, as a share of the height its scene offered. Past
        /// this the drawing is fitted by width instead. The v3 spirit bottles run 0.24–0.46, so
        /// only the squattest of them feels it at all; the juice cartons (0.45–0.52) all come
        /// down to the same box and stand as four cartons rather than at four sizes.
        /// </summary>
        public const float MaxAspect = 0.44f;

        /// <summary>A pixel counts as drawing above this alpha. The art is point-filtered and
        /// hard-edged, so this only skips fringe.</summary>
        private const byte Ink = 16;

        /// <summary>The measurement of one sprite. Sheet-pixel coordinates throughout, y-UP
        /// from the sheet's floor — Unity's own texture space, so nothing is flipped twice.</summary>
        public readonly struct Metrics
        {
            /// <summary>False when the art could not be measured: no sprite, an unreadable
            /// texture, or a sheet with nothing on it. The fallback then treats the whole sheet
            /// as the drawing and its top centre as the mouth — which is exactly what the pour
            /// stages assumed before any of this existed, so a miss costs the old behaviour and
            /// never a crash.</summary>
            public readonly bool Measured;

            /// <summary>The saved canvas, in sprite pixels.</summary>
            public readonly Vector2 Sheet;

            /// <summary>The opaque box on it — the vessel as drawn.</summary>
            public readonly Rect Drawing;

            /// <summary>The cap / spout: where liquid leaves this vessel.</summary>
            public readonly Vector2 Mouth;

            public Metrics(bool measured, Vector2 sheet, Rect drawing, Vector2 mouth)
            {
                Measured = measured; Sheet = sheet; Drawing = drawing; Mouth = mouth;
            }

            /// <summary>Width over height OF THE DRAWING (never of the sheet).</summary>
            public float Aspect => Drawing.height > 0f ? Drawing.width / Drawing.height : 0.5f;

            /// <summary>How tall this vessel stands, as a share of the height its scene offers:
            /// 1 for anything slim, less for anything wide enough to hit <see cref="MaxAspect"/>.</summary>
            public float Stature => Mathf.Min(1f, MaxAspect / Mathf.Max(0.05f, Aspect));
        }

        // Keyed by instance id rather than by the Sprite itself: a destroyed sprite is
        // Unity-fake-null and would sit in the table answering for art that is gone. Cleared
        // with the rest of the art caches when a run starts.
        private static readonly Dictionary<int, Metrics> Cache = new Dictionary<int, Metrics>();

        /// <summary>Spouts found by subtracting a closed shot from an open one, per pair.</summary>
        private static readonly Dictionary<long, Vector2> Caps = new Dictionary<long, Vector2>();

        public static void ClearCache() { Cache.Clear(); Caps.Clear(); }

        /// <summary>Measures a sprite, once. Everything else here is arithmetic on this.</summary>
        public static Metrics Of(Sprite sprite)
        {
            if (sprite == null) return new Metrics(false, Vector2.one, new Rect(0, 0, 1, 1), new Vector2(0.5f, 1f));
            int key = sprite.GetInstanceID();
            if (Cache.TryGetValue(key, out var hit)) return hit;
            var m = Measure(sprite);
            Cache[key] = m;
            return m;
        }

        private static Metrics Measure(Sprite sprite)
        {
            // TWO SPACES, and they are not the same one. A UI Image lays a sprite out on
            // `sprite.rect` — the full sheet, transparent border and all — while the importer
            // trims the mesh down to `textureRect`, the window where pixels actually are, and
            // records where that window sits inside the sheet as `textureRectOffset`. Measure
            // in the window (that is where the pixels live), then answer in the SHEET (that is
            // where the Image will draw them). Measuring in one and answering in the other put
            // the lemon carton's spout 44px above its own cap, which is how this was found.
            Vector2 sheet = sprite.rect.size;
            var whole = new Metrics(false, sheet, new Rect(0, 0, sheet.x, sheet.y),
                                    new Vector2(sheet.x * 0.5f, sheet.y));
            var tex = sprite.texture;
            // A tightly packed atlas sprite has no window to read — asking throws. Nothing in
            // this project is packed, and if that ever changes the sheet still answers.
            if (tex == null || sheet.x <= 0f || sheet.y <= 0f) return whole;
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight) return whole;

            Rect window = sprite.textureRect;
            Vector2 offset = sprite.textureRectOffset;
            int w = Mathf.RoundToInt(window.width), h = Mathf.RoundToInt(window.height);
            if (w <= 0 || h <= 0) return whole;
            // Every sprite under Resources/Items is imported readable (PatronArtPostprocessor),
            // but art can arrive from anywhere and an unreadable texture throws rather than
            // returning nothing — so the flag is asked, and the throw is caught anyway.
            if (!tex.isReadable) return whole;
            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (UnityException) { return whole; }

            int ox = Mathf.RoundToInt(window.x), oy = Mathf.RoundToInt(window.y);
            int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            for (int y = 0; y < h; y++)
            {
                int row = (oy + y) * tex.width + ox;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a < Ink) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
            if (x1 < x0) return whole;      // a blank sheet: nothing to measure

            // The mouth: the widest unbroken run of pixels along the drawing's top row. The
            // widest run rather than the row's centroid, because a leaf or a label corner that
            // reaches the same height would otherwise drag the spout off the cap.
            int top = (oy + y1) * tex.width + ox;
            int bestFrom = x0, bestTo = x0, from = -1;
            for (int x = 0; x <= w; x++)
            {
                bool ink = x < w && px[top + x].a >= Ink;
                if (ink && from < 0) from = x;
                if (ink || from < 0) continue;
                if (x - 1 - from > bestTo - bestFrom) { bestFrom = from; bestTo = x - 1; }
                from = -1;
            }
            var mouth = new Vector2(offset.x + (bestFrom + bestTo + 1) * 0.5f, offset.y + y1 + 1);
            return new Metrics(true, sheet,
                new Rect(offset.x + x0, offset.y + y0, x1 - x0 + 1, y1 - y0 + 1), mouth);
        }

        /// <summary>
        /// How big this vessel comes out when a scene offers it <paramref name="nominalH"/> —
        /// the DRAWING's size, which is what a shelf packs by and a shadow is cut to.
        /// <paramref name="gauge"/> is the sprite that sets the scale when it is not the one
        /// being drawn: an open bottle is measured against its own closed art, so taking the
        /// cap off makes a bottle lose a cap instead of growing to fill the gap.
        /// </summary>
        public static Vector2 StandSize(Sprite sprite, float nominalH, Sprite gauge = null)
        {
            if (sprite == null) return Placeholder(nominalH);
            var m = Of(sprite);
            float scale = Scale(m, gauge, nominalH);
            return new Vector2(m.Drawing.width * scale, m.Drawing.height * scale);
        }

        /// <summary>The slot a vessel with NO art gets: a plain slim bottle-shaped box, which
        /// is what the stages drew before any art existed and what the style tint still fills
        /// when a brand's sprite is missing.</summary>
        private static Vector2 Placeholder(float nominalH) => new Vector2(nominalH * 0.5f, nominalH);

        private static float Scale(Metrics m, Sprite gauge, float nominalH)
        {
            var g = gauge != null ? Of(gauge) : m;
            if (g.Drawing.height <= 0f) return 1f;
            return nominalH * g.Stature / g.Drawing.height;
        }

        /// <summary>
        /// Where the spout is, when the art in hand is the CAPLESS shot and a closed shot of
        /// the same vessel exists to compare it with.
        ///
        /// The top of the silhouette is the mouth on a BOTTLE, whose neck is the highest thing
        /// on the sheet with or without its cap. It is not the mouth on a CARTON: a carton's
        /// top is a flat roof, its spout a small stub set into it, and the highest row is the
        /// whole roof — so the juice poured out of the roof's middle, a good 20px from the
        /// hole (the author, 2026-08-11: "sıvının çıkış yerini kapak olarak ayarla").
        ///
        /// But the cap is precisely WHAT THE TWO SHOTS DISAGREE ABOUT. Everything else on the
        /// sheet is the same drawing; the cap is there in one and a dark hole in the other. So
        /// the spout is found by difference: the pixels that changed, up in the top of the
        /// vessel where a closure lives. Their middle is the spout's middle and the highest of
        /// them is where the pour leaves — measured, on any art, with no table to maintain.
        /// </summary>
        private static Vector2 Spout(Sprite drawn, Sprite gauge, Metrics md, Metrics mg)
        {
            // Only for the same drawing photographed twice. The v3 bottles' open shots are
            // re-cropped without their caps, so the two sheets are different sizes and there
            // is nothing to subtract — and there a bare neck IS the top of the silhouette.
            if (gauge == null || gauge == drawn || !md.Measured || !mg.Measured) return md.Mouth;
            if (!Close(md.Sheet.x, mg.Sheet.x) || !Close(md.Sheet.y, mg.Sheet.y)
                || !Close(md.Drawing.x, mg.Drawing.x) || !Close(md.Drawing.y, mg.Drawing.y)
                || !Close(md.Drawing.width, mg.Drawing.width)
                || !Close(md.Drawing.height, mg.Drawing.height)) return md.Mouth;

            long key = ((long)drawn.GetInstanceID() << 32) ^ (uint)gauge.GetInstanceID();
            if (Caps.TryGetValue(key, out var hit)) return hit;
            var cap = CapBetween(drawn, gauge, md);
            Caps[key] = cap;
            return cap;
        }

        private static Vector2 CapBetween(Sprite open, Sprite shut, Metrics m)
        {
            var ta = open.texture; var tb = shut.texture;
            if (ta == null || tb == null || !ta.isReadable || !tb.isReadable) return m.Mouth;
            Color32[] pa, pb;
            try { pa = ta.GetPixels32(); pb = tb.GetPixels32(); }
            catch (UnityException) { return m.Mouth; }

            Rect wa = open.textureRect, wb = shut.textureRect;
            int w = Mathf.RoundToInt(wa.width), h = Mathf.RoundToInt(wa.height);
            if (Mathf.RoundToInt(wb.width) != w || Mathf.RoundToInt(wb.height) != h) return m.Mouth;
            int ax = Mathf.RoundToInt(wa.x), ay = Mathf.RoundToInt(wa.y);
            int bx = Mathf.RoundToInt(wb.x), by = Mathf.RoundToInt(wb.y);

            // The top of the vessel only. A closure lives up there; a label redrawn between two
            // shots does not, and would otherwise drag the spout down the bottle.
            int floor = Mathf.RoundToInt(h * 0.6f);
            double count = 0, sumX = 0;
            int top = -1;
            for (int y = h - 1; y >= floor; y--)
            {
                int ra = (ay + y) * ta.width + ax, rb = (by + y) * tb.width + bx;
                for (int x = 0; x < w; x++)
                {
                    Color32 ca = pa[ra + x], cb = pb[rb + x];
                    int d = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g)
                          + Mathf.Abs(ca.b - cb.b) + Mathf.Abs(ca.a - cb.a);
                    if (d < 48) continue;
                    sumX += x; count++;
                    if (top < 0) top = y;
                }
            }
            // Two shots that agree everywhere are one drawing used twice: nothing was learned,
            // so the silhouette's own top stands.
            if (count < 4) return m.Mouth;
            return new Vector2(open.textureRectOffset.x + (float)(sumX / count),
                               open.textureRectOffset.y + top + 1);
        }

        private static bool Close(float a, float b) => Mathf.Abs(a - b) < 0.5f;

        /// <summary>
        /// Stands a vessel: sizes <paramref name="rect"/> to the sprite's sheet at the scale
        /// its drawing wants, then places it so the drawing's feet are on <paramref name="foot"/>
        /// and its middle is over it. The rect is the SHEET, not the drawing, because the art
        /// (and the fill stencil behind it) is drawn stretched across the whole sheet — this
        /// way <c>preserveAspect</c> has nothing left to letterbox and the two layers can never
        /// drift apart.
        ///
        /// Returns the MOUTH as an offset from the parent's pivot, which is the one number a
        /// pour stage needs from all of this: it swings with the vessel (<see cref="Swing"/>).
        /// </summary>
        public static Vector2 StandOn(RectTransform rect, Vector2 anchor, Sprite sprite,
            float nominalH, Vector2 foot, Sprite gauge = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            if (sprite == null)
            {
                rect.sizeDelta = Placeholder(nominalH);
                rect.anchoredPosition = foot;
                Vector2 bare = rect.localPosition;
                return new Vector2(bare.x, bare.y + nominalH);
            }

            var m = Of(sprite);
            float scale = Scale(m, gauge, nominalH);
            Vector2 size = m.Sheet * scale;
            rect.sizeDelta = size;
            // The sheet is shoved sideways by however far its drawing sits off centre, and down
            // by however much air was saved under its feet.
            float offCentre = (m.Drawing.x + m.Drawing.width * 0.5f) * scale - size.x * 0.5f;
            rect.anchoredPosition = new Vector2(foot.x - offCentre, foot.y - m.Drawing.y * scale);

            // Read back rather than recomputed: localPosition is the parent's PIVOT space,
            // which is the space the stage swings the vessel in, and Unity has already done
            // the anchor arithmetic that gets there.
            Vector2 mouthPx = Spout(sprite, gauge, m, gauge != null ? Of(gauge) : m);
            Vector2 home = rect.localPosition;
            return new Vector2(home.x + mouthPx.x * scale - size.x * 0.5f,
                               home.y + mouthPx.y * scale);
        }

        /// <summary>Where an offset from the pivot ends up once the vessel is tilted
        /// <paramref name="tiltDeg"/> counter-clockwise — the pour stages' one bit of
        /// trigonometry, written once.</summary>
        public static Vector2 Swing(Vector2 offset, float tiltDeg)
        {
            float rad = tiltDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);
        }
    }
}
