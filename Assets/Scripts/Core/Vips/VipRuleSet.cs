using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// VIP rules resolved for one specific order (random debuff type already rolled,
    /// target scale and rail delta already applied by the run layer). The round consults
    /// this every Mix; <see cref="Empty"/> means a regular customer.
    /// </summary>
    /// <summary>How a VIP rewrites what the ID shows (GDD 19 §8).</summary>
    public enum ReadOverride
    {
        None,

        /// <summary>Nothing legible. You are reading the room, not the licence.</summary>
        AllUnknown,

        /// <summary>Every stat printed. The tension moves entirely to landing it.</summary>
        AllExact
    }

    public sealed class VipRuleSet
    {
        public static readonly VipRuleSet Empty = new VipRuleSet(null, false, 0, false);

        public IReadOnlyCollection<IngredientType> DebuffedTypes { get; }
        public bool OnlyFirstMixScores { get; }
        public int MinRecipeLevel { get; }
        public bool EachMixDifferentRecipe { get; }

        /// <summary>Blanket rewrite of every reading's tier; <see cref="ReadOverride.None"/> normally.</summary>
        public ReadOverride ReadOverride { get; }

        /// <summary>One legible reading is false, at its own tier so it looks trustworthy.</summary>
        public bool OneReadingFalse { get; }

        public VipRuleSet(IReadOnlyCollection<IngredientType> debuffedTypes,
            bool onlyFirstMixScores, int minRecipeLevel, bool eachMixDifferentRecipe,
            ReadOverride readOverride = ReadOverride.None, bool oneReadingFalse = false)
        {
            DebuffedTypes = debuffedTypes ?? new IngredientType[0];
            OnlyFirstMixScores = onlyFirstMixScores;
            MinRecipeLevel = minRecipeLevel;
            EachMixDifferentRecipe = eachMixDifferentRecipe;
            ReadOverride = readOverride;
            OneReadingFalse = oneReadingFalse;
        }

        public bool HasAnyRule =>
            DebuffedTypes.Count > 0 || OnlyFirstMixScores || MinRecipeLevel > 0 ||
            EachMixDifferentRecipe || ReadOverride != ReadOverride.None || OneReadingFalse;
    }
}
