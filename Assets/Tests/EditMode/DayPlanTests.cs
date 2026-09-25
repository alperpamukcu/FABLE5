using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// WHAT THE NIGHT ASKS FOR (2026-09-23, the author's economy brief: *"tamamen rastgele değil
    /// bir düzen içerisinde rastgele olmalı, her şeyi rastgele yapıp oyuncuya kötü şans sonucu kötü
    /// bir oynanış sunmak istemiyorum"*).
    ///
    /// The promise this file holds is narrow and it is the whole point of <see cref="DayPlan"/>: two
    /// nights at the same standing on the same menu ask for THE SAME DRINKS in a different order.
    /// Everything else here — the rung tail, the difficulty ramp, the cap on one page — is the shape
    /// of that composition, and each of them is a line of the brief.
    /// </summary>
    public sealed class DayPlanTests
    {
        private static SeededRng Rng(string seed) => new RunRng(seed).GetStream("plan");

        private static IReadOnlyList<RecipeDefinition> Book(double stars) =>
            RecipeCatalog.CreateDefault()
                .Where(r => r.RatioRequirements.Count > 0)
                .Where(r => r.Rank <= Gate(stars))
                .ToList();

        /// <summary>The same split TycoonRun.RecipeStarGate makes, so these plans are cut from a
        /// menu a bar at that standing could really own.</summary>
        private static int Gate(double stars) =>
            stars < 1 ? 8 : stars < 2 ? 11 : stars < 3 ? 14 : stars < 4 ? 21 : stars < 5 ? 29 : 30;

        private static Dictionary<int, int> ByRung(DayPlan plan, double stars)
        {
            int bar = (int)Math.Floor(stars);
            var counts = new Dictionary<int, int>();
            foreach (var r in plan.Queue)
            {
                int rung = Math.Min(bar, DrinkPricing.RungOf(r));
                counts[rung] = counts.TryGetValue(rung, out int n) ? n + 1 : 1;
            }
            return counts;
        }

        [Test]
        public void TheSameNight_IsTheSameNight()
        {
            var a = DayPlan.Roll(Book(2), 6, 2.0, TycoonConfig.Default, Rng("steady"));
            var b = DayPlan.Roll(Book(2), 6, 2.0, TycoonConfig.Default, Rng("steady"));
            CollectionAssert.AreEqual(a.Queue.Select(r => r.Id).ToList(),
                b.Queue.Select(r => r.Id).ToList(), "a seed is a night");
        }

        [Test]
        public void TwoSeeds_PourTheSameDrinksInADifferentOrder()
        {
            // THE PROMISE. Luck decides who walks in when; it does not decide what the night is
            // worth. Under the old uniform roll these two lists could differ by half the menu.
            var a = DayPlan.Roll(Book(3), 9, 3.0, TycoonConfig.Default, Rng("one"));
            var b = DayPlan.Roll(Book(3), 9, 3.0, TycoonConfig.Default, Rng("two"));

            CollectionAssert.AreEquivalent(a.Queue.Select(r => r.Id).ToList(),
                b.Queue.Select(r => r.Id).ToList(), "the same covers");
            Assert.AreNotEqual(string.Join(",", a.Queue.Select(r => r.Id)),
                string.Join(",", b.Queue.Select(r => r.Id)), "in a different order");
        }

        [Test]
        public void ANightsTakings_DoNotSwingOnTheDice()
        {
            // Measured rather than asserted by construction: the sheet value of a night, across
            // twenty seeds, must not move. This is the number the brief is actually about.
            var takes = Enumerable.Range(0, 20)
                .Select(i => DayPlan.Roll(Book(3), 9, 3.0, TycoonConfig.Default, Rng("n" + i)))
                .Select(p => p.Queue.Sum(DrinkOrder.MenuPrice))
                .ToList();
            Assert.AreEqual(takes.Min(), takes.Max(),
                "the same composition is the same money, whoever walks in first");
        }

        [Test]
        public void TheOpeningNight_AsksNothingHard()
        {
            // *"oyuncu oyundan bezdirilmesin"* — the first shift is the one that decides whether
            // there is a second. Eighty-five per cent of it is the easy column and none of it is
            // the hard one.
            var plan = DayPlan.Roll(Book(0), 1, 0.0, TycoonConfig.Default, Rng("opening"));
            Assert.IsFalse(plan.Queue.Any(r => RecipeDifficulty.Of(r) == DrinkDifficulty.Hard),
                "nothing hard is asked for on the ground floor");
            int easy = plan.Queue.Count(r => RecipeDifficulty.Of(r) == DrinkDifficulty.Easy);
            Assert.GreaterOrEqual(easy / (double)plan.Covers, 0.7, "and most of it is the easy column");
        }

        [Test]
        public void TheHardColumn_GrowsWithTheHouse()
        {
            // *"oyuncuya yüksek yıldızlı zor kokteyl yapmanın ödülü de olmalı"*: the reward is in
            // two places, and this is the second one — the room ASKS for the hard pages more often
            // as the bar climbs, so the skill has somewhere to go.
            double last = -1;
            for (int stars = 0; stars <= 5; stars++)
            {
                var plan = DayPlan.Roll(Book(stars), 5 + 3 * stars, stars,
                    TycoonConfig.Default, Rng("ramp" + stars));
                double hard = plan.Queue.Count(r => RecipeDifficulty.Of(r) == DrinkDifficulty.Hard)
                              / (double)plan.Covers;
                Assert.GreaterOrEqual(hard, last - 0.02, stars + "*: the hard share never falls");
                last = hard;
            }
            Assert.Greater(last, 0.2, "and by five stars it is most of a third of the night");
        }

        [Test]
        public void TheNewestRung_IsMostOfTheNight_AndTheOldOnesSurvive()
        {
            // The shape that pays for climbing: buying up moves the WHOLE night's takings, because
            // the pages of the rung you just reached are the ones the room mostly asks for. The
            // tail is what stops it being a cliff — the opening menu is still poured.
            var plan = DayPlan.Roll(Book(3), 12, 3.0, TycoonConfig.Default, Rng("rungs"));
            var counts = ByRung(plan, 3.0);
            Assert.Greater(counts.GetValueOrDefault(3), counts.GetValueOrDefault(2),
                "the newest rung is the busiest");
            Assert.Greater(counts.GetValueOrDefault(2), 0, "and the one under it is still poured");
            Assert.Greater(counts.GetValueOrDefault(0), 0, "and so is the bar it opened as");
            Assert.AreEqual(plan.Covers, counts.Values.Sum());
        }

        [Test]
        public void NoOnePage_IsMoreThanAFifthOfTheNight()
        {
            // A rung with one page in it would otherwise eat that rung's whole share, and the game
            // would read as nagging for the drink you just bought.
            foreach (int stars in new[] { 1, 2, 3, 4 })
            {
                var plan = DayPlan.Roll(Book(stars), 8 + stars, stars, TycoonConfig.Default,
                    Rng("cap" + stars));
                int worst = plan.Queue.GroupBy(r => r.Id).Max(g => g.Count());
                int allowed = Math.Max(1, (int)Math.Ceiling(plan.Covers * DayPlan.PageCap)) + 1;
                Assert.LessOrEqual(worst, allowed,
                    stars + "*: no page is asked for more than the cap allows");
            }
        }

        [Test]
        public void EveryPageOnTheMenu_CanBeAskedFor()
        {
            // THE BUG THE PLAN WAS WRITTEN AGAINST. The old pool took the lowest-ranked 3 + day
            // pages, so a three-star bar on day four owned twenty-one recipes and was asked for
            // seven — all of them from its opening menu. Over a week, every page must appear.
            var book = Book(3);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int day = 8; day < 15; day++)
                foreach (var r in DayPlan.Roll(book, day, 3.0, TycoonConfig.Default, Rng("week" + day)).Queue)
                    seen.Add(r.Id);
            var missing = book.Select(r => r.Id).Where(id => !seen.Contains(id)).ToList();
            Assert.LessOrEqual(missing.Count, book.Count / 4,
                "a week asks for most of the book: never asked = " + string.Join(", ", missing));
        }

        [Test]
        public void TheNightIsCutForTheNightThatHappens()
        {
            // Covers come from the floor's own arithmetic, so the composition the plan promises is
            // the composition that is actually served rather than a number picked to look right.
            foreach (int day in new[] { 1, 5, 12, 30 })
            {
                int covers = DayPlan.CoversFor(day, 0.0, TycoonConfig.Default);
                double gap = TycoonConfig.Default.ArrivalGap(day, 0.0);
                Assert.AreEqual(Math.Max(DayPlan.MinCovers,
                        (int)Math.Round(TycoonConfig.Default.NightSeconds / gap,
                            MidpointRounding.AwayFromZero)), covers);
                Assert.GreaterOrEqual(covers, DayPlan.MinCovers);
            }
            Assert.Greater(DayPlan.CoversFor(20, 0.0, TycoonConfig.Default),
                DayPlan.CoversFor(1, 0.0, TycoonConfig.Default), "a busier bar sells more");
        }

        [Test]
        public void ABusyNight_OutlastsItsPlanWithoutFallingBackToDice()
        {
            var plan = DayPlan.Roll(Book(1), 4, 1.0, TycoonConfig.Default, Rng("long"));
            var first = Enumerable.Range(0, plan.Covers).Select(_ => plan.Take().Id).ToList();
            var second = Enumerable.Range(0, plan.Covers).Select(_ => plan.Take().Id).ToList();
            CollectionAssert.AreEqual(first, second,
                "working fast serves more of the same night, not a different one");
        }

        [Test]
        public void ABarWithOnePourablePage_StillHasANight()
        {
            var one = RecipeCatalog.CreateDefault().Where(r => r.Id == "draught").ToList();
            var plan = DayPlan.Roll(one, 1, 0.0, TycoonConfig.Default, Rng("lonely"));
            Assert.AreEqual(plan.Covers, plan.Queue.Count(r => r.Id == "draught"));
        }

        [Test]
        public void AnEmptyMenu_SaysSoInWords()
        {
            var thrown = Assert.Throws<InvalidOperationException>(() =>
                DayPlan.Roll(Array.Empty<RecipeDefinition>(), 1, 0.0, TycoonConfig.Default, Rng("none")));
            Assert.IsTrue(Said.TryGet(thrown, out var line));
            Assert.AreEqual("rule.no_pourable_drinks", line.Key);
        }

        // ── the written week (FirstWeek) ────────────────────────────────────────

        [Test]
        public void TheWrittenWeek_OnlyNamesPagesANewBarOwns()
        {
            // The week is authored, so this is the parity gate that keeps it honest: every id it
            // names must be a page the bar OPENS ITS DOORS owning, or the lesson silently drops a
            // cover the first time somebody edits recipes.json.
            var opening = RecipeCatalog.CreateDefault()
                .Where(r => !r.Locked && r.RatioRequirements.Count > 0)
                .Select(r => r.Id).ToList();
            foreach (var id in FirstWeek.PagesNamed)
                CollectionAssert.Contains(opening, id, "the written week names a page nobody owns");
        }

        [Test]
        public void TheWrittenWeek_TeachesOneThingANight()
        {
            // Night one is the builds and nothing else; each night after it adds exactly one page
            // the week has not asked for yet, and nothing ever disappears.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var book = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();
            for (int day = 1; day <= FirstWeek.Nights; day++)
            {
                var night = FirstWeek.For(day, 0.0, book, TycoonConfig.Default);
                Assert.IsNotNull(night, "day " + day + " is written");
                var ids = night.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
                int added = ids.Count(id => !seen.Contains(id));
                Assert.LessOrEqual(added, day == 1 ? 3 : 1, "day " + day + " adds one thing");
                seen.UnionWith(ids);
            }
            Assert.AreEqual(6, seen.Count, "and by the seventh night the whole opening menu is out");
        }

        [Test]
        public void TheWrittenWeek_PaysMoreEveryNight()
        {
            // *"oyuncu oyundan bezdirilmesin"*. A week where night two takes less than night one
            // reads as going backwards however good the play was, and the opening week is the one
            // that decides whether there is a second. Measured on the sheet, so it is a fact about
            // the WRITING rather than about how well anybody poured.
            var book = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();
            int last = 0;
            for (int day = 1; day <= FirstWeek.Nights; day++)
            {
                int take = FirstWeek.For(day, 0.0, book, TycoonConfig.Default)
                    .Sum(r => DrinkOrder.MenuPrice(r, 0.0));
                Assert.Greater(take, last, "night " + day + " must take more than the night before");
                last = take;
            }
            Assert.GreaterOrEqual(last, 60, "and the last written night is a real shift");
        }

        [Test]
        public void TheWrittenWeek_StandsDownWhenItStopsBeingTrue()
        {
            var book = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();
            Assert.IsNull(FirstWeek.For(FirstWeek.Nights + 1, 0.0, book, TycoonConfig.Default),
                "day eight is cut by the weights");
            Assert.IsNull(FirstWeek.For(3, 1.0, book, TycoonConfig.Default),
                "and so is a bar that climbed off the ground floor inside its first week");
            Assert.IsNotNull(DayPlan.Roll(book, 3, 1.0, TycoonConfig.Default, Rng("climbed")));
        }

        [Test]
        public void TheWrittenWeek_SurvivesAMenuMissingOneOfItsPages()
        {
            var thin = RecipeCatalog.CreateDefault()
                .Where(r => r.Id == "vodka_soda" || r.Id == "draught").ToList();
            var plan = DayPlan.Roll(thin, 4, 0.0, TycoonConfig.Default, Rng("thin"));
            Assert.AreEqual(FirstWeek.CoversOn(4), plan.Covers, "the night keeps its covers");
            CollectionAssert.IsSubsetOf(plan.Queue.Select(r => r.Id).Distinct().ToList(),
                thin.Select(r => r.Id).ToList(), "and never asks for what is not there");
        }

        [Test]
        public void AnOpeningNightWithNobodyServed_DoesNotPutTheBarInDebt()
        {
            // THE PURSE IS SIZED FOR THE WORST FIRST NIGHT (2026-09-23). Measured when the PlayMode
            // market test found a fresh bar at -$9: a night on which nobody is served costs the rent
            // AND a walk-out fee per drinker. Whatever the seed, the bar must close that night with
            // money left for the cheapest thing on the market's first aisle.
            IngredientCard Card(string id, IngredientType type, string style) =>
                new IngredientCard(id, id, type, 1, info: new IngredientInfo(style, tier: 1, price: 6));
            foreach (var seed in new[] { "a", "b", "c", "d", "e", "f", "g", "h" })
            {
                var shelf = new Shelf(new[]
                {
                    new ShelfBottle(Card("vodka_w", IngredientType.Spirit, "vodka"), capacity: 6),
                    new ShelfBottle(Card("gin_w", IngredientType.Spirit, "gin"), capacity: 6),
                    new ShelfBottle(Card("bourbon_w", IngredientType.Spirit, "bourbon"), capacity: 6),
                    new ShelfBottle(Card("soda_w", IngredientType.Bubbly, "soda"), capacity: 3),
                    new ShelfBottle(Card("tonic_w", IngredientType.Bubbly, "tonic"), capacity: 3),
                    new ShelfBottle(Card("cola_w", IngredientType.Bubbly, "cola"), capacity: 3),
                });
                var run = new TycoonRun(shelf, RecipeCatalog.CreateDefault(), new RunRng(seed),
                    config: TycoonConfig.ForTheScene);
                Assert.AreEqual(TycoonConfig.DefaultStartingMoney, run.Money, "the scene opens on the default purse");
                run.DevSkipToDayEnd();
                Assert.Greater(run.DayWalkOuts, 0, seed + ": the night really was empty");
                Assert.GreaterOrEqual(run.Money, 4,
                    seed + ": after rent $" + run.DayRent + " and walk-out fees $" + run.DayWalkOutFees
                    + " the till must still cover a soda");
            }
        }

        [Test]
        public void TheWrittenWeek_IsShuffledLikeAnyOtherNight()
        {
            var book = RecipeCatalog.CreateDefault().Where(r => !r.Locked).ToList();
            var a = DayPlan.Roll(book, 5, 0.0, TycoonConfig.Default, Rng("wa"));
            var b = DayPlan.Roll(book, 5, 0.0, TycoonConfig.Default, Rng("wb"));
            CollectionAssert.AreEquivalent(a.Queue.Select(r => r.Id).ToList(),
                b.Queue.Select(r => r.Id).ToList());
            Assert.AreNotEqual(string.Join(",", a.Queue.Select(r => r.Id)),
                string.Join(",", b.Queue.Select(r => r.Id)));
        }

        // ── the projection (EconomyProjection) ──────────────────────────────────

        [Test]
        public void TheProjection_SaysWhatTheBriefAsksItToSay()
        {
            // *"her gün ekonomiye göre ... oyuncu hangi gün ne kadar kazanabilecek"*. These are the
            // figures Docs/ECONOMY_2026-09-23.md is written from, pinned so a constant cannot move
            // them without somebody looking at the document again.
            var book = RecipeCatalog.CreateDefault();
            var cfg = TycoonConfig.Default;
            var nights = EconomyProjection.Walk(book, cfg, 42, EconomyProjection.Competent);

            var first = nights[0];
            Assert.AreEqual(1, first.Day);
            Assert.That(first.Gross + first.Tips, Is.InRange(55, 75),
                "the opening night takes about the sixty dollars the brief asks for");
            Assert.Greater(first.Net, 0, "and it never closes the first night in the red");

            for (int i = 1; i < nights.Count; i++)
                Assert.Greater(nights[i].Till, nights[i - 1].Till,
                    "day " + nights[i].Day + ": the till never goes backwards on a competent night");

            var last = nights[nights.Count - 1];
            Assert.Greater(last.AverageCover, 10 * first.AverageCover,
                "and a five-star cover is worth more than ten times an opening one");
        }

        [Test]
        public void NobodyAtTheseStandards_EverDrawsABrokeCrowd()
        {
            // Written down because the first reading of this projection got it backwards and made
            // the opening week look like a trap. The broke line reads a NIGHT's satisfaction, not
            // the bar's standing, so a new bar is not broke — a bad one is.
            foreach (var q in new[] { EconomyProjection.Learning, EconomyProjection.Competent,
                                      EconomyProjection.Sharp })
                Assert.AreNotEqual(WealthTier.Broke,
                    BarRating.CrowdFor(BarRating.ExactStarsFor(EconomyProjection.SatisfactionFor(q))));
            Assert.AreEqual(WealthTier.HighRoller,
                BarRating.CrowdFor(BarRating.ExactStarsFor(
                    EconomyProjection.SatisfactionFor(EconomyProjection.Sharp))),
                "and a player who knows the book draws the room that can pay for it");
        }

        [Test]
        public void TheSplit_AlwaysSumsToTheWhole()
        {
            foreach (int total in new[] { 0, 1, 6, 7, 13, 40 })
                foreach (var shares in new[]
                         {
                             new[] { 45, 30, 15, 7, 2, 1 }, new[] { 85, 15, 0 },
                             new[] { 1, 1, 1 }, new[] { 0, 0, 5 },
                         })
                {
                    var parts = DayPlan.Split(total, shares);
                    Assert.AreEqual(total, parts.Sum(), total + " across " + string.Join("/", shares));
                    Assert.IsFalse(parts.Any(p => p < 0));
                }
            Assert.AreEqual(0, DayPlan.Split(9, new[] { 0, 0, 0 }).Sum(), "nothing to split across");
        }
    }
}
