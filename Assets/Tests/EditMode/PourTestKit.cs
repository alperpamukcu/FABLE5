using System.Collections.Generic;
using System.Linq;
using LastCall.Core;

namespace LastCall.Tests
{
    /// <summary>
    /// Shared scaffolding for the pour system.
    ///
    /// Most suites in this assembly are not testing the deck — they used
    /// <c>run.Mix(new[]{ run.CurrentRound.Rail[0] })</c> as the cheapest way to advance the
    /// game so they could assert about shops, vouchers, packs, favours or patrons. Migrating
    /// seventeen files by hand invites seventeen chances to get it subtly wrong, so the
    /// scaffolding lives here once and every suite calls into it.
    /// </summary>
    public static class PourTestKit
    {
        /// <summary>A generic bar: enough distinct bottles to build anything a test needs.</summary>
        public static List<IngredientCard> SpiritCards(int count = 12, int flavor = 6) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, flavor))
                .ToList();

        /// <summary>
        /// Deduplicates by id the way <c>RunController.BuildShelf</c> does — the starter deck
        /// carries two Rye and two Single Malt, and shelf bottles are unique.
        /// </summary>
        public static Shelf NewShelf(IEnumerable<IngredientCard> cards = null, double capacity = 20)
        {
            var seen = new HashSet<string>();
            var bottles = new List<ShelfBottle>();
            foreach (var card in cards ?? SpiritCards())
                if (seen.Add(card.Id)) bottles.Add(new ShelfBottle(card, capacity));
            return new Shelf(bottles);
        }

        /// <summary>The id of the first bottle with anything left in it.</summary>
        public static string AnyBottle(RunController run) =>
            run.Shelf.Bottles.First(b => !b.IsEmpty).Id;

        public static string AnyBottle(RoundController round) =>
            round.Shelf.Bottles.First(b => !b.IsEmpty).Id;

        /// <summary>
        /// Pours a plain drink and hands it over. The default measure fills most of the glass,
        /// which keeps the drink well clear of both the empty and the spilled edges.
        /// </summary>
        public static ScoreBreakdown ServeSomething(RunController run, double volume = 0.6)
        {
            run.PourMeasure(AnyBottle(run), volume);
            return run.Serve();
        }

        public static ScoreBreakdown ServeSomething(RoundController round, double volume = 0.6)
        {
            round.PourMeasure(AnyBottle(round), volume);
            return round.Serve();
        }

        /// <summary>
        /// Serves until the customer is satisfied or gives up. Suites that only want to reach
        /// the Back Room use this rather than caring how many drinks it took.
        /// </summary>
        public static void WinCurrentCustomer(RunController run)
        {
            while (run.Phase == RunPhase.CustomerRound &&
                   run.CurrentRound.Phase == RoundPhase.InProgress)
            {
                ServeSomething(run);
            }
        }
    }
}
