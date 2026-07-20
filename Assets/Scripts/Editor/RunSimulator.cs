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
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
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
                    // The bot buys no brands or patrons, but it does keep the well stocked:
                    // refills are upkeep, not strategy. Without them the 12-bottle base bar
                    // runs dry mid-week and every run dies of thirst, which measures the
                    // shelf size instead of the design.
                    int cost = run.Shelf.RefillCost(run.Config.RefillPricePerCapacity);
                    if (cost > 0 && run.Money >= cost) run.RefillShelf();
                    run.ContinueToNextCustomer();
                    continue;
                }

                int weekBefore = run.Quota.Week;
                PlayCustomer(run, stats, recipes);
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

        private static void PlayCustomer(RunController run, Aggregate stats,
            IReadOnlyList<RecipeDefinition> recipes)
        {
            var round = run.CurrentRound;
            while (run.Phase == RunPhase.CustomerRound && round.Phase == RoundPhase.InProgress)
            {
                if (!BuildDrink(round, recipes)) break;

                run.Serve();
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
            if (round.Customer.HasEmotion)
                stats.RecordDemand(round.Customer.Read.Demand, round.SatisfactionEarned);
        }

        /// <summary>
        /// Pours a drink aimed at the customer's intent, and reports whether anything went in.
        ///
        /// Under the card system the bot enumerated every legal subset. Ratios are continuous,
        /// so it now *solves* instead: pick the two strongest bottles that move the intent
        /// stat the right way, mix them 65/35, probe (purely, off a scratch glass) whether
        /// that mix lands a band recipe, and size the pour for whichever charge multiplier
        /// will actually apply. It never knowingly overshoots, and it reads only the ID —
        /// the probe consults the matcher, not the customer.
        /// </summary>
        private static bool BuildDrink(RoundController round, IReadOnlyList<RecipeDefinition> recipes)
        {
            var shelf = round.Shelf;
            if (!round.Customer.HasEmotion)
            {
                // No read to aim at: pour a plain half glass so the round still progresses.
                var any = shelf.Bottles.FirstOrDefault(b => !b.IsEmpty);
                return any != null && round.PourMeasure(any.Id, round.Config.GlassCapacity * 0.5) > 0;
            }

            var read = round.Customer.Read;
            var intent = read.Intent;
            int estimate = EstimateFromRead(read[intent]);
            int target = read.TargetValue;
            double needed = target - estimate;               // signed movement wanted
            if (Math.Abs(needed) < 1e-9)
            {
                // Already on target but the score bill is not paid: serve the gentlest
                // glass on the shelf rather than stalling the round forever.
                var mild = shelf.Bottles.Where(b => !b.IsEmpty)
                    .OrderBy(b => Math.Abs(ChargeOn(b.Ingredient, intent))).FirstOrDefault();
                return mild != null && round.PourMeasure(mild.Id, round.Config.GlassCapacity * 0.2) > 0;
            }

            ShelfBottle best = null;
            double bestPerGlass = 0;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty) continue;
                double perGlass = ChargeOn(bottle.Ingredient, intent);
                if (Math.Sign(perGlass) != Math.Sign(needed)) continue;
                if (Math.Abs(perGlass) > Math.Abs(bestPerGlass))
                {
                    bestPerGlass = perGlass;
                    best = bottle;
                }
            }
            if (best == null)
            {
                // Nothing moves the stat the right way (that side of the shelf is drained):
                // pour a modest neutral glass and eat the miss. The old `return false` here
                // spun forever on the same customer once the 12-bottle bar ran one-sided,
                // and every run died "stuck".
                var fallback = shelf.Bottles.Where(b => !b.IsEmpty)
                    .OrderBy(b => Math.Abs(ChargeOn(b.Ingredient, intent))).FirstOrDefault();
                return fallback != null && round.PourMeasure(fallback.Id, round.Config.GlassCapacity * 0.4) > 0;
            }

            // Recipe seeking (GDD 21 §9): a player who wants score pours a recipe's bands
            // with bottles that also move the intent. The bot does the same — for each
            // pourable recipe, staff every band with the best-aligned bottle of that type,
            // pour at the band midpoints, and size the glass for the recipe's multiplier.
            double capacity = round.Config.GlassCapacity;
            RecipeDefinition bestRecipe = null;
            List<(ShelfBottle bottle, double share)> bestPlan = null;
            double bestVolume = 0;

            foreach (var recipe in recipes)
            {
                if (recipe.RatioRequirements.Count == 0) continue;

                var plan = new List<(ShelfBottle bottle, double share)>();
                double mixPerGlass = 0;
                bool staffed = true;
                foreach (var band in recipe.RatioRequirements)
                {
                    ShelfBottle pick = null;
                    double pickCharge = 0;
                    foreach (var bottle in shelf.Bottles)
                    {
                        if (bottle.IsEmpty || bottle.Ingredient.Type != band.Type) continue;
                        double charge = ChargeOn(bottle.Ingredient, intent);
                        // Prefer the bottle pushing the intent the right way; failing that,
                        // the most neutral one this type offers.
                        bool pickAligned = pick != null && Math.Sign(pickCharge) == Math.Sign(needed);
                        bool aligned = Math.Sign(charge) == Math.Sign(needed);
                        if (pick == null ||
                            (aligned && (!pickAligned || Math.Abs(charge) > Math.Abs(pickCharge))) ||
                            (!aligned && !pickAligned && Math.Abs(charge) < Math.Abs(pickCharge)))
                        {
                            pick = bottle;
                            pickCharge = charge;
                        }
                    }
                    if (pick == null) { staffed = false; break; }

                    double share = (band.MinRatio + band.MaxRatio) / 2.0;
                    plan.Add((pick, share));
                    mixPerGlass += pickCharge * share;
                }
                if (!staffed || Math.Sign(mixPerGlass) != Math.Sign(needed)) continue;

                double mult = Math.Min(EmotionResolver.MaxChargeMultiplier, recipe.ChargeMultiplier);
                double mixVolume = needed / (mixPerGlass * mult);
                // A full glass that still undershoots is fine — serve it and close the gap
                // with the next drink. Overshooting is the only thing never allowed.
                mixVolume = Math.Min(mixVolume, capacity);
                if (mixVolume < recipe.MinFill * capacity) continue;

                bool pourable = true;
                foreach (var (bottle, share) in plan)
                    if (mixVolume * share > bottle.Remaining) { pourable = false; break; }
                if (!pourable) continue;

                // All valid candidates land exactly on the needed movement, so the tie
                // breaks on score: the highest-ranked recipe pays the most.
                if (bestRecipe == null || recipe.Rank > bestRecipe.Rank)
                {
                    bestRecipe = recipe;
                    bestPlan = plan;
                    bestVolume = mixVolume;
                }
            }

            if (bestPlan != null)
            {
                bool poured = false;
                foreach (var (bottle, share) in bestPlan)
                    poured |= round.PourMeasure(bottle.Id, bestVolume * share) > 0;
                return poured;
            }

            // Single bottle: no recipe will match a one-ingredient glass, so charges land
            // on the ×0.5 no-recipe path.
            double effective = bestPerGlass * PourResolver.NoRecipeMultiplier;
            double volume = needed / effective;

            // Never knowingly overshoot: the glass is the ceiling and so is the target.
            volume = Math.Min(volume, capacity);
            volume = Math.Min(volume, best.Remaining);
            if (volume <= 0) return false;

            return round.PourMeasure(best.Id, volume) > 0;
        }

        private static double ChargeOn(IngredientCard card, Emotion emotion)
        {
            if (card?.Charges == null) return 0;
            double total = 0;
            foreach (var charge in card.Charges)
                if (charge.Emotion == emotion) total += charge.Amount;
            return total;
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

        private sealed class Aggregate
        {
            public int Runs, Won, Lost, Stuck;
            public int Customers, OrdersFilled, Mixes, Busts, CleanServes, BlindReads;
            public int Satisfaction, NightsReached;

            private readonly Dictionary<int, int> _weekAttempts = new Dictionary<int, int>();
            private readonly Dictionary<int, int> _weekPasses = new Dictionary<int, int>();

            /// <summary>Satisfaction actually earned in each closed week, per week number.</summary>
            private readonly Dictionary<int, List<int>> _weekEarned = new Dictionary<int, List<int>>();

            private readonly Dictionary<DemandLevel, List<int>> _byDemand =
                new Dictionary<DemandLevel, List<int>>();

            public void RecordDemand(DemandLevel demand, int satisfaction)
            {
                if (!_byDemand.TryGetValue(demand, out var list))
                    _byDemand[demand] = list = new List<int>();
                list.Add(satisfaction);
            }

            private string DemandBreakdown()
            {
                var sb = new StringBuilder();
                sb.AppendLine("## How hard customers were to please");
                sb.AppendLine();
                sb.AppendLine("| Demand | Customers | Satisfaction each |");
                sb.AppendLine("|---|---|---|");
                foreach (DemandLevel demand in Enum.GetValues(typeof(DemandLevel)))
                {
                    if (!_byDemand.TryGetValue(demand, out var list) || list.Count == 0) continue;
                    sb.AppendLine($"| {demand} | {Pct(list.Count, Customers)} | " +
                                  $"{list.Average():0.00} |");
                }
                return sb.ToString();
            }

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
                sb.AppendLine("Greedy one-ply bot reading only the ID; it refills the well but buys");
                sb.AppendLine("nothing else in the Back Room. Every survival figure is therefore a floor.");
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
                sb.Append(DemandBreakdown());
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
