using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>How close the served drink came to what was asked for (GDD 23 §4).</summary>
    public enum OrderMatch
    {
        /// <summary>The drink they named. Full price.</summary>
        Exact,
        /// <summary>Wrong drink from the right family — dominant type matches. Full price, mild grumble.</summary>
        Close,
        /// <summary>Something else entirely. Half price and real anger.</summary>
        Wrong,
    }

    /// <summary>The money and outcome of one serve (GDD 23 §4–§5).</summary>
    public sealed class ServiceVerdict
    {
        public OrderMatch Match { get; }
        public int BasePaid { get; }
        public int Tip { get; }
        public int Total => BasePaid + Tip;
        /// <summary>They asked for garnishes and got every one of them (the craft read landed).</summary>
        public bool CraftLanded { get; }
        public bool OrdersAgain { get; }
        public double Satisfaction { get; }

        public ServiceVerdict(OrderMatch match, int basePaid, int tip,
            bool craftLanded, bool ordersAgain, double satisfaction)
        {
            Match = match;
            BasePaid = basePaid;
            Tip = tip;
            CraftLanded = craftLanded;
            OrdersAgain = ordersAgain;
            Satisfaction = satisfaction;
        }
    }

    /// <summary>
    /// Turns "what was served to whom, how fast" into money and satisfaction (GDD 23 §4).
    /// Pure and stateless: the visit carries the wait and the read, the caller carries the
    /// glass identification (recipe match + applied charges), the judge only prices it.
    /// </summary>
    public static class ServiceJudge
    {
        // GDD 23 §4. Emotion→recipe pivot (2026-07-22): the tip is the right drink served
        // fast, nothing to do with a hidden mood. The garnish craft they asked for lifts
        // satisfaction (and so the reputation and the crowd's wealth), and gates the extra
        // round — but does not pay a direct tip, keeping the till predictable.
        public const int SpeedTipMax = 4;          // biggest speed tip, at zero wait
        public const double SpeedTipWindow = 0.5;  // served inside the first half of patience
        // Widened 0.75 → 0.90 (2026-07-22): the extra order should reward *reading* someone
        // and serving their drink right, not also racing the clock. The user's ask was that
        // it be reachable, not fiendish — the skill is the read, the timing is a bonus (the
        // speed tip already pays that). Only a near-storm-off serve misses the extra round.
        public const double ExtraOrderWindow = 0.90;

        /// <summary>
        /// Compares the served glass to the order. Exact needs the named recipe; Close
        /// forgives the drink but not the family — its dominant type must match the
        /// order's dominant band type.
        /// </summary>
        public static OrderMatch Compare(DrinkOrder order, RecipeMatch served,
            GlassContents glass, Func<string, IngredientCard> lookup)
        {
            if (order == null || glass == null || glass.IsEmpty) return OrderMatch.Wrong;
            if (served?.Recipe != null && served.Recipe.Id == order.Wanted.Id) return OrderMatch.Exact;

            return DominantGlassType(glass, lookup) == DominantBandType(order.Wanted)
                ? OrderMatch.Close
                : OrderMatch.Wrong;
        }

        /// <summary>
        /// Prices one serve (emotion→recipe pivot, 2026-07-22). Wrong pays nothing; the right
        /// drink pays full plus a speed tip that fades with the wait. The garnishes the
        /// customer asked for — read off their licence — lift satisfaction when made and cost
        /// it when missed, and getting them all (on an exact, fast serve) earns another round.
        /// </summary>
        public static ServiceVerdict Judge(CustomerVisit visit, OrderMatch match,
            GlassContents delivered, WealthTier crowd = WealthTier.Regular, double ambienceBonus = 0)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            // The wrong drink is a wasted pour: they pay nothing and leave sore. The right
            // drink (exact or close family) is paid in full.
            int basePaid = match == OrderMatch.Wrong ? 0 : visit.Order.Price;

            // The garnish craft: how many of the asked-for garnishes actually made it in.
            var wanted = visit.Order.Garnishes;
            int wantedCount = wanted?.Count ?? 0;
            int matched = 0;
            if (wantedCount > 0 && delivered != null && match != OrderMatch.Wrong)
                foreach (var g in wanted) if (delivered.HasPreparation(g.Id)) matched++;
            bool allMatched = matched == wantedCount;                  // plain orders qualify
            bool craftLanded = wantedCount > 0 && allMatched;          // they wanted some, got them all
            double garnishScore = wantedCount == 0 ? 1.0 : (double)matched / wantedCount;

            // The faster the right drink lands, the bigger the tip — full inside the window,
            // fading to nothing at its edge. A broke crowd never tips for speed.
            int speedTip = 0;
            if (crowd != WealthTier.Broke && match != OrderMatch.Wrong)
            {
                double earliness = 1.0 - visit.WaitFraction / SpeedTipWindow;
                if (earliness > 0) speedTip = (int)Math.Ceiling(SpeedTipMax * earliness);
            }

            double satisfaction =
                (match == OrderMatch.Exact ? 0.9 : match == OrderMatch.Close ? 0.6 : 0.05)
                + (wantedCount > 0 && match != OrderMatch.Wrong ? 0.15 * (garnishScore - 0.5) : 0.0)
                - 0.3 * visit.WaitFraction
                + ambienceBonus;
            satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));

            // Another round is the reward for reading their garnish and nailing it — the
            // exact drink, every garnish they asked for, comfortably inside patience.
            bool ordersAgain = match == OrderMatch.Exact && craftLanded
                && visit.WaitFraction < ExtraOrderWindow
                && visit.ExtraOrdersTaken < CustomerVisit.MaxExtraOrders;

            return new ServiceVerdict(match, basePaid, speedTip, craftLanded, ordersAgain, satisfaction);
        }

        /// <summary>The type holding the biggest share of the glass.</summary>
        private static IngredientType DominantGlassType(GlassContents glass,
            Func<string, IngredientCard> lookup)
        {
            var byType = new Dictionary<IngredientType, double>();
            foreach (var id in glass.Ingredients)
            {
                var card = lookup?.Invoke(id);
                if (card == null) continue;
                byType.TryGetValue(card.Type, out double volume);
                byType[card.Type] = volume + glass.VolumeOf(id);
            }

            IngredientType best = default;
            double bestVolume = -1;
            foreach (var pair in byType)
                if (pair.Value > bestVolume) { best = pair.Key; bestVolume = pair.Value; }
            return best;
        }

        /// <summary>The type the recipe leans on hardest — the widest band midpoint.</summary>
        private static IngredientType DominantBandType(RecipeDefinition recipe)
        {
            IngredientType best = default;
            double bestMid = -1;
            foreach (var band in recipe.RatioRequirements)
            {
                double mid = (band.MinRatio + band.MaxRatio) / 2.0;
                if (mid > bestMid) { best = band.Type; bestMid = mid; }
            }
            return best;
        }
    }
}
