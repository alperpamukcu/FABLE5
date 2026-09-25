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
        /// <summary>
        /// A CHARACTER IS WORTH A NOTCH OFF THE SHEET (2026-09-22, the author: "o kokteyller
        /// hiçbir özelliği olmayanlardan bir tık daha uygun fiyatlı olabilir"). A buff is cheaper
        /// by <see cref="TraitPriceShare"/> and a nerf dearer by it, applied LAST and in exactly
        /// one place in the game, so nothing else has to know that characters exist.
        ///
        /// Stored as a share rather than a dollar so a future price curve keeps the notch
        /// proportional instead of inheriting a literal. Across the live range ($8 at rank 9 to
        /// $18 at rank 30) the rounding makes it exactly one dollar every time, which is a figure
        /// the book can print and the player can hold in their head — biggest as a SHARE where the
        /// drink is cheapest and the character weakest, smallest where the character is strongest.
        /// </summary>
        public const double TraitPriceShare = 0.07;

        /// <summary>The sheet below which a character is free. Below it the ladder has no room — a
        /// dollar off a $4 page is a quarter of the drink — and the opening menu is the fragile
        /// part of the economy, so the whole zero-star book is priced exactly as it was. At 0.07
        /// the integer rounding already lands the break here; the constant is written down so a
        /// tuner raising the share cannot move it by accident.</summary>
        public const int TraitPriceFloor = 8;

        /// <summary>What the page is worth before its character moves it: its rung's band for its
        /// kind of work (<see cref="DrinkPricing"/>, 2026-09-23). The menu prints this beside the
        /// real price on a page that carries a character, so the trade reads as a trade.</summary>
        public static int SheetPrice(RecipeDefinition recipe) => DrinkPricing.SheetPrice(recipe);

        /// <summary>
        /// THE TWO PAGES THE RANK TABLE CANNOT PRICE (2026-09-23). A pint and a neat pour carry no
        /// authored bands, so <see cref="DrinkPricing.RungOf"/> reads them as rank-1 pages and
        /// prices them at the bottom of the opening menu for ever — the keg would leave the economy
        /// the moment the bar climbed off the ground. Their price rides the BAR's rung instead; every
        /// caller that knows the standing passes it, and the ones that do not (a bench test, a page
        /// drawn with no run behind it) get the ground-floor figure, which is what they had.
        /// </summary>
        public static int MenuPrice(RecipeDefinition recipe, double barStars)
        {
            if (DrinkPricing.IsHousePour(recipe))
                return DrinkPricing.HousePourPrice(recipe, barStars);
            return MenuPrice(recipe);
        }

        public static int MenuPrice(RecipeDefinition recipe)
        {
            int sheet = SheetPrice(recipe);
            int sign = DrinkTraits.Of(recipe).PriceSign;
            if (sign == 0 || sheet < TraitPriceFloor) return sheet;
            return Math.Max(1, sheet + sign * (int)Math.Round(
                sheet * TraitPriceShare, MidpointRounding.AwayFromZero));
        }

        /// <summary>The garnishes a customer can ask for. Kept as the old name for callers.</summary>
        public static IReadOnlyList<PreparationDefinition> GarnishPool => ServingSpec.GarnishPool;

        /// <summary>
        /// Rolls one order uniformly from everything the bar can pour. THE REAL RUN DOES NOT COME
        /// THROUGH HERE ANY MORE (2026-09-23): a night's covers are cut as a whole by
        /// <see cref="DayPlan"/>, because a uniform pick is how the same bar doing the same work
        /// banked $40 one night and $95 the next. What is left here is the bench's roll — a fixture
        /// that wants "an order, any order" without standing up a run — and the day-scaled pool went
        /// with the rewrite: it capped the menu at the lowest-ranked <c>3 + day</c> pages, so
        /// everything a player bought to climb was unorderable for a fortnight.
        /// </summary>
        public static DrinkOrder Roll(IReadOnlyList<RecipeDefinition> recipes, int day,
            TycoonConfig config, SeededRng rng, IReadOnlyList<PreparationDefinition> garnishes = null)
        {
            // A page whose signature extra the bar cannot give tonight (the ladder's rung, the market's jar —
            // TycoonRun.PreparationsOpen) is not ordered (2026-09-21): nobody asks for a Southside where there
            // is no mint. A caller that passes no pool at all keeps every page.
            var pool = recipes
                .Where(r => r.RatioRequirements.Count > 0)
                .Where(r => r.Garnish == null || garnishes == null || garnishes.Any(g => g.Id == r.Garnish))
                .OrderBy(r => r.Rank)
                .ToList();
            if (pool.Count == 0)
                throw Said.With(new InvalidOperationException("No drinks you can pour yet."),
                    Line.Of("rule.no_pourable_drinks"));

            var pick = pool[rng.NextInt(pool.Count)];
            return new DrinkOrder(pick, MenuPrice(pick), ServingSpec.Roll(pick, rng, garnishes));
        }
    }
}
