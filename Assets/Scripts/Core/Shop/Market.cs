using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One bottle on the end-of-night market (GDD 22 §4, inventory pivot 2026-07-23). Either a
    /// <b>new stock</b> — a bottle of a style you do not carry yet, which is <i>added</i> to
    /// the shelf so you can serve drinks that need it — or an <b>upgrade</b>: a better brand of
    /// a style you already stock, which <i>replaces</i> it. New brands arrive full.
    /// </summary>
    public sealed class MarketOffer
    {
        public IngredientCard Bottle { get; }
        public bool IsNewStock { get; }   // true = adds a style you lack; false = upgrades one you have
        public int Price { get; }
        public string Style => Bottle.Info?.Style ?? string.Empty;
        public bool Sold { get; private set; }

        public MarketOffer(IngredientCard bottle, bool isNewStock, int price)
        {
            Bottle = bottle ?? throw new ArgumentNullException(nameof(bottle));
            if (bottle.Info == null)
                throw new ArgumentException($"'{bottle.Id}' has no info and cannot be sold.", nameof(bottle));
            IsNewStock = isNewStock;
            Price = price;
        }

        public void MarkSold() => Sold = true;
    }

    /// <summary>
    /// The end-of-night market (GDD 22 §4). It sells *stock*: bottles you do not carry yet
    /// (to grow the menu as customers ask for new drinks) and better brands of what you do
    /// (to earn more per pour). It only opens when the night closes — a bar takes deliveries
    /// then. Deterministic and rng-free: the catalogue is fixed data.
    /// </summary>
    public static class Market
    {
        /// <summary>A tier-1 bottle with no listed price is priced by type and tier here.</summary>
        public static int StockPrice(IngredientCard card)
        {
            if (card?.Info == null) return 0;
            if (card.Info.Price > 0) return card.Info.Price;
            int byTier = 8 + card.Info.Tier * 6;
            return card.Type == IngredientType.Spirit ? byTier + 6
                : card.Type == IngredientType.Garnish ? Math.Max(4, byTier - 6)
                : byTier;
        }

        /// <summary>
        /// The offers against the current shelf: for each style you carry, the next brand up
        /// (an upgrade); for each style in the catalogue you do <b>not</b> carry, the cheapest
        /// bottle of it (new stock to add).
        /// </summary>
        public static List<MarketOffer> OffersFor(Shelf shelf, IReadOnlyList<IngredientCard> catalogue)
        {
            var offers = new List<MarketOffer>();
            if (shelf == null || catalogue == null) return offers;

            // New stock: the cheapest catalogue bottle of each style not on the shelf.
            var newByStyle = new Dictionary<string, IngredientCard>();
            foreach (var candidate in catalogue)
            {
                var style = candidate.Info?.Style;
                if (string.IsNullOrEmpty(style)) continue;
                if (FindByStyle(shelf, style) != null) continue;   // already stocked → not new stock
                if (!newByStyle.TryGetValue(style, out var best) || candidate.Info.Tier < best.Info.Tier)
                    newByStyle[style] = candidate;
            }
            foreach (var card in newByStyle.Values)
                offers.Add(new MarketOffer(card, isNewStock: true, StockPrice(card)));

            // Upgrades: catalogue brands of a stocked style with a higher tier.
            foreach (var candidate in catalogue)
            {
                if (candidate.Info == null) continue;
                var current = FindByStyle(shelf, candidate.Info.Style);
                if (current?.Ingredient.Info == null) continue;
                if (candidate.Info.Tier > current.Ingredient.Info.Tier)
                    offers.Add(new MarketOffer(candidate, isNewStock: false, StockPrice(candidate)));
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
