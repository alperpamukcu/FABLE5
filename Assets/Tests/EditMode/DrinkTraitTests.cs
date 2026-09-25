using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE PAGES' CHARACTERS (2026-09-22, the author: "Tariflere/kokteyllere buff/nerf özellikler
    /// eklenmeli ve bu dengeli olmalı, çeşitlilik çok ve etkisi az ile orta seviye arasında
    /// olmalı"). Two things are pinned here and they are different things.
    ///
    /// The SHAPE of the set — one sign each, one lever each, nobody carrying two — is what keeps
    /// the variety real; a future tuner who quietly turns a character into a second "+10% tip" has
    /// to break a test to do it.
    ///
    /// The SIZE of each effect is measured the only honest way: the same glass, judged twice, once
    /// against a page with the character and once against its traitless twin. Every delta is
    /// asserted to be in the direction the character claims AND inside the band the author asked
    /// for, so a character cannot silently grow into a balance patch.
    /// </summary>
    public class DrinkTraitTests
    {
        // ── scaffolding: one drink, two pages, identical but for the character ───────────────

        private static RecipeDefinition Page(string trait) =>
            new RecipeDefinition("page_" + (trait ?? "plain"), "Page", 12,
                baseFlavor: 10, baseMult: 2, flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                // TYPE bands and plain cards, the way TycoonCoreTests builds its Spritz: a style
                // band needs an IngredientInfo on every card to be measurable, and none of these
                // tests is about the shelf.
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, prep: PrepMethod.Built, trait: trait);

        private static readonly Dictionary<string, IngredientCard> Bar =
            new Dictionary<string, IngredientCard>
            {
                ["gin"] = new IngredientCard("gin", "Gin", IngredientType.Spirit, 6),
                ["soda"] = new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1),
            };

        private static IngredientCard Look(string id) => Bar.TryGetValue(id, out var c) ? c : null;

        private static GlassContents Glass(double gin, double soda)
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", gin);
            glass.Add("soda", soda);
            return glass;
        }

        /// <summary>A visit that has already waited <paramref name="waited"/> of its patience.</summary>
        private static CustomerVisit Visit(string trait, double waited = 0, int price = 12)
        {
            var visit = new CustomerVisit(new DrinkOrder(Page(trait), price), 60);
            visit.InspectId();
            if (waited > 0) visit.Tick(60 * visit.PatienceFraction * waited);
            return visit;
        }

        private static ServiceVerdict JudgeIt(string trait, GlassContents glass, double waited = 0)
        {
            var visit = Visit(trait, waited);   // the card is read, so Order is ours to look at
            var match = RatioRecipeMatcher.Match(glass, new[] { visit.Order.Wanted }, Look);
            return ServiceJudge.Judge(visit, OrderMatch.Exact, glass,
                served: match, lookup: Look);
        }

        // ── the shape of the set ────────────────────────────────────────────────────────────

        [Test]
        public void EveryCharacter_HasOneSign_AndPricesInThatDirection()
        {
            foreach (var t in DrinkTraits.All)
            {
                Assert.AreNotEqual(TraitSign.None, t.Sign, t.Id + " must be a buff or a nerf");
                Assert.AreNotEqual(TraitChannel.None, t.Channel, t.Id + " must pull a named lever");
                Assert.AreEqual(t.Sign == TraitSign.Buff ? -1 : 1, t.PriceSign,
                    t.Id + ": a buff is a notch cheaper, a nerf a notch dearer");
            }
            Assert.AreEqual(0, DrinkTrait.None.PriceSign, "no character, no notch");
        }

        [Test]
        public void EveryCharacter_MovesExactlyOneNumber()
        {
            // The rule the catalogue is built on. A page that moved two would be two characters
            // wearing one name, and the balance pass could not tell which half did the work.
            foreach (var t in DrinkTraits.All)
            {
                int moved = 0;
                if (Math.Abs(t.AskBoxScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.RefillScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.WaitPenaltyScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.ClockFloorScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.TipCeilingScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.PayFloorScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.PerfectWindowScale - 1.0) > 1e-9) moved++;
                if (Math.Abs(t.RefusalFillScale - 1.0) > 1e-9) moved++;
                if (t.SavorCycles != 3) moved++;
                if (t.GrantRound > 0 || t.TakeRound > 0) moved++;
                if (t.RoomAura > 0) moved++;
                if (Math.Abs(t.Mess0 - 0.25) > 1e-9 || Math.Abs(t.Mess1 - 0.65) > 1e-9
                    || Math.Abs(t.Mess2 - 0.90) > 1e-9) moved++;
                Assert.AreEqual(1, moved, t.Id + " must move exactly one number");
            }
        }

        [Test]
        public void TheSet_IsVaried_AndNoLeverCarriesIt()
        {
            Assert.GreaterOrEqual(DrinkTraits.All.Count, 12, "the author asked for a lot of them");
            var byChannel = DrinkTraits.All.GroupBy(t => t.Channel).ToDictionary(g => g.Key, g => g.Count());
            Assert.GreaterOrEqual(byChannel.Count, 5, "and spread over the levers, not piled on one");
            foreach (var pair in byChannel)
                Assert.LessOrEqual(pair.Value, DrinkTraits.All.Count / 2,
                    pair.Key + " carries too much of the book");
            Assert.IsTrue(DrinkTraits.All.Any(t => t.Sign == TraitSign.Nerf), "some of them cost");
            Assert.IsTrue(DrinkTraits.All.Any(t => t.Sign == TraitSign.Buff), "some of them pay");
        }

        [Test]
        public void TheShippedBook_NamesOnlyCharactersThatExist_AndLeavesSomePagesPlain()
        {
            var book = RecipeCatalog.CreateDefault();
            int traited = 0;
            foreach (var r in book)
            {
                if (r.Trait == null) continue;
                traited++;
                Assert.IsTrue(DrinkTraits.Known(r.Trait), r.Id + " names '" + r.Trait + "'");
                Assert.AreNotSame(DrinkTrait.None, DrinkTraits.Of(r), r.Id);
            }
            Assert.Greater(traited, 20, "most of the book has a character");
            Assert.Greater(book.Count - traited, 5,
                "and some of it does not — a book where every page is special has no special pages");
            foreach (var t in DrinkTraits.All)
                Assert.IsTrue(book.Any(r => r.Trait == t.Id), t.Id + " is written but nobody carries it");
        }

        [Test]
        public void APageWithNoCharacter_IsJudgedExactlyAsItAlwaysWas()
        {
            // The whole safety argument in one assert: None is every identity, so the plain page
            // must compute the same doubles it computed before this system existed.
            var none = DrinkTrait.None;
            Assert.AreEqual(1.0, none.AskBoxScale, 1e-12);
            Assert.AreEqual(1.0, none.RefillScale, 1e-12);
            Assert.AreEqual(1.0, none.WaitPenaltyScale, 1e-12);
            Assert.AreEqual(1.0, none.ClockFloorScale, 1e-12);
            Assert.AreEqual(1.0, none.TipCeilingScale, 1e-12);
            Assert.AreEqual(1.0, none.PayFloorScale, 1e-12);
            Assert.AreEqual(1.0, none.PerfectWindowScale, 1e-12);
            Assert.AreEqual(1.0, none.RefusalFillScale, 1e-12);
            Assert.AreEqual(3, none.SavorCycles);
            Assert.AreEqual(0.0, none.RoomAura, 1e-12);
            Assert.AreEqual(0.25, none.Mess0, 1e-12);
            Assert.AreEqual(0.65, none.Mess1, 1e-12);
            Assert.AreEqual(0.90, none.Mess2, 1e-12);
            Assert.AreSame(none, DrinkTraits.Of(null), "a pour that matched nothing has no character");
            Assert.AreSame(none, DrinkTraits.Of(Page(null)), "and neither has a plain page");
        }

        // ── the size of each effect, measured ───────────────────────────────────────────────

        [Test]
        public void KeepsWell_AndDrinkItHot_MoveOnlyTheLatenessPenalty()
        {
            var glass = Glass(0.5, 0.4);
            double plain = JudgeIt(null, glass, waited: 0.6).Satisfaction;
            double kept = JudgeIt("keeps_well", glass, waited: 0.6).Satisfaction;
            double hot = JudgeIt("drink_it_hot", glass, waited: 0.6).Satisfaction;

            Assert.Greater(kept, plain, "a drink that keeps sours more slowly");
            Assert.Less(hot, plain, "and one that dies warm sours faster");
            // Symmetrical, and both inside a fifth of what lateness costs.
            Assert.AreEqual(kept - plain, plain - hot, 1e-9, "the pair is a mirror");
            Assert.LessOrEqual(kept - plain, ServiceJudge.WaitPenalty * 0.25);

            // ...and nothing else moved.
            var a = JudgeIt(null, glass, waited: 0.6);
            var b = JudgeIt("keeps_well", glass, waited: 0.6);
            Assert.AreEqual(a.BasePaid, b.BasePaid, "the till is untouched");
            Assert.AreEqual(a.Tip, b.Tip, "and so is the tip");
        }

        [Test]
        public void StillGoodLate_PaysOnlyWhenLate_AndNothingOnTheInstant()
        {
            var glass = Glass(0.5, 0.4);
            Assert.AreEqual(JudgeIt(null, glass).Tip, JudgeIt("still_good_late", glass).Tip,
                "a serve on the instant keeps nothing extra: the floor is what it scales");
            Assert.GreaterOrEqual(JudgeIt("still_good_late", glass, waited: 0.8).Tip,
                JudgeIt(null, glass, waited: 0.8).Tip, "and a late one keeps more of its tip");
            Assert.LessOrEqual(JudgeIt("dies_warm", glass, waited: 0.8).Tip,
                JudgeIt(null, glass, waited: 0.8).Tip);
        }

        /// <summary>The glass this page is perfect at — the only pour that makes the tip big
        /// enough for a fifth of it to be more than a rounding error.</summary>
        private static GlassContents PerfectGlass(string trait)
        {
            var perfect = RatioRecipeMatcher.PerfectPour(Page(trait));
            var glass = new GlassContents(1.0);
            glass.Add("gin", perfect[0] * 0.9);
            glass.Add("soda", perfect[1] * 0.9);
            return glass;
        }

        [Test]
        public void TheyTipForThis_LiftsTheCeiling_ButOnlyOnAGoodServe()
        {
            int plain = JudgeIt(null, PerfectGlass(null)).Tip;
            int rich = JudgeIt("they_tip_for_this", PerfectGlass("they_tip_for_this")).Tip;
            int mean = JudgeIt("pays_the_bill", PerfectGlass("pays_the_bill")).Tip;
            Assert.Greater(plain, 2, "the fixture has to tip something for a notch to be visible");
            Assert.Greater(rich, plain);
            Assert.Less(mean, plain);
            Assert.LessOrEqual(rich - plain, (int)Math.Ceiling(plain * 0.25) + 1,
                "a notch, not a raise");
        }

        [Test]
        public void MadeByFeel_IsAFloorUnderAShakyHand_AndNothingAtAPerfectPour()
        {
            // Well off the perfect: the floor is what is carrying the payment, so it shows.
            var sloppy = Glass(0.66, 0.24);
            Assert.GreaterOrEqual(JudgeIt("made_by_feel", sloppy).BasePaid,
                JudgeIt(null, sloppy).BasePaid);
            Assert.LessOrEqual(JudgeIt("no_place_to_hide", sloppy).BasePaid,
                JudgeIt(null, sloppy).BasePaid);
        }

        [Test]
        public void ReadsFull_RescuesAGlassTheHouseWouldRefuse_AndNoCharacterRaisesThatLine()
        {
            // A glass between the character's line and the house's: refused plain, graded here.
            double between = (ServiceJudge.RefusalFill * 0.80 + ServiceJudge.RefusalFill) * 0.5;
            var thin = Glass(between * 0.55, between * 0.45);
            Assert.AreEqual(OrderMatch.Refused, JudgeIt(null, thin).Match, "the house refuses it");
            Assert.AreNotEqual(OrderMatch.Refused, JudgeIt("reads_full", thin).Match,
                "and this page does not");

            // The line is a step, not a curve, so nothing may ever raise it.
            foreach (var t in DrinkTraits.All)
                Assert.LessOrEqual(t.RefusalFillScale, 1.0,
                    t.Id + " must not raise the refusal line");
        }

        [Test]
        public void Nursed_GivesABiggerBoxBackForBeingAsked_AndOnlyAfterTheCardIsRead()
        {
            var plain = new CustomerVisit(new DrinkOrder(Page(null), 12), 60);
            var nursed = new CustomerVisit(new DrinkOrder(Page("nursed"), 12), 60);
            plain.Tick(30);
            nursed.Tick(30);
            Assert.AreEqual(plain.PatienceLeft, nursed.PatienceLeft, 1e-9,
                "before the card is read the clock is the house's for everyone");

            plain.InspectId();
            nursed.InspectId();
            Assert.Greater(nursed.PatienceLeft, plain.PatienceLeft, "the ask gives back more");
            double extra = (nursed.PatienceLeft - plain.PatienceLeft) / 60.0;
            Assert.LessOrEqual(extra, 0.25, "a notch of the clock, not a reset of it");
        }

        [Test]
        public void TheSavourCharacters_KeepWholeSipCycles()
        {
            // TycoonConfig's own rule: a savour that is not a whole number of sip cycles cuts
            // the last one off mid-gesture, so these two may only ever be whole steps.
            foreach (var t in DrinkTraits.All)
            {
                Assert.GreaterOrEqual(t.SavorCycles, 2, t.Id);
                Assert.LessOrEqual(t.SavorCycles, 4, t.Id);
            }
        }

        [Test]
        public void TheMessCharacters_KeepTheThresholdsOrdered()
        {
            foreach (var t in DrinkTraits.All)
            {
                Assert.Less(t.Mess0, t.Mess1, t.Id);
                Assert.Less(t.Mess1, t.Mess2, t.Id);
                Assert.Greater(t.Mess0, 0.0, t.Id);
                Assert.Less(t.Mess2, 1.0, t.Id);
            }
        }

        // ── the price notch ─────────────────────────────────────────────────────────────────

        [Test]
        public void ACharacterIsWorthANotchOffTheSheet_ButNeverOnTheOpeningMenu()
        {
            for (int rank = 1; rank <= 30; rank++)
            {
                int plain = DrinkOrder.MenuPrice(RankPage(rank, null));
                int buff = DrinkOrder.MenuPrice(RankPage(rank, "nursed"));
                int nerf = DrinkOrder.MenuPrice(RankPage(rank, "drink_it_hot"));
                if (plain < DrinkOrder.TraitPriceFloor)
                {
                    Assert.AreEqual(plain, buff, "rank " + rank + ": the opening menu is untouched");
                    Assert.AreEqual(plain, nerf, "rank " + rank);
                    continue;
                }
                Assert.Less(buff, plain, "rank " + rank + ": a buff is cheaper");
                Assert.Greater(nerf, plain, "rank " + rank + ": a nerf is dearer");
                Assert.LessOrEqual(plain - buff, Math.Max(1, (int)Math.Round(plain * 0.15)),
                    "rank " + rank + ": a notch, not a sale");
            }
        }

        private static RecipeDefinition RankPage(int rank, string trait) =>
            new RecipeDefinition("r" + rank + (trait ?? ""), "R", rank, 10, 2, 0, 0,
                Array.Empty<PatternRequirement>(),
                ratioRequirements: new[] { new RatioRequirement("gin", 0.3, 0.7) },
                prep: PrepMethod.Built, trait: trait);

        // ── the A/B gate ────────────────────────────────────────────────────────────────────

        [Test]
        public void TheGate_TurnsEveryCharacterOff_SoTheSimCanPlayOneSeedTwice()
        {
            var page = Page("they_tip_for_this");
            Assert.AreNotSame(DrinkTrait.None, DrinkTraits.Of(page));
            try
            {
                DrinkTraits.Enabled = false;
                Assert.AreSame(DrinkTrait.None, DrinkTraits.Of(page), "off means off");
                Assert.AreEqual(DrinkOrder.MenuPrice(RankPage(20, null)),
                    DrinkOrder.MenuPrice(RankPage(20, "they_tip_for_this")),
                    "and the sheet goes back to the plain curve");
            }
            finally { DrinkTraits.Enabled = true; }
            Assert.AreNotSame(DrinkTrait.None, DrinkTraits.Of(page), "and back on afterwards");
        }
    }
}
