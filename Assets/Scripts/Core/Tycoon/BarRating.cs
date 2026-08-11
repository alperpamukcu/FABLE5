using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What the bar is worth to the people who drink in it (v5 P12, GDD 23 §7) — rebuilt
    /// 2026-08-02 on the author's loop ruling: **the stars are the game's spine now.**
    ///
    /// A new bar starts at ZERO. The standing is inertial: one night, however perfect,
    /// only drags it a step — and it climbs slower than it falls, because a reputation is
    /// easier to lose than to earn. The night's stars are also CAPPED by the caller
    /// (fittings and menu tier — see TycoonRun.UpgradeStarCap / MenuStarCap) before they
    /// touch the standing, which is what forces the loop: serve better drinks out of a
    /// better-fitted bar or stay a two-star dive forever. Five stars is the endgame.
    /// </summary>
    public sealed class BarRating
    {
        public const int MinStars = 1, MaxStars = 5;

        /// <summary>A brand-new bar's standing. Zero, on purpose: the climb IS the game.</summary>
        public const double StartStars = 0.0;

        /// <summary>Crowd thresholds (GDD 23 §7). The broke line reads TONIGHT's stars,
        /// not the standing (2026-08-02): keyed to the zero-start standing it either
        /// starved the whole first week (1.5) or went unreachable entirely (1.0, under the
        /// 1.0 clamp). Against a single night it means what it says — serve a dreadful
        /// night and tomorrow's crowd has no money; fame alone brings the rollers.</summary>
        public const double HighRollerStars = 4.2;
        public const double BrokeStars = 1.5;

        /// <summary>Display midpoint of the scale.</summary>
        public const double NeutralStars = 3.0;

        /// <summary>The standing the arrival rate pivots around. BELOW the display middle
        /// on purpose (2026-08-02): the fittings cap a young bar near 2.0, and pivoting at
        /// 3.0 starved every capped bar's doors for the whole run.</summary>
        public const double ArrivalPivot = 2.5;

        /// <summary>How fast the standing moves toward a night's (capped) stars.</summary>
        public const double GainRate = 0.10, LossRate = 0.20;

        /// <summary>The most one night can ever add. A five-star night in a one-star bar
        /// is a good story, not a new reputation.</summary>
        public const double MaxNightlyGain = 0.25;

        private readonly List<double> _nights = new List<double>();
        private double _standing = StartStars;

        /// <summary>Stars left over the whole run so far (per-visit tallies; feeds nothing
        /// but curiosity now that the standing is inertial).</summary>
        public int Ratings { get; private set; }

        private double _sum;

        /// <summary>The bar's standing overall — what the top corner shows.</summary>
        public double Average => _standing;

        /// <summary>Last night's (capped) stars, or the start value before any night closes.</summary>
        public double LastNight => _nights.Count == 0 ? StartStars : _nights[_nights.Count - 1];

        /// <summary>The star value of a satisfaction score, unrounded.</summary>
        public static double ExactStarsFor(double satisfaction) =>
            1.0 + 4.0 * Math.Max(0.0, Math.Min(1.0, satisfaction));

        /// <summary>Records one customer's rating as they leave.</summary>
        public void Record(double satisfaction)
        {
            _sum += ExactStarsFor(satisfaction);
            Ratings++;
        }

        /// <summary>Closes a night with no external ceiling (bench setups and old tests).</summary>
        public void CloseNight(double nightAverageSatisfaction) =>
            CloseNight(nightAverageSatisfaction, MaxStars);

        /// <summary>
        /// Closes a night: the night's stars are clamped to <paramref name="cap"/> (what
        /// the fittings and the menu allow it to be worth), then the standing takes one
        /// inertial step toward them — small when climbing, larger when falling.
        /// </summary>
        public void CloseNight(double nightAverageSatisfaction, double cap)
        {
            double night = Math.Min(ExactStarsFor(nightAverageSatisfaction), cap);
            _nights.Add(night);
            double delta = night - _standing;
            double move = delta * (delta >= 0 ? GainRate : LossRate);
            if (move > MaxNightlyGain) move = MaxNightlyGain;
            _standing = Math.Max(0.0, Math.Min(MaxStars, _standing + move));
        }

        /// <summary>Dev tooling only: parks the standing for the game-mode presets.</summary>
        public void DevSet(double stars) =>
            _standing = Math.Max(0.0, Math.Min(MaxStars, stars));

        /// <summary>
        /// How the standing bends the arrival rate (GDD 23 §7). A well-reviewed bar is
        /// busier; a no-name bar waits longer between doors. Widened for the zero start:
        /// at zero stars the gaps stretch to 135% of neutral.
        /// </summary>
        public static double ArrivalRateFactor(double stars)
        {
            double offset = stars - ArrivalPivot;
            double factor = 1.0 - 0.125 * offset;
            return factor < 0.75 ? 0.75 : factor > 1.30 ? 1.30 : factor;
        }

        /// <summary>The crowd a standing draws tomorrow.</summary>
        public static WealthTier CrowdFor(double stars) =>
            stars >= HighRollerStars ? WealthTier.HighRoller
            : stars >= BrokeStars ? WealthTier.Regular
            : WealthTier.Broke;
    }
}
