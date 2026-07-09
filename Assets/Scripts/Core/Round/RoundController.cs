using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly HashSet<string> _mixedRecipeIds = new HashSet<string>();
        private readonly SeededRng _shatterRng;
        private readonly List<IngredientCard> _lastShattered = new List<IngredientCard>();
        private readonly List<IngredientCard> _lastDoubledCopies = new List<IngredientCard>();

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

        /// <summary>The active VIP rules; <see cref="VipRuleSet.Empty"/> for regular customers.</summary>
        public VipRuleSet VipRules { get; }

        /// <summary>Frozen cards destroyed by the shatter roll of the most recent Mix.</summary>
        public IReadOnlyList<IngredientCard> LastShatteredCards => _lastShattered;

        /// <summary>Permanent copies minted by Doubled cards in the most recent Mix.</summary>
        public IReadOnlyList<IngredientCard> LastDoubledCopies => _lastDoubledCopies;

        public RoundController(Deck deck, IReadOnlyList<RecipeDefinition> recipes,
            CustomerOrder customer, RoundConfig config = null,
            IReadOnlyDictionary<string, int> recipeLevels = null,
            IReadOnlyList<PatronInstance> patrons = null,
            VipRuleSet vipRules = null, SeededRng shatterRng = null)
        {
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            Config = config ?? RoundConfig.Default;
            _recipeLevels = recipeLevels;
            _patrons = patrons ?? Array.Empty<PatronInstance>();
            VipRules = vipRules ?? VipRuleSet.Empty;
            _shatterRng = shatterRng; // null = Frozen cards never shatter (bench setups)

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
            if (IsVoidedByVipRule(match, out string voidReason))
                return ScoreBreakdown.Voided(match.Recipe, LevelOf(match), voidReason);

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
            ScoreBreakdown breakdown;
            if (match != null && IsVoidedByVipRule(match, out string voidReason))
            {
                // Voided mixes still consume the attempt and the cards — the rule text
                // is on the ticket; ignoring it is the player's mistake (GDD 6).
                breakdown = ScoreBreakdown.Voided(match.Recipe, LevelOf(match), voidReason);
            }
            else
            {
                breakdown = ScoringEngine.Score(match, LevelOf(match), _patrons, BuildContext(selection, match));
            }
            if (match != null) _mixedRecipeIds.Add(match.Recipe.Id);
            AccumulatedScore += breakdown.FinalScore;

            RemoveFromRail(selection);
            _deck.Discard(ResolveScoredEnhancements(selection, match, breakdown.IsVoided));
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

        /// <summary>
        /// Applies a single-use Tool to rail cards (GDD 7.3). Consuming the tool from the
        /// inventory is the run layer's job. Destroyed cards leave the run for good (they
        /// never return to the deck); copies join the rail and get discarded into the deck
        /// with it, becoming permanent.
        /// </summary>
        public void ApplyTool(ToolDefinition tool, IReadOnlyList<IngredientCard> targets)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            EnsureRoundInProgress();
            ValidateToolTargets(tool, targets);

            switch (tool.Op)
            {
                case ToolOp.Enhance:
                    foreach (var card in targets) card.Enhance(tool.Enhancement);
                    break;
                case ToolOp.Destroy:
                    foreach (var card in targets) _rail.Remove(card);
                    break;
                case ToolOp.Copy:
                    foreach (var card in targets)
                        _rail.Insert(_rail.IndexOf(card) + 1, card.Clone());
                    break;
                case ToolOp.ConvertType:
                    foreach (var card in targets) card.ConvertType(tool.ConvertTo);
                    break;
            }
        }

        private void ValidateToolTargets(ToolDefinition tool, IReadOnlyList<IngredientCard> targets)
        {
            if (targets == null || targets.Count == 0)
                throw new ArgumentException("Select at least one card.", nameof(targets));
            if (targets.Count > tool.MaxTargets)
                throw new ArgumentException($"{tool.Name} targets at most {tool.MaxTargets} card(s).", nameof(targets));

            var seen = new HashSet<IngredientCard>();
            foreach (var card in targets)
            {
                if (!seen.Add(card))
                    throw new ArgumentException($"Card '{card.Name}' selected twice.", nameof(targets));
                if (!_rail.Contains(card))
                    throw new ArgumentException($"Card '{card.Name}' is not on the rail.", nameof(targets));
            }
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

        /// <summary>
        /// Post-scoring deck mutations for scored, non-debuffed cards (GDD 3.3): a Frozen
        /// card rolls 1-in-4 to shatter (destroyed instead of discarded); a Doubled card
        /// mints a plain permanent copy into the deck (plain, so copies don't re-copy).
        /// Voided and recipe-less mixes score nothing, so nothing shatters or doubles.
        /// Returns the cards to discard.
        /// </summary>
        private IReadOnlyList<IngredientCard> ResolveScoredEnhancements(
            IReadOnlyList<IngredientCard> selection, RecipeMatch match, bool voided)
        {
            _lastShattered.Clear();
            _lastDoubledCopies.Clear();
            if (match == null || voided) return selection;

            var discard = new List<IngredientCard>(selection);
            foreach (var card in match.ScoredCards)
            {
                if (VipRules.DebuffedTypes.Contains(card.Type)) continue;

                if (card.Enhancement == Enhancement.Frozen &&
                    _shatterRng != null && _shatterRng.NextInt(4) == 0)
                {
                    discard.Remove(card);
                    _lastShattered.Add(card);
                }
                else if (card.Enhancement == Enhancement.Doubled)
                {
                    var copy = card.Clone();
                    copy.Enhance(Enhancement.None);
                    discard.Add(copy);
                    _lastDoubledCopies.Add(copy);
                }
            }
            return discard;
        }

        private EffectContext BuildContext(IReadOnlyList<IngredientCard> selection, RecipeMatch match) =>
            new EffectContext(selection, match?.Recipe, MixesUsed, RestocksUsed, VipRules.DebuffedTypes);

        private bool IsVoidedByVipRule(RecipeMatch match, out string reason)
        {
            if (VipRules.OnlyFirstMixScores && MixesUsed > 0)
            {
                reason = "only the first Mix counts";
                return true;
            }
            if (VipRules.MinRecipeLevel > 0 && LevelOf(match) < VipRules.MinRecipeLevel)
            {
                reason = $"only Recipes level {VipRules.MinRecipeLevel}+ score";
                return true;
            }
            if (VipRules.EachMixDifferentRecipe && _mixedRecipeIds.Contains(match.Recipe.Id))
            {
                reason = "every Mix must be a different Recipe";
                return true;
            }
            reason = string.Empty;
            return false;
        }

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
