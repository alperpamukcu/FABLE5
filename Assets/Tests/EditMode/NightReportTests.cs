using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE NIGHT'S END, ASKED BEFORE IT HAPPENS (2026-08-25).
    ///
    /// The books now show the player where tonight leaves their bar — the standing it
    /// climbs to, the crowd it draws, the ceiling it was held under — and every one of
    /// those is a RULE, shown before <see cref="TycoonRun.ContinueToNextDay"/> has run it.
    /// A screen that worked any of them out for itself would be a second copy of the loop,
    /// free to drift from the one the books keep.
    ///
    /// So every test here is the same shape: ASK, then CLOSE, then check the two agree.
    /// </summary>
    public class NightReportTests
    {
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

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 40),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
        });

        private static TycoonRun NewRun(string seed = "night-report") =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(200, orderDecisionSeconds: 0, savorSeconds: 0));

        /// <summary>Serves every seated customer an exact Spritz until the day closes, and
        /// keeps the counter clean while it does (GDD 27 §4) — the numbers pinned here are a
        /// clean bar's.</summary>
        private static void PlayDayServingEveryone(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 600, "the day must terminate");
                run.Tick(5);
                TestNight.Clean(run);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    if (visit.State != VisitState.Waiting) continue;
                    run.PourMeasure("gin", 0.35);
                    run.PourMeasure("soda", 0.35);
                    run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
                    run.ServeTo(visit);
                }
            }
        }

        // ── the step, asked without taking it ───────────────────────────────────

        [Test]
        public void StandingAfter_AnswersExactlyWhatClosingTheNightDoes()
        {
            foreach (double start in new[] { 0.0, 1.25, 2.5, 4.9 })
            foreach (double night in new[] { 0.0, 1.0, 2.5, 5.0 })
            {
                var asked = new BarRating(); asked.DevSet(start);
                var closed = new BarRating(); closed.DevSet(start);

                double preview = asked.StandingAfter(night);
                closed.CloseNight(night / BarRating.MaxStars);   // ExactStarsFor is 5x

                Assert.AreEqual(closed.Average, preview, 1e-12,
                    $"a {night}-star night on a {start}-star bar");
                Assert.AreEqual(start, asked.Average, 1e-12,
                    "asking where a night leads must not walk there");
            }
        }

        [Test]
        public void StandingAfter_KeepsTheClimbSlowerThanTheFall()
        {
            var bar = new BarRating();
            bar.DevSet(2.0);
            double up = bar.StandingAfter(4.0) - 2.0;
            double down = 2.0 - bar.StandingAfter(0.0);
            Assert.Greater(down, up, "a reputation is easier to lose than to earn");
        }

        [Test]
        public void Nights_KeepEveryClosedNightInTheOrderItWasFiled()
        {
            var bar = new BarRating();
            bar.CloseNight(0.2);   // 1.0
            bar.CloseNight(0.8);   // 4.0
            bar.CloseNight(1.0, cap: 2.5);
            CollectionAssert.AreEqual(new[] { 1.0, 4.0, 2.5 }, bar.Nights.ToArray());
            Assert.AreEqual(3, bar.NightsClosed);
        }

        // ── tonight's own stars ─────────────────────────────────────────────────

        [Test]
        public void TonightStars_AreHeldUnderTheFittingsAndTheMenu()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);

            Assert.Greater(run.Floor.AverageSatisfaction, 0.8,
                "every drink was exact, so the ROOM was happy");
            Assert.AreEqual(2.0, run.StarCeiling, 1e-9,
                "a dive with the starter menu is capped at two");
            Assert.AreEqual(2.0, run.TonightStars, 1e-9,
                "and the night is worth the ceiling, not the room's own five");
        }

        [Test]
        public void TonightStars_AreTheNumberTheBooksThenFile()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);

            double asked = run.TonightStars;
            run.ContinueToNextDay();

            Assert.AreEqual(asked, run.Rating.LastNight, 1e-12,
                "the screen and the books cannot disagree about the night");
        }

        [Test]
        public void StandingAfterTonight_IsWhereTheBooksLeaveTheBar()
        {
            var run = NewRun();
            run.Rating.DevSet(3.0);
            PlayDayServingEveryone(run);

            double before = run.Rating.Average;
            double asked = run.StandingAfterTonight;
            Assert.AreEqual(3.0, before, 1e-12, "asking moved nothing");

            run.ContinueToNextDay();
            Assert.AreEqual(asked, run.Rating.Average, 1e-12);
            Assert.Less(run.Rating.Average, 3.0,
                "a two-star ceiling drags a three-star bar down, which is the whole loop");
        }

        [Test]
        public void ANightNobodyWasServed_TakesTheBarDown()
        {
            var run = NewRun();
            run.Rating.DevSet(3.0);
            run.DevSkipToDayEnd();

            Assert.AreEqual(0.0, run.TonightStars, 1e-9, "nobody was poured for");
            double asked = run.StandingAfterTonight;
            run.ContinueToNextDay();
            Assert.AreEqual(asked, run.Rating.Average, 1e-12);
        }

        // ── tomorrow's crowd ────────────────────────────────────────────────────

        [Test]
        public void CrowdTomorrow_IsTheCrowdTheBooksThenSet()
        {
            var run = NewRun();
            run.Rating.DevSet(3.0);
            run.DevSkipToDayEnd();

            var asked = run.CrowdTomorrow;
            Assert.AreEqual(WealthTier.Broke, asked,
                "a night with nobody served empties tomorrow's pockets");

            run.ContinueToNextDay();
            Assert.AreEqual(asked, run.CrowdToday);
        }

        [Test]
        public void AMadeBar_KeepsItsRollersThroughABadNight()
        {
            // GDD 23 §7, and the reason the rule reads the standing the bar WALKED IN with:
            // fame alone brings the rollers, so one dreadful evening cannot cost a famous
            // bar its room. Pinned here because the preview and the books now share the
            // line that decides it.
            var run = NewRun();
            run.Rating.DevSet(4.5);
            run.DevSkipToDayEnd();

            Assert.AreEqual(WealthTier.HighRoller, run.CrowdTomorrow);
            run.ContinueToNextDay();
            Assert.AreEqual(WealthTier.HighRoller, run.CrowdToday);
        }
    }
}
