using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Measures a bottle's art for the UI: which sprite a card draws with, and its aspect,
    /// so shelves can size a slim bottle by height without stretching it.
    ///
    /// This used to be a thousand lines of layered-liquid machinery — back plates, cavity
    /// fills, films, level curves, seam-cut opens. The 2026-08-07 sweep removed it whole:
    /// the bottles are single flat sprites (the author's picked takes), what is left in one
    /// lives on the hover card, and a properly layered set is an external artist's brief.
    /// When that art lands, the liquid layer gets rebuilt against it — not resurrected
    /// from here.
    /// </summary>
    public static class BottleArt
    {
        public readonly struct Piece
        {
            public readonly Sprite Sprite;
            public readonly float Aspect;   // width / height of the trimmed art

            public Piece(Sprite sprite)
            {
                Sprite = sprite;
                Aspect = sprite != null && sprite.rect.height > 0f
                    ? sprite.rect.width / sprite.rect.height
                    : 0f;
            }

            public bool Exists => Sprite != null;
        }

        // Cached per brand + openness. Pieces survive across runs, but domain reload is off
        // in this project, so a play session that re-imports art must start clean — hence
        // ClearCache from the HUD's run-start.
        private static readonly Dictionary<string, Piece> Cache = new Dictionary<string, Piece>();

        public static void ClearCache() => Cache.Clear();

        /// <summary>The bottle a card draws with — the closed art, or the capless pour art.</summary>
        public static Piece For(IngredientCard card, bool open = false)
        {
            if (card == null) return default;
            string key = (open ? "o:" : "c:") + card.Id;
            if (Cache.TryGetValue(key, out var cached) && cached.Sprite != null) return cached;

            var piece = new Piece(open ? ItemArt.BottleOpen(card) : ItemArt.Bottle(card));
            if (piece.Exists) Cache[key] = piece;   // never cache a miss (Unity fake-null)
            return piece;
        }
    }
}
