using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The last customer (GDD 26, PLAN_last_call S1) — the beat as Core plays it, before
    /// anybody says a word. One scripted guest comes in after the door is shut, asks for a
    /// written drink, and is served, told no, or left waiting; the arc moves on or comes back.
    ///
    /// The load-bearing claim in here is the one about the END OF THE NIGHT. The day closes on
    /// a single line — the door is shut and the last stool is empty (<see cref="BarDay.IsComplete"/>,
    /// read in exactly one place) — and this whole module works by putting somebody on a stool
    /// after the door has shut. If that ever grows a second condition, these tests are where it
    /// shows up.
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

        private static StoryBeat Beat(string id, int day, string next = null,
            int returnsAfterDays = 2, double patience = 60) =>
            new StoryBeat(id, Guest(), TheAsk, day, patience,
                returnsAfterDays: returnsAfterDays, nextId: next);

        /// <summary>Two written nights: one on day 2, the next on day 3.</summary>
        private static StoryArc TwoNights() =>
            new StoryArc(new[] { Beat("one", day: 2, next: "two"), Beat("two", day: 3) });

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

        // ── the beat's own clock ────────────────────────────────────────────────

        [Test]
        public void Nobody_comes_in_on_a_night_the_story_has_not_written()
        {
            var run = NewRun(TwoNights());   // the first beat is day 2; this is day 1
            PlayToClosing(run);

            Assert.IsNull(run.LastCustomer, "day one is not written; the room should just close");
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "and it closes on the ordinary line");
            Assert.AreEqual("one", run.Story.Current.Id, "the beat is still standing there");
            Assert.AreEqual(2, run.Story.DueDay);
        }

        [Test]
        public void The_written_night_seats_the_guest_after_the_door_is_shut()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            Assert.AreEqual(2, run.Day);

            PlayToClosing(run);

            Assert.IsNotNull(run.LastCustomer, "day two is written — somebody should be on the stool");
            Assert.IsTrue(run.Floor.IsClosingTime, "and only after the door was shut");
            Assert.AreEqual(1, run.Floor.Seated.Count, "alone: the crowd is gone");
            Assert.AreSame(run.LastCustomer, run.Floor.Seated[0]);
            Assert.AreEqual("one", run.LastCallBeat.Id);
            Assert.AreEqual("Graham Sedgwick", run.LastCustomer.Regular.Name);
        }

        [Test]
        public void The_guest_asks_for_the_written_drink_and_hides_it_behind_the_licence()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
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
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
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
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
            var guest = run.LastCustomer;

            guest.InspectId();
            BuildTheAsk(run);
            var verdict = run.ServeTo(guest);

            Assert.AreEqual(OrderMatch.Exact, verdict.Match);
            Assert.AreEqual(1, run.Story.Kept);
            Assert.AreEqual(0, run.Story.Missed);
            Assert.AreEqual("two", run.Story.Current.Id, "the next night is armed");
            Assert.AreEqual(3, run.Story.DueDay, "on its own day, never twice in one closing");

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100, "the night must close once they have gone");
                run.Tick(5);
            }
            Assert.IsNull(run.LastCustomer, "the stool is empty again");
        }

        [Test]
        public void Near_enough_is_not_enough_and_the_beat_comes_back()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
            var guest = run.LastCustomer;

            guest.InspectId();
            BuildTheWrongThing(run);
            var verdict = run.ServeTo(guest);

            Assert.AreNotEqual(OrderMatch.Exact, verdict.Match);
            Assert.AreEqual(0, run.Story.Kept);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.AreEqual("one", run.Story.Current.Id, "the same beat is still owed");
            Assert.AreEqual(4, run.Story.DueDay, "back in two nights, the way the beat says");
        }

        [Test]
        public void An_honest_no_costs_the_night_and_not_the_arc()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);

            var verdict = run.DeclineLastCall();

            Assert.AreEqual(OrderMatch.Declined, verdict.Match);
            Assert.AreEqual(1, run.DeclinedOrders, "it is the ordinary decline, marked the ordinary way");
            Assert.AreEqual("one", run.Story.Current.Id);
            Assert.AreEqual(1, run.Story.Missed);
            Assert.AreEqual(4, run.Story.DueDay);
        }

        [Test]
        public void Saying_no_when_nobody_is_there_is_refused()
        {
            var run = NewRun(TwoNights());
            Assert.Throws<InvalidOperationException>(() => run.DeclineLastCall());
        }

        [Test]
        public void Leaving_them_to_wait_is_an_answer_too()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
            Assert.IsNotNull(run.LastCustomer);

            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 100, "they must give up eventually");
                run.Tick(5);
            }

            Assert.AreEqual(1, run.Story.Missed, "nobody came to the stool; the beat took the night");
            Assert.AreEqual("one", run.Story.Current.Id);
            Assert.AreEqual(4, run.Story.DueDay);
            Assert.IsNull(run.LastCustomer, "and the run is not still holding on to them");
        }

        [Test]
        public void One_last_customer_a_night_even_after_they_have_gone()
        {
            var run = NewRun(TwoNights());
            PlayToClosing(run);
            run.ContinueToNextDay();
            PlayToClosing(run);
            run.DeclineLastCall();

            // The beat is due again the moment it is missed only in the sense that its clock
            // starts; tonight is spent. Ticking the rest of this night must not seat anybody.
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
        public void A_beat_pushed_past_its_day_still_happens()
        {
            // The return clock can land on a night the player closes early, and a beat must
            // never be lost to arithmetic: due means due FROM that night on.
            var arc = new StoryArc(new[] { Beat("one", day: 2, returnsAfterDays: 2) });
            var progress = new StoryProgress(arc);

            Assert.IsFalse(progress.IsDueOn(1));
            Assert.IsTrue(progress.IsDueOn(2));
            progress.RecordMissed(2);
            Assert.AreEqual(4, progress.DueDay);
            Assert.IsFalse(progress.IsDueOn(3));
            Assert.IsTrue(progress.IsDueOn(9), "a beat waited past is still owed");
        }

        [Test]
        public void The_same_person_is_remembered_across_their_nights()
        {
            var arc = TwoNights();
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
            var progress = new StoryProgress(new StoryArc(new[] { Beat("one", day: 2) }));
            progress.RecordServed(2);

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
        public void An_arc_with_no_beats_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryArc(Array.Empty<StoryBeat>()));
        }

        [Test]
        public void A_beat_that_leads_nowhere_real_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() =>
                new StoryArc(new[] { Beat("one", day: 2, next: "the_missing_night") }));
            Assert.That(e.Message, Does.Contain("the_missing_night"));
        }

        [Test]
        public void An_arc_that_goes_in_a_circle_is_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", day: 2, next: "two"),
                Beat("two", day: 3, next: "one"),
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
                Beat("one", day: 2),
                Beat("orphan", day: 3),
            }));
            Assert.That(e.Message, Does.Contain("orphan"));
        }

        [Test]
        public void Two_beats_on_one_night_are_refused()
        {
            var e = Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", day: 2, next: "two"),
                Beat("two", day: 2),
            }));
            Assert.That(e.Message, Does.Contain("one last call a night"));
        }

        [Test]
        public void Two_beats_with_one_name_are_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryArc(new[]
            {
                Beat("one", day: 2, next: "one"),
                Beat("one", day: 3),
            }));
        }

        [Test]
        public void A_beat_that_comes_back_the_same_night_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryBeat("one", Guest(), TheAsk, day: 2, patienceSeconds: 60,
                    returnsAfterDays: 0));
        }

        [Test]
        public void A_beat_with_nobody_in_it_or_nothing_to_pour_is_refused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", null, TheAsk, day: 2, patienceSeconds: 60));
            Assert.Throws<ArgumentNullException>(() =>
                new StoryBeat("one", Guest(), null, day: 2, patienceSeconds: 60));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StoryBeat("one", Guest(), TheAsk, day: 2, patienceSeconds: 0));
        }

        [Test]
        public void A_character_with_no_face_or_no_papers_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "", "Nobody"));
            Assert.Throws<ArgumentException>(() => new StoryCharacter("ghost", "execman", ""));
        }
    }
}
