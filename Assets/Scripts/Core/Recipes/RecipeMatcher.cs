using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>Result of matching a mix: the winning recipe and which cards actually score.</summary>
    public sealed class RecipeMatch
    {
        public RecipeDefinition Recipe { get; }

        /// <summary>Scoring cards in mix order (left to right), per the scoring formula step 2.</summary>
        public IReadOnlyList<IngredientCard> ScoredCards { get; }

        /// <summary>
        /// How much each scored card counts, parallel to <see cref="ScoredCards"/>.
        ///
        /// Under the pour system this is the ingredient's share of the glass, so a drink that
        /// is 70% vodka scores 70% of vodka's Flavor (GDD 21 §4). Card-era matches leave it
        /// null, which means "everything counts once" — that is what keeps Flavor values,
        /// quality tiers and enhancements alive across the pivot instead of silently becoming
        /// dead content.
        /// </summary>
        public IReadOnlyList<double> ScoredWeights { get; }

        public RecipeMatch(RecipeDefinition recipe, IReadOnlyList<IngredientCard> scoredCards,
            IReadOnlyList<double> scoredWeights = null)
        {
            Recipe = recipe;
            ScoredCards = scoredCards;
            ScoredWeights = scoredWeights;
        }

        /// <summary>The weight of the card at <paramref name="index"/>; 1 when unweighted.</summary>
        public double WeightAt(int index) =>
            ScoredWeights != null && index >= 0 && index < ScoredWeights.Count
                ? ScoredWeights[index]
                : 1.0;
    }

    /// <summary>
    /// Pattern-matches a mix of 1–5 cards against the recipe table. Only the best
    /// (highest-rank) match applies (GDD 02). A mix that matches nothing returns null
    /// and scores zero — unlike poker there is no "high card" fallback.
    /// </summary>
    public static class RecipeMatcher
    {
        public static RecipeMatch Match(IReadOnlyList<IngredientCard> mix, IReadOnlyList<RecipeDefinition> recipes)
        {
            if (mix == null || mix.Count == 0 || recipes == null) return null;

            foreach (var recipe in recipes.OrderByDescending(r => r.Rank))
            {
                var scored = TryMatch(mix, recipe);
                if (scored != null) return new RecipeMatch(recipe, scored);
            }
            return null;
        }

        /// <summary>Returns the scored cards (mix order) if the recipe matches, else null.</summary>
        private static IReadOnlyList<IngredientCard> TryMatch(IReadOnlyList<IngredientCard> mix, RecipeDefinition recipe)
        {
            if (recipe.ExactMixSize > 0 && mix.Count != recipe.ExactMixSize) return null;
            if (recipe.MinMixSize > 0 && mix.Count < recipe.MinMixSize) return null;
            if (recipe.AllDistinctTypes && !AllTypesDistinct(mix)) return null;
            if (recipe.AllEqualFlavor && mix.Select(c => c.Flavor).Distinct().Count() != 1) return null;

            // Value/mono-Type group recipes (GDD 02 v1.1) have no type-slot requirements;
            // the qualifying group IS the pattern and the only scoring set.
            if (recipe.EqualFlavorGroupSize > 0) return MatchEqualFlavorGroup(mix, recipe.EqualFlavorGroupSize);
            if (recipe.AscendingFlavorGroupSize > 0) return MatchAscendingGroup(mix, recipe.AscendingFlavorGroupSize);
            if (recipe.SameTypeGroupMin > 0) return MatchSameTypeGroup(mix, recipe.SameTypeGroupMin);

            // Expand requirements into single-card slots, most restrictive (fewest types) first,
            // then backtrack. Candidates are tried highest Flavor first so when a requirement
            // could consume either of two cards the player keeps the better one scoring.
            var slots = new List<PatternRequirement>();
            foreach (var req in recipe.Requirements)
                for (int i = 0; i < req.Count; i++)
                    slots.Add(req);
            slots.Sort((a, b) => a.Types.Count.CompareTo(b.Types.Count));

            var assigned = new IngredientCard[slots.Count];
            var used = new HashSet<int>(); // card indices in mix
            if (!AssignSlot(0, slots, mix, used, assigned)) return null;

            if (recipe.ScoreAllMixCards) return mix.ToList();

            var scoredSet = new HashSet<IngredientCard>(assigned);
            return mix.Where(c => scoredSet.Contains(c)).ToList();
        }

        /// <summary>A Premium (wild) card counts as any one Type (GDD 3.3).</summary>
        private static bool IsWild(IngredientCard card) => card.Enhancement == Enhancement.Premium;

        /// <summary>House Special: the highest Flavor value shared by ≥ size cards scores.</summary>
        private static IReadOnlyList<IngredientCard> MatchEqualFlavorGroup(
            IReadOnlyList<IngredientCard> mix, int size)
        {
            var best = mix.GroupBy(c => c.Flavor)
                .Where(g => g.Count() >= size)
                .OrderByDescending(g => g.Key)
                .FirstOrDefault();
            if (best == null) return null;
            var scored = new HashSet<IngredientCard>(best.Take(size));
            return mix.Where(scored.Contains).ToList();
        }

        /// <summary>Layered Pour: one card each of the top <paramref name="size"/> distinct values.</summary>
        private static IReadOnlyList<IngredientCard> MatchAscendingGroup(
            IReadOnlyList<IngredientCard> mix, int size)
        {
            var groups = mix.GroupBy(c => c.Flavor).OrderByDescending(g => g.Key).ToList();
            if (groups.Count < size) return null;
            var scored = new HashSet<IngredientCard>(groups.Take(size).Select(g => g.First()));
            return mix.Where(scored.Contains).ToList();
        }

        /// <summary>Straight Booze: every card of the strongest Type (wilds complete the set).</summary>
        private static IReadOnlyList<IngredientCard> MatchSameTypeGroup(
            IReadOnlyList<IngredientCard> mix, int min)
        {
            var wilds = mix.Where(IsWild).ToList();
            var best = mix.Where(c => !IsWild(c))
                .GroupBy(c => c.Type)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(c => c.Flavor))
                .FirstOrDefault();
            int printedCount = best != null ? best.Count() : 0;
            if (printedCount + wilds.Count < min) return null;

            var scored = new HashSet<IngredientCard>(wilds);
            if (best != null) foreach (var card in best) scored.Add(card);
            return mix.Where(scored.Contains).ToList();
        }

        /// <summary>
        /// Distinctness check with wilds: printed types must not repeat, and each wild
        /// claims one of the remaining unused types (6 exist, mixes hold at most 5).
        /// </summary>
        private static bool AllTypesDistinct(IReadOnlyList<IngredientCard> mix)
        {
            var printed = mix.Where(c => !IsWild(c)).Select(c => c.Type).ToList();
            return printed.Distinct().Count() == printed.Count;
        }

        private static bool AssignSlot(int slotIndex, List<PatternRequirement> slots,
            IReadOnlyList<IngredientCard> mix, HashSet<int> used, IngredientCard[] assigned)
        {
            if (slotIndex == slots.Count) return true;

            var slot = slots[slotIndex];
            // Natural type matches are tried before wilds so a Premium card is only
            // spent on a slot no printed type can fill.
            var candidates = Enumerable.Range(0, mix.Count)
                .Where(i => !used.Contains(i) && (slot.Types.Contains(mix[i].Type) || IsWild(mix[i])))
                .OrderBy(i => slot.Types.Contains(mix[i].Type) ? 0 : 1)
                .ThenByDescending(i => mix[i].Flavor);

            foreach (int i in candidates)
            {
                used.Add(i);
                assigned[slotIndex] = mix[i];
                if (AssignSlot(slotIndex + 1, slots, mix, used, assigned)) return true;
                used.Remove(i);
            }
            return false;
        }
    }
}
