using System.Collections.Generic;
using System.Linq;
using LastCall.Core;

namespace LastCall.Tests
{
    /// <summary>
    /// Shared shelf scaffolding for the pour suites. The old-loop serve helpers retired with
    /// <c>RunController</c> in the tycoon demolition; what survives is the bottle building the
    /// pure pour and market tests still need.
    /// </summary>
    public static class PourTestKit
    {
        /// <summary>A generic bar: enough distinct bottles to build anything a test needs.</summary>
        public static List<IngredientCard> SpiritCards(int count = 12, int flavor = 6) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, flavor))
                .ToList();

        /// <summary>Deduplicates by id the way the shelf builder does; shelf bottles are unique.</summary>
        public static Shelf NewShelf(IEnumerable<IngredientCard> cards = null, double capacity = 20)
        {
            var seen = new HashSet<string>();
            var bottles = new List<ShelfBottle>();
            foreach (var card in cards ?? SpiritCards())
                if (seen.Add(card.Id)) bottles.Add(new ShelfBottle(card, capacity));
            return new Shelf(bottles);
        }
    }
}
