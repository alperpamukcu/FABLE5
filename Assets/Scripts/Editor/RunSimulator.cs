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
    /// Batch-simulates seeded runs so the emotion pivot's numbers can be measured instead of
    /// guessed (GDD 20 balance). Drives the real <see cref="RunController"/> — there is no
    /// second implementation of the rules here, so what this measures is what ships.
    ///
    /// The bot plays from the **ID, not the truth**. It estimates a hidden stat the way a
    /// player must (Exact → the number, Range → the midpoint, Unknown → a shrug at 50), which
    /// is the whole point: a bot that peeked at <see cref="RegularState.Stats"/> would report
    /// a game that nobody can actually play.
    ///
    /// Known floor: the bot never shops. Real runs buy patrons and tools, so treat every
    /// survival number here as a lower bound.
    /// </summary>
    public static class RunSimulator
    {
        private const int DefaultRuns = 300;

        [MenuItem("LastCall/Simulate 300 Runs")]
        public static void SimulateDefault() => Simulate(DefaultRuns);

        [MenuItem("LastCall/Simulate 2000 Runs (slow)")]
        public static void SimulateLong() => Simulate(2000);

        public static void Simulate(int runs)
        {
            var deck = DataLoader.ParseDeck(Read("decks/classic_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));

            var stats = new Aggregate();
            for (int i = 0; i < runs; i++)
                PlayRun($"SIM-{i:0000}", deck, recipes, archetypes, stats);

            string report = stats.Report(runs);
            Debug.Log(report);
            var path = Path.Combine(Application.dataPath, "..", "Docs", "sim_report.md");
            File.WriteAllText(Path.GetFullPath(path), report);
            Debug.Log($"[Sim] wrote {Path.GetFullPath(path)}");
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static void PlayRun(string seed, LoadedDeck deck,
            IReadOnlyList<RecipeDefinition> recipes, IReadOnlyList<ArchetypeDefinition> archetypes,
            Aggregate stats)
        {
            // Fresh card instances per run: IngredientCard is mutable (Tools rework it).
            var cards = deck.Cards
                .Select(c => new IngredientCard(c.Id, c.Name, c.Type, c.Flavor, c.Quality, c.Charges))
                .ToList();

            var run = new RunController(cards, recipes, new RunRng(seed), archetypes: archetypes);
            int guard = 0;

            while (run.Phase == RunPhase.CustomerRound || run.Phase == RunPhase.BackRoom)
            {
                if (guard++ > 2000) { stats.Stuck++; return; }

                if (run.Phase == RunPhase.BackRoom)
                {
                    // The bot buys nothing — see the class note on this being a floor.
                    run.ContinueToNextCustomer();
                    continue;
                }

                int weekBefore = run.Quota.Week;
                PlayCustomer(run, stats);
                if (run.Phase == RunPhase.CustomerRound) continue;

                // A week closes when the counter rolls over, when the quota fails, or when
                // the final week is cleared — that last case wins the run without ever
                // incrementing the week, and an earlier version of this silently dropped it,
                // reporting the final week as 0% passed while runs were plainly being won.
                bool weekClosed = run.Quota.Week != weekBefore ||
                                  run.Phase == RunPhase.RunLost ||
                                  run.Phase == RunPhase.RunWon;
                // LastClosedWeek is the frozen tally: reading run.Quota here would either be
                // the *next* week's empty counter or miss the final customer's contribution.
                if (weekClosed && run.LastClosedWeek != null)
                    stats.RecordWeek(weekBefore, run.Phase != RunPhase.RunLost,
                        run.LastClosedWeek.Earned);
            }

            stats.Runs++;
            stats.NightsReached += run.Night;
            if (run.Phase == RunPhase.RunWon) stats.Won++;
            else stats.Lost++;
        }

        private static void PlayCustomer(RunController run, Aggregate stats)
        {
            var round = run.CurrentRound;
            while (run.Phase == RunPhase.CustomerRound && round.Phase == RoundPhase.InProgress)
            {
                var pick = BestMix(round);
                if (pick == null || pick.Count == 0) break;

                run.Mix(pick);
                stats.Mixes++;

                var verdict = round.LastResonance;
                if (verdict != null)
                {
                    if (verdict.IsBust) stats.Busts++;
                    if (verdict.CleanServe) stats.CleanServes++;
                    if (verdict.BlindRead) stats.BlindReads++;
                    stats.Satisfaction += verdict.Satisfaction;
                }
            }
            stats.Customers++;
            if (round.Phase == RoundPhase.Won) stats.OrdersFilled++;
        }

        /// <summary>
        /// Greedy one-ply choice over every legal selection. Enough to tell a curve that is
        /// merely hard from one that is impossible; not a claim about optimal play.
        /// </summary>
        private static IReadOnlyList<IngredientCard> BestMix(RoundController round)
        {
            var rail = round.Rail;
            int n = Math.Min(rail.Count, 12);
            IReadOnlyList<IngredientCard> best = null;
            double bestValue = double.NegativeInfinity;

            for (int mask = 1; mask < (1 << n); mask++)
            {
                int size = CountBits(mask);
                if (size > round.Config.MaxMixSelection) continue;

                var pick = new List<IngredientCard>(size);
                for (int i = 0; i < n; i++)
                    if ((mask & (1 << i)) != 0) pick.Add(rail[i]);

                double value = Evaluate(round, pick);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = pick;
                }
            }
            return best;
        }

        private static double Evaluate(RoundController round, IReadOnlyList<IngredientCard> pick)
        {
            var breakdown = round.PreviewScore(pick);
            double points = breakdown.FinalScore;

            if (!round.Customer.HasEmotion) return points;

            var read = round.Customer.Read;
            var intent = read.Intent;
            int move = round.PreviewCharges(pick)[intent];
            int estimate = EstimateFromRead(read[intent]);
            int target = read.TargetValue;
            int projected = estimate + move;

            bool towardZero = read.Direction == IntentDirection.Extinguish;
            bool likelyOvershoot = towardZero ? projected < target : projected > target;
            int progress = towardZero ? estimate - projected : projected - estimate;

            // Points are the tiebreak, not the goal: satisfaction is what survives the week.
            double value = points / 500.0;
            if (likelyOvershoot) return value - 1000;          // never knowingly bust
            if (projected == target) value += 300;             // Clean Serve is worth chasing
            value += progress * 10;
            return value;
        }

        /// <summary>What a player can honestly believe about a stat, given only the ID.</summary>
        private static int EstimateFromRead(StatReading reading)
        {
            switch (reading.Tier)
            {
                case VisibilityTier.Exact: return reading.Low;
                case VisibilityTier.Range: return (reading.Low + reading.High) / 2;
                default: return 50;
            }
        }

        private static int CountBits(int v)
        {
            int c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }

        private sealed class Aggregate
        {
            public int Runs, Won, Lost, Stuck;
            public int Customers, OrdersFilled, Mixes, Busts, CleanServes, BlindReads;
            public int Satisfaction, NightsReached;

            private readonly Dictionary<int, int> _weekAttempts = new Dictionary<int, int>();
            private readonly Dictionary<int, int> _weekPasses = new Dictionary<int, int>();

            /// <summary>Satisfaction actually earned in each closed week, per week number.</summary>
            private readonly Dictionary<int, List<int>> _weekEarned = new Dictionary<int, List<int>>();

            public void RecordWeek(int week, bool passed, int earned)
            {
                _weekAttempts.TryGetValue(week, out int a);
                _weekAttempts[week] = a + 1;
                if (passed)
                {
                    _weekPasses.TryGetValue(week, out int p);
                    _weekPasses[week] = p + 1;
                }

                if (!_weekEarned.TryGetValue(week, out var list))
                    _weekEarned[week] = list = new List<int>();
                list.Add(earned);
            }

            /// <summary>
            /// What each candidate requirement would have passed, computed from the observed
            /// distribution. Lets the curve be chosen from one simulation run instead of
            /// re-simulating per guess — and makes it obvious when a week is a free pass.
            /// </summary>
            private string QuotaSweep()
            {
                var sb = new StringBuilder();
                sb.AppendLine("## Quota sweep (what each requirement would have passed)");
                sb.AppendLine();
                sb.AppendLine("Measured from the earned-satisfaction distribution. A week that");
                sb.AppendLine("passes at ~100% is not a gate, it is a formality.");
                sb.AppendLine();
                sb.AppendLine("**Survivorship warning:** week N only contains runs that already");
                sb.AppendLine("cleared weeks 1..N-1, so later rows are drawn from a progressively");
                sb.AppendLine("stronger population. Raising an early requirement culls weak runs");
                sb.AppendLine("and *raises* the later rows — these columns are not independent,");
                sb.AppendLine("and multiplying them together will understate the real win rate.");
                sb.AppendLine();

                var candidates = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
                sb.Append("| Week | earned p25 / median / p75 |");
                foreach (int c in candidates) sb.Append($" {c} |");
                sb.AppendLine();
                sb.Append("|---|---|");
                foreach (var _ in candidates) sb.Append("---|");
                sb.AppendLine();

                foreach (var week in _weekEarned.Keys.OrderBy(w => w))
                {
                    var sorted = _weekEarned[week].OrderBy(v => v).ToList();
                    if (sorted.Count == 0) continue;
                    sb.Append($"| {week} | {Percentile(sorted, 0.25)} / {Percentile(sorted, 0.5)}" +
                              $" / {Percentile(sorted, 0.75)} |");
                    foreach (int c in candidates)
                    {
                        int pass = sorted.Count(v => v >= c);
                        sb.Append($" {100.0 * pass / sorted.Count:0}% |");
                    }
                    sb.AppendLine();
                }
                return sb.ToString();
            }

            private static int Percentile(List<int> sorted, double q) =>
                sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];

            public string Report(int requested)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Sim report — emotion pivot balance");
                sb.AppendLine();
                sb.AppendLine($"Runs: **{Runs}** of {requested} requested" +
                              (Stuck > 0 ? $" ({Stuck} abandoned as stuck)" : ""));
                sb.AppendLine();
                sb.AppendLine("Greedy one-ply bot reading only the ID, buying nothing in the Back");
                sb.AppendLine("Room. Every survival figure is therefore a floor.");
                sb.AppendLine();
                sb.AppendLine("| Metric | Value |");
                sb.AppendLine("|---|---|");
                sb.AppendLine($"| Runs won | {Pct(Won, Runs)} |");
                sb.AppendLine($"| Runs lost to quota | {Pct(Lost, Runs)} |");
                sb.AppendLine($"| Avg night reached | {Avg(NightsReached, Runs):0.00} |");
                sb.AppendLine($"| Customers served | {Customers} |");
                sb.AppendLine($"| Orders filled (score target) | {Pct(OrdersFilled, Customers)} |");
                sb.AppendLine($"| Satisfaction per customer | {Avg(Satisfaction, Customers):0.00} / 3 |");
                sb.AppendLine($"| Mixes | {Mixes} |");
                sb.AppendLine($"| Bust rate | {Pct(Busts, Mixes)} |");
                sb.AppendLine($"| Clean Serve rate | {Pct(CleanServes, Mixes)} |");
                sb.AppendLine($"| Blind-read mixes | {Pct(BlindReads, Mixes)} |");
                sb.AppendLine();
                sb.AppendLine("## Weekly quota gate");
                sb.AppendLine();
                sb.AppendLine("| Week | Required | Attempts | Passed |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var week in _weekAttempts.Keys.OrderBy(w => w))
                {
                    _weekPasses.TryGetValue(week, out int passed);
                    sb.AppendLine($"| {week} | {QuotaTable.Standard(week)} | " +
                                  $"{_weekAttempts[week]} | {Pct(passed, _weekAttempts[week])} |");
                }
                sb.AppendLine();
                sb.Append(QuotaSweep());
                return sb.ToString();
            }

            private static double Avg(int total, int count) => count == 0 ? 0 : (double)total / count;

            private static string Pct(int part, int whole) =>
                whole == 0 ? "—" : $"{part} ({100.0 * part / whole:0.0}%)";
        }
    }
}
