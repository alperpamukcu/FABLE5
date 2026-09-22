using System;
using System.Collections.Generic;
using System.IO;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// WHAT THE LICENCE PRINTS FITS THE LICENCE (2026-09-22, the author's eighth list: "Kimlikte yazan isimler yazılar
    /// çok uzun olmamalı sayfa düzeni için bir karakter sınırı koymalıyız ve o karakter sınırına göre ülke isimleri
    /// veya müşteri isimleri seçmeliyiz").
    ///
    /// The card sets the first name and the surname in two boxes side by side and the nationality in a half-width
    /// one. A name too long for its box used to be a card that printed over its own rule, in a language nobody here
    /// reads, found by nobody. The limits are <see cref="Papers"/>'s, the loader refuses a row that breaks them, and
    /// these hold the SHIPPED files and the shipped tables to them.
    ///
    /// It also guards the strangers (Resources/Data/strangers.json), whose licences a minor borrows: each one is a
    /// face the bar never draws, so each needs a photograph of its own and none of them may be in the cast.
    /// </summary>
    public sealed class LicenceTextTests
    {
        [Serializable] private sealed class Row { public string key; public string text; }
        [Serializable] private sealed class Table { public string code; public List<Row> strings; }

        private const string CastPath = "/Data/customers/papers.json";
        private const string StrangersPath = "/Resources/Data/strangers.json";
        private const string FacesFolder = "/Resources/Strangers/";
        private const string LocFolder = "/Resources/Data/loc/";

        private static PatronRoster Read(string relative)
        {
            string path = Application.dataPath + relative;
            Assert.That(File.Exists(path), Is.True, $"the file is missing: {path}");
            return DataLoader.ParsePapers(File.ReadAllText(path));
        }

        private static Table Loc(string code)
        {
            string path = Application.dataPath + LocFolder + code + ".json";
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(path));
            Assert.That(table?.strings, Is.Not.Null, $"{code}.json did not parse");
            return table;
        }

        [Test]
        public void The_strangers_parse_and_every_one_of_them_has_a_photograph()
        {
            var strangers = Read(StrangersPath);
            Assert.That(strangers.All.Count, Is.GreaterThan(5),
                "a borrowed card picks one of these; a handful is not enough for a run");
            foreach (var p in strangers.All)
            {
                Assert.That(p.Slug, Is.Not.Empty, $"'{p.Name}' has no photograph to be");
                string png = Application.dataPath + FacesFolder + p.Slug + ".png";
                Assert.That(File.Exists(png), Is.True,
                    $"'{p.Name}' lends a card with no face on it: {png} is missing (Tools/stranger_faces.py writes it)");
                var bytes = File.ReadAllBytes(png);
                // the PNG header's IHDR: width and height, big-endian, at 16 and 20
                int w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                int h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                Assert.That(new[] { w, h }, Is.EqualTo(new[] { 64, 64 }),
                    $"'{p.Slug}.png' is {w}x{h}; the card draws a face at exactly 2x, so it has to be the cast's 64x64");
                Assert.That(p.Age, Is.GreaterThan(20), $"'{p.Name}' could not lend a card that passes for of age");
            }
        }

        [Test]
        public void No_stranger_is_in_the_cast()
        {
            // A stranger the bar can seat is not a stranger: the tell of a borrowed card is that the person on the
            // stool is not the man on it, and that dies the moment he can walk in himself.
            var cast = Read(CastPath);
            foreach (var p in Read(StrangersPath).All)
                Assert.That(cast.For(p.Slug), Is.Null,
                    $"'{p.Slug}' lends borrowed cards AND has papers in the cast file");
        }

        [Test]
        public void Every_flag_a_card_can_print_has_a_nationality_word()
        {
            var en = Loc("en");
            var have = new HashSet<string>();
            foreach (var row in en.strings)
                if (row.key.StartsWith("id.nationality.", StringComparison.Ordinal))
                    have.Add(row.key.Substring("id.nationality.".Length));
            var wanted = new SortedSet<string>();
            foreach (var p in Read(CastPath).All) if (!string.IsNullOrEmpty(p.Iso)) wanted.Add(p.Iso);
            foreach (var p in Read(StrangersPath).All) if (!string.IsNullOrEmpty(p.Iso)) wanted.Add(p.Iso);
            foreach (var iso in wanted)
                Assert.That(have, Does.Contain(iso),
                    $"the licence prints a nationality and has no word for '{iso}' (add id.nationality.{iso})");
        }

        [Test]
        public void Every_nationality_word_fits_the_box_it_is_printed_in()
        {
            foreach (var file in Directory.GetFiles(Application.dataPath + LocFolder, "*.json"))
            {
                var table = JsonUtility.FromJson<Table>(File.ReadAllText(file));
                if (table?.strings == null) continue;
                foreach (var row in table.strings)
                {
                    if (!row.key.StartsWith("id.nationality.", StringComparison.Ordinal)) continue;
                    Assert.That(row.text.Length, Is.LessThanOrEqualTo(Papers.NationalityMax),
                        $"{Path.GetFileName(file)}: '{row.text}' ({row.key}) is {row.text.Length} letters and the box "
                        + $"prints {Papers.NationalityMax} — pick the short demonym");
                }
            }
        }

        [Test]
        public void A_first_name_the_card_cannot_print_is_refused()
        {
            string json = @"{""version"":1,""papers"":[{""slug"":""x"",""name"":""Maximilianus Vega"",""age"":30,
                             ""country"":""Spain"",""iso"":""es""}]}";
            var e = Assert.Throws<FormatException>(() => DataLoader.ParsePapers(json));
            Assert.That(e.Message, Does.Contain("first name"));
        }

        [Test]
        public void A_surname_the_card_cannot_print_is_refused()
        {
            string json = @"{""version"":1,""papers"":[{""slug"":""x"",""name"":""Ana Hollingsworth"",""age"":30,
                             ""country"":""United Kingdom"",""iso"":""gb""}]}";
            var e = Assert.Throws<FormatException>(() => DataLoader.ParsePapers(json));
            Assert.That(e.Message, Does.Contain("surname"));
        }

        [Test]
        public void The_shipped_names_are_all_inside_the_limits()
        {
            // Parsing IS the check (the loader refuses a long one), so this says which file failed.
            foreach (var path in new[] { CastPath, StrangersPath })
                foreach (var p in Read(path).All)
                {
                    Assert.That(p.First.Length, Is.LessThanOrEqualTo(Papers.FirstNameMax), $"{path}: {p.Name}");
                    Assert.That(p.Surname.Length, Is.LessThanOrEqualTo(Papers.SurnameMax), $"{path}: {p.Name}");
                }
        }

        [Test]
        public void Nobody_in_the_cast_shares_a_name_with_anybody_else()
        {
            // Two faces called one name is a card that cannot be read: the player learns a name, and it walks in
            // wearing somebody else's face. (It happened: 'shaved' and 'kadikoy' were both Emre Kayhan until
            // 2026-09-22.)
            var seen = new Dictionary<string, string>();
            foreach (var p in Read(CastPath).All)
            {
                Assert.That(seen.ContainsKey(p.Name), Is.False,
                    $"'{p.Name}' is the name of two faces: {(seen.TryGetValue(p.Name, out var other) ? other : "")} and {p.Slug}");
                seen[p.Name] = p.Slug;
            }
        }
    }
}
