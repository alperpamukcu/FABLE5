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
    /// THE WEEK'S JOB (2026-09-04). It is the first thing in this game that spans more than
    /// one night, so the two things worth pinning are the two that only a calendar can get
    /// wrong: WHEN one is handed over, and WHAT it is allowed to ask for.
    /// </summary>
    public sealed class WeeklyJobTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static TycoonRun NewBar(string seed = "job")
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var glassware = DataLoader.ParseGlassware(Read("glassware/glassware.json"));
            var starting = deck.Cards
                .Where(c => c.Info == null || c.Info.Tier <= 1)
                .Select(c => new ShelfBottle(c.Clone(), 60)).ToList();
            // A PURSE THAT SURVIVES SIX NIGHTS OF RENT. These tests play empty nights —
            // nobody is served, so nothing comes in — and the opening $20 is gone by
            // Wednesday: the run goes bankrupt on day 4 and the calendar simply stops, which
            // is what the first cut of these tests measured instead of the hand-over.
            // The rent is the only number being escaped; everything else is the shipped config.
            var config = new TycoonConfig(startingMoney: 5000, savorSeconds: 13.2);
            return new TycoonRun(new Shelf(starting), recipes, new RunRng(seed),
                config: config, glassware: glassware, lockedStock: deck.LockedCards);
        }

        /// <summary>Plays a night out and closes the books, the way the HUD does. Fails
        /// loudly rather than silently stalling: a run that stops advancing turns every
        /// assertion below into a lie about the calendar.</summary>
        private static void CloseANight(TycoonRun run)
        {
            int was = run.Day;
            for (int guard = 0; guard < 20000 && run.Phase == TycoonPhase.DayOpen; guard++)
                run.Tick(0.25);
            Assert.That(run.Phase, Is.EqualTo(TycoonPhase.DayEnd),
                "day " + was + " never closed — the run is in " + run.Phase);
            run.ContinueToNextDay();
            Assert.That(run.Day, Is.EqualTo(was + 1),
                "day " + was + " closed and the calendar did not move");
        }

        [Test]
        public void TheFirstWeekHasNoJob_AndTheSecondDoes()
        {
            var run = NewBar();
            Assert.That(run.Job, Is.Null, "a job was handed over before the bar had opened");
            Assert.That(BarCalendar.WeekOf(run.Day), Is.EqualTo(1));

            // Six nights is one week; the seventh day is the next week's Monday.
            for (int i = 0; i < BarCalendar.OpenNights; i++)
                CloseANight(run);

            Assert.That(BarCalendar.WeekOf(run.Day), Is.EqualTo(2), "the calendar did not turn over");
            Assert.That(run.Job, Is.Not.Null, "the second week opened with no job on the bar");
            Assert.That(run.Job.Week, Is.EqualTo(2), "the job was set for the wrong week");
            Assert.That(run.Job.Target, Is.InRange(WeeklyJobs.MinTarget, WeeklyJobs.MaxTarget));
        }

        /// <summary>
        /// ONE JOB PER WEEK, NOT ONE PER NIGHT. The hand-over is read off the calendar, so
        /// the nights inside a week must leave it exactly where it was — including its
        /// progress, which a re-roll would silently reset.
        /// </summary>
        [Test]
        public void AJobSurvivesTheNightsInsideItsWeek()
        {
            var run = NewBar();
            for (int i = 0; i < BarCalendar.OpenNights; i++)
                CloseANight(run);
            var job = run.Job;
            Assert.That(job, Is.Not.Null);

            CloseANight(run);
            Assert.That(run.Job, Is.SameAs(job), "a second night re-rolled the week's job");
            Assert.That(BarCalendar.WeekOf(run.Day), Is.EqualTo(2), "the week ended early");
        }

        /// <summary>
        /// IT MAY ONLY ASK FOR WHAT THE BAR CAN POUR. A job naming a drink the player has no
        /// bottle for is not a challenge — it is a bug they cannot tell from one.
        /// </summary>
        [Test]
        public void AJobOnlyEverNamesADrinkTheShelfCanMake()
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var run = NewBar("job-" + seed);
                for (int i = 0; i < BarCalendar.OpenNights; i++)
                    CloseANight(run);
                if (run.Job == null) continue;

                var recipe = run.MenuRecipes.FirstOrDefault(r => r.Id == run.Job.RecipeId);
                Assert.That(recipe, Is.Not.Null,
                    "seed " + seed + " asked for '" + run.Job.RecipeId + "', which is not on the menu");
                Assert.That(recipe.HasAuthoredRatios, Is.True,
                    "seed " + seed + " asked for a drink with nothing to get right: " + recipe.Name);
                foreach (var band in recipe.RatioRequirements)
                {
                    bool answered = run.Shelf.Bottles.Any(b => band.IsStyleBand
                        ? b.Ingredient.Info != null && b.Ingredient.Info.Style == band.Style
                          && b.Ingredient.Info.Tier >= band.MinTier
                        : b.Ingredient.Type == band.Type);
                    Assert.That(answered, Is.True,
                        "seed " + seed + " asked for " + recipe.Name + ", and the shelf has no "
                        + (band.IsStyleBand ? band.Style : band.Type.ToString()));
                }
            }
        }

        [Test]
        public void CountingOnlyTakesItsOwnDrink_AndStopsAtTheTarget()
        {
            var job = new WeeklyJob("negroni", "Negroni", 2, 3);
            Assert.That(job.Count("gin_tonic"), Is.False, "a different drink was counted");
            Assert.That(job.Served, Is.EqualTo(0));
            Assert.That(job.Count("negroni"), Is.False, "it finished one short");
            Assert.That(job.Count("negroni"), Is.True, "the second one did not finish it");
            Assert.That(job.IsDone, Is.True);
            job.Count("negroni");
            Assert.That(job.Served, Is.EqualTo(2), "it kept counting past the target");
        }

        /// <summary>The target grows with the bar and never past the ceiling.</summary>
        [Test]
        public void TheTargetClimbsWithTheWeeksAndStops()
        {
            Assert.That(WeeklyJobs.TargetFor(1), Is.EqualTo(WeeklyJobs.MinTarget));
            Assert.That(WeeklyJobs.TargetFor(2), Is.EqualTo(WeeklyJobs.MinTarget));
            Assert.That(WeeklyJobs.TargetFor(3), Is.GreaterThan(WeeklyJobs.MinTarget));
            for (int week = 1; week < 60; week++)
                Assert.That(WeeklyJobs.TargetFor(week),
                    Is.InRange(WeeklyJobs.MinTarget, WeeklyJobs.MaxTarget), "week " + week);
        }

        /// <summary>A job belongs to its week and to no other, which is what the HUD strip
        /// asks before it draws anything.</summary>
        [Test]
        public void AJobRunsOnlyInItsOwnWeek()
        {
            var job = new WeeklyJob("negroni", "Negroni", 3, 2);
            Assert.That(job.RunsOn(BarCalendar.DayOf(2, BarNight.Monday)), Is.True);
            Assert.That(job.RunsOn(BarCalendar.DayOf(2, BarNight.Saturday)), Is.True);
            Assert.That(job.RunsOn(BarCalendar.DayOf(1, BarNight.Saturday)), Is.False);
            Assert.That(job.RunsOn(BarCalendar.DayOf(3, BarNight.Monday)), Is.False);
        }
    }
}
