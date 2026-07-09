using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// VIP rules resolved for one specific order (random debuff type already rolled,
    /// target scale and rail delta already applied by the run layer). The round consults
    /// this every Mix; <see cref="Empty"/> means a regular customer.
    /// </summary>
    public sealed class VipRuleSet
    {
        public static readonly VipRuleSet Empty = new VipRuleSet(null, false, 0, false);

        public IReadOnlyCollection<IngredientType> DebuffedTypes { get; }
        public bool OnlyFirstMixScores { get; }
        public int MinRecipeLevel { get; }
        public bool EachMixDifferentRecipe { get; }

        public VipRuleSet(IReadOnlyCollection<IngredientType> debuffedTypes,
            bool onlyFirstMixScores, int minRecipeLevel, bool eachMixDifferentRecipe)
        {
            DebuffedTypes = debuffedTypes ?? new IngredientType[0];
            OnlyFirstMixScores = onlyFirstMixScores;
            MinRecipeLevel = minRecipeLevel;
            EachMixDifferentRecipe = eachMixDifferentRecipe;
        }

        public bool HasAnyRule =>
            DebuffedTypes.Count > 0 || OnlyFirstMixScores || MinRecipeLevel > 0 || EachMixDifferentRecipe;
    }
}
