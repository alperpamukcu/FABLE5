using System.Collections.Generic;
using System.IO;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// THE HOSTESS'S WORDS IN THE STRING TABLE (2026-09-28, the quest chain's screen half). Two ways the chain can go
    /// quietly wrong on screen without a single rule breaking: the table WINS over quests.json (Localizer.GetOr), so a
    /// line the author rewrites in the book shows the old English until the table is rebuilt; and a key Core or the
    /// message asks for that the table never got prints as "[quest.owed.pints]". Both are pinned here, and so is the
    /// rule that makes a rename one field: no English line names her - {name} and the cast file do.
    /// </summary>
    public sealed class QuestLocTests
    {
        private static string Resource(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Data", relative));

        private static string ReadData(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static StringTable English() => DataLoader.ParseStringTable(Resource(Path.Combine("loc", "en.json")));

        private static QuestBook LiveBook() => DataLoader.ParseQuests(Resource("quests.json"),
            DataLoader.ParseFixtures(ReadData("fixtures/fixtures.json")).Fixtures);

        private const string Rebuild =
            " - quests.json and the string table disagree; run Tools/loc/data_keys.py, then merge_fragments.py " +
            "--update --write, or the game keeps showing the table's English";

        [Test]
        public void TheEnglishTableSaysWhatTheBookSays()
        {
            var en = English();
            var book = LiveBook();
            foreach (var q in book.Quests)
            {
                Same(en, $"data.quest.{q.Id}.title", q.Title);
                for (int i = 0; i < q.HandOver.Count; i++) Same(en, $"data.quest.{q.Id}.hand_over.{i}", q.HandOver[i]);
                for (int i = 0; i < q.Done.Count; i++) Same(en, $"data.quest.{q.Id}.done.{i}", q.Done[i]);
            }
            for (int i = 0; i < book.Finale.Count; i++) Same(en, $"data.hostess.finale.say.{i}", book.Finale[i]);
            Same(en, "data.hostess.not_yet.say.0", book.NotYet);
        }

        private static void Same(StringTable en, string key, string book)
        {
            Assert.IsTrue(en.TryGet(key, out string said), key + " is not in the English table" + Rebuild);
            Assert.AreEqual(book, said, key + Rebuild);
        }

        [Test]
        public void EveryLineTheJobMessagePrintsIsInTheEnglishTable()
        {
            var en = English();
            // What Core asks for, kind by kind - one job of every kind written here, and the garnish kind with one dish
            // and with two, so an edit to the book can never turn a string-table test red.
            QuestDefinition Row(string id, QuestKind kind, int rung = 0, int target = 2,
                PreparationDefinition[] preps = null, int goalRung = 0, double goalComfort = 0,
                string slot = null, int level = 0, string fixtureId = null) =>
                new QuestDefinition(id, kind, rung, target, 10, id.ToUpperInvariant(), new[] { "Go." }, new[] { "Good." },
                    preps: preps, goalRung: goalRung, goalComfort: goalComfort, slot: slot, level: level, fixtureId: fixtureId);
            var quests = new List<ActiveQuest>
            {
                new ActiveQuest(Row("serve", QuestKind.Serve), "gin_tonic", "Gin & Tonic", 2, 0, 10, 1),
                new ActiveQuest(Row("perfect", QuestKind.Perfect), "", "", 2, 0, 10, 1),
                new ActiveQuest(Row("pints", QuestKind.Pints), "", "", 2, 0, 10, 1),
                new ActiveQuest(Row("one_dish", QuestKind.Garnish, rung: 1, preps: new[] { Preparations.Ice }), "", "", 2, 0, 10, 1),
                new ActiveQuest(Row("two_dishes", QuestKind.Garnish, rung: 2,
                    preps: new[] { Preparations.SaltRim, Preparations.SugarRim }), "", "", 2, 0, 10, 1),
                new ActiveQuest(Row("clean", QuestKind.Clean, target: 1), "", "", 1, 0, 10, 1),
                new ActiveQuest(Row("door", QuestKind.Door, rung: 2, target: 1), "", "", 1, 0, 10, 1),
                new ActiveQuest(Row("rank", QuestKind.Rank, target: 0, goalRung: 3), "", "", 1, 0, 10, 1),
                new ActiveQuest(Row("comfort", QuestKind.Comfort, target: 0, goalComfort: 1.0), "", "", 1, 0, 10, 1),
                new ActiveQuest(Row("fit", QuestKind.Fit, target: 0, slot: "wall_center", level: 1, fixtureId: "art_city"),
                    "", "", 1, 0, 10, 1),
            };
            var kinds = new HashSet<QuestKind>();
            foreach (var aq in quests)
            {
                kinds.Add(aq.Kind);
                Has(en, aq.OwedLine().Key);
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(QuestKind)).Length, kinds.Count, "one job of every kind");

            foreach (var key in new[]
                     {
                         "chrome.quest.reward", "chrome.quest.pays", "chrome.quest.done_line", "chrome.quest.number",
                         "chrome.quest.waiting", "chrome.quest.comes_tonight", "chrome.quest.comes_tomorrow",
                         "chrome.quest.progress_comfort", "chrome.quest.progress_rank", "chrome.quest.progress_fit",
                         "chrome.toast.quest_paid", "chrome.log.quest_done", "chrome.log.quest_given",
                         "chrome.hostess.on_it", "chrome.ledger.quest", "dayend.bill.quest",
                     })
                Has(en, key);
        }

        /// <summary>A plain line, or a counted one with its #other form.</summary>
        private static void Has(StringTable en, string key) =>
            Assert.IsTrue(en.TryGet(key, out _) || en.TryGet(key + StringTable.PluralMark + "other", out _),
                $"'{key}' is asked for and the English table has no line for it - the screen would print [{key}]");

        [Test]
        public void NoEnglishLineSpellsHerName()
        {
            // THE NAME IS ONE FIELD (spec E.1): papers.json writes it, {name} and ShortName carry it everywhere else.
            var hostess = DataLoader.ParsePapers(ReadData("customers/papers.json")).For("hostess");
            Assert.That(hostess, Is.Not.Null, "the cast file has no hostess");
            string full = hostess.Name.Trim();
            int space = full.IndexOf(' ');
            string first = space > 0 ? full.Substring(0, space) : full;

            var en = English();
            string raw = Resource(Path.Combine("loc", "en.json"));
            Assert.That(raw.IndexOf(full, System.StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                $"the English table spells out '{full}' - a rename would have to find it");
            foreach (string key in en.Keys)
            {
                bool hers = key.StartsWith("data.quest.") || key.StartsWith("data.hostess.")
                            || key.StartsWith("chrome.quest.") || key.StartsWith("chrome.hostess.")
                            || key == "chrome.toast.quest_paid" || key.StartsWith("chrome.log.quest_")
                            || key == "chrome.ledger.quest" || key == "dayend.bill.quest";
                if (!hers) continue;
                en.TryGet(key, out string text);
                Assert.That(text.IndexOf(first, System.StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                    $"'{key}' spells '{first}'; her lines say {{name}} and the chrome says {{who}}");
            }
        }
    }
}
