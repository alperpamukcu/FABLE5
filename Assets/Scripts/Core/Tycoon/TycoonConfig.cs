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
        // The live game's tuning: a served customer nurses the drink through THREE sip cycles
        // before getting up to leave — 3 × TycoonHud.DrinkCycleSeconds (4.4), which is the
        // glass going up, the swallow, the glass coming down, and a moment standing with it
        // at their side.
        //
        // The number is unchanged and was right by accident: the cycle it was written against
        // (2026-07-23) claimed 2.6s of clip, while the clip actually ran 1.42s, so 13.2 was
        // buying 4.1 cycles and a customer stood up mid-gesture, glass halfway to their mouth.
        // The sip is drawn out to a real swallow now (2026-08-20) and the cycle is a fixed
        // 4.4s whatever length a character's clip shipped at, so this is three whole sips for
        // everybody. CHANGE ONE AND THE OTHER: a savour that is not a whole number of cycles
        // cuts the last one off wherever it happens to be.
        public static readonly TycoonConfig Default = new TycoonConfig(savorSeconds: 13.2);

        /// <summary>The scene's config: <see cref="Default"/> with the counter's MARKS off
        /// until the cloth is drawn (PLAN_house_and_law H4) — see
        /// <see cref="CounterSmudges"/>. The glasses are on: the scene already draws and
        /// collects them.</summary>
        public static readonly TycoonConfig ForTheScene =
            new TycoonConfig(savorSeconds: 13.2, counterSmudges: false);

        // orderDecisionSeconds 4.0 → 5.0 (2026-08-19, the author: "düşünme süresi biraz daha
        // uzun sürsün"): the "..." beat over the head is the thing the player waits ON now,
        // and at 4s the short rolls were over before the dots read as thinking.
        public TycoonConfig(int startingMoney = 20,
            double orderDecisionSeconds = 5.0, double savorSeconds = 6.0,
            bool counterSmudges = true)
        {
            if (orderDecisionSeconds < 0) throw new ArgumentOutOfRangeException(nameof(orderDecisionSeconds));
            if (savorSeconds < 0) throw new ArgumentOutOfRangeException(nameof(savorSeconds));
            StartingMoney = startingMoney;
            OrderDecisionSeconds = orderDecisionSeconds;
            SavorSeconds = savorSeconds;
            CounterSmudges = counterSmudges;
        }

        /// <summary>
        /// Whether a served leaver marks the counter as well as leaving their glass (GDD 27
        /// §4.1). The RULE is always on — the sim and every test measure the whole of it —
        /// and this exists for one caller: the scene, which switches the marks on the night
        /// it can draw them and hand the player the cloth (PLAN_house_and_law H4). A filed
        /// comfort the player cannot see or clean would be the invisible clamp GDD 27 was
        /// written to remove; until the cloth is drawn the scene's counter costs only what
        /// it shows — the glasses.
        /// </summary>
        public bool CounterSmudges { get; }

        /// <summary>
        /// Floor-time multiplier while a menu (service flow, licence) is open (GDD 24 §10):
        /// building a drink must not cost a storm-off by itself, but the clock never fully
        /// stops — haste still matters.
        /// </summary>
        public const double MenuTimeScale = 0.3;

        /// <summary>
        /// Floor-time multiplier while the recipe BOOK is open (2026-08-24, the author:
        /// "Menü açıkken zaman çok yavaş geçmeli"). Reading the book is homework, not
        /// service — it slows the room far below the service menus, but the clock still
        /// never fully stops: the night keeps its one-way arrow.
        /// </summary>
        public const double BookTimeScale = 0.05;

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

        /// <summary>
        /// The WHOLE wait, in seconds: from the moment they have made up their mind to the
        /// moment the drink lands. It covers being kept waiting to be ASKED as well — those
        /// were two clocks between 2026-08-02 and 2026-09-04, and taking the order started
        /// the second one from full, which meant a bar that got to a stool quickly paid for
        /// none of the wait it had already spent. One clock now, and reading the card pays a
        /// third of it back (<see cref="CustomerVisit.OrderTakenPatienceBonus"/>).
        ///
        /// The number is deliberately unchanged through that rewrite. Measured over 200
        /// seeded runs of a busy one-stool-at-a-time bot, the merge moves storm-offs from
        /// 28.4% to 7.4% and the average serve from 8% of the wait spent to 35% — which is
        /// the point: the three bands were decorative before (14 red serves in 54,000, because
        /// the gauge refilled at the order) and the tip's clock now has something to say.
        /// A shorter curve would claw the pressure back; that is a separate design call and
        /// wants its own measurement, not a silent ride-along on this one.
        /// </summary>
        public double PatienceSeconds(int day) => Math.Max(22.0, 50.0 - 2.5 * day);
        public const double PatienceJitter = 0.20;

        /// <summary>One patience roll, jittered from the named stream.</summary>
        public double RollPatience(int day, SeededRng rng) =>
            PatienceSeconds(day) * (1.0 + (rng.NextDouble() * 2.0 - 1.0) * PatienceJitter);

        // ── deciding & savouring (GDD 23 §2, 2026-07-23) ────────────────────────
        /// <summary>Seconds a freshly seated customer mulls the menu before ordering. Zero
        /// disables the beat entirely (the headless economy tests order the instant they sit).</summary>
        public double OrderDecisionSeconds { get; }
        /// <summary>Widened 0.35 → 0.55 (v5 P11): the notes ask for people who decide in two
        /// seconds and people who take five or more. At ±35% every customer mulled for much
        /// the same beat; at ±55% the range stops feeling metered (~2.3–7.8s at the live 5s
        /// base since 2026-08-19).</summary>
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
        /// <summary>
        /// THE DAY THE RENT STOPS CLIMBING (2026-08-14, the author: "kasıtlı değil fiyatları
        /// ekonomik dengeyi buna göre ayarla").
        ///
        /// The quadratic above was aimed at a bar that never grows, and it hit one that
        /// cannot. Measured over 120 nights: the takings climb to about $176 by night 21 and
        /// STOP — six stools is the cap, the menu finishes, and a better bottle is a purchase
        /// the margin never reaches — while rent keeps compounding. Two curves of different
        /// orders cross exactly once, on night 31, and every long run died at a median day 36.
        /// The date was arithmetic, not difficulty.
        ///
        /// So the ramp keeps its measured early shape and then levels off where the bar's own
        /// ceiling is. What that gives up is late pressure FROM RENT, deliberately: the
        /// author's answer to the late game is more to spend money ON, not less to spend it
        /// with. When those sinks land, the pressure comes back as ambition rather than as a
        /// landlord who outgrows the room he is renting.
        /// </summary>
        public int RentPlateauDay { get; } = 21;

        public int Rent(int day)
        {
            int d = Math.Min(Math.Max(day, 0), RentPlateauDay);
            return 12 + 2 * d + (d * d) / 9;
        }

        // ── orders (GDD 23 §3) ──────────────────────────────────────────────────
        /// <summary>The order roll pool: this many lowest-rank pourable recipes.</summary>
        public int OrderPoolSize(int day) => 3 + day;

        /// <summary>Premium stock lifts the menu price (2026-07-23): each tier a drink's spirit
        /// is above the base adds this much to its price. Start cheap, pour top-shelf, charge
        /// top-shelf. Zero for a bar that never upgrades — the sim bot's floor is unchanged.</summary>
        public int StockPremiumPerTier { get; } = 2;
    }
}
