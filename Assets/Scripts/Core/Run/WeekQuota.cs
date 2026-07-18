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

        /// <summary>How much is still owed; 0 once the quota is met.</summary>
        public int Remaining => Math.Max(0, Required - Earned);

        public void Add(int satisfaction) => Earned += Math.Max(0, satisfaction);

        public override string ToString() => $"Week {Week}: {Earned}/{Required}";
    }

    /// <summary>
    /// The pressure curve (GDD 20). Tuned against a ceiling of 3 satisfaction per customer
    /// and <see cref="NightsPerWeek"/> × 3 customers per week, so week 1 asks for a third of
    /// a perfect week and the last asks for most of one.
    /// </summary>
    public static class QuotaTable
    {
        /// <summary>Nights in a week; the quota gate fires when the last one closes.</summary>
        public const int NightsPerWeek = 2;

        private static readonly int[] Curve = { 6, 9, 12, 14 };

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
