using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One ingredient's allowed share of a recipe (GDD 21 §9). Bounds are inclusive: a
    /// Martini specified at 55–75% gin accepts exactly 55% and exactly 75%, because a band
    /// the player can see should not have invisible slivers cut off its ends.
    /// </summary>
    public readonly struct RatioRequirement
    {
        public string IngredientId { get; }
        public double MinRatio { get; }
        public double MaxRatio { get; }

        public RatioRequirement(string ingredientId, double minRatio, double maxRatio)
        {
            if (string.IsNullOrWhiteSpace(ingredientId))
                throw new ArgumentException("Ingredient id is required", nameof(ingredientId));
            if (minRatio < 0 || maxRatio > 1 || maxRatio < minRatio)
                throw new ArgumentException($"Bad ratio band {minRatio}–{maxRatio} for '{ingredientId}'.");
            IngredientId = ingredientId;
            MinRatio = minRatio;
            MaxRatio = maxRatio;
        }

        public bool Accepts(double ratio) => ratio >= MinRatio && ratio <= MaxRatio;

        public override string ToString() => $"{IngredientId} {MinRatio:P0}–{MaxRatio:P0}";
    }

    /// <summary>
    /// Matches a poured glass against the recipe table by proportion instead of card count
    /// (GDD 21 §9).
    ///
    /// Recipes are a **bonus, not a gate**. Serving the emotionally correct drink satisfies
    /// the customer whether or not it happens to be a Martini; matching one pays Flavor, Mult
    /// and the charge multiplier on top. Inverting that would put the craft layer back in
    /// charge of a game that is now about reading people.
    /// </summary>
    public static class RatioRecipeMatcher
    {
        /// <summary>
        /// The best (highest-rank) recipe whose bands the glass satisfies, or null.
        /// A spilled glass matches nothing — it never reached anyone.
        /// </summary>
        public static RecipeMatch Match(GlassContents glass, IReadOnlyList<RecipeDefinition> recipes)
        {
            if (glass == null || glass.IsEmpty || glass.IsOverflowing || recipes == null) return null;

            RecipeDefinition best = null;
            foreach (var recipe in recipes)
            {
                if (recipe.RatioRequirements == null || recipe.RatioRequirements.Count == 0) continue;
                if (!Satisfies(glass, recipe)) continue;
                if (best == null || recipe.Rank > best.Rank) best = recipe;
            }

            // The scored "cards" are gone with the deck; a ratio match scores off the recipe
            // alone, so the card list is deliberately empty rather than faked.
            return best == null ? null : new RecipeMatch(best, Array.Empty<IngredientCard>());
        }

        private static bool Satisfies(GlassContents glass, RecipeDefinition recipe)
        {
            if (glass.FillFraction < recipe.MinFill) return false;

            double accounted = 0;
            foreach (var requirement in recipe.RatioRequirements)
            {
                double ratio = glass.RatioOf(requirement.IngredientId);
                if (!requirement.Accepts(ratio)) return false;
                accounted += ratio;
            }

            // Anything the recipe does not name is a stray. A splash is fine; a third of the
            // glass means this is a different drink wearing the recipe's proportions.
            return 1.0 - accounted <= MaxUnnamedShare + 1e-9;
        }

        /// <summary>How much of the glass may be ingredients the recipe never mentions.</summary>
        public const double MaxUnnamedShare = 0.10;
    }
}
