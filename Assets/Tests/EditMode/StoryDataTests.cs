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
        public void The_shipped_arc_parses_against_the_real_cast_and_the_real_book()
        {
            var arc = Load();
            Assert.That(arc.Beats.Count, Is.EqualTo(4),
                "four nights are written; if that changed on purpose, change it here too");
            Assert.That(arc.Lessons.Count, Is.GreaterThan(0), "the host teaches");
        }

        [Test]
        public void The_arc_plays_the_calendar_the_design_says()
        {
            // GDD 26 §11's table, in one assertion: who, which night, which day.
            var arc = Load();
            var schedule = arc.Beats
                .Select(b => $"{b.Id}@{BarCalendar.Label(b.Day)}")
                .ToArray();

            Assert.That(schedule, Is.EqualTo(new[]
            {
                "ece_1@WEEK 1 · TUESDAY",
                "collector_1@WEEK 1 · FRIDAY",
                "influencer_1@WEEK 2 · SATURDAY",
                "gourmet_1@WEEK 3 · FRIDAY",
            }));
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
            // The arc's own shape: the last night is the hardest one written.
            var last = arc.Beats[arc.Beats.Count - 1];
            Assert.That(last.Trial.AllowedMistakes, Is.EqualTo(0), "the inspector allows none");
            Assert.That(last.Trial.Asks.Count, Is.EqualTo(3));
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
            var e = Refused(Wrap(@"
                { ""id"": ""b1"", ""character"": ""guest"", ""week"": 1, ""night"": ""monday"",
                  ""asks"": [""neat_pour""], ""seconds"": 60 }"));
            Assert.That(e.Message, Does.Contain("monday"));
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
