using System.Collections.Generic;
using UnityEngine;

namespace LastCall.DebugUI
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
    }
}
