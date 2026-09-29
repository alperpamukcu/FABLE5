using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// THE HOUSE TOUR (2026-09-28, TycoonRun.Tour) — played through the verbs the player uses, with the screen's few
    /// reports made by hand (TourSaw): the door is held for as long as a step is up (no clock, nobody in, no patience,
    /// no marks ageing) while the one guest still walks in, decides, drinks and leaves; the steps move on when their
    /// waits come true, and catch a player up who has run ahead; skipping opens the night where it stands; the first
    /// job is handed over as the tour ends. The shipped tour is played the whole way through on the shipped content.
    /// </summary>
    public sealed class TourTests
    {
        private static readonly IngredientCard Gin =
            new IngredientCard("gin", "Gin", IngredientType.Spirit, 6, new IngredientInfo("gin"));
        private static readonly IngredientCard Soda =
            new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1, new IngredientInfo("soda", carbonated: true));

        private static readonly IReadOnlyList<RecipeDefinition> Menu = new[]
        {
            new RecipeDefinition("gin_soda", "Gin & Soda", rank: 2, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement("gin", 0.3, 0.5),
                    new RatioRequirement("soda", 0.5, 0.7),
                },
                minFill: 0.5),
        };

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(Gin.Clone(), capacity: 4000),
            new ShelfBottle(Soda.Clone(), capacity: 4000),
        });

        private static TourStep S(string id, TourWait wait, string point = "") =>
            new TourStep(id, new[] { id + " line" }, wait, point);

        /// <summary>The shipped tour's shape, one line a step.</summary>
        private static TourScript Script() => new TourScript(new[]
        {
            S("welcome", TourWait.Heard),
            S("guest", TourWait.GuestReady, "stool"),
            S("stool", TourWait.CardRead, "stool"),
            S("card_away", TourWait.CardPutAway),
            S("cellar", TourWait.CellarOpen, "cellar"),
            S("bottle", TourWait.BottleInHand, "bottle"),
            S("pour", TourWait.FirstPour, "bench_bottle"),
            S("measure", TourWait.Heard, "tin_gauge"),
            S("mixer", TourWait.TinBuilt, "bottle"),
            S("cap", TourWait.Capped, "lid"),
            S("tip", TourWait.InTheGlass, "shaker"),
            S("serve_key", TourWait.OnTheCounter, "serve_key"),
            S("hand_over", TourWait.Served, "counter_glass"),
            S("finish", TourWait.GuestGone, "guest"),
            S("glass", TourWait.GlassCollected, "dirty_glass"),
            S("wipe", TourWait.CounterWiped, "cloth"),
            S("book", TourWait.BookOpen, "book"),
            S("book_shut", TourWait.BookShut),
            S("doors", TourWait.Heard),
        });

        private static QuestBook Book() => new QuestBook(new[]
            {
                new QuestDefinition("first_wage", QuestKind.Serve, 0, 3, 12, "FIRST WAGE",
                    new[] { "{name} has a job for you." }, new[] { "Thank you." }, QuestPick.Any, null, 0, 0, null, 0, null),
            },
            new[] { "That is the whole book." }, "The next one waits till we're {rung}.");

        /// <summary>The scene's own switches (counter marks on, a real sip and a real think); only the purse is the
        /// bench's.</summary>
        private static TycoonRun NewRun(string seed, TourScript tour, QuestBook book = null) =>
            new TycoonRun(NewShelf(), Menu, new RunRng(seed), config: new TycoonConfig(startingMoney: 5000, savorSeconds: 6.0),
                tours: tour != null ? new TourBook(tour) : null, quests: book);

        private static void TickFor(TycoonRun run, double seconds, double step = 0.25)
        {
            for (double t = 0; t < seconds; t += step) run.Tick(step);
        }

        /// <summary>Ticks until <paramref name="done"/> holds; fails the test after <paramref name="limit"/> seconds.</summary>
        private static void TickUntil(TycoonRun run, Func<bool> done, string what, double limit = 120)
        {
            for (double t = 0; !done(); t += 0.25)
            {
                Assert.Less(t, limit, what);
                run.Tick(0.25);
            }
        }

        private static void PourTheOrder(TycoonRun run)
        {
            run.PourMeasure("gin", 0.4);
            run.PourMeasure("soda", 0.55);
        }

        // ── the door ────────────────────────────────────────────────────────────────

        [Test]
        public void ARunBuiltWithoutATour_HasNoneAndItsDoorIsOpen()
        {
            var run = NewRun("no-tour", null);
            Assert.IsFalse(run.TourRunning);
            Assert.IsNull(run.TourStep);
            TickFor(run, 10);
            Assert.Greater(run.Floor.Elapsed, 9.9, "the night's clock runs");
        }

        [Test]
        public void WhileSheTalks_TheClockStandsAndNobodyWalksIn()
        {
            var run = NewRun("held-door", Script());
            Assert.IsTrue(run.TourRunning);
            Assert.AreEqual("welcome", run.TourStep.Id);
            TickFor(run, 600);
            Assert.AreEqual(0.0, run.Floor.Elapsed, "the night has not started");
            Assert.AreEqual(0, run.Floor.Arrived, "and nobody has come in");
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);
        }

        [Test]
        public void TheGuestStepSeatsOneGuest_WhoDecidesButNeverLosesPatience()
        {
            var run = NewRun("the-guest", Script());
            run.HearTour();
            Assert.AreEqual("guest", run.TourStep.Id);
            Assert.IsNull(run.TourGuest, "the guest is seated on the floor's tick, not on the key");
            var seated = run.Tick(0.25);
            Assert.IsNotNull(run.TourGuest);
            CollectionAssert.Contains(seated.ToList(), run.TourGuest, "the tick says who sat down, for the screen");
            Assert.AreEqual(1, run.Floor.Arrived);

            TickUntil(run, () => run.TourStep.Id != "guest", "the guest makes up their mind");
            Assert.AreEqual("stool", run.TourStep.Id);
            Assert.IsTrue(run.TourGuest.HasOrdered);
            double patience = run.TourGuest.PatienceLeft;
            TickFor(run, 900);
            Assert.AreEqual(patience, run.TourGuest.PatienceLeft, "nobody's patience runs while she shows the player round");
            Assert.AreEqual(VisitState.Waiting, run.TourGuest.State);
            Assert.AreEqual(1, run.Floor.Arrived, "and still nobody else has come in");
            Assert.AreEqual(0.0, run.Floor.Elapsed);
        }

        // ── the whole tour ──────────────────────────────────────────────────────────

        [Test]
        public void TheTourPlaysThrough_OnThePlayersOwnVerbs_AndHandsTheNightOver()
        {
            var run = NewRun("the-tour", Script(), Book());
            run.HearTour();
            TickUntil(run, () => run.TourStep.Id == "stool", "the guest sits and decides");

            // The screen's reports are ignored unless the step is waiting for them.
            run.TourSaw(TourWait.BookOpen);
            Assert.AreEqual("stool", run.TourStep.Id, "a report for another step moves nothing");
            run.HearTour();
            Assert.AreEqual("stool", run.TourStep.Id, "and the key does not skip a step that waits for the player");

            Assert.IsNull(run.TourNextBottle, "the drink lives behind the card");
            run.TourGuest.InspectId();
            run.Tick(0.25);
            Assert.AreEqual("card_away", run.TourStep.Id);
            run.TourSaw(TourWait.CardPutAway);
            Assert.AreEqual("cellar", run.TourStep.Id);
            run.TourSaw(TourWait.CellarOpen);
            Assert.AreEqual("bottle", run.TourStep.Id);
            Assert.AreEqual("gin", run.TourNextBottle, "the page's first bottle");
            run.TourSaw(TourWait.BottleInHand);
            Assert.AreEqual("pour", run.TourStep.Id);

            run.PourMeasure("gin", 0.4);
            run.Tick(0.25);
            Assert.AreEqual("measure", run.TourStep.Id);
            run.HearTour();
            Assert.AreEqual("mixer", run.TourStep.Id);
            Assert.AreEqual("soda", run.TourNextBottle, "then the one still missing from the tin");
            run.PourMeasure("soda", 0.55);
            run.Tick(0.25);
            Assert.IsNull(run.TourNextBottle, "the tin has everything the page names");
            Assert.AreEqual("cap", run.TourStep.Id);
            run.TourSaw(TourWait.Capped);
            Assert.AreEqual("tip", run.TourStep.Id);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            run.Tick(0.25);
            Assert.AreEqual("serve_key", run.TourStep.Id);
            run.TourSaw(TourWait.OnTheCounter);
            Assert.AreEqual("hand_over", run.TourStep.Id);

            var guest = run.TourGuest;
            Assert.AreEqual(OrderMatch.Exact, run.ServeTo(guest).Match);
            run.Tick(0.25);
            Assert.AreEqual("finish", run.TourStep.Id);
            TickUntil(run, () => run.TourStep.Id != "finish", "the guest drinks up and goes");
            Assert.IsTrue(run.Floor.Finished.Contains(guest));
            Assert.AreEqual(0.0, run.Floor.Elapsed, "the night's clock still has not moved");

            Assert.AreEqual("glass", run.TourStep.Id);
            foreach (var mess in run.Floor.Messes.ToList()) if (mess.HasGlass) run.CollectGlass(mess);
            run.WashGlasses();
            run.Tick(0.25);
            // The dice may have left no mark: then there is nothing to wipe and the step passes unsaid.
            if (run.TourStep.Id == "wipe")
            {
                foreach (var mess in run.Floor.Messes.ToList()) if (mess.Smudged) run.Wipe(mess);
                run.Tick(0.25);
            }
            Assert.AreEqual("book", run.TourStep.Id);
            run.TourSaw(TourWait.BookOpen);
            run.TourSaw(TourWait.BookShut);
            Assert.AreEqual("doors", run.TourStep.Id);
            Assert.AreEqual(0, run.Floor.House.DirtSpotSeconds, "no mark aged against a clock that was not running");

            Assert.IsNull(run.HostessVisit, "the job comes with her last word, not before");
            run.HearTour();
            Assert.IsFalse(run.TourRunning, "the tour is over");
            Assert.IsFalse(run.TourSkipped);
            Assert.IsNotNull(run.HostessVisit, "she stays for the first job");
            Assert.AreEqual("first_wage", run.HostessVisit.Offered.Id);
            run.HearHostess();
            Assert.AreEqual("first_wage", run.Quest.Id, "the job is on the bar for the night she showed the player round");

            TickFor(run, 5);
            Assert.Greater(run.Floor.Elapsed, 4.9, "and the night opens");
            Assert.Greater(guest.Paid, 0, "the tour's guest paid like anybody");
        }

        [Test]
        public void SheDoesNotComeBackAtTheClose_OnceTheTourHandedTheJobOver()
        {
            var run = NewRun("no-second-visit", Script(), Book());
            run.SkipTour();
            Assert.IsNotNull(run.HostessVisit, "skipping still leaves her with the job to hand over");
            run.HearHostess();
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 20000, "the night must end");
                run.Tick(0.5);
                TestNight.Clean(run);
                foreach (var v in run.Floor.Seated.ToList())
                    if (v.State == VisitState.Waiting && v.HasOrdered) run.DeclineOrder(v);
                Assert.IsNull(run.HostessVisit, "she said her piece at the start of the night");
            }
        }

        [Test]
        public void APlayerWhoRunsAhead_IsCaughtUp()
        {
            var run = NewRun("ahead", Script());
            run.HearTour();
            TickUntil(run, () => run.TourStep.Id == "stool", "the guest sits and decides");
            run.TourGuest.InspectId();
            // Straight through without waiting for her: the drink made and handed over while she is on the card.
            PourTheOrder(run);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            run.ServeTo(run.TourGuest);
            run.Tick(0.25);
            Assert.AreEqual("finish", run.TourStep.Id, "everything already done is done - she catches up");
        }

        [Test]
        public void AStepAlreadyTrue_PassesUnsaid_ButAThingSaidIsAlwaysSaid()
        {
            var run = NewRun("unsaid", Script());
            run.HearTour();
            TickUntil(run, () => run.TourStep.Id == "stool", "the guest sits and decides");
            run.TourGuest.InspectId();
            run.Tick(0.25);
            run.TourSaw(TourWait.CardPutAway);
            run.TourSaw(TourWait.CellarOpen);
            run.TourSaw(TourWait.BottleInHand);
            PourTheOrder(run);   // both bottles at once: the mixer step will have nothing left to ask
            run.Tick(0.25);
            Assert.AreEqual("measure", run.TourStep.Id, "the look-ahead never jumps a thing she has to say");
            run.HearTour();
            Assert.AreEqual("cap", run.TourStep.Id, "the tin was built already: the mixer step passes without a word");
        }

        [Test]
        public void ADrinkBinnedOnTheWay_SendsTheTourBackToTheCellar()
        {
            var run = NewRun("binned", Script());
            run.HearTour();
            TickUntil(run, () => run.TourStep.Id == "stool", "the guest sits and decides");
            run.TourGuest.InspectId();
            run.Tick(0.25);
            run.TourSaw(TourWait.CardPutAway);
            run.TourSaw(TourWait.CellarOpen);
            run.TourSaw(TourWait.BottleInHand);
            PourTheOrder(run);
            run.Tick(0.25);
            run.HearTour();
            Assert.AreEqual("cap", run.TourStep.Id);
            run.DiscardGlass();
            run.Tick(0.25);
            Assert.AreEqual("cellar", run.TourStep.Id, "no drink any more: she starts it again from the cellar");
            Assert.IsTrue(run.TourRunning);
        }

        // ── skipping ────────────────────────────────────────────────────────────────

        [Test]
        public void SkippingOpensTheNightWhereItStands()
        {
            var run = NewRun("skip", Script());
            run.HearTour();
            TickUntil(run, () => run.TourStep.Id == "stool", "the guest sits and decides");
            var guest = run.TourGuest;
            double patience = guest.PatienceLeft;
            run.SkipTour();
            Assert.IsFalse(run.TourRunning);
            Assert.IsTrue(run.TourSkipped);
            Assert.IsNull(run.TourStep);
            TickFor(run, 5);
            Assert.Less(guest.PatienceLeft, patience, "the guest is an ordinary drinker now: their patience runs");
            Assert.Greater(run.Floor.Elapsed, 4.9, "and so does the night");
            run.HearTour();
            run.TourSaw(TourWait.BookOpen);   // harmless once it is over
        }

        [Test]
        public void TheTourOnlyEverOpensTheFirstNight()
        {
            var run = NewRun("first-night-only", Script());
            Assert.AreEqual(1, run.Day);
            Assert.IsTrue(run.TourRunning);
            run.SkipTour();
            Assert.IsFalse(run.TourRunning);
        }

        // ── the script ──────────────────────────────────────────────────────────────

        [Test]
        public void TheScript_RefusesAShapeItCannotPlay()
        {
            Assert.Throws<ArgumentException>(() => new TourScript(new[] { S("a", TourWait.Heard), S("b", TourWait.CardRead) }),
                "a step on the guest before one is seated");
            Assert.Throws<ArgumentException>(() => new TourScript(new[]
                { S("a", TourWait.GuestReady), S("b", TourWait.GuestReady), S("c", TourWait.Heard) }), "two guests");
            Assert.Throws<ArgumentException>(() => new TourScript(new[] { S("a", TourWait.Heard), S("a", TourWait.Heard) }),
                "an id written twice");
            Assert.Throws<ArgumentException>(() => new TourScript(new[] { S("a", TourWait.Heard), S("b", TourWait.CellarOpen) }),
                "a tour that ends waiting for the player");
            Assert.DoesNotThrow(() => new TourScript(new[] { S("a", TourWait.CellarOpen), S("b", TourWait.Heard) }),
                "the cellar can be shown before anybody sits down");
        }

        // ── the first-use lessons ───────────────────────────────────────────────────

        private static readonly IngredientCard Lemon =
            new IngredientCard("lemon", "Lemon", IngredientType.Sour, 2, new IngredientInfo("lemon"));

        private static readonly RecipeDefinition GinSour =
            new RecipeDefinition("gin_sour_t", "Gin Sour", rank: 3, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement("gin", 0.5, 0.7),
                    new RatioRequirement("lemon", 0.3, 0.5),
                },
                minFill: 0.5, prep: PrepMethod.Shaken);

        private static TourScript Lesson(string id, TourCue when, params TourStep[] steps) => new TourScript(id, when, steps);

        private static TourBook Lessons() => new TourBook(new[]
        {
            Lesson("door", TourCue.FirstDoor, S("door_age", TourWait.Heard, "card_age"), S("door_kick", TourWait.Heard, "card_kick")),
            Lesson("shake", TourCue.FirstShake, S("shake_cap", TourWait.Capped, "lid"), S("shake_it", TourWait.Shaken, "tin"),
                S("shake_done", TourWait.Heard)),
        });

        private static TycoonRun LessonRun(string seed, IReadOnlyList<RecipeDefinition> menu = null) =>
            new TycoonRun(new Shelf(new[]
                {
                    new ShelfBottle(Gin.Clone(), capacity: 4000),
                    new ShelfBottle(Soda.Clone(), capacity: 4000),
                    new ShelfBottle(Lemon.Clone(), capacity: 4000),
                }),
                menu ?? Menu, new RunRng(seed), config: new TycoonConfig(startingMoney: 5000, savorSeconds: 6.0),
                tours: Lessons());

        [Test]
        public void TheDoorLesson_StartsTheFirstTimeACardIsReadWithTheDoorOpen_AndOnlyOnce()
        {
            var run = LessonRun("door-lesson");
            Assert.IsFalse(run.TourRunning, "no house tour in this book");
            TickUntil(run, () => run.Floor.Seated.Any(v => v.HasOrdered), "somebody sits and decides");
            var first = run.Floor.Seated.First(v => v.HasOrdered);
            first.InspectId();
            run.Tick(0.25);
            Assert.IsFalse(run.TourRunning, "no door yet: the bar has no stars");

            run.DevPresetStars(1.0);
            Assert.IsTrue(run.Has(Feature.Door));
            TickUntil(run, () => run.Floor.Seated.Any(v => v.HasOrdered && !v.IdInspected), "a second guest decides");
            var second = run.Floor.Seated.First(v => v.HasOrdered && !v.IdInspected);
            second.InspectId();
            run.Tick(0.25);
            Assert.AreEqual("door", run.TourId, "the card read with the door open starts the lesson");
            Assert.AreSame(second, run.TourSubject, "about the guest whose card it was");
            double patience = second.PatienceLeft;
            TickFor(run, 60);
            Assert.AreEqual(patience, second.PatienceLeft, "the door is held while she explains it");
            run.HearTour();
            run.HearTour();
            Assert.IsFalse(run.TourRunning);
            Assert.IsNull(run.TourSubject);
            CollectionAssert.Contains(run.ToursTaught.ToList(), "door");

            TickUntil(run, () => run.Floor.Seated.Any(v => v.HasOrdered && !v.IdInspected), "a third guest decides");
            run.Floor.Seated.First(v => v.HasOrdered && !v.IdInspected).InspectId();
            run.Tick(0.25);
            Assert.IsFalse(run.TourRunning, "a lesson plays once a run");
        }

        [Test]
        public void TheShakeLesson_StartsWhenAShakenDrinkStandsUnmixed_AndWaitsForTheShake()
        {
            var run = LessonRun("shake-lesson", new[] { Menu[0], GinSour });
            int i0 = 0, i1 = 1;
            double lo0 = Math.Max(0.5, GinSour.PerfectBoxes[i0] * 0.2), hi0 = Math.Min(0.7, GinSour.PerfectBoxes[i0] * 0.2 + 0.2);
            double lo1 = Math.Max(0.3, GinSour.PerfectBoxes[i1] * 0.2), hi1 = Math.Min(0.5, GinSour.PerfectBoxes[i1] * 0.2 + 0.2);
            double gin = (lo0 + hi0) * 0.5, lemon = (lo1 + hi1) * 0.5, total = gin + lemon;
            run.PourMeasure("gin", gin / total * 0.9);
            run.PourMeasure("lemon", lemon / total * 0.9);
            Assert.AreEqual(PrepMethod.Shaken, run.TinMethod, "the tin reads as the shaken page");
            run.Tick(0.25);
            Assert.AreEqual("shake", run.TourId, "an unmixed shaken drink starts the lesson");
            Assert.AreEqual("shake_cap", run.TourStep.Id);
            run.TourSaw(TourWait.Capped);
            Assert.AreEqual("shake_it", run.TourStep.Id);
            run.Shake(1.0);
            run.Tick(0.25);
            Assert.AreEqual("shake_done", run.TourStep.Id, "the shake moves it on");
            run.HearTour();
            Assert.IsFalse(run.TourRunning);
        }

        [Test]
        public void ALessonWhoseDrinkIsBinned_IsOver()
        {
            var run = LessonRun("binned-lesson", new[] { Menu[0], GinSour });
            run.PourMeasure("gin", 0.55);
            run.PourMeasure("lemon", 0.35);
            run.Tick(0.25);
            if (!run.TourRunning) Assert.Inconclusive("the tin did not read as the shaken page at these shares");
            run.DiscardGlass();
            run.Tick(0.25);
            Assert.IsFalse(run.TourRunning, "nothing left to shake: the lesson is over, the door open again");
            TickFor(run, 5);
            Assert.Greater(run.Floor.Elapsed, 4.9);
        }

        [Test]
        public void TheLessonsPlayed_ComeBackWithASavedBar()
        {
            var run = LessonRun("saved-lessons");
            run.DevPresetStars(1.0);
            TickUntil(run, () => run.Floor.Seated.Any(v => v.HasOrdered), "somebody sits and decides");
            run.Floor.Seated.First(v => v.HasOrdered).InspectId();
            run.Tick(0.25);
            Assert.AreEqual("door", run.TourId);
            run.SkipTour();
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 40000, "the night must end");
                run.Tick(0.5);
                TestNight.Clean(run);
                foreach (var v in run.Floor.Seated.ToList())
                    if (v.State == VisitState.Waiting && v.HasOrdered) run.DeclineOrder(v);
            }
            RunSnapshot snap = null;
            run.ContinueToNextDay(s => snap = s);
            Assert.IsNotNull(snap);
            CollectionAssert.Contains(snap.toursTaught, "door");
            var back = TycoonRun.Restore(snap, Menu, new[] { Gin, Soda, Lemon },
                config: new TycoonConfig(startingMoney: 5000, savorSeconds: 6.0), tours: Lessons());
            CollectionAssert.Contains(back.ToursTaught.ToList(), "door", "the resumed bar remembers the lesson");
            Assert.IsFalse(back.TourRunning);
        }

        // ── the shipped tour ────────────────────────────────────────────────────────

        private static string ReadData(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static string ReadResource(string file) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Data", file));

        [Test]
        public void TheShippedTour_Parses_AndPointsOnlyAtThingsTheScreenKnows()
        {
            var book = DataLoader.ParseTours(ReadResource("tour.json"));
            var tour = book.For(TourCue.FirstNight);
            Assert.IsNotNull(tour, "the house tour");
            Assert.Greater(tour.Steps.Count, 10);
            Assert.AreEqual(1, tour.Steps.Count(s => s.Wait == TourWait.GuestReady), "one guest");
            Assert.IsTrue(tour.Steps.Any(s => s.Wait == TourWait.RecipePinned), "the tour shows the recipe can be pinned");
            foreach (var cue in new[] { TourCue.FirstDoor, TourCue.FirstShake, TourCue.FirstStir, TourCue.FirstGarnish, TourCue.FirstRim })
                Assert.IsNotNull(book.For(cue), "a lesson for " + cue);
            foreach (var t in book.Tours)
            {
                foreach (var step in t.Steps)
                {
                    Assert.IsTrue(TourPoints.Known(step.Point), step.Id);
                    Assert.IsTrue(TourPoints.Known(step.To), step.Id);
                }
                Assert.AreEqual(TourWait.Heard, t.Steps[t.Steps.Count - 1].Wait, t.Id + " ends on a thing said");
            }
        }

        [Test]
        public void TheLoader_RefusesADrinkNamedBeforeTheCard()
        {
            const string early = "{ \"steps\": [ { \"id\": \"a\", \"wait\": \"heard\", \"point\": \"\", \"say\": [\"Make me a {drink}.\"] }," +
                                 " { \"id\": \"b\", \"wait\": \"guest_ready\", \"point\": \"stool\", \"say\": [\"x\"] }," +
                                 " { \"id\": \"c\", \"wait\": \"heard\", \"point\": \"\", \"say\": [\"y\"] } ] }";
            Assert.Throws<FormatException>(() => DataLoader.ParseTours(Book(early)));
            const string point = "{ \"steps\": [ { \"id\": \"a\", \"wait\": \"heard\", \"point\": \"moon\", \"say\": [\"x\"] } ] }";
            Assert.Throws<FormatException>(() => DataLoader.ParseTours(Book(point)));
            const string wait = "{ \"steps\": [ { \"id\": \"a\", \"wait\": \"dance\", \"point\": \"\", \"say\": [\"x\"] } ] }";
            Assert.Throws<FormatException>(() => DataLoader.ParseTours(Book(wait)));
            const string garnish = "{ \"steps\": [ { \"id\": \"a\", \"wait\": \"heard\", \"point\": \"\", \"say\": [\"A {garnish}.\"] } ] }";
            Assert.Throws<FormatException>(() => DataLoader.ParseTours(Book(garnish, "first_door")),
                "only the garnish lessons know a garnish");
            Assert.DoesNotThrow(() => DataLoader.ParseTours(Book(garnish, "first_garnish")));
        }

        /// <summary>A one-tour book around the old one-script shape.</summary>
        private static string Book(string script, string when = "first_night") =>
            "{ \"tours\": [ { \"id\": \"t\", \"when\": \"" + when + "\", " + script.Trim().TrimStart('{').TrimEnd('}') + " } ] }";



        /// <summary>
        /// The first night as the scene deals it - the starting shelf, the real book of recipes and jobs, the scene's
        /// config - played through the shipped tour with the screen's reports made by hand. Every seed's first guest
        /// orders a drink the tour can walk the player through, bottle by bottle.
        /// </summary>
        [TestCase("tour-a")]
        [TestCase("tour-b")]
        [TestCase("tour-c")]
        public void TheShippedTour_WalksTheFirstNightThrough_OnTheShippedContent(string seed)
        {
            var tour = DataLoader.ParseTours(ReadResource("tour.json"));
            var bar = DataLoader.ParseDeck(ReadData("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(ReadData("recipes/recipes.json"));
            var fixtures = DataLoader.ParseFixtures(ReadData("fixtures/fixtures.json")).Fixtures;
            var book = DataLoader.ParseQuests(ReadResource("quests.json"), fixtures);
            var shelf = new Shelf(bar.Cards.Where(bar.IsStarting).Select(c => new ShelfBottle(c.Clone())).ToList());
            var run = new TycoonRun(shelf, recipes, new RunRng(seed), config: TycoonConfig.ForTheScene,
                fixtures: fixtures, quests: book, tours: tour);

            int guard = 0;
            while (run.TourRunning)
            {
                Assert.Less(guard++, 20000, "the tour must end; stuck on " + run.TourStep);
                var step = run.TourStep;
                var guest = run.TourGuest;
                switch (step.Wait)
                {
                    case TourWait.Heard: run.HearTour(); continue;
                    case TourWait.CardRead: guest.InspectId(); break;
                    case TourWait.FirstPour:
                    case TourWait.TinBuilt:
                        var next = run.TourNextBottle;
                        Assert.IsNotNull(next, "the tour names a bottle on the shelf at " + step.Id);
                        run.PourMeasure(next, ShareOf(guest.Order.Wanted, shelf, next) * 0.9);
                        break;
                    case TourWait.InTheGlass: run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0); break;
                    case TourWait.Served:
                        Assert.AreEqual(OrderMatch.Exact, run.ServeTo(guest).Match, "the tour's drink is the drink ordered");
                        break;
                    case TourWait.GlassCollected:
                        foreach (var mess in run.Floor.Messes.ToList()) if (mess.HasGlass) run.CollectGlass(mess);
                        if (run.GlassesInHand > 0 && !run.SinkBusy) run.WashGlasses();
                        break;
                    case TourWait.CounterWiped:
                        foreach (var mess in run.Floor.Messes.ToList()) if (mess.Smudged) run.Wipe(mess);
                        break;
                    default:
                        if (TourWaits.IsScreen(step.Wait)) { run.TourSaw(step.Wait); continue; }
                        break;
                }
                run.Tick(0.25);
            }
            Assert.AreEqual(0.0, run.Floor.Elapsed, "the whole tour played on a held door");
            Assert.AreEqual(1, run.Floor.Arrived, "with its one guest");
            Assert.IsNotNull(run.HostessVisit);
            Assert.AreEqual("first_wage", run.HostessVisit.Offered.Id, "and ended on the book's first job");
        }

        /// <summary>The middle of the box the page lights for the bottle's style (the matcher's test: each share lands in
        /// its perfect value's 20-point box), kept inside the page's band, as a share of the tin.</summary>
        private static double ShareOf(RecipeDefinition recipe, Shelf shelf, string bottleId)
        {
            var style = shelf.Bottles.First(b => b.Ingredient.Id == bottleId).Ingredient.Info.Style;
            int i = recipe.RatioRequirements.ToList().FindIndex(r => r.IsStyleBand && r.Style == style);
            var band = recipe.RatioRequirements[i];
            double lo = Math.Max(band.MinRatio, recipe.PerfectBoxes[i] * 0.2);
            double hi = Math.Min(band.MaxRatio, recipe.PerfectBoxes[i] * 0.2 + 0.2);
            return (lo + hi) * 0.5;
        }
    }
}
