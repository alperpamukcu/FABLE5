using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// One named drink a customer asks for, with its menu price (GDD 23 §3) and how they want
    /// it served (§3.1). Orders come from the bar's UNLOCKED menu — a drink nobody has bought
    /// the recipe for is never asked for — but not from its stock: an unlocked drink whose
    /// bottle has run dry can still be ordered, and answering "we're out" is part of the job
    /// (v5 P11, C2).
    /// </summary>
    public sealed class DrinkOrder
    {
        public RecipeDefinition Wanted { get; }
        public int Price { get; }

        /// <summary>How they want it served (v5 P11). Reading the licence reveals it; missing
        /// any part of it costs tip, never the payment.</summary>
        public ServingSpec Spec { get; }

        /// <summary>The garnishes on the spec — kept for the UI and the older read paths.</summary>
        public IReadOnlyList<PreparationDefinition> Garnishes => Spec.Garnishes;

        public DrinkOrder(RecipeDefinition wanted, int price, ServingSpec spec = null)
        {
            Wanted = wanted ?? throw new ArgumentNullException(nameof(wanted));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            Price = price;
            Spec = spec ?? ServingSpec.Plain;
        }

        /// <summary>
        /// Menu price (v5 P11, GDD 23 §3): deliberately LOW. Serving the right drink pays this
        /// and little more; the money is in serving it well, which is what the tip is for. It
        /// was <c>4 + rank</c> — about twice this — back when the tip was a $4 rounding error
        /// and a correct-but-careless serve earned nearly as much as a perfect one.
        /// </summary>
        /// <summary>The P16 redesign tried steeper ladders here (3+r, 3+0.75r) and measured
        /// both too rich once the floor bot could actually GROW the menu — the original curve
        /// was never the problem, the unplayable menu was. Rank 1 = $4, 7 = $7, 28 = $17.</summary>
        public static int MenuPrice(RecipeDefinition recipe) => 3 + (recipe.Rank + 1) / 2;

        /// <summary>The garnishes a customer can ask for. Kept as the old name for callers.</summary>
        public static IReadOnlyList<PreparationDefinition> GarnishPool => ServingSpec.GarnishPool;

        /// <summary>
        /// Rolls an order from the day-scaled pool (stream "orders"): the lowest-rank pourable
        /// recipes on the bar's menu, the pool growing by one each day.
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
            return new DrinkOrder(pick, MenuPrice(pick), ServingSpec.Roll(pick, rng));
        }
    }
}
