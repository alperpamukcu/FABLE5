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
        // The live game's tuning: a served customer nurses the drink through three sip cycles
        // (~2.6s sip + ~1.8s hold each, matched in TycoonHud) before getting up to leave.
        public static readonly TycoonConfig Default = new TycoonConfig(savorSeconds: 13.2);

        public TycoonConfig(int startingMoney = 20,
            double orderDecisionSeconds = 4.0, double savorSeconds = 6.0)
        {
            if (orderDecisionSeconds < 0) throw new ArgumentOutOfRangeException(nameof(orderDecisionSeconds));
            if (savorSeconds < 0) throw new ArgumentOutOfRangeException(nameof(savorSeconds));
            StartingMoney = startingMoney;
            OrderDecisionSeconds = orderDecisionSeconds;
            SavorSeconds = savorSeconds;
        }

        /// <summary>
        /// Floor-time multiplier while a menu (service flow, licence) is open (GDD 24 §10):
        /// building a drink must not cost a storm-off by itself, but the clock never fully
        /// stops — haste still matters.
        /// </summary>
        public const double MenuTimeScale = 0.3;

        // ── the till ────────────────────────────────────────────────────────────
        public int StartingMoney { get; }
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

        // ── ambience upgrades (GDD 23 §8): a nicer bar pleases the room ──────────
        // Glassware, the counter, the back wall and a live musician are prestige, not
        // throughput or margin: each lifts every visit's satisfaction a little, which
        // draws a richer crowd tomorrow (§7). That is the third leg of the compounding
        // loop — seats sell throughput, brands sell margin, ambience sells reputation.
        public int MaxAmbienceTier { get; } = 3;
        public int GlasswarePrice(int tier) => 50 * tier;   // tier = current level; the next step costs this
        public int CounterPrice(int tier) => 40 * tier;
        public int WallPrice(int tier) => 40 * tier;
        public int MusicianPrice { get; } = 90;

        /// <summary>Satisfaction added to every served visit by the bar's look (capped).</summary>
        public double AmbienceBonus(int glasswareTier, int counterTier, int wallTier, bool musician) =>
            Math.Min(0.15,
                0.03 * ((glasswareTier - 1) + (counterTier - 1) + (wallTier - 1))
                + (musician ? 0.06 : 0.0));

        /// <summary>Seconds between arrivals, before jitter. Busier as days pass.</summary>
        public double ArrivalGap(int day) => Math.Max(6.0, 12.0 - 0.5 * day);
        public const double ArrivalJitter = 0.30;

        // ── patience (GDD 23 §2, balance v1) ────────────────────────────────────
        public double PatienceSeconds(int day) => Math.Max(22.0, 50.0 - 2.5 * day);
        public const double PatienceJitter = 0.20;

        /// <summary>One patience roll, jittered from the named stream.</summary>
        public double RollPatience(int day, SeededRng rng) =>
            PatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        // ── deciding & savouring (GDD 23 §2, 2026-07-23) ────────────────────────
        /// <summary>Seconds a freshly seated customer mulls the menu before ordering. Zero
        /// disables the beat entirely (the headless economy tests order the instant they sit).</summary>
        public double OrderDecisionSeconds { get; }
        public const double OrderDecisionJitter = 0.35;

        /// <summary>One decision-delay roll, jittered from the named stream.</summary>
        public double RollDecideDelay(SeededRng rng) =>
            OrderDecisionSeconds <= 0 ? 0.0
                : OrderDecisionSeconds * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * OrderDecisionJitter);

        /// <summary>Seconds a served customer nurses the drink on the stool before getting up
        /// to leave. The seat stays taken meanwhile; zero leaves on the next tick (the sim).</summary>
        public double SavorSeconds { get; }

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
