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
    /// THE HOUSE SUMS TO FIVE (2026-09-26, the author: "Tüm geliştirmeleri ekonomi dengesine dahil et, Oyuncu
    /// oyunda maksimum 5 konfora ulaşmalı ve bu oyun sonlarına yakın gerçekleşmeli. Şu an sanki tüm gelişmeler
    /// +5 oluyormuş gibi gözüküyor.").
    ///
    /// The shipped room summed to eighteen against a ceiling of five: the meter read 5/5 by night fourteen with
    /// the standing under two stars, every preset from three stars up opened full, and the market printed each
    /// rung's absolute comfort as if it were added. These hold the shipped data to the author's word — every
    /// fitting, glass step, stool and bar-top step together is exactly five — and hold the shape that keeps
    /// the game playable: each gate opens more room than the next gate needs, the walls carry the most, every
    /// piece adds a little, and the projection that buys the room lands five near the end of the climb.
    /// </summary>
    public sealed class ComfortEconomyTests
    {
        private const double Eps = 1e-9;

        private static string Read(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        private static IReadOnlyList<FixtureDefinition> Fixtures() =>
            DataLoader.ParseFixtures(Read("fixtures/fixtures.json")).Fixtures;

        private static IReadOnlyList<GlasswareDefinition> Glassware() =>
            DataLoader.ParseGlassware(Read("glassware/glassware.json"));

        /// <summary>What the room is worth with every piece whose gate is at or under <paramref name="stars"/>
        /// climbed: the top open rung of every ladder and every open single — the room's pieces only.</summary>
        private static double FixturesOpenAt(IReadOnlyList<FixtureDefinition> fixtures, double stars)
        {
            var top = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
            double singles = 0;
            foreach (var f in fixtures)
            {
                if (f.Stars > stars + Eps) continue;
                if (f.Level <= 0) { singles += f.Comfort; continue; }
                if (!top.TryGetValue(f.Slot, out var held) || f.Level > held.Level) top[f.Slot] = f;
            }
            return singles + top.Values.Sum(f => f.Comfort);
        }

        /// <summary>The comfort nothing gates: every glass step (BuyGlassTier asks no star), both stools and
        /// both bar-top steps — one a night, but open from the first.</summary>
        private static double Ungated(IReadOnlyList<GlasswareDefinition> glassware, TycoonConfig config) =>
            glassware.Sum(g => g.TierComfort.Sum())
            + VenueComfort.StoolComfort * (config.MaxSeats - config.StartingSeats)
            + VenueComfort.CounterComfort * (config.MaxAmbienceTier - 1);

        // ── the data ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ShippedHouse_SumsToExactlyFive()
        {
            var config = TycoonConfig.Default;
            double fixtures = FixturesOpenAt(Fixtures(), double.MaxValue);
            double glass = Glassware().Sum(g => g.TierComfort.Sum());
            Assert.AreEqual(4.30, fixtures, Eps, "every fitting's top rung and every single piece");
            Assert.AreEqual(0.50, glass, Eps, "every step of every glass line");
            Assert.AreEqual(5.00, fixtures + Ungated(Glassware(), config), Eps,
                "the whole house — fittings, glass, both stools, both bar-top steps — is exactly five");
            Assert.AreEqual(VenueComfort.MaxComfort, 5.0, Eps);
        }

        [Test]
        public void ShippedHouse_OpensAheadOfTheStanding()
        {
            // The standing can never pass the room (the night files the lower), so a gate that opened less room
            // than the next gate needs would deadlock the climb under it. Every gate opens at least the next
            // gate plus half a star — counting the glass, the stools and the bar top, which no star gates (the
            // critic's 3.5-star case: the fittings alone open 4.25 there, 4.95 with them) — and the last gate
            // opens the whole house.
            var fixtures = Fixtures();
            var config = TycoonConfig.Default;
            double ungated = Ungated(Glassware(), config);
            var gates = fixtures.Where(f => !f.StartsInTheRoom).Select(f => f.Stars).Distinct().OrderBy(g => g).ToList();
            Assert.Greater(gates.Count, 3);
            for (int i = 0; i < gates.Count; i++)
            {
                double next = i + 1 < gates.Count ? gates[i + 1] : BarRating.MaxStars;
                double open = FixturesOpenAt(fixtures, gates[i]) + ungated;
                Assert.GreaterOrEqual(open + Eps, Math.Min(BarRating.MaxStars, next + 0.5),
                    $"at {gates[i]:0.0} stars the room can be worth {open:0.00}; the next gate ({next:0.0}) wants more");
            }
            Assert.AreEqual(5.0, FixturesOpenAt(fixtures, gates[gates.Count - 1]) + ungated, Eps,
                "everything is open at the last gate");
        }

        [Test]
        public void ShippedWalls_CarryTheMostComfort()
        {
            // GDD 27 §3.1 (2026-09-06, the author: "duvar geliştirmeleri konforu en çok arttıran geliştirmeler
            // olmalı"): the back wall's top rung is worth more than any other ladder's top rung.
            var fixtures = Fixtures();
            double walls = fixtures.Where(f => f.Slot == "walls").Max(f => f.Comfort);
            foreach (var slot in fixtures.Where(f => f.Slot != "walls").Select(f => f.Slot).Distinct())
                Assert.Greater(walls, fixtures.Where(f => f.Slot == slot).Max(f => f.Comfort), slot);
        }

        [Test]
        public void EveryBoughtPiece_AddsASmallAmount()
        {
            // "Şu an sanki tüm gelişmeler +5 oluyormuş gibi gözüküyor": no single purchase is worth more than a
            // fifth of a star, every one is worth something, and a ladder only ever climbs.
            var fixtures = Fixtures();
            foreach (var f in fixtures)
            {
                if (f.StartsInTheRoom || f.IsTap) continue;
                double gain = TycoonRun.ComfortGainIn(f, fixtures);
                Assert.That(gain, Is.GreaterThan(0).And.LessThanOrEqualTo(0.20 + Eps), f.Id + " adds " + gain);
            }
            foreach (var g in Glassware())
                foreach (var step in g.TierComfort)
                    Assert.That(step, Is.GreaterThan(0).And.LessThanOrEqualTo(0.20 + Eps), g.Id);
            Assert.That(VenueComfort.StoolComfort, Is.GreaterThan(0).And.LessThanOrEqualTo(0.20));
            Assert.That(VenueComfort.CounterComfort, Is.GreaterThan(0).And.LessThanOrEqualTo(0.20));
        }

        [Test]
        public void ComfortGain_IsTheIncrementOverTheRungBelow()
        {
            var run = OpeningBar.Run("gain");
            var walls = run.FixtureCatalogue.Where(f => f.Slot == "walls").OrderBy(f => f.Level).ToList();
            Assert.AreEqual(0.0, run.ComfortGain(walls[0]), Eps, "the cracked plaster the room opens with adds nothing");
            Assert.AreEqual(walls[1].Comfort, run.ComfortGain(walls[1]), Eps, "the first bought rung adds all it is worth");
            for (int i = 2; i < walls.Count; i++)
                Assert.AreEqual(walls[i].Comfort - walls[i - 1].Comfort, run.ComfortGain(walls[i]), Eps,
                    walls[i].Id + " adds what it is worth over the rung under it");
            var kit = run.FixtureById(TycoonRun.CounterPaintFixture);
            Assert.AreEqual(kit.Comfort, run.ComfortGain(kit), Eps, "a single piece adds its own comfort");
            Assert.AreEqual(walls[walls.Count - 1].Comfort, run.LadderComfort("walls"), Eps,
                "and the ladder's top is what the ladder head prints the climb against");
            Assert.AreEqual(0.0, run.ComfortGain(null), Eps);
        }

        // ── the glass ─────────────────────────────────────────────────────────────────────

        [Test]
        public void TheGlassLoader_RefusesABadTierComfort()
        {
            const string head = "{\"version\":1,\"glasses\":[{\"id\":\"x\",\"name\":\"X\",\"capacity\":1.0," +
                                "\"profile\":[0.9,1.0],\"tierPrices\":[10,20,30,40,50]";
            Assert.DoesNotThrow(() => DataLoader.ParseGlassware(head + ",\"tierComfort\":[0.02,0.02,0.02,0.02,0.02]}]}"));
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(head + "}]}"),
                "a line that forgot its comfort would be worth nothing to the room without a word");
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(head + ",\"tierComfort\":[0.02,0.02,0.02,0.02]}]}"),
                "four figures for five steps");
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(head + ",\"tierComfort\":[0.02,0.02,0.02,0.02,0.02,0.02]}]}"),
                "six figures for five steps");
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(head + ",\"tierComfort\":[0.02,-0.01,0.02,0.02,0.02]}]}"),
                "a step that takes comfort away");
            Assert.Throws<FormatException>(() => DataLoader.ParseGlassware(head + ",\"tierComfort\":[0.02,0.02,0.02,0.02,3.0]}]}"),
                "a step worth more than a glass step can be");

            // The constructor keeps its old callers: no comfort given is a line worth nothing to the room.
            var bare = new GlasswareDefinition("y", "Y", new[] { 1.0, 1.0 }, new[] { 10, 20, 30, 40, 50 }, 1.0);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0, 0.0, 0.0, 0.0 }, bare.TierComfort);
            Assert.Throws<ArgumentException>(() => new GlasswareDefinition("z", "Z", new[] { 1.0, 1.0 },
                new[] { 10, 20, 30, 40, 50 }, 1.0, new[] { 0.1, 0.1 }));
        }

        [Test]
        public void TheRoom_CountsTheBarTopAndTheGlassStep_ItBought()
        {
            var counter = OpeningBar.Run("counter-comfort", new TycoonConfig(startingMoney: 900));
            counter.DevSkipToDayEnd();
            double before = counter.ComfortBase;
            counter.BuyCounter();
            Assert.AreEqual(before + VenueComfort.CounterComfort, counter.ComfortBase, Eps, "a bar-top step is part of the room");

            var glass = OpeningBar.Run("glass-comfort", new TycoonConfig(startingMoney: 900));
            glass.DevSkipToDayEnd();
            before = glass.ComfortBase;
            var line = glass.Glassware[0];
            glass.BuyGlassTier(line.Id);
            Assert.AreEqual(before + line.TierComfort[0], glass.ComfortBase, Eps, "a glass step adds its line's own figure");
            Assert.AreEqual(line.TierComfort[0], glass.GlassComfort, Eps);
        }

        // ── the presets ───────────────────────────────────────────────────────────────────

        [Test]
        public void EveryDevPreset_ReadsUnderFiveUntilTheTop()
        {
            // Every preset from three stars up used to open with the meter full: the rung caps that tried to keep
            // it under were for a house of eighteen. With the house at five the preset buys everything its gate
            // opens, its room never falls as the stars rise, stays under five below the top, and is exactly five
            // only there — and the 0-star preset is the bar as it opens, no decor bought.
            double last = -1;
            foreach (double stars in new[] { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 })
            {
                var run = OpeningBar.Run("preset-comfort-" + stars);
                run.DevPresetStars(stars);
                double comfort = run.ComfortBase;
                Assert.GreaterOrEqual(comfort + Eps, last, stars + " stars reads less room than the preset under it");
                if (stars < BarRating.MaxStars) Assert.Less(comfort, VenueComfort.MaxComfort - 0.1, stars + " stars opens full");
                else Assert.AreEqual(VenueComfort.MaxComfort, comfort, Eps, "the five-star preset is the whole house");
                if (stars == 0.0)
                    foreach (var f in run.FixtureCatalogue)
                        if (run.OwnsFixture(f.Id) && !f.StartsInTheRoom)
                            Assert.IsTrue(f.IsTap || f.IsDrain || f.Slot == "shaker", "the 0-star preset bought " + f.Id);
                last = comfort;
            }
            Assert.Greater(last, 4.9);

            var mid = OpeningBar.Run("preset-mid");
            mid.DevPreset(1);
            Assert.Less(mid.ComfortBase, VenueComfort.MaxComfort - 0.5, "the mid preset reads as a room worked on");
            var late = OpeningBar.Run("preset-late");
            late.DevPreset(2);
            Assert.AreEqual(VenueComfort.MaxComfort, late.ComfortBase, Eps, "the endgame preset is the whole house");
        }

        // ── the projection that buys the room ─────────────────────────────────────────────

        private static List<EconomyProjection.RoomNight> Furnish(double quality, int nights = 120)
        {
            var deck = OpeningBar.Deck();
            var catalogue = deck.Cards.Where(c => !deck.IsStarting(c)).Concat(deck.LockedCards).ToList();
            return EconomyProjection.WalkFurnishing(DataLoader.ParseRecipes(Read("recipes/recipes.json")),
                TycoonConfig.Default, Fixtures(), Glassware(), nights, quality, deck.StartingCards, catalogue);
        }

        [Test]
        public void TheFurnishingWalk_LandsFiveComfortNearTheEnd()
        {
            // The walk buys its bottles, its pages and its room out of its own till; the climb on the service
            // side is the projection's assumption (EconomyProjection.Climb), so no five-star NIGHT is pinned here —
            // what is pinned is the night the money buys the whole house, and its shape.
            var sharp = Furnish(EconomyProjection.Sharp);
            var competent = Furnish(EconomyProjection.Competent);
            var learning = Furnish(EconomyProjection.Learning);

            var again = Furnish(EconomyProjection.Competent);
            Assert.AreEqual(competent.Count, again.Count);
            for (int i = 0; i < competent.Count; i++)
                Assert.AreEqual(competent[i], again[i], "night " + competent[i].Night.Day + " is not deterministic");

            foreach (var walk in new[] { sharp, competent, learning })
                foreach (var n in walk)
                {
                    Assert.LessOrEqual(n.Comfort, VenueComfort.MaxComfort + Eps);
                    Assert.LessOrEqual(n.ComfortAfter, VenueComfort.MaxComfort + Eps);
                    Assert.LessOrEqual(n.Standing, n.Comfort + Eps, "the standing never passes the room it was filed in");
                }

            int sharpFive = EconomyProjection.ComfortFiveNight(sharp);
            int competentFive = EconomyProjection.ComfortFiveNight(competent);
            int learningFive = EconomyProjection.ComfortFiveNight(learning);
            // Measured 2026-09-26 on the shipped data: sharp night 52, competent night 75; a player still
            // learning never buys the house inside 120 nights (it stalls at one star — see ECONOMY's room table).
            Assert.That(sharpFive, Is.InRange(46, 60), "the house is five for a sharp bar near the end of its climb");
            Assert.That(competentFive, Is.InRange(66, 86), "and for a competent one later");
            Assert.Less(sharpFive, competentFive, "a sharper bar furnishes sooner");
            Assert.IsTrue(learningFive == 0 || learningFive > competentFive,
                "and a bar still learning never before a competent one");

            // THE ROOM IS NOT FULL IN THE FIRST FORTNIGHT: the complaint this answers.
            Assert.Less(competent[13].ComfortAfter, 2.5, "a competent room on night fourteen is not the endgame's");
            Assert.Less(sharp[13].ComfortAfter, 3.0, "nor a sharp one's");
        }
    }
}
