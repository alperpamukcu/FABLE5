using System.Collections.Generic;
using LastCall.Core;
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
        /// A BRAND's own bottle (the author, 2026-08-02: four vodkas were four cards wearing
        /// one drawing, because the art was keyed by STYLE). Each card has its own vessel
        /// under <c>bot_{id}</c>; the style sprite stays the fallback, both for a card with
        /// no art of its own and for the places that only know a style — a recipe's
        /// ingredient row names gin, not a gin.
        /// </summary>
        public static Sprite Bottle(IngredientCard card)
        {
            if (card == null) return null;
            var own = Load("bot_" + card.Id);
            return own != null ? own : Load(card.Info?.Style);
        }

        /// <summary>The same brand with its closure off — what a build stage shows, because
        /// a bottle you are pouring from is open. Drawn open, never cropped.</summary>
        public static Sprite BottleOpen(IngredientCard card)
        {
            if (card == null) return null;
            var open = Load("bot_" + card.Id + "_open");
            return open != null ? open : Bottle(card);
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
