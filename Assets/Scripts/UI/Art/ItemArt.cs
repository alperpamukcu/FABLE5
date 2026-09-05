using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The drink item sprites (2026-07-23): bottles by style, the shaker, the serving glass,
    /// and the ice/lemon/salt/sugar preparations — hi-bit pixel art in Assets/Resources/Items,
    /// point-imported by PatronArtPostprocessor. Loaded once and cached; the service flow shows
    /// these in the menu boxes, in the pouring hand, and as the vessels themselves (GDD 24 §2–3).
    /// </summary>
    public static class ItemArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // A MISS is never cached, and a destroyed sprite does not count as a hit
            // (Unity's fake-null). Both bit at once on 2026-08-05: plates shipped
            // while the editor sat in play, and a front plate asked for before its
            // import was remembered as "does not exist" for the whole session — the
            // sandwich then had no front, and the drink floated OVER the bottle.
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>($"Items/{name}");
            if (s != null) Cache[name] = s;
            else Cache.Remove(name);
            return s;
        }

        // (BoardPlate retired on 2026-08-26. The plate was generated art doing chrome's
        //  job and its frame was three different rails on one rectangle, none repeating —
        //  a nine-slice of noise. It is drawn now: ChromeArt.Instrument.)

        /// <summary>Forget every cached sprite — a new run re-resolves the art. The
        /// measurements go with them: a re-imported drawing is a new sprite and its old
        /// box would be an answer about a texture nobody is holding any more.</summary>
        public static void ClearCache() { Cache.Clear(); Opaque.Clear(); }

        // ── what a drawing actually covers (2026-09-04) ──────────────────────────
        //
        // Every pixel drawing in this game is a CANVAS with a drawing somewhere inside it,
        // and the two are not the same box: a 32x32 ice bucket carries two transparent rows
        // under its base, a v4 cellar copy is a 32x64 canvas round a 16px bottle. Anything
        // that stands a sprite ON something — the cellar's shelf, the counter's foot line —
        // has to ask where the DRAWING stops, or it lines up canvases and calls it level.
        //
        // It lived in DiegeticStage, where the cellar wrote it; it is here because the
        // counter needs the same answer, and two readings of one texture is how two props
        // on one bar end up on two lines.
        private static readonly Dictionary<Sprite, Rect> Opaque = new Dictionary<Sprite, Rect>();

        /// <summary>
        /// The opaque bounding box inside a sprite's canvas, in art pixels from its
        /// bottom-left. Measured once per sprite and kept — a texture read is not free and
        /// the answer cannot change while the sprite lives.
        ///
        /// A texture that is not readable answers with its whole canvas, which is the same
        /// thing every caller assumed before this existed: no worse than not asking.
        /// </summary>
        public static Rect OpaqueBounds(Sprite sp)
        {
            if (sp == null) return Rect.zero;
            if (Opaque.TryGetValue(sp, out var hit)) return hit;
            var r = Measure(sp);
            Opaque[sp] = r;
            return r;
        }

        /// <summary>How many transparent art rows sit UNDER the drawing — what a prop has to
        /// be lifted by for its lowest drawn pixel to land on a line.</summary>
        public static float FootPadding(Sprite sp)
        {
            var ob = OpaqueBounds(sp);
            return ob.width > 0f ? ob.y : 0f;
        }

        private static Rect Measure(Sprite sp)
        {
            var tex = sp.texture;
            if (tex == null || !tex.isReadable) return new Rect(0, 0, sp.rect.width, sp.rect.height);
            int x0 = Mathf.RoundToInt(sp.rect.x), y0 = Mathf.RoundToInt(sp.rect.y);
            int w = Mathf.RoundToInt(sp.rect.width), h = Mathf.RoundToInt(sp.rect.height);
            var px = tex.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[(y0 + y) * tex.width + x0 + x].a > 127)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return Rect.zero;
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // ── the house's two icons (2026-09-04) ───────────────────────────────────
        //
        // ONE STAR AND ONE HEART, EVERYWHERE (the author: "bundan sonra oyunda kalp ve
        // yıldız iconu olarak her yerde bunları kullanacaksın"). The game used to count in
        // three different stars — the author's shaded star3d in the top bar, a flat white
        // Items/star tinted per caller, and a 16px silhouette drawn in ChromeArt — which is
        // three answers to "how many stars is this bar". There is one now, in two states
        // (lit, and the empty socket), and the heart is drawn to the same construction
        // (Tools/heart_icon.py).
        //
        // THEY CARRY THEIR OWN COLOUR. A caller may dim one — the alpha still reads — but
        // nothing tints them any more: a gold star tinted with a line's ink is a different
        // star, which is the thing that was wrong with the flat one.
        //
        // Each ships at two sizes and this picks between them, because a 32px shaded icon
        // squeezed onto a 14px square under a point filter is mud (the bottle lesson,
        // PLAN_bottle_art_v4 §9.18): the 16 is drawn for the small rows, the 32 for the
        // night's big gauge. Falls back to the old flat star if the art is missing, so a
        // half-imported project still counts.

        /// <summary>The star, lit or empty, at the size it is about to be drawn.</summary>
        public static Sprite Star(bool lit, float px) =>
            Load(Name("star3d", lit, px)) ?? Load("star");

        /// <summary>The heart, lit or empty, at the size it is about to be drawn.</summary>
        public static Sprite Heart(bool lit, float px) =>
            Load(Name("heart3d", lit, px));

        private static string Name(string icon, bool lit, float px) =>
            icon + (lit ? "" : "_socket") + (px <= 20f ? "_16" : "");

        /// <summary>The bottle for a shelf style ("vodka", "gin", …); the asset names match.</summary>
        public static Sprite Bottle(string style) => Load(style);

        /// <summary>
        /// A BRAND's own bottle where one was drawn for it (the author, 2026-08-03: the
        /// 48-dollar vodka wore the house pour's bottle). The upper tiers of each spirit
        /// have their own vessel under <c>bot_{id}</c>; the tier that opens the bar keeps
        /// the style art, which is its art, so the fallback is the rule and not a mercy.
        /// </summary>
        // ── the v4 sandwich (2026-09-04, Docs/PLAN_bottle_art_v4.md) ──────────────
        //
        // A v4 bottle is THREE plates on one 96x192 canvas: the interior (back), the drink's
        // mask (shoulder to base — full means the shoulder), and the glass front with the
        // label pressed on it. The cellar carries the same three at 32x64, rebuilt at that
        // size rather than shrunk, with a drawn cap. Nothing here composes them: the hand
        // bench builds a BottleArt, the cellar builds sprite renderers under a SpriteMask.

        /// <summary>The v4 plates for a card, or null if the card has no v4 art yet.
        /// <paramref name="cellar"/> picks the 32x64 set.</summary>
        public static BottlePlates Plates(LastCall.Core.IngredientCard card, bool cellar = false)
        {
            if (card == null) return null;
            string s = cellar ? "_c" : "";
            var front = Load("v4_" + card.Id + "_front" + s);
            if (front == null) return null;
            return new BottlePlates(Load("v4_" + card.Id + "_back" + s),
                                    Load("v4_" + card.Id + "_mask" + s), front);
        }

        public sealed class BottlePlates
        {
            public readonly Sprite Back, Mask, Front;
            public BottlePlates(Sprite back, Sprite mask, Sprite front) { Back = back; Mask = mask; Front = front; }
            /// <summary>A sealed vessel (carton, can) ships one sprite and no cavity.</summary>
            public bool Sealed => Mask == null;
        }

        public static Sprite Bottle(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            // v4 first: the CELLAR front is the capped, closed bottle — the icon of the
            // brand wherever a closed bottle is asked for (market, hover card, the cellar).
            var v4 = Load("v4_" + card.Id + "_front_c") ?? Load("v4_" + card.Id + "_c");
            if (v4 != null) return v4;
            // The FLAT era (the author, 2026-08-05: "gerekirse sıvıları kaldır" — and it
            // was gerekli): the bottle is ONE composed sprite, back and front baked
            // together in the pipeline. No layers at runtime, nothing to mis-stack,
            // no liquid to stand in front of anything. The hover card carries what
            // is left in each bottle, as it always has.
            var flat = Load("v3_" + card.Id + "_flat");
            if (flat != null) return flat;
            var own = Load("bot_" + card.Id);
            return own != null ? own : Load(card.Info?.Style);
        }

        /// <summary>The same brand with its closure off — what the pour stage shows, because
        /// a bottle you are pouring from is open.</summary>
        public static Sprite BottleOpen(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            // v4: the master IS the open bottle (generated uncapped), so the hand front is it.
            var v4 = Load("v4_" + card.Id + "_front") ?? Load("v4_" + card.Id);
            if (v4 != null) return v4;
            var flatOpen = Load("v3_" + card.Id + "_flat_open");
            if (flatOpen != null) return flatOpen;
            // The brand's own capless shot, then its STYLE's — a tier-one brand has no art
            // of its own but its style does, and falling straight through to the shut
            // bottle would have put the cap back on the one in your hand.
            return Load("bot_" + card.Id + "_open")
                ?? Load(card.Info?.Style + "_open")
                ?? Bottle(card);
        }

        public static Sprite Shaker => Load("shaker");

        /// <summary>The tray piece for a preparation id (ice / lemon_twist).</summary>
        public static Sprite Prep(string prepId)
        {
            switch (prepId)
            {
                case "ice": return Load("ice");
                case "lemon_twist": return Load("prep_lemon");
                // No salt_rim/sugar_rim (2026-08-27): a rim is a CRUST drawn onto the
                // mouth (GlassDecor), never a piece off a tray, so nothing asked for
                // those two and their drawings were swept with the accessor's callers.
                default: return null;
            }
        }

        // (Bucket retired 2026-08-27 with the eight drawings behind it. Its one caller
        //  was the serve bench's AddFinishTub, cut with the finishing table; the room's
        //  counter draws its own dishes from the rail table in TycoonHud.Seats.)
    }
}
