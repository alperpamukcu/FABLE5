using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Rarity-weighted patron draws (GDD 7.1). Base weights Common 60 / Uncommon 30 /
    /// Rare 10; Legendary weight is 0 — legendaries appear "only via special means"
    /// (the Speakeasy Pack). The Neon Sign voucher multiplies the non-common weights.
    /// </summary>
    public static class PatronRoll
    {
        /// <summary>Weighted pick, or null when no candidate can roll (e.g. all Legendary).</summary>
        public static PatronDefinition Weighted(SeededRng rng,
            IReadOnlyList<PatronDefinition> candidates, int rareBoost = 1)
        {
            if (candidates == null || candidates.Count == 0) return null;

            int total = 0;
            var weights = new int[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = WeightOf(candidates[i].Rarity, rareBoost);
                total += weights[i];
            }
            if (total == 0) return null;

            int roll = rng.NextInt(total);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static int WeightOf(PatronRarity rarity, int rareBoost)
        {
            switch (rarity)
            {
                case PatronRarity.Common: return 60;
                case PatronRarity.Uncommon: return 30 * rareBoost;
                case PatronRarity.Rare: return 10 * rareBoost;
                default: return 0; // Legendary: Speakeasy Pack only
            }
        }
    }
}
