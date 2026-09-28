using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE HOSTESS AND HER BOOK (2026-09-27, TycoonRun.Quests) — played through the verbs the player and the bot use,
    /// on small hand-written books, so every rule runs without the content loader: when she comes, what she brings,
    /// what counts and from when, what it pays and into which night, when she waits and when she passes a row over,
    /// how the book ends, and what the save carries. The live book is pinned in QuestDataTests.
    ///
    /// The bar here pours one page, a gin and soda; a second page wants a sour nobody stocks, so it is on the menu
    /// and never pourable — the case a serve job must never name. Nobody takes a moment to decide or to drink, so a
    /// night is a few hundred ticks and every serve is Exact. The jobs that count one drink are also played on a
    /// four-star bar with pages on every rung (Ladder), where the night's plan would never ask for them by itself.
    /// </summary>
    public sealed class QuestChainTests
    {
        private static readonly IngredientCard Gin = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6);
        private static readonly IngredientCard Soda = new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1);

        private static readonly IReadOnlyList<RecipeDefinition> Menu = new[]
        {
            new RecipeDefinition("spritz", "Spritz", rank: 2, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5),
            new RecipeDefinition("sour", "Sour", rank: 3, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.4, 0.7),
                    new RatioRequirement(IngredientType.Sour, 0.3, 0.6),
                },
                minFill: 0.5),
        };

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(Gin.Clone(), capacity: 4000),
            new ShelfBottle(Soda.Clone(), capacity: 4000),
        });

        /// <summary>The shipped switches; only the purse and the two waits are the bench's.</summary>
        private static TycoonConfig Config(int money = 5000) =>
            new TycoonConfig(startingMoney: money, orderDecisionSeconds: 0, savorSeconds: 0);

        private static RegularsRegistry People() => new RegularsRegistry(new[]
        {
            new ArchetypeDefinition("after_shift", "Off the Late Shift",
                new[] { "Marguerite", "Dev", "Ola", "Kit", "Rasmus", "Nuray" }, 1,
                new[] { "this side of town" }),
        });

        private static TycoonRun NewRun(QuestBook book, string seed, bool people = false,
            IReadOnlyList<FixtureDefinition> fixtures = null, int money = 5000,
            IReadOnlyList<RecipeDefinition> menu = null, Shelf shelf = null) =>
            new TycoonRun(shelf ?? NewShelf(), menu ?? Menu, new RunRng(seed), config: Config(money),
                regulars: people ? People() : null, fixtures: fixtures, quests: book);

        /// <summary>A page of gin and soda at <paramref name="rank"/> — every one of them pourable off the bench's shelf.</summary>
        private static RecipeDefinition Highball(string id, int rank, PrepMethod prep = PrepMethod.Built) =>
            new RecipeDefinition(id, id, rank: rank, baseFlavor: 6, baseMult: 1, flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, prep: prep);

        /// <summary>
        /// A BOOK UP THE WHOLE LADDER (for the jobs that count one drink): pages on every rung to four stars, the pint
        /// and one stirred page on the ground floor. A four-star plan gives that floor two covers in a hundred, so
        /// without her rule neither is ever ordered there.
        /// </summary>
        private static readonly IReadOnlyList<RecipeDefinition> Ladder = new[]
        {
            new RecipeDefinition("draught", "Draught", 1, 5, 1, 10, 1,
                new[] { new PatternRequirement(1, IngredientType.Beer) },
                exactMixSize: 1, minFill: 0.75, glassId: "pint", prep: PrepMethod.Built),
            Highball("low_a", 2), Highball("low_stir", 3, PrepMethod.Stirred), Highball("low_b", 4), Highball("low_c", 5),
            Highball("one_a", 9), Highball("one_b", 10),
            Highball("two_a", 12), Highball("two_b", 13),
            Highball("three_a", 15), Highball("three_b", 16), Highball("three_c", 17),
            Highball("four_a", 22), Highball("four_b", 23), Highball("four_c", 24), Highball("four_d", 25),
        };

        private static Shelf LadderShelf() => new Shelf(new[]
        {
            new ShelfBottle(Gin.Clone(), capacity: 40000),
            new ShelfBottle(Soda.Clone(), capacity: 40000),
            new ShelfBottle(new IngredientCard("lager", "Lager", IngredientType.Beer, 3), capacity: 40000),
        });

        /// <summary>One row, with lines that pass the book's rules for any kind.</summary>
        private static QuestDefinition Q(string id, QuestKind kind, int rung = 0, int target = 1, int reward = 10,
            QuestPick pick = QuestPick.Any, PreparationDefinition[] preps = null, int goalRung = 0,
            double goalComfort = 0, string slot = null, int level = 0, string fixtureId = null) =>
            new QuestDefinition(id, kind, rung, target, reward, id.ToUpperInvariant(),
                new[] { "{name} has a job for you." }, new[] { "Thank you." },
                pick, preps, goalRung, goalComfort, slot, level, fixtureId);

        private static QuestBook Book(params QuestDefinition[] rows) =>
            new QuestBook(rows, new[] { "That is the whole book." }, "The next one waits till we're {rung}.");

        private static FixtureDefinition Picture(int level, double stars) =>
            new FixtureDefinition("pic_" + level, "Picture " + level, "wall_center", 20 * level, stars,
                "Something on the wall.", "fx_pic_" + level, level: level, comfort: 0.1 * level);

        // ── the hands ──────────────────────────────────────────────────────────────

        /// <summary>A spritz for every drinker with an order in, card read first. Returns how many went out.</summary>
        private static int ServeWaiting(TycoonRun run)
        {
            if (run.Phase != TycoonPhase.DayOpen) return 0;
            int served = 0;
            foreach (var v in run.Floor.Seated.ToList())
            {
                if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                v.InspectId();
                if (!run.CanMake(v.Order)) { run.DeclineOrder(v); continue; }
                Pour(run);
                Assert.AreEqual(OrderMatch.Exact, run.ServeTo(v).Match, "the bench pours the page exactly");
                served++;
            }
            return served;
        }

        private static void Pour(TycoonRun run)
        {
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        /// <summary>Everybody with an order in is told no: nothing crosses the bar, so the counter stays clean.</summary>
        private static void DeclineWaiting(TycoonRun run)
        {
            if (run.Phase != TycoonPhase.DayOpen) return;
            foreach (var v in run.Floor.Seated.ToList())
                if (v.State == VisitState.Waiting && v.HasOrdered) run.DeclineOrder(v);
        }

        private static void Step(TycoonRun run, bool serve)
        {
            run.Tick(1.0);
            TestNight.Clean(run);
            if (serve) ServeWaiting(run); else DeclineWaiting(run);
        }

        /// <summary>Plays the night until she walks in (returns her visit) or the night closes without her (null).
        /// Nobody listens: once she is in, this stops, and what happens next is the caller's.</summary>
        private static HostessVisit PlayToHerArrival(TycoonRun run, bool serve = true)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen && run.HostessVisit == null)
            {
                Assert.Less(guard++, 5000, "the night must reach its close");
                Step(run, serve);
            }
            return run.HostessVisit;
        }

        /// <summary>Plays the night out with nobody listening — Core hands the job over itself — and files it.</summary>
        private static DayResult PlayNight(TycoonRun run, bool serve = true)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 5000, "the night must end");
                Step(run, serve);
            }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "the bar must still be open for business");
            return run.ContinueToNextDay();
        }

        // ── 1. when she comes ───────────────────────────────────────────────────────

        [Test]
        public void SheComesAtTheFirstCloseOfNightOne()
        {
            var run = NewRun(Book(Q("serve_one", QuestKind.Serve, target: 3), Q("then", QuestKind.Perfect)), "first-close");
            Assert.AreEqual(QuestRules.FirstVisitNight, run.HostessComesOn, "the first close is booked from the start");
            Assert.IsNull(run.Quest, "nothing is on the bar before she has been");
            int guard = 0;
            while (true)
            {
                Assert.Less(guard++, 5000);
                Step(run, serve: true);
                if (run.Floor.IsClosingTime) break;
                Assert.IsNull(run.HostessVisit, "she does not come while the door is open");
            }
            var visit = run.HostessVisit;
            Assert.IsNotNull(visit, "she walks in on the first tick at closing time");
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);
            Assert.AreEqual(1, visit.Day);
            Assert.IsNull(visit.Finished);
            Assert.IsFalse(visit.Finale);
            Assert.AreEqual("serve_one", visit.Offered.Id);
            var drink = run.MenuRecipes.First(r => r.Id == visit.Offered.RecipeId);
            Assert.IsTrue(run.CanServe(drink), "she only ever names a drink the shelf can pour");
            Assert.IsNull(run.Quest, "and it is not on the bar until she has been heard");
        }

        // ── 2. the close waits for her ──────────────────────────────────────────────

        [Test]
        public void TheNightCannotCloseUnderHer()
        {
            var run = NewRun(Book(Q("serve_one", QuestKind.Serve)), "not-under-her");
            var visit = PlayToHerArrival(run, serve: false);
            Assert.IsNotNull(visit);
            DeclineWaiting(run);
            run.Tick(0.01);    // the last stools free
            Assert.IsTrue(run.Floor.IsComplete, "the floor is empty");
            Assert.IsTrue(run.Floor.House.CounterClear, "the counter is clear");
            Assert.IsFalse(run.Floor.House.SinkBusy);
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase, "and still the books do not come down over her");

            run.BeginTalk();
            for (int i = 0; i < 100; i++) run.Tick(1.0);
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase, "held, the night stands still");
            Assert.AreSame(visit, run.HostessVisit, "and the backstop's clock never ran");

            run.HearHostess();
            run.EndTalk();
            Assert.IsNull(run.HostessVisit);
            Assert.AreEqual("serve_one", run.Quest?.Id, "heard out, the job is on the bar");
            run.Tick(0.01);
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "and the night closes");
        }

        // ── 3. nobody listening ─────────────────────────────────────────────────────

        [Test]
        public void NobodyListening_CoreHandsItOverOnTheFourthTick()
        {
            // Two pourable pages, so the drink she names is a real pick: the same seed must pick the same one whether
            // anybody listens or not, and across the seeds tried both pages must come up.
            var twoPages = new[] { Highball("spritz", 2), Highball("long_spritz", 3) };
            var book = Book(Q("serve_one", QuestKind.Serve, target: 3, reward: 12), Q("then", QuestKind.Perfect));
            var picked = new HashSet<string>();
            for (int s = 0; s < 12 && picked.Count < 2; s++)
            {
                string seed = "grace-" + s;
                var headless = NewRun(book, seed, menu: twoPages);
                Assert.IsNotNull(PlayToHerArrival(headless, serve: false));
                int ticks = 0;
                while (headless.HostessVisit != null)
                {
                    Assert.Less(ticks, 10, "the backstop must fire");
                    headless.Tick(1.0);
                    ticks++;
                }
                Assert.AreEqual(4, ticks, "handed over on the tick the unheard floor-seconds reach four");

                // The same seed with somebody listening as long as they like: the same job, the same drink, the same pay.
                var played = NewRun(book, seed, menu: twoPages);
                Assert.IsNotNull(PlayToHerArrival(played, serve: false));
                played.BeginTalk();
                for (int i = 0; i < 30; i++) played.Tick(1.0);
                played.HearHostess();
                played.EndTalk();

                Assert.AreEqual(played.Quest.Id, headless.Quest.Id, seed);
                Assert.AreEqual(played.Quest.RecipeId, headless.Quest.RecipeId, seed + ": the same drink");
                Assert.AreEqual(played.Quest.Target, headless.Quest.Target, seed);
                Assert.AreEqual(played.Quest.Reward, headless.Quest.Reward, seed);
                Assert.AreEqual(played.Quest.GivenDay, headless.Quest.GivenDay, seed);
                picked.Add(headless.Quest.RecipeId);
            }
            Assert.AreEqual(QuestRules.HostessGraceSeconds, 4.0);
            Assert.AreEqual(2, picked.Count, "both pages came up across the seeds: the pick was a real draw");
        }

        // ── 4. counting and paying ──────────────────────────────────────────────────

        [Test]
        public void CountingStartsAtTheHandOver_AndPaysOnTheSpot()
        {
            var run = NewRun(Book(Q("serve_three", QuestKind.Serve, target: 3, reward: 30), Q("then", QuestKind.Perfect)),
                "counting");
            int servedBefore = 0, guard = 0;
            while (run.HostessVisit == null)
            {
                Assert.Less(guard++, 5000);
                run.Tick(1.0);
                TestNight.Clean(run);
                servedBefore += ServeWaiting(run);
            }
            Assert.Greater(servedBefore, 2, "spritzes went out on night one, before she came");
            Assert.AreEqual("spritz", run.HostessVisit.Offered.RecipeId, "the one page the shelf pours");

            run.HearHostess();
            Assert.AreEqual(0, run.Quest.Progress, "nothing poured before the hand-over counts");
            Assert.AreSame(run.Quest, run.TakeQuestJustGiven(), "the hand-over is said once");
            Assert.IsNull(run.TakeQuestJustGiven());
            Assert.AreEqual(0, run.HostessComesOn, "and nothing more is booked until it is done");
            PlayNight(run, serve: false);
            Assert.AreEqual(0, run.Quest.Progress);

            // Night two: every serve counts, and the one that finishes it pays on the spot.
            guard = 0;
            int paidOn = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 5000);
                run.Tick(1.0);
                TestNight.Clean(run);
                if (run.Phase != TycoonPhase.DayOpen) break;
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                    v.InspectId();
                    int money = run.Money, bonus = run.DayBonus, hers = run.DayQuestPaid;
                    bool wasDone = run.Quest.IsDone;
                    Pour(run);
                    run.ServeTo(v);
                    if (!wasDone && run.Quest.IsDone)
                    {
                        paidOn = run.Day;
                        Assert.AreEqual(30, run.Money - money, "her pay lands in the till on the spot");
                        Assert.AreEqual(30, run.DayBonus - bonus, "in the night's bonus line");
                        Assert.AreEqual(30, run.DayQuestPaid - hers, "marked as hers");
                    }
                    else
                    {
                        Assert.AreEqual(money, run.Money, "no serve moves the till");
                        Assert.AreEqual(hers, run.DayQuestPaid);
                    }
                }
            }
            Assert.AreEqual(2, paidOn, "three spritzes went out on night two");
            Assert.AreEqual(3, run.Quest.Progress, "and the count stops at the target");
            Assert.AreEqual(2, run.Quest.DoneDay);
            Assert.AreEqual(30, run.DayQuestPaid, "paid once");
            Assert.AreSame(run.Quest, run.TakeQuestJustDone(), "the room is told once");
            Assert.IsNull(run.TakeQuestJustDone());
            Assert.IsTrue(run.QuestDoneUnsaid);
            Assert.AreEqual(3, run.HostessComesOn, "she comes the night after");

            var night = run.ContinueToNextDay();
            Assert.AreEqual(30, night.QuestPaid, "the night's row says whose money it was");
            Assert.GreaterOrEqual(night.Bonus, 30);
            Assert.AreEqual(0, run.DayQuestPaid, "tomorrow starts clean");
        }

        // ── 5. the night after ──────────────────────────────────────────────────────

        [Test]
        public void SheComesTheNightAfterItIsDone()
        {
            var run = NewRun(Book(Q("clean_one", QuestKind.Clean, reward: 24), Q("perfect_two", QuestKind.Perfect, target: 2)),
                "night-after");
            PlayNight(run);                                       // night 1: handed over at the close
            Assert.AreEqual("clean_one", run.Quest?.Id);
            Assert.IsFalse(run.Quest.IsDone, "the night she handed it over is not a night after it");

            // Night 2: every drinker served, promptly and right — a clean night, paid at the close.
            bool visitedTwo = false;
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 5000);
                Step(run, serve: true);
                visitedTwo |= run.HostessVisit != null;
            }
            Assert.IsFalse(visitedTwo, "she does not come the night the job is being done");
            Assert.IsTrue(run.Quest.IsDone, "nobody walked, nothing went wrong");
            Assert.AreEqual(2, run.Quest.DoneDay);
            Assert.AreEqual(24, run.DayQuestPaid, "paid at the close, before the slip");
            run.ContinueToNextDay();

            // Night 3: she comes with her thanks and the next job.
            var visit = PlayToHerArrival(run);
            Assert.IsNotNull(visit, "she comes at the close of the next night");
            Assert.AreEqual(3, visit.Day);
            Assert.AreEqual("clean_one", visit.Finished?.Id);
            Assert.AreEqual("perfect_two", visit.Offered?.Id);
            Assert.IsFalse(visit.Finale);
        }

        // ── 6. state goals ──────────────────────────────────────────────────────────

        [Test]
        public void StateGoalsLandAtDawn()
        {
            var fixtures = new[] { Picture(1, 0.0), Picture(2, 1.0) };
            var book = Book(Q("hang_one", QuestKind.Fit, reward: 24, slot: "wall_center", level: 1, fixtureId: "pic_1"),
                Q("then", QuestKind.Perfect));
            var run = NewRun(book, "dawn", fixtures: fixtures);
            PlayNight(run);
            Assert.AreEqual("hang_one", run.Quest?.Id);

            // Night 2 is played out; the picture goes up at the market, after the slip.
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(run, serve: true); }
            run.DevFit("pic_1");
            Assert.IsFalse(run.Quest.IsDone, "nothing is read before the dawn");
            Assert.AreEqual(0, run.DayQuestPaid, "so it is not on this night's slip");
            var night = run.ContinueToNextDay();
            Assert.IsTrue(run.Quest.IsDone, "read at the dawn");
            Assert.AreEqual(2, run.Quest.DoneDay, "done on the night the dawn filed");
            Assert.AreEqual(24, night.QuestPaid, "paid into the closed night's row");
            Assert.AreSame(run.Quest, run.TakeQuestJustDone(), "the flash waits for the next night's first frame");
            Assert.AreEqual(3, run.HostessComesOn);

            var visit = PlayToHerArrival(run);
            Assert.AreEqual("hang_one", visit?.Finished?.Id, "and she comes that night");
            Assert.AreEqual("then", visit.Offered?.Id);
        }

        /// <summary>
        /// THE DAWN'S PAY, ASKED BEFORE THE DAWN (2026-09-28, the night's TOMORROW board): QuestPaysAtDawn is a preview
        /// of CountQuestStateGoal, the way StandingAfterTonight is of the close - so the board that says "PAYS AT DAWN"
        /// can never promise what the dawn then refuses, or stay silent over what it pays.
        /// </summary>
        [Test]
        public void TheDawnsPayIsAskedBeforeTheDawn()
        {
            // A fitting goal: open all night, met once the picture goes up - and the preview says so before the dawn.
            var fixtures = new[] { Picture(1, 0.0), Picture(2, 1.0) };
            var fit = NewRun(Book(Q("hang_one", QuestKind.Fit, reward: 24, slot: "wall_center", level: 1, fixtureId: "pic_1"),
                Q("then", QuestKind.Perfect)), "dawn-preview-fit", fixtures: fixtures);
            Assert.IsFalse(fit.QuestPaysAtDawn, "nothing is on the bar before she has been");
            PlayNight(fit);
            Assert.AreEqual("hang_one", fit.Quest?.Id);
            int guard = 0;
            while (fit.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(fit, serve: true); }
            Assert.IsFalse(fit.QuestPaysAtDawn, "the wall is bare: the dawn will not pay");
            fit.DevFit("pic_1");
            Assert.IsTrue(fit.QuestPaysAtDawn, "the picture is up: the dawn will pay");
            Assert.AreEqual(0, fit.DayQuestPaid, "and a state goal is on no tape");
            Assert.AreEqual(24, fit.ContinueToNextDay().QuestPaid, "the dawn paid what the preview said it would");
            Assert.IsFalse(fit.QuestPaysAtDawn, "a paid job owes nothing at the next dawn");

            // A rung goal, read where the dawn reads it: the rung tonight files, not the one the bar stands on now.
            // Nobody is served, so no night climbs by itself; one bar is lifted to the rung before its close.
            foreach (bool lifted in new[] { false, true })
            {
                var rank = NewRun(Book(Q("half_a_star", QuestKind.Rank, reward: 18, goalRung: 1), Q("then", QuestKind.Perfect)),
                    "dawn-preview-rank");
                PlayNight(rank, serve: false);
                Assert.AreEqual("half_a_star", rank.Quest?.Id, "the rung goal is on the bar");
                guard = 0;
                while (rank.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(rank, serve: false); }
                if (lifted) rank.Rating.DevSet(0.5);
                bool said = rank.QuestPaysAtDawn;
                Assert.AreEqual(lifted, said, lifted ? "the rung is reached tonight" : "the bar has not climbed");
                Assert.AreEqual(rank.RankAfterTonight.Index >= 1, said, "the preview reads the rung tonight files");
                Assert.AreEqual(said ? 18 : 0, rank.ContinueToNextDay().QuestPaid, "the dawn did what the preview said");
            }

            // A count job is paid on the spot or not at all: never at the dawn.
            var serve = NewRun(Book(Q("serve_one", QuestKind.Serve, target: 3), Q("then", QuestKind.Perfect)), "dawn-preview-serve");
            PlayNight(serve, serve: false);
            Assert.AreEqual("serve_one", serve.Quest?.Id);
            Assert.IsFalse(serve.QuestPaysAtDawn, "a serve job is never the dawn's to pay");
        }

        // ── 7. the rung gate ────────────────────────────────────────────────────────

        [Test]
        public void ARungGateMakesHerWait()
        {
            var run = NewRun(Book(Q("serve_one", QuestKind.Serve), Q("door_job", QuestKind.Door, rung: 2)), "gate");
            PlayNight(run);                                        // 1: handed over
            PlayNight(run);                                        // 2: done
            Assert.IsTrue(run.Quest.IsDone);

            var visit = PlayToHerArrival(run);                     // 3: thanks, and nothing to hand over yet
            Assert.IsNotNull(visit);
            Assert.AreEqual("serve_one", visit.Finished?.Id);
            Assert.IsNull(visit.Offered, "a rung gate makes her wait — it never skips the row");
            Assert.IsFalse(visit.Finale);
            Assert.AreSame(BarRank.Rungs[2], visit.WaitingFor);
            run.HearHostess();
            Assert.AreEqual(0, run.QuestsSkipped);
            Assert.AreEqual("door_job", run.QuestNextUp?.Id);
            Assert.IsFalse(run.QuestChainOver);
            PlayNight(run);

            for (int night = 4; night <= 5; night++)
            {
                Assert.IsNull(PlayToHerArrival(run), "night " + night + ": nothing to say, so she stays home");
                PlayNight(run);
            }

            run.Rating.DevSet(1.0);                                // the door's rung
            visit = PlayToHerArrival(run);
            Assert.IsNotNull(visit, "the rung is reached: she comes with it");
            Assert.IsNull(visit.Finished);
            Assert.AreEqual("door_job", visit.Offered?.Id);
        }

        // ── 8, 9. rows passed over ──────────────────────────────────────────────────

        [Test]
        public void ADoneDealIsSkipped()
        {
            var run = NewRun(Book(Q("half_a_star", QuestKind.Rank, goalRung: 1), Q("then", QuestKind.Perfect)), "done-deal");
            run.Rating.DevSet(0.5);                                // the bar already stands on rung 1
            var visit = PlayToHerArrival(run);
            Assert.AreEqual("then", visit?.Offered?.Id, "she never hands over a done deal");
            Assert.AreEqual(1, run.QuestsSkipped);
            run.HearHostess();
            Assert.IsNull(run.QuestNextUp, "the book moved past both rows");
        }

        [Test]
        public void AServeWithNothingToPourIsSkipped()
        {
            var run = NewRun(Book(Q("stir_it", QuestKind.Serve, rung: 3, pick: QuestPick.Stirred),
                Q("then", QuestKind.Perfect, rung: 3)), "nothing-stirred");
            run.Rating.DevSet(2.0);                                // the spoon's rung — and not one stirred page
            var visit = PlayToHerArrival(run);
            Assert.AreEqual("then", visit?.Offered?.Id, "a job that could not be done as written is passed over");
            Assert.AreEqual(1, run.QuestsSkipped);
        }

        // ── 10. the save ────────────────────────────────────────────────────────────

        private static TycoonRun Reborn(RunSnapshot snap, QuestBook book) =>
            TycoonRun.Restore(snap, Menu, new[] { Gin, Soda }, config: Config(), quests: book);

        private static string StreamState(RunSnapshot snap, string name) =>
            snap.streams.FirstOrDefault(s => s.name == name)?.state;

        [Test]
        public void AMidJobRidesTheSnapshot_AndABookTheSaveDisagreesWithIsRefused()
        {
            var book = Book(Q("serve_three", QuestKind.Serve, target: 3, reward: 30), Q("then", QuestKind.Perfect));
            var run = NewRun(book, "snapshot");
            PlayNight(run, serve: false);                          // handed over at the close of night 1
            int guard = 0, poured = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 5000);
                run.Tick(1.0);
                TestNight.Clean(run);
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (run.Phase != TycoonPhase.DayOpen || v.State != VisitState.Waiting || !v.HasOrdered) continue;
                    if (poured >= 2) { run.DeclineOrder(v); continue; }
                    v.InspectId();
                    Pour(run);
                    run.ServeTo(v);
                    poured++;
                }
            }
            Assert.AreEqual(2, run.Quest.Progress);
            RunSnapshot snap = null;
            run.ContinueToNextDay(s => snap = s);
            Assert.AreEqual(1, snap.questFormat);
            Assert.IsTrue(snap.hasQuests);

            var back = Reborn(snap, book);
            Assert.AreEqual(run.Quest.Id, back.Quest.Id);
            Assert.AreEqual(run.Quest.RecipeId, back.Quest.RecipeId);
            Assert.AreEqual(2, back.Quest.Progress);
            Assert.AreEqual(run.Quest.Target, back.Quest.Target);
            Assert.AreEqual(run.Quest.Reward, back.Quest.Reward);
            Assert.AreEqual(1, back.Quest.GivenDay);
            Assert.AreEqual(run.QuestNextUp?.Id, back.QuestNextUp?.Id);
            Assert.AreEqual(run.HostessComesOn, back.HostessComesOn);
            Assert.AreEqual(run.QuestsSkipped, back.QuestsSkipped);
            Assert.AreEqual(run.QuestDoneUnsaid, back.QuestDoneUnsaid);

            Assert.Throws<ArgumentException>(() => Reborn(snap, null),
                "a save written with the book cannot land in a run without one");
            snap.quest.id = "a_job_nobody_wrote";
            Assert.Throws<ArgumentException>(() => Reborn(snap, book), "nor name a job the book no longer has");
        }

        [Test]
        public void AnUnpourableDrinkIsRepairedOnRestore()
        {
            var book = Book(Q("serve_three", QuestKind.Serve, target: 3, reward: 30), Q("then", QuestKind.Perfect));
            var run = NewRun(book, "repair");
            RunSnapshot snap = null;
            PlayNightThen(run, s => snap = s);                    // handed over at the close of night 1
            Assert.AreEqual("spritz", snap.quest.recipeId);
            string questStream = StreamState(snap, "quest");
            Assert.IsNotNull(questStream, "her pick drew once, at her arrival");

            // A data update took the drink away: the save names a page the shelf cannot pour.
            snap.quest.recipeId = "sour";
            snap.quest.progress = 2;
            var back = Reborn(snap, book);
            Assert.AreEqual("spritz", back.Quest.RecipeId, "pointed at the first pourable page of the same rule");
            Assert.AreEqual(0, back.Quest.Progress, "the count starts again");
            Assert.AreEqual(3, back.Quest.Target, "the target is kept");
            Assert.AreEqual(30, back.Quest.Reward, "and so is the pay");

            // …and no die was rolled for it: the next dawn's "quest" stream is where the save left it.
            RunSnapshot next = null;
            PlayNightThen(back, s => next = s);
            Assert.AreEqual(questStream, StreamState(next, "quest"));
        }

        private static void PlayNightThen(TycoonRun run, Action<RunSnapshot> atDawn)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(run, serve: false); }
            run.ContinueToNextDay(atDawn);
        }

        // ── 11. the end of the book ─────────────────────────────────────────────────

        [Test]
        public void TheChainEndsInTheFinale()
        {
            var run = NewRun(Book(Q("first", QuestKind.Serve), Q("second", QuestKind.Serve)), "finale");
            HostessVisit finale = null;
            for (int night = 1; night <= 8 && finale == null; night++)
            {
                var visit = PlayToHerArrival(run);
                if (visit != null && visit.Finale) finale = visit;
                PlayNight(run);
            }
            Assert.IsNotNull(finale, "she comes once more after the last job");
            Assert.AreEqual("second", finale.Finished?.Id);
            Assert.IsNull(finale.Offered);
            Assert.IsNull(finale.WaitingFor);
            Assert.IsTrue(run.QuestChainOver, "heard out, the book is spent");
            Assert.AreEqual(0, run.HostessComesOn);
            Assert.AreEqual("second", run.Quest?.Id, "the last job stays on the bar, done");

            for (int night = 0; night < 3; night++)
            {
                Assert.IsNull(PlayToHerArrival(run), "and she stops coming");
                PlayNight(run);
            }
        }

        // ── 12. the same order every run ────────────────────────────────────────────

        [Test]
        public void TheOrderIsTheSameEveryRun()
        {
            var seen = new List<string>();
            foreach (string seed in new[] { "ORDER-A", "ORDER-B", "ORDER-C" })
            {
                var book = Book(
                    Q("a_serve", QuestKind.Serve, target: 2),
                    Q("b_clean", QuestKind.Clean),
                    Q("c_rank", QuestKind.Rank, goalRung: 1),
                    Q("d_serve", QuestKind.Serve, rung: 1),
                    Q("e_rank", QuestKind.Rank, rung: 1, goalRung: 2));
                var run = NewRun(book, seed);
                var handed = new List<string>();
                for (int night = 1; night <= 14; night++)
                {
                    int guard = 0;
                    while (run.Phase == TycoonPhase.DayOpen)
                    {
                        Assert.Less(guard++, 5000);
                        Step(run, serve: true);
                        var given = run.TakeQuestJustGiven();
                        if (given != null) handed.Add(given.Id);
                    }
                    // The scripted climb: a room with no fittings files no stars of its own.
                    if (night == 6) run.Rating.DevSet(0.5);
                    if (night == 10) run.Rating.DevSet(1.0);
                    run.ContinueToNextDay();
                }
                Assert.IsTrue(run.QuestChainOver, seed + ": the whole book was walked");
                Assert.AreEqual(0, run.QuestsSkipped, seed);
                seen.Add(string.Join(" ", handed));
            }
            Assert.AreEqual("a_serve b_clean c_rank d_serve e_rank", seen[0], "the book's order, row by row");
            Assert.AreEqual(seen[0], seen[1]);
            Assert.AreEqual(seen[0], seen[2]);
        }

        // ── 13. what counts ─────────────────────────────────────────────────────────

        [Test]
        public void EveryKindCountsOnlyWhatItSays()
        {
            var ice = new[] { Preparations.Ice };
            var salt = new[] { Preparations.SaltRim };
            var serve = Q("s", QuestKind.Serve);
            var perfect = Q("p", QuestKind.Perfect);
            var pints = Q("b", QuestKind.Pints);
            var garnish = Q("g", QuestKind.Garnish, rung: 1, preps: new[] { Preparations.Ice, Preparations.LemonTwist });
            bool Counts(QuestDefinition def, string wanted = "negroni", OrderMatch match = OrderMatch.Exact,
                bool perfectMake = false, bool pint = false, bool craft = false,
                IReadOnlyList<PreparationDefinition> asked = null) =>
                QuestRules.ServeCounts(def, "negroni", match, wanted, perfectMake, pint, craft, asked);

            Assert.IsTrue(Counts(serve));
            Assert.IsFalse(Counts(serve, wanted: "spritz"), "somebody else's drink");
            Assert.IsFalse(Counts(serve, match: OrderMatch.Close), "a near miss is not the drink");
            Assert.IsFalse(QuestRules.ServeCounts(serve, "", OrderMatch.Exact, "", false, false, false, null));

            Assert.IsTrue(Counts(perfect, wanted: "spritz", perfectMake: true), "any drink, poured perfectly");
            Assert.IsFalse(Counts(perfect, craft: true));
            Assert.IsFalse(Counts(perfect, match: OrderMatch.Wrong, perfectMake: true));

            Assert.IsTrue(Counts(pints, pint: true, craft: true), "a pint with its head in the band");
            Assert.IsFalse(Counts(pints, pint: true), "a flat pint");
            Assert.IsFalse(Counts(pints, craft: true), "a cocktail made well is not a pint");

            Assert.IsTrue(Counts(garnish, craft: true, asked: ice), "asked for ice and got every ask");
            Assert.IsFalse(Counts(garnish, craft: true, asked: salt), "an ask that is not hers");
            Assert.IsFalse(Counts(garnish, asked: ice), "an ask missed");
            Assert.IsFalse(Counts(garnish, pint: true, craft: true, asked: ice), "never a pint");
            Assert.IsFalse(Counts(garnish, craft: true), "a plain order");

            foreach (var def in new[]
                     {
                         Q("c", QuestKind.Clean), Q("d", QuestKind.Door, rung: 2),
                         Q("r", QuestKind.Rank, goalRung: 1), Q("m", QuestKind.Comfort, goalComfort: 1.0),
                         Q("f", QuestKind.Fit, slot: "wall_center", level: 1, fixtureId: "pic_1"),
                     })
                Assert.IsFalse(Counts(def, perfectMake: true, pint: true, craft: true, asked: ice),
                    def.Kind + " is never counted on a serve");
        }

        [Test]
        public void ADrinkServedToSomebodyWhoShouldHaveBeenShownTheDoor_CountsForNothing()
        {
            // A deep purse: every adult is told no until a minor comes, and nights that sell nothing still pay rent.
            var run = NewRun(Book(Q("serve_many", QuestKind.Serve, target: 12, reward: 50)), "door-right",
                people: true, money: 1_000_000);
            run.Rating.DevSet(BarRank.Granting(Feature.Door).Stars);   // minors come in from the door's rung
            PlayNight(run, serve: false);
            Assert.AreEqual("serve_many", run.Quest?.Id);

            // Tell every adult no, so the count holds still, until somebody the card says to show the door sits down.
            CustomerVisit minor = null;
            for (int night = 0; night < 80 && minor == null; night++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen && minor == null)
                {
                    Assert.Less(guard++, 5000);
                    run.Tick(1.0);
                    TestNight.Clean(run);
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                        v.InspectId();
                        if (v.Papers != null && v.Papers.ShouldBeKicked) { minor = v; break; }
                        run.DeclineOrder(v);
                    }
                }
                if (minor == null) run.ContinueToNextDay();
            }
            Assert.IsNotNull(minor, "no minor came in eighty nights");
            Assert.AreEqual(0, run.Quest.Progress);

            Pour(run);
            Assert.AreEqual(OrderMatch.Exact, run.ServeTo(minor).Match, "their drink, made right");
            Assert.AreEqual(0, run.Quest.Progress, "and it counts for nothing");
            Assert.AreEqual(0, run.DayQuestPaid);

            // The next adult served the same drink counts.
            int guard2 = 0;
            while (run.Quest.Progress == 0)
            {
                Assert.Less(guard2++, 20000, "an adult must come in");
                if (run.Phase == TycoonPhase.DayEnd) { run.ContinueToNextDay(); continue; }
                bool served = false;
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                    v.InspectId();
                    if (v.Papers != null && v.Papers.ShouldBeKicked) { run.Kick(v); continue; }
                    Pour(run);
                    run.ServeTo(v);
                    served = true;
                    break;
                }
                if (!served) { run.Tick(1.0); TestNight.Clean(run); }
            }
            Assert.AreEqual(1, run.Quest.Progress, "an adult's drink is counted");
        }

        [Test]
        public void ARightKickCountsForTheDoorJob()
        {
            var run = NewRun(Book(Q("one_kick", QuestKind.Door, rung: 2, reward: 64)), "door-right",
                people: true, money: 1_000_000);
            run.Rating.DevSet(BarRank.Granting(Feature.Door).Stars);
            PlayNight(run, serve: false);
            Assert.AreEqual("one_kick", run.Quest?.Id);
            for (int night = 0; night < 80 && !run.Quest.IsDone; night++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 5000);
                    run.Tick(1.0);
                    TestNight.Clean(run);
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                        v.InspectId();
                        if (v.Papers != null && v.Papers.ShouldBeKicked)
                        {
                            int hers = run.DayQuestPaid;
                            bool wasDone = run.Quest.IsDone;
                            run.Kick(v);
                            if (!wasDone && run.Quest.IsDone)
                                Assert.AreEqual(64, run.DayQuestPaid - hers, "paid on the kick");
                        }
                        else run.DeclineOrder(v);
                    }
                }
                run.ContinueToNextDay();
            }
            Assert.IsTrue(run.Quest.IsDone, "a right kick did the job");
        }

        // ── 14. the dev verbs ───────────────────────────────────────────────────────

        [Test]
        public void DevJumpToNight_ClearsTheQuestPay()
        {
            var run = NewRun(Book(Q("serve_one", QuestKind.Serve, reward: 15), Q("then", QuestKind.Perfect)), "dev-jump");
            PlayNight(run, serve: false);
            // Night 2: the job done, and a wrong drink over the bar.
            int guard = 0;
            while (!run.Quest.IsDone)
            {
                Assert.Less(guard++, 5000);
                run.Tick(1.0);
                TestNight.Clean(run);
                ServeWaiting(run);
            }
            var someone = run.Floor.Seated.FirstOrDefault(v => v.State == VisitState.Waiting && v.HasOrdered);
            guard = 0;
            while (someone == null)
            {
                Assert.Less(guard++, 5000);
                Assert.AreEqual(TycoonPhase.DayOpen, run.Phase, "somebody else comes in on night two");
                run.Tick(1.0);
                TestNight.Clean(run);
                someone = run.Floor.Seated.FirstOrDefault(v => v.State == VisitState.Waiting && v.HasOrdered);
            }
            someone.InspectId();
            run.PourMeasure("gin", 0.7);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            Assert.AreNotEqual(OrderMatch.Exact, run.ServeTo(someone).Match);
            Assert.AreEqual(15, run.DayQuestPaid);
            Assert.IsTrue(run.NightHadAMistake);

            run.DevJumpToNight(9);
            Assert.AreEqual(0, run.DayQuestPaid, "cleared with the bonus it is part of");
            Assert.AreEqual(0, run.DayBonus);
            Assert.IsFalse(run.NightHadAMistake, "the jumped-to night starts clean");
            Assert.LessOrEqual(run.HostessComesOn, run.Day);
            Assert.IsNull(run.HostessVisit);
            Assert.AreEqual("serve_one", PlayToHerArrival(run)?.Finished?.Id, "and she still comes to say it");
        }

        [Test]
        public void APresetThatWindsTheCalendarBack_BringsHerVisitWithIt()
        {
            var run = NewRun(Book(Q("serve_one", QuestKind.Serve), Q("then", QuestKind.Perfect)), "dev-preset");
            run.DevJumpToNight(20);
            PlayNight(run, serve: false);                         // 20: handed over
            PlayNight(run);                                       // 21: done
            Assert.IsTrue(run.Quest.IsDone);
            Assert.AreEqual(22, run.HostessComesOn);

            run.DevPresetStars(0.5);                              // back to night 3
            Assert.AreEqual(3, run.Day);
            Assert.AreEqual(3, run.HostessComesOn, "a visit booked past the wound-back calendar is tonight's");
            Assert.AreEqual("serve_one", PlayToHerArrival(run)?.Finished?.Id);
        }

        [Test]
        public void APresetThatWindsTheCalendarBack_BringsAnOpenJobsNightWithIt()
        {
            var run = NewRun(Book(Q("clean_one", QuestKind.Clean, reward: 24), Q("then", QuestKind.Perfect)), "dev-rewind");
            run.DevJumpToNight(20);
            PlayNight(run, serve: false);                         // 20: handed over
            Assert.AreEqual("clean_one", run.Quest?.Id);
            Assert.AreEqual(20, run.Quest.GivenDay);

            run.DevPresetStars(0.5);                              // back to night 3
            Assert.AreEqual(3, run.Day);
            Assert.That(run.Quest.GivenDay, Is.LessThan(run.Day), "the job is dated no later than last night");

            // Night 3 is a night after the hand-over now: served promptly and right, it counts.
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(run, serve: true); }
            Assert.IsTrue(run.Quest.IsDone, "a clean night on the wound-back calendar did the job");
            Assert.AreEqual(3, run.Quest.DoneDay);
        }

        // ── 15. her drink is ordered ────────────────────────────────────────────────

        /// <summary>A job-less twin and the run under test, the same seed, both held at four stars through the dawn —
        /// what the plan is cut from is then the same in both, so their plans can be laid side by side.</summary>
        private static void HoldAtFourStars(params TycoonRun[] runs)
        {
            foreach (var run in runs) run.Rating.DevSet(4.0);
        }

        private static string[] PlanOf(TycoonRun run) => run.Plan.Queue.Select(r => r.Id).ToArray();

        private static void DeclineTheNight(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 5000); Step(run, serve: false); }
            run.ContinueToNextDay();
        }

        [Test]
        public void AServeJobsDrinkIsOnEveryNightsPlanWhileItIsOpen()
        {
            var book = Book(Q("stir_three", QuestKind.Serve, rung: 3, target: 3, pick: QuestPick.Stirred),
                Q("then", QuestKind.Perfect, rung: 3));
            var run = NewRun(book, "kept-cover", menu: Ladder, shelf: LadderShelf());
            var twin = NewRun(null, "kept-cover", menu: Ladder, shelf: LadderShelf());
            HoldAtFourStars(run, twin);
            CollectionAssert.AreEqual(PlanOf(twin), PlanOf(run), "no job yet: the same seed plans the same night");
            DeclineTheNight(run);
            DeclineTheNight(twin);
            Assert.AreEqual("low_stir", run.Quest?.RecipeId, "the one stirred page, on the ground floor");

            int starved = 0;
            var slots = new HashSet<int>();
            for (int night = 2; night <= 9; night++)
            {
                HoldAtFourStars(run, twin);
                Assert.IsFalse(run.Quest.IsDone, "nobody is served, so the job stays open");
                string[] mine = PlanOf(run), theirs = PlanOf(twin);
                Assert.Contains("low_stir", mine, "night " + night + ": her drink is on the plan");
                Assert.AreEqual(theirs.Length, mine.Length, "the night is as long as it was");
                var moved = Enumerable.Range(0, mine.Length).Where(i => mine[i] != theirs[i]).ToList();
                if (theirs.Contains("low_stir"))
                {
                    Assert.IsEmpty(moved, "night " + night + ": already planned, nothing moves");
                }
                else
                {
                    starved++;
                    Assert.AreEqual(1, moved.Count, "night " + night + ": exactly one cover is given up");
                    Assert.AreEqual("low_stir", mine[moved[0]]);
                    Assert.Greater(moved[0], 0, "never the night's first drinker");
                    Assert.LessOrEqual(moved[0], mine.Length / 2, "in the first half of the night");
                    slots.Add(moved[0]);
                }
                DeclineTheNight(run);
                DeclineTheNight(twin);
            }
            Assert.Greater(starved, 3, "a four-star plan does not ask for a ground-floor page by itself");
            Assert.Greater(slots.Count, 1, "the kept cover falls where the shuffle put its page, not on one drinker");
        }

        [Test]
        public void AJobThatCountsNoDrinkPlansExactlyAsBefore()
        {
            var run = NewRun(Book(Q("perfect_two", QuestKind.Perfect, rung: 3, target: 2)), "no-drink",
                menu: Ladder, shelf: LadderShelf());
            var twin = NewRun(null, "no-drink", menu: Ladder, shelf: LadderShelf());
            for (int night = 1; night <= 6; night++)
            {
                HoldAtFourStars(run, twin);
                CollectionAssert.AreEqual(PlanOf(twin), PlanOf(run), "night " + night + ": the same plan, cover for cover");
                DeclineTheNight(run);
                DeclineTheNight(twin);
            }
            Assert.AreEqual("perfect_two", run.Quest?.Id, "the job was on the bar the whole time");
        }

        /// <summary>A pint pulled the way the mechanic asks — leaned over while it fills, then stood up until the head
        /// is in its band.</summary>
        private static void PullAGoodPint(TycoonRun run)
        {
            run.BeginPull("lager");
            for (int i = 0; i < 200 && run.ServingGlass.FillFraction < 0.78; i++) run.PourTilted(0.05, TapPour.IdealTilt);
            for (int i = 0; i < 200 && run.ServingGlass.Head / run.ServingGlass.Capacity < TapPour.IdealHead
                            && !run.ServingGlass.IsFull; i++)
                run.PourTilted(0.05, 0.0);
            run.EndPull();
        }

        [Test]
        public void APintJobHandedToAFourStarBar_IsDone()
        {
            var book = Book(Q("proper_head", QuestKind.Pints, rung: 3, target: 3, reward: 108));
            var run = NewRun(book, "four-star-pints", menu: Ladder, shelf: LadderShelf());
            var twin = NewRun(null, "four-star-pints", menu: Ladder, shelf: LadderShelf());
            HoldAtFourStars(run, twin);
            DeclineTheNight(run);
            DeclineTheNight(twin);
            Assert.AreEqual("proper_head", run.Quest?.Id);

            int nights = 0, twinPints = 0;
            while (!run.Quest.IsDone)
            {
                Assert.Less(nights++, 6, "three good pints within six nights of the hand-over");
                HoldAtFourStars(run, twin);
                twinPints += PlanOf(twin).Count(id => id == "draught");
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 5000);
                    run.Tick(1.0);
                    TestNight.Clean(run);
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered) continue;
                        v.InspectId();
                        if (v.Order.Wanted.Id != "draught") { run.DeclineOrder(v); continue; }
                        PullAGoodPint(run);
                        var verdict = run.ServeTo(v);
                        Assert.AreEqual(OrderMatch.Exact, verdict.Match, "a pint, as ordered");
                        Assert.IsTrue(verdict.CraftLanded, "with its head in the band");
                    }
                }
                run.ContinueToNextDay();
                DeclineTheNight(twin);
            }
            Assert.AreEqual(0, twinPints, "the job-less twin was never asked for a pint on those nights");
        }

        // ── 16. a book that moved under a save ──────────────────────────────────────

        [Test]
        public void ARowPutInFrontOfTheJob_NeverHandsItOverTwice()
        {
            var book = Book(Q("serve_one", QuestKind.Serve, reward: 15), Q("then", QuestKind.Perfect));
            var run = NewRun(book, "moved-book");
            RunSnapshot snap = null;
            PlayNightThen(run, s => snap = s);                    // handed over at the close of night 1
            Assert.AreEqual("serve_one", snap.quest.id);
            Assert.AreEqual("then", snap.questNextId, "the next row is carried by its id");

            // A data update writes a new first row: every row behind it moves down one.
            var moved = Book(Q("a_new_first_row", QuestKind.Perfect), Q("serve_one", QuestKind.Serve, reward: 15),
                Q("then", QuestKind.Perfect));
            var back = Reborn(snap, moved);
            Assert.AreEqual("serve_one", back.Quest.Id);
            Assert.AreEqual("then", back.QuestNextUp?.Id, "she hands over what came after it, not the job again");

            // ...and a save written before the id rode along falls back on the index, never at or behind the job.
            snap.questNextId = "";
            Assert.AreEqual("then", Reborn(snap, moved).QuestNextUp?.Id);

            // A row taken out in front of it: the id finds the next row where the index would have skipped it.
            var shorter = Book(Q("serve_one", QuestKind.Serve, reward: 15), Q("then", QuestKind.Perfect));
            snap.questNext = 2;
            snap.questNextId = "then";
            Assert.AreEqual("then", Reborn(snap, shorter).QuestNextUp?.Id);
        }
    }
}
