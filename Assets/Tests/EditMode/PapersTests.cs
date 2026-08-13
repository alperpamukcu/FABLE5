using System;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The cast's papers, now that they are content instead of code (PLAN_last_call S0).
    ///
    /// Two jobs. The first is a NET UNDER THE MOVE: three people are pinned to the exact
    /// papers they had while they lived in TycoonHud, so a careless edit to the file — or a
    /// bad hand-merge of it — is caught here rather than on a licence in play. The second is
    /// the loud-failure contract every content file in this project signs: the mistakes that
    /// would otherwise fail silently, on a card the player is being asked to READ, have to
    /// throw at load.
    /// </summary>
    public sealed class PapersTests
    {
        /// <summary>The SHIPPED file, read off disk. EditMode tests run inside the editor,
        /// so the real content is right there — testing a copy would only prove the copy.</summary>
        private static PatronRoster Load()
        {
            string path = Application.dataPath + "/Data/customers/papers.json";
            Assert.That(System.IO.File.Exists(path), Is.True, $"the cast file is missing: {path}");
            return DataLoader.ParsePapers(System.IO.File.ReadAllText(path));
        }

        [Test]
        public void The_cast_file_parses_and_holds_the_whole_bar()
        {
            var cast = Load();
            Assert.That(cast.All.Count, Is.EqualTo(31),
                "the bar has 30 drinkers and Ece behind it; if that changed on purpose, change it here too");
        }

        [TestCase("", "Miles Corrigan", 26, "us")]
        [TestCase("nightnurse", "Marilou Cabrera", 37, "ph")]
        [TestCase("profess", "Ulrich Brenner", 66, "de")]
        [TestCase("execman", "Graham Sedgwick", 54, "gb")]
        public void A_face_still_carries_the_papers_it_had_in_code(
            string slug, string name, int age, string iso)
        {
            var papers = Load().For(slug);
            Assert.That(papers, Is.Not.Null, $"nobody claims the look '{slug}'");
            Assert.That(papers.Name, Is.EqualTo(name));
            Assert.That(papers.Age, Is.EqualTo(age));
            Assert.That(papers.Iso, Is.EqualTo(iso));
        }

        [Test]
        public void An_unclaimed_look_answers_with_nothing_and_not_with_somebody_elses_papers()
        {
            Assert.That(Load().For("nobody_by_that_name"), Is.Null);
        }

        [Test]
        public void The_story_characters_faces_are_in_the_cast()
        {
            // GDD 26 §8: a story character IS a face plus its papers. If one of the reserved
            // looks ever leaves the file, the arc loses its person — quietly, at the far end.
            // (Recast 2026-08-13: ember went back to the crowd, teal is the influencer.)
            var cast = Load();
            foreach (var slug in new[] { "execman", "teal", "profess" })
                Assert.That(cast.For(slug), Is.Not.Null,
                    $"'{slug}' is reserved for the last-call arc and has no papers");
        }

        [Test]
        public void Ece_is_in_the_cast_and_is_turkish()
        {
            // GDD 26 §1b: she works the shift, so she has papers like anyone else — but she
            // is never rolled into the crowd. Losing this row would leave the story's host
            // nameless on the plate, which is the one line the arc cannot do without.
            var ece = Load().For("ece");
            Assert.That(ece, Is.Not.Null, "the bar has no bartender");
            Assert.That(ece.Name, Is.EqualTo("Ece Toprak"));
            Assert.That(ece.Iso, Is.EqualTo("tr"));
        }

        // ── the loud failures ────────────────────────────────────────────────────

        [Test]
        public void Two_people_claiming_one_face_is_refused()
        {
            const string json = @"{""version"":1,""papers"":[
                {""slug"":""ember"",""name"":""Meredith Nolan"",""age"":34,""country"":""United States"",""iso"":""us""},
                {""slug"":""ember"",""name"":""Somebody Else"",""age"":30,""country"":""United States"",""iso"":""us""}]}";
            var e = Assert.Throws<FormatException>(() => DataLoader.ParsePapers(json));
            Assert.That(e.Message, Does.Contain("ember"));
            Assert.That(e.Message, Does.Contain("Somebody Else"));
        }

        [Test]
        public void A_flag_the_art_cannot_draw_is_refused()
        {
            const string json = @"{""version"":1,""papers"":[
                {""slug"":""x"",""name"":""Test Person"",""age"":30,""country"":""Nowhere"",""iso"":""USA""}]}";
            var e = Assert.Throws<FormatException>(() => DataLoader.ParsePapers(json));
            Assert.That(e.Message, Does.Contain("USA"));
        }

        [Test]
        public void Papers_with_no_name_are_refused()
        {
            const string json = @"{""version"":1,""papers"":[
                {""slug"":""x"",""name"":"""",""age"":30,""country"":""Nowhere"",""iso"":""us""}]}";
            Assert.Throws<FormatException>(() => DataLoader.ParsePapers(json));
        }

        [Test]
        public void An_empty_cast_is_refused()
        {
            Assert.Throws<FormatException>(() => DataLoader.ParsePapers(@"{""version"":1,""papers"":[]}"));
        }
    }
}
