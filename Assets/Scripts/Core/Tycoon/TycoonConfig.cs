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

        // ── the floor (GDD 23 §1) ───────────────────────────────────────────────
        public int StartingSeats { get; } = 4;
        public int MaxSeats { get; } = 6;

        /// <summary>Seconds between arrivals, before jitter. Busier as days pass.</summary>
        public double ArrivalGap(int day) => Math.Max(8.0, 14.0 - 0.5 * day);
        public const double ArrivalJitter = 0.30;

        // ── patience (GDD 23 §2) ────────────────────────────────────────────────
        public double PatienceSeconds(int day) => Math.Max(30.0, 60.0 - 2.0 * day);
        public const double PatienceJitter = 0.20;

        /// <summary>One patience roll, jittered from the named stream.</summary>
        public double RollPatience(int day, SeededRng rng) =>
            PatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        // ── the day (GDD 23 §6) ─────────────────────────────────────────────────
        public int CustomersOnDay(int day) => Math.Min(14, 8 + day / 2);
        public int Rent(int day) => 8 + 2 * day;

        // ── orders (GDD 23 §3) ──────────────────────────────────────────────────
        /// <summary>The order roll pool: this many lowest-rank pourable recipes.</summary>
        public int OrderPoolSize(int day) => 3 + day;
    }
}
