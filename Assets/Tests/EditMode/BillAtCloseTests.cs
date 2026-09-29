using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE BILL ON THE BEAM AND THE DAWN THAT SHUTS THE BAR (2026-09-28, the top bar's register and the game over).
    /// Core now answers two questions before the close does the thing they ask about: what tonight's close will take
    /// from the till (<see cref="TycoonRun.BillAtClose"/> and its three parts), and whether this dawn is the one that
    /// shuts the bar (<see cref="TycoonRun.ClosesAtDawn"/>). Both are previews, and a preview can only be pinned one
    /// way: asked, then checked against what the close and the dawn then actually did, night after night, seed after
    /// seed. A false "closes" would skip a market the player could have used; a bill that disagrees with the slip
    /// would make the beam's RED NIGHTS lamp a liar.
    ///
    /// Played on the door bench (DoorWiringTests' bar: gin, soda, one spritz page, the regulars with their papers,
    /// the door granted) with a short purse, so red nights come often and some runs go under, and with a hostess's
    /// book whose jobs pay on every road there is — a right kick mid-shift, a clean night at the close, a picture at
    /// the dawn — so the state's thanks can never be confused with her pay. Pure Core, unlike SaveTests: its loader
    /// needs the editor, and neither rule needs the shipped content to be pinned.
    /// </summary>
    public sealed class BillAtCloseTests
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
        };

        private static readonly IReadOnlyList<FixtureDefinition> Wall = new[]
        {
            new FixtureDefinition("pic_1", "Picture 1", "wall_center", 20, 0.0,
                "Something on the wall.", "fx_pic_1", level: 1, comfort: 0.1),
        };

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(Gin.Clone(), capacity: 40000),
            new ShelfBottle(Soda.Clone(), capacity: 40000),
        });

        private static TycoonConfig Config(int money) =>
            new TycoonConfig(startingMoney: money, orderDecisionSeconds: 0, savorSeconds: 0);

        private static RegularsRegistry People() => new RegularsRegistry(new[]
        {
            new ArchetypeDefinition("after_shift", "Off the Late Shift",
                new[] { "Marguerite", "Dev", "Ola", "Kit", "Rasmus", "Nuray" }, 1,
                new[] { "this side of town" }),
        });

        private static QuestDefinition Q(string id, QuestKind kind, int target = 1, int reward = 10,
            string slot = null, int level = 0, string fixtureId = null, int rung = 0) =>
            new QuestDefinition(id, kind, rung, target, reward, id.ToUpperInvariant(),
                new[] { "{name} has a job for you." }, new[] { "Thank you." },
                slot: slot, level: level, fixtureId: fixtureId);

        private static QuestBook Book(params QuestDefinition[] rows) =>
            new QuestBook(rows, new[] { "That is the whole book." }, "The next one waits till we're {rung}.");

        /// <summary>Her jobs, one per road her pay can take: mid-shift (on the very nights the state thanks the same
        /// kicks), at the close, at the dawn. All on the door's rung, which the bench stands on from night one.</summary>
        private static QuestBook EveryRoad()
        {
            int door = BarRank.Granting(Feature.Door).Index;
            return Book(
                Q("door_two", QuestKind.Door, target: 2, reward: 25, rung: door),
                Q("clean_one", QuestKind.Clean, reward: 30, rung: door),
                Q("hang_one", QuestKind.Fit, reward: 60, slot: "wall_center", level: 1, fixtureId: "pic_1", rung: door),
                Q("then", QuestKind.Perfect, rung: door));
        }

        private static TycoonRun NewRun(string seed, int money, QuestBook book, bool door = true)
        {
            var run = new TycoonRun(NewShelf(), Menu, new RunRng(seed), config: Config(money),
                regulars: People(), fixtures: Wall, quests: book);
            if (door) run.Rating.DevSet(BarRank.Granting(Feature.Door).Stars);
            return run;
        }

        private static TycoonRun Reborn(RunSnapshot snap, QuestBook book) =>
            TycoonRun.Restore(snap, Menu, new[] { Gin, Soda }, config: Config(0), regulars: People(),
                fixtures: Wall, quests: book);

        // ── the hands ──────────────────────────────────────────────────────────────

        /// <summary>How a night is worked: every order answered (minors shown the door), nobody answered (they all
        /// walk), or every other face answered and the rest left to walk.</summary>
        private enum Hand { Worked, Idle, Half }

        /// <summary>Each seed works its own fortnight: bit (night % 5) of a number read off the seed says whether the
        /// night is worked at all, and odd seeds only half-work theirs. Deterministic, and wide enough that some runs
        /// go under, some stay red without going under, and some never go red.</summary>
        private static Hand HandFor(int seed, int night)
        {
            int mask = (seed * 7 + 3) % 32;
            if ((mask >> (night % 5) & 1) == 0) return Hand.Idle;
            return seed % 2 == 1 ? Hand.Half : Hand.Worked;
        }

        private static void Pour(TycoonRun run)
        {
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        /// <summary>What tonight's bill was asked for across a night, for the non-vacuity checks.</summary>
        private sealed class Seen
        {
            public int Nights, Thanks, WalkOuts, HerPay, Both, StrikeLit, ClosingTickWalkOuts;
        }

        /// <summary>
        /// Plays one night to its books. With <paramref name="seen"/> the bill is asked at every step — its sum, its
        /// strike — and at the close it is held against what the close charged: the bill read on the last open step,
        /// plus whatever walk-outs got up inside the closing tick itself (they are settled in the same tick, before
        /// the close charges them), must be the rent, the walk-outs and the thanks the books then show.
        /// </summary>
        private static void PlayNight(TycoonRun run, Hand hand, Seen seen = null)
        {
            var answered = new HashSet<CustomerVisit>();
            var ignored = new HashSet<CustomerVisit>();
            int guard = 0, faces = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 20000, "the night must terminate");
                int billSeen = run.BillAtClose, owedSeen = run.WalkOutsOwed;
                if (seen != null)
                {
                    Assert.AreEqual(run.RentTonight + run.WalkOutsOwed - run.ThanksTonight, billSeen,
                        "the bill is rent plus walk-outs less the thanks");
                    Assert.AreEqual(run.Money - billSeen < 0, run.StrikeTonight,
                        "the strike lamp is the till against the bill");
                    if (run.StrikeTonight) seen.StrikeLit++;
                }
                if (run.Floor.IsComplete && !run.Floor.House.CounterClear) run.Floor.House.SweepForClosing();
                run.Tick(2.0);
                if (run.Phase != TycoonPhase.DayOpen)
                {
                    if (seen == null) break;
                    int charged = run.DayRent + run.DayWalkOutFees - (run.DayBonus - run.DayQuestPaid);
                    int lateWalkOuts = run.WalkOutsOwed - owedSeen;
                    Assert.AreEqual(billSeen + lateWalkOuts, charged,
                        "the close charged the bill the beam showed, and the walk-outs of its own tick");
                    if (lateWalkOuts > 0) seen.ClosingTickWalkOuts++;
                    break;
                }
                TestNight.Clean(run);
                if (hand == Hand.Idle) continue;
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    if (v.State != VisitState.Waiting || !v.HasOrdered || ignored.Contains(v)) continue;
                    if (!answered.Contains(v) && hand == Hand.Half && faces++ % 2 == 1) { ignored.Add(v); continue; }
                    answered.Add(v);
                    v.InspectId();
                    if (v.Papers != null && v.Papers.ShouldBeKicked && run.Has(Feature.Door)) { run.Kick(v); continue; }
                    if (!run.CanMake(v.Order)) { run.DeclineOrder(v); continue; }
                    Pour(run);
                    run.ServeTo(v);
                }
            }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "a night ends at its books");
        }

        // ── the bill ────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE BILL ON THE BEAM IS THE BILL THE CLOSE CHARGED. Asked at every step of every night (PlayNight), and
        /// at every set of books: the rent the books took is the rent the beam read, the bonus less her pay is the
        /// thanks, the walk-outs charged are the walk-outs owed — and once the night is closed the beam has no bill.
        /// </summary>
        [Test]
        public void TheBillOnTheBeam_IsTheBillTheCloseCharged()
        {
            var seen = new Seen();
            for (int i = 0; i < 30; i++)
            {
                var run = NewRun("BILL-" + i, money: 20, book: EveryRoad());
                for (int night = 1; night <= 8; night++)
                {
                    PlayNight(run, HandFor(i, night), seen);
                    string at = "seed " + i + " night " + night;
                    Assert.AreEqual(run.DayRent, run.RentTonight, at + ": the rent");
                    Assert.AreEqual(run.DayBonus - run.DayQuestPaid, run.ThanksTonight, at + ": the thanks");
                    Assert.AreEqual(run.DayWalkOutFees, run.WalkOutsOwed, at + ": the walk-outs");
                    Assert.AreEqual(0, run.BillAtClose, at + ": after the close the bill is on the slip");
                    Assert.IsFalse(run.StrikeTonight, at + ": and the lamp waits for the next open night");
                    seen.Nights++;
                    if (run.ThanksTonight > 0) seen.Thanks++;
                    if (run.WalkOutsOwed > 0) seen.WalkOuts++;
                    if (run.DayQuestPaid > 0) seen.HerPay++;
                    if (run.ThanksTonight > 0 && run.DayQuestPaid > 0) seen.Both++;
                    // The picture goes up at the books, so her book moves on past its dawn job.
                    if (run.Quest?.Id == "hang_one" && !run.Quest.IsDone) run.DevFit("pic_1");
                    run.ContinueToNextDay();
                    if (run.Phase == TycoonPhase.Closed) break;
                }
            }
            TestContext.WriteLine($"nights {seen.Nights}, thanks {seen.Thanks}, walk-outs {seen.WalkOuts}, her pay " +
                                  $"{seen.HerPay}, both {seen.Both}, strike lit {seen.StrikeLit}, " +
                                  $"walk-outs in the closing tick {seen.ClosingTickWalkOuts}");
            Assert.Greater(seen.Thanks, 0, "some nights were thanked for a right kick");
            Assert.Greater(seen.WalkOuts, 0, "some nights paid for walk-outs");
            Assert.Greater(seen.Both, 0, "and some nights paid her beside the thanks, which must not swallow her pay");
            Assert.Greater(seen.StrikeLit, 0, "and the strike lamp was lit on some step");
        }

        // ── the dawn ────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE DAWN THAT SHUTS THE BAR IS FORETOLD AT THE BOOKS: at every night's end, what ClosesAtDawn says is what
        /// ContinueToNextDay then does. On a third of the seeds the picture goes up at the books while her picture job
        /// is on the bar, so the dawn's own pay is part of the forecast on the nights it can decide it.
        /// </summary>
        [Test]
        public void TheDawnThatShutsTheBar_IsForetoldAtTheBooks()
        {
            int closed = 0, redSurvived = 0, black = 0;
            for (int i = 0; i < 30; i++)
            {
                var run = NewRun("DAWN-" + i, money: 20, book: EveryRoad());
                for (int night = 1; night <= 10; night++)
                {
                    PlayNight(run, HandFor(i, night));
                    if (i % 3 == 0 && run.Quest?.Id == "hang_one" && !run.Quest.IsDone) run.DevFit("pic_1");
                    bool foretold = run.ClosesAtDawn;
                    bool redTill = DayLedger.IsRedClose(run.Money);
                    Assert.AreEqual(run.Ledger.WouldClose(run.Money + (run.QuestPaysAtDawn ? run.Quest.Reward : 0)),
                        foretold, "the forecast is the ledger's own rule");
                    run.ContinueToNextDay();
                    Assert.AreEqual(foretold, run.Phase == TycoonPhase.Closed,
                        "seed " + i + " night " + night + ": the dawn did what the books foretold");
                    if (run.Phase == TycoonPhase.Closed) { closed++; break; }
                    if (redTill) redSurvived++; else black++;
                }
                Assert.IsFalse(run.ClosesAtDawn, "only a night's end can foretell a dawn");
            }
            TestContext.WriteLine($"closed {closed}, red and survived {redSurvived}, black {black}");
            Assert.Greater(closed, 0, "some bars went under");
            Assert.Greater(redSurvived, 0, "some red nights were not the last one");
            Assert.Greater(black, 0, "and some nights closed in the black");
        }

        /// <summary>
        /// HER PAY AT THE DAWN IS IN THE FORECAST. Two red nights on the books, a third red till at the books, and her
        /// picture job on the bar: with the wall bare the dawn shuts the bar; with the picture up, the dawn pays her
        /// job before it files the night, the till closes in the black, and the forecast has to have known.
        /// </summary>
        [Test]
        public void HerPayAtDawn_IsInTheForecast()
        {
            foreach (bool pictureUp in new[] { false, true })
            {
                var book = Book(Q("hang_one", QuestKind.Fit, reward: QuestRules.MaxReward, slot: "wall_center",
                    level: 1, fixtureId: "pic_1"), Q("then", QuestKind.Perfect));
                var run = NewRun("DAWN-PAY", money: 0, book: book, door: false);
                for (int night = 1; night <= 2; night++)
                {
                    PlayNight(run, Hand.Idle);
                    Assert.IsFalse(run.ClosesAtDawn, "night " + night + " is not yet the third");
                    run.ContinueToNextDay();
                }
                Assert.AreEqual(DayLedger.StrikesToClose - 1, run.Ledger.DebtStrikes, "two red nights on the books");
                Assert.AreEqual("hang_one", run.Quest?.Id, "her picture job is on the bar");

                PlayNight(run, Hand.Idle);
                Assert.Less(run.Money, 0, "a third red till at the books");
                Assert.GreaterOrEqual(run.Money + run.Quest.Reward, 0, "her pay would cover it");
                if (pictureUp) run.DevFit("pic_1");
                Assert.AreEqual(pictureUp, run.QuestPaysAtDawn);
                Assert.AreEqual(!pictureUp, run.ClosesAtDawn, pictureUp ? "her pay saves the bar" : "the bar closes");

                // Through the save's door, the way the scene calls it: the dawn that shuts the bar hands no run to be
                // written down, so the game over's CONTINUE has nothing new to find (the scene clears the old save too;
                // the PlayMode suite keeps none, so this is where the save's half is held).
                int written = 0;
                var filed = run.ContinueToNextDay(_ => written++);
                Assert.AreEqual(pictureUp ? TycoonPhase.DayOpen : TycoonPhase.Closed, run.Phase);
                Assert.AreEqual(pictureUp ? 1 : 0, written,
                    pictureUp ? "a dawn the bar lives through is saved" : "the dawn that shuts the bar wrote a save");
                Assert.AreEqual(pictureUp ? QuestRules.MaxReward : 0, filed.QuestPaid, "the dawn paid what it foretold");
                Assert.AreEqual(pictureUp ? 1 : 0, run.QuestsDone, "and a paid job is a job done");
            }
        }

        /// <summary>
        /// HER JOBS DONE RIDE THE SAVE (the Z report's "{WHO}'S JOBS"). Written at the dawn and read back; a save
        /// from before the count (it reads 0) counts the ledger's paid nights instead; and a save from before the book
        /// starts the count with the chain, at nothing.
        /// </summary>
        [Test]
        public void HerJobsDone_RideTheSave()
        {
            var book = Book(Q("hang_one", QuestKind.Fit, reward: 40, slot: "wall_center", level: 1, fixtureId: "pic_1"),
                Q("then", QuestKind.Perfect));
            var run = NewRun("JOBS-DONE", money: 500, book: book, door: false);
            PlayNight(run, Hand.Idle);
            run.ContinueToNextDay();
            PlayNight(run, Hand.Idle);
            run.DevFit("pic_1");
            RunSnapshot snap = null;
            run.ContinueToNextDay(s => snap = s);
            Assert.AreEqual(1, run.QuestsDone, "the picture job was paid at the dawn");
            Assert.AreEqual(1, snap.questsDone, "and written down with it");

            Assert.AreEqual(1, Reborn(snap, book).QuestsDone, "read back");

            snap.questsDone = 0;
            Assert.AreEqual(1, Reborn(snap, book).QuestsDone, "a save from before the count reads the ledger");

            snap.questFormat = 0;
            snap.hasQuests = false;
            Assert.AreEqual(0, Reborn(snap, book).QuestsDone, "a save from before the book starts the count at nothing");
        }
    }
}
