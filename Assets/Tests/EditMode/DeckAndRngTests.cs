using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// Determinism guarantees (GDD 13): the custom PCG32 must reproduce identical sequences
    /// from identical string seeds, on every platform, with independent named streams.
    ///
    /// This suite used to also cover the Deck (shuffle/draw/discard); the Deck died with the
    /// pour pivot and its tests went with it — the file keeps its name so history stays
    /// findable.
    /// </summary>
    public class DeckAndRngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new RunRng("MARTINI-77").GetStream("deal");
            var b = new RunRng("MARTINI-77").GetStream("deal");

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at draw {i}");
        }

        [Test]
        public void DifferentSeeds_DifferentSequences()
        {
            var a = new RunRng("MARTINI-77").GetStream("deal");
            var b = new RunRng("NEGRONI-13").GetStream("deal");

            bool anyDifferent = Enumerable.Range(0, 20).Any(_ => a.NextUInt() != b.NextUInt());
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void NamedStreams_AreIndependent()
        {
            // Consuming one stream must not change what another produces — pouring never
            // perturbs the shop.
            var untouched = new RunRng("SEED").GetStream("shop");
            var expected = Enumerable.Range(0, 20).Select(_ => untouched.NextUInt()).ToList();

            var rng = new RunRng("SEED");
            for (int i = 0; i < 100; i++) rng.GetStream("customer").NextUInt();
            var actual = Enumerable.Range(0, 20).Select(_ => rng.GetStream("shop").NextUInt()).ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void NextInt_StaysInBounds()
        {
            var rng = new RunRng("BOUNDS").GetStream("test");
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(8);
                Assert.That(v, Is.InRange(0, 7));
            }
        }
    }
}
