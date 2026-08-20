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
        public static Sprite Bottle(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
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

        /// <summary>The tray piece for a preparation id (ice / lemon_twist / salt_rim / sugar_rim).</summary>
        public static Sprite Prep(string prepId)
        {
            switch (prepId)
            {
                case "ice": return Load("ice");
                case "lemon_twist": return Load("prep_lemon");
                case "salt_rim": return Load("salt");
                case "sugar_rim": return Load("sugar");
                default: return null;
            }
        }

        /// <summary>The source bucket a preparation is dragged out of, on the shaker stage.</summary>
        public static Sprite Bucket(string prepId)
        {
            switch (prepId)
            {
                case "ice": return Load("ice_bucket");
                case "lemon_twist": return Load("lemon_bucket");
                case "salt_rim": return Load("salt_bucket");
                case "sugar_rim": return Load("sugar_bucket");
                default: return null;
            }
        }
    }
}
