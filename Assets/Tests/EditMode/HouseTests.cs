using System;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE ROOM HAS A RATING OF ITS OWN (GDD 27, 2026-09-04, the author: "Oyuncular hem alkolü
    /// puanlar hem mekanı, 2 ayrı metrik olacak … bardaklar toplanmadıysa, tezgah silinmediyse
    /// bu konfor puanını düşürecek").
    ///
    /// These pin the PURE rules before anything reads them (PLAN_house_and_law H1): what the
    /// room is worth, what the mess costs, and the four verbs on the counter. The run's wiring
    /// (H1b) is proved in TycoonRunTests/NightReportTests in the ask-then-close shape; here
    /// nothing is run, only asked.
    /// </summary>
    public sealed class HouseTests
    {
        private const double Eps = 1e-9;

        // ── what the room is worth ───────────────────────────────────────────────

        [Test]
        public void TheMess_CanNeverTurnTheCrowdBrokeByItself()
        {
            // The broke line is 0.625 and the crowd reads the SERVICE side anyway (GDD 27
            // §2.3) — but the penalty is held under the free base with room to spare, so a
            // filthy fresh bar still files a room worth something.
            Assert.Greater(VenueComfort.FreeBase - VenueComfort.DirtPenalty, BarRating.BrokeStars);
        }

        [Test]
        public void AFreshRoom_IsWorthTheFreeBase()
        {
            // The number the old fittings ceiling opened with: a fresh bar still caps at two.
            Assert.AreEqual(2.0, VenueComfort.FreeBase, Eps);
            Assert.AreEqual(2.0, VenueComfort.Base(0, 0, 0), Eps);
        }

        [Test]
        public void TheGlassLadder_CountsAtHalfItsOldWeight()
        {
            // One full glass line used to lift the ceiling by 0.60; it lifts comfort by 0.30.
            Assert.AreEqual(2.30, VenueComfort.Base(0, 0.60, 0), Eps);
            Assert.AreEqual(0.5, VenueComfort.GlassComfortShare, Eps);
        }

        [Test]
        public void StoolsAndFittings_AddWhatTheySay()
        {
            Assert.AreEqual(2.50, VenueComfort.Base(0, 0, 2), Eps, "two extra stools, a quarter each");
            Assert.AreEqual(2.90, VenueComfort.Base(0.9, 0, 0), Eps, "three steel tables");
            Assert.AreEqual(2.0, VenueComfort.Base(0, 0, -3), Eps, "a bar cannot lose stools it never had");
        }

        [Test]
        public void Comfort_NeverPassesFiveStars()
        {
            Assert.AreEqual(5.0, VenueComfort.Base(4.0, 3.0, 2), Eps);
            Assert.AreEqual(VenueComfort.MaxComfort, BarRating.MaxStars, Eps);
        }

        [Test]
        public void TheRoom_RefusesNegativeContributions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VenueComfort.Base(-0.1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => VenueComfort.Base(0, -0.1, 0));
        }

        // ── what the mess costs ──────────────────────────────────────────────────

        [Test]
        public void ACleanNight_LosesNothing_AndAFilthyOne_LosesThePenalty()
        {
            Assert.AreEqual(2.0, VenueComfort.Tonight(2.0, 1.0), Eps);
            Assert.AreEqual(2.0 - VenueComfort.DirtPenalty, VenueComfort.Tonight(2.0, 0.0), Eps);
            Assert.AreEqual(3.0 - VenueComfort.DirtPenalty * 0.5, VenueComfort.Tonight(3.0, 0.5), Eps, "half clean, half the penalty");
            Assert.AreEqual(0.0, VenueComfort.Tonight(0.5, 0.0), Eps, "and it floors at zero");
        }

        [Test]
        public void TheLiveReading_DropsPerDirtySeat()
        {
            Assert.AreEqual(3.0, VenueComfort.Now(3.0, 0, 4), Eps);
            Assert.AreEqual(3.0 - VenueComfort.DirtPenalty * 0.25, VenueComfort.Now(3.0, 1, 4), Eps);
            Assert.AreEqual(3.0 - VenueComfort.DirtPenalty, VenueComfort.Now(3.0, 4, 4), Eps);
            Assert.AreEqual(3.0 - VenueComfort.DirtPenalty, VenueComfort.Now(3.0, 9, 4), Eps, "more dirt than seats is still all of it");
            Assert.AreEqual(3.0, VenueComfort.Now(3.0, 2, 0), Eps, "a room with no seats has nothing to dirty");
        }

        [Test]
        public void TheNightsStars_AreTheLowerOfTheTwo()
        {
            Assert.AreEqual(2.0, VenueComfort.NightStars(4.9, 2.0), Eps, "a perfect service in a two-star room");
            Assert.AreEqual(1.5, VenueComfort.NightStars(1.5, 4.0), Eps, "a poor service in a fine room");
            Assert.AreEqual(5.0, VenueComfort.NightStars(7.0, 6.0), Eps, "and never past five");
        }

        // ── the counter's night ──────────────────────────────────────────────────

        private static Housekeeping Counter() => new Housekeeping();

        [Test]
        public void ALeaver_LeavesAGlassAndAMark_AndTheGlassHoldsTheStool()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks");
            Assert.IsTrue(mess.HasGlass);
            Assert.IsTrue(mess.Smudged);
            Assert.AreEqual("rocks", mess.GlasswareId);
            Assert.AreEqual(1, house.GlassesOnCounter);
            Assert.AreEqual(1, house.MessesLeft);
            Assert.AreEqual(0, house.DirtySpots, "nothing counts inside the grace");
        }

        [Test]
        public void DirtInsideTheGrace_CostsNothing_AndPastIt_CostsEverySecond()
        {
            var house = Counter();
            house.LeaveMess("highball");
            house.Tick(Housekeeping.DirtGrace - 1.0);
            Assert.AreEqual(0.0, house.DirtSpotSeconds, Eps, "five seconds in, still free");
            Assert.AreEqual(0, house.DirtySpots);

            house.Tick(2.0);   // crosses the line one second in
            Assert.AreEqual(1.0, house.DirtSpotSeconds, Eps, "only the second past the grace is paid for");
            Assert.AreEqual(1, house.DirtySpots);

            house.Tick(3.0);
            Assert.AreEqual(4.0, house.DirtSpotSeconds, Eps);
        }

        [Test]
        public void OneBigTick_PaysExactlyWhatIsPastTheGrace()
        {
            var house = Counter();
            house.LeaveMess(null);
            house.Tick(10.0);
            Assert.AreEqual(10.0 - Housekeeping.DirtGrace, house.DirtSpotSeconds, Eps);
        }

        [Test]
        public void OneFilthyStoolAllNight_OnFourStools_IsThreeQuartersClean()
        {
            var house = Counter();
            house.LeaveMess("coupe");
            house.Tick(Housekeeping.DirtGrace + 95.0);      // 95 seat-seconds past the grace
            Assert.AreEqual(0.75, house.Cleanliness(seats: 4, elapsedSeconds: 95.0), Eps);
            Assert.AreEqual(1.0, house.Cleanliness(seats: 0, elapsedSeconds: 95.0), Eps, "no seats, nothing to dirty");
            Assert.AreEqual(1.0, Counter().Cleanliness(4, 95.0), Eps, "an untouched counter is clean");
        }

        [Test]
        public void Cleanliness_FloorsAtZero()
        {
            var house = Counter();
            for (int i = 0; i < 6; i++) house.LeaveMess("pint");
            house.Tick(1000.0);
            Assert.AreEqual(0.0, house.Cleanliness(4, 95.0), Eps);
        }

        [Test]
        public void YouCannotWipeUnderAGlass_CollectFirst()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks");
            Assert.Throws<InvalidOperationException>(() => house.Wipe(mess));

            house.CollectGlass(mess);
            Assert.IsFalse(mess.HasGlass);
            Assert.IsTrue(mess.Smudged, "the mark is still there");
            Assert.AreEqual(0, house.GlassesOnCounter, "the stool is free the instant the glass is lifted");
            Assert.AreEqual(1, house.GlassesInHand);
            Assert.AreEqual(1, house.Messes.Count, "the smudge keeps the mess on the list");

            house.Wipe(mess);
            Assert.IsTrue(mess.IsClean);
            Assert.AreEqual(0, house.Messes.Count, "and a clean spot is off the list");
            Assert.AreEqual(1, house.Wipes);
            Assert.AreEqual(1, house.GlassesCollected);
        }

        [Test]
        public void ASmudgeLeftBehind_StillCounts()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks");
            house.CollectGlass(mess);
            house.Tick(Housekeeping.DirtGrace + 4.0);
            Assert.AreEqual(4.0, house.DirtSpotSeconds, Eps, "a wet ring under no glass is still a wet ring");
            Assert.AreEqual(1, house.DirtySpots);
        }

        [Test]
        public void AMessWithNoMark_IsGoneWhenTheGlassIs()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks", smudge: false);
            house.CollectGlass(mess);
            Assert.IsTrue(mess.IsClean);
            Assert.AreEqual(0, house.Messes.Count);
        }

        [Test]
        public void TheVerbs_RefuseWhatIsNotThere()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks");
            house.CollectGlass(mess);
            Assert.Throws<InvalidOperationException>(() => house.CollectGlass(mess), "no glass twice");
            house.Wipe(mess);
            Assert.Throws<InvalidOperationException>(() => house.Wipe(mess), "the counter is clean here");
            Assert.Throws<InvalidOperationException>(() => house.CollectGlass(new Housekeeping().LeaveMess("x")),
                "not on this counter");
            Assert.Throws<ArgumentNullException>(() => house.Wipe(null));
        }

        [Test]
        public void TheSink_WantsAFullHand_AndRunsForTheStack()
        {
            var house = Counter();
            Assert.Throws<InvalidOperationException>(() => house.WashGlasses(), "nothing in your hands");

            var a = house.LeaveMess("rocks");
            var b = house.LeaveMess("coupe");
            house.CollectGlass(a);
            house.CollectGlass(b);
            double seconds = house.WashGlasses();
            Assert.AreEqual(Housekeeping.WashSecondsFor(2), seconds, Eps);
            Assert.AreEqual(2.5, seconds, Eps);
            Assert.IsTrue(house.SinkBusy);
            Assert.AreEqual(0, house.GlassesInHand, "the hand is empty from the moment the tap runs");
            Assert.AreEqual(2, house.GlassesWashing);

            var c = house.LeaveMess("pint");
            house.CollectGlass(c);
            Assert.Throws<InvalidOperationException>(() => house.WashGlasses(), "the sink is running");

            house.Tick(2.0);
            Assert.IsTrue(house.SinkBusy);
            house.Tick(0.5);
            Assert.IsFalse(house.SinkBusy);
            Assert.AreEqual(2, house.GlassesWashed);
            Assert.AreEqual(0, house.GlassesWashing);
            Assert.AreEqual(1, house.GlassesInHand, "the third is still waiting for the next wash");
        }

        [Test]
        public void GlassesInTheHand_DrainNothing()
        {
            var house = Counter();
            var mess = house.LeaveMess("rocks", smudge: false);
            house.CollectGlass(mess);
            house.Tick(60.0);
            Assert.AreEqual(0.0, house.DirtSpotSeconds, Eps, "off the counter is off the counter");
        }

        [Test]
        public void Closing_WashesTheHandForFree()
        {
            var house = Counter();
            var a = house.LeaveMess("rocks");
            var b = house.LeaveMess("coupe");
            house.CollectGlass(a);
            house.CollectGlass(b);
            house.WashGlasses();
            var c = house.LeaveMess("pint");
            house.CollectGlass(c);
            house.CloseNight();
            Assert.IsFalse(house.SinkBusy);
            Assert.AreEqual(0, house.GlassesInHand);
            Assert.AreEqual(3, house.GlassesWashed);
        }
    }
}
