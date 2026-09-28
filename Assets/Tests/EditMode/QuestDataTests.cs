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
    /// THE HOSTESS'S BOOK AS CONTENT (2026-09-27): the shipped <c>Resources/Data/quests.json</c>, read the way
    /// GameBootstrap reads it and held to the ladder it is written for — and the loader's loud refusals, one broken
    /// book at a time. A book that asks for the door before the bar has one, or for a comfort no room at that rung
    /// can reach, would not crash: it would be a job standing on the bar that nobody can ever finish.
    /// The same rules without the loader are QuestBookTests; the chain played on hand-written books is QuestChainTests.
    /// </summary>
    public sealed class QuestDataTests
    {
        private static string ReadData(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static string ReadBookFile() =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Data", "quests.json"));

        private static IReadOnlyList<FixtureDefinition> Fixtures() =>
            DataLoader.ParseFixtures(ReadData("fixtures/fixtures.json")).Fixtures;

        private static QuestBook LiveBook() => DataLoader.ParseQuests(ReadBookFile(), Fixtures());

        /// <summary>The scene's own bar, as GameBootstrap builds it, with the live book on it.</summary>
        private static TycoonRun LiveRun(string seed, QuestBook book = null)
        {
            var deck = DataLoader.ParseDeck(ReadData("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(ReadData("recipes/recipes.json"));
            var fixtures = Fixtures();
            var cast = DataLoader.ParsePapers(ReadData("customers/papers.json"));
            var starting = new List<ShelfBottle>();
            var catalogue = new List<IngredientCard>();
            foreach (var card in deck.Cards)
            {
                if (deck.IsStarting(card)) starting.Add(new ShelfBottle(card.Clone()));
                else catalogue.Add(card);
            }
            return new TycoonRun(new Shelf(starting), recipes, new RunRng(seed),
                config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(DataLoader.ParseArchetypes(ReadData("customers/archetypes.json"))),
                brandCatalogue: catalogue,
                glassware: DataLoader.ParseGlassware(ReadData("glassware/glassware.json")),
                lockedStock: deck.LockedCards,
                fixtures: fixtures,
                story: DataLoader.ParseStory(ReadData("story/story.json"), cast, recipes),
                quests: book ?? DataLoader.ParseQuests(ReadBookFile(), fixtures));
        }

        // ── the shipped book ─────────────────────────────────────────────────────────

        [Test]
        public void TheLiveBook_Parses_InLadderOrder_AndEveryGateHolds()
        {
            var book = LiveBook();
            var fixtures = Fixtures();
            Assert.That(book.Count, Is.InRange(14, 18), "the chain the approval page drew");
            Assert.AreEqual("first_wage", book[0].Id, "the first job is a count of one drink");
            Assert.AreEqual(QuestKind.Serve, book[0].Kind);

            int lastRung = 0;
            foreach (var q in book.Quests)
            {
                string who = q.Id + ": ";
                Assert.That(q.Rung, Is.GreaterThanOrEqualTo(lastRung), who + "rungs never go down the book");
                lastRung = q.Rung;
                Assert.That(q.Reward, Is.InRange(1, QuestRules.MaxReward), who + "pays");
                if (q.Kind == QuestKind.Door)
                    Assert.That(q.Rung, Is.GreaterThanOrEqualTo(BarRank.Granting(Feature.Door).Index), who + "the door");
                if (q.Kind == QuestKind.Serve && q.Pick == QuestPick.Stirred)
                    Assert.That(q.Rung, Is.GreaterThanOrEqualTo(BarRank.Granting(Feature.Spoon).Index), who + "the spoon");
                foreach (var p in q.Preps)
                    Assert.That(q.Rung, Is.GreaterThanOrEqualTo(QuestRules.RungOf(p)), who + p.Id + " on the counter");
                if (q.Kind == QuestKind.Rank)
                    Assert.That(q.GoalRung, Is.GreaterThan(q.Rung), who + "a rank still to reach");
                if (q.Kind == QuestKind.Fit)
                {
                    var piece = fixtures.First(f => f.Id == q.FixtureId);
                    Assert.AreEqual(q.Slot, piece.Slot, who + "the piece stands in its slot");
                    Assert.AreEqual(q.Level, piece.Level, who + "on its rung");
                    Assert.That(piece.Stars, Is.LessThanOrEqualTo(BarRank.Rungs[q.Rung].Stars + BarRank.Epsilon),
                        who + "the shop sells it where the job is handed over");
                }
                if (q.Kind == QuestKind.Comfort)
                {
                    // REACHABLE FROM WHAT A BAR AT THAT RUNG CAN BUY: the preset fits everything the shop has opened
                    // by then (1.80 at half a star, 2.40 at one when this was written).
                    var run = LiveRun("comfort-" + q.Id, book);
                    run.DevPresetStars(BarRank.Rungs[q.Rung].Stars);
                    Assert.That(run.ComfortBase, Is.GreaterThanOrEqualTo(q.GoalComfort - 1e-9),
                        who + "a room at rung " + q.Rung + " can reach " + q.GoalComfort);
                }
            }
            Assert.That(book.Finale.Count, Is.InRange(1, 3), "she has something to say when the book is done");
            Assert.That(book.NotYet, Does.Contain("{rung}"), "and says which rung the next job waits on");
        }

        [Test]
        public void TheLiveBook_HandsOverTheSameFirstJob_HeardOrNot()
        {
            foreach (string seed in new[] { "QUEST-A", "QUEST-B", "QUEST-C" })
            {
                var headless = LiveRun(seed);
                var visit = TickToHer(headless);
                Assert.AreEqual(1, visit.Day, seed + ": the close of night one");
                Assert.AreEqual("first_wage", visit.Offered?.Id, seed);
                var drink = headless.MenuRecipes.FirstOrDefault(r => r.Id == visit.Offered.RecipeId);
                Assert.IsNotNull(drink, seed + ": the drink is on the menu");
                Assert.IsTrue(drink.HasAuthoredRatios, seed + ": and has something to get right");
                Assert.IsTrue(headless.CanServe(drink), seed + ": and the shelf pours it tonight");

                // THE PICK IS MADE AT HER ARRIVAL: the drink she walked in with is the one put on the bar, whatever the
                // room draws on its other streams in between (a drinker's voice line, here) and whoever hears her.
                string atArrival = visit.Offered.RecipeId;
                for (int i = 0; i < 7; i++) headless.VoiceStream.NextInt(100);
                int ticks = 0;
                while (headless.HostessVisit != null)
                {
                    Assert.Less(ticks, 10, seed + ": the backstop must fire");
                    headless.Tick(1.0);
                    ticks++;
                }
                Assert.AreEqual(4, ticks, seed + ": handed over on the fourth unheard floor-second");
                Assert.AreEqual(atArrival, headless.Quest.RecipeId, seed + ": the drink she walked in with");

                var heard = LiveRun(seed);
                Assert.AreEqual(atArrival, TickToHer(heard).Offered?.RecipeId, seed + ": the same seed, the same pick");
                heard.BeginTalk();
                for (int i = 0; i < 20; i++) heard.Tick(1.0);
                heard.HearHostess();
                heard.EndTalk();
                Assert.AreEqual(heard.Quest.Id, headless.Quest.Id, seed);
                Assert.AreEqual(atArrival, heard.Quest.RecipeId, seed + ": the same drink, heard or not");
                Assert.AreEqual(heard.Quest.Target, headless.Quest.Target, seed);
                Assert.AreEqual(heard.Quest.Reward, headless.Quest.Reward, seed);
            }
        }

        private static HostessVisit TickToHer(TycoonRun run)
        {
            int guard = 0;
            while (run.HostessVisit == null)
            {
                Assert.Less(guard++, 5000, "she must come at the first close");
                Assert.AreEqual(TycoonPhase.DayOpen, run.Phase, "the night closed without her");
                run.Tick(1.0);
                TestNight.Clean(run);
            }
            return run.HostessVisit;
        }

        // ── the loud failures ────────────────────────────────────────────────────────

        private const string Visits =
            @"""visits"": [ { ""id"": ""finale"", ""say"": [""That is all.""] },
                            { ""id"": ""not_yet"", ""say"": [""Wait till {rung}.""] } ]";

        private const string Good =
            @"{ ""id"": ""good"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 2, ""reward"": 20, ""title"": ""GOOD"",
                ""handOver"": [""Give me {n}.""], ""done"": [""Thanks.""] }";

        private static string Book(string rows, string visits = Visits) =>
            @"{ ""version"": 1, ""quests"": [" + rows + "], " + visits + " }";

        [Test]
        public void TheLoaderReadsAGoodBook()
        {
            var book = DataLoader.ParseQuests(Book(Good), Fixtures());
            Assert.AreEqual(1, book.Count);
            Assert.AreEqual("good", book[0].Id);
            Assert.AreEqual("Wait till {rung}.", book.NotYet);
        }

        [TestCase("bad_kind", @"{ ""id"": ""bad_kind"", ""kind"": ""juggle"", ""rung"": 0, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("olive_job", @"{ ""id"": ""olive_job"", ""kind"": ""garnish"", ""preps"": [""olive""], ""rung"": 3,
            ""target"": 2, ""reward"": 10, ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("early_door", @"{ ""id"": ""early_door"", ""kind"": ""door"", ""rung"": 1, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("done_deal", @"{ ""id"": ""done_deal"", ""kind"": ""rank"", ""goalRung"": 1, ""rung"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("no_slot", @"{ ""id"": ""no_slot"", ""kind"": ""fit"", ""slot"": ""the_moon"", ""level"": 1, ""rung"": 0,
            ""reward"": 10, ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("no_rung", @"{ ""id"": ""no_rung"", ""kind"": ""fit"", ""slot"": ""wall_center"", ""level"": 9, ""rung"": 0,
            ""reward"": 10, ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("silent", @"{ ""id"": ""silent"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [], ""done"": [""Done.""] }")]
        [TestCase("drinkless", @"{ ""id"": ""drinkless"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Make me a {drink}.""], ""done"": [""Done.""] }")]
        [TestCase("nickname", @"{ ""id"": ""nickname"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Hi {nickname}.""], ""done"": [""Done.""] }")]
        [TestCase("too_many", @"{ ""id"": ""too_many"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 13, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("low", @"{ ""id"": ""high"", ""kind"": ""door"", ""rung"": 2, ""target"": 1, ""reward"": 10,
            ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] },
            { ""id"": ""low"", ""kind"": ""perfect"", ""rung"": 0, ""target"": 1, ""reward"": 10,
            ""title"": ""Y"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("good", Good + ", " + Good)]
        [TestCase("picky", @"{ ""id"": ""picky"", ""kind"": ""perfect"", ""pick"": ""top"", ""rung"": 0, ""target"": 1,
            ""reward"": 10, ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        [TestCase("no_such_prep", @"{ ""id"": ""no_such_prep"", ""kind"": ""garnish"", ""preps"": [""umbrella""], ""rung"": 2,
            ""target"": 1, ""reward"": 10, ""title"": ""X"", ""handOver"": [""Go.""], ""done"": [""Done.""] }")]
        public void TheLoaderRefusesABrokenBook(string id, string rows)
        {
            var e = Assert.Throws<FormatException>(() => DataLoader.ParseQuests(Book(rows), Fixtures()));
            Assert.That(e.Message, Does.Contain(id), "the refusal names the job");
        }

        [Test]
        public void TheLoaderRefusesABookWithNothingToSayOutsideAJob()
        {
            Assert.Throws<FormatException>(() => DataLoader.ParseQuests(
                Book(Good, @"""visits"": [ { ""id"": ""not_yet"", ""say"": [""Wait till {rung}.""] } ]"), Fixtures()),
                "no finale");
            Assert.Throws<FormatException>(() => DataLoader.ParseQuests(
                Book(Good, @"""visits"": [ { ""id"": ""finale"", ""say"": [""Bye.""] } ]"), Fixtures()),
                "no \"not yet\"");
            Assert.Throws<FormatException>(() => DataLoader.ParseQuests(
                Book(Good, @"""visits"": [ { ""id"": ""finale"", ""say"": [""Bye.""] },
                                            { ""id"": ""not_yet"", ""say"": [""Not yet.""] } ]"), Fixtures()),
                "\"not yet\" has to say which rung");
            Assert.Throws<FormatException>(() => DataLoader.ParseQuests(
                Book(Good, @"""visits"": [ { ""id"": ""finale"", ""say"": [""Bye.""] },
                                            { ""id"": ""not_yet"", ""say"": [""Wait till {rung}.""] },
                                            { ""id"": ""birthday"", ""say"": [""Cake.""] } ]"), Fixtures()),
                "a visit nothing plays");
            Assert.Throws<FormatException>(() => DataLoader.ParseQuests(@"{ ""version"": 1, ""quests"": [], " + Visits + " }",
                Fixtures()), "an empty book");
        }
    }
}
