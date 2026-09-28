using System;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE NIGHT WAITS WHILE SHE TALKS (2026-09-27, the author: "Konuşmalar yaşanırken zaman ilerlememeli, yeni
    /// insanlar gelmemeli"). The hold is Core's, asked for by a verb: while it is on, Tick moves nothing - the
    /// clock, the door, the patience of whoever is already waiting.
    /// </summary>
    public class TalkHoldTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Book = new[]
        {
            new RecipeDefinition("spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, prep: PrepMethod.Built),
        };

        private static TycoonRun NewRun() => new TycoonRun(
            new Shelf(new[]
            {
                new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
                new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
            }),
            Book, new RunRng("talk-hold"),
            config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0, weeklyJobs: false));

        [Test]
        public void While_she_talks_nothing_on_the_floor_moves()
        {
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 200); run.Tick(5); }
            var waiting = run.Floor.Seated[0];
            double elapsed = run.Floor.Elapsed, patience = waiting.PatienceLeft;
            int arrived = run.Floor.Arrived;

            run.BeginTalk();
            Assert.IsTrue(run.Talking);
            for (int i = 0; i < 60; i++)
                CollectionAssert.IsEmpty(run.Tick(10), "nobody sits down while she talks");

            Assert.AreEqual(elapsed, run.Floor.Elapsed, "the clock stood still");
            Assert.AreEqual(arrived, run.Floor.Arrived, "the door stayed shut");
            Assert.AreEqual(patience, waiting.PatienceLeft, "nobody waiting lost patience");
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);

            run.EndTalk();
            Assert.IsFalse(run.Talking);
            run.Tick(5);
            Assert.Greater(run.Floor.Elapsed, elapsed, "and the night picks up where it stopped");
        }

        [Test]
        public void A_night_that_is_not_open_cannot_be_held_and_letting_go_is_always_safe()
        {
            var run = NewRun();
            run.EndTalk();                       // nothing held: harmless
            Assert.IsFalse(run.Talking);
            run.BeginTalk();
            Assert.IsTrue(run.Talking, "an open night can be held");
            run.EndTalk();
            // play the night out; at the books a hold is refused
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 2000, "the night must end");
                run.Tick(5);
                TestNight.Clean(run);        // nobody is served; they walk, and the counter is kept clear
            }
            run.BeginTalk();
            Assert.IsFalse(run.Talking, "a closed night has no clock to hold");
        }

        [Test]
        public void DevSkipToDayEnd_LetsGoOfAHeldNight()
        {
            // The dev verb plays the night out on the real clock; a conversation left holding it (a lesson, the
            // hostess) would freeze that clock and hand the night back still open after twenty thousand ticks.
            var run = NewRun();
            run.Tick(5);
            run.BeginTalk();
            Assert.IsTrue(run.Talking);
            run.DevSkipToDayEnd();
            Assert.IsFalse(run.Talking, "the hold is let go first");
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase, "and the night is played to its close");
        }
    }
}
