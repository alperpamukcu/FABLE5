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

        /// <summary>The garnishes this customer wants on it (2026-07-22, emotion→recipe pivot):
        /// some want it on ice, some with a twist. Reading the licence (GDD 24 §5) reveals
        /// them; adding what they asked lifts satisfaction and the tip.</summary>
        public IReadOnlyList<PreparationDefinition> Garnishes { get; }

        public DrinkOrder(RecipeDefinition wanted, int price,
            IReadOnlyList<PreparationDefinition> garnishes = null)
        {
            Wanted = wanted ?? throw new ArgumentNullException(nameof(wanted));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            Price = price;
            Garnishes = garnishes ?? Array.Empty<PreparationDefinition>();
        }

        /// <summary>Menu price v0 (GDD 23 §3): $4 + $1 × rank. Wealth tiers and brand
        /// tiers modify this at the till, not on the menu.</summary>
        public static int MenuPrice(RecipeDefinition recipe) => 4 + recipe.Rank;

        /// <summary>The garnishes a customer can ask for (the four droppable preparations).</summary>
        public static readonly IReadOnlyList<PreparationDefinition> GarnishPool = new[]
        {
            Preparations.Ice, Preparations.LemonTwist, Preparations.SaltRim, Preparations.SugarRim,
        };

        /// <summary>
        /// Rolls an order from the day-scaled pool (stream "orders"): the lowest-rank
        /// pourable recipes, pool growing by one each day — day 1 asks for simple things,
        /// day 10 asks for the top of the card. Also rolls 0–2 garnish wants.
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
            return new DrinkOrder(pick, MenuPrice(pick), RollGarnishes(rng));
        }

        /// <summary>Half the time plain; otherwise one or two garnishes off the pool.</summary>
        private static IReadOnlyList<PreparationDefinition> RollGarnishes(SeededRng rng)
        {
            int count = rng.NextInt(100) < 50 ? 0 : (rng.NextInt(100) < 65 ? 1 : 2);
            if (count == 0) return Array.Empty<PreparationDefinition>();

            var chosen = new List<PreparationDefinition>(count);
            var bag = new List<PreparationDefinition>(GarnishPool);
            for (int i = 0; i < count && bag.Count > 0; i++)
            {
                int k = rng.NextInt(bag.Count);
                chosen.Add(bag[k]);
                bag.RemoveAt(k);
            }
            return chosen;
        }
    }
}
