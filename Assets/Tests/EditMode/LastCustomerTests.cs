using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The last customer (GDD 26, PLAN_last_call S1 + S1b) — the beat as Core plays it, before
    /// anybody says a word. One scripted guest comes in after the door is shut, on the night of
    /// the week they are written for, asks for a written drink, and is served, told no, or left
    /// waiting; the arc moves on or comes back.
    ///
    /// Two load-bearing claims live in here. The first is about the END OF THE NIGHT: the day
    /// closes on a single line — the door is shut and the last stool is empty
    /// (<see cref="BarDay.IsComplete"/>, read in exactly one place) — and this whole module
    /// works by putting somebody on a stool after the door has shut. The second is about the
    /// CALENDAR: a guest comes at the weekend, so a missed beat has to come back on a night it
    /// can actually happen on. A beat pushed onto a Wednesday is a beat that never happens
    /// again, and the arc would stall there for the rest of the run without ever failing.
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

        /// <summary>The beat's drink is the book's OWN object — a story that asks for a
        /// recipe the bar does not have in its book is a beat nobody can ever pour.</summary>
        private static RecipeDefinition TheAsk => Book[0];

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
        });

        private static StoryCharacter Guest(string id = "collector") => new StoryCharacter(
            id, look: "execman", name: "Graham Sedgwick", age: 54,
            hometown: "United Kingdom", blurb: "Collects for the building.");

        private static StoryCharacter Host() => new StoryCharacter(
            "ece", look: "ece", name: "Ece Toprak", age: 31,
            hometown: "Turkey", isHost: true);

        private static StoryBeat Beat(string id, int week, BarNight night, string next = null,
            int returnsAfterWeeks = 1, double patience = 60, StoryCharacter who = null) =>
            new StoryBeat(id, who ?? Guest(), TheAsk, week, night, patience,
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

        /// <summary>An exact Spritz, built and poured with the two verbs the player has.</summary>
        private static void BuildTheAsk(TycoonRun run)
        {
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
        }

        /// <summary>A glass of plain soda: a drink, and not the one that was asked for.</summary>
        private static void BuildTheWrongThing(TycoonRun run)
        {
            run.PourMeasure("soda", 0.7);
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
            // The words moved from TycoonHud into Core; a screen the player has been reading
            // for weeks must not change because a rule was put behind it.
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
        public void Not_on_the_wednesday_or_the_thursday_either()
        {
            var run = NewRun(OneWeekend());
            for (int day = 1; day <= 3; day++)
            {
                PlayToClosing(run);
                Assert.IsNull(run.LastCustomer,
                    $"{BarCalendar.Label(run.Day)} is not a night anybody important comes in on");
                run.ContinueToNextDay();
            }
            Assert.AreEqual(4, run.Day, "and now it is Friday");
        }

        [Test]
        public void The_weekend_seats_the_guest_after_the_door_is_shut()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);

            Assert.AreEqual(BarNight.Friday, run.Tonight);
            Assert.IsTrue(run.IsWeekend);
            Assert.IsNotNull(run.LastCustomer, "the Friday is written — somebody should be on the stool");
            Assert.IsTrue(run.Floor.IsClosingTime, "and only after the door was shut");
            Assert.AreEqual(1, run.Floor.Seated.Count, "alone: the crowd is gone");
            Assert.AreSame(run.LastCustomer, run.Floor.Seated[0]);
            Assert.AreEqual("one", run.LastCallBeat.Id);
            Assert.AreEqual("Graham Sedgwick", run.LastCustomer.Regular.Name);
        }

        [Test]
        public void The_guest_asks_for_the_written_drink_and_hides_it_behind_the_licence()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            // The scripted customer is a customer: what they came for lives behind the card,
            // exactly like everybody else's (the house rule, twice broken, once written down).
            Assert.That(() => guest.Order, Throws.Exception,
                "the ask must be behind the licence for the last customer too");
            guest.InspectId();
            Assert.AreEqual("spritz", guest.Order.Wanted.Id, "the drink is the beat's, not a roll");
            Assert.Greater(guest.Order.Price, 0, "and it is priced like anything else on the menu");
            Assert.AreEqual(60, guest.PatienceMax, "the wait is written down, not rolled");
        }

        [Test]
        public void The_night_cannot_end_while_the_guest_is_on_the_stool()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
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

        // ── how the night can go ────────────────────────────────────────────────

        [Test]
        public void What_they_asked_for_moves_the_story_on()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            guest.InspectId();
            BuildTheAsk(run);
            var verdict = run.ServeTo(guest);

            Assert.AreEqual(OrderMatch.Exact, verdict.Match);
            Assert.AreEqual(1, run.Story.Kept);
            Assert.AreEqual(0, run.Story.Missed);
            Assert.AreEqual("two", run.Story.Current.Id, "the next night is armed");
            Assert.AreEqual(5, run.Story.DueDay, "the Saturday — never twice in one closing");

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100, "the night must close once they have gone");
                run.Tick(5);
            }
            Assert.IsNull(run.LastCustomer, "the stool is empty again");
        }

        [Test]
        public void A_weekend_can_hold_a_beat_on_each_of_its_nights()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var friday = run.LastCustomer;
            friday.InspectId();
            BuildTheAsk(run);
            run.ServeTo(friday);

            PlayUntilDay(run, 5);
            Assert.AreEqual(BarNight.Saturday, run.Tonight);
            Assert.IsNotNull(run.LastCustomer, "the Saturday beat is written too");
            Assert.AreEqual("two", run.LastCallBeat.Id);
        }

        [Test]
        public void Near_enough_is_not_enough_and_the_beat_comes_back_next_week()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            var guest = run.LastCustomer;

            guest.InspectId();
            BuildTheWrongThing(run);
            var verdict = run.ServeTo(guest);

            Assert.AreNotEqual(OrderMatch.Exact, verdict.Match);
            Assert.AreEqual(0, run.Story.Kept);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.AreEqual("one", run.Story.Current.Id, "the same beat is still owed");
            Assert.AreEqual(10, run.Story.DueDay, "next Friday, a week later");
        }

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
            Assert.IsTrue(BarCalendar.IsWeekend(progress.DueDay));
            Assert.IsTrue(progress.IsDueOn(progress.DueDay), "and the gate opens on it");

            progress.RecordMissed(progress.DueDay);
            Assert.AreEqual(16, progress.DueDay, "and again the week after");
        }

        [Test]
        public void An_honest_no_costs_the_night_and_not_the_arc()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);

            var verdict = run.DeclineLastCall();

            Assert.AreEqual(OrderMatch.Declined, verdict.Match);
            Assert.AreEqual(1, run.DeclinedOrders, "it is the ordinary decline, marked the ordinary way");
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
        public void Leaving_them_to_wait_is_an_answer_too()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            Assert.IsNotNull(run.LastCustomer);

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100, "they must give up eventually");
                run.Tick(5);
            }

            Assert.AreEqual(1, run.Story.Missed, "nobody came to the stool; the beat took the night");
            Assert.AreEqual("one", run.Story.Current.Id);
            Assert.AreEqual(10, run.Story.DueDay);
            Assert.IsNull(run.LastCustomer, "and the run is not still holding on to them");
        }

        [Test]
        public void One_last_customer_a_night_even_after_they_have_gone()
        {
            var run = NewRun(OneWeekend());
            PlayUntilDay(run, 4);
            run.DeclineLastCall();

            // Tonight is spent whichever way it went. Ticking out the rest of this night must
            // not seat anybody, even though the beat is standing again.
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100);
                run.Tick(5);
                Assert.IsNull(run.LastCustomer, "the last call already happened tonight");
            }
            Assert.AreEqual(1, run.Story.Missed);
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
            // The weekend rule is the design (GDD 26 §2b), so it is enforced where the beat is
            // built rather than trusted to whoever edits the file.
            var e = Assert.Throws<ArgumentException>(() =>
                Beat("one", week: 1, night: BarNight.Wednesday));
            Assert.That(e.Message, Does.Contain("WEDNESDAY"));
            Assert.That(e.Message, Does.Contain("weekend"));
        }

        [Test]
        public void The_house_can_work_a_quiet_night()
        {
            // Ece is not a guest: she is already behind the bar, and beat zero is hers on the
            // opening Tuesday, when nothing is at stake and the beat teaches itself.
            var beat = Beat("ece_1", week: 1, night: BarNight.Tuesday, who: Host());
            Assert.AreEqual(1, beat.Day);
            Assert.AreEqual(BarNight.Tuesday, beat.Night);
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
            // Written, paid for in somebody's afternoon, and unreachable — the failure this
            // module is most likely to actually have.
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
        public void An_arc_that_runs_backwards_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", week: 2, night: BarNight.Friday, next: "two"),
                Beat("two", week: 1, night: BarNight.Saturday),
            }));
            Assert.That(e.Message, Does.Contain("follows"));
        }

        [Test]
        public void Two_beats_with_one_name_are_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", week: 1, night: BarNight.Friday, next: "one"),
                Beat("one", week: 1, night: BarNight.Saturday),
            }));
        }

        [Test]
        public void A_beat_that_comes_back_the_same_night_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryBeat("one", Guest(), TheAsk, week: 1, night: BarNight.Friday,
                    patienceSeconds: 60, returnsAfterWeeks: 0));
        }

        [Test]
        public void A_beat_with_nobody_in_it_or_nothing_to_pour_is_refused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", null, TheAsk, 1, BarNight.Friday, 60));
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", Guest(), null, 1, BarNight.Friday, 60));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryBeat("one", Guest(), TheAsk, 1, BarNight.Friday, 0));
        }

        [Test]
        public void A_character_with_no_face_or_no_papers_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "", "Nobody"));
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "execman", ""));
        }
    }
}
