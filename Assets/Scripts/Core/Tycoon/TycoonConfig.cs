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

        /// <summary>The single glass, for a bar with no glass set (v5 P14). The glassware
        /// capacities are scaled against this, so a highball is 1.0 and the rest read off it.</summary>
        public double GlassCapacity { get; } = 1.0;

        /// <summary>The glass a drink lands in when its recipe names none (v5 P14 / C9).</summary>
        public string DefaultGlassId { get; } = "highball";

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
        // Glassware and the counter are prestige, not throughput or margin: each lifts every
        // visit's satisfaction a little, which draws a richer crowd tomorrow (§7). That is the
        // third leg of the compounding loop — seats sell throughput, brands sell margin,
        // ambience sells reputation. The back wall and the live musician were deleted on
        // 2026-08-04 (the author): both were prices attached to a tint and a placeholder
        // silhouette, so what they actually sold was neither reputation nor a picture.
        public int MaxAmbienceTier { get; } = 3;

        /// <summary>
        /// How many FITTINGS one night may buy (the author, 2026-08-07): a stool, a step up
        /// a glass line, a better counter. One. A bar is rebuilt over weeks, and a night
        /// where the whole shop is bought at once is a night with no decision in it. Stock
        /// — bottles, recipes, restocking — is not a fitting and is not capped.
        /// </summary>
        public int MaxUpgradesPerNight { get; } = 1;
        public int GlasswarePrice(int tier) => 50 * tier;   // tier = current level; the next step costs this
        public int CounterPrice(int tier) => 40 * tier;

        // The old AmbienceBonus(glassware, counter, wall, musician) went with them. It had been
        // dead for a while regardless — TycoonRun.Ambience is what the judge reads, and has
        // been since the glassware became per-line ladders.

        /// <summary>Seconds between arrivals, before jitter. Busier as days pass, and busier
        /// again for a well-reviewed bar (v5 P12): the standing bends the gap by up to a
        /// quarter either way, and a neutral three stars leaves it exactly as it was.</summary>
        public double ArrivalGap(int day, double stars = BarRating.NeutralStars) =>
            Math.Max(6.0, 12.0 - 0.5 * day) * BarRating.ArrivalRateFactor(stars);
        public const double ArrivalJitter = 0.30;

        // ── the night (v5 P12, GDD 23 §6) ───────────────────────────────────────
        /// <summary>
        /// How long a shift runs, in seconds of bar time. The night is **open**: there is no
        /// quota of customers any more (C4). People keep arriving until closing, and how many
        /// of them get through the door is set by how fast the stools empty — which is to say,
        /// by how fast the player works. That was already the machinery (a full bar makes the
        /// next arrival wait at the door); the quota was what hid it.
        ///
        /// Set so the OPEN night lands on the curve the quota used to draw, rather than above
        /// it: at 95s a day-1 shift (11.5s gaps) admits about eight, and a day-12 shift (the
        /// 6s floor) about fifteen — against the old fixed 8 and its cap of 14. At 120s it was
        /// 17.5 a night and the floor bot banked $992 by day 30 against $87; removing a cap is
        /// meant to make throughput the player's business, not to hand it to them.
        /// </summary>
        public double NightSeconds { get; } = 95.0;

        /// <summary>The clock the night is shown on (GDD 23 §6): a shift from 18:00 to 02:00.
        /// Presentation only — the run's own time is <see cref="NightSeconds"/>.</summary>
        public const int OpeningHour = 18, ClosingHour = 26;   // 26 = 02:00 next day

        /// <summary>
        /// How many people can already be waiting on a drink before the next one through the
        /// door takes one look and keeps walking (v5 P12). Without it an open night hands a
        /// struggling bar an unbounded queue of people to disappoint: the door admitted anyone
        /// the instant a stool freed, however far behind the bar was, and a third of the night
        /// stormed off. Real rooms balk. It also tightens the loop the notes actually asked
        /// for — serve faster, fewer people waiting, more of them willing to sit.
        /// </summary>
        public int BalkAtWaiting { get; } = 3;

        // ── patience (GDD 23 §2, balance v1) ────────────────────────────────────
        public double PatienceSeconds(int day) => Math.Max(22.0, 50.0 - 2.5 * day);
        public const double PatienceJitter = 0.20;

        /// <summary>One patience roll, jittered from the named stream.</summary>
        public double RollPatience(int day, SeededRng rng) =>
            PatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        /// <summary>
        /// Seconds a customer will sit with their order ready before giving up on being
        /// ASKED for it (the author, 2026-08-02: ordering and waiting are two different
        /// waits). Deliberately a different, shorter curve than
        /// <see cref="PatienceSeconds"/>: being ignored while you are trying to order is
        /// a sharper insult than waiting on a drink somebody is visibly making, and one
        /// clock reused for both would have made the split invisible.
        /// </summary>
        public double OrderPatienceSeconds(int day) => Math.Max(14.0, 30.0 - 1.6 * day);

        /// <summary>One order-patience roll, jittered from the named stream.</summary>
        public double RollOrderPatience(int day, SeededRng rng) =>
            OrderPatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        // ── deciding & savouring (GDD 23 §2, 2026-07-23) ────────────────────────
        /// <summary>Seconds a freshly seated customer mulls the menu before ordering. Zero
        /// disables the beat entirely (the headless economy tests order the instant they sit).</summary>
        public double OrderDecisionSeconds { get; }
        /// <summary>Widened 0.35 → 0.55 (v5 P11): the notes ask for people who decide in two
        /// seconds and people who take five or more. At ±35% every customer mulled for much
        /// the same beat; at ±55% the range is ~1.8–6.2s and the floor stops feeling metered.</summary>
        public const double OrderDecisionJitter = 0.55;

        /// <summary>One decision-delay roll, jittered from the named stream.</summary>
        public double RollDecideDelay(SeededRng rng) =>
            OrderDecisionSeconds <= 0 ? 0.0
                : OrderDecisionSeconds * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * OrderDecisionJitter);

        /// <summary>Seconds a served customer nurses the drink on the stool before getting up
        /// to leave. The seat stays taken meanwhile; zero leaves on the next tick (the sim).</summary>
        public double SavorSeconds { get; }

        // ── the day (GDD 23 §6) ─────────────────────────────────────────────────

        /// <summary>
        /// Balance v2 (P18, 2026-07-31): the late game squeezes a bar that never grows. The v1
        /// line (<c>14 + 4.5×day</c>) produced red days that climbed — 16.5% of runs in the red
        /// by day 15 — and **zero** bankruptcies in 200, because the till banked in the easy
        /// early days absorbed every one of them: the threat existed and never landed. Steeper
        /// LINEAR rent was already tried and recorded above as a cliff (1.5%→43.5%), because it
        /// squeezes day 3 exactly as hard as day 25. The quadratic term instead leaves the first
        /// ten days a shade *gentler* than v1 (day 5: $26 vs $36) and outruns a flat income late
        /// (day 30: $239 against the floor bot's ~$133/night take) — so the pressure is aimed
        /// where the sim showed the slack, and the answer to it is the tycoon's own verb: grow.
        /// The floor bot cannot grow, so its late collapse is the point, not a bug: its
        /// bankruptcy share is the measure that this threat finally lands.
        /// </summary>
        /// <remarks>Two earlier tunings left stale numbers here (audit 2026-08-11) — the
        /// 5.5-divisor and the "d²/10 soft pass" both predate the shipped curve. What SHIPS
        /// is `12 + 2d + d²/9` with integer division: **$124 on day 24, $172 on day 30.**
        /// The history stands in git; this remark carries only the live measurement.</remarks>
        public int Rent(int day) => 12 + 2 * day + (day * day) / 9;

        // ── orders (GDD 23 §3) ──────────────────────────────────────────────────
        /// <summary>The order roll pool: this many lowest-rank pourable recipes.</summary>
        public int OrderPoolSize(int day) => 3 + day;

        /// <summary>Premium stock lifts the menu price (2026-07-23): each tier a drink's spirit
        /// is above the base adds this much to its price. Start cheap, pour top-shelf, charge
        /// top-shelf. Zero for a bar that never upgrades — the sim bot's floor is unchanged.</summary>
        public int StockPremiumPerTier { get; } = 2;
    }
}
