using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>Who tomorrow's crowd is, decided by today's satisfaction bar (GDD 23 §7).</summary>
    public enum WealthTier
    {
        /// <summary>Prices ×0.75, no speed tips. A bar with a bad night draws a broke crowd.</summary>
        Broke,
        Regular,
        /// <summary>Prices ×1.25, mood tips +$2. Reputation compounds.</summary>
        HighRoller,
    }

    /// <summary>One closed day on the books (GDD 23 §6).</summary>
    public sealed class DayResult
    {
        public int Day { get; }
        public int Income { get; }
        public int Expenses { get; }
        public int Net => Income - Expenses;
        public double AverageSatisfaction { get; }

        public DayResult(int day, int income, int expenses, double averageSatisfaction)
        {
            Day = day;
            Income = income;
            Expenses = expenses;
            AverageSatisfaction = averageSatisfaction;
        }
    }

    /// <summary>
    /// The books, and with them the only way to lose (GDD 23 §6): three consecutive days
    /// in the red close the bar. One good day wipes the strikes — debt is a spiral you can
    /// climb out of, not a slow certainty. Also decides tomorrow's crowd (§7).
    /// </summary>
    public sealed class DayLedger
    {
        public const int StrikesToClose = 3;
        public const double HighRollerBar = 0.75;
        public const double BrokeBar = 0.40;

        private readonly List<DayResult> _history = new List<DayResult>();
        public IReadOnlyList<DayResult> History => _history;

        /// <summary>Consecutive red days so far.</summary>
        public int DebtStrikes { get; private set; }

        public bool IsBankrupt => DebtStrikes >= StrikesToClose;

        public WealthTier TomorrowsCrowd { get; private set; } = WealthTier.Regular;

        /// <summary>Closes a day: books it, advances the strike count, sets the crowd.</summary>
        public DayResult CloseDay(int day, int income, int expenses, double averageSatisfaction)
        {
            if (income < 0) throw new ArgumentOutOfRangeException(nameof(income));
            if (expenses < 0) throw new ArgumentOutOfRangeException(nameof(expenses));

            var result = new DayResult(day, income, expenses, averageSatisfaction);
            _history.Add(result);

            DebtStrikes = result.Net < 0 ? DebtStrikes + 1 : 0;

            TomorrowsCrowd = averageSatisfaction >= HighRollerBar ? WealthTier.HighRoller
                : averageSatisfaction >= BrokeBar ? WealthTier.Regular
                : WealthTier.Broke;

            return result;
        }
    }
}
