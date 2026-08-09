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
        /// <summary>Prices ×1.25. Reputation compounds.</summary>
        HighRoller,
    }

    /// <summary>
    /// One closed day on the books (GDD 23 §6).
    ///
    /// It used to carry four numbers, so the register's book could only ever say that a day
    /// made money or lost it — never WHY. Every field below was already known at the moment
    /// the day closed and simply was not written down: the run has the sales and the tips
    /// apart, the rent apart from the restock apart from the fittings, and the floor knows
    /// who drank and who walked. A book that says "Day 4, −$180" tells the player nothing
    /// they can act on; one that says the rent was covered but $210 went on stock and two
    /// customers left without a drink is a night they can play differently.
    ///
    /// The optional arguments keep every existing caller and test compiling: a day booked
    /// without the detail reads as a day whose detail was never recorded, not as a day where
    /// nothing happened.
    /// </summary>
    public sealed class DayResult
    {
        public int Day { get; }
        public int Income { get; }
        public int Expenses { get; }
        public int Net => Income - Expenses;
        public double AverageSatisfaction { get; }

        /// <summary>What the drinks themselves brought in, before anything was tipped.</summary>
        public int Sales { get; }

        /// <summary>What was tipped on top — the part of the income the player EARNED rather
        /// than charged, which is the half a price list cannot explain.</summary>
        public int Tips { get; }

        /// <summary>The three expenses, apart. A night that loses money to rent is a different
        /// night from one that loses it to restocking the shelf.</summary>
        public int Rent { get; }
        public int Stock { get; }
        public int Upgrades { get; }

        /// <summary>Customers who drank and left, and customers who left without one.</summary>
        public int Served { get; }
        public int WalkedOut { get; }

        /// <summary>The night's own stars, before the standing averaged them in — what THIS
        /// night was worth, as opposed to what the bar is worth.</summary>
        public double NightStars { get; }

        /// <summary>The till once everything was paid. This is the number the strike watches,
        /// so it is the number the book has to show.</summary>
        public int TillAfter { get; }

        /// <summary>Whether the detail below was recorded at all (see the class remarks).</summary>
        public bool HasDetail { get; }

        public DayResult(int day, int income, int expenses, double averageSatisfaction,
            int sales = 0, int tips = 0, int rent = 0, int stock = 0, int upgrades = 0,
            int served = 0, int walkedOut = 0, double nightStars = 0, int tillAfter = 0,
            bool hasDetail = false)
        {
            Day = day;
            Income = income;
            Expenses = expenses;
            AverageSatisfaction = averageSatisfaction;
            Sales = sales;
            Tips = tips;
            Rent = rent;
            Stock = stock;
            Upgrades = upgrades;
            Served = served;
            WalkedOut = walkedOut;
            NightStars = nightStars;
            TillAfter = tillAfter;
            HasDetail = hasDetail;
        }
    }

    /// <summary>What a night was made of, handed to <see cref="DayLedger.CloseDay"/> so the
    /// book can keep it. A plain carrier: the run owns every one of these numbers already,
    /// and this exists only so closing a day does not need eight more parameters.</summary>
    public sealed class DayDetail
    {
        public int Sales, Tips, Rent, Stock, Upgrades, Served, WalkedOut;
        public double NightStars;
    }

    /// <summary>
    /// The books, and with them the only way to lose (GDD 23 §6): close **three
    /// consecutive days with the till below zero** and the bar is gone. In debt means in
    /// debt — a rich bar can eat a losing day; a broke one is on the clock. One day back
    /// above water wipes the strikes. Also decides tomorrow's crowd (§7).
    /// </summary>
    public sealed class DayLedger
    {
        public const int StrikesToClose = 3;

        /// <summary>Superseded by <see cref="BarRating.HighRollerStars"/> and
        /// <see cref="BarRating.BrokeStars"/> (v5 P12 / D3), which are the same two lines read
        /// on the scale the player can see. Kept because they are the satisfaction the star
        /// bars were derived from, and the derivation is the point.</summary>
        public const double HighRollerBar = 0.75;
        public const double BrokeBar = 0.40;

        private readonly List<DayResult> _history = new List<DayResult>();
        public IReadOnlyList<DayResult> History => _history;

        /// <summary>Consecutive days closed with the till below zero.</summary>
        public int DebtStrikes { get; private set; }

        public bool IsBankrupt => DebtStrikes >= StrikesToClose;

        public WealthTier TomorrowsCrowd { get; private set; } = WealthTier.Regular;

        /// <summary>Closes a day: books it, advances the strike count, sets the crowd.
        /// <paramref name="tillAfter"/> is the money left once everything is paid — the
        /// strike watches the till, not the day's net.</summary>
        /// <param name="averageSatisfaction">Whatever the crowd is to be judged on. The run
        /// passes the bar's RUNNING star average converted back to satisfaction (v5 P12), so
        /// reputation carries between nights instead of resetting every morning.</param>
        public DayResult CloseDay(int day, int income, int expenses, double averageSatisfaction,
            int tillAfter)
            => CloseDay(day, income, expenses, averageSatisfaction, tillAfter, null);

        /// <summary>Closes a day and writes the night's breakdown down with it. Everything in
        /// <paramref name="detail"/> is already known to the caller at this moment; the book
        /// simply had nowhere to put it (see <see cref="DayResult"/>).</summary>
        public DayResult CloseDay(int day, int income, int expenses, double averageSatisfaction,
            int tillAfter, DayDetail detail)
        {
            if (income < 0) throw new ArgumentOutOfRangeException(nameof(income));
            if (expenses < 0) throw new ArgumentOutOfRangeException(nameof(expenses));

            var result = detail == null
                ? new DayResult(day, income, expenses, averageSatisfaction)
                : new DayResult(day, income, expenses, averageSatisfaction,
                    detail.Sales, detail.Tips, detail.Rent, detail.Stock, detail.Upgrades,
                    detail.Served, detail.WalkedOut, detail.NightStars, tillAfter, true);
            _history.Add(result);

            DebtStrikes = tillAfter < 0 ? DebtStrikes + 1 : 0;

            // v5 P12 / D3: the crowd is drawn by the bar's STANDING, not by one night's mood.
            // A single bad night no longer empties the room of money -- and one good one no
            // longer buys a rich crowd outright, which is what a reputation should mean.
            TomorrowsCrowd = BarRating.CrowdFor(BarRating.ExactStarsFor(averageSatisfaction));

            return result;
        }
    }
}
