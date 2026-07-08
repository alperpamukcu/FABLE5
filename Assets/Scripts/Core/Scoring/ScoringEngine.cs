using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Pure scoring function (GDD 02 + 13): no rendering, no state, no randomness.
    /// Order of operations:
    ///   1. Base Flavor and Mult from the recipe at its current level.
    ///   2. Each scored card left to right: add its Flavor value, then its per-card
    ///      effects (quality tier; seals/enhancements/Patron card-triggers arrive in M2+).
    ///   3. "On hand scored" effects in Patron slot order (M2+ hook).
    ///   4. FinalScore = Flavor × Mult.
    /// </summary>
    public static class ScoringEngine
    {
        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel = 1)
        {
            if (match == null) return ScoreBreakdown.NoRecipe;

            var recipe = match.Recipe;
            var steps = new List<ScoreStep>();
            double flavor = recipe.FlavorAtLevel(recipeLevel);
            double mult = recipe.MultAtLevel(recipeLevel);
            steps.Add(new ScoreStep($"{recipe.Name} (Lv{recipeLevel})", EffectOp.AddFlavor, flavor, flavor, mult));

            foreach (var card in match.ScoredCards)
            {
                if (card.Quality != QualityTier.Bootleg)
                {
                    flavor += card.Flavor;
                    steps.Add(new ScoreStep(card.Name, EffectOp.AddFlavor, card.Flavor, flavor, mult));
                }
                else
                {
                    steps.Add(new ScoreStep($"{card.Name} (Bootleg)", EffectOp.AddFlavor, 0, flavor, mult));
                }

                switch (card.Quality)
                {
                    case QualityTier.TopShelf:
                        flavor += 30;
                        steps.Add(new ScoreStep($"{card.Name} (Top Shelf)", EffectOp.AddFlavor, 30, flavor, mult));
                        break;
                    case QualityTier.BarrelAged:
                        mult += 8;
                        steps.Add(new ScoreStep($"{card.Name} (Barrel-Aged)", EffectOp.AddMult, 8, flavor, mult));
                        break;
                    case QualityTier.Signature:
                        mult *= 1.5;
                        steps.Add(new ScoreStep($"{card.Name} (Signature)", EffectOp.MultMult, 1.5, flavor, mult));
                        break;
                }
            }

            // Step 3 (Patron "on hand scored" effects) plugs in here in M2.

            return new ScoreBreakdown(recipe, recipeLevel, match.ScoredCards, steps, flavor, mult);
        }
    }
}
