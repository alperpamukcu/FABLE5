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
    /// a prediction — it never buys brands.
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

        /// <summary>Pins the editor's number culture too. The report below is a checked-in
        /// document; without this it reads "$133,2" on one desktop and "$133.2" on another,
        /// and every regeneration shows up as a diff nobody made.</summary>
        [InitializeOnLoadMethod]
        private static void PinCulture() => RunCulture.Pin();

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

            var stats = new Aggregate();
            for (int i = 0; i < runs; i++)
                PlayRun($"TYC-{i:0000}", deck, recipes, archetypes, stats,
                    DrinkBuildSeconds, DayCap, glassware, snacks);

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
            IReadOnlyList<SnackDefinition> snacks = null)
        {
            var starting = deck.Cards
                .Where(c => c.Info == null || c.Info.Tier <= 1)
                .Select(c => c.Clone()).ToList();
            var shelf = new Shelf(starting.Select(c => new ShelfBottle(c)));
            // The quarantined bottles ride along (P16): buying a recipe releases its styles
            // into the market, and without this the sim's bought menu was undrinkable — half
            // of every night silently declined for want of a tonic that existed nowhere.
            var run = new TycoonRun(shelf, recipes, new RunRng(seed),
                regulars: new RegularsRegistry(archetypes), glassware: glassware, snacks: snacks,
                lockedStock: deck.LockedCards);

            double buildTimer = buildSeconds;
            int guard = 0;
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
                        if (stats.Serves % 3 == 0 && run.Snacks.Count > 0)
                        {
                            var snack = run.Snacks[(stats.Serves / 3) % run.Snacks.Count];
                            if (run.SnackLeft(snack.Id) > 0)
                            {
                                run.ServeSnack(snack.Id, visit);
                                stats.SnackServes++;
                                stats.SnackIncome += snack.Price;
                            }
                        }
                        if (!BuildOrderedDrink(run, visit)) continue;
                        bool pint = run.ServingGlass.HasPreparation(Preparations.Draught.Id);
                        double head = pint ? run.ServingGlass.Head / run.ServingGlass.Capacity : 0;
                        int specRequests = visit.Order.Spec.RequestCount;
                        var verdict = run.ServeTo(visit);
                        stats.RecordServe(verdict, pint, head, specRequests);
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
                    // No purchase in the last week: a recipe bought on day 26 cannot earn
                    // its price back, and a floor that buys it measures generosity, not play.
                    // Wave 3 taught this — five more buyables took the floor from 7.5% to
                    // 42.5% bankruptcies on late-run purchases alone.
                    if (cheapest != null && run.Day <= 23 &&
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
                    if (run.Seats < run.Config.MaxSeats &&
                        run.Money >= run.Config.SeatPrice(run.Seats) + 40) run.BuySeat();

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
                    if (bestGlass != null && run.Money >= bestPrice + cushion)
                        run.BuyGlassTier(bestGlass.Id);

                    stats.RecordNight(run.Floor.Elapsed, run.Rating.LastNight);
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
            double volume = Math.Max(recipe.MinFill, 0.85)
                            * Math.Min(glassCap, run.Glass.Capacity);
            // Carbonated bands go in AT THE GLASS (C8): Core refuses them in the shaker, so
            // the bot builds the way the player does — still parts into the tin, fizz after
            // the pour-out. The old menu had no carbonated style bands, so the first Gin &
            // Tonic the bot ever built CRASHED the whole batch here (2026-07-31).
            var atGlass = new List<(string id, double vol)>();
            // The EXACT pour the book prints (2026-08-02), not the bands' raw midpoints:
            // those total 103% on a Gin Sour and 94% on an Espresso Martini, so the bot was
            // measuring a bartender who cannot add up. IdealPour is inside every band and
            // fills the glass, which is what the card now tells the player to do.
            var ideal = RatioRecipeMatcher.IdealPour(recipe);
            for (int bi = 0; bi < recipe.RatioRequirements.Count; bi++)
            {
                var band = recipe.RatioRequirements[bi];
                var bottle = band.IsStyleBand
                    ? PickByStyle(run.Shelf, band.Style)
                    : PickBottle(run.Shelf, band.Type, visit);
                if (bottle == null) return false;
                double share = ideal[bi];
                double amount = Math.Min(volume * share, bottle.Remaining);
                if (bottle.Ingredient.Info?.Carbonated == true)
                    atGlass.Add((bottle.Id, amount));
                else
                    run.PourMeasure(bottle.Id, amount);
            }
            if (run.Glass.IsEmpty && atGlass.Count == 0) return false;

            foreach (var garnish in spec.Garnishes)
                if (!run.Glass.IsFull) run.AddPreparation(garnish);
            if (spec.ExtraShaken) run.Shake(1.0);

            // Into the glass, dead on the rim. The bot used to hand the shaker over whole, which
            // the rules now refuse (2026-07-28); pouring perfectly keeps its standing unchanged —
            // it never had to aim, and the aim is the player's skill, not the floor's.
            if (!run.Glass.IsEmpty)
                run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            foreach (var (id, vol) in atGlass)
                run.PourAtGlass(id, vol);
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
        private static ShelfBottle PickByStyle(Shelf shelf, string style)
        {
            ShelfBottle best = null;
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.IsEmpty || bottle.Ingredient.Info?.Style != style) continue;
                if (best == null || bottle.Remaining > best.Remaining) best = bottle;
            }
            return best;
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
            // v5 P11: the base/tip split is the phase's whole point, and refusals/declines are
            // the two new ways a serve can end.
            public int Refused, Declined, SpecOrders, SpecFull;
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
                sb.AppendLine("Floor bot: serves the named order at band midpoints, pulls a pint");
                sb.AppendLine("leaned over then straightened, and never buys brands.");
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
                sb.AppendLine($"| Recipes bought (of 200 runs) | {RecipesBought} |");
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
