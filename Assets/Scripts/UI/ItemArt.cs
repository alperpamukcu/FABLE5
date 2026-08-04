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
            if (Cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>($"Items/{name}");
            Cache[name] = s;
            return s;
        }

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
            // v3 sandwich bottles (GDD 25 §3): the base image is the vessel's INTERIOR —
            // the back plate — and the walls, cap and label arrive on the front plate that
            // BottleArt.AddLiquid hangs over the drink. The back is the same shut or open,
            // because the closure lives entirely in the front layer.
            var v3 = Load("v3_" + card.Id + "_back");
            if (v3 != null) return v3;
            var own = Load("bot_" + card.Id);
            return own != null ? own : Load(card.Info?.Style);
        }

        /// <summary>The same brand with its closure off — what the pour stage shows, because
        /// a bottle you are pouring from is open.</summary>
        public static Sprite BottleOpen(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            var v3 = Load("v3_" + card.Id + "_back");   // stateless: the cap is front-plate
            if (v3 != null) return v3;
            // The brand's own capless shot, then its STYLE's — a tier-one brand has no art
            // of its own but its style does, and falling straight through to the shut
            // bottle would have put the cap back on the one in your hand.
            return Load("bot_" + card.Id + "_open")
                ?? Load(card.Info?.Style + "_open")
                ?? Bottle(card);
        }

        public static Sprite Shaker => Load("shaker");
        public static Sprite Glass => Load("glass");

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
