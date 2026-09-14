using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// Localization L0 (2026-09-13): the language list, CLDR plural forms, capitals per language,
    /// the table's refusals and the localizer's fallback. Pure Core — no files, no Unity — so these
    /// also run outside the editor.
    /// </summary>
    public sealed class LocalizationTests
    {
        private static StringTable Table(string code, params string[] keyThenText)
        {
            var entries = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < keyThenText.Length; i += 2)
                entries.Add(new KeyValuePair<string, string>(keyThenText[i], keyThenText[i + 1]));
            return new StringTable(code, entries);
        }

        // ---- languages ------------------------------------------------------------------

        [Test]
        public void TwentyNineLanguagesWithUniqueCodes()
        {
            Assert.AreEqual(29, Languages.All.Count);
            var codes = new HashSet<string>();
            var steam = new HashSet<string>();
            foreach (var info in Languages.All)
            {
                Assert.IsTrue(codes.Add(info.Code), "code " + info.Code + " twice");
                Assert.IsTrue(steam.Add(info.SteamApi), "steam code " + info.SteamApi + " twice");
                Assert.IsFalse(string.IsNullOrEmpty(info.Name));
                if (info.Fallback != null) Assert.IsNotNull(Languages.Find(info.Fallback), info.Code + " falls back to an unshipped language");
            }
            Assert.AreEqual("en", Languages.All[0].Code);
        }

        [Test]
        public void SteamAndSystemNamesMapToCodes()
        {
            Assert.AreEqual("zh-CN", Languages.FromSteam("schinese"));
            Assert.AreEqual("zh-TW", Languages.FromSteam("tchinese"));
            Assert.AreEqual("ko", Languages.FromSteam("koreana"));
            Assert.AreEqual("es-419", Languages.FromSteam("latam"));
            Assert.AreEqual("pt-BR", Languages.FromSteam("brazilian"));
            Assert.IsNull(Languages.FromSteam("arabic"));
            Assert.AreEqual("zh-TW", Languages.FromSystemLanguage("ChineseTraditional"));
            Assert.AreEqual("tr", Languages.FromSystemLanguage("Turkish"));
            Assert.IsNull(Languages.FromSystemLanguage("Arabic"));
        }

        [Test]
        public void ChainsEndInEnglishAndUkrainianNeverReadsRussian()
        {
            CollectionAssert.AreEqual(new[] { "es-419", "es", "en" }, Languages.Chain("es-419"));
            CollectionAssert.AreEqual(new[] { "ms", "id", "en" }, Languages.Chain("ms"));
            CollectionAssert.AreEqual(new[] { "uk", "en" }, Languages.Chain("uk"));
            CollectionAssert.AreEqual(new[] { "en" }, Languages.Chain("en"));
            CollectionAssert.AreEqual(new[] { "en" }, Languages.Chain("xx"));
        }

        // ---- plurals --------------------------------------------------------------------

        [TestCase("en", 0, PluralCategory.Other)]
        [TestCase("en", 1, PluralCategory.One)]
        [TestCase("en", 2, PluralCategory.Other)]
        [TestCase("tr", 1, PluralCategory.One)]
        [TestCase("fr", 0, PluralCategory.One)]
        [TestCase("fr", 1, PluralCategory.One)]
        [TestCase("fr", 2, PluralCategory.Other)]
        [TestCase("pt-BR", 0, PluralCategory.One)]
        [TestCase("pt", 0, PluralCategory.Other)]
        [TestCase("es", 1000000, PluralCategory.Many)]
        [TestCase("ru", 1, PluralCategory.One)]
        [TestCase("ru", 2, PluralCategory.Few)]
        [TestCase("ru", 5, PluralCategory.Many)]
        [TestCase("ru", 11, PluralCategory.Many)]
        [TestCase("ru", 12, PluralCategory.Many)]
        [TestCase("ru", 21, PluralCategory.One)]
        [TestCase("ru", 22, PluralCategory.Few)]
        [TestCase("ru", 104, PluralCategory.Few)]
        [TestCase("ru", 111, PluralCategory.Many)]
        [TestCase("uk", 23, PluralCategory.Few)]
        [TestCase("pl", 1, PluralCategory.One)]
        [TestCase("pl", 2, PluralCategory.Few)]
        [TestCase("pl", 5, PluralCategory.Many)]
        [TestCase("pl", 21, PluralCategory.Many)]
        [TestCase("pl", 22, PluralCategory.Few)]
        [TestCase("cs", 1, PluralCategory.One)]
        [TestCase("cs", 3, PluralCategory.Few)]
        [TestCase("cs", 5, PluralCategory.Other)]
        [TestCase("ro", 0, PluralCategory.Few)]
        [TestCase("ro", 1, PluralCategory.One)]
        [TestCase("ro", 19, PluralCategory.Few)]
        [TestCase("ro", 20, PluralCategory.Other)]
        [TestCase("ro", 101, PluralCategory.Other)]
        [TestCase("ro", 102, PluralCategory.Few)]
        [TestCase("ja", 1, PluralCategory.Other)]
        [TestCase("zh-CN", 2, PluralCategory.Other)]
        public void PluralFormsFollowCldr(string code, long n, PluralCategory expected)
        {
            Assert.AreEqual(expected, PluralRules.For(code, n));
        }

        [Test]
        public void EveryFormARuleCanPickIsRequiredOrFallsToOther()
        {
            foreach (var info in Languages.All)
            {
                var required = PluralRules.Required(info.Code);
                for (long n = 0; n <= 2000; n++)
                {
                    var c = PluralRules.For(info.Code, n);
                    Assert.IsTrue(required.Contains(c) || required.Contains(PluralCategory.Other),
                        $"{info.Code}: {n} picks {c}, which a table need not spell and cannot fall back from");
                }
            }
        }

        // ---- capitals -------------------------------------------------------------------

        [Test]
        public void CapitalsFollowTheLanguage()
        {
            Assert.AreEqual("İSTANBUL ILIK", TextCase.Upper("tr", "istanbul ılık"));
            Assert.AreEqual("ISTANBUL", TextCase.Upper("en", "istanbul"));
            Assert.AreEqual("STRASSE", TextCase.Upper("de", "Straße"));
            Assert.AreEqual("ΕΛΕΓΞΕ ΤΗΝ ΤΑΥΤΟΤΗΤΑ", TextCase.Upper("el", "Έλεγξε την ταυτότητα"));
            Assert.AreEqual("ΪΣ", TextCase.Upper("el", "ΐς"));
            Assert.AreEqual("ПРОВЕРЬ", TextCase.Upper("ru", "проверь"));
            Assert.AreEqual("调酒", TextCase.Upper("zh-CN", "调酒"));
        }

        [Test]
        public void CapitalsLeaveRichTextTagsAlone()
        {
            Assert.AreEqual("<color=#ff7dc6>LAST</color> CALL", TextCase.Upper("en", "<color=#ff7dc6>last</color> call"));
            Assert.AreEqual("<b>İÇKİ</b>", TextCase.Upper("tr", "<b>içki</b>"));
        }

        // ---- the table ------------------------------------------------------------------

        [Test]
        public void TableRefusesWhatWouldShowWrong()
        {
            Assert.Throws<ArgumentException>(() => Table("xx", "a", "b"), "unknown language");
            Assert.Throws<ArgumentException>(() => Table("en", "", "b"), "empty key");
            Assert.Throws<ArgumentException>(() => Table("en", "a b", "c"), "space in key");
            Assert.Throws<ArgumentException>(() => Table("en", "a", "x", "a", "y"), "duplicate");
            Assert.Throws<ArgumentException>(() => Table("en", "a#lots", "x"), "bad plural suffix");
            Assert.Throws<ArgumentException>(() => Table("en", "#one", "x"), "suffix without a key");
            Assert.Throws<ArgumentException>(() => Table("en", "a", "{open"), "brace never closes");
            Assert.Throws<ArgumentException>(() => Table("en", "a", "shut}"), "brace closes nothing");
            Assert.Throws<ArgumentException>(() => Table("en", "a", "{Bad Name}"), "placeholder not snake");
            Assert.DoesNotThrow(() => Table("en", "a", "{drink_2} and {n}", "a#one", "one {n}"));
        }

        [Test]
        public void PlaceholdersAndBaseKeysAreRead()
        {
            CollectionAssert.AreEqual(new[] { "n", "drink" }, StringTable.Placeholders("{n} x {drink} y {n}"));
            Assert.AreEqual("night.count", StringTable.BaseKey("night.count#few"));
            Assert.AreEqual("plain", StringTable.BaseKey("plain"));
        }

        // ---- the localizer --------------------------------------------------------------

        [Test]
        public void ArgumentsGoWhereTheLanguagePutsThem()
        {
            var en = Table("en", "order", "One {drink}, {guest}.", "drink.mojito", "Mojito");
            var tr = Table("tr", "order", "{guest} bir {drink} istiyor.", "drink.mojito", "Mojito");
            var line = Line.Of("order").With("drink", Line.Of("drink.mojito")).With("guest", "Ece");
            Assert.AreEqual("One Mojito, Ece.", new Localizer("en", new[] { en, tr }).Render(line));
            Assert.AreEqual("Ece bir Mojito istiyor.", new Localizer("tr", new[] { en, tr }).Render(line));
        }

        [Test]
        public void CountedLinesPickTheirFormByTheTablesLanguage()
        {
            var en = Table("en", "nights#one", "{n} night", "nights#other", "{n} nights");
            var ru = Table("ru", "nights#one", "{n} ночь", "nights#few", "{n} ночи", "nights#many", "{n} ночей");
            var ruSpeaker = new Localizer("ru", new[] { en, ru });
            Assert.AreEqual("21 ночь", ruSpeaker.Render(Line.Of("nights").Counting("n", 21)));
            Assert.AreEqual("3 ночи", ruSpeaker.Render(Line.Of("nights").Counting("n", 3)));
            Assert.AreEqual("11 ночей", ruSpeaker.Render(Line.Of("nights").Counting("n", 11)));
            Assert.AreEqual("1 night", new Localizer("en", new[] { en, ru }).Render(Line.Of("nights").Counting("n", 1)));
        }

        [Test]
        public void AMissingFormReadsOther()
        {
            var es = Table("es", "glasses#one", "{n} vaso", "glasses#other", "{n} vasos");
            Assert.AreEqual("1000000 vasos", new Localizer("es", new[] { es }).Render(Line.Of("glasses").Counting("n", 1000000)));
        }

        [Test]
        public void MissingLinesFallDownTheChainThenShowTheKey()
        {
            var en = Table("en", "a", "English A", "b", "English B", "c", "English C");
            var es = Table("es", "a", "A de España", "b", "B de España");
            var latam = Table("es-419", "a", "A latina");
            var speaker = new Localizer("es-419", new[] { en, es, latam });
            Assert.AreEqual("A latina", speaker.Get("a"));
            Assert.AreEqual("B de España", speaker.Get("b"));
            Assert.AreEqual("English C", speaker.Get("c"));
            Assert.AreEqual("[d]", speaker.Get("d"));
            Assert.IsTrue(speaker.Has("c"));
            Assert.IsFalse(speaker.Has("d"));
        }

        [Test]
        public void AnUnknownLanguageSpeaksEnglishAndAnUnfilledPlaceholderStaysVisible()
        {
            var en = Table("en", "a", "Hello {name}");
            var speaker = new Localizer("xx", new[] { en });
            Assert.AreEqual("en", speaker.Code);
            Assert.AreEqual("Hello {name}", speaker.Get("a"));
        }

        [Test]
        public void TheLocalizerCapitalizesInItsOwnLanguage()
        {
            Assert.AreEqual("İÇKİ", new Localizer("tr", new StringTable[0]).Upper("içki"));
            Assert.AreEqual("ICKI", new Localizer("en", new StringTable[0]).Upper("icki"));
        }
    }
}
