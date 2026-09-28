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
    /// THE ACHIEVEMENTS (2026-09-28). Three promises, each held here rather than trusted to the screen:
    /// the lifetime ledger rules on a stat exactly once and announces progress only at its marks; the
    /// shipped book parses and every achievement in it can be named in the string tables; and the run
    /// reports what a real night did — and nothing at all when a dev verb touched it or nobody listens.
    /// </summary>
    public sealed class AchievementTests
    {
        private static AchievementDefinition A(string id, string stat, long target, bool hidden = false) =>
            new AchievementDefinition(id, new[] { stat }, target, hidden, hidden ? "secret" : "early", id, id);

        // ── the ledger ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ASumStat_UnlocksOnceAtItsTarget()
        {
            var t = new AchievementTracker(new[] { A("FIRST", Stats.ServesExact, 1), A("FIVE", Stats.ServesExact, 5) });
            var news = t.Apply(new StatBump(Stats.ServesExact, 1));
            CollectionAssert.AreEqual(new[] { "FIRST" }, news.Unlocked.Select(a => a.Id));
            Assert.IsTrue(t.IsUnlocked("FIRST"));
            Assert.IsEmpty(t.Apply(new StatBump(Stats.ServesExact, 3)).Unlocked, "four of five is not five");
            CollectionAssert.AreEqual(new[] { "FIVE" }, t.Apply(new StatBump(Stats.ServesExact, 1)).Unlocked.Select(a => a.Id));
            Assert.IsTrue(t.Apply(new StatBump(Stats.ServesExact, 10)).IsEmpty, "nothing is earned twice");
            Assert.AreEqual(15, t.Stat(Stats.ServesExact));
        }

        [Test]
        public void ABestStat_OnlyEverRises()
        {
            var t = new AchievementTracker(new[] { A("RANK3", Stats.BestRank, 3) });
            t.Apply(new StatBump(Stats.BestRank, 2));
            Assert.IsTrue(t.Apply(new StatBump(Stats.BestRank, 1)).IsEmpty);
            Assert.AreEqual(2, t.Stat(Stats.BestRank), "a worse bar does not undo a better one");
            Assert.AreEqual(1, t.Apply(new StatBump(Stats.BestRank, 3)).Unlocked.Count);
        }

        [Test]
        public void ASet_NeedsEveryStat_AndSaysSoAsEachComesIn()
        {
            var set = new AchievementDefinition("ALL_FOUR",
                new[] { Stats.CaughtBorrowed, Stats.CaughtAltered, Stats.CaughtCopied, Stats.CaughtDrawn }, 1, false, "mid", "x", "x");
            var t = new AchievementTracker(new[] { set });
            var first = t.Apply(new StatBump(Stats.CaughtBorrowed, 1));
            Assert.IsEmpty(first.Unlocked);
            Assert.AreEqual(1, first.Marks.Single().Have);
            Assert.AreEqual(4, first.Marks.Single().Need);
            Assert.IsTrue(t.Apply(new StatBump(Stats.CaughtBorrowed, 1)).IsEmpty, "a second borrowed card is not a new kind");
            t.Apply(new StatBump(Stats.CaughtAltered, 1));
            t.Apply(new StatBump(Stats.CaughtCopied, 1));
            var last = t.Apply(new StatBump(Stats.CaughtDrawn, 1));
            CollectionAssert.AreEqual(new[] { "ALL_FOUR" }, last.Unlocked.Select(a => a.Id));
            Assert.IsEmpty(last.Marks, "the unlock says what the mark would have");
        }

        [Test]
        public void ProgressMarks_AreTheQuartersOfAHundred_TheHalfOfTen_AndNothingSmaller()
        {
            var hundred = A("HUNDRED", Stats.ServesExact, 100);
            var ten = A("TEN", Stats.Wipes, 10);
            var five = A("FIVE", Stats.GlassesWashed, 5);
            var t = new AchievementTracker(new[] { hundred, ten, five });
            var marks = new List<long>();
            for (int i = 0; i < 99; i++)
                foreach (var m in t.Apply(new StatBump(Stats.ServesExact, 1)).Marks) marks.Add(m.Have);
            CollectionAssert.AreEqual(new long[] { 25, 50, 75 }, marks);
            Assert.AreEqual(1, t.Apply(new StatBump(Stats.Wipes, 5)).Marks.Count);
            Assert.IsEmpty(t.Apply(new StatBump(Stats.Wipes, 1)).Marks);
            Assert.IsTrue(t.Apply(new StatBump(Stats.GlassesWashed, 3)).IsEmpty, "a count of five says nothing on the way");
            // A bump that jumps several marks at once says so once.
            var jump = new AchievementTracker(new[] { A("JUMP", Stats.Tips, 100) });
            Assert.AreEqual(1, jump.Apply(new StatBump(Stats.Tips, 80)).Marks.Count);
        }

        [Test]
        public void TheSavedLedger_ComesBackWhole_AndCatchesUpOnANewAchievement()
        {
            var old = new AchievementTracker(new[] { A("FIRST", Stats.ServesExact, 1) });
            old.Apply(new StatBump(Stats.ServesExact, 30));
            // An update adds an achievement the player's stats already cover.
            var book = new[] { A("FIRST", Stats.ServesExact, 1), A("TWENTY", Stats.ServesExact, 20) };
            var back = new AchievementTracker(book, old.AllStats.Concat(new[] { new KeyValuePair<string, long>("gone_stat", 9) }),
                old.UnlockedIds);
            Assert.AreEqual(30, back.Stat(Stats.ServesExact));
            Assert.IsTrue(back.IsUnlocked("FIRST"));
            Assert.IsFalse(back.IsUnlocked("TWENTY"));
            CollectionAssert.AreEqual(new[] { "TWENTY" }, back.Reconcile().Unlocked.Select(a => a.Id));
            Assert.IsTrue(back.Reconcile().IsEmpty);
        }

        [Test]
        public void WhatTheStoreRemembers_IsTakenInQuietly()
        {
            var t = new AchievementTracker(new[] { A("HUNDRED", Stats.ServesExact, 100) });
            t.Apply(new StatBump(Stats.ServesExact, 10));
            t.Absorb(Stats.ServesExact, 140);   // another computer
            t.Absorb(Stats.ServesExact, 20);    // never lower
            Assert.AreEqual(140, t.Stat(Stats.ServesExact));
            Assert.IsFalse(t.IsUnlocked("HUNDRED"), "absorbing announces nothing");
            Assert.AreEqual(1, t.Reconcile().Unlocked.Count);
            Assert.IsTrue(t.Adopt("SOMETHING_STEAM_HAS"));
            Assert.IsFalse(t.Adopt("SOMETHING_STEAM_HAS"));
        }

        // ── the shipped book ────────────────────────────────────────────────────────────────────────

        private static IReadOnlyList<AchievementDefinition> ShippedBook() =>
            DataLoader.ParseAchievements(File.ReadAllText(
                Path.Combine(Application.dataPath, "Resources", "Data", "achievements.json")));

        [Test]
        public void TheShippedBook_Parses_AndEveryAchievementIsInTheStringTable()
        {
            var book = ShippedBook();
            Assert.GreaterOrEqual(book.Count, 30);
            var en = DataLoader.ParseStringTable(File.ReadAllText(
                Path.Combine(Application.dataPath, "Resources", "Data", "loc", "en.json")));
            foreach (var a in book)
            {
                string key = "achievement." + a.Id.ToLowerInvariant();
                Assert.IsTrue(en.TryGet(key + ".name", out var name), a.Id + " has no name in en.json");
                Assert.AreEqual(a.Name, name, a.Id + " name");
                Assert.IsTrue(en.TryGet(key + ".description", out var what), a.Id + " has no description in en.json");
                Assert.AreEqual(a.Description, what, a.Id + " description");
            }
        }

        [Test]
        public void TheShippedBook_HasSomethingForTheFirstNight_AndSomethingForTheLongHaul()
        {
            var book = ShippedBook();
            Assert.GreaterOrEqual(book.Count(a => a.Tier == "opening"), 2, "the first night earns something");
            Assert.GreaterOrEqual(book.Count(a => a.Tier == "week"), 6, "the first week earns something most nights");
            Assert.GreaterOrEqual(book.Count(a => a.Tier == "late"), 6, "and some are hard");
            Assert.IsTrue(book.Any(a => a.Stats.Contains(Stats.BestRank) && a.Target == 6), "the top of the ladder is one");
        }

        [Test]
        public void EveryAchievement_HasItsIcon_LitAndInBlackAndWhite()
        {
            // The list and the card draw the same pictures Steam shows (Tools/steamworks/achievement_icons.py --ship):
            // lit when earned, black and white on the way, and the padlock for a secret still to find.
            string items = Path.Combine(Application.dataPath, "Resources", "Items");
            Assert.IsTrue(File.Exists(Path.Combine(items, "ach_secret.png")), "the secret's padlock");
            foreach (var a in ShippedBook())
            {
                string id = a.Id.ToLowerInvariant();
                Assert.IsTrue(File.Exists(Path.Combine(items, "ach_" + id + ".png")), a.Id + " has no lit icon");
                Assert.IsTrue(File.Exists(Path.Combine(items, "ach_" + id + "_off.png")), a.Id + " has no black-and-white icon");
            }
        }

        [Test]
        public void ABadBook_IsRefusedLoudly()
        {
            string Row(string body) => "{\"achievements\":[" + body + "]}";
            Assert.Throws<FormatException>(() => DataLoader.ParseAchievements(Row(
                "{\"id\":\"first_round\",\"tier\":\"opening\",\"stat\":\"serves_exact\",\"target\":1,\"name\":\"a\",\"description\":\"b\"}")),
                "an id Steamworks would not take");
            Assert.Throws<FormatException>(() => DataLoader.ParseAchievements(Row(
                "{\"id\":\"X\",\"tier\":\"opening\",\"stat\":\"serves_exactly\",\"target\":1,\"name\":\"a\",\"description\":\"b\"}")),
                "a stat the run does not report");
            Assert.Throws<FormatException>(() => DataLoader.ParseAchievements(Row(
                "{\"id\":\"X\",\"tier\":\"secret\",\"stat\":\"blowouts\",\"target\":1,\"name\":\"a\",\"description\":\"b\"}")),
                "a secret that is not hidden");
            Assert.Throws<FormatException>(() => DataLoader.ParseAchievements(Row(
                "{\"id\":\"X\",\"tier\":\"week\",\"stat\":\"nights\",\"target\":0,\"name\":\"a\",\"description\":\"b\"}")),
                "a target of nothing");
            Assert.Throws<FormatException>(() => DataLoader.ParseAchievements(Row(
                "{\"id\":\"X\",\"tier\":\"week\",\"stat\":\"nights\",\"target\":1,\"name\":\"a\",\"description\":\"b\"}," +
                "{\"id\":\"X\",\"tier\":\"week\",\"stat\":\"nights\",\"target\":2,\"name\":\"a\",\"description\":\"b\"}")),
                "an id twice");
        }

        // ── the run reports ─────────────────────────────────────────────────────────────────────────

        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        /// <summary>A small bar the tests can serve exactly every time: gin and soda on the shelf, one built
        /// page between them, and the shipped room (its sink closes the night).</summary>
        private static readonly RecipeDefinition LongOne =
            new RecipeDefinition("long_plain", "Long One", 22,
                baseFlavor: 10, baseMult: 2, flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, prep: PrepMethod.Built);

        private static TycoonRun NewRun(string seed) =>
            new TycoonRun(new Shelf(new[]
                {
                    new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 60),
                    new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 60),
                }),
                new[] { LongOne }, new RunRng(seed),
                config: new TycoonConfig(500, orderDecisionSeconds: 0, savorSeconds: 0),
                fixtures: DataLoader.ParseFixtures(ReadDataFile("fixtures/fixtures.json")).Fixtures);

        /// <summary>A worked night: every drinker who has ordered gets the page, half and half, off a clean counter.</summary>
        private static void WorkANight(TycoonRun run)
        {
            for (int guard = 0; run.Phase == TycoonPhase.DayOpen; guard++)
            {
                Assert.Less(guard, 3000, "the night must end");
                run.Tick(1.0);
                TestNight.Clean(run);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    if (run.Phase != TycoonPhase.DayOpen) break;
                    if (visit.State != VisitState.Waiting || !visit.HasOrdered) continue;
                    run.PourMeasure("gin", 0.35);
                    run.PourMeasure("soda", 0.35);
                    run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
                    run.ServeTo(visit);
                }
            }
        }

        private static Dictionary<string, long> Sum(IEnumerable<StatBump> bumps)
        {
            var d = new Dictionary<string, long>();
            foreach (var b in bumps)
            {
                d.TryGetValue(b.Stat, out long v);
                d[b.Stat] = Stats.KindOf(b.Stat) == StatKind.Sum ? v + b.Value : Math.Max(v, b.Value);
            }
            return d;
        }

        [Test]
        public void AWorkedNight_ReportsItsServesAndItsClose()
        {
            var run = NewRun("ACH-A");
            run.Feats.Recording = true;
            WorkANight(run);
            var served = Sum(run.Feats.Take());
            Assert.Greater(served.GetValueOrDefault(Stats.ServesExact), 0, "vodka-sodas went over the bar exactly");
            Assert.IsFalse(served.ContainsKey(Stats.Nights), "the night is counted when its books close, not before");
            run.ContinueToNextDay();
            var closed = Sum(run.Feats.Take());
            Assert.AreEqual(1, closed[Stats.Nights]);
            Assert.AreEqual(1, closed[Stats.LongestRun]);
            Assert.AreEqual(run.Seats, closed[Stats.Stools]);
            Assert.AreEqual(1, closed[Stats.RecipesKnown]);
            Assert.IsFalse(run.Feats.Pending, "Take empties the queue");

            // The same run through the lifetime ledger earns the first night's two.
            var ledger = new AchievementTracker(ShippedBook());
            var news = ledger.Apply(served.Select(kv => new StatBump(kv.Key, kv.Value))
                .Concat(closed.Select(kv => new StatBump(kv.Key, kv.Value))));
            var ids = news.Unlocked.Select(a => a.Id).ToList();
            CollectionAssert.Contains(ids, "FIRST_ROUND");
            CollectionAssert.Contains(ids, "LIGHTS_OUT");
        }

        [Test]
        public void ARunNobodyListensTo_QueuesNothing()
        {
            var run = NewRun("ACH-B");
            WorkANight(run);
            run.ContinueToNextDay();
            Assert.IsFalse(run.Feats.Pending);
        }

        [Test]
        public void ADevVerb_SilencesTheRunForGood_AndTheSaveRemembers()
        {
            var run = NewRun("ACH-C");
            run.Feats.Recording = true;
            run.DevSkipToDayEnd();
            Assert.IsTrue(run.DevTouched);
            run.ContinueToNextDay();
            Assert.IsFalse(run.Feats.Pending, "a skipped night is not a night anybody worked");
            WorkANight(run);
            run.ContinueToNextDay();
            Assert.IsFalse(run.Feats.Pending, "and nothing after it counts either");

            RunSnapshot snap = null;
            WorkANight(run);
            run.ContinueToNextDay(s => snap = s);
            Assert.IsTrue(snap.devTouched, "the mark rides the save");

            var parked = NewRun("ACH-D");
            parked.Rating.DevSet(3.0);   // the ladder's dev climb reaches the rating directly
            Assert.IsTrue(parked.DevTouched);
        }
    }
}
