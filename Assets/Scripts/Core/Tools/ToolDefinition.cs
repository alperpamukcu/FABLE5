using System;

namespace LastCall.Core
{
    /// <summary>
    /// What a Tool does (GDD 7.3). The first five target rail cards; DoubleMoney and
    /// CreateLastTool are run-level ops that ignore card targets entirely.
    /// </summary>
    public enum ToolOp
    {
        Enhance,        // apply an Enhancement (Muddler → Infused, Jigger → Overproof)
        Destroy,        // remove cards from the run permanently (Ice Pick)
        Copy,           // add a fresh identical instance next to the original (Bar Spoon)
        ConvertType,    // rewrite the ingredient type (Citrus Press → Sour)
        SetQuality,     // rewrite the quality tier (Cocktail Umbrella → Signature)
        ShiftValue,     // shift Flavor values (Muddling Stick +1, GDD 02 v1.1 value axis)
        DoubleMoney,    // double the wallet, capped (Tab Ledger)
        CreateLastTool  // recreate the last Tool used this run (Bottle Opener)
    }

    /// <summary>
    /// A single-use consumable (Tarot equivalent). Targets 1–MaxTargets cards on the rail
    /// during a customer round; the run layer owns the inventory (max held: 2).
    /// </summary>
    public sealed class ToolDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public ToolOp Op { get; }
        public int MaxTargets { get; }
        public Enhancement Enhancement { get; }
        public IngredientType ConvertTo { get; }
        public QualityTier Quality { get; }
        public int ShiftAmount { get; }
        public string Description { get; }

        /// <summary>True for ops the run resolves without rail targets.</summary>
        public bool IsRunOp => Op == ToolOp.DoubleMoney || Op == ToolOp.CreateLastTool;

        public ToolDefinition(string id, string name, int cost, ToolOp op, int maxTargets,
            Enhancement enhancement = Enhancement.None, IngredientType convertTo = default,
            string description = null, QualityTier quality = QualityTier.HousePour,
            int shiftAmount = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tool id is required", nameof(id));
            if (maxTargets <= 0) throw new ArgumentOutOfRangeException(nameof(maxTargets));
            if (op == ToolOp.Enhance && enhancement == Enhancement.None)
                throw new ArgumentException($"Tool '{id}' enhances but has no enhancement.", nameof(enhancement));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Cost = cost;
            Op = op;
            MaxTargets = maxTargets;
            Enhancement = enhancement;
            ConvertTo = convertTo;
            Quality = quality;
            ShiftAmount = shiftAmount;
            Description = description ?? string.Empty;
        }
    }
}
