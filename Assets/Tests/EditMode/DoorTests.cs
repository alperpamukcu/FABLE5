using System;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE DOOR HAS A LAW (GDD 28, 2026-09-04, the author: "20 yaş altı kişiler alkol
    /// alamayacak … sahte kimlik de işin içerisine eklenecek … oyundaki gelişmişlik seviyesine
    /// göre ceza belirlenecek … doğru şekilde kovması ise gün sonunda küçük bonus").
    ///
    /// The pure half (PLAN_house_and_law H2): what a card can be, how it is rolled, what a
    /// served minor costs. The verbs on the run — the kick, the fine when they get up, the
    /// thanks at close — are proved in H2b against TycoonRun.
    /// </summary>
    public sealed class DoorTests
    {
        [Test]
        public void NineteenIsAMinor_TwentyIsServed()
        {
            var nineteen = new IdPapers(19, 19, Forgery.None, looksYoung: true);
            var twenty = new IdPapers(20, 20, Forgery.None);
            Assert.IsTrue(nineteen.IsMinor);
            Assert.IsTrue(nineteen.ShouldBeKicked);
            Assert.IsFalse(twenty.IsMinor);
            Assert.IsFalse(twenty.ShouldBeKicked);
            Assert.IsTrue(twenty.IsHonestAdult);
            Assert.AreEqual(20, IdPapers.DrinkingAge);
        }

        [Test]
        public void ABorrowedCard_PrintsAnAdult_AndIsStillTheDoor()
        {
            var borrowed = new IdPapers(18, 24, Forgery.Borrowed, looksYoung: true);
            Assert.IsTrue(borrowed.IsForged);
            Assert.IsTrue(borrowed.IsMinor);
            Assert.IsTrue(borrowed.ShouldBeKicked);
            Assert.IsFalse(borrowed.IsHonestAdult);
            Assert.AreEqual(24, borrowed.PrintedAge);
        }

        [Test]
        public void ACard_CannotLieTheWrongWay()
        {
            // An honest card says the truth; a forged one is forged to LOOK of age; and a
            // minor always looks young — the face is the one fact the room may see.
            Assert.Throws<ArgumentException>(() => new IdPapers(19, 23, Forgery.None, looksYoung: true));
            Assert.Throws<ArgumentException>(() => new IdPapers(19, 19, Forgery.Borrowed, looksYoung: true));
            Assert.Throws<ArgumentException>(() => new IdPapers(19, 19, Forgery.None, looksYoung: false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdPapers(0, 20, Forgery.None));
        }

        [Test]
        public void OpeningNight_HasNoMinors_AndTheOddsClimbToOneInEight()
        {
            Assert.AreEqual(0.0, IdPapers.MinorChance(1), 1e-9);
            Assert.AreEqual(0.05, IdPapers.MinorChance(2), 1e-9);
            Assert.AreEqual(0.12, IdPapers.MinorChance(9), 1e-9);
            Assert.AreEqual(0.12, IdPapers.MinorChance(30), 1e-9, "it plateaus");
        }

        [Test]
        public void TheRoll_NeverMakesAMinor_OnOpeningNight()
        {
            var rng = new RunRng("opening").GetStream("papers");
            for (int i = 0; i < 500; i++)
            {
                var p = IdPapers.Roll(rng, 1, 34);
                Assert.IsNotNull(p, "everyone carries papers");
                Assert.IsTrue(p.IsHonestAdult);
                Assert.AreEqual(34, p.PrintedAge, "an honest adult's card says what the registry rolled");
            }
        }

        [Test]
        public void TheRoll_IsTheSameForTheSameSeed()
        {
            var a = new RunRng("door-seed").GetStream("papers");
            var b = new RunRng("door-seed").GetStream("papers");
            for (int i = 0; i < 300; i++)
            {
                var pa = IdPapers.Roll(a, 10, 40);
                var pb = IdPapers.Roll(b, 10, 40);
                Assert.AreEqual(pa.TrueAge, pb.TrueAge, $"draw {i}");
                Assert.AreEqual(pa.PrintedAge, pb.PrintedAge);
                Assert.AreEqual(pa.Forgery, pb.Forgery);
                Assert.AreEqual(pa.LooksYoung, pb.LooksYoung);
            }
        }

        [Test]
        public void TheRoll_LandsNearItsOdds_AndSplitsTheForgeries()
        {
            var rng = new RunRng("odds").GetStream("papers");
            int minors = 0, forged = 0, altered = 0, youngAdults = 0, adults = 0;
            const int draws = 4000;
            for (int i = 0; i < draws; i++)
            {
                var p = IdPapers.Roll(rng, 30, 45);
                if (p.IsHonestAdult)
                {
                    adults++;
                    if (p.LooksYoung) youngAdults++;
                    continue;
                }
                minors++;
                Assert.IsTrue(p.IsMinor, "every roll that is not an honest adult is a minor");
                Assert.IsTrue(p.LooksYoung, "and every minor looks young");
                Assert.IsTrue(p.TrueAge == 18 || p.TrueAge == 19);
                if (p.IsForged)
                {
                    forged++;
                    Assert.GreaterOrEqual(p.PrintedAge, IdPapers.DrinkingAge + 1, "a forged card prints of age");
                    if (p.Forgery == Forgery.Altered)
                    {
                        altered++;
                        Assert.LessOrEqual(p.PrintedAge, IdPapers.DrinkingAge + 4, "the year is bumped, not invented");
                    }
                    else Assert.AreEqual(Forgery.Borrowed, p.Forgery);
                }
                else Assert.AreEqual(p.TrueAge, p.PrintedAge);
            }
            double share = minors / (double)draws;
            Assert.That(share, Is.InRange(0.09, 0.15), $"minors {share:P1} of {draws} at 12% odds");
            double split = forged / (double)Math.Max(1, minors);
            Assert.That(split, Is.InRange(0.38, 0.62), $"forged {split:P0} of the minors at a 50% share");
            double kinds = altered / (double)Math.Max(1, forged);
            Assert.That(kinds, Is.InRange(0.38, 0.62), $"altered {kinds:P0} of the forged at a 50% share (H6)");
            double young = youngAdults / (double)Math.Max(1, adults);
            Assert.That(young, Is.InRange(0.18, 0.32), $"young adults {young:P0} at a 25% share — the face is not the tell");
        }

        [Test]
        public void TheRoll_TouchesNoOtherStream()
        {
            // Two runs on one seed: one rolls a hundred papers, the other never asks. Every
            // other stream must read the same — the whole point of a named stream.
            var quiet = new RunRng("streams");
            var busy = new RunRng("streams");
            var busyPapers = busy.GetStream("papers");
            for (int i = 0; i < 100; i++) IdPapers.Roll(busyPapers, 20, 30);
            foreach (var name in new[] { "arrivals", "orders", "patience", "customer", "read", "decide" })
            {
                var q = quiet.GetStream(name);
                var b = busy.GetStream(name);
                for (int i = 0; i < 20; i++) Assert.AreEqual(q.NextUInt(), b.NextUInt(), name);
            }
        }

        [Test]
        public void TheRoll_RefusesAMinorFromTheRegistry()
        {
            // The registry rolls adults; who is under twenty is this stream's question alone.
            var rng = new RunRng("x").GetStream("papers");
            Assert.Throws<ArgumentOutOfRangeException>(() => IdPapers.Roll(rng, 5, 19));
        }

        [Test]
        public void TheFine_ClimbsWithTheStanding()
        {
            Assert.AreEqual(20, IdPapers.FineFor(0.0));
            Assert.AreEqual(20, IdPapers.FineFor(0.9), "whole stars only");
            Assert.AreEqual(40, IdPapers.FineFor(1.0));
            Assert.AreEqual(60, IdPapers.FineFor(2.5));
            Assert.AreEqual(100, IdPapers.FineFor(4.0));
            Assert.AreEqual(120, IdPapers.FineFor(5.0));
            Assert.AreEqual(120, IdPapers.FineFor(7.0), "and no further");
            Assert.AreEqual(20, IdPapers.FineFor(-1.0));
            Assert.AreEqual(5, IdPapers.KickBonus, "a well drink, not a round");
        }

        [Test]
        public void APerson_StartsUnasked_AndUnbarred()
        {
            // The card is rolled by the run at first arrival (H2b, TycoonRun.NextArrival) and
            // set ONCE through a Core-only door; until then nothing about the face says young,
            // and nobody is barred. The once-only rule and the bar are pinned through the
            // run's own verbs in H2b.
            var person = new RegularState("p1", "Dev", "after_shift", 34, "Chicago");
            Assert.IsFalse(person.LooksYoung);
            Assert.IsFalse(person.Barred);
        }
    }
}
