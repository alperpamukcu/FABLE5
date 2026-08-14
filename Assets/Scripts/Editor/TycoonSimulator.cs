using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Batch-plays seeded tycoon runs (PLAN_tycoon_pivot P3 gate) and writes
    /// Docs/tycoon_sim_report.md. The bot reads only what a player could: the order on the
    /// seat, its recipe bands, and the always-visible half of the read. It builds each
    /// ordered drink at band midpoints with intent-aligned bottles, takes nine seconds of
    /// bar-time per drink, restocks at day end, and buys stools when flush. A floor, not
    /// a prediction.
    ///
    /// ~~IT TRIES TO BUY BETTER BOTTLES AND CANNOT AFFORD ONE.~~ **That was the instrument,
    /// not the bar** (2026-08-14, found by the lineup pass). The count was zero across two
    /// hundred runs at every cushion tried, and the money was never the reason: `PlayRun`
    /// built its run WITHOUT a brand catalogue, so the better bottles were never handed to
    /// the market and the offer never existed to be afforded. It also opened with every
    /// unlocked tier-1 bottle already on the shelf — a richer bar than the game's own six.
    ///
    /// Both are fixed: the bot opens with GameBootstrap's six and shops the same catalogue
    /// the player does. The first report after it buys **999** upgrades. Every measurement
    /// taken before this — the star track's plateau included — was of a bar that could not
    /// pour anything well, and should be read again.
    ///
    /// ~~AND THEN IT BOUGHT THE GOOD BOTTLES AND NEVER POURED THEM.~~ Same day, same shape
    /// of mistake: `PickByStyle` reached for the FULLEST bottle of a style and never read
    /// the band's `MinTier`, so the reserve gin it had just bought sat on the shelf while
    /// the well gin went into the Gimlet. 7.3% of every serve missed its band with perfectly
    /// steady hands, while the day-end counter insisted the shelf could answer every tier
    /// its menu demanded — both true, and nothing in between reached for the bottle. Fixed
    /// by picking the fullest bottle that is GOOD ENOUGH for the band; the floor goes back
    /// to 100% Exact, its standing from 2.60 to 2.85 stars, and the thirty-night table from
    /// **none of 200** reaching three stars to **102 of 200**.
    /// </summary>
    public static class TycoonSimulator
    {
        private const int DayCap = 30;                 // the endless game needs a horizon to report on
        private const double DrinkBuildSeconds = 9.0;  // roughly a competent player, not a machine

        [MenuItem("LastCall/Simulate Tycoon 200 Runs")]
        public static void Simulate200() => Simulate(200);

        /// <summary>
        /// The v5 P12 gate: an open night must pay a faster bar in customers. Runs the same
        /// seeds at three service speeds and prints what each got through the door — if the
        /// three come out level, the night is not open, it is just quiet.
        /// </summary>
        [MenuItem("LastCall/Measure Service Speed Response")]
        public static void MeasureSpeedResponse()
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));

            var sb = new StringBuilder();
            sb.AppendLine("Service speed -> customers served (40 runs, 10-day horizon)");
            double baseline = 0;
            foreach (double seconds in new[] { 5.0, 9.0, 15.0 })
            {
                var stats = new Aggregate();
                for (int i = 0; i < 40; i++)
                    PlayRun($"SPD-{i:0000}", deck, recipes, archetypes, stats, seconds, 10);
                double perNight = (double)stats.CustomersFinished / Math.Max(1, stats.NightsClosed);
                if (baseline == 0) baseline = perNight;
                sb.AppendLine($"  {seconds,5:0.0}s per drink : {perNight,5:0.0} served/night" +
                              $"  ({perNight / baseline:P0} of the fastest)" +
                              $"  storm-offs {100.0 * stats.StormOffs / Math.Max(1, stats.CustomersFinished):0.0}%");
            }
            Debug.Log(sb.ToString());
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs",
                "tycoon_speed_response.md"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[TycoonSim] wrote {path}");
        }

        /// <summary>
        /// WHERE THE STAR TRACK ACTUALLY ENDS (GDD 26 §12.2 step 3). The 200-run report
        /// answers this question too, and its answer is cut off: the horizon is thirty days,
        /// so "no run reached 3.5" there means "not in the first five weeks", which is a very
        /// different sentence and the design cannot be built on the wrong one.
        ///
        /// This asks the long version — a quarter of the runs over four times the nights —
        /// because the only thing being read is where the climb PLATEAUS. Fewer runs is fine
        /// for a median; a truncated curve is not fine for anything.
        /// </summary>
        [MenuItem("LastCall/Measure the Star Track")]
        public static void MeasureStarTrack()
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));
            var glassware = DataLoader.ParseGlassware(Read("glassware/glassware.json"));
            var snacks = DataLoader.ParseSnacks(Read("snacks/snacks.json"));
            var cast = DataLoader.ParsePapers(Read("customers/papers.json"));
            var story = DataLoader.ParseStory(Read("story/story.json"), cast, recipes);

            const int Runs = 60, Horizon = 120;
            var stats = new Aggregate();
            for (int i = 0; i < Runs; i++)
                PlayRun($"STAR-{i:0000}", deck, recipes, archetypes, stats,
                    DrinkBuildSeconds, Horizon, glassware, snacks, story);

            var sb = new StringBuilder();
            sb.AppendLine("# The star track — how far a bar climbs, and how fast");
            sb.AppendLine();
            sb.AppendLine($"{Runs} runs, horizon **{Horizon} days** (twenty weeks). The 200-run");
            sb.AppendLine("report answers the same question over thirty days and is cut off by it.");
            sb.AppendLine();
            sb.AppendLine("**This is still a floor.** The bot reads only the ID and plays no better");
            sb.AppendLine("than band midpoints — but since 2026-08-14 it DOES shop: stock its menu");
            sb.AppendLine("names, the cheapest passing recipe, stools, glass steps, and one brand");
            sb.AppendLine("UPGRADE a night behind a fat cushion. What the table is trusted for is the");
            sb.AppendLine("SHAPE: the spacing between rungs, and where the curve stops moving. A rung");
            sb.AppendLine("nobody reaches in twenty weeks is a rung no guest can be written for.");
            sb.AppendLine();
            sb.AppendLine("| Rung | Runs that reached it | Day p25/median/p75 | Median week |");
            sb.AppendLine("|---|---|---|---|");
            for (int i = 0; i < Aggregate.Rungs; i++)
            {
                var days = stats.ReachedOn[i];
                if (days.Count == 0)
                {
                    sb.AppendLine($"| {Aggregate.RungStars(i):0.0}★ | **none of {Runs}** | — | — |");
                    continue;
                }
                int med = Aggregate.Quantile(days, 0.5);
                sb.AppendLine($"| {Aggregate.RungStars(i):0.0}★ | " +
                              $"{days.Count} ({100.0 * days.Count / Runs:0.0}%) | " +
                              $"{Aggregate.Quantile(days, 0.25)} / {med} / {Aggregate.Quantile(days, 0.75)} | " +
                              $"{BarCalendar.WeekOf(Math.Max(1, med))} |");
            }
            sb.AppendLine();
            int lived = Aggregate.Quantile(stats.DaysSurvived, 0.5);
            sb.AppendLine($"Nights closed: {stats.NightsClosed}. Bankruptcies: {stats.Bankruptcies} of {Runs}. " +
                          $"Days survived p25/median/p75: {Aggregate.Quantile(stats.DaysSurvived, 0.25)} / " +
                          $"{lived} / {Aggregate.Quantile(stats.DaysSurvived, 0.75)}.");
            sb.AppendLine($"Standing across every night: {stats.StarsSum / Math.Max(1, stats.NightsClosed):0.00} stars.");
            sb.AppendLine();

            // READ THE TWO NUMBERS TOGETHER OR NOT AT ALL. A rung nobody reaches means one of
            // two completely different things, and the table cannot tell them apart on its
            // own: the climb flattened, or the bar died first. When the median run is dead at
            // about the day the last reached rung lands on, the top of this table is measuring
            // the BOT'S WALLET and not the game's ceiling — and a threshold chosen off it
            // would be chosen off the wrong curve.
            int lastReached = -1;
            for (int i = Aggregate.Rungs - 1; i >= 0; i--)
                if (stats.ReachedOn[i].Count > 0) { lastReached = i; break; }
            int lastDay = lastReached >= 0 ? Aggregate.Quantile(stats.ReachedOn[lastReached], 0.5) : 0;
            if (stats.Bankruptcies > Runs / 2 && lastDay > lived - 14)
                sb.AppendLine($"> **The top of this table is not an answer.** The median run is dead by day " +
                              $"{lived}, and the highest rung anyone reaches ({Aggregate.RungStars(lastReached):0.0}★) " +
                              $"lands on day {lastDay}. Above that the bot ran out of MONEY, not out of stars: " +
                              $"it never shops, so it cannot buy the bottles a better night is made of. Those " +
                              $"rungs are UNMEASURED, not unreachable, and nothing should be written for them " +
                              $"until a bot that shops has been down this road.");

            // ── the crossing ─────────────────────────────────────────────────
            // Rent is `12 + 2d + d²/9` — quadratic — and a bar's takings are not: they are
            // capped by the room, the clock and the price list. Two curves of different
            // orders cross exactly once, and the day they cross is the day the game ends.
            // The averages in the balance report hide it completely: "$150 in, $145 out"
            // over thirty days is a bar making money in week one and losing it in week five.
            sb.AppendLine();
            sb.AppendLine("## Where the money goes, five nights at a time");
            sb.AppendLine();
            sb.AppendLine("Rent against what the room can take. If rent outgrows the takings the");
            sb.AppendLine("crossing is the end of the run, and the date is arithmetic, not difficulty.");
            sb.AppendLine();
            sb.AppendLine("| Nights | Avg take | Avg rent | Avg stock | Net |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int band = 0; band < 12; band++)
            {
                int lo = band * 5 + 1, hi = lo + 4;
                long take = 0, rent = 0, stock = 0, nights = 0;
                for (int d = lo; d <= hi; d++)
                {
                    if (!stats.ByNight.TryGetValue(d, out var n)) continue;
                    take += n.income; rent += n.rent; stock += n.stock; nights += n.nights;
                }
                if (nights == 0) continue;
                double t = (double)take / nights, r = (double)rent / nights, k = (double)stock / nights;
                sb.AppendLine($"| {lo}–{hi} | ${t:0} | ${r:0} | ${k:0} | " +
                              (t - r - k >= 0 ? $"**+${t - r - k:0}**" : $"**−${r + k - t:0}**") + " |");
            }

            // THE CEILING, SOLVED. The takings flatten because the room does: so many stools,
            // so many minutes, so long a price list. Rent does not flatten. Printing the best
            // night the bar ever has, and the night rent passes it, turns the table above from
            // a shape into a date — and a date is the thing a design can argue with.
            double bestTake = 0; int bestBand = 0;
            for (int d = 1; d <= Horizon; d++)
                if (stats.ByNight.TryGetValue(d, out var n) && n.nights > 0)
                {
                    double per = (double)n.income / n.nights;
                    if (per > bestTake) { bestTake = per; bestBand = d; }
                }
            // ASK THE RULE, DO NOT RESTATE IT. This paragraph carried the rent formula as a
            // copy and went on quoting `12 + 2d + d²/9` after the shipped curve had grown a
            // plateau — announcing a crossing that no longer happens. It reads the config now,
            // so a rent change cannot leave a confident wrong sentence behind it.
            var rentRule = new TycoonConfig();
            int crosses = 0;
            for (int d = 1; d <= 999; d++)
                if (rentRule.Rent(d) >= bestTake) { crosses = d; break; }
            sb.AppendLine();
            sb.AppendLine($"The best night this bar ever has is **${bestTake:0}** (night {bestBand}); " +
                          $"rent on night {Horizon} is **${rentRule.Rent(Horizon)}**.");
            if (crosses > 0)
                sb.AppendLine($"Rent passes the best night on **night {crosses}** — everything after it " +
                              "is a countdown, and the run ends on a date rather than on a decision.");
            else
                sb.AppendLine("**Rent never passes it.** The bar can out-earn its landlord for as long " +
                              "as it keeps growing, so what ends a run is a choice or a mistake — not " +
                              "the calendar. Late pressure has to come from something to spend money " +
                              "ON; there is nothing here that takes it away.");

            Debug.Log(sb.ToString());
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs",
                "star_track_report.md"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[TycoonSim] wrote {path}");
        }

        /// <summary>Pins the editor's number culture too. The report below is a checked-in
        /// document; without this it reads "$133,2" on one desktop and "$133.2" on another,
        /// and every regeneration shows up as a diff nobody made.</summary>
        [InitializeOnLoadMethod]
        private static void PinCulture() => RunCulture.Pin();

        /// <summary>
        /// HOW STEADY THE BARTENDER'S HANDS ARE (2026-08-14). The floor bot pours the book's
        /// exact ideal, garnishes everything, works every tin and hits the rim dead on — so
        /// 200 runs came back **100% Exact, 0% Close, 0% Wrong, 0% Refused**, and the whole
        /// half of this economy that pays for getting it nearly right has never been measured
        /// once. The star track was calibrated against a bartender who does not exist.
        ///
        /// (Later the same day the ideal bot stopped scoring 100%: the recipe/bottle ladder
        /// gave sixteen recipes a MinTier band, and a bar pouring the bottle it can afford
        /// misses those however steady its hands are. Which is the point of measuring — the
        /// column moved for a reason that had nothing to do with aim.)
        ///
        /// Every field is a SIGMA or a CHANCE, and all-zero is the floor bot exactly as it
        /// was — so the shipped report does not move by a cent when this lands.
        /// </summary>
        public sealed class Hands
        {
            /// <summary>Error on each pour's share of the glass, as a fraction of that share.</summary>
            public double RatioSigma;
            /// <summary>Error on how full the glass is aimed for.</summary>
            public double FillSigma;
            /// <summary>How far off the rim the tin goes at the pour-out. Spills.</summary>
            public double AimSigma;
            /// <summary>Chance of forgetting a garnish the licence asked for.</summary>
            public double SkipSpec;
            /// <summary>Chance of not working the tin — only where Core does not refuse it.</summary>
            public double ForgetMix;

            /// <summary>Its own stream. Set by PlayRun; never shared with the run's.</summary>
            public SeededRng Dice;

            /// <summary>The floor bot: no error anywhere.</summary>
            public static Hands Steady => new Hands();

            // A FIXED HAND OF DICE PER DRINK, IN A FIXED ORDER. The obvious way — draw when
            // you need a number — desynchronises the sweep: a recipe with three bands draws
            // three times and one with two draws twice, a plain order skips the garnish
            // draws, and a sigma of zero would draw nothing at all. Two noise levels would
            // then be pouring for different customers by the second night, and the table
            // would be reading that shuffle instead of the noise.
            //
            // So every drink deals the same twelve numbers whatever it turns out to need,
            // and each knob reads its own slot. Unused slots are thrown away, which is the
            // price of the sweep points staying on the same night as each other.
            private const int Slots = 12;
            private readonly double[] _hand = new double[Slots];

            public void Deal()
            {
                for (int i = 0; i < Slots; i++) _hand[i] = Dice?.NextDouble() ?? 0.5;
            }

            /// <summary>Symmetric error in [-sigma, +sigma]. Uniform rather than gaussian on
            /// purpose: a bounded mistake is what a hand makes, and an unbounded one would
            /// put a long tail of nonsense drinks into a table meant to read as a slope.</summary>
            public double Jitter(double sigma, int slot) =>
                sigma <= 0 ? 0 : (_hand[slot % Slots] * 2.0 - 1.0) * sigma;

            public bool Slips(double chance, int slot) =>
                chance > 0 && _hand[slot % Slots] < chance;

            public bool IsSteady =>
                RatioSigma <= 0 && FillSigma <= 0 && AimSigma <= 0 && SkipSpec <= 0 && ForgetMix <= 0;
        }

        /// <summary>
        /// WHAT GETTING IT NEARLY RIGHT IS WORTH (2026-08-14). The 200-run report used to
        /// come back 100% Exact / 0% Close / 0% Wrong / 0% Refused, because the floor bot
        /// pours the book's ideal to the millilitre. Half this economy — everything that pays
        /// for craft — had therefore never been measured once, and the star track was
        /// calibrated against a bartender who does not exist.
        ///
        /// This table is also where the middle grade was found missing and then found again:
        /// it read 0% Close at every steadiness because <c>ServiceJudge.Compare</c> could not
        /// produce that grade at all, and it is what measured the rewrite.
        ///
        /// Same seeds, same nights, five steadinesses of hand. Its own file, and the
        /// configuration is printed into it, because a noisy run committed over
        /// tycoon_sim_report.md would be indistinguishable in git from a balance change.
        /// </summary>
        [MenuItem("LastCall/Measure Imperfect Hands")]
        public static void MeasureImperfectHands()
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));
            var glassware = DataLoader.ParseGlassware(Read("glassware/glassware.json"));
            var snacks = DataLoader.ParseSnacks(Read("snacks/snacks.json"));

            const int Runs = 80;
            var levels = new (string Name, Hands H)[]
            {
                ("steady (the shipped floor)", Hands.Steady),
                ("a good night",   new Hands { RatioSigma = 0.04, FillSigma = 0.04, AimSigma = 0.03, SkipSpec = 0.03, ForgetMix = 0.02 }),
                ("an ordinary hand", new Hands { RatioSigma = 0.10, FillSigma = 0.08, AimSigma = 0.07, SkipSpec = 0.08, ForgetMix = 0.05 }),
                ("busy and rushed", new Hands { RatioSigma = 0.18, FillSigma = 0.14, AimSigma = 0.12, SkipSpec = 0.15, ForgetMix = 0.10 }),
                ("all thumbs",     new Hands { RatioSigma = 0.30, FillSigma = 0.22, AimSigma = 0.20, SkipSpec = 0.25, ForgetMix = 0.20 }),
            };

            var sb = new StringBuilder();
            sb.AppendLine("# What getting it nearly right is worth");
            sb.AppendLine();
            sb.AppendLine($"{Runs} runs a level, {DayCap}-day horizon, the SAME seeds at every level.");
            sb.AppendLine();
            sb.AppendLine("The bot's dice come off the run's own `RunRng` under a stream named");
            sb.AppendLine("`hands`, which is seeded independently of arrivals/orders/patience — so a");
            sb.AppendLine("shaky night is the same NIGHT as a steady one, with the same crowd wanting");
            sb.AppendLine("the same drinks. Every drink deals a fixed twelve dice in a fixed order,");
            sb.AppendLine("whatever it needs, so the levels cannot drift apart from each other.");
            sb.AppendLine();
            sb.AppendLine("**Read the per-serve columns, not the money.** Tips are rounded to whole");
            sb.AppendLine("dollars on $4–8 drinks and are paid at ALL only when the crowd is above");
            sb.AppendLine("Broke, so a money column is partly a census of which nights paid anything.");
            sb.AppendLine();
            sb.AppendLine("| Hands | ratio σ | Exact | Close | Wrong | Refused | spec | fill | tip/serve | served/night | stars |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (name, h) in levels)
            {
                var stats = new Aggregate();
                for (int i = 0; i < Runs; i++)
                    PlayRun($"HAND-{i:0000}", deck, recipes, archetypes, stats,
                        DrinkBuildSeconds, DayCap, glassware, snacks, null, h);
                int serves = Math.Max(1, stats.Serves);
                sb.AppendLine($"| {name} | {h.RatioSigma:0.00} | " +
                              $"{100.0 * stats.Exact / serves:0.0}% | " +
                              $"{100.0 * stats.Close / serves:0.0}% | " +
                              $"{100.0 * stats.Wrong / serves:0.0}% | " +
                              $"{100.0 * stats.Refused / serves:0.0}% | " +
                              $"{stats.SpecScoreSum / serves:P0} | " +
                              $"{stats.FillScoreSum / serves:P0} | " +
                              $"${stats.TipSum / serves:0.00} | " +
                              $"{(double)stats.CustomersFinished / Math.Max(1, stats.NightsClosed):0.0} | " +
                              $"{stats.StarsSum / Math.Max(1, stats.NightsClosed):0.00} |");
            }
            sb.AppendLine();
            sb.AppendLine("## The headline: the pour barely matters");
            sb.AppendLine();
            sb.AppendLine("Measured 2026-08-14. A bartender **eighteen percent off on every single");
            sb.AppendLine("measure** still gets 99.9% of drinks identified as exactly the right");
            sb.AppendLine("drink, loses about a dollar of tip a serve, and ends on the same stars as");
            sb.AppendLine("a machine. The bands are wide enough — typically a tenth of the glass");
            sb.AppendLine("either side of the ideal — that relative error has to reach roughly 30%");
            sb.AppendLine("before it crosses one, and only then does anything happen at all.");
            sb.AppendLine();
            sb.AppendLine("So the game's central interaction currently has almost no consequence,");
            sb.AppendLine("and what consequence it does have is nearly all in the GARNISH and the");
            sb.AppendLine("FILL, which degrade smoothly, rather than in the pour, which does not");
            sb.AppendLine("degrade at all until it falls off a cliff. That is a balance question and");
            sb.AppendLine("it is the author's: narrower bands would make aim worth something, and");
            sb.AppendLine("would also make every existing measurement in this folder harder.");
            sb.AppendLine();
            sb.AppendLine("## The middle grade exists now");
            sb.AppendLine();
            sb.AppendLine("The first run of this table (2026-08-14, before the rewrite) had no Close");
            sb.AppendLine("column, because `Compare` only returned Close when the ordered recipe had");
            sb.AppendLine("a dominant TYPE band and every banded recipe in `recipes.json` is");
            sb.AppendLine("style-banded — the grade could not be produced by the shipped game at all,");
            sb.AppendLine("so a pour that drifted out of its bands went straight to Wrong: paid at");
            sb.AppendLine("the menu price of whatever the glass happened to match against the bar's");
            sb.AppendLine("UNLOCKED menu, which for an early bar is usually nothing at all.");
            sb.AppendLine();
            sb.AppendLine("Close is now the ordered drink poured OUT OF TOLERANCE: everything the");
            sb.AppendLine("recipe names is in the glass, nothing much else is, and the shares");
            sb.AppendLine("missed. Same seeds, same bot, judge swapped underneath:");
            sb.AppendLine();
            sb.AppendLine("| | old judge | new judge |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| steady hands — Exact / stars | 100.0% / 2.84 | 100.0% / 2.84 |");
            sb.AppendLine("| all thumbs — Close | 0.0% | 7.9% |");
            sb.AppendLine("| all thumbs — Wrong | 9.0% | 1.2% |");
            sb.AppendLine("| all thumbs — stars | 2.58 | 2.78 |");
            sb.AppendLine();
            sb.AppendLine("A steady hand does not notice, which is the check that it changed nothing");
            sb.AppendLine("it should not: the grade only ever touches a serve that was not exact.");
            sb.AppendLine("Eight of the nine points that used to be total losses are now graded");
            sb.AppendLine("misses — paid for, tipped at half, and a quarter of a satisfaction point");
            sb.AppendLine("worse — and a clumsy bar's standing goes from 2.58 stars to 2.78. What is");
            sb.AppendLine("left in the Wrong column is what belongs there: drinks that left an");
            sb.AppendLine("ingredient out, or that are a third something the recipe never mentions.");
            sb.AppendLine();
            sb.AppendLine("## And the tier ladder was being measured by a bot that ignored it");
            sb.AppendLine();
            sb.AppendLine("Found on the way, and worth more than the rewrite that found it. The");
            sb.AppendLine("first table above ran at **92.7% Exact with perfectly steady hands** —");
            sb.AppendLine("7.3% of every serve in the game missing its band while the day-end");
            sb.AppendLine("counter insisted the shelf could answer every tier its menu demanded (0");
            sb.AppendLine("of 15,916). Both numbers were true. `PickByStyle` chose the FULLEST");
            sb.AppendLine("bottle of a style and never read the band's `MinTier`, so a bar that had");
            sb.AppendLine("bought the reserve gin poured its well gin into the recipe that asked for");
            sb.AppendLine("it. The instrument, not the bar — the third time that has been the answer");
            sb.AppendLine("in this file. With the bottle chosen the way a bartender would choose it,");
            sb.AppendLine("the floor is 100% Exact again and its standing goes from 2.60 to 2.84.");
            sb.AppendLine();
            sb.AppendLine("**And it is a cliff, not a slope.** A band either accepts a ratio or it");
            sb.AppendLine("does not, so small error does nothing at all until it crosses an edge and");
            sb.AppendLine("then costs everything. Fill error is one-sided for the same kind of");
            sb.AppendLine("reason: the glass cannot overflow, and the fill score only counts");
            sb.AppendLine("shortfalls, so pouring long is free and pouring short is not.");

            Debug.Log(sb.ToString());
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs",
                "imperfect_hands_report.md"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[TycoonSim] wrote {path}");
        }

        public static void Simulate(int runs)
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));

            // The glass set is part of the bar now (v5 P14): a pint holds more than a coupe,
            // so the vessel decides how much liquid a drink costs. Leaving it out here would
            // measure a bar nobody plays.
            var glassware = DataLoader.ParseGlassware(Read("glassware/glassware.json"));
            var snacks = DataLoader.ParseSnacks(Read("snacks/snacks.json"));

            // THE ARC IS BUILT ONCE AND PLAYED TWO HUNDRED TIMES (GDD 26 §9). It is content:
            // immutable, shared, and each run keeps its own StoryProgress through it — which
            // is exactly why those are two classes. The sim plays the story even while the
            // scene does not (PLAN S2a's storyInPlay), because balance cannot wait for a UI.
            var cast = DataLoader.ParsePapers(Read("customers/papers.json"));
            var story = DataLoader.ParseStory(Read("story/story.json"), cast, recipes);

            var stats = new Aggregate();
            for (int i = 0; i < runs; i++)
                PlayRun($"TYC-{i:0000}", deck, recipes, archetypes, stats,
                    DrinkBuildSeconds, DayCap, glassware, snacks, story);

            string report = stats.Report(runs);
            Debug.Log(report);
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "tycoon_sim_report.md"));
            File.WriteAllText(path, report);
            Debug.Log($"[TycoonSim] wrote {path}");
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static void PlayRun(string seed, LoadedDeck deck,
            IReadOnlyList<RecipeDefinition> recipes, IReadOnlyList<ArchetypeDefinition> archetypes,
            Aggregate stats, double buildSeconds = DrinkBuildSeconds, int dayCap = DayCap,
            IReadOnlyList<GlasswareDefinition> glassware = null,
            IReadOnlyList<SnackDefinition> snacks = null,
            StoryArc story = null,
            Hands handsIn = null)
        {
            stats.BeginRun();   // this bar has climbed nothing yet

            // THE BOT OPENS THE BAR THE PLAYER OPENS (2026-08-14, the lineup pass).
            //
            // It used to start with EVERY unlocked tier-1 bottle already on the shelf and no
            // brand catalogue at all — so it opened richer than the game does and could never
            // buy a better bottle, because the better bottles were never handed to the run.
            // That is why "brand upgrades bought" has read 0 in every report ever written,
            // and the header blamed a five-dollar margin: the money was never asked. It is
            // also why the star track came back saying the climb stops around three, on a bar
            // that was structurally incapable of pouring anything well.
            //
            // GameBootstrap's own six, and everything else offered across the counter.
            var startingIds = new HashSet<string>
            {
                "vodka_astra", "gin_boothby", "soda_klara", "lemon_fresh", "syrup_house",
                "beer_kestrel",
            };
            var starting = new List<IngredientCard>();
            var catalogue = new List<IngredientCard>();
            foreach (var card in deck.Cards)
            {
                if (startingIds.Contains(card.Id)) starting.Add(card.Clone());
                else catalogue.Add(card);
            }
            if (starting.Count == 0)   // the same data-drift guard the game keeps
                foreach (var card in deck.Cards)
                    if (card.Info == null || card.Info.Tier <= 1) starting.Add(card.Clone());
            var shelf = new Shelf(starting.Select(c => new ShelfBottle(c)));
            // The quarantined bottles ride along (P16): buying a recipe releases its styles
            // into the market, and without this the sim's bought menu was undrinkable — half
            // of every night silently declined for want of a tonic that existed nowhere.
            // THE BOT'S OWN DICE (2026-08-14). Held here rather than reached for inside
            // TycoonRun, and drawn from a stream NOBODY ELSE USES: RunRng seeds every named
            // stream independently off the seed hash, so adding "hands" cannot perturb
            // arrivals, orders, patience, customer, read or decide. A run with imperfect
            // hands is the same NIGHT as a run with perfect ones — same crowd, same orders,
            // same clock — which is the only way the two can be compared at all.
            var rng = new RunRng(seed);
            var run = new TycoonRun(shelf, recipes, rng,
                regulars: new RegularsRegistry(archetypes), brandCatalogue: catalogue,
                glassware: glassware, snacks: snacks,
                lockedStock: deck.LockedCards, story: story);
            var hands = handsIn ?? Hands.Steady;
            hands.Dice = rng.GetStream("hands");

            double buildTimer = buildSeconds;
            int guard = 0;
            int servedHere = 0;      // this run's own serves; see the snack cadence below
            while (run.Phase != TycoonPhase.Closed && run.Ledger.History.Count < dayCap)
            {
                if (guard++ > 300_000) { stats.Stuck++; return; }

                if (run.Phase == TycoonPhase.DayOpen)
                {
                    run.Tick(1.0);
                    // The bussing beat (D2): the bot clears empty glasses as part of its
                    // round, so the floor measures a bar that busses -- an ignored glass
                    // blocks its stool for BarDay.BusSeconds, and that cost belongs to
                    // inattention, not to the floor.
                    foreach (var g in run.Floor.Dirty)
                        if (!g.Cleared) { g.Bus(); stats.GlassesBussed++; }

                    // TAKING ORDERS is not building drinks (2026-08-02, the two-clock split).
                    // A customer with their mind made up is now on a clock of their own until
                    // somebody asks, and asking costs a glance — the player taps the card the
                    // moment it lights up, whatever else is on the bar. Reading it inside the
                    // build gate would have measured a bartender who refuses to look at the
                    // second customer until the first drink is poured, and blamed the walk-out
                    // on the patience curve.
                    foreach (var visit in run.Floor.Seated)
                        if (visit.State == VisitState.Waiting && visit.HasOrdered && !visit.IdInspected)
                            visit.InspectId();

                    buildTimer += 1.0;
                    if (buildTimer < buildSeconds) continue;

                    // THE LAST CUSTOMER GETS THE BAR TO THEMSELVES (GDD 26). They are the only
                    // one on the floor when they are there, and they are not served through
                    // the crowd's loop: no licence to read, no price to take, and a standard
                    // the ordinary build does not aim at. One drink per build tick, like
                    // everybody else, so the trial is measured against the same hands.
                    if (run.LastCustomer != null)
                    {
                        PourForTheLastCustomer(run, stats);
                        buildTimer = 0;
                        continue;
                    }

                    foreach (var visit in run.Floor.Seated)
                    {
                        if (visit.State != VisitState.Waiting) continue;
                        // A seated customer spends a few seconds deciding before they order
                        // (TycoonConfig.OrderDecisionSeconds). Serving one mid-thought throws,
                        // which is what the sim had been doing since decisions were added — it
                        // has evidently not been run since (2026-07-27).
                        if (!visit.HasOrdered) continue;
                        // The bot does what the fiction has always said it does: it reads the
                        // ID. Since v5 C3 that is also the only way Core will hand the order
                        // over — an uninspected visit refuses to name its drink.
                        visit.InspectId();
                        // An order the bar cannot make is DECLINED, not left to storm off
                        // (P11's honest verb): scored above a storm-off, and the stool frees
                        // now. A competent bartender says "we can't make that".
                        if (!run.CanMake(visit.Order))
                        {
                            run.DeclineOrder(visit);
                            stats.Declined++;
                            continue;
                        }
                        // Every third serve gets a bowl alongside (v5 P16): enough traffic to
                        // measure the snack share without pretending everyone eats. Cycling
                        // the bowls spreads the stock; a drained bowl just skips.
                        // COUNTED PER RUN, NOT ACROSS ALL OF THEM (2026-08-14). This read
                        // `stats.Serves` — the aggregate shared by every run in the batch — so
                        // the bot's snack cadence in run 200 depended on how many drinks runs
                        // 1..199 had poured. Runs were not independent samples, and any A/B
                        // between two bot configurations was comparing two different
                        // experiments. It is the run's own count now.
                        if (servedHere % 3 == 0 && run.Snacks.Count > 0)
                        {
                            var snack = run.Snacks[(servedHere / 3) % run.Snacks.Count];
                            if (run.SnackLeft(snack.Id) > 0)
                            {
                                run.ServeSnack(snack.Id, visit);
                                stats.SnackServes++;
                                stats.SnackIncome += snack.Price;
                            }
                        }
                        if (!BuildOrderedDrink(run, visit, hands)) continue;
                        bool pint = run.ServingGlass.HasPreparation(Preparations.Draught.Id);
                        double head = pint ? run.ServingGlass.Head / run.ServingGlass.Capacity : 0;
                        int specRequests = visit.Order.Spec.RequestCount;
                        var verdict = run.ServeTo(visit);
                        servedHere++;
                        stats.RecordServe(verdict, pint, head, specRequests);
                        buildTimer = 0;
                        break;
                    }
                }
                else
                {
                    // THE COUNTED NIGHT ONLY (GDD 26 §3): the guest of the house is not one of
                    // the bar's customers, so a trial must not show up as a serve, a storm-off
                    // or a head in the throughput numbers.
                    var counted = run.Floor.FinishedCounted();
                    foreach (var visit in counted)
                        if (visit.State == VisitState.StormedOff) stats.StormOffs++;
                    stats.CustomersFinished += counted.Count;

                    int refill = run.Shelf.RefillCost(run.Config.RefillPricePerCapacity);
                    if (refill > 0 && run.Money >= refill) run.RefillShelf();

                    // The menu is bought now (v5 P16). It used to be the FREE progression --
                    // the order pool grew into ranks 1-14 on its own -- so the floor buys the
                    // cheapest gate-passing recipe a night, and the new stock its menu names,
                    // or it would measure a bar frozen at four drinks. Brand UPGRADES, extra
                    // ambience upgrades stay off the floor as before.
                    RecipeDefinition cheapest = null;
                    foreach (var r in run.LockedRecipes)
                        if (run.Rating.Average >= run.RecipeStarGate(r) &&
                            (cheapest == null || r.Rank < cheapest.Rank))
                            cheapest = r;
                    // No purchase in the last week: a recipe bought seven nights from the end
                    // cannot earn its price back, and a floor that buys it measures
                    // generosity, not play. Wave 3 taught this — five more buyables took the
                    // floor from 7.5% to 42.5% bankruptcies on late-run purchases alone.
                    //
                    // RELATIVE TO THE HORIZON, not day 23 (2026-08-14). It was written as an
                    // absolute against a thirty-day run and silently became "stop shopping on
                    // day 24, forever" the moment the star track asked for a hundred and
                    // twenty — so the long measurement was of a bar that quits improving in
                    // its fourth week. A cutoff that means "near the end" has to be told
                    // where the end is.
                    bool shoppingWeek = run.Day <= dayCap - 7;
                    if (cheapest != null && shoppingWeek &&
                        run.Money >= run.RecipePrice(cheapest) + 40)
                    {
                        run.UnlockRecipe(cheapest.Id);
                        stats.RecipesBought++;
                    }
                    var neededStyles = new HashSet<string>();
                    foreach (var r in run.MenuRecipes)
                        foreach (var band in r.RatioRequirements)
                            if (band.IsStyleBand) neededStyles.Add(band.Style);
                    for (int oi = 0; oi < run.MarketOffers.Count; oi++)
                    {
                        var offer = run.MarketOffers[oi];
                        if (offer.Sold || !offer.IsNewStock || !neededStyles.Contains(offer.Style)) continue;
                        if (run.Money >= offer.Price + 40) run.BuyBrand(oi);
                    }

                    // A TIER THE MENU DEMANDS IS STOCK, NOT A LUXURY (2026-08-14, the lineup
                    // pass). The rule below treats every upgrade as skippable — "nothing
                    // stops working without it" — and that stopped being true the day the
                    // book started naming tiers: a Gimlet asks for the reserve gin, and a bar
                    // holding only the well bottle cannot make one at all. Measured before
                    // this branch existed: the bot DECLINED 10,520 orders across 200 runs
                    // against 2 before, and its standing fell from 2.82 to 2.64 — which was
                    // a bot that could not shop, not a design that could not be played.
                    //
                    // Bought at the same $40 cushion new stock is, because it is new stock in
                    // everything but the market's own two-branch vocabulary.
                    var demanded = new Dictionary<string, int>();
                    foreach (var r in run.MenuRecipes)
                        foreach (var band in r.RatioRequirements)
                            if (band.IsStyleBand && band.MinTier > 1)
                                demanded[band.Style] = Math.Max(
                                    demanded.TryGetValue(band.Style, out int had) ? had : 0, band.MinTier);
                    for (int oi = 0; oi < run.MarketOffers.Count; oi++)
                    {
                        var offer = run.MarketOffers[oi];
                        if (offer.Sold || offer.IsNewStock) continue;
                        if (!demanded.TryGetValue(offer.Style, out int want)) continue;
                        stats.TierOffersSeen++;
                        if ((offer.Bottle.Info?.Tier ?? 1) < want) continue;
                        bool already = false;
                        foreach (var b in run.Shelf.Bottles)
                            if (b.Ingredient.Info?.Style == offer.Style
                                && (b.Ingredient.Info?.Tier ?? 1) >= want) { already = true; break; }
                        if (already) continue;
                        if (run.Money >= offer.Price + 40)
                        { run.BuyBrand(oi); stats.BrandsBought++; stats.TierBuys++; }
                    }
                    foreach (var kv in demanded)
                    {
                        stats.TierDemands++;
                        bool met = false;
                        foreach (var b2 in run.Shelf.Bottles)
                            if (b2.Ingredient.Info?.Style == kv.Key
                                && (b2.Ingredient.Info?.Tier ?? 1) >= kv.Value) { met = true; break; }
                        if (!met) stats.TierShort++;
                    }

                    // THE BOT SHOPS FOR BETTER BOTTLES NOW (2026-08-14, GDD 26 §12.2 step 3).
                    // It bought STOCK — a style its menu named and its shelf lacked — and
                    // never an UPGRADE, which is the whole other half of the market and the
                    // half that makes a drink better rather than merely possible. That gap is
                    // why the star track's top five rungs came back empty: the bar could pour
                    // everything on its menu and never pour any of it well.
                    //
                    // One a night, cheapest first, behind a fat cushion. The cushion is not
                    // caution for its own sake — wave 3 measured five new buyables taking the
                    // floor from 7.5% to 42.5% bankruptcies — and an upgrade is the most
                    // skippable purchase in the game: nothing stops working without it.
                    // NOT filtered by `neededStyles` like new stock is, and the difference is
                    // the point: new stock is bought to make a drink POSSIBLE, so it has to be
                    // a style the menu names. An upgrade is bought to make a drink BETTER, and
                    // the market only ever offers upgrades for styles already on the shelf —
                    // which are, by definition, styles this bar pours. Reusing the menu's
                    // style set here filtered on a set built from style BANDS only, and most
                    // recipes ask by ingredient type.
                    int bestUpgrade = -1, bestUpgradePrice = int.MaxValue;
                    for (int oi = 0; oi < run.MarketOffers.Count; oi++)
                    {
                        var offer = run.MarketOffers[oi];
                        if (offer.Sold || offer.IsNewStock) continue;
                        if (offer.Price < bestUpgradePrice) { bestUpgradePrice = offer.Price; bestUpgrade = oi; }
                    }
                    if (bestUpgrade >= 0 && shoppingWeek && run.Money >= bestUpgradePrice + 250)
                    {
                        run.BuyBrand(bestUpgrade);
                        stats.BrandsBought++;
                    }
                    // ONE fitting a night (2026-08-07). The bot spends it the way a player
                    // working the cap would: a stool first while the room is small — seats
                    // are throughput and throughput is money — and the glass ladder with
                    // whatever night is left over.
                    bool fittingSpent = false;
                    if (run.CanFitTonight && run.Seats < run.Config.MaxSeats &&
                        run.Money >= run.Config.SeatPrice(run.Seats) + 40)
                    { run.BuySeat(); fittingSpent = true; }

                    // The star loop (2026-08-02): the standing is CAPPED by the fittings,
                    // and glassware went per-LINE — so the bot buys the cheapest next step
                    // across the lines, the way a player working the cap would.
                    // The six-step ladder is front-loaded (TycoonRun.GlassStepCap): the
                    // first two steps of a line carry most of its ceiling, the rest are
                    // endgame prestige. The bot plays that shape — early steps on a small
                    // cushion, deep steps only when genuinely flush — because a bot that
                    // chases legendary sets while the rent climbs measures its own greed
                    // (50% bankruptcies), not the design.
                    GlasswareDefinition bestGlass = null;
                    int bestPrice = int.MaxValue, bestStep = 0;
                    foreach (var g in run.Glassware)
                    {
                        int t = run.GlassTier(g.Id);
                        if (t >= TycoonRun.MaxGlassTier) continue;
                        int price = g.TierPrices[t - 1];
                        if (price < bestPrice) { bestPrice = price; bestGlass = g; bestStep = t - 1; }
                    }
                    int cushion = bestStep < 2 ? 70 : 250;
                    if (!fittingSpent && run.CanFitTonight && bestGlass != null
                        && run.Money >= bestPrice + cushion)
                        run.BuyGlassTier(bestGlass.Id);

                    stats.RecordNight(run.Floor.Elapsed, run.Rating.LastNight);
                    // THE STAR TRACK, MEASURED (GDD 26 §12.2 step 3). The standing is read
                    // AFTER the night is filed and BEFORE the next day opens, which is the
                    // moment the slip shows it to the player — so "the week the bar reached
                    // 2.5" means the same thing here and on the screen.
                    stats.RecordStanding(run.Day, run.Rating.Average);
                    stats.RecordDay(run.ContinueToNextDay());
                }
            }

            stats.Runs++;
            stats.DaysSurvived.Add(run.Ledger.History.Count);
            stats.FinalMoney.Add(run.Money);
            if (run.Phase == TycoonPhase.Closed) stats.Bankruptcies++;
            // WHERE THE STORY GOT TO when the nights ran out (GDD 26 §9). A beat that stalls
            // every run is either written for a shelf the bar cannot have by then or asks for
            // more than the clock allows — and that shows up here as a name, not as a hunch.
            if (run.Story != null)
            {
                if (run.Story.IsFinished) stats.ArcsFinished++;
                else
                {
                    string id = run.Story.Current.Id;
                    stats.StalledOn.TryGetValue(id, out int stalled);
                    stats.StalledOn[id] = stalled + 1;
                }
            }
        }

        /// <summary>
        /// The bot's night with the story's guest (GDD 26 §4, PLAN_last_call S2). It has no
        /// dialogue to read, so it starts the trial the moment it gets to the stool, then
        /// builds each ask as it is named — one per build tick, the same hands the crowd
        /// gets. A shelf that cannot pour the ask gets the HONEST NO rather than a wrong
        /// drink: the beat comes back next week, which is what the design says a blocked
        /// night costs, and it is what a player who reads the warning would do.
        ///
        /// It pours FULLER here than for the crowd. The ordinary build fills 0.85 of the
        /// glass and the trial wants <see cref="StoryTrial.MinFill"/> — a bartender pours to
        /// the standard they are being held to, and a bot that did not would fail every
        /// trial on the fill alone and report the whole arc as impossible.
        /// </summary>
        private static void PourForTheLastCustomer(TycoonRun run, Aggregate stats)
        {
            var guest = run.LastCustomer;
            var trial = run.Trial;
            if (guest == null || trial == null) return;

            if (trial.State == TrialState.Talking)
            {
                stats.StoryTrials++;
                run.BeginLastCallTrial();
            }
            if (trial.IsOver) return;

            var ask = trial.Current;
            if (ask == null) return;
            // CAN IT ACTUALLY BE POURED, not just "is there a bottle of that style" — the
            // last call happens at the DRIEST moment of the night, after the crowd has drunk
            // the well down and before the morning's refill (2026-08-13, measured: the bot
            // was topping an inspector's vodka soda out of an empty soda bottle and serving
            // the vodka, 1600 times). CanMake answers the shelf's question; this one answers
            // the bottle's. A bartender who cannot pour it says so.
            // A Built drink goes together IN THE GLASS, the way the design says it does and
            // the way the trial's fill standard demands (2026-08-13, measured). Anything the
            // book wants shaken or stirred still goes through the tin, because the verb is
            // half the grade.
            run.DiscardGlass();
            bool built = ask.Prep == PrepMethod.Built
                ? BuildInTheGlass(run, ask, 0.98)
                : BuildOrderedDrink(run, guest, Hands.Steady, fillTarget: 0.98);
            if (!run.CanMake(guest.Order) || !EnoughLeftToPour(run, ask) || !built)
            {
                run.DeclineLastCall();
                stats.StoryDeclined++;
                return;
            }

            // The glass as it goes over the bar, folded into the miss key. A trial drink that
            // comes back is worth exactly one line of evidence, and this is it — the wrong
            // drink is never the interesting part, the vessel and the ratios are.
            string glass = $"{run.ServingGlassware?.Id} {run.ServingGlass.TotalVolume:0.00}/" +
                           $"{run.ServingGlass.Capacity:0.00} [" +
                           string.Join(" ", run.ServingGlass.Ingredients.Select(
                               i => i + "=" + run.ServingGlass.RatioOf(i).ToString("0.00"))) + "]";
            var verdict = run.ServeTo(guest);
            stats.RecordTrialDrink(verdict, ask, trial.Trial.MinFill, glass);
            if (trial.State == TrialState.Passed) stats.StoryPassed++;
            else if (trial.State == TrialState.Failed) stats.StoryFailed++;
        }

        /// <summary>
        /// Builds the ordered recipe at band midpoints. Bottle choice inside a type prefers
        /// charges aligned with the seat's visible intent — the same information a player
        /// has without opening the licence.
        /// </summary>
        /// <summary>
        /// Builds a BUILT drink in the glass, topping it up each time the glass changes under
        /// it (2026-08-13, measured — GDD 26 §4). This exists because of a real trap the trial
        /// walked straight into: a drink DECLARES ITSELF at the glass (`TycoonRun.PourAtGlass`
        /// re-vessels on every match), so a half-built highball is repeatedly re-housed — pure
        /// soda reads as something that lives in a rocks glass, and the spirit that follows no
        /// longer fits. The bot's tin-first build survived it only by accident: at the crowd's
        /// 0.85 target the clamped ratio happens to stay inside its band, and at the fill an
        /// inspector wants it does not. It came back as 1600 identical wrong drinks and one
        /// diagnostic line: `rocks:0.70/0.70 [soda_klara=0.84 vodka_astra=0.16]`.
        ///
        /// A player solves this without thinking: pour, watch the barman swap the glass, top
        /// it up. Three passes are enough — a drink cannot be re-housed more often than that.
        /// </summary>
        private static bool BuildInTheGlass(TycoonRun run, RecipeDefinition recipe, double fillTarget)
        {
            var bands = recipe.RatioRequirements;
            var ideal = RatioRecipeMatcher.IdealPour(recipe);
            var bottles = new ShelfBottle[bands.Count];
            for (int i = 0; i < bands.Count; i++)
            {
                bottles[i] = bands[i].IsStyleBand
                    ? PickByStyle(run.Shelf, bands[i].Style)
                    : PickBottleWithMost(run.Shelf, bands[i].Type);
                if (bottles[i] == null) return false;
            }

            // IN SMALL ROUNDS, ALWAYS IN RATIO. Big confident pours are what break here: the
            // glass can shrink under the drink (a highball's worth of vodka and ginger reads
            // as something that lives in a rocks glass), and whatever was already in it stays
            // in it — so an overfilled glass is a permanently wrong ratio, and the ingredient
            // that had not gone in yet never gets in at all. That is exactly how the mule came
            // back 1600 times as `rocks 0.70/0.70 [vodka=0.55 ginger=0.45]`, with no lime in
            // it. A round is capped at a fraction of the glass, so a re-vessel costs a
            // splash instead of the drink.
            const int Rounds = 12;
            for (int round = 0; round < Rounds; round++)
            {
                double capacity = run.ServingGlass.Capacity;
                double target = Math.Max(recipe.MinFill, fillTarget) * capacity;
                double gap = target - run.ServingGlass.TotalVolume;
                if (gap <= 1e-6) break;
                double step = Math.Min(gap, capacity * 0.15);
                for (int i = 0; i < bands.Count; i++)
                    if (step * ideal[i] > 1e-6) run.PourAtGlass(bottles[i].Id, step * ideal[i]);
            }
            return run.DrinkReady;
        }

        private static bool BuildOrderedDrink(TycoonRun run, CustomerVisit visit,
            Hands hands, double fillTarget = 0.85)
        {
            run.DiscardGlass();
            hands.Deal();                     // one hand of dice per drink, before any branch
            var recipe = visit.Order.Wanted;
            if (WantsBeer(recipe)) return PullPint(run, visit);

            // v5 P11: the serving spec is printed on the licence, so a competent player reads
            // it and does what it says. A bot that ignored it would understate the floor by the
            // whole spec share of the tip -- it measured 56% spec score and 2.7% fully-met
            // orders, which is a strawman, not a floor.
            var spec = visit.Order.Spec;
            // Sized for the glass the drink will LAND in, not for the shaker (2026-07-31):
            // a gin sour lives in a 0.7 rocks glass, and building 0.85 of a 1.0 shaker for
            // it wasted an eighth of the stock every serve.
            double glassCap = run.Config.GlassCapacity;
            if (run.Glassware != null)
                foreach (var gw in run.Glassware)
                    if (gw.Id == recipe.GlassId) { glassCap = gw.Capacity; break; }
            // A hand aims for a fill, it does not compute one (see Hands). The floor's
            // jitter is zero, so this is the same line it always was.
            double aimedFill = Math.Max(recipe.MinFill, fillTarget) * (1.0 + hands.Jitter(hands.FillSigma, 6));
            double volume = Math.Max(0.05, Math.Min(1.0, aimedFill))
                            * Math.Min(glassCap, run.Glass.Capacity);
            // EVERYTHING GOES IN THE TIN (2026-08-14, GDD 21 §12 overturned). Carbonated
            // bands used to be held back and poured at the glass after the pour-out, because
            // Core refused fizz in the shaker; it does not any more, and a bot that still
            // built drinks in two places would stop measuring the game the player plays.
            // It also means the tin now holds the WHOLE drink when the glassware is chosen,
            // so the bot's Gin & Tonic reaches for a highball on the strength of being a
            // Gin & Tonic rather than of being pure gin.
            // The EXACT pour the book prints (2026-08-02), not the bands' raw midpoints:
            // those total 103% on a Gin Sour and 94% on an Espresso Martini, so the bot was
            // measuring a bartender who cannot add up. IdealPour is inside every band and
            // fills the glass, which is what the card now tells the player to do.
            var ideal = RatioRecipeMatcher.IdealPour(recipe);
            for (int bi = 0; bi < recipe.RatioRequirements.Count; bi++)
            {
                var band = recipe.RatioRequirements[bi];
                var bottle = band.IsStyleBand
                    ? PickByStyle(run.Shelf, band.Style, band.MinTier)
                    : PickBottle(run.Shelf, band.Type, visit);
                if (bottle == null) return false;
                // The error is on the SHARE, not on the millilitre: a bartender who is
                // heavy on the gin is heavy in proportion, and it is the proportion the
                // bands are graded on.
                double share = Math.Max(0, ideal[bi] * (1.0 + hands.Jitter(hands.RatioSigma, bi)));
                double amount = Math.Min(volume * share, bottle.Remaining);
                run.PourMeasure(bottle.Id, amount);
            }
            if (run.Glass.IsEmpty) return false;

            int garnishSlot = 0;
            foreach (var garnish in spec.Garnishes)
                if (!run.Glass.IsFull && !hands.Slips(hands.SkipSpec, 8 + garnishSlot++)) run.AddPreparation(garnish);
            // THE BOT MIXES THE WAY THE BOOK SAYS (GDD 21 §14 + 23 §4, 2026-08-11): the
            // method is the RECIPE's demand — the judge grades it now — and the mandatory
            // mix refuses an unworked tin at the pour-out besides. The last branch is the
            // one that catches a tin the book reads as some OTHER drink than the one being
            // built; a stir is what a bartender who built it in a tin would do.
            if (!run.Glass.IsEmpty)
            {
                // A FORGOTTEN MIX ONLY WHERE CORE FORGIVES IT. Where the pour-out refuses an
                // unworked tin (GDD 21 §14) the bench tells the player so and they work it —
                // measuring a bot that walks into a wall it can see would measure nothing.
                // Where the method is only GRADED, forgetting it is a real, payable mistake.
                bool forget = !run.MixRequired && hands.Slips(hands.ForgetMix, 10);
                if (forget) { }
                else if (recipe.Prep == PrepMethod.Shaken) run.Shake(1.0);
                else if (recipe.Prep == PrepMethod.Stirred) run.Stir(1.0);
                else if (run.MixRequired) run.Stir(1.0);
            }

            // Into the glass, dead on the rim. The bot used to hand the shaker over whole, which
            // the rules now refuse (2026-07-28); pouring perfectly keeps its standing unchanged —
            // it never had to aim, and the aim is the player's skill, not the floor's.
            // THE AIM IS THE PLAYER'S SKILL, and the floor never had to have any — that
            // was written here as a deliberate exemption. It is a dial now: off the rim
            // spills, and what spills is gone.
            if (!run.Glass.IsEmpty)
                run.PourIntoServingGlass(run.Glass.TotalVolume,
                    accuracy: Math.Max(0.0, 1.0 - Math.Abs(hands.Jitter(hands.AimSigma, 7))));
            return run.DrinkReady;
        }

        private static bool WantsBeer(RecipeDefinition recipe)
        {
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer) return true;
            return false;
        }

        /// <summary>The fullest bottle of a named style (v5 P10 style bands). Locked recipes
        /// mean nothing reaches this yet — it is here so the day they unlock, the bot answers
        /// them instead of quietly reading every style band as Spirit and building nonsense.</summary>
        /// <summary>
        /// Is there enough IN THE BOTTLES to pour this to the trial's standard? The build
        /// clamps every measure to what is left (`Math.Min(share, bottle.Remaining)`), which
        /// for the crowd is the right forgiving behaviour — a short measure still reads as
        /// the drink — but for an inspector it silently hands over something else. Checked
        /// against the ideal pour with a little slack, because the standard has a little too.
        /// </summary>
        private static bool EnoughLeftToPour(TycoonRun run, RecipeDefinition recipe)
        {
            if (recipe.RatioRequirements == null || recipe.RatioRequirements.Count == 0) return true;
            double glassCap = run.Config.GlassCapacity;
            if (run.Glassware != null)
                foreach (var gw in run.Glassware)
                    if (gw.Id == recipe.GlassId) { glassCap = gw.Capacity; break; }
            double volume = Math.Max(recipe.MinFill, 0.98) * Math.Min(glassCap, run.ServingGlass.Capacity);
            var ideal = RatioRecipeMatcher.IdealPour(recipe);
            for (int i = 0; i < recipe.RatioRequirements.Count; i++)
            {
                var band = recipe.RatioRequirements[i];
                var bottle = band.IsStyleBand
                    ? PickByStyle(run.Shelf, band.Style, band.MinTier)
                    : PickBottleWithMost(run.Shelf, band.Type);
                if (bottle == null || bottle.Remaining < volume * ideal[i] * 0.95) return false;
            }
            return true;
        }

        private static ShelfBottle PickBottleWithMost(Shelf shelf, IngredientType type)
        {
            ShelfBottle best = null;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty || bottle.Ingredient.Type != type) continue;
                if (best == null || bottle.Remaining > best.Remaining) best = bottle;
            }
            return best;
        }

        /// <summary>
        /// The bottle a bartender would reach for to fill this band: the fullest one of the
        /// style that is GOOD ENOUGH for it, and the fullest of the style otherwise.
        ///
        /// **The tier used to be invisible here** (2026-08-14). It picked purely on what was
        /// left, so a bar that owned the reserve gin still poured its well gin into a Gimlet
        /// that names tier 3 — and the band failed, on a shelf that could answer it. It cost
        /// 7.3% of every serve in the game, with perfectly steady hands, and it read as a
        /// design problem: the day-end counter said the shelf could answer every demand it
        /// was making (0 of 15,916), while the serve counter said one in fourteen drinks was
        /// missing its band. Both were true, because nothing in between reached for the
        /// bottle. The instrument, not the bar — the third time that has been the answer in
        /// this file, which is why the fallback below is deliberate rather than tidy: a bar
        /// that has NOT got the good bottle still pours the drink, badly, the way a player
        /// would, and that is the Close grade doing its job.
        /// </summary>
        private static ShelfBottle PickByStyle(Shelf shelf, string style, int minTier = 0)
        {
            ShelfBottle best = null, bestAtTier = null;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty || bottle.Ingredient.Info?.Style != style) continue;
                if (best == null || bottle.Remaining > best.Remaining) best = bottle;
                if (minTier > 1 && (bottle.Ingredient.Info?.Tier ?? 1) >= minTier
                    && (bestAtTier == null || bottle.Remaining > bestAtTier.Remaining))
                    bestAtTier = bottle;
            }
            return bestAtTier ?? best;
        }

        /// <summary>
        /// A pint, poured the way the mechanic asks for it (GDD 21 §10.2): leaned over while it
        /// fills, then straightened at the end to raise the head. Before this the bot answered
        /// draught orders by building beer in the shaker, which the rules now refuse and which
        /// scored a headless glass as a perfect pint — every beer figure it reported was fiction.
        ///
        /// It pours competently, not perfectly, which is the same standing the band-midpoint
        /// cocktail has: this bot is a floor, not a ceiling.
        /// </summary>
        private static bool PullPint(TycoonRun run, CustomerVisit visit)
        {
            var keg = PickBottle(run.Shelf, IngredientType.Beer, visit);
            if (keg == null) return false;

            run.BeginPull(keg.Id);
            const double step = 0.05;
            // Leaned over until the glass is nearly there, then upright to build the head.
            const double leanTo = 0.78;
            for (int i = 0; i < 40 && run.ServingGlass.FillFraction < leanTo && run.PullingId != null; i++)
                run.PourTilted(step, TapPour.IdealTilt);
            for (int i = 0; i < 20 && run.ServingGlass.FillFraction < 0.97 && run.PullingId != null; i++)
                run.PourTilted(step, 6.0);
            run.EndPull();
            return !run.ServingGlass.IsEmpty;
        }

        private static ShelfBottle PickBottle(Shelf shelf, IngredientType type, CustomerVisit visit)
        {
            ShelfBottle best = null;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty || bottle.Ingredient.Type != type) continue;
                if (best == null) best = bottle;
            }
            return best;
        }

        // ── bookkeeping ─────────────────────────────────────────────────────────

        private sealed class Aggregate
        {
            public int Runs, Stuck, Bankruptcies, StormOffs, CustomersFinished;
            public int Serves, Exact, Close, Wrong, CraftServes, SpeedTips, ExtraOrders;
            public int SnackServes, SnackIncome;
            public int GlassesBussed;
            public int RecipesBought;
            public int BrandsBought;   // upgrades only; new stock is not a choice the bot makes

            // WHETHER THE BAR EVER GETS THE BOTTLES ITS OWN BOOK DEMANDS (2026-08-14, the
            // lineup pass). A minTier band is a page you own and cannot pour, so a floor
            // that quietly declines those is measuring nothing. Counted at every day end:
            // how many (style, tier) demands the menu makes that the shelf cannot answer.
            public int TierDemands;
            public int TierShort;
            public int TierBuys;
            public int TierOffersSeen;   // upgrade offers whose style the menu demands
            // v5 P11: the base/tip split is the phase's whole point, and refusals/declines are
            // the two new ways a serve can end.
            public int Refused, Declined, SpecOrders, SpecFull;
            // The written nights (GDD 26 §9). Kept apart from the serve counters on purpose:
            // a trial is not a service, and folding it in would move numbers the story is
            // supposed to leave alone. StalledOn is the canary the design asked for — which
            // beat a run was still owing when its thirty nights ran out.
            public int StoryTrials, StoryDrinks, StoryPassed, StoryFailed, StoryDeclined;
            public int ArcsFinished;
            public readonly Dictionary<string, int> StalledOn = new Dictionary<string, int>();
            // WHY a trial drink came back (GDD 26 §4). Without this the report can only say
            // that the bot fails, which is a fact and not a reason — and the reason is the
            // whole point of measuring: a standard nobody can meet is a design bug, and a
            // bot that pours wrong is a bot bug, and they look identical from the outside.
            public readonly Dictionary<string, int> TrialMisses = new Dictionary<string, int>();

            public void RecordTrialDrink(ServiceVerdict verdict, RecipeDefinition ask,
                double minFill, string glass = null)
            {
                StoryDrinks++;
                if (verdict.CraftLanded) return;   // the trial's verdict carries "perfect" here
                string why = verdict.Match != OrderMatch.Exact ? $"{ask.Id}: not the drink ({verdict.Match})"
                    : verdict.SpecScore < 1.0 ? $"{ask.Id}: craft ({verdict.SpecScore:0.00})"
                    : verdict.FillScore < minFill ? $"{ask.Id}: short pour ({verdict.FillScore:0.00})"
                    : $"{ask.Id}: method";
                if (glass != null) why += $" — {glass}";
                TrialMisses.TryGetValue(why, out int n);
                TrialMisses[why] = n + 1;
            }
            public long BaseSum, TipSum;
            public double SpecScoreSum, FillScoreSum;
            // v5 P12: the night is open, so throughput is a result rather than a setting.
            public double NightSecondsSum, StarsSum;
            public int NightsClosed;
            public int Pints, GoodPints;
            public double HeadSum;
            public double SatisfactionSum;
            public long IncomeSum, ExpenseSum;
            public int DaysClosed;
            public readonly List<int> DaysSurvived = new List<int>();
            public readonly List<int> FinalMoney = new List<int>();
            // WHERE THE MONEY GOES, NIGHT BY NIGHT (2026-08-14). The averages over a run
            // hide the only thing that matters here: rent is quadratic and takings are not,
            // so a bar that reads "$150 in, $145 out" on average is a bar that was making
            // money in week one and losing it in week five. One row a night, so the crossing
            // can be seen rather than argued about.
            public readonly Dictionary<int, (int income, int rent, int stock, int nights)> ByNight =
                new Dictionary<int, (int, int, int, int)>();

            public readonly Dictionary<int, (int reds, int closes)> ByDay =
                new Dictionary<int, (int, int)>();

            public void RecordServe(ServiceVerdict verdict, bool pint = false, double head = 0,
                int specRequests = 0)
            {
                Serves++;
                if (pint)
                {
                    Pints++;
                    HeadSum += head;
                    if (TapPour.HeadScore(head) >= 1.0) GoodPints++;
                }
                if (verdict.Match == OrderMatch.Exact) Exact++;
                else if (verdict.Match == OrderMatch.Close) Close++;
                else if (verdict.Match == OrderMatch.Refused) Refused++;
                else if (verdict.Match == OrderMatch.Declined) Declined++;
                else Wrong++;
                if (verdict.CraftLanded) CraftServes++;
                if (verdict.OrdersAgain) ExtraOrders++;
                BaseSum += verdict.BasePaid;
                TipSum += verdict.Tip;
                SpecScoreSum += verdict.SpecScore;
                FillScoreSum += verdict.FillScore;
                if (specRequests > 0)
                {
                    SpecOrders++;
                    if (verdict.SpecScore >= 1.0) SpecFull++;
                }
            }

            public void RecordNight(double seconds, double stars)
            {
                NightSecondsSum += seconds;
                StarsSum += stars;
                NightsClosed++;
            }

            // ── the star track (GDD 26 §12) ──────────────────────────────────
            //
            // Eleven rungs, half a star apart, and one written guest standing on each. The
            // question the design cannot answer by argument is WHEN a bar reaches each one,
            // because that is what says whether a 2.5-star guest belongs in week three or
            // week nine. This records, per run, the first day the standing crossed each rung.
            //
            // A run that never gets there records nothing for that rung — which is itself the
            // answer, and the reason the table prints how many runs reached it at all before
            // it prints a week.

            public const int Rungs = 11;                       // 0.0 .. 5.0 by halves
            public static double RungStars(int i) => i * 0.5;
            public readonly List<int>[] ReachedOn = MakeRungs();
            private bool[] _rungHit = new bool[Rungs];

            private static List<int>[] MakeRungs()
            {
                var a = new List<int>[Rungs];
                for (int i = 0; i < Rungs; i++) a[i] = new List<int>();
                return a;
            }

            /// <summary>Starts a fresh run's rung memory. Without it the second run would
            /// think it had already climbed everything the first one did.</summary>
            public void BeginRun() => _rungHit = new bool[Rungs];

            public void RecordStanding(int day, double stars)
            {
                for (int i = 0; i < Rungs; i++)
                {
                    if (_rungHit[i] || stars + 1e-9 < RungStars(i)) continue;
                    _rungHit[i] = true;
                    ReachedOn[i].Add(day);
                }
            }

            public void RecordDay(DayResult result)
            {
                DaysClosed++;
                IncomeSum += result.Income;
                ExpenseSum += result.Expenses;
                SatisfactionSum += result.AverageSatisfaction;
                ByDay.TryGetValue(result.Day, out var row);
                ByDay[result.Day] = (row.reds + (result.Net < 0 ? 1 : 0), row.closes + 1);
                ByNight.TryGetValue(result.Day, out var n);
                ByNight[result.Day] = (n.income + result.Income, n.rent + result.Rent,
                                       n.stock + result.Stock, n.nights + 1);
            }

            public string Report(int requested)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Tycoon sim report — GDD 23 balance");
                sb.AppendLine();
                sb.AppendLine($"Runs: **{Runs}** of {requested}" +
                              (Stuck > 0 ? $" ({Stuck} abandoned as stuck)" : "") +
                              $", horizon {DayCap} days, one drink per {DrinkBuildSeconds:0}s of bar time.");
                sb.AppendLine("Floor bot: serves the named order at band midpoints, pulls a pint");
                sb.AppendLine("leaned over then straightened, and shops — stock, recipes, stools,");
                sb.AppendLine("glass steps, and one brand upgrade a night it never once affords.");
                sb.AppendLine("Every survival figure is a floor.");
                sb.AppendLine();
                sb.AppendLine("| Metric | Value |");
                sb.AppendLine("|---|---|");
                sb.AppendLine($"| Bankruptcies | {Pct(Bankruptcies, Runs)} |");
                sb.AppendLine($"| Reached the {DayCap}-day horizon | {Pct(Runs - Bankruptcies, Runs)} |");
                sb.AppendLine($"| Days survived p25/median/p75 | {Q(DaysSurvived, 0.25)} / {Q(DaysSurvived, 0.5)} / {Q(DaysSurvived, 0.75)} |");
                sb.AppendLine($"| Final till p25/median/p75 | ${Q(FinalMoney, 0.25)} / ${Q(FinalMoney, 0.5)} / ${Q(FinalMoney, 0.75)} |");
                sb.AppendLine($"| Avg income / expenses per day | ${(double)IncomeSum / Math.Max(1, DaysClosed):0.0} / ${(double)ExpenseSum / Math.Max(1, DaysClosed):0.0} |");
                sb.AppendLine($"| Avg daily satisfaction | {SatisfactionSum / Math.Max(1, DaysClosed):P0} |");
                sb.AppendLine($"| Storm-offs | {Pct(StormOffs, CustomersFinished)} |");
                sb.AppendLine($"| Customers per night | {(double)CustomersFinished / Math.Max(1, NightsClosed):0.0} |");
                sb.AppendLine($"| Served per bar-minute | {CustomersFinished * 60.0 / Math.Max(1.0, NightSecondsSum):0.00} |");
                sb.AppendLine($"| Bar standing (avg night) | {StarsSum / Math.Max(1, NightsClosed):0.00} stars |");
                sb.AppendLine($"| Serves Exact / Close / Wrong | {Pct(Exact, Serves)} / {Pct(Close, Serves)} / {Pct(Wrong, Serves)} |");
                sb.AppendLine($"| Refused (too little in the glass) / declined | {Pct(Refused, Serves)} / {Declined} |");
                sb.AppendLine($"| Take: base / tip | ${BaseSum} / ${TipSum} ({Pct((int)TipSum, (int)Math.Max(1, BaseSum + TipSum))} of it tip) |");
                sb.AppendLine($"| Avg base / tip per serve | ${(double)BaseSum / Math.Max(1, Serves):0.00} / ${(double)TipSum / Math.Max(1, Serves):0.00} |");
                sb.AppendLine($"| Avg spec score / fill score | {SpecScoreSum / Math.Max(1, Serves):P0} / {FillScoreSum / Math.Max(1, Serves):P0} |");
                sb.AppendLine($"| Orders with a serving spec, fully met | {Pct(SpecFull, SpecOrders)} of {SpecOrders} |");
                sb.AppendLine($"| Garnish craft landed | {Pct(CraftServes, Serves)} |");
                sb.AppendLine($"| Extra orders earned (of serves) | {Pct(ExtraOrders, Serves)} |");
                sb.AppendLine($"| Extra orders earned (of exact) | {Pct(ExtraOrders, Exact)} |");
                sb.AppendLine($"| Draught share of serves | {Pct(Pints, Serves)} |");
                sb.AppendLine($"| Pints in the good head band | {Pct(GoodPints, Pints)} |");
                sb.AppendLine($"| Average head poured | {HeadSum / Math.Max(1, Pints):P0} |");
                sb.AppendLine($"| Snack serves (of serves) | {Pct(SnackServes, Serves)} · ${SnackIncome} |");
                sb.AppendLine($"| Glasses bussed | {GlassesBussed} |");
                sb.AppendLine($"| Recipes bought (of {Runs} runs) | {RecipesBought} |");
                sb.AppendLine($"| Brand upgrades bought | {BrandsBought} |");
                sb.AppendLine($"| Tier demands the shelf could not answer | {TierShort} of {TierDemands}" +
                              $" ({100.0 * TierShort / Math.Max(1, TierDemands):0.0}%) |");
                sb.AppendLine($"| Demanded upgrades bought | {TierBuys} |");
                sb.AppendLine($"| Demanded upgrades OFFERED | {TierOffersSeen} |");
                sb.AppendLine();

                // ── the star track (GDD 26 §12.2 step 3) ─────────────────────
                sb.AppendLine("## The star track — when a bar reaches each rung");
                sb.AppendLine();
                sb.AppendLine("Eleven rungs, one written guest on each. This is the table the");
                sb.AppendLine("thresholds get chosen from, and it is a FLOOR like everything else the");
                sb.AppendLine("bot measures: it reads only the ID, never shops, and never buys a brand,");
                sb.AppendLine("so a played bar climbs faster than this. Trust the SHAPE — how far apart");
                sb.AppendLine("the rungs are — over the absolute weeks. A rung no run reaches is the");
                sb.AppendLine("most useful line here: it says a guest written for it would never come.");
                sb.AppendLine();
                sb.AppendLine("| Rung | Runs that reached it | Day p25/median/p75 | Median week |");
                sb.AppendLine("|---|---|---|---|");
                for (int i = 0; i < Rungs; i++)
                {
                    var days = ReachedOn[i];
                    if (days.Count == 0)
                    {
                        sb.AppendLine($"| {RungStars(i):0.0}★ | **none of {Runs}** | — | — |");
                        continue;
                    }
                    int med = Q(days, 0.5);
                    sb.AppendLine($"| {RungStars(i):0.0}★ | {Pct(days.Count, Runs)} | " +
                                  $"{Q(days, 0.25)} / {med} / {Q(days, 0.75)} | " +
                                  $"{BarCalendar.WeekOf(Math.Max(1, med))} |");
                }
                sb.AppendLine();
                sb.AppendLine("## The written nights (GDD 26)");
                sb.AppendLine();
                sb.AppendLine("The bot starts the trial the moment it reaches the stool (it has no");
                sb.AppendLine("dialogue to read), pours every ask to the trial's own fill standard, and");
                sb.AppendLine("says an honest no when the shelf cannot make one. None of this touches");
                sb.AppendLine("the numbers above: a guest of the house is not a customer.");
                sb.AppendLine();
                sb.AppendLine("| Measure | Value |");
                sb.AppendLine("|---|---|");
                sb.AppendLine($"| Trials walked in | {StoryTrials} |");
                sb.AppendLine($"| Drinks poured for them | {StoryDrinks} |");
                sb.AppendLine($"| Passed / failed / declined | {StoryPassed} / {StoryFailed} / {StoryDeclined} |");
                sb.AppendLine($"| Arcs finished inside {DayCap} nights | {Pct(ArcsFinished, Runs)} |");
                if (StalledOn.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("| Still owing at the horizon | Runs |");
                    sb.AppendLine("|---|---|");
                    foreach (var pair in StalledOn.OrderByDescending(p => p.Value))
                        sb.AppendLine($"| {pair.Key} | {Pct(pair.Value, Runs)} |");
                }
                if (TrialMisses.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("| What came back, and why | Drinks |");
                    sb.AppendLine("|---|---|");
                    foreach (var pair in TrialMisses.OrderByDescending(p => p.Value))
                        sb.AppendLine($"| {pair.Key} | {pair.Value} |");
                }

                sb.AppendLine();
                sb.AppendLine("## Red days by day number");
                sb.AppendLine();
                sb.AppendLine("| Day | Closed | In the red |");
                sb.AppendLine("|---|---|---|");
                foreach (var day in ByDay.Keys.OrderBy(d => d).Take(15))
                {
                    var row = ByDay[day];
                    sb.AppendLine($"| {day} | {row.closes} | {Pct(row.reds, row.closes)} |");
                }
                return sb.ToString();
            }

            private static string Pct(int part, int whole) =>
                whole == 0 ? "—" : $"{part} ({(double)part / whole:P1})";

            private static int Q(List<int> values, double q) => Quantile(values, q);

            /// <summary>The same arithmetic the report tables use, reachable from the
            /// star-track tool so the two documents cannot quote different medians.</summary>
            public static int Quantile(List<int> values, double q)
            {
                if (values.Count == 0) return 0;
                var sorted = values.OrderBy(v => v).ToList();
                return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];
            }
        }
    }
}
