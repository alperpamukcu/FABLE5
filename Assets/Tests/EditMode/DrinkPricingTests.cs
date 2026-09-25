using System;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// WHAT A DRINK IS WORTH (2026-09-23, the author's economy brief: *"Kokteyl fiyatları
    /// yıldızlara göre değişmeli ... Her yıldız seviyesinin kolay-orta-zor kokteylleri olacak ...
    /// aynı yıldız grubunda hepsinin fiyatı aynı olmayacak ama gruplar arası fiyat farkı olacak"*).
    ///
    /// The price was one straight line through the whole book and then everything was multiplied by
    /// the bar's standing. What is pinned here is the SHAPE that replaced it, because the figures
    /// themselves are going to move once the 200-run sim has been read: the bands rise, they do not
    /// overlap, every page lands inside its own, and a page is worth what the PAGE is worth rather
    /// than what the bar happens to be standing at.
    /// </summary>
    public sealed class DrinkPricingTests
    {
        private static RecipeDefinition Page(int rank, int pours = 2, PrepMethod prep = PrepMethod.Built,
            string trait = null)
        {
            var bands = new RatioRequirement[pours];
            for (int i = 0; i < pours; i++)
                bands[i] = new RatioRequirement(i == 0 ? IngredientType.Spirit : IngredientType.Bubbly,
                    0.1, 0.9);
            return new RecipeDefinition("p" + rank + "_" + pours + "_" + prep + (trait ?? ""), "P",
                rank, 10, 2, 0, 0, Array.Empty<PatternRequirement>(),
                ratioRequirements: bands, prep: prep, trait: trait);
        }

        [Test]
        public void TheOpeningMenu_IsTheAuthorsOwnFigures()
        {
            // *"0 yıldız: Kolay 3-6$ / Orta 7-10$ / Zor 11-12$"* — quoted to the dollar, because
            // the first night's economy is the fragile one and these are the numbers it was
            // designed against.
            Assert.AreEqual((3, 6), DrinkPricing.BandOfRung(0, DrinkDifficulty.Easy));
            Assert.AreEqual((7, 10), DrinkPricing.BandOfRung(0, DrinkDifficulty.Medium));
            Assert.AreEqual((11, 12), DrinkPricing.BandOfRung(0, DrinkDifficulty.Hard));
        }

        [Test]
        public void TheBands_RiseWithTheRung_AndWithTheWork()
        {
            for (int rung = 0; rung < DrinkPricing.Rungs; rung++)
            {
                var easy = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Easy);
                var med = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Medium);
                var hard = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Hard);

                Assert.Less(easy.Lo, easy.Hi, rung + "*: a band is a range, not a number");
                Assert.Less(easy.Hi, med.Lo, rung + "*: easy and medium must not overlap");
                Assert.Less(med.Hi, hard.Lo, rung + "*: medium and hard must not overlap");

                if (rung == 0) continue;
                // EACH COLUMN RISES, and that is the rule — not "the whole rung is dearer". A hard
                // drink is worth more than an easy one whatever rung it is on, so a three-star EASY
                // page is allowed to cost less than a two-star HARD one. What climbing buys is
                // ACCESS to dearer pages, which the columns guarantee.
                foreach (var work in new[] { DrinkDifficulty.Easy, DrinkDifficulty.Medium, DrinkDifficulty.Hard })
                {
                    var here = DrinkPricing.BandOfRung(rung, work);
                    var under = DrinkPricing.BandOfRung(rung - 1, work);
                    Assert.Greater(here.Lo, under.Lo, rung + "* " + work + ": the floor must rise");
                    Assert.Greater(here.Hi, under.Hi, rung + "* " + work + ": and so must the ceiling");
                }
            }
        }

        [Test]
        public void EveryPageInTheBook_LandsInsideItsOwnBand()
        {
            foreach (var r in RecipeCatalog.CreateDefault())
            {
                int rung = DrinkPricing.RungOf(r);
                var band = DrinkPricing.BandOfRung(rung, RecipeDifficulty.Of(r));
                int sheet = DrinkPricing.SheetPrice(r);
                Assert.GreaterOrEqual(sheet, band.Lo, r.Id + " (" + rung + "*)");
                Assert.LessOrEqual(sheet, band.Hi, r.Id + " (" + rung + "*)");
            }
        }

        [Test]
        public void InsideARung_TheLaterPageIsTheDearerPage()
        {
            // *"aynı yıldız grubunda hepsinin fiyatı aynı olmayacak"* — and the order inside the
            // rung is the book's own order, so the spread is a fact about the page rather than a
            // sprinkle of noise.
            Assert.Less(DrinkPricing.SheetPrice(Page(1)), DrinkPricing.SheetPrice(Page(8)));
            Assert.Less(DrinkPricing.SheetPrice(Page(15)), DrinkPricing.SheetPrice(Page(21)));
            Assert.AreEqual(DrinkPricing.SheetPrice(Page(15)), DrinkPricing.SheetPrice(Page(15)),
                "and it is the same figure every time it is asked");
        }

        [Test]
        public void ClimbingBeatsGrinding()
        {
            // The rule that keeps the menu a ladder: the dearest drink a rung can sell must not
            // out-earn the MIDDLE of the rung above it. Grinding the hard pages of the rung you are
            // on is then always worth less than buying up and pouring its ordinary ones — which is
            // the whole reason to climb. (A higher rung's EASY page may well be cheaper; a hard
            // drink is hard wherever it is poured.)
            for (int rung = 1; rung < DrinkPricing.Rungs; rung++)
                Assert.LessOrEqual(DrinkPricing.BandOfRung(rung - 1, DrinkDifficulty.Hard).Hi,
                    DrinkPricing.BandOfRung(rung, DrinkDifficulty.Medium).Hi,
                    "rung " + (rung - 1) + " hard must not out-earn rung " + rung + " medium");
        }

        [Test]
        public void NoDrinkComesNearFourHundred()
        {
            // The author, 2026-09-23: "bir kokteyl 400 dolar olmamalı." The band table is cut so
            // the shipped book cannot approach it, and the ceiling is the backstop for pages
            // nobody has written yet.
            Assert.Less(DrinkPricing.CeilingPerDrink, 400);
            foreach (var r in RecipeCatalog.CreateDefault())
                Assert.Less(DrinkOrder.MenuPrice(r) * 4, DrinkPricing.CeilingPerDrink,
                    r.Id + ": even four times its menu price must stay under the ceiling");
        }

        [Test]
        public void ACharactersNotch_MovesThePriceButNeverTheOpeningMenu()
        {
            // The trait notch rides on top of the band, last and once. The zero-star row sits under
            // DrinkOrder.TraitPriceFloor, so the opening menu is priced exactly as the brief wrote
            // it whatever characters those pages carry.
            foreach (var r in RecipeCatalog.CreateDefault())
            {
                int sheet = DrinkPricing.SheetPrice(r);
                int menu = DrinkOrder.MenuPrice(r);
                if (sheet < DrinkOrder.TraitPriceFloor)
                {
                    Assert.AreEqual(sheet, menu, r.Id + ": the opening menu takes no notch");
                    continue;
                }
                int notch = Math.Abs(menu - sheet);
                Assert.LessOrEqual(notch, (int)Math.Ceiling(sheet * 0.15),
                    r.Id + ": a notch, not a sale");
                int sign = DrinkTraits.Of(r).PriceSign;
                if (sign == 0) Assert.AreEqual(sheet, menu, r.Id + ": no character, no notch");
                else Assert.AreEqual(sign, Math.Sign(menu - sheet), r.Id);
            }
        }

        [Test]
        public void ThePriceBelongsToThePage_NotToTheBarsStanding()
        {
            // The whole point of the rewrite, stated once: nothing about the price reads a standing.
            // (TycoonRun.PriceOf adds the crowd's mood and the shelf's premium on top, and
            // StarEconomyTests holds that the LANDLORD still reads the stage.)
            var page = Page(2);
            int first = DrinkOrder.MenuPrice(page);
            Assert.AreEqual(first, DrinkOrder.MenuPrice(page), "pure, and a pure function of the page");
            Assert.AreEqual(DrinkPricing.SheetPrice(page), first);
        }

        [Test]
        public void EveryRung_HasARankRangeAndNoPageFallsBetweenTwo()
        {
            var seen = RecipeCatalog.CreateDefault().Select(DrinkPricing.RungOf).Distinct().OrderBy(x => x).ToList();
            Assert.AreEqual(0, seen.First(), "the book starts on the ground");
            Assert.AreEqual(DrinkPricing.Rungs - 1, seen.Last(), "and reaches the top rung");
            for (int i = 1; i < seen.Count; i++)
                Assert.AreEqual(seen[i - 1] + 1, seen[i], "no rung in the book is empty");
        }
    }
}
