using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The Cabinet: a draw pile and a discard pile. Restocked (discarded) and mixed cards
    /// go to the discard pile and do not return until the next customer reshuffle.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<IngredientCard> _drawPile = new List<IngredientCard>();
        private readonly List<IngredientCard> _discardPile = new List<IngredientCard>();

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public Deck(IEnumerable<IngredientCard> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _drawPile.AddRange(cards);
        }

        /// <summary>Fisher–Yates shuffle of the draw pile.</summary>
        public void Shuffle(SeededRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>Draws up to <paramref name="count"/> cards; fewer if the pile runs dry.</summary>
        public List<IngredientCard> Draw(int count)
        {
            int n = Math.Min(count, _drawPile.Count);
            var drawn = new List<IngredientCard>(n);
            for (int i = 0; i < n; i++)
            {
                int last = _drawPile.Count - 1;
                drawn.Add(_drawPile[last]);
                _drawPile.RemoveAt(last);
            }
            return drawn;
        }

        public void Discard(IEnumerable<IngredientCard> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _discardPile.AddRange(cards);
        }

        /// <summary>Returns all discards to the draw pile (start of a new customer).</summary>
        public void ResetForNewCustomer(SeededRng rng)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(rng);
        }
    }
}
