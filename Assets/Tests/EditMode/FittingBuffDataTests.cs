using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE ROOM'S BUFFS AS SHIPPED (2026-09-23). FittingBuffTests pins the rules on a synthetic room;
    /// these pin the CONTENT — every row of fixtures.json names its slot's kind, the room as it opens
    /// is the house standard, the full room lands exactly on every cap, and the book prints what the
    /// drinker pays under the real neon — and the loader's refusals in its own words.
    ///
    /// The slot→kind map is pinned as a dictionary on purpose: moving a shelf's stat is a design
    /// decision, and it should take a loud, deliberate edit here to make one.
    /// </summary>
    public sealed class FittingBuffDataTests
    {
        private static LoadedFixtures Shipped() =>
            DataLoader.ParseFixtures(System.IO.File.ReadAllText(
                UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json"));

        private static readonly Dictionary<string, string> KindOfSlot = new Dictionary<string, string>
        {
            ["walls"] = "arrivals",
            ["walls_right"] = "comfort",
            ["ceiling"] = "refill",
            ["floor"] = "grace",
            ["floor_rug"] = "patience",
            ["plant_left"] = "patience",
            ["plant_right"] = "patience",
            ["wall_tv"] = "lateness",
            ["wall_right_art"] = "late_tip",
            ["wall_center"] = "service",
            ["wall_right"] = "price",
            ["wall_lamps"] = "tip",
            ["counter_lamps"] = "window",
            ["table_left"] = "round",
            ["table_right"] = "round",
            ["counter_paint"] = "grace",
            ["beer_mat"] = "grace",
            ["prep_mat"] = "grace",
            ["taps"] = "pour_speed",
            ["sink"] = "wash",
            ["shaker"] = "shake_speed",
        };

        private static Shelf PlainShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 40),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 40),
        });

        private static TycoonConfig Cfg() => new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0);

        private static readonly IReadOnlyList<RecipeDefinition> OnePage = new[]
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

        private static CustomerVisit FirstArrival(TycoonRun run)
        {
            for (int guard = 0; guard < 4000; guard++)
            {
                var sat = run.Floor.Seated.FirstOrDefault(v => v.State == VisitState.Waiting && v.HasOrdered);
                if (sat != null) return sat;
                run.Tick(0.05);
            }
            Assert.Fail("nobody came in");
            return null;
        }

        // ── the loader's refusals ────────────────────────────────────────────────────────────

        private const string Slots = @"""slots"": [
            { ""id"": ""s1"", ""x"": 0, ""y"": 0 },
            { ""id"": ""s2"", ""x"": 10, ""y"": 0 },
            { ""id"": ""taps"", ""x"": 20, ""y"": 0, ""onCounter"": true }],";

        private static void Refused(string why, string fixtures, string names)
        {
            var e = Assert.Throws<FormatException>(() => DataLoader.ParseFixtures("{" + Slots + @" ""fixtures"": [" + fixtures + "] }"), why);
            StringAssert.Contains("'" + names + "'", e.Message, why + ": the message names it");
        }

        private static string Row(string id, string slot, string extra) =>
            @"{ ""id"": """ + id + @""", ""name"": ""X"", ""slot"": """ + slot + @""", ""price"": 20, ""sprite"": ""fx"" "
            + (string.IsNullOrEmpty(extra) ? "" : ", " + extra) + " }";

        [Test]
        public void TheLoader_RefusesBadBuffs()
        {
            Refused("an unknown kind", Row("bad", "s1", @"""buff"": ""nope"", ""buffPct"": 5"), "bad");
            Refused("a figure without a kind", Row("bad", "s1", @"""buffPct"": 5"), "bad");
            Refused("a tool kind with a figure", Row("bad", "s1", @"""buff"": ""shake_speed"", ""buffPct"": 10"), "bad");
            Refused("free drain named", Row("bad", "s1", @"""drain"": true, ""buff"": ""free_drain"""), "bad");
            Refused("pour speed off a tower", Row("bad", "s1", @"""buff"": ""pour_speed"""), "bad");
            Refused("wash off a drain", Row("bad", "s1", @"""buff"": ""wash"""), "bad");
            Refused("shake speed on a drain", Row("bad", "s1", @"""drain"": true, ""buff"": ""shake_speed"""), "bad");
            Refused("an opening piece with a figure",
                Row("bad", "s1", @"""startsInTheRoom"": true, ""buff"": ""tip"", ""buffPct"": 2"), "bad");
            Refused("a bought data piece at 0", Row("bad", "s1", @"""buff"": ""tip"", ""buffPct"": 0"), "bad");
            Refused("a figure pointing the wrong way", Row("bad", "s1", @"""buff"": ""lateness"", ""buffPct"": 5"), "bad");
            Refused("a figure over MaxPerPiece", Row("bad", "s1", @"""buff"": ""price"", ""buffPct"": 13"), "bad");
            Refused("two kinds in one slot",
                Row("a", "s1", @"""level"": 1, ""buff"": ""tip"", ""buffPct"": 2") + ", " +
                Row("b", "s1", @"""level"": 2, ""buff"": ""price"", ""buffPct"": 4"), "s1");
            Refused("a ladder whose figure does not rise",
                Row("a", "s1", @"""level"": 1, ""buff"": ""tip"", ""buffPct"": 4") + ", " +
                Row("b", "s1", @"""level"": 2, ""buff"": ""tip"", ""buffPct"": 4"), "s1");

            // ...and what it accepts, on the same hooks.
            var fine = DataLoader.ParseFixtures("{" + Slots + @" ""fixtures"": [" +
                Row("a", "s1", @"""level"": 1, ""startsInTheRoom"": true, ""buff"": ""tip"", ""buffPct"": 0") + ", " +
                Row("b", "s1", @"""level"": 2, ""buff"": ""tip"", ""buffPct"": 4") + ", " +
                Row("t", "taps", @"""tapLevel"": 1, ""startsInTheRoom"": true, ""buff"": ""pour_speed""") + "] }");
            Assert.AreSame(FittingBuffs.Tip, fine.Fixtures[1].Buff);
            Assert.AreEqual(4, fine.Fixtures[1].BuffPct);
            Assert.AreSame(FittingBuffs.PourSpeed, fine.Fixtures[2].Buff);
        }

        [Test]
        public void TheLoader_AcceptsRowsWithoutABuff()
        {
            var old = DataLoader.ParseFixtures("{" + Slots + @" ""fixtures"": [" +
                Row("fern", "s1", "") + ", " + Row("lamp", "s2", @"""level"": 1") + "] }");
            foreach (var f in old.Fixtures)
            {
                Assert.IsNull(f.Buff, f.Id + ": an older row names no buff");
                Assert.IsFalse(f.HasBuff);
                Assert.AreEqual(0, f.BuffPct);
                Assert.AreEqual(0, FittingBuffs.EffectsOf(f, old.Fixtures).Count, f.Id + " has no effects");
            }
        }

        // ── the shipped data ─────────────────────────────────────────────────────────────────

        [Test]
        public void ShippedFixtures_EveryRowNamesItsKind_OneKindPerSlot()
        {
            var catalogue = Shipped().Fixtures;
            Assert.AreEqual(88, catalogue.Count, "the catalogue this was written against; a new row names its kind too");
            var seen = new Dictionary<string, string>();
            foreach (var f in catalogue)
            {
                Assert.IsTrue(f.HasBuff, f.Id + " names no buff");
                Assert.IsTrue(KindOfSlot.TryGetValue(f.Slot, out var kind), f.Id + " stands in unpinned slot " + f.Slot);
                Assert.AreEqual(kind, f.Buff.Id, f.Id + ": slot " + f.Slot + " carries " + kind);
                seen[f.Slot] = f.Buff.Id;
                if (f.StartsInTheRoom || f.Buff.IsTool)
                    Assert.AreEqual(0, f.BuffPct, f.Id + ": the opening room and the tools carry 0");
                else
                    Assert.AreNotEqual(0, f.BuffPct, f.Id + ": a bought piece buys something");
            }
            CollectionAssert.AreEquivalent(KindOfSlot.Keys, seen.Keys, "every pinned slot is in the room");

            foreach (var slot in catalogue.Where(f => f.Level > 0).Select(f => f.Slot).Distinct())
            {
                var rungs = catalogue.Where(f => f.Slot == slot).OrderBy(f => f.Level).ToList();
                var figures = rungs.Select(f => Math.Abs(FittingBuffs.EffectsOf(f, catalogue)[0].Percent)).ToList();
                for (int i = 1; i < figures.Count; i++)
                    Assert.Greater(figures[i], figures[i - 1],
                        $"{slot}: {rungs[i].Id} must carry more than {rungs[i - 1].Id} ({string.Join(", ", figures)})");
            }
        }

        [Test]
        public void ShippedFixtures_TheFullRoomLandsExactlyOnEveryCap()
        {
            var catalogue = Shipped().Fixtures;
            var full = HouseBuffs.FullRoomAt(catalogue, BarRating.MaxStars);
            foreach (var kind in FittingBuffs.DataKinds)
                Assert.AreEqual(kind.Direction * kind.HouseCap, full.RawPercent(kind),
                    kind.Id + ": the shipped full room is the cap, so the clamp is only ever a guard");
            Assert.IsTrue(HouseBuffs.FullRoomAt(catalogue, 0.0).Percent(FittingBuffs.Price) == 0,
                "no price before the neon's first star");
        }

        [Test]
        public void ShippedFixtures_TheToolsDeriveTheirFigures()
        {
            var catalogue = Shipped().Fixtures;
            FixtureDefinition F(string id) => catalogue.First(f => f.Id == id);
            Assert.AreEqual(50, FittingBuffs.EffectsOf(F("shaker_gold"), catalogue)[0].Percent);
            Assert.AreEqual(-12, FittingBuffs.EffectsOf(F("counter_sink"), catalogue)[0].Percent);
            var brass = FittingBuffs.EffectsOf(F("sink_brass"), catalogue);
            Assert.AreEqual(-26, brass[0].Percent);
            Assert.AreSame(FittingBuffs.FreeDrain, brass[1].Kind);
            foreach (var f in catalogue.Where(f => f.StartsInTheRoom))
                Assert.IsTrue(FittingBuffs.EffectsOf(f, catalogue)[0].IsBase, f.Id + " is the house standard");
        }

        [Test]
        public void TheRoomAsItOpens_IsTodaysRoom_OnTheShippedCatalogue()
        {
            var catalogue = Shipped().Fixtures;
            var fresh = new TycoonRun(PlainShelf(), OnePage, new RunRng("open"), config: Cfg(), fixtures: catalogue);
            foreach (var k in FittingBuffs.All) Assert.AreEqual(0, fresh.Buffs.Percent(k), k.Id);
            Assert.AreEqual(3.4, fresh.SinkSeconds, 1e-12, "the old sink the room opens with");
            Assert.AreEqual(Housekeeping.DirtGrace, fresh.Floor.House.Grace);
            Assert.AreEqual(1.0, fresh.WorkSpeed("shaker"), 1e-12);
            Assert.AreEqual(1.0, fresh.TapSpeed, 1e-12);

            (int price, double patience, double at, double comfort, DayResult night) Play(bool enabled)
            {
                bool was = HouseBuffs.Enabled;
                try
                {
                    HouseBuffs.Enabled = enabled;
                    var run = new TycoonRun(PlainShelf(), OnePage, new RunRng("open"), config: Cfg(), fixtures: catalogue);
                    var first = FirstArrival(run);
                    double at = run.Floor.Elapsed;
                    first.InspectId();
                    int price = first.Order.Price;
                    double comfort = run.ComfortNow;
                    run.DevSkipToDayEnd();
                    return (price, first.PatienceMax, at, comfort, run.ContinueToNextDay());
                }
                finally { HouseBuffs.Enabled = was; }
            }
            var on = Play(true);
            var off = Play(false);
            Assert.AreEqual(off.price, on.price);
            Assert.AreEqual(off.patience, on.patience);
            Assert.AreEqual(off.at, on.at);
            Assert.AreEqual(off.comfort, on.comfort);
            Assert.AreEqual(off.night.Income, on.night.Income);
            Assert.AreEqual(off.night.Expenses, on.night.Expenses);
            Assert.AreEqual(off.night.Served, on.night.Served);
            Assert.AreEqual(off.night.WalkedOut, on.night.WalkedOut);
            Assert.AreEqual(off.night.AverageSatisfaction, on.night.AverageSatisfaction);
            Assert.AreEqual(off.night.ComfortStars, on.night.ComfortStars);
            Assert.AreEqual(off.night.TillAfter, on.night.TillAfter);
        }

        [Test]
        public void ThePageSaysWhatTheDrinkerPays_UnderTheShippedNeon()
        {
            // Every page a bar can be asked for, priced twice — by the till for a regular crowd with no
            // premium on the shelf, and by the book — with the Neon Pelican on the wall and without it.
            var neon = Shipped().Fixtures.Where(f => f.Id == "neon6_bird").ToList();
            Assert.AreEqual(1, neon.Count);
            int checkedPages = 0;
            foreach (var page in RecipeCatalog.CreateDefault())
            {
                if (page.RatioRequirements.Count == 0) continue;
                bool asked = true;
                foreach (bool lit in new[] { false, true })
                {
                    var run = new TycoonRun(PlainShelf(), new[] { page }, new RunRng("page-" + page.Id),
                        config: Cfg(), fixtures: lit ? neon : null);
                    run.DevPresetStars(BarRating.MaxStars);         // every page open, the neon up
                    Assert.AreEqual(lit ? 12 : 0, run.Buffs.Percent(FittingBuffs.Price), page.Id);
                    // A page whose extra lives in a jar this bare shelf does not stock is not asked for.
                    if (page.Garnish != null && !run.PreparationsOpen.Any(g => g.Id == page.Garnish))
                    {
                        asked = false;
                        break;
                    }
                    Assert.AreEqual(WealthTier.Regular, run.CrowdToday);
                    var first = FirstArrival(run);
                    first.InspectId();
                    Assert.AreEqual(page.Id, first.Order.Wanted.Id);
                    Assert.AreEqual(first.Order.Price, run.PagePrice(page),
                        page.Id + (lit ? " under the neon" : "") + ": the book prints what the till takes");
                }
                if (asked) checkedPages++;
            }
            Assert.Greater(checkedPages, 30, "most of the book was asked for");
        }
    }
}
