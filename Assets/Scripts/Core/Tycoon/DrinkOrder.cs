using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// One named drink a customer asks for, with its menu price (GDD 23 §3). Orders come
    /// from what the bar can actually make — the pourable recipes — so a request is always
    /// answerable, and the only questions are craft and speed.
    /// </summary>
    public sealed class DrinkOrder
    {
        public RecipeDefinition Wanted { get; }
        public int Price { get; }

        public DrinkOrder(RecipeDefinition wanted, int price)
        {
            Wanted = wanted ?? throw new ArgumentNullException(nameof(wanted));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            Price = price;
        }

        /// <summary>Menu price v0 (GDD 23 §3): $4 + $1 × rank. Wealth tiers and brand
        /// tiers modify this at the till, not on the menu.</summary>
        public static int MenuPrice(RecipeDefinition recipe) => 4 + recipe.Rank;

        /// <summary>
        /// Rolls an order from the day-scaled pool (stream "orders"): the lowest-rank
        /// pourable recipes, pool growing by one each day — day 1 asks for simple things,
        /// day 10 asks for the top of the card.
        /// </summary>
        public static DrinkOrder Roll(IReadOnlyList<RecipeDefinition> recipes, int day,
            TycoonConfig config, SeededRng rng)
        {
            var pool = recipes
                .Where(r => r.RatioRequirements.Count > 0)
                .OrderBy(r => r.Rank)
                .Take(config.OrderPoolSize(day))
                .ToList();
            if (pool.Count == 0)
                throw new InvalidOperationException("No pourable recipes to order from.");

            var pick = pool[rng.NextInt(pool.Count)];
            return new DrinkOrder(pick, MenuPrice(pick));
        }
    }
}
