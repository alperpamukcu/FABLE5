using System;
using System.Linq;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The bar-dressing fixtures (2026-08-10): parse rules, and the purchase contract
    /// every buyable in this game signs — books the expense, applies the effect, refunds
    /// the same night, never sells on credit, and respects its gate.
    /// </summary>
    public sealed class FixtureTests
    {
        // ── the catalogue under test ────────────────────────────────────────────

        private static FixtureDefinition Fern(double stars = 0) =>
            new FixtureDefinition("fern_pot", "Potted Fern", "plant_left", 20, stars,
                "A little green.", "fx_fern");

        private static FixtureDefinition Lantern() =>
            new FixtureDefinition("lantern", "Hanging Lantern", "lamp_left", 60, 1.5,
                "Warm.", "fx_lantern", 1f, 0.78f, 0.5f, 0.55f, 70f);

        /// <summary>The first beer font: the one fixture the room opens already owning, and
        /// the only door onto the draught station (2026-08-15).</summary>
        private static FixtureDefinition FirstTap() =>
            new FixtureDefinition("taps_one", "Single Draught Tower", "s2", 35, 0,
                "One line, one lager.", "fx_tap_single",
                startsInTheRoom: true, tapLevel: 1);

        /// <summary>The rungs above it (2026-08-19): the same station in the same slot.</summary>
        private static FixtureDefinition Tap(int level) =>
            new FixtureDefinition("taps_" + level, level + "-Line Tower", "s2", 30 * level, 0,
                level + " lines.", "fx_tap_single", tapLevel: level);

        /// <summary>A rung of the wall-lamp ladder (2026-08-24): the ladder that proved the
        /// rung concept is not about beer. Same slot, climbing marks, no draught in it.</summary>
        private static FixtureDefinition Lamp(int level, bool startsOwned = false) =>
            new FixtureDefinition("lamps_" + level, "Mark " + level + " Lamps", "s1",
                25 * level, 0, "Two on the wall.", "fx_wall_lamp_lv" + (level - 1),
                0.9f, 0.7f, 0.5f, 0.9f, 120f,
                startsInTheRoom: startsOwned, level: level);

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
        });

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[]
        {
            new RecipeDefinition("neat", "Neat Pour", rank: 1, baseFlavor: 6, baseMult: 1,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[] { new RatioRequirement(IngredientType.Spirit, 0.9, 1.0) },
                minFill: 0.2),
        };

        private static TycoonRun RunAtDayEnd(int money = 200, params FixtureDefinition[] fixtures)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng("fixtures"),
                config: new TycoonConfig(money, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: fixtures);
            run.DevSkipToDayEnd();
            return run;
        }

        // ── parsing ─────────────────────────────────────────────────────────────

        /// <summary>The hooks every catalogue in these tests stands its pieces in. A room
        /// with no slots is a content bug of its own, so every fixture case needs them.</summary>
        private const string Slots = @"""slots"": [
            { ""id"": ""plant_left"", ""x"": 20, ""y"": 129 },
            { ""id"": ""lamp_left"", ""x"": 151, ""y"": 266 },
            { ""id"": ""s1"", ""x"": 0, ""y"": 0 },
            { ""id"": ""s2"", ""x"": 10, ""y"": 0, ""onCounter"": true }],";

        [Test]
        public void ParseFixtures_ReadsTheCatalogue_LightsAndAll()
        {
            var loaded = DataLoader.ParseFixtures(@"{
                ""version"": 1," + Slots + @"
                ""fixtures"": [
                    { ""id"": ""fern"", ""name"": ""Fern"", ""slot"": ""plant_left"",
                      ""price"": 20, ""stars"": 0, ""flavor"": ""green"", ""sprite"": ""fx_fern"" },
                    { ""id"": ""lantern"", ""name"": ""Lantern"", ""slot"": ""lamp_left"",
                      ""price"": 60, ""stars"": 1.5, ""sprite"": ""fx_lantern"",
                      ""lightR"": 1.0, ""lightG"": 0.8, ""lightB"": 0.5,
                      ""lightIntensity"": 0.55, ""lightRadius"": 70 },
                    { ""id"": ""taps"", ""name"": ""Taps"", ""slot"": ""s2"",
                      ""price"": 35, ""stars"": 0, ""sprite"": ""fx_tap_single"",
                      ""tapLevel"": 1, ""startsInTheRoom"": true }
                ]}");

            var fixtures = loaded.Fixtures;
            Assert.AreEqual(3, fixtures.Count);
            Assert.IsFalse(fixtures[0].HasLight, "a fern does not shine");
            Assert.IsTrue(fixtures[1].HasLight);
            Assert.AreEqual(70f, fixtures[1].LightRadius);
            Assert.AreEqual(1.5, fixtures[1].Stars);

            // The font's two flags (2026-08-15). Both default false, which is what keeps
            // every entry that does not mention them reading exactly as it did.
            Assert.IsTrue(fixtures[2].IsTap, "a beer font says so in the data");
            Assert.AreEqual(1, fixtures[2].TapLevel, "and says how many lines it runs");
            Assert.IsTrue(fixtures[2].StartsInTheRoom);
            Assert.IsFalse(fixtures[0].IsTap, "and a fern is not a door");
            Assert.AreEqual(0, fixtures[0].TapLevel);
            Assert.IsFalse(fixtures[0].StartsInTheRoom);

            // The slots come out of the same file, and they carry where AND how they draw.
            Assert.AreEqual(4, loaded.Slots.Count);
            Assert.AreEqual(129f, loaded.Slots[0].Y);
            Assert.IsFalse(loaded.Slots[2].OnCounter);
            Assert.IsTrue(loaded.Slots[3].OnCounter, "a counter-top slot says so in the data");
        }

        [Test]
        public void ABackdropSlot_AndASwatch_ComeOutOfTheData()
        {
            // The wall ladder (2026-09-06): a slot whose piece IS the room's back wall, and
            // a rung that names the window of itself the market shows. Both default off,
            // so every entry that never mentions them reads as it did.
            var loaded = DataLoader.ParseFixtures(@"{ ""slots"": [
                    { ""id"": ""walls"", ""x"": 320, ""y"": 180, ""backdrop"": true },
                    { ""id"": ""corner"", ""x"": 10, ""y"": 10 } ],
                ""fixtures"": [
                    { ""id"": ""w1"", ""name"": ""Cracked"", ""slot"": ""walls"",
                      ""price"": 40, ""sprite"": ""fx_walls_1"", ""swatch"": ""fx_walls_swatch_1"",
                      ""group"": ""Walls"", ""level"": 1, ""startsInTheRoom"": true },
                    { ""id"": ""w2"", ""name"": ""Plaster"", ""slot"": ""walls"",
                      ""price"": 70, ""sprite"": ""fx_walls_2"", ""swatch"": ""fx_walls_swatch_2"",
                      ""level"": 2, ""comfort"": 0.3 },
                    { ""id"": ""fern"", ""name"": ""Fern"", ""slot"": ""corner"",
                      ""price"": 10, ""sprite"": ""fx_fern"" }]}");
            Assert.IsTrue(loaded.Slots[0].Backdrop, "the wall says it is the room");
            Assert.IsFalse(loaded.Slots[1].Backdrop, "and a corner is a hook");
            Assert.AreEqual("fx_walls_swatch_1", loaded.Fixtures[0].Swatch);
            Assert.AreEqual("walls", loaded.Fixtures[0].Group, "the shelf it is sold from, lower-cased");
            Assert.IsNull(loaded.Fixtures[2].Group, "a piece that names no shelf");
            Assert.AreEqual(2, loaded.Fixtures[1].Level, "the rungs climb like any ladder");
            Assert.IsNull(loaded.Fixtures[2].Swatch, "a fern's sprite is its own picture");
        }

        [Test]
        public void TheRoomOpensBare_AndTheDressingIsBought()
        {
            // The author, 2026-09-06: the picture, the wall lamps, the rug and the set are
            // UPGRADES — bought, not given — and the bar mat is not one. What the bar opens
            // with is the FreeBase and carries no comfort; what is bought carries some.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var given = loaded.Fixtures.Where(f => f.StartsInTheRoom).Select(f => f.Id).OrderBy(s => s).ToArray();
            Assert.AreEqual(new[] { "beer_mat", "counter_lamps", "prep_mat", "shaker_steel", "sink_old",
                                    "table5_bistro_L", "table5_bistro_R", "taps_one",
                                    "walls_1", "walls_right_1" }, given,
                "the room opens as the author's 'Oyun açılış v2' (2026-09-13): the cracked back wall, "
                + "the peeling right wall, the two bistro tables, the steel sink and both mats — and "
                + "the tools, one tap and the steel tin, which are only ever upgraded — and, since "
                + "2026-09-22, the pendants over the counter and the OLD sink the last owner left - the steel "
                + "one is bought now (the author: 'bu başlangıç sinki olacak, satarım')");
            foreach (var id in given)
                Assert.AreEqual(0, loaded.Fixtures.First(f => f.Id == id).Comfort, id + " is the FreeBase");
            foreach (var id in new[] { "flamingo_triptych", "wall_lamps_one", "floor_rug", "wall_tv" })
            {
                var piece = loaded.Fixtures.First(f => f.Id == id);
                Assert.IsFalse(piece.StartsInTheRoom, id + " is bought now");
                Assert.Greater(piece.Comfort, 0, id + " is worth something to the room");
                Assert.Greater(piece.Price, 0);
            }
            foreach (var f in loaded.Fixtures)
                Assert.IsNotNull(f.Group, f.Id + " names no shelf of the upgrade screen");
        }

        [Test]
        public void TheShakerIsALadderTheRoomDoesNotStand()
        {
            // The author's own tin, and a gold one to buy (2026-09-06). It is a TOOL: the
            // slot is carried, so the room never stands it at a hook — the counter draws it
            // while a drink waits in it and the bench puts it in your hand. What the data
            // has to get right is that it is a two-rung ladder like any other and that both
            // its plates are on the shelf the HUD loads them from.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var slot = loaded.Slots.First(s => s.Id == "shaker");
            Assert.IsTrue(slot.Carried, "the shaker is carried, not stood");
            Assert.IsFalse(slot.Backdrop);

            var rungs = loaded.Fixtures.Where(f => f.Slot == "shaker").OrderBy(f => f.Level).ToList();
            Assert.AreEqual(new[] { 1, 2 }, rungs.Select(r => r.Level).ToArray());
            Assert.IsTrue(rungs[0].StartsInTheRoom, "the steel tin came with the room");
            Assert.AreEqual(0, rungs[0].Comfort, "and it is part of the FreeBase");
            Assert.Greater(rungs[1].Comfort, 0, "the gold one is worth something to the room");
            Assert.AreEqual("counter", rungs[0].Group, "sold off the counter's shelf");
            foreach (var r in rungs)
                Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Items/" + r.Sprite),
                    r.Id + "'s picture is not on the Items shelf");
            // The tin the benches actually draw, both tiers, on the sheet they share.
            foreach (var plate in new[] { "shaker", "tin_open", "shaker_cap", "shaker_cap_pour" })
                foreach (var tier in new[] { "", "_t2" })
                {
                    var art = UnityEngine.Resources.Load<UnityEngine.Sprite>("Items/" + plate + tier);
                    Assert.IsNotNull(art, plate + tier + " is missing");
                    Assert.AreEqual(new UnityEngine.Vector2(116, 208), art.rect.size,
                        plate + tier + " is off the shared sheet — the lid would not land on the neck");
                }
        }

        [Test]
        public void TheShippedWalls_AreALadder_ThatOpensCracked()
        {
            // The author's four plates, as content (2026-09-06): club_room4 is the bar the
            // run opens in and the others climb from it. Read off the real file, because a
            // rung that names a plate nobody drew is a wall that never changes.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var walls = loaded.Slots.First(s => s.Id == "walls");
            Assert.IsTrue(walls.Backdrop);
            var rungs = loaded.Fixtures.Where(f => f.Slot == "walls").OrderBy(f => f.Level).ToList();
            // FIVE since the tree shipped (2026-09-13): the chevron went in under the harlequin. SIX since the
            // eighth list (2026-09-22): round seven's wave mural between the chevron and the harlequin.
            Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, rungs.Select(r => r.Level).ToArray());
            Assert.AreEqual("back6_chevron", rungs[3].Id, "the author's tier order");
            Assert.AreEqual("back7_wave", rungs[4].Id, "the author's pick B2, under the harlequin");
            Assert.IsTrue(rungs[0].StartsInTheRoom, "the bar opens in the cracked room");
            Assert.AreEqual(0, rungs[0].Comfort, "and what it opens with is the FreeBase");
            for (int i = 1; i < rungs.Count; i++)
            {
                Assert.Greater(rungs[i].Comfort, rungs[i - 1].Comfort, rungs[i].Id + " is worth more");
                Assert.Greater(rungs[i].Price, rungs[i - 1].Price, rungs[i].Id + " costs more");
                Assert.IsFalse(rungs[i].StartsInTheRoom);
            }
            foreach (var r in rungs)
            {
                Assert.IsNotNull(r.Swatch, r.Id + " needs a swatch for the market");
                Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + r.Sprite),
                    r.Id + "'s plate is not drawn");
                Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + r.Swatch),
                    r.Id + "'s swatch is not cut");
            }
        }

        [Test]
        public void TheShippedRightWall_IsALadderOfItsOwn_LaidOverThePlate()
        {
            // The author's right wall, drawn four ways (2026-09-12): it climbs its own ladder
            // instead of changing with the back wall, and its rungs are LAYERS over the plate
            // rather than plates. Read off the real file, like the back wall's own test.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var slot = loaded.Slots.First(s => s.Id == "walls_right");
            Assert.IsTrue(slot.Backdrop && slot.Overlay, "a layer of the room's picture, laid over it");
            Assert.AreEqual("The right wall", slot.Place, "the market says where the rung goes");
            var rungs = loaded.Fixtures.Where(f => f.Slot == "walls_right").OrderBy(f => f.Level).ToList();
            // SEVEN since the tree shipped (2026-09-13): the author's four and round six's three. EIGHT since the
            // eighth list (2026-09-22): round seven's deco fans at the top.
            Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, rungs.Select(r => r.Level).ToArray());
            Assert.IsTrue(rungs[0].StartsInTheRoom, "the bar opens with the wall it has");
            Assert.AreEqual(0, rungs[0].Comfort, "and the wall it has is worth nothing");
            for (int i = 1; i < rungs.Count; i++)
            {
                Assert.Greater(rungs[i].Comfort, rungs[i - 1].Comfort, rungs[i].Id + " is worth more");
                Assert.Greater(rungs[i].Price, rungs[i - 1].Price, rungs[i].Id + " costs more");
                Assert.IsFalse(rungs[i].StartsInTheRoom);
            }
            foreach (var r in rungs)
            {
                var art = UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + r.Sprite);
                Assert.IsNotNull(art, r.Id + "'s layer is not drawn");
                Assert.AreEqual(new UnityEngine.Vector2(640, 360), art.rect.size,
                    r.Id + " is off the plate's canvas — it would not land on the wall it was cut from");
                Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + r.Swatch),
                    r.Id + "'s swatch is not cut");
            }
        }

        [Test]
        public void AnOverlay_IsALayerOfThePicture_SoItMustBeABackdrop()
        {
            // An overlay laid over a hook would be a 640x360 canvas stood on its feet at x, y.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{ ""slots"": [
                    { ""id"": ""walls"", ""x"": 320, ""y"": 180, ""backdrop"": true },
                    { ""id"": ""odd"", ""x"": 10, ""y"": 10, ""overlay"": true } ],
                ""fixtures"": [
                    { ""id"": ""w1"", ""name"": ""Cracked"", ""slot"": ""walls"",
                      ""price"": 40, ""sprite"": ""fx_walls_1"" }]}"));
        }

        [Test]
        public void AFixture_CannotName_ASlotTheRoomDoesNotHave()
        {
            // The room used to warn about this at RUNTIME, on the night the piece was
            // bought — a fixture sold and never seen. It is a content bug, so it fails at
            // the load like every other content bug.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @"
                ""fixtures"": [
                    { ""id"": ""a"", ""name"": ""A"", ""slot"": ""nowhere"",
                      ""price"": 10, ""sprite"": ""x"" }]}"));
        }

        [Test]
        public void ARoom_WithNoSlots_IsRefused()
        {
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{ ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" }]}"));
        }

        [Test]
        public void ParseFixtures_RefusesContentBugs_AtLoad()
        {
            // The same id twice.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" },
                { ""id"": ""a"", ""name"": ""B"", ""slot"": ""s2"", ""price"": 10, ""sprite"": ""x"" }]}"));
            // Two fixtures fighting over one hook.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" },
                { ""id"": ""b"", ""name"": ""B"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" }]}"));
            // ...including a tower sharing with something that is not one. The exception
            // below is for a LADDER, and half a ladder is just the old bug.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"" },
                { ""id"": ""b"", ""name"": ""B"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""tapLevel"": 1 }]}"));
            // Two towers on the same rung: one of them could never be bought.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""tapLevel"": 1 },
                { ""id"": ""b"", ""name"": ""B"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""tapLevel"": 1 }]}"));
            // A ladder with a rung missing: the market sells one line at a time, so the
            // third tower would be unreachable for the whole game and nothing would say so.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""tapLevel"": 1 },
                { ""id"": ""b"", ""name"": ""B"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""tapLevel"": 3 }]}"));
            // A lamp that shines but has no radius.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 10, ""sprite"": ""x"",
                  ""lightIntensity"": 0.5 }]}"));
            // A freebie.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""a"", ""name"": ""A"", ""slot"": ""s1"", ""price"": 0, ""sprite"": ""x"" }]}"));
        }

        // ── the purchase contract ───────────────────────────────────────────────

        [Test]
        public void BuyFixture_BooksTheExpense_AndDressesTheRoom()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            int before = run.Money;

            int price = run.BuyFixture("fern_pot");

            Assert.AreEqual(20, price, "the definition prices it");
            Assert.AreEqual(before - 20, run.Money);
            Assert.AreEqual(20, run.DayUpgrades, "the invoice itemises it");
            Assert.IsTrue(run.OwnsFixture("fern_pot"));
            Assert.AreEqual(1, run.OwnedFixtureCount);
            var slip = run.TodaysPurchases[run.TodaysPurchases.Count - 1];
            Assert.AreEqual(TycoonRun.DayPurchase.Kind.Fixture, slip.What);
            Assert.AreEqual("Potted Fern", slip.Name);
        }

        // ── the piece the room opens with (2026-08-15) ──────────────────────────

        [Test]
        public void TheFirstTap_IsStandingBeforeTheBarOpens()
        {
            // Beer left the back-bar wall for the font on the counter, and `draught` is a
            // rank-1 page every bar starts with — so a bar that had to BUY the door could not
            // pour a pint on its opening night.
            var run = new TycoonRun(NewShelf(), Book, new RunRng("fixtures"),
                fixtures: new[] { Fern(), FirstTap() });

            Assert.IsTrue(run.OwnsFixture("taps_one"), "the font is on the counter from night one");
            Assert.IsFalse(run.OwnsFixture("fern_pot"), "and nothing else is");
            Assert.AreEqual(1, run.OwnedFixtureCount);
        }

        [Test]
        public void ThePieceTheRoomOpensWith_IsNeitherSoldNorSoldBack()
        {
            var run = RunAtDayEnd(fixtures: FirstTap());
            int before = run.Money;

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("taps_one"),
                "the bar cannot be sold a thing it already has");
            Assert.AreEqual(0, run.TodaysPurchases.Count,
                "and nothing it never bought can be refunded for money nobody paid");
            Assert.AreEqual(before, run.Money);
            Assert.IsTrue(run.OwnsFixture("taps_one"));
        }

        // ── the tower ladder (2026-08-19) ───────────────────────────────────────

        [Test]
        public void TheRoomHasOneTower_DrawnAsTheAuthorsTapBeer()
        {
            // 2026-09-21, the author: the tower's drawing no longer changes with the beer upgrade; the ladder's
            // rungs add the lines instead (TycoonRun.TapLevel). The catalogue keeps one tower, standing from
            // the start, and no taller one to buy over it.
            string path = UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json";
            var loaded = DataLoader.ParseFixtures(System.IO.File.ReadAllText(path));
            var towers = loaded.Fixtures.Where(f => f.IsTap).ToList();
            Assert.AreEqual(1, towers.Count, "one tower in the shipped catalogue");
            Assert.AreEqual("taps_one", towers[0].Id);
            Assert.IsTrue(towers[0].StartsInTheRoom, "and the room opens with it");
            Assert.AreEqual("fx_tap_beer", towers[0].Sprite, "the author's tap_beer at every line count");
            Assert.AreEqual(1, towers[0].TapLevel, "its own line is the first; the ladder brings the rest");
        }

        [Test]
        public void TheLadder_ParsesAsThreeLevelsInOneSlot()
        {
            var loaded = DataLoader.ParseFixtures(@"{" + Slots + @" ""fixtures"": [
                { ""id"": ""t1"", ""name"": ""One"", ""slot"": ""s2"", ""price"": 35,
                  ""sprite"": ""fx_tap_single"", ""tapLevel"": 1, ""startsInTheRoom"": true },
                { ""id"": ""t2"", ""name"": ""Two"", ""slot"": ""s2"", ""price"": 65,
                  ""sprite"": ""fx_tap_double"", ""tapLevel"": 2 },
                { ""id"": ""t3"", ""name"": ""Three"", ""slot"": ""s2"", ""price"": 100,
                  ""sprite"": ""fx_tap_triple"", ""tapLevel"": 3 }]}");
            Assert.AreEqual(3, loaded.Fixtures.Count);
            foreach (var f in loaded.Fixtures)
                Assert.AreEqual("s2", f.Slot, "three levels of one station stand in one place");
        }

        [Test]
        public void TheTower_ClimbsOneRungAtATime()
        {
            var run = RunAtDayEnd(500, FirstTap(), Tap(2), Tap(3));
            Assert.AreEqual(1, run.TapLevel, "the bar opens with one line");

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("taps_3"),
                "the triple cannot be fitted over a bar that never ran two lines");
            Assert.AreEqual(1, run.TapLevel, "and the refusal costs nothing");

            run.BuyFixture("taps_2");
            Assert.AreEqual(2, run.TapLevel);
            run.BuyFixture("taps_3");
            Assert.AreEqual(3, run.TapLevel);
        }

        [Test]
        public void OnlyTheTallestTower_IsStandingOnTheCounter()
        {
            var run = RunAtDayEnd(500, FirstTap(), Tap(2), Tap(3));
            Assert.AreEqual("taps_one", run.StandingTap().Id);

            run.BuyFixture("taps_2");

            // Both are OWNED - a tower is fitted over, not sold back - but the room stands
            // one of them, or it draws two towers in the same slot, one inside the other.
            Assert.IsTrue(run.OwnsFixture("taps_one"));
            Assert.AreEqual("taps_2", run.StandingTap().Id);
        }

        [Test]
        public void ARungCannotBeTakenBack_FromUnderTheOneAboveIt()
        {
            var run = RunAtDayEnd(500, FirstTap(), Tap(2), Tap(3));
            run.BuyFixture("taps_2");
            int twin = run.TodaysPurchases.Count - 1;
            run.BuyFixture("taps_3");

            Assert.Throws<InvalidOperationException>(() => run.RefundToday(twin),
                "refunding the twin under a standing triple would hand the bar three " +
                "lines it never fitted the second of");
            Assert.AreEqual(3, run.TapLevel);

            // The taller one first, which is the order they were bought in, and then it works.
            run.RefundToday(run.TodaysPurchases.Count - 1);
            run.RefundToday(twin);
            Assert.AreEqual(1, run.TapLevel);
        }

        [Test]
        public void AFixture_IsDressing_NotAFitting()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            Assert.IsTrue(run.CanFitTonight, "the night starts with its fitting free");

            run.BuyFixture("fern_pot");

            Assert.IsTrue(run.CanFitTonight,
                "a fern changes what the room looks like, not what the bar IS — " +
                "the night's one fitting must still be spendable");
        }

        [Test]
        public void SameNightRefund_PutsTheFixtureBackOnTheTruck()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            int before = run.Money;
            run.BuyFixture("fern_pot");

            run.RefundToday(run.TodaysPurchases.Count - 1);

            Assert.AreEqual(before, run.Money, "the till is made whole");
            Assert.AreEqual(0, run.DayUpgrades);
            Assert.IsFalse(run.OwnsFixture("fern_pot"));
            // ...and having been refunded, it can be bought again tonight.
            run.BuyFixture("fern_pot");
            Assert.IsTrue(run.OwnsFixture("fern_pot"));
        }

        [Test]
        public void AtDawn_TheFixtureIsFinal_AndBilled()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            run.BuyFixture("fern_pot");

            var result = run.ContinueToNextDay();

            // ...AND WHATEVER THE NIGHT'S WALK-OUTS COST (2026-09-23). RunAtDayEnd plays a real
            // night with nobody serving, so everyone who sat down left without their drink, and a
            // drink that never came is a line on the bill now (TycoonConfig.WalkOutPenalty). The
            // fern and the rent are what this test is about; the third line is read off the run
            // rather than typed, so it cannot go stale when the penalty is tuned.
            Assert.AreEqual(run.Config.Rent(1) + 20 + result.WalkOutFees, result.Expenses,
                "rent + the fern + the drinks that never came");
            Assert.Greater(result.WalkOutFees, 0, "and the night did have walk-outs to pay for");
            Assert.AreEqual(0, run.TodaysPurchases.Count, "the slip is torn up");
            Assert.IsTrue(run.OwnsFixture("fern_pot"), "the fern stays");
        }

        [Test]
        public void NothingSellsOnCredit_FixturesIncluded()
        {
            // The skip to day end plays a real day — rent lands — so the broke-bar premise
            // is asserted on what the till actually holds THERE, not on the starting money.
            var run = RunAtDayEnd(money: 10, fixtures: Fern());
            int tillAtClose = run.Money;
            Assert.Less(tillAtClose, 20, "the premise: the bar cannot afford the fern");
            int upgradesBefore = run.DayUpgrades;

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("fern_pot"));

            Assert.AreEqual(tillAtClose, run.Money, "a refused buy takes nothing");
            Assert.AreEqual(upgradesBefore, run.DayUpgrades, "and books nothing");
            Assert.IsFalse(run.OwnsFixture("fern_pot"));
        }

        [Test]
        public void TheStarGate_HoldsUntilTheRoomEarnsIt()
        {
            var run = RunAtDayEnd(fixtures: Lantern());
            Assert.Less(run.Rating.Average, 1.5, "a new bar has no stars yet");

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("lantern"),
                "a 1.5-star lantern is not for a no-star room");

            run.Rating.DevSet(1.5);
            run.BuyFixture("lantern");
            Assert.IsTrue(run.OwnsFixture("lantern"));
        }

        [Test]
        public void UnknownAndDoubleBuys_AreRefused()
        {
            var run = RunAtDayEnd(fixtures: Fern());
            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("chandelier"),
                "the catalogue has no chandelier");

            run.BuyFixture("fern_pot");
            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("fern_pot"),
                "one fern per plant_left");
        }

        // ── the ladder is not about beer (2026-08-24, the wall lamps) ────────────

        [Test]
        public void ALadder_NeedsNoBeerInIt()
        {
            // Three lamp marks in one slot, sharing it the way the towers share theirs —
            // the slot-collision exception reads the RUNG now, not the tap.
            var loaded = DataLoader.ParseFixtures(@"{ " + Slots + @"
                ""fixtures"": [
                  { ""id"": ""l1"", ""name"": ""Mark 1"", ""slot"": ""s1"", ""price"": 25,
                    ""sprite"": ""fx_wall_lamp_lv0"", ""level"": 1, ""startsInTheRoom"": true },
                  { ""id"": ""l2"", ""name"": ""Mark 2"", ""slot"": ""s1"", ""price"": 50,
                    ""sprite"": ""fx_wall_lamp_lv1"", ""level"": 2 },
                  { ""id"": ""l3"", ""name"": ""Mark 3"", ""slot"": ""s1"", ""price"": 75,
                    ""sprite"": ""fx_wall_lamp_lv2"", ""level"": 3 }] }");
            Assert.AreEqual(3, loaded.Fixtures.Count);
            Assert.AreEqual(1, loaded.Fixtures[0].Level);
            Assert.IsFalse(loaded.Fixtures[0].IsTap, "a lamp ladder is not a draught tower");
            Assert.AreEqual(0, loaded.Fixtures[0].TapLevel, "and unlocks no kegs");
        }

        [Test]
        public void APairedSlot_CarriesItsSpreadAndItsClock()
        {
            var loaded = DataLoader.ParseFixtures(@"{ ""slots"": [
                  { ""id"": ""pair"", ""x"": 319, ""y"": 234,
                    ""pairSpreadPx"": 172, ""houseLight"": true }],
                ""fixtures"": [
                  { ""id"": ""l1"", ""name"": ""Mark 1"", ""slot"": ""pair"", ""price"": 25,
                    ""sprite"": ""fx_wall_lamp_lv0"", ""level"": 1 }] }");
            Assert.AreEqual(172f, loaded.Slots[0].PairSpreadPx);
            Assert.IsTrue(loaded.Slots[0].HouseLight);
        }

        [Test]
        public void AHangingSlot_SaysSo_AndTheFloorIsTheDefault()
        {
            // A picture on the wall touches no floor: no contact shadow, and it draws
            // behind the dressing standing in front of the wall. The slot carries it —
            // every hanger before the triptych happened to be lit, and "has no light"
            // was quietly standing in for "touches the floor" (2026-08-24).
            var loaded = DataLoader.ParseFixtures(@"{ ""slots"": [
                  { ""id"": ""wall"", ""x"": 319, ""y"": 190, ""hangs"": true },
                  { ""id"": ""floor"", ""x"": 20, ""y"": 129 }],
                ""fixtures"": [
                  { ""id"": ""art"", ""name"": ""Triptych"", ""slot"": ""wall"",
                    ""price"": 45, ""sprite"": ""fx_triptych"" }] }");
            Assert.IsTrue(loaded.Slots[0].Hangs, "a wall hook says so in the data");
            Assert.IsFalse(loaded.Slots[1].Hangs, "and the floor is the default");
        }

        [Test]
        public void AFlatSlot_SaysSo_OnTheFloorAndOnTheBar()
        {
            // A MAT is neither a hanger nor a prop: it lies on a surface that already has
            // dressing standing on it, so it draws under that dressing and casts no contact
            // shadow of its own (2026-08-25, the rug on the boards and the drip mat on the
            // bar). It is independent of onCounter, because both surfaces have one.
            var loaded = DataLoader.ParseFixtures(@"{ ""slots"": [
                  { ""id"": ""rug"", ""x"": 320, ""y"": 106, ""flat"": true },
                  { ""id"": ""mat"", ""x"": 540, ""y"": 74, ""onCounter"": true, ""flat"": true },
                  { ""id"": ""floor"", ""x"": 20, ""y"": 129 }],
                ""fixtures"": [
                  { ""id"": ""rug"", ""name"": ""Rug"", ""slot"": ""rug"",
                    ""price"": 35, ""sprite"": ""fx_floor_rug"" }] }");
            Assert.IsTrue(loaded.Slots[0].Flat, "a mat on the boards says so in the data");
            Assert.IsFalse(loaded.Slots[0].OnCounter);
            Assert.IsTrue(loaded.Slots[1].Flat, "and so does one on the bar top");
            Assert.IsTrue(loaded.Slots[1].OnCounter, "which is still on the counter");
            Assert.IsFalse(loaded.Slots[2].Flat, "standing up is the default");
        }

        [Test]
        public void TheLamps_ClimbOneRungAtATime_AndTheFirstIsAlreadyOnTheWall()
        {
            var run = RunAtDayEnd(fixtures: new[] { Lamp(1, startsOwned: true),
                                                    Lamp(2), Lamp(3) });
            Assert.IsTrue(run.OwnsFixture("lamps_1"), "the room opens lit");
            Assert.AreEqual(1, run.LadderLevel("s1"));

            Assert.Throws<InvalidOperationException>(() => run.BuyFixture("lamps_3"),
                "mark 3 over mark 1 skips the ladder");
            Assert.AreEqual(1, run.LadderLevel("s1"), "and the refusal changes nothing");

            run.BuyFixture("lamps_2");
            run.BuyFixture("lamps_3");
            Assert.AreEqual(3, run.LadderLevel("s1"), "climbed in order, it opens");
            Assert.AreEqual(0, run.TapLevel, "and the taps never heard about any of it");
        }

        /// <summary>A rung of a ladder that is worth something to the room, for the wearing
        /// tests: what is bought and what is SHOWN are two different questions (2026-09-12).</summary>
        private static FixtureDefinition Rung(int level, double comfort, bool startsOwned = false) =>
            new FixtureDefinition("wall_" + level, "Mark " + level + " Wall", "s1",
                20 * level, 0, "A wall.", "fx_walls_" + level,
                startsInTheRoom: startsOwned, level: level, comfort: comfort);

        [Test]
        public void ABoughtRung_CanBeWornAfterTheLadderClimbsPastIt_AndTheRoomKeepsItsComfort()
        {
            // The author, 2026-09-12: "oyuncu bir sonraki geliştirmeyi almak zorunda ama görsel
            // olarak önceki görselleri beğendiyse onları kullanabilecek ve konforu düşmeyecek."
            var run = RunAtDayEnd(fixtures: new[] { Rung(1, 0, startsOwned: true),
                                                    Rung(2, 1.0), Rung(3, 2.0) });
            run.BuyFixture("wall_2");
            run.BuyFixture("wall_3");
            Assert.AreEqual("wall_3", run.WornRung("s1").Id, "a ladder shows what it climbed to");
            double climbed = run.FixtureComfort;
            Assert.AreEqual(2.0, climbed, 1e-9, "and is worth the top rung");

            Assert.IsTrue(run.WearFixture("wall_1"), "the cracked wall is still the bar's own");
            Assert.AreEqual("wall_1", run.WornRung("s1").Id, "so it may go back on the wall");
            Assert.AreEqual(climbed, run.FixtureComfort, 1e-9,
                "and taste costs the room nothing: comfort is the rung it CLIMBED to");
            Assert.AreEqual(3, run.LadderLevel("s1"), "which is still mark 3");

            Assert.AreEqual(3, run.OwnedRungs("s1").Count, "three marks bought, three to choose from");
            Assert.IsTrue(run.WearTopRung("s1"));
            Assert.AreEqual("wall_3", run.WornRung("s1").Id, "and it goes back to the top");
        }

        [Test]
        public void ARung_TheBarNeverBought_CannotBeWorn()
        {
            // A look is not a way round a ladder: the room may only show what it paid for.
            var run = RunAtDayEnd(fixtures: new[] { Rung(1, 0, startsOwned: true),
                                                    Rung(2, 1.0), Rung(3, 2.0) });
            Assert.IsFalse(run.WearFixture("wall_3"), "mark 3 was never bought");
            Assert.IsFalse(run.WearFixture("nothing_at_all"));
            Assert.AreEqual("wall_1", run.WornRung("s1").Id, "the wall is what it was");
            int look = run.LookRevision;
            run.BuyFixture("wall_2");
            Assert.AreEqual("wall_2", run.WornRung("s1").Id, "a bought rung goes up by itself");
            Assert.AreEqual(look, run.LookRevision, "buying is not wearing");
            Assert.IsTrue(run.WearFixture("wall_1"));
            Assert.Greater(run.LookRevision, look, "and a swap tells the room to re-dress");
        }

        [Test]
        public void ALampRung_CannotBeTakenBack_FromUnderTheOneAboveIt()
        {
            var run = RunAtDayEnd(fixtures: new[] { Lamp(1, startsOwned: true),
                                                    Lamp(2), Lamp(3) });
            run.BuyFixture("lamps_2");
            run.BuyFixture("lamps_3");
            int mark2 = -1;
            for (int i = 0; i < run.TodaysPurchases.Count; i++)
                if (run.TodaysPurchases[i].Id == "lamps_2") mark2 = i;

            Assert.Throws<InvalidOperationException>(() => run.RefundToday(mark2),
                "mark 2 is under mark 3");
        }

        [Test]
        public void ASlot_HoldsOneKindOfLadder()
        {
            // A tower rung and a lamp rung sharing a hook would pass the rung arithmetic
            // and then lie to everything that reads the ladder — LadderLevel would let a
            // tap unlock a lamp mark. Refused at load, like every other content bug.
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{ " + Slots + @"
                ""fixtures"": [
                  { ""id"": ""t1"", ""name"": ""Tower"", ""slot"": ""s2"", ""price"": 35,
                    ""sprite"": ""fx_tap_single"", ""tapLevel"": 1 },
                  { ""id"": ""l2"", ""name"": ""Lamp"", ""slot"": ""s2"", ""price"": 50,
                    ""sprite"": ""fx_wall_lamp_lv1"", ""level"": 2 }] }"),
                "a slot's rungs must all be towers or none of them");
        }

        [Test]
        public void APiece_CannotClimbTwoLaddersAtOnce()
        {
            Assert.Throws<ArgumentException>(() =>
                new FixtureDefinition("both", "Confused", "s1", 10, 0, "", "fx",
                    tapLevel: 1, level: 2),
                "a tower's rung IS its tap level — carrying both is a content bug");
        }

        // ── the tree, shipped (2026-09-13) ──────────────────────────────────────

        private static LoadedFixtures ShippedRoom() =>
            DataLoader.ParseFixtures(System.IO.File.ReadAllText(
                UnityEngine.Application.dataPath + "/Data/fixtures/fixtures.json"));

        [Test]
        public void EveryShippedLadder_CostsMore_AndIsWorthMore_AsItClimbs()
        {
            // The author set each ladder's tiers in the upgrade tree; a rung that cost less or
            // gave less than the one under it would make the order a lie the player pays for.
            var loaded = ShippedRoom();
            foreach (var slot in loaded.Fixtures.Where(f => f.Level > 0).Select(f => f.Slot).Distinct())
            {
                var rungs = loaded.Fixtures.Where(f => f.Slot == slot).OrderBy(f => f.Level).ToList();
                for (int i = 1; i < rungs.Count; i++)
                {
                    Assert.Greater(rungs[i].Price, rungs[i - 1].Price, rungs[i].Id + " costs more than " + rungs[i - 1].Id);
                    Assert.Greater(rungs[i].Comfort, rungs[i - 1].Comfort, rungs[i].Id + " is worth more than " + rungs[i - 1].Id);
                    Assert.GreaterOrEqual(rungs[i].Stars, rungs[i - 1].Stars, rungs[i].Id + " asks no less of the room");
                    Assert.IsFalse(rungs[i].StartsInTheRoom, rungs[i].Id + " is bought, not given");
                }
            }
        }

        [Test]
        public void EveryShippedPiece_HasItsPicture_AndEveryLayerIsTheWholeRoom()
        {
            // The market shows every rung now, bought or not (the decor catalogue), so a rung
            // with no picture is a hole in the shelf as well as in the room.
            var loaded = ShippedRoom();
            var slots = loaded.Slots.ToDictionary(s => s.Id);
            foreach (var f in loaded.Fixtures)
            {
                var slot = slots[f.Slot];
                var art = UnityEngine.Resources.Load<UnityEngine.Sprite>((slot.Carried ? "Items/" : "Fixtures/") + f.Sprite);
                Assert.IsNotNull(art, f.Id + "'s picture " + f.Sprite + " is not on its shelf");
                if (slot.Overlay)
                    Assert.AreEqual(new UnityEngine.Vector2(640, 360), art.rect.size,
                        f.Id + " is off the plate's canvas — it would not land where it was cut from");
                if (f.Swatch != null)
                    Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + f.Swatch),
                        f.Id + "'s swatch " + f.Swatch + " is not cut");
                if (slot.Backdrop)
                    Assert.IsNotNull(f.Swatch, f.Id + " is a whole-room picture and needs a swatch for the market");
            }
        }

        [Test]
        public void TheCeilingAndTheFloor_AreLayersOverTheRightWall()
        {
            var loaded = ShippedRoom();
            foreach (var id in new[] { "ceiling", "floor" })
            {
                var slot = loaded.Slots.First(s => s.Id == id);
                Assert.IsTrue(slot.Backdrop && slot.Overlay, id + " is a layer of the room's picture");
                Assert.AreEqual(13, slot.Order, id + " lies over the right wall's 12");
                Assert.IsNotNull(slot.Title, id + " has a name on the market's shelf");
            }
            Assert.AreEqual(0, loaded.Slots.First(s => s.Id == "walls_right").Order, "the right wall keeps the default");
            Assert.IsFalse(loaded.Fixtures.Any(f => (f.Slot == "ceiling" || f.Slot == "floor") && f.StartsInTheRoom),
                "the bar opens under the plate's own ceiling, on its own boards");
        }

        [Test]
        public void TheScreenAndThePosters_ShareASpot_AndThePictureHangsAboveThem()
        {
            // The author, 2026-09-13: "posterlerle televizyon aynı klasmanda olacak fakat diğer
            // sağ duvar tablosu televizyonun altında üstünde gözükecek, duvar kalabalıklaşacak."
            var loaded = ShippedRoom();
            var screenSpot = loaded.Fixtures.Where(f => f.Slot == "wall_tv").OrderBy(f => f.Level).ToList();
            Assert.Greater(screenSpot.Count, 1, "one ladder: the set and the posters");
            Assert.IsTrue(screenSpot[0].IsScreen, "the set is its first rung");
            Assert.IsTrue(screenSpot.Skip(1).All(f => !f.IsScreen), "and the rest are posters");

            var tvSlot = loaded.Slots.First(s => s.Id == "wall_tv");
            float spotTop = 0f;
            foreach (var f in screenSpot)
            {
                var art = UnityEngine.Resources.Load<UnityEngine.Sprite>("Fixtures/" + f.Sprite);
                float h = f.IsScreen ? f.CellH : art.rect.height;
                float y = float.IsNaN(f.Y) ? tvSlot.Y : f.Y;
                spotTop = Math.Max(spotTop, y + h);
            }
            var pictures = loaded.Fixtures.Where(f => f.Slot == "wall_right_art").ToList();
            Assert.Greater(pictures.Count, 0);
            var picSlot = loaded.Slots.First(s => s.Id == "wall_right_art");
            foreach (var p in pictures)
            {
                float bottom = float.IsNaN(p.Y) ? picSlot.Y : p.Y;
                Assert.GreaterOrEqual(bottom, spotTop, p.Id + " hangs over the screen's spot, not on it");
            }
        }

        [Test]
        public void TheTools_WorkFasterAsTheyClimb()
        {
            // "Musluk ve shaker ... sadece upgrade edilecek, upgrade etmenin üretime buffu olacak."
            var loaded = ShippedRoom();
            foreach (var slot in new[] { "taps", "shaker" })
            {
                var rungs = loaded.Fixtures.Where(f => f.Slot == slot).OrderBy(f => f.Level).ToList();
                Assert.IsTrue(rungs[0].StartsInTheRoom, slot + " comes with the room");
                Assert.AreEqual(1.0, rungs[0].WorkSpeed, 1e-9, "at the plain speed");
                for (int i = 1; i < rungs.Count; i++)
                    Assert.Greater(rungs[i].WorkSpeed, rungs[i - 1].WorkSpeed, rungs[i].Id + " works faster");
            }
            var sinks = loaded.Fixtures.Where(f => f.Slot == "sink").OrderBy(f => f.Level).ToList();
            Assert.IsTrue(sinks[0].StartsInTheRoom);
            Assert.Greater(sinks[1].WashSeconds, 0);
            Assert.Less(sinks[1].WashSeconds, Housekeeping.WashSeconds, "the better basin washes faster");
        }

        [Test]
        public void AToolsSpeed_IsTheRungItWears()
        {
            // INVERTED 2026-09-23 (Option A of the fitting buffs). This pinned the 2026-09-13 reading
            // — the speed is the CLIMB's, so wearing the steel tin kept the gold one's pace — and the
            // author's "hangi geliştirme takılıysa o buff aktif olacak, konfor gibi değil" reverses it:
            // every effect of a fitting but comfort reads the piece that is INSTALLED, tools included.
            var steel = new FixtureDefinition("tin_1", "Steel", "s1", 35, 0, "", "shaker_prop",
                startsInTheRoom: true, level: 1);
            var gold = new FixtureDefinition("tin_2", "Gold", "s1", 140, 0, "", "shaker_prop_t2",
                level: 2, comfort: 0.4, workSpeed: 1.5);
            var run = RunAtDayEnd(fixtures: new[] { steel, gold });
            Assert.AreEqual(1.0, run.WorkSpeed("s1"), 1e-9, "the tin the bar opened with");
            Assert.AreEqual(1.0, run.WorkSpeed("nowhere"), 1e-9, "a slot with nothing in it works at the plain speed");
            run.BuyFixture("tin_2");
            Assert.AreEqual(1.5, run.WorkSpeed("s1"), 1e-9, "the gold tin, bought, goes up and shakes faster");
            Assert.IsTrue(run.WearFixture("tin_1"));
            Assert.AreEqual(1.0, run.WorkSpeed("s1"), 1e-9, "gold owned and steel worn: the steel tin's pace");
            Assert.AreEqual(0.4, run.FixtureComfort, 1e-9, "while the comfort stays the climb's");
            Assert.IsTrue(run.WearFixture("tin_2"));
            Assert.AreEqual(1.5, run.WorkSpeed("s1"), 1e-9, "wear the gold one and it is the gold one's again");
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FixtureDefinition("x", "X", "s1", 10, 0, "", "fx", workSpeed: -1));
        }

        [Test]
        public void AnOrder_BelongsToALayerOfThePicture()
        {
            var layer = DataLoader.ParseFixtures(@"{ ""slots"": [
                { ""id"": ""ceiling"", ""x"": 0, ""y"": 0, ""backdrop"": true, ""overlay"": true, ""order"": 13, ""title"": ""Ceiling"" }],
                ""fixtures"": [ { ""id"": ""c1"", ""name"": ""Beams"", ""slot"": ""ceiling"", ""price"": 40, ""sprite"": ""fx_ceil_beams"" }] }");
            Assert.AreEqual(13, layer.Slots[0].Order);
            Assert.AreEqual("Ceiling", layer.Slots[0].Title);
            Assert.Throws<FormatException>(() => DataLoader.ParseFixtures(@"{ ""slots"": [
                { ""id"": ""hook"", ""x"": 0, ""y"": 0, ""order"": 13 }],
                ""fixtures"": [ { ""id"": ""c1"", ""name"": ""Fern"", ""slot"": ""hook"", ""price"": 40, ""sprite"": ""fx_fern"" }] }"),
                "a hook's order is decided by what stands on it");
        }
    }
}
