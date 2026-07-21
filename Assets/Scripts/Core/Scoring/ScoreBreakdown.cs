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

        /// <summary>True when a VIP rule voided this mix (recipe recognized, score forced to 0).</summary>
        public bool IsVoided { get; private set; }

        /// <summary>The rule text explaining the void; empty otherwise.</summary>
        public string VoidReason { get; private set; } = string.Empty;

        /// <summary>
        /// How the serve read the customer (GDD 19 §6); null for mixes scored without the
        /// emotion layer (bench setups, previews with no customer).
        /// </summary>
        public ResonanceResult Resonance { get; }

        public ScoreBreakdown(RecipeDefinition recipe, int recipeLevel,
            IReadOnlyList<IngredientCard> scoredCards, IReadOnlyList<ScoreStep> steps,
            double totalFlavor, double totalMult, ResonanceResult resonance = null)
        {
            Resonance = resonance;
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

        /// <summary>
        /// A mix that matched nothing still reached the customer, so it still carries a
        /// verdict (GDD 19 §5, D5) — it just scores zero points doing it.
        /// </summary>
        public static ScoreBreakdown NoRecipeWith(ResonanceResult resonance) =>
            resonance == null ? NoRecipe
                : new ScoreBreakdown(null, 1, new IngredientCard[0], new ScoreStep[0], 0, 0, resonance);

        /// <summary>
        /// The house-pour fallback (GDD 21 §9, 2026-07-20): a drink that matches no recipe
        /// still pays a little — its volume-weighted Flavor at ×1 — so pouring *something*
        /// always beats pouring nothing, while a real recipe pays an order of magnitude more.
        /// </summary>
        public static ScoreBreakdown HousePour(double flavor,
            IReadOnlyList<IngredientCard> cards, ResonanceResult resonance) =>
            new ScoreBreakdown(null, 1, cards ?? new IngredientCard[0],
                new[] { new ScoreStep("House pour", EffectOp.AddFlavor, flavor, flavor, 1) },
                flavor, 1, resonance);

        /// <summary>A mix cancelled by a VIP rule: the recipe is shown, the score is zero.</summary>
        public static ScoreBreakdown Voided(RecipeDefinition recipe, int recipeLevel, string reason) =>
            new ScoreBreakdown(recipe, recipeLevel, new IngredientCard[0], new ScoreStep[0], 0, 0)
            {
                IsVoided = true,
                VoidReason = reason ?? string.Empty
            };
    }
}
