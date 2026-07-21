using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The hidden information has to stay hidden. Everything the player sees is derived from
    /// <see cref="CustomerRead"/>; the moment a preview or a label reaches past it into
    /// <see cref="RegularState.Stats"/>, blind reads stop being reads. These tests pin the
    /// boundary at its source — the readings themselves and the factory that builds them.
    /// (The Chat/preview/resonance probes retired with the card loop; the read integrity
    /// they guarded now lives here and in <c>RegularsAndReadTests</c>.)
    /// </summary>
    public class ReadIntegrityTests
    {
        [Test]
        public void AnUnknownReading_ExposesNoBound()
        {
            // StatReading.Unknown must not carry the truth in its Low/High: it is rendered,
            // logged and previewed, and anything stored on it is one ToString() from leaking.
            var unknown = StatReading.Unknown;

            Assert.AreEqual(EmotionStats.Min, unknown.Low);
            Assert.AreEqual(EmotionStats.Max, unknown.High);
            Assert.AreEqual("??", unknown.ToString());
        }

        [Test]
        public void ARangeReading_NeverPrintsTheExactValue()
        {
            var reading = StatReading.Range(50, 8);

            Assert.AreEqual("42-58", reading.ToString());
            Assert.AreNotEqual(reading.Low, reading.High, "a zero-width range would be an Exact in disguise");
        }

        [Test]
        public void TheCard_ShowsAtMostOneExactValue()
        {
            var truth = new EmotionStats(new[] { 11, 22, 33, 44, 55, 66 });

            for (int i = 0; i < 25; i++)
            {
                var read = CustomerReadFactory.Build(truth, 3, new RunRng($"s{i}").GetStream("read"));
                int exact = Emotions.All.Count(e => read[e].Tier == VisibilityTier.Exact);
                Assert.AreEqual(1, exact, "exactly one stat is ever printed outright");
            }
        }
    }
}
