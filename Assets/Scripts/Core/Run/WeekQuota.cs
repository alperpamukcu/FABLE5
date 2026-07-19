using System;

namespace LastCall.Core
{
    /// <summary>
    /// The run's only loss condition (fork B). One customer you couldn't read never ends a
    /// run; a week of them does. Satisfaction accumulates across every serve in the week and
    /// is checked once, when the week closes.
    /// </summary>
    public sealed class WeekQuota
    {
        public int Week { get; }
        public int Required { get; }
        public int Earned { get; private set; }

        public WeekQuota(int week, int required)
        {
            if (week <= 0) throw new ArgumentOutOfRangeException(nameof(week));
            if (required < 0) throw new ArgumentOutOfRangeException(nameof(required));
            Week = week;
            Required = required;
        }

        public bool Met => Earned >= Required;

        /// <summary>A frozen copy, for reporting a week after the gate has moved on.</summary>
        public WeekQuota Snapshot()
        {
            var copy = new WeekQuota(Week, Required);
            copy.Earned = Earned;
            return copy;
        }

        /// <summary>How much is still owed; 0 once the quota is met.</summary>
        public int Remaining => Math.Max(0, Required - Earned);

        public void Add(int satisfaction) => Earned += Math.Max(0, satisfaction);

        public override string ToString() => $"Week {Week}: {Earned}/{Required}";
    }

    /// <summary>
    /// The pressure curve (GDD 20), measured rather than guessed — see `Docs/sim_report.md`
    /// and the `LastCall/Simulate` menu item.
    ///
    /// The first pass (6/9/12/14) held up on overall difficulty but had the wrong shape: a
    /// greedy bot cleared week 1 97% of the time and week 2 93%, so half the run was a
    /// formality and every decision that mattered lived in weeks 3–4. This curve keeps the
    /// end-to-end difficulty roughly where it was and moves pressure earlier, so week 2 is a
    /// real gate instead of a lap of honour.
    ///
    /// Caveat worth keeping in mind before trusting these numbers too far: they come from a
    /// one-ply bot that never shops. The *shape* comparison is sound; the absolute win rate
    /// is a floor, not a prediction.
    /// </summary>
    public static class QuotaTable
    {
        /// <summary>Nights in a week; the quota gate fires when the last one closes.</summary>
        public const int NightsPerWeek = 2;

        private static readonly int[] Curve = { 7, 11, 12, 14 };

        /// <summary>Satisfaction required to survive the given 1-based week.</summary>
        public static int Standard(int week)
        {
            if (week <= 0) throw new ArgumentOutOfRangeException(nameof(week));
            return week <= Curve.Length
                ? Curve[week - 1]
                : Curve[Curve.Length - 1] + 3 * (week - Curve.Length);
        }

        /// <summary>Which 1-based week a 1-based night belongs to.</summary>
        public static int WeekOfNight(int night) => (Math.Max(1, night) - 1) / NightsPerWeek + 1;

        /// <summary>True when this night is the last of its week — the gate closes after it.</summary>
        public static bool IsWeekEnd(int night) => night % NightsPerWeek == 0;
    }
}
