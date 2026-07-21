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

    /// <summary>The money and mood outcome of one serve (GDD 23 §4–§5).</summary>
    public sealed class ServiceVerdict
    {
        public OrderMatch Match { get; }
        public int BasePaid { get; }
        public int Tip { get; }
        public int Total => BasePaid + Tip;
        public bool MoodTipLanded { get; }
        public bool OrdersAgain { get; }
        public double Satisfaction { get; }

        public ServiceVerdict(OrderMatch match, int basePaid, int tip,
            bool moodTipLanded, bool ordersAgain, double satisfaction)
        {
            Match = match;
            BasePaid = basePaid;
            Tip = tip;
            MoodTipLanded = moodTipLanded;
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
        // GDD 23 §4 — change these and the module table together.
        public const int MoodTipThreshold = 8;     // intent movement that earns the tip
        public const int MoodTipMin = 3;
        public const int MoodTipMax = 5;
        public const int SpeedTip = 1;
        public const double SpeedTipWindow = 0.35; // served inside the first 35% of patience
        public const double ExtraOrderWindow = 0.75;

        /// <summary>High rollers add this to a landed mood tip (GDD 23 §7).</summary>
        public const int HighRollerMoodBonus = 2;

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
        /// Prices one serve. Wrong pays half and tips nothing; mood and speed tips stack on
        /// Exact/Close; a perfect serve — exact, mood landed, comfortably inside patience —
        /// earns another order (GDD 23 §5), deliberately reachable. The crowd's wealth tier
        /// (GDD 23 §7) sweetens or sours the tips: high rollers tip bigger on a landed
        /// mood, a broke crowd never tips for speed.
        /// </summary>
        public static ServiceVerdict Judge(CustomerVisit visit, OrderMatch match,
            EmotionDelta applied, WealthTier crowd = WealthTier.Regular)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            int basePaid = match == OrderMatch.Wrong
                ? visit.Order.Price / 2
                : visit.Order.Price;

            bool moodLanded = false;
            int moodTip = 0;
            if (visit.Read != null && applied != null && match != OrderMatch.Wrong)
            {
                int move = applied[visit.Read.Intent];
                int toward = visit.Read.Direction == IntentDirection.Extinguish ? -move : move;
                if (toward >= MoodTipThreshold)
                {
                    moodLanded = true;
                    moodTip = Math.Min(MoodTipMax, MoodTipMin + (toward - MoodTipThreshold) / 5);
                    if (crowd == WealthTier.HighRoller) moodTip += HighRollerMoodBonus;
                }
            }

            int speedTip = crowd != WealthTier.Broke && match != OrderMatch.Wrong
                && visit.WaitFraction < SpeedTipWindow
                ? SpeedTip : 0;

            double satisfaction =
                (match == OrderMatch.Exact ? 0.9 : match == OrderMatch.Close ? 0.6 : 0.2)
                - 0.3 * visit.WaitFraction
                + (moodLanded ? 0.1 : 0.0);
            satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));

            bool ordersAgain = match == OrderMatch.Exact && moodLanded
                && visit.WaitFraction < ExtraOrderWindow
                && visit.ExtraOrdersTaken < CustomerVisit.MaxExtraOrders;

            return new ServiceVerdict(match, basePaid, moodTip + speedTip,
                moodLanded, ordersAgain, satisfaction);
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
