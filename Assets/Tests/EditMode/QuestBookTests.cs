using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE BOOK'S OWN RULES, WITHOUT THE LOADER (2026-09-27). QuestDataTests reads the shipped file and throws broken
    /// ones at the content loader; most of what the loader refuses is Core's refusal (QuestDefinition, QuestBook,
    /// QuestRules.ResolveFit), so it is pinned here a second time, in pure Core, where a bench can run it — and so are
    /// the small rules the run leans on: the pool a serve job picks from, the line the bubble prints, the short name.
    /// </summary>
    public sealed class QuestBookTests
    {
        private static readonly string[] Say = { "{name} has a job." };
        private static readonly string[] Thanks = { "Thank you." };

        private static QuestDefinition Row(string id = "a_job", QuestKind kind = QuestKind.Perfect, int rung = 0,
            int target = 2, int reward = 20, string title = "A JOB", string[] handOver = null, string[] done = null,
            QuestPick pick = QuestPick.Any, PreparationDefinition[] preps = null, int goalRung = 0,
            double goalComfort = 0, string slot = null, int level = 0, string fixtureId = null) =>
            new QuestDefinition(id, kind, rung, target, reward, title, handOver ?? Say, done ?? Thanks,
                pick, preps, goalRung, goalComfort, slot, level, fixtureId);

        private static QuestBook Book(IReadOnlyList<QuestDefinition> rows, string[] finale = null,
            string notYet = "Wait till {rung}.") =>
            new QuestBook(rows, finale ?? new[] { "That is all." }, notYet);

        /// <summary>Refused, in words that name the job.</summary>
        private static void Refused(TestDelegate build, string id)
        {
            var e = Assert.Catch<ArgumentException>(build);
            Assert.That(e.Message, Does.Contain(id), "the refusal names the job");
        }

        [Test]
        public void A_row_that_breaks_a_rule_is_refused_naming_it()
        {
            Assert.DoesNotThrow(() => Row());
            Refused(() => Row("Bad-Id"), "Bad-Id");
            Refused(() => Row("rung", rung: 7), "rung");
            Refused(() => Row("free", reward: 0), "free");
            Refused(() => Row("dear", reward: 1001), "dear");
            Refused(() => Row("untitled", title: " "), "untitled");
            Refused(() => Row("long_title", title: "A TITLE FAR TOO LONG TO FIT"), "long_title");
            Refused(() => Row("braced_title", title: "{n} POURS"), "braced_title");
            Refused(() => Row("silent", handOver: new string[0]), "silent");
            Refused(() => Row("chatty", handOver: new[] { "a", "b", "c", "d", "e" }), "chatty");
            Refused(() => Row("blank_line", handOver: new[] { "one", " " }), "blank_line");
            Refused(() => Row("long_quote", handOver: new[] { new string('x', 121) }), "long_quote");
            Refused(() => Row("no_thanks", done: new string[0]), "no_thanks");
            Refused(() => Row("too_many", target: 13), "too_many");
            Refused(() => Row("none", target: 0), "none");
            Refused(() => Row("drinkless", handOver: new[] { "Make me a {drink}." }), "drinkless");
            Refused(() => Row("nickname", handOver: new[] { "Hi {nickname}." }), "nickname");
            Refused(() => Row("dangling", done: new[] { "Thanks {name." }), "dangling");
            Refused(() => Row("count_on_a_state", kind: QuestKind.Rank, target: 0, goalRung: 2,
                handOver: new[] { "{n} of them." }), "count_on_a_state");
        }

        [Test]
        public void The_kinds_are_held_to_their_rungs()
        {
            // The door and the spoon are verbs a bar does not have below their rungs.
            Refused(() => Row("early_door", kind: QuestKind.Door, rung: 1, target: 1), "early_door");
            Assert.DoesNotThrow(() => Row("door", kind: QuestKind.Door, rung: 2, target: 1));
            Refused(() => Row("early_stir", kind: QuestKind.Serve, rung: 2, pick: QuestPick.Stirred), "early_stir");
            Assert.DoesNotThrow(() => Row("stir", kind: QuestKind.Serve, rung: 3, pick: QuestPick.Stirred));

            // A garnish is on the counter from its rung; a jar the shop can withhold is never asked for.
            Refused(() => Row("early_ice", kind: QuestKind.Garnish, preps: new[] { Preparations.Ice }), "early_ice");
            Refused(() => Row("early_rim", kind: QuestKind.Garnish, rung: 1, preps: new[] { Preparations.SaltRim }), "early_rim");
            Refused(() => Row("olives", kind: QuestKind.Garnish, rung: 3, preps: new[] { Preparations.Olive }), "olives");
            Refused(() => Row("mint", kind: QuestKind.Garnish, rung: 3, preps: new[] { Preparations.Mint }), "mint");
            Refused(() => Row("three", kind: QuestKind.Garnish, rung: 2,
                preps: new[] { Preparations.Ice, Preparations.SaltRim, Preparations.SugarRim }), "three");
            Refused(() => Row("no_preps", kind: QuestKind.Garnish, rung: 2), "no_preps");
            Assert.DoesNotThrow(() => Row("twist", kind: QuestKind.Garnish, rung: 1,
                preps: new[] { Preparations.Ice, Preparations.LemonTwist }));

            // A rank job aims above the rung it is handed over on; a comfort job inside the room's five.
            Refused(() => Row("done_deal", kind: QuestKind.Rank, rung: 1, target: 0, goalRung: 1), "done_deal");
            Refused(() => Row("off_the_top", kind: QuestKind.Rank, target: 0, goalRung: 7), "off_the_top");
            Refused(() => Row("no_room", kind: QuestKind.Comfort, target: 0, goalComfort: 0), "no_room");
            Refused(() => Row("palace", kind: QuestKind.Comfort, target: 0, goalComfort: 5.5), "palace");
            Refused(() => Row("rung_zero", kind: QuestKind.Fit, target: 0, slot: "wall_center", level: 0,
                fixtureId: "pic_1"), "rung_zero");

            // The state kinds are met or not: their target is read as one.
            Assert.AreEqual(1, Row("met", kind: QuestKind.Rank, target: 0, goalRung: 1).Target);
        }

        private static FixtureDefinition Piece(string id, string slot, int level, double stars) =>
            new FixtureDefinition(id, id, slot, 20, stars, "A piece.", "fx_" + id, level: level);

        [Test]
        public void A_fit_job_names_a_rung_the_shop_sells_where_it_is_handed_over()
        {
            var catalogue = new[] { Piece("pic_1", "wall_center", 1, 0.0), Piece("pic_2", "wall_center", 2, 1.0) };
            Assert.AreEqual("pic_1", QuestRules.ResolveFit(catalogue, "hang", "wall_center", 1, 0).Id);
            Assert.AreEqual("pic_2", QuestRules.ResolveFit(catalogue, "hang", "wall_center", 2, 2).Id);
            Refused(() => QuestRules.ResolveFit(catalogue, "moon", "the_moon", 1, 0), "moon");
            Refused(() => QuestRules.ResolveFit(catalogue, "rung_nine", "wall_center", 9, 0), "rung_nine");
            Refused(() => QuestRules.ResolveFit(catalogue, "too_soon", "wall_center", 2, 1), "too_soon");
        }

        [Test]
        public void The_book_is_climbed_in_order_and_says_what_it_owes()
        {
            var rows = new[] { Row("first"), Row("door", QuestKind.Door, rung: 2, target: 1), Row("low") };
            Refused(() => Book(rows), "low");
            Refused(() => Book(new[] { Row("twice"), Row("twice") }), "twice");
            Assert.Catch<ArgumentException>(() => Book(new QuestDefinition[0]), "a book with no jobs");
            Assert.Catch<ArgumentException>(() => Book(new[] { Row() }, finale: new string[0]), "no finale");
            Assert.Catch<ArgumentException>(() => Book(new[] { Row() }, finale: new[] { "a", "b", "c", "d" }));
            Assert.Catch<ArgumentException>(() => Book(new[] { Row() }, finale: new[] { "Bye {rung}." }));
            Assert.Catch<ArgumentException>(() => Book(new[] { Row() }, notYet: "Not yet."),
                "\"not yet\" has to say which rung");
            Assert.Catch<ArgumentException>(() => Book(new[] { Row() }, notYet: "Not till {drink}."));

            var book = Book(new[] { Row("one"), Row("two", rung: 1), Row("three", rung: 1) });
            Assert.AreEqual(3, book.Count);
            Assert.AreEqual(new[] { 0, 1, 2 }, book.Quests.Select(q => q.Index).ToArray(), "the book numbers its rows");
            Assert.AreSame(book[1], book.Find("two"));
            Assert.IsNull(book.Find("four"));
        }

        [Test]
        public void The_words_the_data_file_writes()
        {
            Assert.AreEqual(9, QuestRules.KindNamesInData.Count);
            foreach (QuestKind kind in Enum.GetValues(typeof(QuestKind)))
                Assert.AreEqual(kind, QuestRules.ParseKind(QuestRules.KindName(kind)));
            Assert.IsNull(QuestRules.ParseKind("Serve"), "lower case only, like every id");
            Assert.IsNull(QuestRules.ParseKind("juggle"));
            Assert.AreEqual(QuestPick.Any, QuestRules.ParsePick(""));
            Assert.AreEqual(QuestPick.Any, QuestRules.ParsePick("any"));
            Assert.AreEqual(QuestPick.Stirred, QuestRules.ParsePick("stirred"));
            Assert.AreEqual(QuestPick.Top, QuestRules.ParsePick("top"));
            Assert.IsNull(QuestRules.ParsePick("shaken"));
            CollectionAssert.AreEqual(new[] { "name", "n", "drink" },
                QuestRules.SlotsIn("{name}: {n} {drink}.", "x", "say"));
        }

        private static RecipeDefinition Page(string id, int rank, PrepMethod prep = PrepMethod.Built, bool authored = true) =>
            new RecipeDefinition(id, id, rank, baseFlavor: 6, baseMult: 1, flavorPerLevel: 0, multPerLevel: 0,
                requirements: authored
                    ? Array.Empty<PatternRequirement>()
                    : new[] { new PatternRequirement(1, IngredientType.Spirit) },
                ratioRequirements: authored
                    ? new[] { new RatioRequirement(IngredientType.Spirit, 0.4, 0.7), new RatioRequirement(IngredientType.Bubbly, 0.3, 0.6) }
                    : null,
                minFill: 0.5, prep: prep);

        [Test]
        public void A_serve_job_picks_from_the_pourable_authored_pages()
        {
            var menu = new[]
            {
                Page("neat", 1, authored: false),               // nothing to get right: never named
                Page("fizz", 4),
                Page("martini", 16, PrepMethod.Stirred),
                Page("vesper", 20, PrepMethod.Stirred),         // the shelf cannot pour it tonight
                Page("negroni", 16, PrepMethod.Stirred),
            };
            Func<RecipeDefinition, bool> pourable = r => r.Id != "vesper";
            CollectionAssert.AreEqual(new[] { "fizz", "martini", "negroni" },
                QuestRules.Pool(QuestPick.Any, menu, pourable).Select(r => r.Id).ToArray(), "in menu order");
            CollectionAssert.AreEqual(new[] { "martini", "negroni" },
                QuestRules.Pool(QuestPick.Stirred, menu, pourable).Select(r => r.Id).ToArray());
            var any = QuestRules.Pool(QuestPick.Any, menu, pourable);
            Assert.AreEqual("martini", QuestRules.Top(any).Id, "the highest rank, ties to the earliest");
            Assert.AreEqual("martini", QuestRules.FirstOf(QuestPick.Top, any).Id);
            Assert.AreEqual("fizz", QuestRules.FirstOf(QuestPick.Any, any).Id);
            Assert.IsNull(QuestRules.FirstOf(QuestPick.Any, new RecipeDefinition[0]));
        }

        [Test]
        public void The_bubble_line_says_what_is_owed()
        {
            var serve = new ActiveQuest(Row("serve", QuestKind.Serve, target: 3,
                handOver: new[] { "{n} {drink}." }), "negroni", "Negroni", 3, 1, 12, 1);
            var owed = serve.OwedLine();
            Assert.AreEqual("quest.owed.serve", owed.Key);
            Assert.AreEqual(2, owed.Count, "counted by what is left");
            Assert.AreEqual("data.recipe.negroni.name", serve.NameLine.Key);
            Assert.AreEqual(2, serve.Left);
            Assert.IsFalse(serve.IsDone);

            var rims = new ActiveQuest(Row("rims", QuestKind.Garnish, rung: 2,
                preps: new[] { Preparations.SaltRim, Preparations.SugarRim }), "", "", 2, 0, 20, 1);
            Assert.AreEqual("quest.owed.garnish2", rims.OwedLine().Key);
            Assert.AreEqual("", rims.RecipeId, "only a serve job names a drink");
            Assert.AreEqual("quest.name.garnish", rims.NameLine.Key);

            var rank = new ActiveQuest(Row("rank", QuestKind.Rank, target: 0, goalRung: 3), "", "", 1, 0, 30, 1);
            Assert.AreEqual("quest.owed.rank", rank.OwedLine().Key);
            var fit = new ActiveQuest(Row("fit", QuestKind.Fit, target: 0, slot: "wall_center", level: 1,
                fixtureId: "art_city"), "", "", 1, 0, 24, 1);
            Assert.AreEqual("quest.owed.fit", fit.OwedLine().Key);
            Assert.AreEqual("quest.owed.perfect", new ActiveQuest(Row(), "", "", 2, 0, 20, 1).OwedLine().Key);
            Assert.AreEqual("quest.owed.door",
                new ActiveQuest(Row("door", QuestKind.Door, rung: 2, target: 1), "", "", 1, 0, 20, 1).OwedLine().Key);

            Assert.Catch<ArgumentException>(() => new ActiveQuest(Row("s", QuestKind.Serve), "", "", 2, 0, 20, 1),
                "a serve job names its drink");
        }

        [Test]
        public void The_bar_calls_her_by_her_first_name()
        {
            Assert.AreEqual("Roxy", new StoryCharacter("hostess", "hostess", "Roxy Vale", 28).ShortName);
            Assert.AreEqual("Cher", new StoryCharacter("one_name", "one_name", "Cher", 40).ShortName);
        }
    }
}
