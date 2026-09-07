using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The crowd's voices (2026-09-06): a data-driven book of lines per cue, picked on the
    /// run's own rng stream. These pin the shape of the shipped file and the rules of the
    /// pick — no repeats back to back, placeholders always filled, sentence case as written.
    /// </summary>
    public sealed class VoiceBookTests
    {
        private static VoiceBook Shipped()
        {
            string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data", "voices.json");
            Assert.IsTrue(System.IO.File.Exists(path), "the voices file ships at " + path);
            return DataLoader.ParseVoices(System.IO.File.ReadAllText(path));
        }

        private static VoiceDefinition Voice(string id, params string[] people)
        {
            var lines = new Dictionary<VoiceCue, IReadOnlyList<string>>();
            foreach (VoiceCue cue in System.Enum.GetValues(typeof(VoiceCue)))
                lines[cue] = cue == VoiceCue.Sip
                    ? new[] { id + " says {advice_l}", id + ": {advice}" }
                    : cue == VoiceCue.Order
                        ? new[] { id + " wants {drink}.", id + " asks for {drink}." }
                        : new[] { id + " " + cue + " one.", id + " " + cue + " two.", id + " " + cue + " three." };
            return new VoiceDefinition(id, id, id == "default" ? new[] { "us" } : new[] { id.Substring(0, 2) }, people, lines);
        }

        [Test]
        public void TheShippedBook_HasADefaultAndAHouseAccent_AndEveryCueEverywhere()
        {
            var book = Shipped();
            Assert.IsNotNull(book.Default);
            Assert.GreaterOrEqual(book.Voices.Count, 8, "a crowd needs more than a handful of ways to talk");
            Assert.AreEqual("miami", book.ById("miami").Id, "the Malibu Club is in Miami");
            foreach (var v in book.Voices)
                foreach (VoiceCue cue in System.Enum.GetValues(typeof(VoiceCue)))
                    Assert.GreaterOrEqual(v.Lines(cue).Count, 2, $"{v.Id} has too few {cue} lines to vary");
        }

        [Test]
        public void TheShippedLines_AreSentenceCase_AndTheShoutsAreShort()
        {
            var book = Shipped();
            foreach (var v in book.Voices)
                foreach (VoiceCue cue in System.Enum.GetValues(typeof(VoiceCue)))
                    foreach (var line in v.Lines(cue))
                    {
                        string filled = VoiceBook.Fill(line, "Gin Sour", "A little less gin next time.");
                        char first = filled[0];
                        Assert.IsFalse(char.IsLower(first), $"{v.Id}/{cue}: \"{line}\" starts lower-case");
                        Assert.IsFalse(filled == filled.ToUpperInvariant() && filled.Length > 6,
                            $"{v.Id}/{cue}: \"{line}\" is shouted in the source; the pop-up capitalises for itself");
                        char end = filled[filled.Length - 1];
                        Assert.IsTrue(end == '.' || end == '!' || end == '?' || end == '…',
                            $"{v.Id}/{cue}: \"{line}\" does not end a sentence");
                        if (cue == VoiceCue.Perfect || cue == VoiceCue.Another || cue == VoiceCue.Close || cue == VoiceCue.Wrong)
                            Assert.LessOrEqual(filled.Length, 20, $"{v.Id}/{cue}: \"{line}\" is too long for the pop-up");
                    }
        }

        [Test]
        public void ThePersonBeatsTheFlag_AndTheFlagBeatsTheDefault()
        {
            var book = new VoiceBook(new[] { Voice("default"), Voice("brit", "execman"), Voice("miami", "bouncer") });
            Assert.AreEqual("brit", book.For("execman", "us").Id, "the look's own voice wins over the flag");
            Assert.AreEqual("brit", book.For("nobody", "br").Id, "then the flag");
            Assert.AreEqual("miami", book.For(null, "MI").Id, "flag codes are matched lower-case");
            Assert.AreEqual("default", book.For("stranger", "zz").Id, "then the default");
            Assert.AreEqual("default", book.For(null, null).Id);
        }

        [Test]
        public void TheShippedCast_SpeaksInMoreThanOneVoice()
        {
            var book = Shipped();
            Assert.AreEqual("miami", book.For("bouncer", "us").Id);
            Assert.AreEqual("nyc", book.For("courier", "us").Id);
            Assert.AreEqual("brit", book.For("execman", "gb").Id);
            Assert.AreEqual("turkish", book.For("barber", "tr").Id);
            Assert.AreEqual("default", book.For("", "us").Id, "the nameless fallback face speaks plainly");
            Assert.AreEqual("german", book.For("someone-new", "de").Id, "a new German face has a voice before anyone writes one");
        }

        [Test]
        public void ALineIsPickedOnTheStream_AndNeverRepeatedBackToBack()
        {
            var book = new VoiceBook(new[] { Voice("default") });
            var rng = new RunRng("voices").GetStream("voice");
            string last = null;
            for (int i = 0; i < 40; i++)
            {
                string now = book.Say(book.Default, VoiceCue.Perfect, rng);
                Assert.AreNotEqual(last, now, "the same line twice running");
                last = now;
            }
        }

        [Test]
        public void TheSameSeed_SaysTheSameThings()
        {
            var a = new VoiceBook(new[] { Voice("default") });
            var b = new VoiceBook(new[] { Voice("default") });
            var ra = new RunRng("night-seven").GetStream("voice");
            var rb = new RunRng("night-seven").GetStream("voice");
            for (int i = 0; i < 12; i++)
                Assert.AreEqual(a.Say(a.Default, VoiceCue.Leaving, ra), b.Say(b.Default, VoiceCue.Leaving, rb));
        }

        [Test]
        public void PlaceholdersAreFilled_AndTheAdviceKeepsItsSense()
        {
            Assert.AreEqual("Mate, a little less gin next time.",
                VoiceBook.Fill("Mate, {advice_l}", null, "A little less gin next time."));
            Assert.AreEqual("Look, I asked for the ice as well.",
                VoiceBook.Fill("Look, {advice_l}", null, "I asked for the ice as well."), "a leading I stays capital");
            Assert.AreEqual("Hm. A lot more head next time.",
                VoiceBook.Fill("Hm. {advice}", null, "A lot more head next time."));
            Assert.AreEqual("Gin Sour, please.", VoiceBook.Fill("{drink}, please.", "Gin Sour", null));
            var book = Shipped();
            var rng = new RunRng("x").GetStream("voice");
            foreach (var v in book.Voices)
                foreach (VoiceCue cue in System.Enum.GetValues(typeof(VoiceCue)))
                {
                    string said = book.Say(v, cue, rng, "Spritz", "A touch more soda next time.");
                    Assert.IsFalse(said.Contains("{") || said.Contains("}"), $"{v.Id}/{cue}: \"{said}\" left a placeholder");
                    if (cue == VoiceCue.Sip) StringAssert.Contains("more soda next time", said);
                    if (cue == VoiceCue.Order) StringAssert.Contains("Spritz", said);
                }
        }

        [Test]
        public void ABookWithoutADefault_OrAVoiceMissingACue_IsRefused()
        {
            Assert.Throws<System.ArgumentException>(() => new VoiceBook(new[] { Voice("brit") }));
            var lines = new Dictionary<VoiceCue, IReadOnlyList<string>>();
            foreach (VoiceCue cue in System.Enum.GetValues(typeof(VoiceCue)))
                if (cue != VoiceCue.Kicked) lines[cue] = new[] { cue == VoiceCue.Sip ? "{advice}" : "Fine." };
            Assert.Throws<System.ArgumentException>(() => new VoiceDefinition("mute", "Mute", null, null, lines));
            lines[VoiceCue.Kicked] = new[] { "Fine." };
            lines[VoiceCue.Sip] = new[] { "No advice here." };
            Assert.Throws<System.ArgumentException>(() => new VoiceDefinition("deaf", "Deaf", null, null, lines),
                "a sip line that never says what to fix");
        }
    }
}
