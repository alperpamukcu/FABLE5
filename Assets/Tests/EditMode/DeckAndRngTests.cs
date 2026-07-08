using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class DeckAndRngTests
    {
        private static List<IngredientCard> MakeCards(int count) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"card_{i}", $"Card {i}", IngredientType.Spirit, 1 + i % 11))
                .ToList();

        [Test]
        public void SameSeed_SameShuffleOrder()
        {
            var deckA = new Deck(MakeCards(48));
            var deckB = new Deck(MakeCards(48));
            deckA.Shuffle(new RunRng("MARTINI-77").GetStream("deck"));
            deckB.Shuffle(new RunRng("MARTINI-77").GetStream("deck"));

            var idsA = deckA.Draw(48).Select(c => c.Id).ToList();
            var idsB = deckB.Draw(48).Select(c => c.Id).ToList();
            CollectionAssert.AreEqual(idsA, idsB);
        }

        [Test]
        public void DifferentSeeds_DifferentShuffleOrder()
        {
            var deckA = new Deck(MakeCards(48));
            var deckB = new Deck(MakeCards(48));
            deckA.Shuffle(new RunRng("MARTINI-77").GetStream("deck"));
            deckB.Shuffle(new RunRng("NEGRONI-13").GetStream("deck"));

            var idsA = deckA.Draw(48).Select(c => c.Id).ToList();
            var idsB = deckB.Draw(48).Select(c => c.Id).ToList();
            CollectionAssert.AreNotEqual(idsA, idsB);
        }

        [Test]
        public void NamedStreams_AreIndependent()
        {
            // Consuming the deck stream must not change what the shop stream produces.
            var untouched = new RunRng("SEED").GetStream("shop");
            var expected = Enumerable.Range(0, 20).Select(_ => untouched.NextUInt()).ToList();

            var rng = new RunRng("SEED");
            for (int i = 0; i < 100; i++) rng.GetStream("deck").NextUInt();
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

        [Test]
        public void Draw_RemovesCards_AndCapsAtPileSize()
        {
            var deck = new Deck(MakeCards(10));
            var first = deck.Draw(8);
            Assert.AreEqual(8, first.Count);
            Assert.AreEqual(2, deck.DrawCount);

            var rest = deck.Draw(8);
            Assert.AreEqual(2, rest.Count);
            Assert.AreEqual(0, deck.DrawCount);
        }

        [Test]
        public void ResetForNewCustomer_ReturnsDiscardsToDrawPile()
        {
            var deck = new Deck(MakeCards(10));
            var drawn = deck.Draw(6);
            deck.Discard(drawn);
            Assert.AreEqual(4, deck.DrawCount);
            Assert.AreEqual(6, deck.DiscardCount);

            deck.ResetForNewCustomer(new RunRng("SEED").GetStream("deck"));
            Assert.AreEqual(10, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }
    }
}
