using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The corners the standing audit called uncovered (GELISTIRME_RAPORU §4, 2026-09-06):
    /// GOLDEN VECTORS for the seeded RNG and for a seeded night, so a platform that drifts
    /// fails loudly instead of quietly dealing a different bar; the CULTURE PIN, so a report
    /// generated on a Turkish desktop is byte-identical to one generated anywhere; the
    /// relationship thresholds; and the ladder's "one rung ahead" rule read off the real
    /// catalogue — the rule the market's aisle relies on and only the HUD used to carry.
    /// </summary>
    public class CoreCornersTests
    {
        // ── determinism: golden vectors ────────────────────────────────────────────

        /// <summary>
        /// Six draws of NextInt(1000) on three named streams, three seeds, computed on this
        /// machine on 2026-09-06 and pinned. The self-consistency tests in DeckAndRngTests
        /// prove a seed repeats ITSELF; this proves the numbers are the ones every other
        /// machine gets — a string seed must deal the same bar on every platform (CLAUDE.md),
        /// and the only way to know that is to write the deal down.
        /// </summary>
        private static readonly (string seed, string stream, int[] draws)[] Golden =
        {
            ("GOLDEN-A", "arrivals", new[] { 513, 522, 124, 715, 636, 374 }),
            ("GOLDEN-A", "orders", new[] { 795, 437, 83, 931, 673, 866 }),
            ("GOLDEN-A", "patience", new[] { 125, 845, 526, 128, 187, 405 }),
            ("golden-a", "arrivals", new[] { 466, 620, 760, 285, 254, 173 }),   // case is part of the seed
            ("golden-a", "orders", new[] { 277, 765, 442, 865, 197, 657 }),
            ("golden-a", "patience", new[] { 470, 411, 141, 705, 301, 107 }),
            ("LASTCALL-DEV", "arrivals", new[] { 998, 520, 632, 442, 953, 255 }),
            ("LASTCALL-DEV", "orders", new[] { 760, 93, 537, 475, 782, 685 }),
            ("LASTCALL-DEV", "patience", new[] { 748, 799, 802, 845, 549, 333 }),
        };

        [Test]
        public void The_seeded_rng_deals_the_numbers_it_dealt_on_2026_09_06()
        {
            foreach (var (seed, stream, draws) in Golden)
            {
                var s = new RunRng(seed).GetStream(stream);
                var actual = Enumerable.Range(0, draws.Length).Select(_ => s.NextInt(1000)).ToArray();
                Assert.AreEqual(draws, actual,
                    $"seed '{seed}', stream '{stream}' dealt [{string.Join(",", actual)}] — "
                    + "a platform drifted, or the PCG32 was touched");
            }
        }

        [Test]
        public void The_seeded_rng_deals_the_same_double_it_dealt_on_2026_09_06()
        {
            // NextDouble is NextUInt / 2^32, so this pins the raw word as well as the int path.
            var d = new RunRng("GOLDEN-A").GetStream("decide").NextDouble();
            Assert.AreEqual(0.71039469027891755, d, 1e-15);
            var e = new RunRng("LASTCALL-DEV").GetStream("decide").NextDouble();
            Assert.AreEqual(0.46022160560823977, e, 1e-15);
        }

        // ── a seeded night, dealt the same way ────────────────────────────────────

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

        /// <summary>
        /// The first six people through the door on seed GOLDEN-RUN, night one, default
        /// config: WHEN they sat (bar seconds, half-second ticks) and the patience each was
        /// rolled — the arrivals and patience streams, read through the run the way the
        /// game reads them. Pinned 2026-09-06. A change here is either a platform drifting
        /// or somebody adding a draw to the roll order, and both deserve a red.
        /// </summary>
        private static readonly string GoldenNight =
            "9.5@50.30 18.5@45.83 30.5@47.14 67.5@51.43 78.0@48.80 90.5@45.27";

        [Test]
        public void A_seeded_night_seats_the_same_people_at_the_same_seconds()
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng("GOLDEN-RUN"),
                config: new TycoonConfig(500));
            var dealt = new List<string>();
            int guard = 0;
            while (dealt.Count < 6 && run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 2000, "six people never came");
                foreach (var v in run.Tick(0.5))
                    dealt.Add(run.Floor.Elapsed.ToString("0.0", CultureInfo.InvariantCulture)
                              + "@" + v.PatienceMax.ToString("0.00", CultureInfo.InvariantCulture));
            }
            string actual = string.Join(" ", dealt);
            Assert.AreEqual(GoldenNight, actual,
                "the night was dealt differently: " + actual);
        }

        // ── the culture pin ────────────────────────────────────────────────────────

        [Test]
        public void The_run_culture_writes_a_dot_and_a_percent_the_way_the_screens_expect()
        {
            var c = RunCulture.Culture;
            Assert.AreEqual("3.0", 3.0.ToString("0.0", c), "the standing prints with a dot");
            Assert.AreEqual("11.6", 11.6.ToString("0.0", c), "the sim's tables too");
            Assert.AreEqual("75%", 0.75.ToString("P0", c), "a percent is the number then the sign, no space");
            Assert.AreEqual("-5%", (-0.05).ToString("P0", c));
        }

        [Test]
        public void Pinning_the_culture_pins_the_thread_it_runs_on()
        {
            var before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                Assert.AreEqual("11,6", 11.6.ToString("0.0"), "the Turkish desktop the pin exists for");
                RunCulture.Pin();
                Assert.AreEqual("11.6", 11.6.ToString("0.0"), "pinned: the desktop no longer decides");
                Assert.AreSame(RunCulture.Culture, CultureInfo.CurrentCulture);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        // ── relationships ──────────────────────────────────────────────────────────

        [Test]
        public void A_relationship_is_earned_at_one_three_and_six_satisfied_visits()
        {
            Assert.AreEqual(Relationship.Stranger, Relationships.ForSatisfiedVisits(0));
            Assert.AreEqual(Relationship.Familiar, Relationships.ForSatisfiedVisits(1));
            Assert.AreEqual(Relationship.Familiar, Relationships.ForSatisfiedVisits(2));
            Assert.AreEqual(Relationship.Regular, Relationships.ForSatisfiedVisits(3));
            Assert.AreEqual(Relationship.Regular, Relationships.ForSatisfiedVisits(5));
            Assert.AreEqual(Relationship.Confidant, Relationships.ForSatisfiedVisits(6));
            Assert.AreEqual(Relationship.Confidant, Relationships.ForSatisfiedVisits(40));
        }

        // ── the ladder, one rung ahead, on the real catalogue ─────────────────────

        [Test]
        public void Every_ladder_in_the_catalogue_sells_exactly_the_next_rung()
        {
            // The market shows a ladder's owned rung's successor and nothing above it
            // (GDD 27 §3.2, 2026-08-26). The HUD hides rung N+2 itself; what Core promises,
            // and what this pins on the shipped file, is that at every point on every ladder
            // exactly one rung is buyable and it is the one right above what the bar owns.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var run = new TycoonRun(NewShelf(), Book, new RunRng("ladders"),
                config: new TycoonConfig(500), fixtures: loaded.Fixtures);

            var slots = loaded.Fixtures.Where(f => f.Level > 0).Select(f => f.Slot).Distinct().ToList();
            Assert.That(slots, Is.Not.Empty, "the catalogue has no ladders");
            foreach (var slot in slots)
            {
                var rungs = loaded.Fixtures.Where(f => f.Slot == slot).OrderBy(f => f.Level).ToList();
                for (int climbed = run.LadderLevel(slot); climbed < rungs.Count; climbed++)
                {
                    var buyable = rungs.Where(r => run.CanBuyRung(r)).ToList();
                    Assert.AreEqual(1, buyable.Count,
                        $"{slot} at mark {run.LadderLevel(slot)}: {buyable.Count} rungs buyable");
                    Assert.AreEqual(run.LadderLevel(slot) + 1, buyable[0].Level,
                        $"{slot}: the rung on sale is not the next one");
                    run.DevFit(buyable[0].Id);
                }
                Assert.AreEqual(rungs.Count, run.LadderLevel(slot), slot + " climbed to its top");
                Assert.IsFalse(rungs.Any(r => run.CanBuyRung(r)), slot + " at the top sells nothing");
            }
        }
    }
}
