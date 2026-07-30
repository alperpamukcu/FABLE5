using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What the bar is worth to the people who drink in it (v5 P12, GDD 23 §7). Every customer
    /// leaves between one and five stars on their way out — including the ones who left angry,
    /// because a storm-off is a review too.
    ///
    /// This is the **visible** reputation currency and it replaces the TONIGHT satisfaction bar
    /// (D3). Nothing about the judge changed shape to make it: the stars are the same
    /// satisfaction the judge already computed, read on a scale people understand.
    /// </summary>
    public sealed class BarRating
    {
        public const int MinStars = 1, MaxStars = 5;

        /// <summary>Where a night's average sits before the crowd starts arriving richer, and
        /// where it sinks to before they start arriving broke (GDD 23 §7). The old satisfaction
        /// bars (0.75 / 0.40) read straight across: 1 + 4×0.75 = 4.0, 1 + 4×0.40 = 2.6.</summary>
        public const double HighRollerStars = 4.0;
        public const double BrokeStars = 2.6;

        /// <summary>Neutral: what an unrated bar counts as, and the pivot the arrival rate
        /// scales around. A brand-new bar is neither famous nor infamous.</summary>
        public const double NeutralStars = 3.0;

        private readonly List<double> _nights = new List<double>();

        /// <summary>Each closed night's average, oldest first.</summary>
        public IReadOnlyList<double> Nights => _nights;

        /// <summary>Stars left over the whole run so far.</summary>
        public int Ratings { get; private set; }

        private double _sum;

        /// <summary>The bar's standing overall — what the top corner shows. A bar nobody has
        /// rated yet reads neutral rather than zero: it has no reputation, not a bad one.</summary>
        public double Average => Ratings == 0 ? NeutralStars : _sum / Ratings;

        /// <summary>Last night's average, or neutral before the first night closes.</summary>
        public double LastNight => _nights.Count == 0 ? NeutralStars : _nights[_nights.Count - 1];

        /// <summary>The stars one visit leaves. Satisfaction 0 is one star, not zero — the
        /// scale starts at one, and someone who storms off still fills in the card.</summary>
        public static int StarsFor(double satisfaction)
        {
            double raw = 1.0 + 4.0 * Math.Max(0.0, Math.Min(1.0, satisfaction));
            int stars = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            return stars < MinStars ? MinStars : stars > MaxStars ? MaxStars : stars;
        }

        /// <summary>The star value of a satisfaction score, unrounded — what an average is
        /// built from, so a night of 3.4-star serves does not round its way to 3.0.</summary>
        public static double ExactStarsFor(double satisfaction) =>
            1.0 + 4.0 * Math.Max(0.0, Math.Min(1.0, satisfaction));

        /// <summary>Records one customer's rating as they leave.</summary>
        public void Record(double satisfaction)
        {
            _sum += ExactStarsFor(satisfaction);
            Ratings++;
        }

        /// <summary>Closes a night, filing its own average for the receipt and the history.</summary>
        public void CloseNight(double nightAverageSatisfaction) =>
            _nights.Add(ExactStarsFor(nightAverageSatisfaction));

        /// <summary>
        /// How the standing bends the arrival rate (GDD 23 §7). A well-reviewed bar is busier:
        /// at five stars the gaps between arrivals close to 75% of neutral, at one star they
        /// stretch to 130%. Neutral is exactly 1.0, so an unrated bar behaves as it always did.
        /// </summary>
        public static double ArrivalRateFactor(double stars)
        {
            double offset = stars - NeutralStars;              // −2 … +2
            double factor = 1.0 - 0.125 * offset;              // 1.25 … 0.75
            return factor < 0.75 ? 0.75 : factor > 1.30 ? 1.30 : factor;
        }

        /// <summary>The crowd a standing draws tomorrow.</summary>
        public static WealthTier CrowdFor(double stars) =>
            stars >= HighRollerStars ? WealthTier.HighRoller
            : stars >= BrokeStars ? WealthTier.Regular
            : WealthTier.Broke;
    }
}
