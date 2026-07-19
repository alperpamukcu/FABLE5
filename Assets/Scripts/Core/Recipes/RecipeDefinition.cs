using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One slot of a recipe pattern: needs <see cref="Count"/> cards whose type is any of
    /// <see cref="Types"/> (e.g. Martini's "Bitter or Garnish" slot).
    /// </summary>
    public sealed class PatternRequirement
    {
        public IReadOnlyList<IngredientType> Types { get; }
        public int Count { get; }

        public PatternRequirement(int count, params IngredientType[] types)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (types == null || types.Length == 0) throw new ArgumentException("At least one type required", nameof(types));
            Count = count;
            Types = types;
        }
    }

    /// <summary>
    /// A recipe (hand ranking) from GDD 02, section 4. Higher <see cref="Rank"/> wins when
    /// several recipes match the same mix.
    /// </summary>
    public sealed class RecipeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Rank { get; }
        public int BaseFlavor { get; }
        public int BaseMult { get; }
        public int FlavorPerLevel { get; }
        public int MultPerLevel { get; }

        public IReadOnlyList<PatternRequirement> Requirements { get; }

        /// <summary>0 = no constraint. Neat Pour ("1 Spirit alone") is 1, Perfect Serve is 5.</summary>
        public int ExactMixSize { get; }

        /// <summary>0 = no constraint. Tiki's "any 5th" makes it 5.</summary>
        public int MinMixSize { get; }

        /// <summary>Every card in the mix must have a distinct type (Perfect Serve).</summary>
        public bool AllDistinctTypes { get; }

        /// <summary>Every card in the mix must share one Flavor value (Double Perfect).</summary>
        public bool AllEqualFlavor { get; }

        /// <summary>All mix cards count as scored, not just requirement slots (Tiki, Perfect Serve).</summary>
        public bool ScoreAllMixCards { get; }

        // Value/mono-Type group patterns (GDD 02 v1.1). The pattern is exactly the
        // qualifying group; other selected cards are non-pattern and score nothing.

        /// <summary>0 = off. House Special: N cards sharing one Flavor value.</summary>
        public int EqualFlavorGroupSize { get; }

        /// <summary>0 = off. Layered Pour: N cards with strictly ascending (all-distinct) Flavor values.</summary>
        public int AscendingFlavorGroupSize { get; }

        /// <summary>0 = off. Straight Booze: at least N cards of one Type.</summary>
        public int SameTypeGroupMin { get; }

        /// <summary>
        /// How far this recipe carries an ingredient's emotional charges (GDD 19 §5). The
        /// craft layer's whole job after the pivot: the same bottles say more in a drink that
        /// was actually made well. Derived from <see cref="BaseMult"/> unless overridden, so
        /// there is only ever one number to balance.
        /// </summary>
        public double ChargeMultiplier { get; }

        /// <summary>Charge multiplier implied by a recipe's base Mult (GDD 19 §5).</summary>
        public static double ChargeMultiplierFor(int baseMult) =>
            Math.Min(3.0, 1.0 + 0.2 * (baseMult - 1));

        /// <summary>
        /// Proportions this recipe is made at (GDD 21 §9) — the pour system's replacement for
        /// <see cref="Requirements"/>. Empty means the recipe has not been converted yet and
        /// simply cannot be matched by pouring.
        /// </summary>
        public IReadOnlyList<RatioRequirement> RatioRequirements { get; }

        /// <summary>How full the glass must be for this to count as the drink (0 = no floor).</summary>
        public double MinFill { get; }

        public RecipeDefinition(
            string id, string name, int rank,
            int baseFlavor, int baseMult, int flavorPerLevel, int multPerLevel,
            IReadOnlyList<PatternRequirement> requirements,
            int exactMixSize = 0, int minMixSize = 0,
            bool allDistinctTypes = false, bool allEqualFlavor = false,
            bool scoreAllMixCards = false,
            int equalFlavorGroupSize = 0, int ascendingFlavorGroupSize = 0,
            int sameTypeGroupMin = 0,
            double chargeMultiplier = 0,
            IReadOnlyList<RatioRequirement> ratioRequirements = null,
            double minFill = 0)
        {
            RatioRequirements = ratioRequirements ?? Array.Empty<RatioRequirement>();
            MinFill = minFill;
            Id = id;
            Name = name;
            Rank = rank;
            BaseFlavor = baseFlavor;
            BaseMult = baseMult;
            FlavorPerLevel = flavorPerLevel;
            MultPerLevel = multPerLevel;
            Requirements = requirements ?? Array.Empty<PatternRequirement>();
            ExactMixSize = exactMixSize;
            MinMixSize = minMixSize;
            AllDistinctTypes = allDistinctTypes;
            AllEqualFlavor = allEqualFlavor;
            ScoreAllMixCards = scoreAllMixCards;
            EqualFlavorGroupSize = equalFlavorGroupSize;
            AscendingFlavorGroupSize = ascendingFlavorGroupSize;
            SameTypeGroupMin = sameTypeGroupMin;
            ChargeMultiplier = chargeMultiplier > 0
                ? Math.Min(3.0, chargeMultiplier)
                : ChargeMultiplierFor(baseMult);
        }

        /// <summary>Level 1 = base values; Recipe Books raise the level (GDD 02 table, last column).</summary>
        public int FlavorAtLevel(int level) => BaseFlavor + (Math.Max(1, level) - 1) * FlavorPerLevel;

        public int MultAtLevel(int level) => BaseMult + (Math.Max(1, level) - 1) * MultPerLevel;
    }
}
