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
    /// a prediction — it never chases mood tips deliberately and never buys brands.
    /// </summary>
    public static class TycoonSimulator
    {
        private const int DayCap = 30;                 // the endless game needs a horizon to report on
        private const double DrinkBuildSeconds = 9.0;  // roughly a competent player, not a machine

        [MenuItem("LastCall/Simulate Tycoon 200 Runs")]
        public static void Simulate200() => Simulate(200);

        public static void Simulate(int runs)
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var archetypes = DataLoader.ParseArchetypes(Read("customers/archetypes.json"));

            var stats = new Aggregate();
            for (int i = 0; i < runs; i++)
                PlayRun($"TYC-{i:0000}", deck, recipes, archetypes, stats);

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
            Aggregate stats)
        {
            var starting = deck.Cards
                .Where(c => c.Info == null || c.Info.Tier <= 1)
                .Select(c => c.Clone()).ToList();
            var shelf = new Shelf(starting.Select(c => new ShelfBottle(c)));
            var run = new TycoonRun(shelf, recipes, new RunRng(seed),
                regulars: new RegularsRegistry(archetypes));

            double buildTimer = DrinkBuildSeconds;
            int guard = 0;
            while (run.Phase != TycoonPhase.Closed && run.Ledger.History.Count < DayCap)
            {
                if (guard++ > 300_000) { stats.Stuck++; return; }

                if (run.Phase == TycoonPhase.DayOpen)
                {
                    run.Tick(1.0);
                    buildTimer += 1.0;
                    if (buildTimer < DrinkBuildSeconds) continue;

                    foreach (var visit in run.Floor.Seated)
                    {
                        if (visit.State != VisitState.Waiting) continue;
                        if (!BuildOrderedDrink(run, visit)) continue;
                        var verdict = run.ServeTo(visit);
                        stats.RecordServe(verdict);
                        buildTimer = 0;
                        break;
                    }
                }
                else
                {
                    foreach (var visit in run.Floor.Finished)
                        if (visit.State == VisitState.StormedOff) stats.StormOffs++;
                    stats.CustomersFinished += run.Floor.Finished.Count;

                    int refill = run.Shelf.RefillCost(run.Config.RefillPricePerCapacity);
                    if (refill > 0 && run.Money >= refill) run.RefillShelf();
                    if (run.Seats < run.Config.MaxSeats &&
                        run.Money >= run.Config.SeatPrice(run.Seats) + 40) run.BuySeat();

                    stats.RecordDay(run.ContinueToNextDay());
                }
            }

            stats.Runs++;
            stats.DaysSurvived.Add(run.Ledger.History.Count);
            stats.FinalMoney.Add(run.Money);
            if (run.Phase == TycoonPhase.Closed) stats.Bankruptcies++;
        }

        /// <summary>
        /// Builds the ordered recipe at band midpoints. Bottle choice inside a type prefers
        /// charges aligned with the seat's visible intent — the same information a player
        /// has without opening the licence.
        /// </summary>
        private static bool BuildOrderedDrink(TycoonRun run, CustomerVisit visit)
        {
            run.DiscardGlass();
            var recipe = visit.Order.Wanted;
            double volume = Math.Max(recipe.MinFill, 0.85) * run.Glass.Capacity;

            foreach (var band in recipe.RatioRequirements)
            {
                var bottle = PickBottle(run.Shelf, band.Type, visit);
                if (bottle == null) return false;
                double share = (band.MinRatio + band.MaxRatio) / 2.0;
                run.PourMeasure(bottle.Id, Math.Min(volume * share, bottle.Remaining));
            }
            return !run.Glass.IsEmpty;
        }

        private static ShelfBottle PickBottle(Shelf shelf, IngredientType type, CustomerVisit visit)
        {
            ShelfBottle best = null;
            double bestScore = double.MinValue;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty || bottle.Ingredient.Type != type) continue;

                double score = 0;
                if (visit.Read != null)
                {
                    double charge = ChargeOn(bottle.Ingredient, visit.Read.Intent);
                    score = visit.Read.Direction == IntentDirection.Extinguish ? -charge : charge;
                }
                if (best == null || score > bestScore) { best = bottle; bestScore = score; }
            }
            return best;
        }

        private static double ChargeOn(IngredientCard card, Emotion emotion)
        {
            if (card?.Charges == null) return 0;
            double total = 0;
            foreach (var charge in card.Charges)
                if (charge.Emotion == emotion) total += charge.Amount;
            return total;
        }

        // ── bookkeeping ─────────────────────────────────────────────────────────

        private sealed class Aggregate
        {
            public int Runs, Stuck, Bankruptcies, StormOffs, CustomersFinished;
            public int Serves, Exact, Close, Wrong, CraftServes, SpeedTips, ExtraOrders;
            public double SatisfactionSum;
            public long IncomeSum, ExpenseSum;
            public int DaysClosed;
            public readonly List<int> DaysSurvived = new List<int>();
            public readonly List<int> FinalMoney = new List<int>();
            public readonly Dictionary<int, (int reds, int closes)> ByDay =
                new Dictionary<int, (int, int)>();

            public void RecordServe(ServiceVerdict verdict)
            {
                Serves++;
                if (verdict.Match == OrderMatch.Exact) Exact++;
                else if (verdict.Match == OrderMatch.Close) Close++;
                else Wrong++;
                if (verdict.CraftLanded) CraftServes++;
                if (verdict.OrdersAgain) ExtraOrders++;
            }

            public void RecordDay(DayResult result)
            {
                DaysClosed++;
                IncomeSum += result.Income;
                ExpenseSum += result.Expenses;
                SatisfactionSum += result.AverageSatisfaction;
                ByDay.TryGetValue(result.Day, out var row);
                ByDay[result.Day] = (row.reds + (result.Net < 0 ? 1 : 0), row.closes + 1);
            }

            public string Report(int requested)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Tycoon sim report — GDD 23 balance");
                sb.AppendLine();
                sb.AppendLine($"Runs: **{Runs}** of {requested}" +
                              (Stuck > 0 ? $" ({Stuck} abandoned as stuck)" : "") +
                              $", horizon {DayCap} days, one drink per {DrinkBuildSeconds:0}s of bar time.");
                sb.AppendLine("Floor bot: serves the named order at band midpoints, never chases");
                sb.AppendLine("mood tips, never buys brands. Every survival figure is a floor.");
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
                sb.AppendLine($"| Serves Exact / Close / Wrong | {Pct(Exact, Serves)} / {Pct(Close, Serves)} / {Pct(Wrong, Serves)} |");
                sb.AppendLine($"| Garnish craft landed | {Pct(CraftServes, Serves)} |");
                sb.AppendLine($"| Extra orders earned (of serves) | {Pct(ExtraOrders, Serves)} |");
                sb.AppendLine($"| Extra orders earned (of exact) | {Pct(ExtraOrders, Exact)} |");
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

            private static int Q(List<int> values, double q)
            {
                if (values.Count == 0) return 0;
                var sorted = values.OrderBy(v => v).ToList();
                return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];
            }
        }
    }
}
