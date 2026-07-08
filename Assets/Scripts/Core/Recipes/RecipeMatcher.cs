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

        public RecipeMatch(RecipeDefinition recipe, IReadOnlyList<IngredientCard> scoredCards)
        {
            Recipe = recipe;
            ScoredCards = scoredCards;
        }
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
            if (recipe.AllDistinctTypes && mix.Select(c => c.Type).Distinct().Count() != mix.Count) return null;
            if (recipe.AllEqualFlavor && mix.Select(c => c.Flavor).Distinct().Count() != 1) return null;

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

        private static bool AssignSlot(int slotIndex, List<PatternRequirement> slots,
            IReadOnlyList<IngredientCard> mix, HashSet<int> used, IngredientCard[] assigned)
        {
            if (slotIndex == slots.Count) return true;

            var slot = slots[slotIndex];
            var candidates = Enumerable.Range(0, mix.Count)
                .Where(i => !used.Contains(i) && slot.Types.Contains(mix[i].Type))
                .OrderByDescending(i => mix[i].Flavor);

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
