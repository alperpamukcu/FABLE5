using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE LADDER WIRED INTO THE RUN (PLAN_rank_ladder L0, 2026-09-21): what a rung opens is refused below it by
    /// Core itself, through the same verbs the player and the sim bot use. BarRankTests holds the table; this holds
    /// the run to it.
    /// </summary>
    public sealed class LadderWiringTests
    {
        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 4000),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 4000),
        });

        private static readonly System.Collections.Generic.IReadOnlyList<RecipeDefinition> Book = new[]
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

        private static RegularsRegistry People() => new RegularsRegistry(new[]
        {
            new ArchetypeDefinition("after_shift", "Off the Late Shift",
                new[] { "Marguerite", "Dev", "Ola", "Kit", "Rasmus", "Nuray" }, 1,
                new[] { "this side of town" }),
        });

        private static TycoonRun NewRun(double stars, string seed = "ladder", bool people = false)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                regulars: people ? People() : null);
            run.Rating.DevSet(stars);
            return run;
        }

        /// <summary>Every order the night rolls, read off the card as the player would.</summary>
        private static System.Collections.Generic.List<DrinkOrder> OrdersOfTheNight(TycoonRun run, int atMost = 40)
        {
            var seen = new System.Collections.Generic.List<DrinkOrder>();
            var read = new System.Collections.Generic.HashSet<CustomerVisit>();
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen && seen.Count < atMost)
            {
                Assert.Less(guard++, 4000, "the night must end");
                run.Tick(5);
                foreach (var v in run.Floor.Seated.ToList())
                {
                    if (v.State != VisitState.Waiting || !v.HasOrdered || read.Contains(v)) continue;
                    v.InspectId();
                    read.Add(v);
                    seen.Add(v.Order);
                    run.DeclineOrder(v);   // nothing is poured: only what was ASKED for is under test
                }
            }
            return seen;
        }

        // ── the rail ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AtTheFoot_NobodyAsksForAnythingOffTheRail_AndTheGlassTakesNone()
        {
            var run = NewRun(0.0);
            Assert.AreEqual(0, run.Rank.Index);
            Assert.IsEmpty(run.PreparationsOpen, "nothing on the rail yet");
            var orders = OrdersOfTheNight(run);
            Assert.IsNotEmpty(orders, "a night with orders in it");
            Assert.IsTrue(orders.All(o => o.Spec.IsPlain), "no ice, no twist, no rim can be asked for below the first rung");

            var fresh = NewRun(0.0, "ladder-glass");
            Assert.IsFalse(fresh.PreparationOpen(Preparations.Ice));
            Assert.Throws<InvalidOperationException>(() => fresh.AddPreparationAtGlass(Preparations.Ice),
                "the rules layer refuses what the room would not have shown");
            Assert.Throws<InvalidOperationException>(() => fresh.AddPreparation(Preparations.Ice),
                "and the shaker's twin refuses the same");
        }

        [Test]
        public void AtHalfAStar_IceAndTheTwistOpen_TheRimsDoNot()
        {
            var run = NewRun(0.5, "ladder-half");
            Assert.AreEqual(1, run.Rank.Index);
            CollectionAssert.AreEqual(new[] { "ice", "lemon_twist" }, run.PreparationsOpen.Select(p => p.Id).ToList());
            var asked = OrdersOfTheNight(run, 60)
                .SelectMany(o => o.Spec.Garnishes).Select(g => g.Id).Distinct().ToList();
            Assert.IsTrue(asked.All(id => id == "ice" || id == "lemon_twist"),
                "the customers ask only for what the rail carries: " + string.Join(", ", asked));

            var glass = NewRun(0.5, "ladder-half-glass");
            glass.AddPreparationAtGlass(Preparations.Ice);
            glass.AddPreparationAtGlass(Preparations.LemonTwist);
            Assert.Throws<InvalidOperationException>(() => glass.AddPreparationAtGlass(Preparations.SaltRim),
                "no salt before the second rung");
        }

        [Test]
        public void AtOneStar_TheWholeRailIsOut()
        {
            var run = NewRun(1.0, "ladder-one");
            Assert.AreEqual(2, run.Rank.Index);
            Assert.AreEqual(4, run.PreparationsOpen.Count);
            run.AddPreparationAtGlass(Preparations.SaltRim);
            run.AddPreparationAtGlass(Preparations.SugarRim);
            Assert.IsTrue(run.ServingGlass.HasPreparation("salt_rim"));
        }

        // ── the door ─────────────────────────────────────────────────────────────────────────────

        // ── the shop reads the night's climb (2026-09-21) ──────────────────────────────────────

        private static TycoonRun NewRunWith(double stars, string seed, params FixtureDefinition[] fixtures)
        {
            var run = new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: fixtures);
            run.Rating.DevSet(stars);
            return run;
        }

        private static FixtureDefinition Tower() =>
            new FixtureDefinition("taps_one", "Draught Tower", "taps", 35, 0, "One tower.", "fx_tap_beer",
                startsInTheRoom: true, tapLevel: 1);

        private static FixtureDefinition Lamps(double comfort) =>
            new FixtureDefinition("lamps", "Lamps", "wall_lamps", 25, 0, "Two on the wall.", "fx_wall_lamp_lv0",
                startsInTheRoom: true, comfort: comfort);

        private static FixtureDefinition Picture(double stars) =>
            new FixtureDefinition("picture", "A Picture", "art", 40, stars, "On the wall.", "fx_art_city",
                comfort: 0.2);

        /// <summary>Plays the night out serving everyone a spritz and keeping the counter clean.</summary>
        private static void PlayAGoodNight(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 4000, "the night must end");
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

        [Test]
        public void ANightThatCrossesARung_OpensTheRungsListings_TheSameEvening()
        {
            // The author (2026-09-21): olives and mint reached tonight are in TONIGHT's market. The shop's number
            // is the ladder's, carried through the night the books have not yet closed.
            var run = NewRunWith(1.99, "ladder-shop-rise", Tower(), Lamps(5.0), Picture(2.0));
            Assert.AreEqual(2, run.Rank.Index, "just under the third rung");
            Assert.AreEqual(1, run.TapLevel, "one line under the third rung");
            PlayAGoodNight(run);
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            Assert.Greater(run.StandingAfterTonight, 2.0, "a night served well and kept clean climbs past two stars");
            Assert.Less(run.Rating.Average, 2.0, "the books are not closed yet");
            Assert.AreEqual(3, run.RankAfterTonight.Index);
            Assert.AreEqual(run.StandingAfterTonight, run.ShopStars, 1e-9, "the shop reads the climb before the books file it");
            Assert.IsTrue(run.OpenedLastNight(2.0), "a 2.0 listing is NEW tonight");
            Assert.IsFalse(run.OpenedLastNight(1.0), "a gate long open is not");
            Assert.DoesNotThrow(() => run.BuyFixture("picture"), "a 2.0-star piece sells the evening the bar reached it");
            double shop = run.ShopStars;
            run.ContinueToNextDay();
            Assert.AreEqual(shop, run.Rating.BestStanding, 1e-9, "the books file exactly what the shop read");
            Assert.AreEqual(3, run.Rank.Index);
            Assert.AreEqual(2, run.TapLevel, "and the second draught line is open");
            Assert.IsFalse(run.OpenedLastNight(2.5), "a gate still shut is not new");
        }

        [Test]
        public void WhenTheStandingFalls_TheShopKeepsWhatTheLadderOpened()
        {
            var run = NewRunWith(2.0, "ladder-shop-fall", Tower(), Picture(2.0));
            Assert.AreEqual(2, run.TapLevel);
            OrdersOfTheNight(run, 400);   // everyone declined: a dreadful night
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            Assert.Less(run.StandingAfterTonight, 2.0, "the night drags the standing under the rung");
            Assert.AreEqual(2.0, run.ShopStars, 1e-9, "the shop reads the mark, not the fall");
            Assert.DoesNotThrow(() => run.BuyFixture("picture"), "a 2.0-star piece still sells tonight");
            Assert.IsFalse(run.OpenedLastNight(2.0), "and it is not NEW: it opened before tonight");
            run.ContinueToNextDay();
            Assert.Less(run.Rating.Average, 2.0);
            Assert.AreEqual(2.0, run.ShopStars, 1e-9, "tomorrow too");
            Assert.AreEqual(2, run.TapLevel, "the lines do not go back either");
        }

        [Test]
        public void TheDraughtLines_ComeWithTheRungs_OnTheOneTower()
        {
            Assert.AreEqual(0, NewRunWith(3.0, "ladder-no-tower").TapLevel, "no tower, no line: a keg needs a spout");
            var run = NewRunWith(0.0, "ladder-lines", Tower());
            Assert.AreEqual(1, run.TapLevel);
            run.Rating.DevSet(2.0);
            Assert.AreEqual(2, run.TapLevel, "the second line at two stars");
            run.Rating.DevSet(3.0);
            Assert.AreEqual(3, run.TapLevel, "the third at three");
            Assert.AreEqual("taps_one", run.StandingTap().Id, "the same tower stands on the counter at every count");
        }

        [Test]
        public void TheJars_AreExtras_DroppedNotPoured_AndOnlyWhenStocked()
        {
            // The author (2026-09-21): recipes never carry mint or olives; a customer asks for them as an extra,
            // and only once the market's jar is on the shelf. Dropping one adds a step, never a drop of liquid.
            var bare = NewRun(2.0, "ladder-jars-bare");
            Assert.IsTrue(bare.Has(Feature.Jars));
            CollectionAssert.DoesNotContain(bare.PreparationsOpen.Select(p => p.Id).ToList(), "olive", "no jar, no olives");
            Assert.IsFalse(bare.PreparationOpen(Preparations.Mint));
            Assert.Throws<InvalidOperationException>(() => bare.AddPreparationAtGlass(Preparations.Olive), "the rules layer refuses without the jar");

            var stocked = new TycoonRun(new Shelf(new[]
                {
                    new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 4000),
                    new ShelfBottle(new IngredientCard("olive_luca", "Luca Olives", IngredientType.Garnish, 1,
                        new IngredientInfo("olive", 1, 3, "somewhere", 40, "test")), capacity: 40),
                }), Book, new RunRng("ladder-jars"),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0));
            stocked.Rating.DevSet(2.0);
            var open = stocked.PreparationsOpen.Select(p => p.Id).ToList();
            CollectionAssert.Contains(open, "olive", "the jar on the shelf opens the olives");
            CollectionAssert.DoesNotContain(open, "mint", "and no jar of mint, no mint");
            double before = stocked.ServingGlass.TotalVolume;
            stocked.AddPreparationAtGlass(Preparations.Olive);
            Assert.IsTrue(stocked.ServingGlass.HasPreparation("olive"));
            Assert.AreEqual(before, stocked.ServingGlass.TotalVolume, 1e-9, "a spear of olives is not a pour");
            foreach (var r in RecipeCatalog.CreateDefault())
                foreach (var band in r.RatioRequirements)
                    Assert.IsFalse(band.Style == "mint" || band.Style == "olive", r.Id + " still pours a jar");
        }

        [Test]
        public void TheSignatureExtra_MakesThePage_AndTheOrderAlwaysAsksForIt()
        {
            // A Dirty Martini is a Dry Martini with a spear of olives (2026-09-21): the same pour reads as the
            // plain page without the spear and as the signed page with it, whatever their ranks (14 over 22);
            // and every order for the signed page asks for the spear.
            var all = RecipeCatalog.CreateDefault();
            var dirty = all.First(r => r.Id == "dirty_martini");
            var dry = all.First(r => r.Id == "dry_martini");
            Assert.AreEqual("olive", dirty.Garnish);
            Assert.IsNull(dry.Garnish);
            var perfect = RatioRecipeMatcher.PerfectPour(dirty);
            var glass = new GlassContents(1.0);
            for (int i = 0; i < perfect.Length; i++) glass.Add(dirty.RatioRequirements[i].Style, perfect[i]);
            var cards = new System.Collections.Generic.Dictionary<string, IngredientCard>();
            foreach (var band in dirty.RatioRequirements)
                cards[band.Style] = new IngredientCard(band.Style, band.Style, IngredientType.Spirit, 5,
                    new IngredientInfo(band.Style, 4, 5, "somewhere", 40, "test"));   // top shelf: every band's tier met
            System.Func<string, IngredientCard> lookup = id => cards.TryGetValue(id, out var c) ? c : null;
            Assert.AreEqual("dry_martini", RatioRecipeMatcher.Match(glass, all, lookup)?.Recipe.Id, "no spear: a dry martini");
            glass.AddPreparation(Preparations.Olive);
            Assert.AreEqual("dirty_martini", RatioRecipeMatcher.Match(glass, all, lookup)?.Recipe.Id, "the spear makes it a dirty martini");

            var rng = new RunRng("signature").GetStream("orders");
            for (int i = 0; i < 40; i++)
                CollectionAssert.Contains(ServingSpec.Roll(dirty, rng, ServingSpec.GarnishPool).Garnishes.Select(g => g.Id).ToList(), "olive",
                    "every order for a dirty martini asks for its olives");
        }

        [Test]
        public void TheDevPresets_StandOnTheirRungs()
        {
            // The bench's six keys (2026-09-21): each parks the bar on its rung with the calendar, the book and the
            // lines of a bar that got there — and never on a rung above it.
            double[] stars = { 0.5, 1.0, 2.0, 3.0, 4.0, 5.0 };
            int lastDay = 0;
            foreach (double s in stars)
            {
                var run = NewRunWith(0.0, "preset-" + s, Tower());
                run.DevPresetStars(s);
                Assert.AreEqual(BarRank.Of(s).Index, run.Rank.Index, s + " stars stands on its own rung");
                Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);
                Assert.Greater(run.Day, lastDay, "the calendar climbs with the rungs");
                lastDay = run.Day;
                Assert.AreEqual(BarRank.DraughtLines(s), run.TapLevel, "the lines the rung brings, on the one tower");
                Assert.AreEqual(s >= 2.0, run.SpoonUnlocked);
                Assert.IsEmpty(run.RecipesOpeningAt(s), "every page this standing has earned is in the book");
            }
        }

        [Test]
        public void TheCertificate_ListsWhatTheRungBringsToTheShop()
        {
            // The certificate's tiles (2026-09-21): a rung's bottles and pages are the ones whose star gate is
            // exactly the rung's - a tier-2 bottle and a rank-12 page both open at two stars, and nowhere else.
            var book = new List<RecipeDefinition>(Book)
            {
                new RecipeDefinition("stirred_page", "Stirred Page", rank: 12, baseFlavor: 6, baseMult: 1,
                    flavorPerLevel: 0, multPerLevel: 0,
                    requirements: Array.Empty<PatternRequirement>(),
                    ratioRequirements: new[]
                    {
                        new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                        new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                    },
                    minFill: 0.5, locked: true),
            };
            var run = new TycoonRun(NewShelf(), book, new RunRng("ladder-tiles"),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                brandCatalogue: new[]
                {
                    new IngredientCard("vodka_mid", "Mid Vodka", IngredientType.Spirit, 5,
                        new IngredientInfo("vodka", 2, 6, "somewhere", 40, "test")),
                });
            // The bottle's gate is the tier-and-price ladder's answer (a cheap tier-2 bottle sits on the one-star
            // rung since 2026-08-10); the page's is the rank table's. Both are asked, never assumed.
            double bottleGate = Market.RequiredStars(2, 6);
            Assert.AreEqual(2.0, run.RecipeStarGate(book[1]), 1e-9, "a rank-12 page opens at two stars");
            CollectionAssert.AreEqual(new[] { "vodka_mid" }, run.BottlesOpeningAt(bottleGate).Select(c => c.Id).ToList());
            Assert.IsEmpty(run.BottlesOpeningAt(bottleGate + 1.0), "and at no other rung");
            Assert.IsEmpty(run.BottlesOpeningAt(bottleGate - 0.5));
            CollectionAssert.AreEqual(new[] { "stirred_page" }, run.RecipesOpeningAt(2.0).Select(r => r.Id).ToList());
            Assert.IsEmpty(run.RecipesOpeningAt(1.0), "nothing of the book's at one star");
            Assert.IsEmpty(run.RecipesOpeningAt(3.0), "and nothing at three");
        }

        // ── the door ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void BelowTheSecondRung_NobodyIsAMinor_AndTheKickRefuses()
        {
            var run = NewRun(0.5, "ladder-door-shut", people: true);
            Assert.IsFalse(run.Has(Feature.Door));
            int met = 0;
            for (int night = 0; night < 12 && run.Phase == TycoonPhase.DayOpen; night++)
            {
                int guard = 0;
                CustomerVisit someone = null;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 4000, "the night must end");
                    run.Tick(5);
                    foreach (var v in run.Floor.Seated.ToList())
                    {
                        if (v.State != VisitState.Waiting || !v.HasOrdered || v.IdInspected) continue;
                        v.InspectId();
                        met++;
                        Assert.IsTrue(v.Papers == null || !v.Papers.ShouldBeKicked,
                            "before the door is the bar's, everyone who walks in is honest");
                        if (someone == null)
                        {
                            someone = v;
                            // The verb itself refuses — not "read the card first", the door is simply not yours.
                            var ex = Assert.Throws<InvalidOperationException>(() => run.Kick(v));
                            StringAssert.Contains("not yours", ex.Message);
                        }
                        run.DeclineOrder(v);
                    }
                }
                Assert.AreEqual(0, run.MinorsMet, "the papers stream rolled nobody underage");
                if (run.Phase == TycoonPhase.DayEnd) run.ContinueToNextDay();
            }
            Assert.Greater(met, 20, "enough faces read to mean something");
        }

        [Test]
        public void OnTheSecondRung_TheDoorIsYours()
        {
            var run = NewRun(1.0, "ladder-door-open", people: true);
            Assert.IsTrue(run.Has(Feature.Door));
            // The guards that were always there still stand in their order: read the card first.
            int guard = 0;
            CustomerVisit v = null;
            while (v == null)
            {
                Assert.Less(guard++, 4000, "somebody must walk in");
                run.Tick(5);
                v = run.Floor.Seated.FirstOrDefault(x => x.State == VisitState.Waiting && x.HasOrdered);
            }
            var ex = Assert.Throws<InvalidOperationException>(() => run.Kick(v));
            StringAssert.Contains("Read the card", ex.Message);
        }

        // ── the rank itself ───────────────────────────────────────────────────────────────────────

        [Test]
        public void TheRank_IsAHighWaterMark_ABadNightDoesNotTakeTheSpoonBack()
        {
            var run = NewRun(2.0, "ladder-water");
            Assert.AreEqual(3, run.Rank.Index);
            Assert.IsTrue(run.SpoonUnlocked);
            // An empty night files zero and drags the standing down a step; the rank stays where it was.
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 4000); run.Tick(30); }
            run.ContinueToNextDay();
            Assert.Less(run.Rating.Average, 2.0, "the standing fell");
            Assert.AreEqual(2.0, run.Rating.BestStanding, 1e-9, "the mark did not");
            Assert.AreEqual(3, run.Rank.Index);
            Assert.IsTrue(run.SpoonUnlocked, "what a rung opened stays open");
        }

        [Test]
        public void RankAfterTonight_PreviewsTheClimb_WithoutFilingIt()
        {
            var run = NewRun(0.0, "ladder-preview");
            Assert.AreEqual(0, run.RankAfterTonight.Index, "by day, the preview is the rank");
            // A night that never opened its doors files nothing: the preview cannot promise a rung the books
            // will refuse. (A climbing night is played out in the HUD's own tests; here the contract is that
            // the two read the same number.)
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen) { Assert.Less(guard++, 4000); run.Tick(30); }
            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            var preview = run.RankAfterTonight;
            run.ContinueToNextDay();
            Assert.AreEqual(preview.Index, run.Rank.Index, "the bill's preview and the close agree");
        }

        [Test]
        public void DevPreset_MovesTheMarkWithTheStanding()
        {
            var run = NewRun(0.0, "ladder-preset");
            run.Rating.DevSet(2.6);
            Assert.AreEqual(3, run.Rank.Index, "a parked mid bar stands on the third rung");
            run.Rating.DevSet(1.0);
            Assert.AreEqual(3, run.Rank.Index, "parking it lower does not lower the mark");
        }
    }
}
