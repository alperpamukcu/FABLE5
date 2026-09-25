using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE ROOM'S BUFFS (2026-09-23, the author: "Tüm upgrade'ler çeşitli bufflar vermeli ... kimi
    /// upgrade ürün fiyatını bufflar, konforu bufflar, bekleme süresini bufflar, tip'i bufflar,
    /// servisi bufflar ... Hangi geliştirme takılıysa o buff aktif olacak, konfor gibi değil").
    ///
    /// Three things are pinned here. The IDENTITY: a room with no buffs is judged, priced, paced and
    /// drawn exactly as it was before the buffs existed, the same draws in the same order — the
    /// safety argument the page characters were built on. The INSTALLED rule: a buff reads the rung
    /// a slot wears, comfort reads the rung it climbed to, and the wardrobe closes when the doors
    /// open. And each KIND, measured the only honest way: one seed, twice, with and without the one
    /// piece that carries it, and exactly its number moving.
    ///
    /// Pure Core and a synthetic room, so it runs without the editor. The shipped catalogue's own
    /// pins — every row names its kind, the full room lands on every cap — are in
    /// FittingBuffDataTests, which needs the loader.
    /// </summary>
    public sealed class FittingBuffTests
    {
        // ── scaffolding: a shelf, one page, and a room built like the shipped one ──────────────

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 60),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 60),
        });

        private static readonly Dictionary<string, IngredientCard> Bar =
            new Dictionary<string, IngredientCard>
            {
                ["gin"] = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6),
                ["soda"] = new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1),
            };

        private static IngredientCard Look(string id) => Bar.TryGetValue(id, out var c) ? c : null;

        /// <summary>A rank-22 page: $32 on the sheet, so a twelfth more moves the till by dollars
        /// rather than by a rounding error.</summary>
        private static RecipeDefinition Page(string trait = null, int rank = 22) =>
            new RecipeDefinition("long_" + (trait ?? "plain"), "Long One", rank,
                baseFlavor: 10, baseMult: 2, flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, prep: PrepMethod.Built, trait: trait);

        private static FixtureDefinition Piece(string id, string slot, int level, string buff, int pct,
            bool starts = false, double stars = 0, double comfort = 0, bool drain = false,
            bool free = false, double wash = 0, double work = 0, int tap = 0) =>
            new FixtureDefinition(id, id, slot, 20 + 5 * Math.Max(1, level + tap), stars, "", "fx_" + id,
                startsInTheRoom: starts, tapLevel: tap, level: tap > 0 ? 0 : level,
                isDrain: drain, drainsFree: free, comfort: comfort, washSeconds: wash, workSpeed: work,
                buff: buff, buffPct: pct);

        /// <summary>A room built the way fixtures.json is: every slot carries one kind, the pieces
        /// the room opens with carry it at 0, and one bought piece per kind sits at its cap.</summary>
        private static FixtureDefinition[] Room() => new[]
        {
            Piece("walls_1", "walls", 1, "arrivals", 0, starts: true),
            Piece("walls_2", "walls", 2, "arrivals", 4, comfort: 1.0),
            Piece("walls_3", "walls", 3, "arrivals", 10, comfort: 2.0),
            Piece("rwall_1", "walls_right", 1, "comfort", 0, starts: true),
            Piece("rwall_2", "walls_right", 2, "comfort", 10, comfort: 0.5),
            Piece("ceil", "ceiling", 1, "refill", 20, comfort: 0.25),
            Piece("floor", "floor", 1, "grace", 60, comfort: 0.3),
            Piece("kit", "counter_paint", 0, "grace", 20),
            Piece("mat", "beer_mat", 0, "grace", 0, starts: true),
            Piece("rug", "floor_rug", 1, "patience", 12, comfort: 0.2),
            Piece("palm", "plant_left", 1, "patience", 5, comfort: 0.1),
            Piece("tv", "wall_tv", 1, "lateness", -20, comfort: 0.2),
            Piece("art", "wall_right_art", 1, "late_tip", 20, comfort: 0.15),
            Piece("pic", "wall_center", 1, "service", 5, comfort: 0.2),
            Piece("neon", "wall_right", 1, "price", 12, comfort: 0.2),
            Piece("lamp", "wall_lamps", 1, "tip", 15, comfort: 0.1),
            Piece("pend_1", "counter_lamps", 1, "window", 0, starts: true),
            Piece("pend_2", "counter_lamps", 2, "window", 40, comfort: 0.3),
            Piece("table_L1", "table_left", 1, "round", 0, starts: true),
            Piece("table_L2", "table_left", 2, "round", 5, comfort: 0.25),
            Piece("table_R1", "table_right", 1, "round", 0, starts: true),
            Piece("table_R2", "table_right", 2, "round", 5, comfort: 0.25),
            Piece("tower", "taps", 0, "pour_speed", 0, starts: true, tap: 1),
            Piece("sink_old", "sink", 1, "wash", 0, starts: true, drain: true, wash: 3.4),
            Piece("counter_sink", "sink", 2, "wash", 0, drain: true, wash: 3.0, comfort: 0.15),
            Piece("sink_brass", "sink", 3, "wash", 0, drain: true, free: true, wash: 2.5, comfort: 0.4),
            Piece("tin_steel", "shaker", 1, "shake_speed", 0, starts: true),
            Piece("tin_gold", "shaker", 2, "shake_speed", 0, work: 1.5, comfort: 0.4),
        };

        private static TycoonConfig Cfg(int money = 500) =>
            new TycoonConfig(money, orderDecisionSeconds: 0, savorSeconds: 0);

        private static TycoonRun NewRun(string seed, RunRng rng = null, string trait = null,
            IReadOnlyList<FixtureDefinition> fixtures = null, int money = 500) =>
            new TycoonRun(NewShelf(), new[] { Page(trait) }, rng ?? new RunRng(seed), config: Cfg(money),
                fixtures: fixtures ?? Room());

        private static TycoonRun RunAtDayEnd(string seed = "house-end", IReadOnlyList<FixtureDefinition> fixtures = null)
        {
            var run = NewRun(seed, fixtures: fixtures);
            run.DevSkipToDayEnd();
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            return run;
        }

        /// <summary>Ticks until somebody is seated and has ordered; returns the first of them.</summary>
        private static CustomerVisit FirstArrival(TycoonRun run, double step = 0.25)
        {
            for (int guard = 0; guard < 2000; guard++)
            {
                var sat = run.Floor.Seated.FirstOrDefault(v => v.State == VisitState.Waiting && v.HasOrdered);
                if (sat != null) return sat;
                run.Tick(step);
            }
            Assert.Fail("nobody came in");
            return null;
        }

        /// <summary>Pours the page's middle, half and half, and hands it over.</summary>
        private static ServiceVerdict Serve(TycoonRun run, CustomerVisit visit)
        {
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            return run.ServeTo(visit);
        }

        /// <summary>Plays a whole night, serving everyone and keeping the counter; returns how many
        /// serves were handed over (each one draws the "round" stream once).</summary>
        private static int PlayNight(TycoonRun run)
        {
            int serves = 0;
            for (int guard = 0; run.Phase == TycoonPhase.DayOpen; guard++)
            {
                Assert.Less(guard, 3000, "the night must end");
                run.Tick(1.0);
                TestNight.Clean(run);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    if (visit.State != VisitState.Waiting || !visit.HasOrdered) continue;
                    Serve(run, visit);
                    serves++;
                }
            }
            return serves;
        }

        private static GlassContents Glass(double gin, double soda)
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", gin);
            glass.Add("soda", soda);
            return glass;
        }

        /// <summary>A glass off the page's perfect by <paramref name="miss"/> on both ingredients.</summary>
        private static GlassContents OffThePerfect(RecipeDefinition page, double miss)
        {
            var perfect = RatioRecipeMatcher.PerfectPour(page);
            return Glass((perfect[0] + miss) * 0.9, (perfect[1] - miss) * 0.9);
        }

        private static CustomerVisit Visit(RecipeDefinition page, double waited = 0, int price = 60)
        {
            var visit = new CustomerVisit(new DrinkOrder(page, price), 60);
            visit.InspectId();
            if (waited > 0) visit.Tick(60 * visit.PatienceFraction * waited);
            return visit;
        }

        private static ServiceVerdict JudgeIt(CustomerVisit visit, GlassContents glass, HouseBuffs house,
            WealthTier crowd = WealthTier.Regular, double ambience = 0)
        {
            var match = RatioRecipeMatcher.Match(glass, new[] { visit.Order.Wanted }, Look);
            return ServiceJudge.Judge(visit, OrderMatch.Exact, glass, crowd, ambience,
                served: match, lookup: Look, house: house);
        }

        private static HouseBuffs HouseOf(params FixtureDefinition[] pieces) => HouseBuffs.From(pieces);

        private static FixtureDefinition RoomPiece(string id) => Room().First(f => f.Id == id);

        private static void AssertSameVerdict(ServiceVerdict a, ServiceVerdict b, string why)
        {
            Assert.AreEqual(a.Match, b.Match, why);
            Assert.AreEqual(a.BasePaid, b.BasePaid, why);
            Assert.AreEqual(a.Tip, b.Tip, why);
            Assert.AreEqual(a.CraftLanded, b.CraftLanded, why);
            Assert.AreEqual(a.OrdersAgain, b.OrdersAgain, why);
            Assert.AreEqual(a.Satisfaction, b.Satisfaction, why);   // exact: the doubles are the same doubles
            Assert.AreEqual(a.SpecScore, b.SpecScore, why);
            Assert.AreEqual(a.FillScore, b.FillScore, why);
            Assert.AreEqual(a.Accuracy, b.Accuracy, why);
            Assert.AreEqual(a.PerfectMake, b.PerfectMake, why);
        }

        private static void AssertSameLedger(DayResult a, DayResult b, string why)
        {
            Assert.AreEqual(a.Day, b.Day, why);
            Assert.AreEqual(a.Income, b.Income, why);
            Assert.AreEqual(a.Expenses, b.Expenses, why);
            Assert.AreEqual(a.Sales, b.Sales, why);
            Assert.AreEqual(a.Tips, b.Tips, why);
            Assert.AreEqual(a.Rent, b.Rent, why);
            Assert.AreEqual(a.Stock, b.Stock, why);
            Assert.AreEqual(a.Served, b.Served, why);
            Assert.AreEqual(a.WalkedOut, b.WalkedOut, why);
            Assert.AreEqual(a.AverageSatisfaction, b.AverageSatisfaction, why);
            Assert.AreEqual(a.NightStars, b.NightStars, why);
            Assert.AreEqual(a.ServiceStars, b.ServiceStars, why);
            Assert.AreEqual(a.ComfortStars, b.ComfortStars, why);
            Assert.AreEqual(a.WalkOutFees, b.WalkOutFees, why);
            Assert.AreEqual(a.TillAfter, b.TillAfter, why);
        }

        // ── identity ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheCatalogue_IsTwelveDataKindsAndFourTools()
        {
            Assert.AreEqual(16, FittingBuffs.All.Count);
            Assert.AreEqual(12, FittingBuffs.DataKinds.Count);
            Assert.AreEqual(FittingBuffs.All.Count, FittingBuffs.All.Select(k => k.Id).Distinct().Count(), "ids are unique");
            foreach (var k in FittingBuffs.All)
            {
                Assert.AreEqual(k.Id, k.StatKey, "one word for the data, the string table and the icon");
                Assert.AreNotEqual(TraitChannel.None, k.Channel, k.Id + " pulls one of the menu's levers");
                Assert.AreSame(k, FittingBuffs.Find(k.Id));
                Assert.IsTrue(FittingBuffs.Known(k.Id));
                bool data = FittingBuffs.DataKinds.Contains(k);
                Assert.AreEqual(!data, k.IsTool, k.Id);
                if (data)
                {
                    Assert.AreEqual(1, Math.Abs(k.Direction), k.Id + " points one way");
                    Assert.Greater(k.MaxPerPiece, 0, k.Id);
                    Assert.LessOrEqual(k.MaxPerPiece, k.HouseCap, k.Id + ": one piece cannot pass the room");
                }
                else
                {
                    Assert.AreEqual(0, k.MaxPerPiece, k.Id + ": a tool's figure is never written");
                    Assert.AreEqual(0, k.HouseCap, k.Id);
                }
                Assert.AreEqual(k == FittingBuffs.Arrivals || k == FittingBuffs.Round, k.WhileKeepingUp,
                    k.Id + ": only the greed kinds wait for a free bartender");
            }
            Assert.AreEqual(BuffUnit.Points, FittingBuffs.Round.Unit);
            Assert.AreEqual(BuffUnit.Points, FittingBuffs.Service.Unit);
            Assert.AreEqual(BuffUnit.Flag, FittingBuffs.FreeDrain.Unit);
            Assert.AreEqual(-1, FittingBuffs.Lateness.Direction);
            Assert.AreEqual(-1, FittingBuffs.Wash.Direction);
            Assert.IsNull(FittingBuffs.Find("nope"));
            Assert.IsNull(FittingBuffs.Find(null));
            Assert.IsFalse(FittingBuffs.Known(""));
        }

        [Test]
        public void None_IsEveryIdentity()
        {
            var none = HouseBuffs.None;
            Assert.IsTrue(none.IsNone);
            Assert.AreEqual(1.0, none.PriceScale);
            Assert.AreEqual(1.0, none.TipScale);
            Assert.AreEqual(1.0, none.PatienceScale);
            Assert.AreEqual(1.0, none.WaitPenaltyScale);
            Assert.AreEqual(1.0, none.ClockFloorScale);
            Assert.AreEqual(1.0, none.RefillScale);
            Assert.AreEqual(1.0, none.ArrivalGapScale);
            Assert.AreEqual(1.0, none.ComfortScale);
            Assert.AreEqual(1.0, none.GraceScale);
            Assert.AreEqual(1.0, none.PerfectWindowScale);
            Assert.AreEqual(0.0, none.RoundChance);
            Assert.AreEqual(0.0, none.ServiceBonus);
            foreach (var k in FittingBuffs.All)
            {
                Assert.AreEqual(0, none.Percent(k), k.Id);
                Assert.AreEqual(0, none.RawPercent(k), k.Id);
                Assert.AreEqual(0, none.Sources(k).Count, k.Id);
            }

            Assert.IsTrue(HouseBuffs.From(Array.Empty<FixtureDefinition>()).IsNone, "an empty room is the standard");
            Assert.IsTrue(HouseBuffs.From(Room().Where(f => f.StartsInTheRoom)).IsNone,
                "and so is the room as it opens: every piece in it is BASE");
            Assert.IsFalse(HouseBuffs.From(Room()).IsNone);
            try
            {
                HouseBuffs.Enabled = false;
                Assert.AreSame(HouseBuffs.None, HouseBuffs.From(Room()), "the sim's gate turns the room off whole");
            }
            finally { HouseBuffs.Enabled = true; }
        }

        [Test]
        public void ARoomWithNoBuffs_IsJudgedExactlyAsBefore()
        {
            foreach (var trait in new[] { null, "they_tip_for_this", "keeps_well", "easy_to_learn" })
                foreach (double waited in new[] { 0.0, 0.6 })
                {
                    var page = Page(trait);
                    var glass = OffThePerfect(page, 0.01);
                    var visit = Visit(page, waited);
                    var before = JudgeIt(visit, glass, null);
                    string why = (trait ?? "plain") + " at " + waited;
                    AssertSameVerdict(before, JudgeIt(visit, glass, HouseBuffs.None), why);
                    AssertSameVerdict(before, JudgeIt(visit, glass, HouseBuffs.From(Room().Where(f => f.StartsInTheRoom))), why);
                }
        }

        [Test]
        public void TheRoomAsItOpens_IsTodaysRoom()
        {
            // A synthetic room shaped like the shipped one: the pieces it opens with carry their
            // kind at 0, and every buffed piece is still on the truck. The shipped catalogue's own
            // version of this is FittingBuffDataTests'.
            var fresh = NewRun("opening");
            foreach (var k in FittingBuffs.All) Assert.AreEqual(0, fresh.Buffs.Percent(k), k.Id);
            Assert.AreEqual(3.4, fresh.SinkSeconds, 1e-12, "the old sink the room opens with");
            Assert.AreEqual(Housekeeping.DirtGrace, fresh.Floor.House.Grace);

            // The order is behind the card: it is read the way the licence reads it, then compared.
            var on = PlayRead(true);
            var off = PlayRead(false);
            Assert.AreEqual(off.price, on.price, "the first order's price");
            Assert.AreEqual(off.patience, on.patience, "PatienceMax");
            Assert.AreEqual(off.arrivedAt, on.arrivedAt, "the first arrival's time");
            Assert.AreEqual(off.comfort, on.comfort, "ComfortNow");
            AssertSameLedger(off.night, on.night, "a whole skipped night");

            (int price, double patience, double arrivedAt, double comfort, DayResult night) PlayRead(bool enabled)
            {
                bool was = HouseBuffs.Enabled;
                try
                {
                    HouseBuffs.Enabled = enabled;
                    var run = NewRun("opening");
                    var first = FirstArrival(run, 0.05);
                    double at = run.Floor.Elapsed;
                    first.InspectId();
                    int p = first.Order.Price;
                    double comfort = run.ComfortNow;
                    run.DevSkipToDayEnd();
                    return (p, first.PatienceMax, at, comfort, run.ContinueToNextDay());
                }
                finally { HouseBuffs.Enabled = was; }
            }
        }

        // ── one per kind ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Price_ScalesTheTillAndTheBook()
        {
            var bare = NewRun("price");
            var lit = NewRun("price");
            lit.DevFit("neon");
            Assert.AreEqual(12, lit.Buffs.Percent(FittingBuffs.Price));

            var a = FirstArrival(bare); a.InspectId();
            var b = FirstArrival(lit); b.InspectId();
            var page = a.Order.Wanted;
            int menu = DrinkOrder.MenuPrice(page, bare.ShopStars);
            Assert.AreEqual(32, menu, "the fixture's page is a $32 page");
            Assert.AreEqual(menu, a.Order.Price, "a regular crowd, no premium: the bare till is the sheet");
            Assert.AreEqual((int)Math.Round(menu * 1.0 * 1.12, MidpointRounding.AwayFromZero), b.Order.Price,
                "the neon scales the whole ticket");
            Assert.AreEqual(36, b.Order.Price);

            Assert.AreEqual(menu, bare.PagePrice(page), "bare, the page prints the sheet it always did");
            Assert.AreEqual((int)Math.Round(menu * 1.12, MidpointRounding.AwayFromZero), lit.PagePrice(page),
                "and under the neon it prints what the drinker pays");
            Assert.AreEqual(b.Order.Price, lit.PagePrice(page));
            Assert.AreEqual(0, lit.PagePrice(null));
        }

        [Test]
        public void Tip_ScalesOnlyTheTip()
        {
            var page = Page();
            var glass = OffThePerfect(page, 0.0);
            var visit = Visit(page, 0.3, price: 80);
            var plain = JudgeIt(visit, glass, null);
            var lit = JudgeIt(visit, glass, HouseOf(RoomPiece("lamp")));
            Assert.Greater(plain.Tip, 20, "a tip big enough for a sixth of it to show");
            Assert.AreEqual(plain.BasePaid, lit.BasePaid, "the bill is the bill");
            Assert.AreEqual(plain.Satisfaction, lit.Satisfaction, "and nobody likes the drink more for it");
            Assert.AreEqual(plain.Tip * 1.15, lit.Tip, 1.1, "the ceiling is a sixth higher, give or take the rounding");
            Assert.Greater(lit.Tip, plain.Tip);
        }

        [Test]
        public void Patience_ScalesTheOneRoll()
        {
            var rngBare = new RunRng("patience");
            var rngRug = new RunRng("patience");
            var bare = NewRun("patience", rngBare);
            var rug = NewRun("patience", rngRug);
            rug.DevFit("rug");
            Assert.AreEqual(12, rug.Buffs.Percent(FittingBuffs.Patience));

            var a = FirstArrival(bare);
            var b = FirstArrival(rug);
            Assert.AreEqual(a.PatienceMax * 1.12, b.PatienceMax, "the same roll, scaled after it");
            Assert.AreEqual(bare.Floor.Elapsed, rug.Floor.Elapsed, "and the door kept its own time");
            Assert.AreEqual(rngBare.GetStream("patience").NextDouble(), rngRug.GetStream("patience").NextDouble(),
                "one draw each: the next value of the stream is the same one");
        }

        [Test]
        public void Lateness_SoftensOnlyTheWaitTerm()
        {
            var page = Page();
            var glass = OffThePerfect(page, 0.01);
            var visit = Visit(page, 0.6);
            var plain = JudgeIt(visit, glass, null);
            var screened = JudgeIt(visit, glass, HouseOf(RoomPiece("tv")));
            Assert.Greater(plain.Satisfaction, 0.0); Assert.Less(screened.Satisfaction, 1.0);
            Assert.AreEqual(ServiceJudge.WaitPenalty * 0.20 * visit.WaitFraction,
                screened.Satisfaction - plain.Satisfaction, 1e-12, "a fifth of what being late cost");
            Assert.AreEqual(plain.BasePaid, screened.BasePaid);
            Assert.AreEqual(plain.Tip, screened.Tip);
        }

        [Test]
        public void LateTip_PaysOnlyALateServe()
        {
            var page = Page();
            var glass = OffThePerfect(page, 0.0);
            var house = HouseOf(RoomPiece("art"));
            var onTime = Visit(page, 0.0, price: 80);
            Assert.AreEqual(JudgeIt(onTime, glass, null).Tip, JudgeIt(onTime, glass, house).Tip,
                "a serve on the instant is not late, and the floor is all it scales");
            var late = Visit(page, 0.8, price: 80);
            Assert.Greater(JudgeIt(late, glass, house).Tip, JudgeIt(late, glass, null).Tip,
                "a late one keeps more of its tip");
            Assert.AreEqual(JudgeIt(late, glass, null).Satisfaction, JudgeIt(late, glass, house).Satisfaction);
        }

        [Test]
        public void Refill_StartsTheSecondRoundFuller()
        {
            var again = new ServiceVerdict(OrderMatch.Exact, 30, 5, true, true, 0.9);
            var plain = new CustomerVisit(new DrinkOrder(Page(), 30), 60);
            var lit = new CustomerVisit(new DrinkOrder(Page(), 30), 60);
            plain.Resolve(again, new DrinkOrder(Page(), 30));
            lit.Resolve(again, new DrinkOrder(Page(), 30), refillScale: 1.2);
            Assert.AreEqual(Math.Min(60, 60 * CustomerVisit.ExtraOrderPatienceRefill), plain.PatienceLeft, 1e-12);
            Assert.AreEqual(Math.Min(60, 60 * CustomerVisit.ExtraOrderPatienceRefill * 1.2), lit.PatienceLeft, 1e-12,
                "the ceiling's refill starts the second round on a fuller clock");
            var full = new CustomerVisit(new DrinkOrder(Page(), 30), 60);
            full.Resolve(again, new DrinkOrder(Page(), 30), refillScale: 2.0);
            Assert.AreEqual(60, full.PatienceLeft, 1e-12, "and never past a full one");
        }

        // The round is decided on one draw of the "round" stream; which seed puts that draw where
        // is found, not assumed, so the test says what it means on any machine.
        private static string SeedWithFirstRound(double lo, double hi)
        {
            for (int i = 0; i < 5000; i++)
            {
                string seed = "round-" + i;
                double r = new RunRng(seed).GetStream("round").NextDouble();
                if (r >= lo && r < hi) return seed;
            }
            Assert.Fail($"no seed rolls a first round in [{lo}, {hi})");
            return null;
        }

        /// <summary>One serve on a fresh run: was a second round granted? The "round" stream is
        /// asserted to have been drawn exactly once, whatever the answer.</summary>
        private static bool RoundGranted(string seed, string trait, bool tables, bool secondWaiting)
        {
            var rng = new RunRng(seed);
            var run = NewRun(seed, rng, trait);
            if (tables) { run.DevFit("table_L2"); run.DevFit("table_R2"); }
            Assert.AreEqual(tables ? 0.10 : 0.0, run.Buffs.RoundChance, 1e-12);
            var first = FirstArrival(run);
            for (int guard = 0; secondWaiting && run.Floor.Waiting < 2; guard++)
            {
                Assert.Less(guard, 400, "a second drinker should have sat down");
                run.Tick(0.25);
            }
            Assert.AreEqual(secondWaiting, !run.Floor.KeepingUp, "the bar is keeping up unless somebody else waits");
            Assert.AreEqual(VisitState.Waiting, first.State);
            var verdict = Serve(run, first);
            Assert.AreEqual(OrderMatch.Exact, verdict.Match, "the round is only ever on the exact drink");

            var fresh = new RunRng(seed).GetStream("round");
            fresh.NextDouble();
            Assert.AreEqual(fresh.NextDouble(), rng.GetStream("round").NextDouble(),
                "the round stream is drawn once per resolved serve, granted or not");
            return verdict.OrdersAgain;
        }

        [Test]
        public void Round_RidesTheSameRoll_AndWaitsForAFreeBartender()
        {
            string inside = SeedWithFirstRound(0.0, 0.10);     // under the tables' 0.10
            Assert.IsFalse(RoundGranted(inside, null, tables: false, secondWaiting: false), "no tables, no round");
            Assert.IsTrue(RoundGranted(inside, null, tables: true, secondWaiting: false), "the tables grant it");
            Assert.IsFalse(RoundGranted(inside, null, tables: true, secondWaiting: true),
                "but not while somebody else is waiting");
            string outside = SeedWithFirstRound(0.10, 1.0);
            Assert.IsFalse(RoundGranted(outside, null, tables: true, secondWaiting: false),
                "and a roll past the chance is past it");
        }

        [Test]
        public void Service_AddsAtTheCallSite()
        {
            var bare = NewRun("service");
            var hung = NewRun("service");
            hung.DevFit("pic");
            var a = FirstArrival(bare);
            var b = FirstArrival(hung);
            bare.Tick(12); hung.Tick(12);     // a little late, so the sum sits well under the clamp
            var plain = Serve(bare, a);
            var lit = Serve(hung, b);
            Assert.Less(lit.Satisfaction, 1.0);
            Assert.AreEqual(0.05, lit.Satisfaction - plain.Satisfaction, 1e-9, "the picture's five points");
            Assert.AreEqual(plain.BasePaid, lit.BasePaid);
            Assert.AreEqual(plain.Tip, lit.Tip);

            var page = Page();
            var glass = OffThePerfect(page, 0.0);
            var visit = Visit(page);
            Assert.AreEqual(1.0, JudgeIt(visit, glass, null, ambience: 0.99 + 0.05).Satisfaction,
                "and the judge's clamp still holds the top");
        }

        [Test]
        public void Crowd_ShortensTheGap_WhileTheBarKeepsUp()
        {
            var cfg = Cfg();
            double scale = 1.0 / 1.10;
            double gap = cfg.ArrivalGap(1, BarRating.NeutralStars);
            var draws = new RunRng("crowd").GetStream("arrivals");
            double Jitter() => 1.0 + (draws.NextDouble() * 2.0 - 1.0) * TycoonConfig.ArrivalJitter;
            double j1 = Jitter(), j2 = Jitter(), j3 = Jitter();

            var page = Page();
            Func<CustomerVisit> sit = () => new CustomerVisit(new DrinkOrder(page, 10), 1000);
            var bare = new BarDay(1, 4, cfg, new RunRng("crowd").GetStream("arrivals"));
            var busy = new BarDay(1, 4, cfg, new RunRng("crowd").GetStream("arrivals"), gapScale: scale);
            Assert.IsTrue(busy.KeepingUp, "an empty bar is keeping up");

            // The same draw, scaled: the first gap is the bare gap over 1.10.
            double g1Bare = gap * 1.0 * j1, g1 = gap * scale * j1;
            Assert.AreEqual(g1Bare * scale, g1, 1e-12);
            ArrivesAt(bare, g1Bare, sit, 1);
            ArrivesAt(busy, g1, sit, 1);

            // One waiting when the next gap was rolled: still scaled.
            double t2 = g1 + gap * scale * j2;
            ArrivesAt(busy, t2, sit, 2);
            Assert.AreEqual(2, busy.Waiting);
            Assert.IsFalse(busy.KeepingUp, "two waiting is a bar behind");
            // Two waiting when the third gap was rolled: the room waits for the bartender.
            ArrivesAt(busy, t2 + gap * 1.0 * j3, sit, 3);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BarDay(1, 4, cfg, new RunRng("x").GetStream("arrivals"), gapScale: 0));

            // The plan is cut for the door the room makes...
            Assert.Greater(DayPlan.CoversFor(12, BarRating.NeutralStars, cfg, scale),
                DayPlan.CoversFor(12, BarRating.NeutralStars, cfg));
            Assert.AreEqual(DayPlan.CoversFor(12, BarRating.NeutralStars, cfg),
                DayPlan.CoversFor(12, BarRating.NeutralStars, cfg, 1.0), "and a bare room's plan is today's");
            // ...but the written week is written, and nothing in the room rewrites it.
            var book = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();
            for (int day = 1; day <= FirstWeek.Nights; day++)
            {
                var plain = DayPlan.Roll(book, day, 0.0, cfg, new RunRng("week").GetStream("plan"));
                var crowded = DayPlan.Roll(book, day, 0.0, cfg, new RunRng("week").GetStream("plan"), scale);
                CollectionAssert.AreEqual(plain.Queue.Select(r => r.Id), crowded.Queue.Select(r => r.Id),
                    "night " + day);
            }
        }

        /// <summary>Ticks the floor to a hair before the moment <paramref name="at"/> (seconds into the
        /// shift) and then a hair past it: the arrival lands between the two.</summary>
        private static void ArrivesAt(BarDay floor, double at, Func<CustomerVisit> sit, int expected)
        {
            floor.Tick(at - 1e-6 - floor.Elapsed, sit);
            Assert.AreEqual(expected - 1, floor.Arrived, "nobody before " + at);
            floor.Tick(2e-6, sit);
            Assert.AreEqual(expected, floor.Arrived, "somebody at " + at);
        }

        [Test]
        public void Comfort_CushionsTheFiledNightAfterTheMess()
        {
            Assert.AreEqual(5.0, VenueComfort.Tonight(5.0, 0.7, 1.10), 1e-12, "the ceiling still holds");
            Assert.AreEqual(4.775, VenueComfort.Tonight(5.0, 0.7), 1e-12, "the mess comes off first");
            Assert.AreEqual((4.0 - 0.225) * 1.10, VenueComfort.Tonight(4.0, 0.7, 1.10), 1e-12,
                "then the cushion, on what the mess left");
            Assert.AreEqual(2.2, VenueComfort.Tonight(2.0, 1.0, 1.10), 1e-12, "then the room's cushion");
            Assert.AreEqual(VenueComfort.Tonight(3.0, 0.4), VenueComfort.Tonight(3.0, 0.4, 1.0), "1 is the room as it was");
            Assert.AreEqual((3.0 - VenueComfort.DirtPenalty * 0.25) * 1.10, VenueComfort.Now(3.0, 1, 4, 1.10), 1e-12);
            Assert.AreEqual(VenueComfort.Now(3.0, 1, 4), VenueComfort.Now(3.0, 1, 4, 1.0));
            Assert.AreEqual(2.2, VenueComfort.Now(2.0, 0, 0, 1.10), 1e-12, "no seats: the base, cushioned");
            Assert.AreEqual(5.0, VenueComfort.Base(4.0, 2.0, 2), 1e-12, "Base is untouched: it has no scale");

            var run = NewRun("comfort");
            double before = run.ComfortNow;
            run.DevFit("rwall_2");
            Assert.AreEqual(10, run.Buffs.Percent(FittingBuffs.Comfort));
            Assert.AreEqual(run.ComfortBase * 1.10, run.ComfortNow, 1e-12, "the live reading, cushioned");
            Assert.AreEqual(0.0, before, 1e-12, "a bare room is worth nothing, and a tenth of nothing is nothing");
        }

        [Test]
        public void Grace_DelaysTheMark()
        {
            var house = new Housekeeping { Grace = 18 };
            var plain = new Housekeeping();
            Assert.AreEqual(Housekeeping.DirtGrace, plain.Grace);
            house.LeaveMark(); plain.LeaveMark();
            house.Tick(12); plain.Tick(12);
            Assert.AreEqual(0, house.DirtySpots, "twelve seconds is inside eighteen");
            Assert.AreEqual(1, plain.DirtySpots, "and past the house's ten");
            house.Tick(8); plain.Tick(8);
            Assert.AreEqual(2.0, house.DirtSpotSeconds, 1e-9, "only what stood past eighteen counts");
            Assert.AreEqual(10.0, plain.DirtSpotSeconds, 1e-9);

            var run = NewRun("grace");
            run.DevFit("floor");
            run.DevFit("kit");
            Assert.AreEqual(80, run.Buffs.Percent(FittingBuffs.Grace));
            Assert.AreEqual(Housekeeping.DirtGrace * 1.80, run.Floor.House.Grace, 1e-12,
                "the counter is told the moment the room changes");
        }

        [Test]
        public void Window_WidensThePerfect()
        {
            var page = Page();
            var visit = Visit(page);
            var glass = OffThePerfect(page, 0.03);
            Assert.IsFalse(JudgeIt(visit, glass, null).PerfectMake, "three points off is outside the house's window");
            Assert.IsTrue(JudgeIt(visit, glass, HouseOf(RoomPiece("pend_2"))).PerfectMake,
                "and inside the pendants' +40%");
            Assert.AreEqual(JudgeIt(visit, glass, null).BasePaid, JudgeIt(visit, glass, HouseOf(RoomPiece("pend_2"))).BasePaid);
        }

        // ── the installed rule ───────────────────────────────────────────────────────────────

        [Test]
        public void TheWornRung_IsTheBuffThatCounts_ComfortIsTheClimbed()
        {
            var ladder = new[]
            {
                Piece("w1", "s1", 1, "tip", 0, starts: true),
                Piece("w2", "s1", 2, "tip", 2, comfort: 1.0),
                Piece("w3", "s1", 3, "tip", 4, comfort: 2.0),
            };
            var run = RunAtDayEnd(fixtures: ladder);
            run.BuyFixture("w2");
            run.BuyFixture("w3");
            Assert.AreEqual(4, run.Buffs.Percent(FittingBuffs.Tip), "the rung it wears: the top");
            Assert.IsTrue(run.WearFixture("w2"));
            Assert.AreEqual(2, run.Buffs.Percent(FittingBuffs.Tip), "wear mark 2, and mark 2's buff is live");
            Assert.AreEqual(2.0, run.FixtureComfort, 1e-12, "while the comfort is still the climb's");
            Assert.IsTrue(run.IsActive(ladder[1]));
            Assert.IsFalse(run.IsActive(ladder[2]), "the rung above is owned, not installed");
            Assert.IsFalse(run.IsActive(ladder[0]));
            CollectionAssert.AreEqual(new[] { "w2" }, run.ActiveFittings.Select(f => f.Id));
        }

        [Test]
        public void TheWardrobe_ClosesWhenTheDoorsOpen()
        {
            var ladder = new[]
            {
                Piece("w1", "s1", 1, "tip", 0, starts: true),
                Piece("w2", "s1", 2, "tip", 2),
                Piece("w3", "s1", 3, "tip", 4),
            };
            var run = RunAtDayEnd(fixtures: ladder);
            run.BuyFixture("w2");
            run.BuyFixture("w3");
            Assert.IsTrue(run.WearFixture("w2"), "at the day's end the room is dressed");
            run.ContinueToNextDay();
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);

            int look = run.LookRevision;
            Assert.IsFalse(run.WearFixture("w3"), "the doors are open");
            Assert.IsFalse(run.WearFixture("w1"));
            Assert.IsFalse(run.WearTopRung("s1"));
            Assert.AreEqual("w2", run.WornRung("s1").Id);
            Assert.AreEqual(look, run.LookRevision);
            Assert.AreEqual(2, run.Buffs.Percent(FittingBuffs.Tip), "and the night reads one room");
        }

        [Test]
        public void ABoughtRung_GoesUp_EvenOverAWornOne()
        {
            var ladder = new[]
            {
                Piece("w1", "s1", 1, "tip", 0, starts: true),
                Piece("w2", "s1", 2, "tip", 2),
                Piece("w3", "s1", 3, "tip", 4),
            };
            var run = RunAtDayEnd(fixtures: ladder);
            run.BuyFixture("w2");
            Assert.IsTrue(run.WearFixture("w1"));
            Assert.AreEqual(0, run.Buffs.Percent(FittingBuffs.Tip), "the old plaster is on the wall");
            int look = run.LookRevision;
            run.BuyFixture("w3");
            Assert.AreEqual("w3", run.WornRung("s1").Id, "buying is intent: the new rung goes up");
            Assert.AreEqual(4, run.Buffs.Percent(FittingBuffs.Tip), "and its buff is live tonight");
            Assert.AreEqual(look, run.LookRevision, "buying is still not wearing");
        }

        [Test]
        public void ASingle_IsLiveWhileOwned()
        {
            var run = NewRun("single");
            var kit = RoomPiece("kit");
            Assert.AreEqual(0, kit.Level, "the refinish kit is a single piece, no ladder");
            Assert.IsFalse(run.IsActive(kit));
            Assert.AreEqual(0, run.Buffs.Percent(FittingBuffs.Grace));
            run.DevFit("kit");
            Assert.IsTrue(run.IsActive(run.FixtureById("kit")));
            Assert.AreEqual(20, run.Buffs.Percent(FittingBuffs.Grace));
            Assert.IsTrue(run.IsActive(run.FixtureById("mat")), "the mat the room opens with is installed too, at 0");
        }

        [Test]
        public void TheHouseCap_Clamps_AndRawShowsTheSum()
        {
            var a = Piece("rug_a", "rug_a", 1, "patience", 12);
            var b = Piece("rug_b", "rug_b", 1, "patience", 12);
            var house = HouseOf(a, b);
            Assert.AreEqual(24, house.RawPercent(FittingBuffs.Patience), "the sum, as the room adds it");
            Assert.AreEqual(20, house.Percent(FittingBuffs.Patience), "clamped to the kind's cap");
            Assert.AreEqual(1.20, house.PatienceScale, 1e-12, "and the hook reads the clamp");
            CollectionAssert.AreEqual(new[] { a, b }, house.Sources(FittingBuffs.Patience));
            var down = HouseOf(Piece("tv_a", "tv_a", 1, "lateness", -20), Piece("tv_b", "tv_b", 1, "lateness", -20));
            Assert.AreEqual(-40, down.RawPercent(FittingBuffs.Lateness));
            Assert.AreEqual(-20, down.Percent(FittingBuffs.Lateness), "a down kind clamps at minus its cap");
        }

        private static void AssertCacheFresh(TycoonRun run, string after)
        {
            var truth = HouseBuffs.From(run.ActiveFittings);
            foreach (var k in FittingBuffs.All)
                Assert.AreEqual(truth.Percent(k), run.Buffs.Percent(k), after + ": " + k.Id);
        }

        [Test]
        public void TheBuffCache_FollowsEveryVerb()
        {
            var run = RunAtDayEnd("cache");
            AssertCacheFresh(run, "the opening room");
            run.BuyFixture("walls_2");
            AssertCacheFresh(run, "a buy");
            Assert.AreEqual(4, run.Buffs.Percent(FittingBuffs.Arrivals));
            run.BuyFixture("walls_3");
            AssertCacheFresh(run, "a second buy");
            Assert.IsTrue(run.WearFixture("walls_2"));
            AssertCacheFresh(run, "a wear");
            Assert.AreEqual(4, run.Buffs.Percent(FittingBuffs.Arrivals));
            Assert.IsTrue(run.WearTopRung("walls"));
            AssertCacheFresh(run, "a wear-top");
            Assert.AreEqual(10, run.Buffs.Percent(FittingBuffs.Arrivals));
            int last = run.TodaysPurchases.Count - 1;
            Assert.AreEqual("walls_3", run.TodaysPurchases[last].Id);
            run.RefundToday(last);
            AssertCacheFresh(run, "a refund");
            Assert.AreEqual(4, run.Buffs.Percent(FittingBuffs.Arrivals));
            run.DevFit("lamp");
            AssertCacheFresh(run, "a dev fit");
            Assert.AreEqual(15, run.Buffs.Percent(FittingBuffs.Tip));
            try
            {
                HouseBuffs.Enabled = false;
                AssertCacheFresh(run, "the gate off");
                Assert.IsTrue(run.Buffs.IsNone);
            }
            finally { HouseBuffs.Enabled = true; }
            AssertCacheFresh(run, "the gate back on");
            Assert.AreEqual(15, run.Buffs.Percent(FittingBuffs.Tip));
        }

        // ── the tools (Option A: they read the installed piece too) ───────────────────────────

        [Test]
        public void TheBoughtBasin_WashesAtItsOwnSpeed()
        {
            // The bug this fixes: SinkSeconds read the first OWNED drain in catalogue order, and the
            // old sink the room opens with is listed first — so a bought basin never washed faster.
            var run = NewRun("basin");
            Assert.AreEqual(3.4, run.SinkSeconds, 1e-12);
            Assert.IsFalse(run.WasteIsFree);
            run.DevFit("counter_sink");
            Assert.AreEqual(3.0, run.SinkSeconds, 1e-12, "the steel sink washes in three");
            Assert.AreEqual(3.0, run.Floor.House.SinkSeconds, 1e-12, "and the counter is told");
            Assert.IsFalse(run.WasteIsFree);
            run.DevFit("sink_brass");
            Assert.AreEqual(2.5, run.SinkSeconds, 1e-12, "the brass one in two and a half");
            Assert.AreEqual(2.5, run.Floor.House.SinkSeconds, 1e-12);
            Assert.IsTrue(run.WasteIsFree, "and pours away for nothing");

            run.DevSkipToDayEnd();
            Assert.IsTrue(run.WearFixture("sink_old"), "the old basin put back on the counter...");
            Assert.AreEqual(3.4, run.SinkSeconds, 1e-12, "...washes at the old basin's pace");
            Assert.IsFalse(run.WasteIsFree, "and pours away at the old price");
            run.ContinueToNextDay();
            Assert.AreEqual(3.4, run.Floor.House.SinkSeconds, 1e-12, "tomorrow's counter hears the same");
        }

        [Test]
        public void TheTools_WorkAtTheInstalledPiecesPace()
        {
            var towers = new[]
            {
                Piece("tower_1", "taps", 0, "pour_speed", 0, starts: true, tap: 1),
                Piece("tower_2", "taps", 0, "pour_speed", 0, tap: 2, work: 1.4),
                Piece("tin_steel", "shaker", 1, "shake_speed", 0, starts: true),
                Piece("tin_gold", "shaker", 2, "shake_speed", 0, work: 1.5),
            };
            var run = RunAtDayEnd(fixtures: towers);
            Assert.AreEqual(1.0, run.TapSpeed, 1e-12);
            Assert.AreEqual(1.0, run.WorkSpeed("shaker"), 1e-12);
            run.BuyFixture("tower_2");
            run.BuyFixture("tin_gold");
            Assert.AreEqual(1.4, run.TapSpeed, 1e-12, "the tower it bought is the tower it pours from");
            Assert.AreEqual(1.5, run.WorkSpeed("shaker"), 1e-12);
            Assert.IsTrue(run.WearFixture("tower_1"));
            Assert.IsTrue(run.WearFixture("tin_steel"));
            Assert.AreEqual(1.0, run.TapSpeed, 1e-12, "wear the single tower and it pours at the single's pace");
            Assert.AreEqual(1.0, run.WorkSpeed("shaker"), 1e-12, "wear the steel tin and it shakes at the steel's");
            Assert.AreEqual("tower_2", run.StandingTap().Id, "the tallest tower is still what the kegs count");
            Assert.AreEqual(1.0, run.WorkSpeed("nowhere"), 1e-12);
        }

        [Test]
        public void FittingEffects_DeriveTheTools()
        {
            var room = Room();
            var run = NewRun("effects", fixtures: room);
            FittingEffect Lead(string id) => run.FittingEffects(run.FixtureById(id)).First();

            Assert.AreSame(FittingBuffs.ShakeSpeed, Lead("tin_gold").Kind);
            Assert.AreEqual(50, Lead("tin_gold").Percent);
            Assert.AreEqual(-12, Lead("counter_sink").Percent, "3.0 against the old sink's 3.4");
            var brass = run.FittingEffects(run.FixtureById("sink_brass"));
            Assert.AreEqual(2, brass.Count);
            Assert.AreSame(FittingBuffs.Wash, brass[0].Kind);
            Assert.AreEqual(-26, brass[0].Percent);
            Assert.AreSame(FittingBuffs.FreeDrain, brass[1].Kind);
            Assert.IsTrue(brass[1].IsFlag);
            Assert.IsFalse(brass[1].IsBase, "a flag is not the house standard");
            foreach (var id in new[] { "tin_steel", "sink_old", "tower", "walls_1", "table_L1", "mat" })
            {
                Assert.IsTrue(Lead(id).IsBase, id + " is the foot its ladder climbs from");
                Assert.AreEqual(0, Lead(id).Percent, id);
            }
            Assert.AreSame(FittingBuffs.Price, Lead("neon").Kind);
            Assert.AreEqual(12, Lead("neon").Percent);
            Assert.AreEqual(-20, Lead("tv").Percent);

            var bare = new FixtureDefinition("fern", "Fern", "plant_left", 20, 0, "", "fx_fern");
            Assert.IsFalse(bare.HasBuff);
            Assert.AreEqual(0, FittingBuffs.EffectsOf(bare, room).Count, "a row that names no buff has no effects");
            Assert.AreEqual(0, FittingBuffs.EffectsOf(null, room).Count);
            Assert.AreEqual(0, FittingBuffs.EffectsOf(RoomPiece("tin_gold"), null)[0].Percent,
                "with no catalogue a tool is measured against itself");
        }

        // ── a row's own refusals, and the catalogue's (the loader's words are the data tests') ─

        [Test]
        public void ARow_RefusesABadBuff_AndTheCatalogueRefusesAMixedOrFlatSlot()
        {
            void Refused(string why, Func<FixtureDefinition> make)
            {
                var e = Assert.Throws<ArgumentException>(() => make(), why);
                StringAssert.Contains("'bad'", e.Message, why + " names the fixture");
            }
            FixtureDefinition Bad(string buff, int pct, bool starts = false, int tap = 0, bool drain = false, int level = 1) =>
                new FixtureDefinition("bad", "Bad", "s", 20, 0, "", "fx", startsInTheRoom: starts,
                    tapLevel: tap, level: tap > 0 ? 0 : level, isDrain: drain, buff: buff, buffPct: pct);

            Refused("an unknown kind", () => Bad("nope", 5));
            Refused("a figure without a kind", () => Bad(null, 5));
            Refused("a tool kind with a figure", () => Bad("shake_speed", 10));
            Refused("free drain named", () => Bad("free_drain", 0, drain: true));
            Refused("pour speed off a tower", () => Bad("pour_speed", 0));
            Refused("wash off a drain", () => Bad("wash", 0));
            Refused("shake speed on a drain", () => Bad("shake_speed", 0, drain: true));
            Refused("shake speed on a tower", () => Bad("shake_speed", 0, tap: 1));
            Refused("an opening piece with a figure", () => Bad("tip", 2, starts: true));
            Refused("a bought data piece at 0", () => Bad("tip", 0));
            Refused("a figure pointing the wrong way", () => Bad("lateness", 5));
            Refused("a figure over MaxPerPiece", () => Bad("price", 13));
            Assert.AreEqual(12, Bad("price", 12).BuffPct, "at the most one piece may carry");
            Assert.AreEqual(-20, Bad("lateness", -20).BuffPct);
            Assert.AreSame(FittingBuffs.Wash, Bad("wash", 0, drain: true).Buff);
            Assert.AreSame(FittingBuffs.PourSpeed, Bad("pour_speed", 0, tap: 1).Buff);

            Assert.IsNull(FittingBuffs.CatalogueFault(Room()), "the synthetic room is fine");
            var mixed = new[] { Piece("m1", "slot_m", 1, "tip", 2), Piece("m2", "slot_m", 2, "price", 4) };
            StringAssert.Contains("'slot_m'", FittingBuffs.CatalogueFault(mixed), "two kinds in one slot");
            var flat = new[] { Piece("f1", "slot_f", 1, "tip", 4), Piece("f2", "slot_f", 2, "tip", 4) };
            StringAssert.Contains("'slot_f'", FittingBuffs.CatalogueFault(flat), "a rung that buys nothing");
            var falling = new[] { Piece("d1", "slot_d", 1, "lateness", -9), Piece("d2", "slot_d", 2, "lateness", -5) };
            Assert.IsNotNull(FittingBuffs.CatalogueFault(falling), "|figure| must rise, whichever way the kind points");
            var rising = new[] { Piece("u1", "slot_u", 1, "lateness", 0, starts: true), Piece("u2", "slot_u", 2, "lateness", -5) };
            Assert.IsNull(FittingBuffs.CatalogueFault(rising), "the opening rung's 0 is the lowest");
            var unnamed = new[] { Piece("n1", "slot_n", 1, null, 0), Piece("n2", "slot_n", 2, "tip", 2) };
            Assert.IsNull(FittingBuffs.CatalogueFault(unnamed), "a row that names nothing is the loader's to allow");
        }

        // ── how the room and a page compose ───────────────────────────────────────────────────

        [Test]
        public void TheRoomAndThePage_Compose()
        {
            var lamp = HouseOf(RoomPiece("lamp"));
            var tv = HouseOf(RoomPiece("tv"));
            var pend = HouseOf(RoomPiece("pend_2"));

            // THEY TIP FOR THIS × TIP +15: two scales on one ceiling.
            var rich = Page("they_tip_for_this");
            var perfect = OffThePerfect(rich, 0.0);
            var visit = Visit(rich, 0.2, price: 90);
            int page = JudgeIt(visit, perfect, null).Tip;
            int both = JudgeIt(visit, perfect, lamp).Tip;
            Assert.AreEqual(page * 1.15, both, 1.1, "the room's sixth on top of the page's fifth");
            AssertSameVerdict(JudgeIt(visit, perfect, null), JudgeIt(visit, perfect, HouseBuffs.None),
                "the page alone is the page alone");

            // KEEPS WELL × LATE PENALTY −20: the two scales multiply on the wait term.
            var plainPage = Page();
            var kept = Page("keeps_well");
            var plainVisit = Visit(plainPage, 0.6);
            var keptVisit = Visit(kept, 0.6);
            var glass = OffThePerfect(plainPage, 0.01);
            double bare = JudgeIt(plainVisit, glass, null).Satisfaction;
            double stacked = JudgeIt(keptVisit, glass, tv).Satisfaction;
            Assert.AreEqual(plainVisit.WaitFraction, keptVisit.WaitFraction, 1e-12);
            Assert.AreEqual(ServiceJudge.WaitPenalty * (1.0 - 0.8 * 0.8) * plainVisit.WaitFraction, stacked - bare, 1e-12);

            // EASY TO LEARN × the pendants: the windows multiply, so four points off needs both.
            var easy = Page("easy_to_learn");
            var easyVisit = Visit(easy);
            var off = OffThePerfect(easy, 0.04);
            Assert.IsFalse(JudgeIt(easyVisit, off, null).PerfectMake, "the page's own window: 3 points");
            Assert.IsFalse(JudgeIt(Visit(plainPage), OffThePerfect(plainPage, 0.04), pend).PerfectMake,
                "the room's own: 3.5");
            Assert.IsTrue(JudgeIt(easyVisit, off, pend).PerfectMake, "together: 4.2");

            // NEVER JUST ONE + the tables: the grants add on the same roll.
            string between = SeedWithFirstRound(0.22, 0.32);
            Assert.IsFalse(RoundGranted(between, "never_just_one", tables: false, secondWaiting: false),
                "past the page's 0.22");
            Assert.IsTrue(RoundGranted(between, "never_just_one", tables: true, secondWaiting: false),
                "inside the page's and the room's 0.32 together");
        }

        [Test]
        public void ASeedStillReproduces()
        {
            (DayResult night, int arrived, int serves, RunRng rng) Night(string seed, bool fitted)
            {
                var rng = new RunRng(seed);
                var run = NewRun(seed, rng);
                if (fitted)
                    foreach (var id in new[] { "walls_3", "rwall_2", "ceil", "floor", "kit", "rug", "palm", "tv",
                                               "art", "pic", "neon", "lamp", "pend_2", "table_L2", "table_R2",
                                               "sink_brass", "tin_gold" })
                        run.DevFit(id);
                int serves = PlayNight(run);
                int arrived = run.Floor.Arrived;
                return (run.ContinueToNextDay(), arrived, serves, rng);
            }

            var a = Night("seed", fitted: true);
            var b = Night("seed", fitted: true);
            AssertSameLedger(a.night, b.night, "one seed, one fitted night");
            Assert.AreEqual(a.arrived, b.arrived);
            Assert.Greater(a.serves, 3, "a night with people in it");

            var bare = Night("seed", fitted: false);
            foreach (var run in new[] { a, bare })
            {
                var patience = new RunRng("seed").GetStream("patience");
                for (int i = 0; i < run.arrived; i++) patience.NextDouble();
                Assert.AreEqual(patience.NextDouble(), run.rng.GetStream("patience").NextDouble(),
                    "one patience draw per arrival");
                var round = new RunRng("seed").GetStream("round");
                for (int i = 0; i < run.serves; i++) round.NextDouble();
                Assert.AreEqual(round.NextDouble(), run.rng.GetStream("round").NextDouble(),
                    "one round draw per resolved serve");
            }
        }

        [Test]
        public void TheWorstStack_NeverPassesTheCeiling()
        {
            var full = HouseOf(Room());
            Assert.AreEqual(12, full.Percent(FittingBuffs.Price));
            Assert.AreEqual(15, full.Percent(FittingBuffs.Tip));

            // The dearest shipped page, four tier-4 spirits' premium, a high-rolling crowd and the
            // neon, priced the way the till prices it — and a flawless serve of a page that tips.
            var book = RecipeCatalog.CreateDefault();
            int dearest = book.Max(r => DrinkOrder.MenuPrice(r, BarRating.MaxStars));
            int premium = 4 * 3 * TycoonConfig.Default.StockPremiumPerTier;
            int price = Math.Min(DrinkPricing.CeilingPerDrink, (int)Math.Round(
                (dearest + premium) * TycoonConfig.Default.PriceMultiplier(WealthTier.HighRoller) * full.PriceScale,
                MidpointRounding.AwayFromZero));
            var rich = Page("they_tip_for_this");
            var shipped = JudgeIt(Visit(rich, 0.0, price), OffThePerfect(rich, 0.0), full, WealthTier.HighRoller);
            Assert.LessOrEqual(shipped.BasePaid + shipped.Tip, DrinkPricing.CeilingPerDrink, "the shipped worst");

            // A future page that would pass it: a $190 bill and a $302 tip. The tip gives way.
            var future = JudgeIt(Visit(rich, 0.0, 190), OffThePerfect(rich, 0.0), full, WealthTier.HighRoller);
            Assert.AreEqual(190, future.BasePaid, "the bill is what the drink was worth");
            Assert.AreEqual(DrinkPricing.CeilingPerDrink, future.BasePaid + future.Tip, "and the till stops at the ceiling");
        }

        [Test]
        public void TheProjection_WithoutAHouse_IsUnchanged()
        {
            var book = RecipeCatalog.CreateDefault();
            var cfg = TycoonConfig.Default;
            var before = EconomyProjection.Walk(book, cfg, 42, EconomyProjection.Competent);
            var nullHouse = EconomyProjection.Walk(book, cfg, 42, EconomyProjection.Competent, houseOn: null);
            var noneHouse = EconomyProjection.Walk(book, cfg, 42, EconomyProjection.Competent,
                houseOn: (d, s) => HouseBuffs.None);
            Assert.AreEqual(before.Count, nullHouse.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i], nullHouse[i], "night " + before[i].Day);
                Assert.AreEqual(before[i], noneHouse[i], "night " + before[i].Day);
            }

            var room = Room().Select(f => f).ToList();
            var fitted = EconomyProjection.Walk(book, cfg, 42, EconomyProjection.Competent,
                houseOn: (d, s) => HouseBuffs.FullRoomAt(room, s));
            long grossBefore = 0, grossFitted = 0;
            for (int i = 0; i < before.Count; i++)
            {
                Assert.GreaterOrEqual(fitted[i].Covers, before[i].Covers, "the door never closes for a room");
                if (fitted[i].Covers == before[i].Covers)
                    Assert.GreaterOrEqual(fitted[i].Gross + fitted[i].Tips, before[i].Gross + before[i].Tips,
                        "night " + before[i].Day);
                grossBefore += before[i].Gross;
                grossFitted += fitted[i].Gross;
            }
            Assert.Greater(grossFitted, grossBefore, "and a fitted room takes more");
        }

        [Test]
        public void TheFullRoom_IsEveryOpenRungAtItsTop()
        {
            var room = Room();
            var early = HouseBuffs.FullRoomAt(room, 0.0);
            Assert.AreEqual(10, early.Percent(FittingBuffs.Arrivals), "every rung here opens at no stars");
            var gated = new[]
            {
                Piece("g1", "g", 1, "price", 0, starts: true),
                Piece("g2", "g", 2, "price", 6, stars: 1.0),
                Piece("g3", "g", 3, "price", 12, stars: 3.0),
            };
            Assert.AreEqual(0, HouseBuffs.FullRoomAt(gated, 0.5).Percent(FittingBuffs.Price), "the opening rung");
            Assert.AreEqual(6, HouseBuffs.FullRoomAt(gated, 1.0).Percent(FittingBuffs.Price), "the rung one star opens");
            Assert.AreEqual(12, HouseBuffs.FullRoomAt(gated, 5.0).Percent(FittingBuffs.Price), "the top");
            Assert.AreSame(HouseBuffs.None, HouseBuffs.FullRoomAt(null, 5.0));
        }
    }
}
