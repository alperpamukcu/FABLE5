using System.Collections.Generic;
using System.IO;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The shipped string tables (2026-09-13, localization L0): every file parses and is named
    /// after its code; English is the source, so every other table's key exists in English, every
    /// line carries exactly English's placeholders, and every counted line spells each plural form
    /// its language needs. A translation that drifts from the English fails here, not on screen.
    /// </summary>
    public sealed class StringTableFileTests
    {
        private static string Folder => Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data", "loc");

        private static Dictionary<string, StringTable> Shipped()
        {
            Assert.IsTrue(Directory.Exists(Folder), "the tables ship at " + Folder);
            var tables = new Dictionary<string, StringTable>();
            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                var table = DataLoader.ParseStringTable(File.ReadAllText(file));
                Assert.AreEqual(Path.GetFileNameWithoutExtension(file), table.Code, file + " is named after its code");
                tables.Add(table.Code, table);
            }
            Assert.IsTrue(tables.ContainsKey(Languages.Source), "the English table ships");
            return tables;
        }

        [Test]
        public void EveryTableFollowsTheEnglishOne()
        {
            var tables = Shipped();
            var en = tables[Languages.Source];
            var enBases = new Dictionary<string, HashSet<string>>();
            foreach (string key in en.Keys)
            {
                en.TryGet(key, out string text);
                string b = StringTable.BaseKey(key);
                if (!enBases.TryGetValue(b, out var names)) enBases[b] = names = new HashSet<string>();
                names.UnionWith(StringTable.Placeholders(text));
            }
            foreach (var table in tables.Values)
            {
                if (table.Code == Languages.Source) continue;
                foreach (string key in table.Keys)
                {
                    string b = StringTable.BaseKey(key);
                    Assert.IsTrue(enBases.ContainsKey(b), $"{table.Code}: '{key}' is not in the English table");
                    table.TryGet(key, out string text);
                    var names = new HashSet<string>(StringTable.Placeholders(text));
                    Assert.IsTrue(names.SetEquals(enBases[b]),
                        $"{table.Code}: '{key}' has placeholders {{{string.Join(",", names)}}}, English has {{{string.Join(",", enBases[b])}}}");
                }
            }
        }

        [Test]
        public void CountedLinesSpellEveryFormTheirLanguageNeeds()
        {
            foreach (var table in Shipped().Values)
            {
                var counted = new HashSet<string>();
                foreach (string key in table.Keys)
                    if (key.IndexOf(StringTable.PluralMark) > 0) counted.Add(StringTable.BaseKey(key));
                foreach (string b in counted)
                    foreach (var form in PluralRules.Required(table.Code))
                        Assert.IsTrue(table.TryGet(b + StringTable.PluralMark + PluralRules.Suffix(form), out _),
                            $"{table.Code}: '{b}' has no #{PluralRules.Suffix(form)} form");
            }
        }

        [Test]
        public void ParserIsLoudAboutABrokenFile()
        {
            Assert.Throws<System.FormatException>(() => DataLoader.ParseStringTable("{\"strings\":[]}"));
            Assert.Throws<System.FormatException>(() => DataLoader.ParseStringTable("{\"code\":\"en\"}"));
            Assert.Throws<System.FormatException>(() => DataLoader.ParseStringTable(
                "{\"code\":\"en\",\"strings\":[{\"key\":\"a\",\"text\":\"x\"},{\"key\":\"a\",\"text\":\"y\"}]}"));
        }
    }
}
