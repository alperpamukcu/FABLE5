using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The last customer (GDD 26, PLAN_last_call S1/S1b/S1c) — the beat as Core plays it,
    /// before anybody says a word. One scripted guest comes in after the door is shut, on the
    /// night of the week they are written for, and runs the bar through a TRIAL: several
    /// drinks, one clock, a standard nothing else in this game asks for. They are a guest of
    /// the house — no licence, no bill, no rating — and the arc moves on or comes back.
    ///
    /// Three load-bearing claims live in here. The END OF THE NIGHT: the day closes on a
    /// single line — the door is shut and the last stool is empty — and this module works by
    /// putting somebody on a stool after the door has shut. The CALENDAR: a guest comes at
    /// the weekend, and a missed beat must return on a night it can actually happen on. And
    /// the BOOKS: nothing the guest does reaches the till, the standing or the slip, because
    /// a trial that moves the rating is a trial the player can farm — or be robbed by.
    /// </summary>
    public class LastCustomerTests
    {
        // ── the bar these nights are played in ──────────────────────────────────

        private static RecipeDefinition Spritz() => new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5, prep: PrepMethod.Built);

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[] { Spritz() };

        /// <summary>The trial's drink is the book's OWN object — a story that asks for a
        /// recipe the bar does not have in its book is a night nobody can ever pour.</summary>
        private static RecipeDefinition TheAsk => Book[0];

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 40),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
        });

        private static StoryCharacter Guest(string id = "gourmet") => new StoryCharacter(
            id, look: "profess", name: "Ulrich Brenner", age: 66,
            hometown: "Germany", blurb: "Writes about rooms like this one.");

        private static StoryCharacter Host() => new StoryCharacter(
            "ece", look: "ece", name: "Ece Toprak", age: 31,
            hometown: "Turkey", isHost: true);

        /// <summary>Three of the same drink in plenty of time, one mistake allowed unless the
        /// test says otherwise — the shape of the mechanic without the sting of the content.</summary>
        private static StoryTrial Drinks(int count = 3, double seconds = 240,
            int mistakes = 1, double minFill = StoryTrial.DefaultMinFill) =>
            new StoryTrial(Enumerable.Repeat(TheAsk, count).ToList(), seconds, minFill, mistakes);

        private static StoryBeat Beat(string id, int week, BarNight night, string next = null,
            int returnsAfterWeeks = 1, StoryTrial trial = null, StoryCharacter who = null) =>
            new StoryBeat(id, who ?? Guest(), trial ?? Drinks(), week, night,
                returnsAfterWeeks: returnsAfterWeeks, nextId: next);

        /// <summary>One weekend, both of its nights: Friday is day 4, Saturday is day 5.</summary>
        private static StoryArc OneWeekend() => new StoryArc(new[]
        {
            Beat("one", week: 1, night: BarNight.Friday, next: "two"),
            Beat("two", week: 1, night: BarNight.Saturday),
        });

        // Money enough that the rent cannot end these runs before the story does, and no
        // decision beat or savour: this suite is about the arc's clock, not the floor's.
        private static TycoonRun NewRun(StoryArc story, string seed = "last-call") =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                story: story);

        /// <summary>Plays the night out without serving a soul: the crowd storms off, the door
        /// shuts, and the room empties — which is the moment the last call can happen.</summary>
        private static void PlayToClosing(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen && run.LastCustomer == null)
            {
                Assert.Less(guard++, 400, "the night must reach its closing one way or another");
                run.Tick(5);
            }
        }

        /// <summary>Ticks the night all the way out, whoever is still on a stool.</summary>
        private static void CloseTheNight(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 400, $"day {run.Day} never closed");
                run.Tick(5);
            }
        }

        /// <summary>Plays whole nights until the bar is open on the day asked for, then runs
        /// that night down to its closing. The nights in between are ordinary and empty.</summary>
        private static void PlayUntilDay(TycoonRun run, int day)
        {
            int guard = 0;
            while (run.Day < day)
            {
                Assert.Less(guard++, 40, $"the run never reached day {day}");
                CloseTheNight(run);
                run.ContinueToNextDay();
                Assert.AreEqual(TycoonPhase.DayOpen, run.Phase,
                    $"the bar did not survive to day {day}");
            }
            PlayToClosing(run);
        }

        /// <summary>To the stool, talked out, clock running: where every trial test starts.</summary>
        private static void SitAndBegin(TycoonRun run)
        {
            PlayUntilDay(run, 4);
            Assert.IsNotNull(run.LastCustomer, "the Friday is written — somebody should be here");
            run.BeginLastCallTrial();
        }

        /// <summary>An exact Spritz to the trial's own standard: right ratio, full glass.</summary>
        private static void BuildPerfect(TycoonRun run)
        {
            run.PourMeasure("gin", 0.5);
            run.PourMeasure("soda", 0.5);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        /// <summary>The right drink poured SHORT — exact match, honest craft, thin glass. The
        /// crowd would tip less and forgive it; the trial does not.</summary>
        private static void BuildShort(TycoonRun run)
        {
            run.PourMeasure("gin", 0.3);
            run.PourMeasure("soda", 0.3);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        /// <summary>A glass of plain soda: a drink, and not the one that was asked for.</summary>
        private static void BuildWrong(TycoonRun run)
        {
            run.PourMeasure("soda", 0.95);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        // ── the calendar the story is written on ────────────────────────────────

        [TestCase(1, BarNight.Tuesday, 1)]
        [TestCase(3, BarNight.Thursday, 1)]
        [TestCase(4, BarNight.Friday, 1)]
        [TestCase(5, BarNight.Saturday, 1)]
        [TestCase(6, BarNight.Sunday, 1)]
        [TestCase(7, BarNight.Tuesday, 2)]
        [TestCase(10, BarNight.Friday, 2)]
        [TestCase(17, BarNight.Saturday, 3)]
        public void The_week_is_six_nights_and_mondays_are_dark(int day, BarNight night, int week)
        {
            Assert.AreEqual(night, BarCalendar.NightOf(day));
            Assert.AreEqual(week, BarCalendar.WeekOf(day));
            Assert.AreEqual(day, BarCalendar.DayOf(week, night), "the calendar must invert");
        }

        [Test]
        public void The_plaque_still_reads_what_it_always_read()
        {
            Assert.AreEqual("WEEK 1 · TUESDAY", BarCalendar.Label(1));
            Assert.AreEqual("WEEK 2 · FRIDAY", BarCalendar.Label(10));
        }

        [Test]
        public void Only_friday_and_saturday_are_the_weekend()
        {
            Assert.IsTrue(BarCalendar.IsWeekend(4), "day 4 is a Friday");
            Assert.IsTrue(BarCalendar.IsWeekend(5), "day 5 is a Saturday");
            foreach (int day in new[] { 1, 2, 3, 6 })
                Assert.IsFalse(BarCalendar.IsWeekend(day), $"day {day} is a quiet night");
        }

        [Test]
        public void The_next_friday_is_found_from_any_day_of_the_week()
        {
            Assert.AreEqual(4, BarCalendar.NextNightOnOrAfter(1, BarNight.Friday));
            Assert.AreEqual(4, BarCalendar.NextNightOnOrAfter(4, BarNight.Friday), "today counts");
            Assert.AreEqual(10, BarCalendar.NextNightOnOrAfter(5, BarNight.Friday), "next week's");
            Assert.AreEqual(11, BarCalendar.NextNightOnOrAfter(6, BarNight.Saturday));
        }

        // ── the beat's own clock ────────────────────────────────────────────────

        [Test]
        public void Nobody_comes_in_on_a_quiet_night()
        {
            var run = NewRun(OneWeekend());   // the first beat is the Friday; this is Tuesday
            PlayToClosing(run);

            Assert.IsNull(run.LastCustomer, "a guest does not turn up on a Tuesday");
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "and the room closes on the ordinary line");
            Assert.AreEqual("one", run.Story.Current.Id, "the beat is still standing there");
            Assert.AreEqual(4, run.Story.DueDay, "waiting for the weekend");
        }

        [Test]
        public void The_weekend_seats_the_guest_after_the_door_is_shut()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);

            Assert.AreEqual(BarNight.Friday, run.Tonight);
            Assert.IsNotNull(run.LastCustomer);
            Assert.IsTrue(run.Floor.IsClosingTime, "and only after the door was shut");
            Assert.AreEqual(1, run.Floor.Seated.Count, "alone: the crowd is gone");
            Assert.AreSame(run.LastCustomer, run.Floor.Seated[0]);
            Assert.AreEqual("one", run.LastCallBeat.Id);
            Assert.AreEqual("Ulrich Brenner", run.LastCustomer.Regular.Name);
        }

        [Test]
        public void The_night_cannot_end_while_the_guest_is_on_the_stool()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            for (int i = 0; i < 4; i++)
            {
                run.Tick(5);
                Assert.AreEqual(TycoonPhase.DayOpen, run.Phase,
                    "the door is shut and the till is waiting, but somebody is still sitting there");
                Assert.IsFalse(run.Floor.IsComplete);
                Assert.IsTrue(run.Floor.Seated.Contains(guest));
            }
        }

        // ── a guest of the house, not a customer ────────────────────────────────

        [Test]
        public void The_guest_needs_no_licence_and_their_ask_is_readable_at_once()
        {
            // The hidden-information rule stands for the CROWD; the story's guest said who
            // they were on the way in, and the ask lives in the dialogue (GDD 26 §3 — the
            // written exception, not an erosion of the rule).
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            Assert.IsTrue(guest.IdInspected, "no card to read: they introduced themselves");
            Assert.That(() => guest.Order, Throws.Nothing);
            Assert.AreEqual("spritz", guest.Order.Wanted.Id);
            Assert.IsTrue(guest.OnTheHouse);
        }

        [Test]
        public void Nothing_ticks_while_they_are_talking()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            double before = guest.PatienceLeft;
            run.Tick(10);
            Assert.AreEqual(before, guest.PatienceLeft,
                "the clock must hold for the dialogue — the trial starts it, not the stool");
            Assert.AreEqual(TrialState.Talking, run.Trial.State);
        }

        [Test]
        public void The_talking_cannot_hold_the_night_hostage()
        {
            // A plate nobody dismisses — a UI bug, a headless run without the verb — must not
            // hold a clock that never starts. Long past any real dialogue, the trial starts
            // itself, the clock runs down, and the night closes.
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            Assert.AreEqual(TrialState.Talking, run.Trial.State);

            CloseTheNight(run);   // never calls BeginLastCallTrial

            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "the backstop let the night end");
            Assert.AreEqual(1, run.Story.Missed);
        }

        [Test]
        public void The_guest_pays_nothing_and_moves_no_ledger()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;
            int moneyBefore = run.Money;
            int salesBefore = run.DaySales;

            for (int i = 0; i < 3; i++) { BuildPerfect(run); run.ServeTo(guest); }
            Assert.AreEqual(TrialState.Passed, run.Trial.State);

            // The tick that takes the guest off the stool is the same tick that closes the
            // day and lands the rent — there is no moment between to measure. So the rent is
            // accounted for by name: what the till lost must be the LANDLORD'S line, exactly.
            CloseTheNight(run);
            Assert.AreEqual(moneyBefore - run.DayRent, run.Money,
                "a guest of the house pays nothing — only the rent moved the till");
            Assert.AreEqual(salesBefore, run.DaySales, "and books no sale");
            Assert.AreEqual(0, guest.Paid);
            Assert.AreEqual(0, run.DayTips, "and tips nothing");
        }

        [Test]
        public void The_guest_files_no_rating_good_or_bad()
        {
            // BOTH directions, because both are exploits: a passed trial must not lift a
            // dreadful night, and a failed one must not stain a good bar's standing.
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;
            for (int i = 0; i < 3; i++) { BuildPerfect(run); run.ServeTo(guest); }

            CloseTheNight(run);
            Assert.AreEqual(0.0, run.Floor.AverageSatisfaction,
                "an empty night with a perfect trial is still an empty night");
            var counted = run.Floor.FinishedCounted();
            Assert.IsFalse(counted.Any(v => v.OnTheHouse), "the slip never sees the guest");
        }

        // ── the trial itself ────────────────────────────────────────────────────

        [Test]
        public void The_asks_come_one_at_a_time()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            Assert.AreEqual(0, run.Trial.Done, "nothing landed yet");
            Assert.AreSame(TheAsk, run.Trial.Current, "the first ask is on the wall");

            BuildPerfect(run);
            run.ServeTo(guest);
            Assert.AreEqual(1, run.Trial.Done, "one down");
            Assert.AreSame(TheAsk, run.Trial.Current, "and the next is revealed by serving");
        }

        [Test]
        public void Three_perfect_drinks_pass_the_trial_and_move_the_story_on()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            for (int i = 0; i < 3; i++) { BuildPerfect(run); run.ServeTo(guest); }

            Assert.AreEqual(TrialState.Passed, run.Trial.State);
            Assert.AreEqual(1, run.Story.Kept);
            Assert.AreEqual("two", run.Story.Current.Id, "the next night is armed");
            CloseTheNight(run);
            Assert.IsNull(run.LastCustomer, "and they got up owing nothing");
        }

        [Test]
        public void A_short_pour_is_a_mistake_even_when_the_drink_is_right()
        {
            // The trial's one soft edge is the fill, and 0.90 is where soft ends: an exact,
            // honestly-made Spritz in half a glass is a fumble to an inspector.
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            BuildShort(run);
            var verdict = run.ServeTo(guest);

            Assert.AreEqual(OrderMatch.Exact, verdict.Match, "the drink itself was right");
            Assert.AreEqual(0, run.Trial.Done);
            Assert.AreEqual(1, run.Trial.Mistakes);
            Assert.AreSame(TheAsk, run.Trial.Current, "the ask stands — they still want it");
        }

        [Test]
        public void A_brimful_glass_is_not_asked_for()
        {
            // ≥ 0.90 is enough; the standard is strict, not cruel.
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            run.PourMeasure("gin", 0.46);
            run.PourMeasure("soda", 0.46);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            run.ServeTo(guest);

            Assert.AreEqual(1, run.Trial.Done, "0.92 of a glass is a poured glass");
        }

        [Test]
        public void Mistakes_over_the_allowance_end_the_night()
        {
            var run = NewRun(OneWeekend());   // the default trial allows one
            SitAndBegin(run);
            var guest = run.LastCustomer;

            BuildWrong(run); run.ServeTo(guest);
            Assert.AreEqual(TrialState.Pouring, run.Trial.State, "one mistake is allowed");
            BuildWrong(run); run.ServeTo(guest);

            Assert.AreEqual(TrialState.Failed, run.Trial.State);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.AreEqual("one", run.Story.Current.Id, "the beat is still owed");
            Assert.AreEqual(10, run.Story.DueDay, "next Friday");
            CloseTheNight(run);
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
        }

        [Test]
        public void The_clock_runs_out_and_takes_the_night_with_it()
        {
            var run = NewRun(new StoryArc(new[]
                { Beat("one", week: 1, night: BarNight.Friday,
                       trial: Drinks(count: 3, seconds: 30)) }));
            SitAndBegin(run);
            var guest = run.LastCustomer;

            BuildPerfect(run); run.ServeTo(guest);   // one lands...
            CloseTheNight(run);                       // ...and the rest never do

            Assert.AreEqual(TrialState.Failed, run.Trial?.State ?? TrialState.Failed);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.IsNull(run.LastCustomer);
        }

        [Test]
        public void One_clock_for_the_whole_trial_not_one_per_drink()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            var guest = run.LastCustomer;

            run.Tick(5);
            double afterFirst = guest.PatienceLeft;
            BuildPerfect(run);
            run.ServeTo(guest);

            Assert.LessOrEqual(guest.PatienceLeft, afterFirst,
                "landing a drink must not refresh the clock — a trial is not an extra round");
        }

        [Test]
        public void Serving_before_the_talk_is_over_is_refused()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            BuildPerfect(run);
            Assert.Throws<InvalidOperationException>(() => run.ServeTo(guest),
                "nothing has been asked for while they are still talking");
        }

        [Test]
        public void An_honest_no_costs_the_night_and_not_the_arc()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            int declinedBefore = run.DeclinedOrders;

            var verdict = run.DeclineLastCall();

            Assert.AreEqual(OrderMatch.Declined, verdict.Match);
            Assert.AreEqual(declinedBefore, run.DeclinedOrders,
                "a guest of the house is not a declined ORDER — the night's books stay clean");
            Assert.AreEqual(TrialState.Failed, run.Trial.State);
            Assert.AreEqual("one", run.Story.Current.Id);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.AreEqual(10, run.Story.DueDay);
        }

        [Test]
        public void Saying_no_when_nobody_is_there_is_refused()
        {
            var run = NewRun(OneWeekend());
            Assert.Throws<InvalidOperationException>(() => run.DeclineLastCall());
        }

        [Test]
        public void One_last_customer_a_night_even_after_they_have_gone()
        {
            var run = NewRun(OneWeekend());
            SitAndBegin(run);
            run.DeclineLastCall();

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100);
                run.Tick(5);
                Assert.IsNull(run.LastCustomer, "the last call already happened tonight");
            }
            Assert.AreEqual(1, run.Story.Missed);
        }

        // ── the return clock (the silent failure) ───────────────────────────────

        [Test]
        public void A_missed_beat_lands_on_a_night_it_can_actually_happen_on()
        {
            // THE ONE THAT WOULD ROT SILENTLY. "Today plus two" is a perfectly reasonable
            // return clock that puts a Friday guest on a Sunday, where the gate can never open
            // — no exception, no failed test, just a story that stops.
            var arc = new StoryArc(new[] { Beat("one", week: 1, night: BarNight.Friday) });
            var progress = new StoryProgress(arc);

            progress.RecordMissed(4);
            Assert.AreEqual(BarNight.Friday, BarCalendar.NightOf(progress.DueDay));
            Assert.IsTrue(progress.IsDueOn(progress.DueDay), "and the gate opens on it");

            progress.RecordMissed(progress.DueDay);
            Assert.AreEqual(16, progress.DueDay, "and again the week after");
        }

        [Test]
        public void A_beat_waited_past_is_still_owed_but_only_on_its_own_night()
        {
            var arc = new StoryArc(new[] { Beat("one", week: 1, night: BarNight.Friday) });
            var progress = new StoryProgress(arc);

            Assert.IsFalse(progress.IsDueOn(1), "a Tuesday is not their night");
            Assert.IsTrue(progress.IsDueOn(4));
            progress.RecordMissed(4);
            Assert.AreEqual(10, progress.DueDay);
            Assert.IsFalse(progress.IsDueOn(11), "a Saturday is not their night either");
            Assert.IsTrue(progress.IsDueOn(16), "a beat waited past is still owed");
        }

        [Test]
        public void The_same_person_is_remembered_across_their_nights()
        {
            var arc = OneWeekend();
            var progress = new StoryProgress(arc);
            var who = arc.Beats[0].Who;

            var first = progress.PersonFor(who);
            first.RecordVisit(2);
            var second = progress.PersonFor(who);

            Assert.AreSame(first, second, "a story guest who comes back is somebody the bar has met");
            Assert.AreEqual(1, second.Visits);
            Assert.AreEqual("story", second.ArchetypeId, "the archetype says how they came in");
        }

        [Test]
        public void An_arc_that_is_walked_through_is_finished_and_stays_quiet()
        {
            var progress = new StoryProgress(new StoryArc(new[]
                { Beat("one", week: 1, night: BarNight.Friday) }));
            progress.RecordServed(4);

            Assert.IsTrue(progress.IsFinished);
            Assert.IsNull(progress.Current);
            Assert.IsFalse(progress.IsDueOn(99), "there is nothing left to be due");
            Assert.DoesNotThrow(() => progress.RecordMissed(99));
        }

        // ── a bar with no story is the bar that shipped ─────────────────────────

        [Test]
        public void A_run_without_a_story_never_hears_a_last_call()
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng("no-story"),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0));

            Assert.IsNull(run.Story, "the story is opt-in, exactly like the regulars");

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 400, "the night must close");
                run.Tick(5);
                Assert.IsNull(run.LastCustomer);
            }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            Assert.IsTrue(run.Floor.IsComplete);
        }

        // ── the loud failures (a written arc is content, and content is checked) ──

        [Test]
        public void A_guest_written_for_a_wednesday_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() =>
                Beat("one", week: 1, night: BarNight.Wednesday));
            Assert.That(e.Message, Does.Contain("WEDNESDAY"));
            Assert.That(e.Message, Does.Contain("weekend"));
        }

        [Test]
        public void The_house_can_work_a_quiet_night()
        {
            var beat = Beat("ece_1", week: 1, night: BarNight.Tuesday, who: Host());
            Assert.AreEqual(1, beat.Day);
            Assert.AreEqual(BarNight.Tuesday, beat.Night);
        }

        [Test]
        public void A_trial_with_nothing_in_it_is_refused()
        {
            Assert.Throws<ArgumentException>(() =>
                new StoryTrial(Array.Empty<RecipeDefinition>(), 60));
            Assert.Throws<ArgumentException>(() =>
                new StoryTrial(new RecipeDefinition[] { null }, 60));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryTrial(new[] { TheAsk }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryTrial(new[] { TheAsk }, 60, minFill: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryTrial(new[] { TheAsk }, 60, allowedMistakes: -1));
        }

        [Test]
        public void A_trial_run_refuses_to_move_before_or_after_its_time()
        {
            var trial = new StoryTrialRun(Drinks(count: 1));
            Assert.Throws<InvalidOperationException>(() => trial.Landed(),
                "nothing lands while they are talking");
            trial.Begin();
            Assert.Throws<InvalidOperationException>(() => trial.Begin(),
                "a trial starts once");
            trial.Landed();
            Assert.AreEqual(TrialState.Passed, trial.State);
            Assert.Throws<InvalidOperationException>(() => trial.Landed(),
                "a finished trial takes no more drinks");
        }

        [Test]
        public void An_arc_with_no_beats_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryArc(Array.Empty<StoryBeat>()));
        }

        [Test]
        public void A_beat_that_leads_nowhere_real_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
                { Beat("one", week: 1, night: BarNight.Friday, next: "the_missing_night") }));
            Assert.That(e.Message, Does.Contain("the_missing_night"));
        }

        [Test]
        public void An_arc_that_goes_in_a_circle_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", week: 1, night: BarNight.Friday, next: "two"),
                Beat("two", week: 1, night: BarNight.Saturday, next: "one"),
            }));
            Assert.That(e.Message, Does.Contain("circle").IgnoreCase);
        }

        [Test]
        public void A_beat_nothing_leads_to_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", week: 1, night: BarNight.Friday),
                Beat("orphan", week: 1, night: BarNight.Saturday),
            }));
            Assert.That(e.Message, Does.Contain("orphan"));
        }

        [Test]
        public void Two_beats_on_one_night_are_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", week: 1, night: BarNight.Friday, next: "two"),
                Beat("two", week: 1, night: BarNight.Friday),
            }));
            Assert.That(e.Message, Does.Contain("one last call a night"));
        }

        [Test]
        public void A_beat_that_comes_back_the_same_night_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryBeat("one", Guest(), Drinks(), week: 1, night: BarNight.Friday,
                    returnsAfterWeeks: 0));
        }

        [Test]
        public void A_beat_with_nobody_in_it_or_nothing_to_pour_is_refused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", null, Drinks(), 1, BarNight.Friday));
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", Guest(), null, 1, BarNight.Friday));
        }

        [Test]
        public void A_character_with_no_face_or_no_papers_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "", "Nobody"));
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "execman", ""));
        }
    }
}
