using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One brand upgrade on the end-of-night market (GDD 22 §4): a better bottle of a style
    /// you already stock. Buying it replaces the shelf bottle of that style — the new brand
    /// arrives full, and the old one goes back to the distributor.
    /// </summary>
    public sealed class MarketOffer
    {
        public IngredientCard Bottle { get; }
        public int Price => Bottle.Info?.Price ?? 0;
        public string Style => Bottle.Info?.Style ?? string.Empty;
        public bool Sold { get; private set; }

        public MarketOffer(IngredientCard bottle)
        {
            Bottle = bottle ?? throw new ArgumentNullException(nameof(bottle));
            if (bottle.Info == null)
                throw new ArgumentException($"'{bottle.Id}' has no info and cannot be sold as a brand.", nameof(bottle));
        }

        public void MarkSold() => Sold = true;
    }

    /// <summary>
    /// The end-of-night market (GDD 22 §4). Distinct from the Back Room, which sells power
    /// and knowledge between customers: the market sells *stock* — better brands for the
    /// bottles already on the shelf — and only opens when the night closes, because that is
    /// when a bar takes deliveries.
    ///
    /// Deterministic and rng-free: the catalogue is fixed data, and what is on offer is
    /// exactly "every brand strictly better than what you currently stock".
    /// </summary>
    public static class Market
    {
        /// <summary>
        /// The offers available against the current shelf: for each style stocked, every
        /// catalogue brand of that style with a higher tier than the one on the shelf.
        /// </summary>
        public static List<MarketOffer> OffersFor(Shelf shelf, IReadOnlyList<IngredientCard> catalogue)
        {
            var offers = new List<MarketOffer>();
            if (shelf == null || catalogue == null) return offers;

            foreach (var candidate in catalogue)
            {
                if (candidate.Info == null) continue;
                var current = FindByStyle(shelf, candidate.Info.Style);
                if (current?.Ingredient.Info == null) continue;
                if (candidate.Info.Tier > current.Ingredient.Info.Tier)
                    offers.Add(new MarketOffer(candidate));
            }
            return offers;
        }

        /// <summary>The shelf bottle stocking the given style, or null.</summary>
        public static ShelfBottle FindByStyle(Shelf shelf, string style)
        {
            if (shelf == null || string.IsNullOrEmpty(style)) return null;
            foreach (var bottle in shelf.Bottles)
                if (bottle.Ingredient.Info?.Style == style) return bottle;
            return null;
        }
    }
}
