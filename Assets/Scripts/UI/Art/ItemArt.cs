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

        /// <summary>Forget every cached sprite — a new run re-resolves the art.</summary>
        public static void ClearCache() => Cache.Clear();

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
