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

        /// <summary>Puts a refunded offer back on the board (same-day refunds).</summary>
        public void MarkUnsold() => Sold = false;
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
        /// The bar standing a brand rung asks for before the distributor will talk to you
        /// (the author, 2026-08-02: the brand ladder climbs the same stars the menu does —
        /// starter, mid at 2.0, good at 3.0, great at 4.0). Rung 1 sells to anyone.
        /// </summary>
        /// <summary>
        /// THE FIRST STAR HAS TO BUY SOMETHING (2026-08-10, the author: "1 yıldız
        /// eklentileri de eklenmeli... mevcut içerikleri bölerek... oyuncu birden bire
        /// içeriğe boğulmamalı"). The ladder ran 0 → 2 → 3 → 4, so earning the first star
        /// opened nothing at all and the second opened a whole tier at once: a dead rung
        /// followed by a flood.
        ///
        /// Nothing new is added — tier 2 is SPLIT. Measured off base_bar.json, its five
        /// bottles price at 6, 6, 7, 7 and 15, so the four house brands arrive at one star
        /// and the one that costs twice as much waits for two. The split is by price rather
        /// than by a hand-picked list, so new tier-2 content sorts itself.
        /// </summary>
        public const int FirstRungPrice = 10;

        public static double RequiredStars(int tier, int price) =>
            tier <= 1 ? 0.0
            : tier == 2 ? (price > 0 && price < FirstRungPrice ? 1.0 : 2.0)
            : Math.Min(4.0, tier);

        /// <summary>Tier alone, for anything that has no price to hand. Kept honest by
        /// answering the LATER of the two, so nothing is offered a rung too early.</summary>
        public static double RequiredStars(int tier) => RequiredStars(tier, int.MaxValue);

        /// <summary>
        /// The offers against the current shelf: for each style in the catalogue you do
        /// <b>not</b> carry, the cheapest bottle of it (new stock); and every unowned brand
        /// of a style you do carry (a BETTER BOTTLE — which stands beside the old one, never
        /// in its place: the author, 2026-08-02, "mevcut alkol upgrade edilmeyecek, yeni bir
        /// alkol eklenecek". The well vodka keeps pouring the cheap drinks; the reserve
        /// exists for the cocktails that name it). Rungs above the bar's standing stay off
        /// the board — the ladder climbs the stars.
        /// </summary>
        public static List<MarketOffer> OffersFor(Shelf shelf, IReadOnlyList<IngredientCard> catalogue,
            double stars = double.MaxValue)
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
                if (stars >= RequiredStars(card.Info.Tier, card.Info.Price))
                    offers.Add(new MarketOffer(card, isNewStock: true, StockPrice(card)));

            // Better bottles: unowned brands of a stocked style, gated by the stars.
            foreach (var candidate in catalogue)
            {
                if (candidate.Info == null) continue;
                if (shelf.Find(candidate.Id) != null) continue;    // that exact brand is owned
                var current = FindByStyle(shelf, candidate.Info.Style);
                if (current?.Ingredient.Info == null) continue;
                if (stars >= RequiredStars(candidate.Info.Tier, candidate.Info.Price))
                    offers.Add(new MarketOffer(candidate, isNewStock: false, StockPrice(candidate)));
            }
            return offers;
        }

        /// <summary>
        /// What the board is HOLDING BACK, and at what standing — the same two branches as
        /// <see cref="OffersFor"/>, with the star test inverted.
        ///
        /// The shop could only ever show what the room's standing already allowed, so the
        /// stock waiting behind the next rung was invisible and an aisle that was merely
        /// early looked exactly like one that was finished (the author, 2026-08-10). Written
        /// as its own pass rather than folded into OffersFor because the two answer different
        /// questions and a caller wanting one must not pay for the other.
        /// </summary>
        public static List<(IngredientCard Card, double Stars)> GatedFor(
            Shelf shelf, IReadOnlyList<IngredientCard> catalogue, double stars)
        {
            var held = new List<(IngredientCard, double)>();
            if (shelf == null || catalogue == null) return held;

            var newByStyle = new Dictionary<string, IngredientCard>();
            foreach (var candidate in catalogue)
            {
                var style = candidate.Info?.Style;
                if (string.IsNullOrEmpty(style)) continue;
                if (FindByStyle(shelf, style) != null) continue;
                if (!newByStyle.TryGetValue(style, out var best) || candidate.Info.Tier < best.Info.Tier)
                    newByStyle[style] = candidate;
            }
            foreach (var card in newByStyle.Values)
                if (stars < RequiredStars(card.Info.Tier, card.Info.Price))
                    held.Add((card, RequiredStars(card.Info.Tier, card.Info.Price)));

            foreach (var candidate in catalogue)
            {
                if (candidate.Info == null) continue;
                if (shelf.Find(candidate.Id) != null) continue;
                var current = FindByStyle(shelf, candidate.Info.Style);
                if (current?.Ingredient.Info == null) continue;
                if (stars < RequiredStars(candidate.Info.Tier, candidate.Info.Price))
                    held.Add((candidate, RequiredStars(candidate.Info.Tier, candidate.Info.Price)));
            }
            return held;
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
