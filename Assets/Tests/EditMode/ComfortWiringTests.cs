using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE ROOM'S RATING, WIRED (GDD 27, PLAN_house_and_law H1b, 2026-09-05). HouseTests pin
    /// the pure rules; these pin what the RUN does with them — in the night's-end shape
    /// (NightReportTests, 2026-08-25): ask, then close, then check the two agree.
    /// </summary>
    public sealed class ComfortWiringTests
    {
        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 40),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
        });

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[]
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

        private static TycoonRun NewRun(string seed = "house", params FixtureDefinition[] fixtures) =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(200, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: fixtures);

        private static void ServeEveryone(TycoonRun run)
        {
            foreach (var visit in run.Floor.Seated.ToList())
            {
                if (visit.State != VisitState.Waiting) continue;
                run.PourMeasure("gin", 0.35);
                run.PourMeasure("soda", 0.35);
                run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
                run.ServeTo(visit);
            }
        }

        /// <summary>Plays the night out serving everyone; cleans only if asked.</summary>
        private static void PlayNight(TycoonRun run, bool clean)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 600, "the day must terminate");
                run.Tick(5);
                if (clean) TestNight.Clean(run);
                ServeEveryone(run);
            }
        }

        // ── the rung of the room ─────────────────────────────────────────────────

        private static FixtureDefinition Lamp(int level, double comfort, bool owned = false) =>
            new FixtureDefinition("lamps_" + level, "Mark " + level + " Lamps", "wall_lamps",
                25 * level, 0, "Two on the wall.", "fx_wall_lamp_lv" + (level - 1),
                0.9f, 0.7f, 0.5f, 0.9f, 120f, startsInTheRoom: owned, level: level, comfort: comfort);

        private static FixtureDefinition Candle(double comfort) =>
            new FixtureDefinition("candle", "Candle", "counter_end", 30, 0, "A flame.", "fx_candle",
                comfort: comfort);

        [Test]
        public void FixtureComfort_CountsWhatTheRoomStands_NeverAFittedOverRung()
        {
            var run = NewRun("rungs", Lamp(1, 0.0, owned: true), Lamp(2, 0.15), Lamp(3, 0.35), Candle(0.10));
            Assert.AreEqual(0.0, run.FixtureComfort, 1e-9, "the mark the room opens with is the free base");
            Assert.AreEqual(2.0, run.ComfortBase, 1e-9);

            run.DevSkipToDayEnd();
            run.BuyFixture("lamps_2");
            run.BuyFixture("candle");
            Assert.AreEqual(0.25, run.FixtureComfort, 1e-9, "mark two and the candle");
            run.BuyFixture("lamps_3");
            Assert.AreEqual(0.45, run.FixtureComfort, 1e-9,
                "mark three REPLACES mark two — rungs carry absolute values, the fitted-over one counts nothing");
            Assert.AreEqual(2.45, run.ComfortBase, 1e-9);
        }

        [Test]
        public void AFixture_RefusesAComfortOffTheScale()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Candle(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Candle(5.1));
        }

        // ── ask, then close ──────────────────────────────────────────────────────

        [Test]
        public void ACleanNight_FilesTheRoomAtItsBase_AndTheSlipAgrees()
        {
            var run = NewRun();
            PlayNight(run, clean: true);
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);

            double comfort = run.ComfortTonight, service = run.ServiceTonight, tonight = run.TonightStars;
            Assert.AreEqual(2.0, comfort, 1e-9, "nothing bought, nothing left standing: the free base");
            Assert.AreEqual(1.0, run.CleanlinessTonight, 1e-9);
            Assert.AreEqual(Math.Min(service, comfort), tonight, 1e-9, "the night is the lower of the two");

            var result = run.ContinueToNextDay();
            Assert.AreEqual(comfort, result.ComfortStars, 1e-9, "what the slip asked is what the book filed");
            Assert.AreEqual(service, result.ServiceStars, 1e-9);
            Assert.AreEqual(tonight, result.NightStars, 1e-9);
        }

        [Test]
        public void ADirtyNight_FilesLower_ThanACleanOne_OnTheSameSeed()
        {
            var clean = NewRun("same-night");
            PlayNight(clean, clean: true);
            var dirty = NewRun("same-night");
            PlayNight(dirty, clean: false);

            Assert.Greater(dirty.Floor.House.MessesLeft, 0, "somebody drank");
            Assert.Less(dirty.CleanlinessTonight, 1.0, "and nobody cleaned");
            Assert.Less(dirty.ComfortTonight, clean.ComfortTonight, "so the room is worth less");
            Assert.GreaterOrEqual(dirty.ComfortTonight, VenueComfort.FreeBase - VenueComfort.DirtPenalty,
                "and never less than the penalty takes");

            var filed = dirty.ContinueToNextDay();
            Assert.Less(filed.ComfortStars, 2.0);
            Assert.LessOrEqual(filed.NightStars, filed.ComfortStars, "the room held the night down");
        }

        [Test]
        public void TheCrowd_ReadsTheServiceSide_NotTheMess()
        {
            // GDD 27 D8: a filthy counter holds the standing down; it does not by itself
            // turn tomorrow's crowd broke. Same seed, one bar cleans and one does not — the
            // crowd both draw is the same crowd.
            var clean = NewRun("crowd");
            PlayNight(clean, clean: true);
            var dirty = NewRun("crowd");
            PlayNight(dirty, clean: false);
            Assert.Less(dirty.ComfortTonight, clean.ComfortTonight);
            Assert.AreEqual(clean.ServiceTonight, dirty.ServiceTonight, 1e-9, "the drinks were the same drinks");
            Assert.AreEqual(clean.CrowdTomorrow, dirty.CrowdTomorrow);
        }

        [Test]
        public void TheLiveReading_FallsWithAGlassLeft_AndRecoversWhenItIsCarriedAway()
        {
            var run = NewRun("live");
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            Assert.AreEqual(run.ComfortBase, run.ComfortNow, 1e-9, "a clean counter reads the base");

            ServeEveryone(run);
            run.Tick(1.0);                      // they got up: a glass and a mark, inside the grace
            Assert.AreEqual(run.ComfortBase, run.ComfortNow, 1e-9, "the grace: nothing counts yet");
            run.Tick(Housekeeping.DirtGrace);   // …and past it
            Assert.Less(run.ComfortNow, run.ComfortBase, "one dirty spot on four stools");
            Assert.AreEqual(run.ComfortBase - VenueComfort.DirtPenalty / run.Seats, run.ComfortNow, 1e-9);

            var mess = run.Floor.Messes[0];
            run.CollectGlass(mess);
            run.Wipe(mess);
            Assert.AreEqual(run.ComfortBase, run.ComfortNow, 1e-9, "wiped: the room is whole again");
            Assert.AreEqual(1, run.GlassesInHand);
            double seconds = run.WashGlasses();
            Assert.IsTrue(run.SinkBusy);
            run.Tick(seconds + 0.01);
            Assert.IsFalse(run.SinkBusy);
            Assert.AreEqual(0, run.GlassesInHand);
        }

        [Test]
        public void ADeclinedOrder_AndAStormOff_LeaveNothing()
        {
            // GDD 27 C6: a declined order used to leave an invisible glass that held the
            // stool seven seconds. Nothing was poured; nothing is left.
            var run = NewRun("declined");
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];
            run.DeclineOrder(visit);
            run.Tick(0.1);
            Assert.AreEqual(0, run.Floor.Messes.Count, "a declined order leaves nothing on the counter");
            Assert.AreEqual(0, run.Floor.House.MessesLeft);

            // and the storm-off: let one run out of patience
            var stormy = new TycoonRun(NewShelf(), Book, new RunRng("storm"),
                config: new TycoonConfig(200, orderDecisionSeconds: 0, savorSeconds: 0));
            guard = 0;
            while (stormy.Floor.Finished.Count == 0) { Assert.Less(guard++, 400); stormy.Tick(5); }
            Assert.AreEqual(VisitState.StormedOff, stormy.Floor.Finished[0].State);
            Assert.AreEqual(0, stormy.Floor.House.MessesLeft, "a storm-off poured nothing");
        }

        [Test]
        public void TheSceneConfig_KeepsTheMarksOff_UntilTheClothIsDrawn()
        {
            // PLAN H4: the scene files only what it can show. The glass is on; the mark is not.
            Assert.IsTrue(TycoonConfig.Default.CounterSmudges, "the rule itself is on");
            Assert.IsFalse(TycoonConfig.ForTheScene.CounterSmudges);

            var run = new TycoonRun(NewShelf(), Book, new RunRng("scene"),
                config: new TycoonConfig(200, orderDecisionSeconds: 0, savorSeconds: 0, counterSmudges: false));
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            ServeEveryone(run);
            run.Tick(1.0);
            var mess = run.Floor.Messes[0];
            Assert.IsTrue(mess.HasGlass);
            Assert.IsFalse(mess.Smudged, "no mark until the cloth exists");
            run.CollectGlass(mess);
            Assert.AreEqual(0, run.Floor.Messes.Count, "collected, and the spot is clean");
        }

        [Test]
        public void TheCloseBlock_WashesTheHandForFree_BeforeTheNightIsRead()
        {
            var run = NewRun("close");
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 600);
                run.Tick(5);
                if (run.Phase != TycoonPhase.DayOpen) break;   // the tick that closed the door
                // collect and wipe, but never wash: the hand fills up
                var messes = run.Floor.Messes;
                for (int i = messes.Count - 1; i >= 0; i--)
                {
                    if (messes[i].HasGlass) run.CollectGlass(messes[i]);
                    if (messes[i].Smudged) run.Wipe(messes[i]);
                }
                ServeEveryone(run);
            }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            Assert.AreEqual(0, run.GlassesInHand, "closing washed the hand");
            Assert.Greater(run.Floor.House.GlassesWashed, 0);
            Assert.AreEqual(2.0, run.ComfortTonight, 1e-9, "glasses in the hand never cost the room");
        }
    }
}
