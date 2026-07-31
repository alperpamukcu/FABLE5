using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>How close the served drink came to what was asked for (GDD 23 §4).</summary>
    public enum OrderMatch
    {
        /// <summary>The drink they named.</summary>
        Exact,
        /// <summary>Wrong drink from the right family — dominant type matches.</summary>
        Close,
        /// <summary>Something else entirely. Pays only what the thing in the glass is worth.</summary>
        Wrong,
        /// <summary>Barely anything in the glass. They refuse to pay for it (v5 P11).</summary>
        Refused,
        /// <summary>The bar could not make it and said so (v5 P11, C2).</summary>
        Declined,
    }

    /// <summary>The money and outcome of one serve (GDD 23 §4–§5).</summary>
    public sealed class ServiceVerdict
    {
        public OrderMatch Match { get; }
        public int BasePaid { get; }
        public int Tip { get; }
        public int Total => BasePaid + Tip;
        /// <summary>They asked for things doing to the drink and got every one of them.</summary>
        public bool CraftLanded { get; }
        public bool OrdersAgain { get; }
        public double Satisfaction { get; }

        /// <summary>Share of the serving spec that was delivered, 0–1 (1 for a plain order).</summary>
        public double SpecScore { get; }

        /// <summary>How close the glass came to the fill they expected, 0–1.</summary>
        public double FillScore { get; }

        public ServiceVerdict(OrderMatch match, int basePaid, int tip,
            bool craftLanded, bool ordersAgain, double satisfaction,
            double specScore = 1, double fillScore = 1)
        {
            Match = match;
            BasePaid = basePaid;
            Tip = tip;
            CraftLanded = craftLanded;
            OrdersAgain = ordersAgain;
            Satisfaction = satisfaction;
            SpecScore = specScore;
            FillScore = fillScore;
        }
    }

    /// <summary>
    /// Turns "what was served to whom, how fast" into money and satisfaction (GDD 23 §4).
    /// Pure and stateless: the visit carries the wait and the read, the caller carries the
    /// glass identification (recipe match + applied charges), the judge only prices it.
    ///
    /// **v5 P11 rewrite.** The payment matrix the revision notes asked for: the base price is
    /// low and is what a *correct* drink earns, while the tip is the whole reward for doing
    /// the job well — speed, the serving spec, and the fill, each scaling continuously. Two
    /// rules changed outright and their old pins were rewritten with them: a wrong drink now
    /// pays what the thing in the glass is actually worth instead of nothing, and a glass with
    /// barely anything in it is refused outright.
    /// </summary>
    public static class ServiceJudge
    {
        // ── the tip (v5 P11) ────────────────────────────────────────────────────
        /// <summary>The best tip, as a share of the drink's base price. At 1.0 a perfect serve
        /// doubles the take — which is the point: base pay is low, service is the earner.</summary>
        public const double TipCeiling = 1.0;

        /// <summary>What the tip is made of. Speed leads, because it is the pressure the whole
        /// floor runs on; the spec is the craft read; the fill is the pour itself.</summary>
        public const double SpeedWeight = 0.45, SpecWeight = 0.35, FillWeight = 0.20;

        /// <summary>Below this fill the customer refuses to pay at all (v5 P11). A glass with a
        /// third of a drink in it is not a drink, whatever the ratios say.</summary>
        public const double RefusalFill = 0.35;

        // Widened 0.75 → 0.90 (2026-07-22): the extra order should reward *reading* someone
        // and serving their drink right, not also racing the clock.
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

            // No family, no forgiveness: a style-banded recipe (v5 P10) is specified down to
            // its brand of spirit, so there is no "near enough" version of it.
            var family = DominantBandType(order.Wanted);
            return family.HasValue && DominantGlassType(glass, lookup) == family.Value
                ? OrderMatch.Close
                : OrderMatch.Wrong;
        }

        /// <summary>The verdict for an order the bar could not fill and said so (v5 P11, C2).
        /// Nothing is paid and the night takes a mark — but telling someone straight is not
        /// the same as leaving them sitting there, and a storm-off scores worse.</summary>
        public static ServiceVerdict Declined() =>
            new ServiceVerdict(OrderMatch.Declined, 0, 0, false, false, 0.15);

        /// <summary>
        /// Prices one serve.
        ///
        /// <para><paramref name="served"/> is what the glass actually turned out to be — needed
        /// because a wrong drink now pays the delivered drink's own base price (v5 P11 / C1). An
        /// unidentifiable glass is worth nothing, which is the honest reading of "whatever this
        /// is, it is not a drink".</para>
        /// </summary>
        public static ServiceVerdict Judge(CustomerVisit visit, OrderMatch match,
            GlassContents delivered, WealthTier crowd = WealthTier.Regular, double ambienceBonus = 0,
            RecipeMatch served = null, double shakeEnergy = 0)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            var spec = visit.OrderTruth.Spec;
            double fill = delivered?.FillFraction ?? 0;

            // A glass with barely anything in it is refused before anything else is weighed.
            if (delivered != null && !delivered.IsEmpty && fill < RefusalFill)
                return new ServiceVerdict(OrderMatch.Refused, 0, 0, false, false,
                    0.02, 0, 0);

            // What the till takes. The right drink (exact or right family) pays its menu price;
            // the wrong one pays what was actually handed over, which may be nothing at all.
            int basePaid;
            if (match == OrderMatch.Wrong)
                basePaid = served?.Recipe != null ? DrinkOrder.MenuPrice(served.Recipe) : 0;
            else
                basePaid = visit.OrderTruth.Price;

            // How much of the job was done. Each part is a continuous 0–1: nothing here is a
            // cliff, which is the point of the rewrite — patience used to stop mattering at
            // half-time and the spec used to be worth nothing at the till.
            double specScore = spec.Delivered(delivered, shakeEnergy);
            double fillScore = FillScore(fill, spec.ExpectedFill);
            double speedScore = Math.Max(0.0, 1.0 - visit.WaitFraction);

            // A pint's craft is its head (GDD 21 §10.3) — it stands in for the spec, because a
            // pint is not garnished and the head is the part you had to get right by hand.
            bool draught = delivered != null && delivered.HasPreparation(Preparations.Draught.Id);
            double headScore = draught ? TapPour.HeadScore(delivered.Head / delivered.Capacity) : 1.0;
            double craftScore = draught ? headScore : specScore;

            double quality = SpeedWeight * speedScore
                           + SpecWeight * craftScore
                           + FillWeight * fillScore;

            // A broke crowd never tips; a wrong drink is not tipped for either.
            int tip = 0;
            if (crowd != WealthTier.Broke && match != OrderMatch.Wrong && basePaid > 0)
                tip = (int)Math.Round(basePaid * TipCeiling * quality, MidpointRounding.AwayFromZero);

            double satisfaction =
                (match == OrderMatch.Exact ? 0.75 : match == OrderMatch.Close ? 0.5 : 0.05)
                + (match != OrderMatch.Wrong ? 0.20 * (craftScore - 0.5) : 0.0)
                + (match != OrderMatch.Wrong ? 0.12 * (fillScore - 0.5) : 0.0)
                - 0.30 * visit.WaitFraction
                + ambienceBonus;
            satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));

            // Another round is the reward for the exact drink made the way they asked,
            // comfortably inside patience — and only from someone who has been in before.
            // A first-timer orders once (v5 P11, the notes' own rule); an anonymous crowd
            // (no emotion layer) keeps the old behaviour, since "returning" has no meaning
            // when nobody is remembered.
            //
            // The craft gate still needs them to have ASKED for something and got all of it —
            // a plain drink poured plainly is not a feat. Scoring it off the raw spec score
            // instead would hand every plain order a free extra round, which is a different
            // (and much richer) game: the sim's refill bill went up half again.
            bool craftForExtra = draught ? headScore >= 1.0 : (!spec.IsPlain && specScore >= 1.0);
            bool returning = visit.Regular == null || visit.Regular.Visits >= 1;
            bool ordersAgain = match == OrderMatch.Exact && craftForExtra && returning
                && visit.WaitFraction < ExtraOrderWindow
                && visit.ExtraOrdersTaken < CustomerVisit.MaxExtraOrders;

            return new ServiceVerdict(match, basePaid, tip, craftForExtra, ordersAgain,
                satisfaction, specScore, fillScore);
        }

        /// <summary>
        /// How close a fill came to what was expected, 0–1. Only shortfalls count: the glass
        /// cannot be overfilled (GDD 21 §3), so a brim-full glass never scores worse than the
        /// one that was asked for.
        /// </summary>
        public static double FillScore(double fill, double expected)
        {
            if (expected <= 0) return 1.0;
            double shortfall = expected - fill;
            if (shortfall <= 0) return 1.0;
            return Math.Max(0.0, 1.0 - shortfall / expected);
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

        /// <summary>The type the recipe leans on hardest — the widest band midpoint. Style-banded
        /// recipes (v5 P10) name no type, so they have no "family" and only an exact match will
        /// do; that is the right answer for a drink specified down to its brand of spirit.</summary>
        private static IngredientType? DominantBandType(RecipeDefinition recipe)
        {
            IngredientType? best = null;
            double bestMid = -1;
            foreach (var band in recipe.RatioRequirements)
            {
                if (band.IsStyleBand) continue;
                double mid = (band.MinRatio + band.MaxRatio) / 2.0;
                if (mid > bestMid) { best = band.Type; bestMid = mid; }
            }
            return best;
        }
    }
}
