using System;

namespace LastCall.Core
{
    /// <summary>
    /// Every starting number of the tycoon loop in one place (GDD 23 §10). These are
    /// balance v0 stakes, not conclusions — the sim tunes them once the loop stands.
    /// Change a value here and the matching table in GDD 23, together.
    /// </summary>
    public sealed class TycoonConfig
    {
        public static readonly TycoonConfig Default = new TycoonConfig();

        // ── the till ────────────────────────────────────────────────────────────
        public int StartingMoney { get; } = 20;
        public double GlassCapacity { get; } = 1.0;

        /// <summary>Balance v1 (2026-07-22): tripled from v0 — stock is a real cost of
        /// goods now (~$2.5 a drink), not a rounding error. The v0 sim banked $5k by day
        /// 30 with zero bankruptcies; margins had to mean something.</summary>
        public int RefillPricePerCapacity { get; } = 3;

        // ── the floor (GDD 23 §1) ───────────────────────────────────────────────
        public int StartingSeats { get; } = 4;
        public int MaxSeats { get; } = 6;

        /// <summary>Price of the next stool (GDD 23 §8): $30, then $50.</summary>
        public int SeatPrice(int currentSeats) => 30 + 20 * (currentSeats - StartingSeats);

        // ── the crowd (GDD 23 §7) ───────────────────────────────────────────────
        public double PriceMultiplier(WealthTier crowd) =>
            crowd == WealthTier.HighRoller ? 1.25 : crowd == WealthTier.Broke ? 0.75 : 1.0;

        /// <summary>Seconds between arrivals, before jitter. Busier as days pass.</summary>
        public double ArrivalGap(int day) => Math.Max(6.0, 12.0 - 0.5 * day);
        public const double ArrivalJitter = 0.30;

        // ── patience (GDD 23 §2, balance v1) ────────────────────────────────────
        public double PatienceSeconds(int day) => Math.Max(22.0, 50.0 - 2.5 * day);
        public const double PatienceJitter = 0.20;

        /// <summary>One patience roll, jittered from the named stream.</summary>
        public double RollPatience(int day, SeededRng rng) =>
            PatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        // ── the day (GDD 23 §6) ─────────────────────────────────────────────────
        public int CustomersOnDay(int day) => Math.Min(14, 8 + day / 2);

        /// <summary>Balance v1: rent climbs hard enough to make a red day a real threat
        /// for a bar that stops improving — $20 on day 1, $65 by day 10.</summary>
        public int Rent(int day) => 15 + 5 * day;

        // ── orders (GDD 23 §3) ──────────────────────────────────────────────────
        /// <summary>The order roll pool: this many lowest-rank pourable recipes.</summary>
        public int OrderPoolSize(int day) => 3 + day;
    }
}
