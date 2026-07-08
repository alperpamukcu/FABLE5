using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>One applied operation; the UI replays these as the scoring animation.</summary>
    public sealed class ScoreStep
    {
        public string Source { get; }
        public EffectOp Op { get; }
        public double Value { get; }
        public double FlavorAfter { get; }
        public double MultAfter { get; }

        public ScoreStep(string source, EffectOp op, double value, double flavorAfter, double multAfter)
        {
            Source = source;
            Op = op;
            Value = value;
            FlavorAfter = flavorAfter;
            MultAfter = multAfter;
        }

        public override string ToString() =>
            $"{Source}: {Op} {Value} -> Flavor {FlavorAfter}, Mult {MultAfter}";
    }

    /// <summary>Full result of scoring one Mix. FinalScore = Flavor × Mult.</summary>
    public sealed class ScoreBreakdown
    {
        public RecipeDefinition Recipe { get; }
        public int RecipeLevel { get; }
        public IReadOnlyList<IngredientCard> ScoredCards { get; }
        public IReadOnlyList<ScoreStep> Steps { get; }
        public double TotalFlavor { get; }
        public double TotalMult { get; }
        public double FinalScore { get; }

        public ScoreBreakdown(RecipeDefinition recipe, int recipeLevel,
            IReadOnlyList<IngredientCard> scoredCards, IReadOnlyList<ScoreStep> steps,
            double totalFlavor, double totalMult)
        {
            Recipe = recipe;
            RecipeLevel = recipeLevel;
            ScoredCards = scoredCards;
            Steps = steps;
            TotalFlavor = totalFlavor;
            TotalMult = totalMult;
            FinalScore = totalFlavor * totalMult;
        }

        public static ScoreBreakdown NoRecipe { get; } = new ScoreBreakdown(
            null, 1, new IngredientCard[0], new ScoreStep[0], 0, 0);
    }
}
