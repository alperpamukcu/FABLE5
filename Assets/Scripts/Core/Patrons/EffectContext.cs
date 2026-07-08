using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Everything a patron condition may inspect about the current moment: the played mix,
    /// the matched recipe and the round counters. Per-card data is passed separately so one
    /// context serves the whole hand.
    /// </summary>
    public sealed class EffectContext
    {
        public static readonly EffectContext Empty = new EffectContext(null, null, 0, 0);

        /// <summary>The full played selection (not just pattern cards); null outside a mix.</summary>
        public IReadOnlyList<IngredientCard> Mix { get; }

        /// <summary>The matched recipe; null outside a mix.</summary>
        public RecipeDefinition Recipe { get; }

        /// <summary>Mixes already spent this customer before the current one (0 = first mix).</summary>
        public int MixesUsedBefore { get; }

        /// <summary>Restocks spent so far this customer.</summary>
        public int RestocksUsed { get; }

        public EffectContext(IReadOnlyList<IngredientCard> mix, RecipeDefinition recipe,
            int mixesUsedBefore, int restocksUsed)
        {
            Mix = mix;
            Recipe = recipe;
            MixesUsedBefore = mixesUsedBefore;
            RestocksUsed = restocksUsed;
        }
    }
}
