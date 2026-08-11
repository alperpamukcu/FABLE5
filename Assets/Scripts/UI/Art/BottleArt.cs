using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Which sprite a card draws with, cached per brand.
    ///
    /// This used to be a thousand lines of layered-liquid machinery — back plates, cavity
    /// fills, films, level curves, seam-cut opens. The 2026-08-07 sweep removed it whole:
    /// the bottles are single flat sprites (the author's picked takes), what is left in one
    /// lives on the hover card, and a properly layered set is an external artist's brief.
    /// When that art lands, the liquid layer gets rebuilt against it — not resurrected
    /// from here.
    ///
    /// It used to carry the sprite's aspect as well, which is how a shelf sized a bottle. It
    /// no longer does: the SHEET's aspect is not the bottle's, and telling the two apart is
    /// <see cref="VesselArt"/>'s job now (2026-08-11).
    /// </summary>
    public static class BottleArt
    {
        // Cached per brand. Sprites survive across runs, but domain reload is off in this
        // project, so a play session that re-imports art must start clean — hence ClearCache
        // from the HUD's run-start.
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static void ClearCache() => Cache.Clear();

        /// <summary>The bottle a card draws with. (The `open` variant died unreached —
        /// audit 2026-08-11: the pour bench takes ItemArt.BottleOpen directly.)</summary>
        public static Sprite For(IngredientCard card)
        {
            if (card == null) return null;
            if (Cache.TryGetValue(card.Id, out var cached) && cached != null) return cached;

            var sprite = ItemArt.Bottle(card);
            if (sprite != null) Cache[card.Id] = sprite;   // never cache a miss (Unity fake-null)
            return sprite;
        }
    }
}
