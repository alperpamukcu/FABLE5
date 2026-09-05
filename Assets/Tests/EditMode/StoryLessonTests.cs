using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The host's lessons (GDD 26 §1b "the teacher", §10 `lessons`; PLAN_last_call S5) — the
    /// tutorial as a person. Each cue is a condition the run can observe; the first time it is
    /// true the host has one thing to say, and never again that run. Core decides WHEN; what
    /// is said is data; nothing here waits on anybody reading it.
    ///
    /// Two load-bearing claims: a run WITHOUT a story has no lessons and no new behaviour
    /// (the bench, the sim and every older test build one), and a cue fires ONCE — a lesson
    /// that repeats is a tooltip, which is the thing this replaces.
    /// </summary>
    public class StoryLessonTests
    {
        // ── a bar with something to teach about ──────────────────────────────────

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

        private static IngredientCard Booze(string id, string style, string category = null) =>
            new IngredientCard(id, id, IngredientType.Spirit, 6,
                info: new IngredientInfo(style: style, category: category ?? style));

        /// <summary>Two spirits (so the tin can want a mix), a mixer, and a keg.</summary>
        private static Shelf NewShelf(bool bourbon = false)
        {
            var bottles = new List<ShelfBottle>
            {
                new ShelfBottle(Booze("gin_b", "gin"), capacity: 40),
                new ShelfBottle(Booze("vodka_b", "vodka"), capacity: 40),
                new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
                new ShelfBottle(new IngredientCard("lager", "Lager", IngredientType.Beer, 2), capacity: 40),
            };
            if (bourbon) bottles.Add(new ShelfBottle(Booze("bourbon_b", "bourbon", IngredientCategories.Whiskey), capacity: 40));
            return new Shelf(bottles);
        }

        private static StoryCharacter Host() => new StoryCharacter(
            "ece", look: "ece", name: "Ece Toprak", age: 31, hometown: "Turkey", isHost: true);

        private static StoryCharacter Guest() => new StoryCharacter(
            "collector", look: "execman", name: "Graham Sedgwick", age: 54, hometown: "United Kingdom");

        private static StoryBeat Beat(string id, StoryCharacter who, int week, BarNight night,
            string needStyle = null, int needTier = 0) =>
            new StoryBeat(id, who, new StoryTrial(new[] { Book[0] }, 120, StoryTrial.DefaultMinFill, 1),
                week, night, needStyle: needStyle, needTier: needTier);

        private static StoryLesson Lesson(StoryCue cue) =>
            new StoryLesson(cue.ToString(), cue, new[] { "Line one about " + cue + ".", "Line two." });

        /// <summary>Every cue written, one line each — the shape of the shipped file.</summary>
        private static IReadOnlyList<StoryLesson> AllLessons() =>
            Enum.GetValues(typeof(StoryCue)).Cast<StoryCue>().Select(Lesson).ToList();

        /// <summary>Ece's opening Monday, like the live file, and a Saturday guest the same
        /// week who wants bourbon — the beat the CannotPourTheAsk cue is about.</summary>
        private static StoryArc Arc(IReadOnlyList<StoryLesson> lessons = null, bool guestWantsBourbon = true)
            => new StoryArc(new[]
            {
                new StoryBeat("ece_1", Host(), new StoryTrial(new[] { Book[0] }, 90, StoryTrial.DefaultMinFill, 2),
                    1, BarNight.Monday, nextId: "collector_1"),
                Beat("collector_1", Guest(), 1, BarNight.Saturday,
                    needStyle: guestWantsBourbon ? "bourbon" : null),
            }, lessons ?? AllLessons());

        private static TycoonRun NewRun(StoryArc story, Shelf shelf = null, int money = 500,
            double decide = 0) =>
            new TycoonRun(shelf ?? NewShelf(), Book, new RunRng("lessons"),
                config: new TycoonConfig(money, orderDecisionSeconds: decide, savorSeconds: 0),
                story: story);

        /// <summary>Drains what the host has to say right now, in order.</summary>
        private static List<StoryCue> Heard(TycoonRun run)
        {
            var cues = new List<StoryCue>();
            while (run.LessonDue != null)
            {
                cues.Add(run.LessonDue.Cue);
                run.HeardLesson();
            }
            return cues;
        }

        private static void CloseTheNight(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 400, "day " + run.Day + " never closed");
                run.Tick(5);
            }
        }

        /// <summary>Ticks until somebody is seated, has made up their mind, and is waiting.</summary>
        private static CustomerVisit SomebodyWaiting(TycoonRun run)
        {
            for (int i = 0; i < 400; i++)
            {
                foreach (var v in run.Floor.Seated)
                    if (v.State == VisitState.Waiting && v.HasOrdered) return v;
                run.Tick(1);
            }
            Assert.Fail("nobody ever sat down and ordered");
            return null;
        }

        // ── the run's first door ──────────────────────────────────────────────────

        [Test]
        public void The_first_night_is_the_first_thing_the_host_says()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Assert.IsNotNull(run.LessonDue, "night one opens with a word from the host");
            Assert.AreEqual(StoryCue.FirstNight, run.LessonDue.Cue);
            Assert.AreEqual("Line one about FirstNight.", run.LessonDue.Say[0]);
        }

        [Test]
        public void A_lesson_is_said_once_a_run_and_never_on_the_next_night()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Assert.That(Heard(run), Has.Member(StoryCue.FirstNight));
            CloseTheNight(run);
            Heard(run);                       // the closing's own lessons, drained
            run.ContinueToNextDay();
            Assert.That(Heard(run), Has.No.Member(StoryCue.FirstNight),
                "day two is not the first night");
        }

        [Test]
        public void A_run_without_a_story_has_nothing_to_say_and_hearing_is_harmless()
        {
            var run = NewRun(null);
            Assert.IsNull(run.LessonDue);
            run.HeardLesson();
            CloseTheNight(run);
            Assert.IsNull(run.LessonDue, "no story, no lessons — the bench runs are unchanged");
        }

        [Test]
        public void A_cue_the_writer_left_silent_says_nothing_and_stays_spent()
        {
            // Only the keg is written: the first night comes and goes without a word.
            var run = NewRun(Arc(new[] { Lesson(StoryCue.FirstKeg) }, guestWantsBourbon: false));
            Assert.IsNull(run.LessonDue, "nothing is written for the first night");
            Assert.IsTrue(run.Story.HasTaught(StoryCue.FirstNight), "and the moment is spent all the same");
        }

        // ── the floor's cues ──────────────────────────────────────────────────────

        [Test]
        public void Somebody_waiting_with_no_card_ever_read_is_the_first_licence()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Heard(run);
            SomebodyWaiting(run);
            run.Tick(0);
            Assert.That(Heard(run), Has.Member(StoryCue.FirstLicence));
        }

        [Test]
        public void A_card_read_before_the_lesson_lands_spends_it_silently()
        {
            // A crowd that takes its time deciding, so a card can be read (which takes the
            // order) before anybody is sitting there waiting to be asked.
            var run = NewRun(Arc(guestWantsBourbon: false), decide: 60);
            Heard(run);
            CustomerVisit v = null;
            for (int i = 0; i < 400 && v == null; i++)
            {
                run.Tick(1);
                foreach (var s in run.Floor.Seated) if (!s.HasOrdered) { v = s; break; }
            }
            Assert.IsNotNull(v, "somebody sat down still deciding");
            v.InspectId();                    // the player already knows
            run.Tick(0);
            Assert.That(Heard(run), Has.No.Member(StoryCue.FirstLicence));
            Assert.IsTrue(run.Story.HasTaught(StoryCue.FirstLicence), "spent, not postponed");
        }

        [Test]
        public void Two_spirits_standing_unmixed_in_the_tin_is_a_lesson_once()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Heard(run);
            run.PourMeasure("gin_b", 0.3);
            run.Tick(0);
            Assert.That(Heard(run), Has.No.Member(StoryCue.TwoSpiritsInTheTin), "one spirit is not a mix");
            run.PourMeasure("vodka_b", 0.3);
            Assert.IsTrue(run.MixRequired && !run.IsMixed);
            run.Tick(0);
            Assert.That(Heard(run), Has.Member(StoryCue.TwoSpiritsInTheTin));
            run.Tick(0);
            Assert.That(Heard(run), Has.No.Member(StoryCue.TwoSpiritsInTheTin), "said once");
        }

        [Test]
        public void The_first_pull_on_the_tap_is_the_keg_lesson()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Heard(run);
            run.BeginPull("lager");
            Assert.That(Heard(run), Has.Member(StoryCue.FirstKeg));
            run.EndPull();
            run.BeginPull("lager");
            Assert.That(Heard(run), Has.No.Member(StoryCue.FirstKeg));
        }

        // ── the closing's cues ────────────────────────────────────────────────────

        [Test]
        public void The_first_closing_opens_the_market_and_a_night_under_the_rent_is_red()
        {
            var run = NewRun(Arc(guestWantsBourbon: false));
            Heard(run);
            CloseTheNight(run);               // nobody served: the rent is the whole night
            Assert.Less(run.DayIncome, run.DayExpenses, "an unserved night is a red one");
            var said = Heard(run);
            Assert.That(said, Has.Member(StoryCue.FirstMarket));
            Assert.That(said, Has.Member(StoryCue.RedNight));
            Assert.That(said.IndexOf(StoryCue.FirstMarket), Is.LessThan(said.IndexOf(StoryCue.RedNight)),
                "in the order the moments came");

            run.ContinueToNextDay();
            CloseTheNight(run);
            Assert.That(Heard(run), Has.No.Member(StoryCue.RedNight), "the second red night is not news");
        }

        // ── the shopping week (GDD 26 §4) ─────────────────────────────────────────

        [Test]
        public void A_guest_this_week_wanting_a_style_the_shelf_lacks_is_said_at_the_door()
        {
            var run = NewRun(Arc());          // Saturday's guest wants bourbon; the well has none
            Assert.That(Heard(run), Has.Member(StoryCue.CannotPourTheAsk));
        }

        [Test]
        public void A_shelf_that_has_the_style_hears_no_warning()
        {
            var run = NewRun(Arc(), NewShelf(bourbon: true));
            Assert.That(Heard(run), Has.No.Member(StoryCue.CannotPourTheAsk));
        }

        [Test]
        public void The_warning_waits_for_the_guests_own_week()
        {
            // The guest is written for week 2: night one is not the week to worry.
            var arc = new StoryArc(new[]
            {
                new StoryBeat("ece_1", Host(), new StoryTrial(new[] { Book[0] }, 90, StoryTrial.DefaultMinFill, 2),
                    1, BarNight.Monday, nextId: "collector_1"),
                Beat("collector_1", Guest(), 2, BarNight.Saturday, needStyle: "bourbon"),
            }, AllLessons());
            var run = NewRun(arc);
            Assert.That(Heard(run), Has.No.Member(StoryCue.CannotPourTheAsk));
        }

        // ── the open tab (GDD 26 §5) ──────────────────────────────────────────────

        [Test]
        public void The_ask_is_only_an_open_tab_once_it_has_been_heard()
        {
            var progress = new StoryProgress(Arc());
            Assert.IsFalse(progress.CurrentAsked, "before the first visit there is only the warning");
            progress.RecordMissed(6);
            Assert.IsTrue(progress.CurrentAsked, "a missed night leaves the ask standing");
            progress.RecordServed(12);
            Assert.IsFalse(progress.CurrentAsked, "kept: the tab is paid, the next beat is fresh");
        }
    }
}
