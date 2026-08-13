using System;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The written nights as CONTENT (GDD 26 §10, PLAN_last_call S2a) — the shipped
    /// `story.json`, read off disk and built against the real cast and the real recipe book.
    ///
    /// This is the file nobody plays: a story that names a drink which does not exist, a face
    /// nobody has papers for, or a guest written for a Wednesday would not crash and would not
    /// go red — it would simply be a customer who never walks in, six weeks later, on somebody
    /// else's machine. So the file is checked here, and every one of those is a loud failure.
    /// </summary>
    public sealed class StoryDataTests
    {
        private static PatronRoster Cast() =>
            DataLoader.ParsePapers(Read("/Data/customers/papers.json"));

        private static System.Collections.Generic.IReadOnlyList<RecipeDefinition> Book() =>
            DataLoader.ParseRecipes(Read("/Data/recipes/recipes.json"));

        private static StoryArc Load() =>
            DataLoader.ParseStory(Read("/Data/story/story.json"), Cast(), Book());

        private static string Read(string relative)
        {
            string path = Application.dataPath + relative;
            Assert.That(System.IO.File.Exists(path), Is.True, $"missing content file: {path}");
            return System.IO.File.ReadAllText(path);
        }

        [Test]
        public void The_shipped_arc_is_ece_and_only_ece_for_now()
        {
            // THE AUTHOR'S CALL, 2026-08-13: one written night while the story around her is
            // built. The three guests who follow are complete and parked in
            // Docs/story_guests_drafted.json — nothing loads that file, and this test is what
            // notices if one of them is put back without the rest of the shape being ready.
            var arc = Load();
            Assert.That(arc.Beats.Count, Is.EqualTo(1),
                "the live arc is Ece's night alone; the rest wait in Docs/story_guests_drafted.json");
            Assert.That(arc.Beats[0].Id, Is.EqualTo("ece_1"));
            Assert.That(arc.Beats[0].Who.IsHost, Is.True, "and she is the house, not a guest");
            Assert.That(arc.Lessons.Count, Is.GreaterThan(0), "the host still teaches");
        }

        [Test]
        public void The_arc_plays_the_calendar_the_design_says()
        {
            // GDD 26 §11's table, in one assertion: who, which night, which day.
            var arc = Load();
            var schedule = arc.Beats
                .Select(b => $"{b.Id}@{BarCalendar.Label(b.Day)}")
                .ToArray();

            Assert.That(schedule, Is.EqualTo(new[] { "ece_1@WEEK 1 · MONDAY" }));
        }

        [Test]
        public void The_parked_guests_are_still_good_content()
        {
            // The drafts are not loaded by the game, so nothing else would ever catch a typo
            // in them — and a beat that has quietly rotted in a drawer is worse than no beat,
            // because it is discovered on the day somebody wants to use it. Built here against
            // the same cast and the same book the live arc is built against.
            var drafts = Read("/../Docs/story_guests_drafted.json");
            Assert.That(drafts, Does.Contain("gourmet_1"), "the drafted guests went missing");

            // Bolt Ece back on so the file is a legal arc, then build it exactly as the
            // loader would: every rule the live file obeys is checked on the drafts too.
            string live = Read("/Data/story/story.json");
            var merged = MergeForCheck(live, drafts);
            var arc = DataLoader.ParseStory(merged, Cast(), Book());
            Assert.That(arc.Beats.Count, Is.EqualTo(4), "one host night and three guests");
            foreach (var beat in arc.Beats)
                Assert.That(beat.Who.IsHost || BarCalendar.IsWeekend(beat.Night), Is.True,
                    $"'{beat.Id}' brings a guest in on a {BarCalendar.Name(beat.Night)}");
        }

        /// <summary>Ece's file with the drafted guests spliced back in, in order, the way a
        /// writer would do it by hand — the chain relinked from her to the first of them.</summary>
        private static string MergeForCheck(string live, string drafts)
        {
            var liveDoc = MiniJson(live);
            var draftDoc = MiniJson(drafts);
            string characters = draftDoc.characters.TrimStart('[').TrimEnd(']');
            string beats = draftDoc.beats.TrimStart('[').TrimEnd(']');
            string ece = liveDoc.beats.TrimStart('[').TrimEnd(']')
                .Replace("\"next\": \"\"", "\"next\": \"collector_1\"");
            return "{\"version\":3,\"characters\":["
                   + liveDoc.characters.TrimStart('[').TrimEnd(']') + "," + characters
                   + "],\"beats\":[" + ece + "," + beats + "]}";
        }

        /// <summary>The two top-level arrays out of a story file, as raw text. A parser for
        /// two keys, because the alternative is a JSON library this project does not have.</summary>
        private static (string characters, string beats) MiniJson(string json)
        {
            return (Slice(json, "\"characters\""), Slice(json, "\"beats\""));

            string Slice(string src, string key)
            {
                int at = src.IndexOf(key, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), $"no {key} in the file");
                int open = src.IndexOf('[', at), depth = 0;
                for (int i = open; i < src.Length; i++)
                {
                    if (src[i] == '[') depth++;
                    else if (src[i] == ']' && --depth == 0) return src.Substring(open, i - open + 1);
                }
                Assert.Fail($"{key} never closes");
                return "";
            }
        }

        [Test]
        public void Only_the_house_works_a_quiet_night()
        {
            foreach (var beat in Load().Beats)
                Assert.That(beat.Who.IsHost || BarCalendar.IsWeekend(beat.Night), Is.True,
                    $"'{beat.Id}' brings a guest in on a {BarCalendar.Name(beat.Night)}");
        }

        [Test]
        public void The_trials_get_harder_and_none_of_them_is_impossible()
        {
            var arc = Load();
            foreach (var beat in arc.Beats)
            {
                var trial = beat.Trial;
                Assert.That(trial.Asks.Count, Is.InRange(1, 5), $"'{beat.Id}' asks for too much");
                Assert.That(trial.Seconds / trial.Asks.Count, Is.GreaterThanOrEqualTo(30.0),
                    $"'{beat.Id}' leaves under 30s a drink — that is not a trial, it is a joke");
                Assert.That(trial.MinFill, Is.EqualTo(StoryTrial.DefaultMinFill),
                    "the fill standard is the house's, not the beat's, unless somebody meant it");
            }
            // The opener is the kind one: it teaches the beat on a night nothing can be lost
            // (GDD 26 §1b), so it may not be the hardest thing in the file.
            var opener = arc.Opener;
            Assert.That(opener.Trial.Asks.Count, Is.EqualTo(1), "one drink to learn the shape on");
            Assert.That(opener.Trial.AllowedMistakes, Is.GreaterThan(0), "and room to get it wrong");
        }

        [Test]
        public void Every_ask_is_a_drink_the_bar_could_learn()
        {
            var book = Book().ToDictionary(r => r.Id);
            foreach (var beat in Load().Beats)
                foreach (var ask in beat.Trial.Asks)
                    Assert.That(book.ContainsKey(ask.Id), Is.True,
                        $"'{beat.Id}' asks for '{ask.Id}', which is not in recipes.json");
        }

        [Test]
        public void The_host_warns_about_what_the_shelf_will_need()
        {
            // The asks come one at a time (§4), so the host's early warning is the ONLY notice
            // a player gets. A beat that needs a style and never says the word is the "quest
            // or tease" rule broken in data rather than in code — the loader refuses it, and
            // this pins that the SHIPPED file is on the right side of that refusal.
            foreach (var beat in Load().Beats)
            {
                if (string.IsNullOrEmpty(beat.NeedStyle)) continue;
                Assert.That(beat.Lines.HostWarning.Any(l =>
                        l.IndexOf(beat.NeedStyle, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.True,
                    $"'{beat.Id}' needs {beat.NeedStyle} and nobody says the word days early");
            }
        }

        [Test]
        public void The_story_borrows_a_face_it_actually_owns()
        {
            var ece = Load().Cast.First(c => c.IsHost);
            Assert.That(ece.Name, Is.EqualTo("Ece Toprak"), "the papers come off the look");
            Assert.That(ece.PlaceholderLook, Is.Not.Null,
                "until her portrait is drawn the plate borrows one — delete the field with the art");
            Assert.That(Cast().For(ece.PlaceholderLook), Is.Not.Null);
        }

        // ── the loud failures ────────────────────────────────────────────────────

        private static string Wrap(string beatBody, string characters = null) => @"{
            ""version"": 2,
            ""characters"": [" + (characters ?? @"
                { ""id"": ""ece"", ""look"": ""ece"", ""role"": ""host"" },
                { ""id"": ""guest"", ""look"": ""execman"", ""role"": ""guest"" }") + @"],
            ""beats"": [" + beatBody + @"]
        }";

        private static readonly string GoodBeat = @"
            { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""friday"",
              ""asks"": [""neat_pour""], ""seconds"": 60 }";

        private static FormatException Refused(string json) =>
            Assert.Throws<FormatException>(() => DataLoader.ParseStory(json, Cast(), Book()));

        [Test]
        public void A_face_nobody_has_papers_for_is_refused()
        {
            var e = Refused(Wrap(GoodBeat, @"
                { ""id"": ""ece"", ""look"": ""ece"", ""role"": ""host"" },
                { ""id"": ""guest"", ""look"": ""nobody_by_that_name"", ""role"": ""guest"" }"));
            Assert.That(e.Message, Does.Contain("nobody_by_that_name"));
        }

        [Test]
        public void A_drink_that_does_not_exist_is_refused()
        {
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""friday"",
                  ""asks"": [""neat_pour"", ""unicorn_fizz""], ""seconds"": 60 }"));
            Assert.That(e.Message, Does.Contain("unicorn_fizz"));
        }

        [Test]
        public void A_guest_on_a_quiet_night_is_refused()
        {
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""wednesday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60 }"));
            Assert.That(e.Message, Does.Contain("weekend"));
        }

        [Test]
        public void A_night_that_is_not_a_night_is_refused()
        {
            // SUNDAY IS THE DAY OFF (2026-08-14): it is drawn on the calendar and it is not a
            // night, so a beat written for one is a beat that could never happen.
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""sunday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60 }"));
            Assert.That(e.Message, Does.Contain("sunday"));
        }

        [Test]
        public void A_beat_about_nobody_is_refused()
        {
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""ghost"", ""week"": 1, ""night"": ""friday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60 }"));
            Assert.That(e.Message, Does.Contain("ghost"));
        }

        [Test]
        public void A_trial_with_no_time_is_refused()
        {
            Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""friday"",
                  ""asks"": [""neat_pour""], ""seconds"": 0 }"));
        }

        [Test]
        public void A_bar_with_two_hosts_or_none_is_refused()
        {
            var two = Refused(Wrap(GoodBeat, @"
                { ""id"": ""ece"", ""look"": ""ece"", ""role"": ""host"" },
                { ""id"": ""other"", ""look"": ""glam"", ""role"": ""host"" },
                { ""id"": ""guest"", ""look"": ""execman"", ""role"": ""guest"" }"));
            Assert.That(two.Message, Does.Contain("2 hosts"));

            var none = Refused(Wrap(GoodBeat, @"
                { ""id"": ""guest"", ""look"": ""execman"", ""role"": ""guest"" }"));
            Assert.That(none.Message, Does.Contain("0 hosts"));
        }

        [Test]
        public void A_role_the_game_does_not_know_is_refused()
        {
            var e = Refused(Wrap(GoodBeat, @"
                { ""id"": ""ece"", ""look"": ""ece"", ""role"": ""host"" },
                { ""id"": ""guest"", ""look"": ""execman"", ""role"": ""bouncer"" }"));
            Assert.That(e.Message, Does.Contain("bouncer"));
        }

        [Test]
        public void A_lesson_nothing_watches_for_is_refused()
        {
            string json = @"{
                ""version"": 2,
                ""characters"": [
                    { ""id"": ""ece"", ""look"": ""ece"", ""role"": ""host"" },
                    { ""id"": ""guest"", ""look"": ""execman"", ""role"": ""guest"" }],
                ""beats"": [" + GoodBeat + @"],
                ""lessons"": [ { ""id"": ""l1"", ""when"": ""when_the_moon_is_right"",
                                 ""say"": [""...""] } ]
            }";
            var e = Refused(json);
            Assert.That(e.Message, Does.Contain("when_the_moon_is_right"));
            Assert.That(e.Message, Does.Contain("first_night"), "and it lists what it does know");
        }

        [Test]
        public void A_beat_leading_nowhere_is_refused()
        {
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""friday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60, ""next"": ""b2"" }"));
            Assert.That(e.Message, Does.Contain("b2"));
        }

        [Test]
        public void An_empty_story_is_refused()
        {
            Refused(@"{ ""version"": 2, ""characters"": [], ""beats"": [] }");
        }

        [Test]
        public void A_beat_that_needs_a_style_and_never_says_the_word_is_refused()
        {
            // The arc's first standing rule, enforced where it gets broken: in the writing.
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""friday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60, ""needStyle"": ""bourbon"",
                  ""hostWarning"": [""Friday is going to be a difficult one.""] }"));
            Assert.That(e.Message, Does.Contain("bourbon"));
            Assert.That(e.Message, Does.Contain("hostWarning"));
        }
    }
}
