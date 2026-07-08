using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    public enum RoundPhase
    {
        InProgress,
        Won,
        Lost
    }

    /// <summary>
    /// One customer round (GDD 00, per-customer loop): the rail is filled to
    /// <see cref="RoundConfig.RailSize"/>, the player Mixes (scores) or Restocks (discards)
    /// selections of 1–5 cards, and the round ends Won when the accumulated score reaches
    /// the order's target or Lost when the last Mix falls short.
    /// The caller owns deck shuffling; this class never touches randomness.
    /// Patron slot order = roster list order (gameplay-relevant, GDD 08).
    /// </summary>
    public sealed class RoundController
    {
        private readonly Deck _deck;
        private readonly IReadOnlyList<RecipeDefinition> _recipes;
        private readonly IReadOnlyDictionary<string, int> _recipeLevels;
        private readonly IReadOnlyList<PatronInstance> _patrons;
        private readonly List<IngredientCard> _rail = new List<IngredientCard>();

        public CustomerOrder Customer { get; }
        public RoundConfig Config { get; }
        public IReadOnlyList<IngredientCard> Rail => _rail;
        public int MixesRemaining { get; private set; }
        public int RestocksRemaining { get; private set; }
        public double AccumulatedScore { get; private set; }
        public RoundPhase Phase { get; private set; }
        public int DeckDrawCount => _deck.DrawCount;
        public int DeckDiscardCount => _deck.DiscardCount;
        public int MixesUsed => Config.MixesPerCustomer - MixesRemaining;
        public int RestocksUsed => Config.RestocksPerCustomer - RestocksRemaining;
        public IReadOnlyList<PatronInstance> Patrons => _patrons;

        /// <summary>Money owed by OnCustomerEnd patron effects; set once when the round is won.</summary>
        public double PatronPayout { get; private set; }

        public RoundController(Deck deck, IReadOnlyList<RecipeDefinition> recipes,
            CustomerOrder customer, RoundConfig config = null,
            IReadOnlyDictionary<string, int> recipeLevels = null,
            IReadOnlyList<PatronInstance> patrons = null)
        {
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            Config = config ?? RoundConfig.Default;
            _recipeLevels = recipeLevels;
            _patrons = patrons ?? Array.Empty<PatronInstance>();

            MixesRemaining = Config.MixesPerCustomer;
            RestocksRemaining = Config.RestocksPerCustomer;
            Phase = RoundPhase.InProgress;
            FillRail();
        }

        /// <summary>Non-consuming lookup for the UI's live "this would be a Martini" label.</summary>
        public RecipeMatch PreviewMatch(IReadOnlyList<IngredientCard> selection) =>
            selection == null || selection.Count == 0 || selection.Count > Config.MaxMixSelection
                ? null
                : RecipeMatcher.Match(selection, _recipes);

        /// <summary>
        /// Non-consuming full score preview. Runs the same engine path as Mix, but scaling
        /// patrons must not grow from previews, so accumulator changes are rolled back.
        /// </summary>
        public ScoreBreakdown PreviewScore(IReadOnlyList<IngredientCard> selection)
        {
            var match = PreviewMatch(selection);
            if (match == null) return ScoreBreakdown.NoRecipe;

            var accumulatedBefore = new double[_patrons.Count];
            for (int i = 0; i < _patrons.Count; i++) accumulatedBefore[i] = _patrons[i].Accumulated;

            var breakdown = ScoringEngine.Score(match, LevelOf(match), _patrons, BuildContext(selection, match));

            for (int i = 0; i < _patrons.Count; i++)
            {
                double delta = accumulatedBefore[i] - _patrons[i].Accumulated;
                if (delta != 0) _patrons[i].Accumulate(delta);
            }
            return breakdown;
        }

        /// <summary>Plays the selection as a cocktail. Returns the breakdown (zero score if no recipe matched).</summary>
        public ScoreBreakdown Mix(IReadOnlyList<IngredientCard> selection)
        {
            EnsureRoundInProgress();
            ValidateSelection(selection);

            var match = RecipeMatcher.Match(selection, _recipes);
            var breakdown = ScoringEngine.Score(match, LevelOf(match), _patrons, BuildContext(selection, match));
            AccumulatedScore += breakdown.FinalScore;

            RemoveFromRail(selection);
            _deck.Discard(selection);
            MixesRemaining--;

            if (AccumulatedScore >= Customer.TargetScore)
            {
                Phase = RoundPhase.Won;
                PatronPayout = PatronTriggers.ResolveMoney(EffectTrigger.OnCustomerEnd, _patrons,
                    new EffectContext(null, null, MixesUsed, RestocksUsed));
            }
            else if (MixesRemaining == 0)
            {
                Phase = RoundPhase.Lost;
            }
            else
            {
                FillRail();
            }

            return breakdown;
        }

        /// <summary>Discards the selection and draws replacements.</summary>
        public void Restock(IReadOnlyList<IngredientCard> selection)
        {
            EnsureRoundInProgress();
            ValidateSelection(selection);
            if (RestocksRemaining <= 0) throw new InvalidOperationException("No Restocks remaining.");

            RemoveFromRail(selection);
            _deck.Discard(selection);
            RestocksRemaining--;
            FillRail();
        }

        private EffectContext BuildContext(IReadOnlyList<IngredientCard> selection, RecipeMatch match) =>
            new EffectContext(selection, match?.Recipe, MixesUsed, RestocksUsed);

        private int LevelOf(RecipeMatch match) =>
            match != null && _recipeLevels != null && _recipeLevels.TryGetValue(match.Recipe.Id, out int level)
                ? level
                : 1;

        private void FillRail()
        {
            int missing = Config.RailSize - _rail.Count;
            if (missing > 0) _rail.AddRange(_deck.Draw(missing));
        }

        private void RemoveFromRail(IReadOnlyList<IngredientCard> selection)
        {
            foreach (var card in selection) _rail.Remove(card);
        }

        private void EnsureRoundInProgress()
        {
            if (Phase != RoundPhase.InProgress)
                throw new InvalidOperationException($"Round is over ({Phase}).");
        }

        private void ValidateSelection(IReadOnlyList<IngredientCard> selection)
        {
            if (selection == null || selection.Count == 0)
                throw new ArgumentException("Select at least one card.", nameof(selection));
            if (selection.Count > Config.MaxMixSelection)
                throw new ArgumentException($"Select at most {Config.MaxMixSelection} cards.", nameof(selection));

            var seen = new HashSet<IngredientCard>();
            foreach (var card in selection)
            {
                if (!seen.Add(card))
                    throw new ArgumentException($"Card '{card.Name}' selected twice.", nameof(selection));
                if (!_rail.Contains(card))
                    throw new ArgumentException($"Card '{card.Name}' is not on the rail.", nameof(selection));
            }
        }
    }
}
