using System;

namespace LastCall.Core
{
    /// <summary>What a Tool does to its selected rail cards (GDD 7.3).</summary>
    public enum ToolOp
    {
        Enhance,      // apply an Enhancement (Muddler → Infused, Jigger → Overproof)
        Destroy,      // remove cards from the run permanently (Ice Pick)
        Copy,         // add a fresh identical instance next to the original (Bar Spoon)
        ConvertType   // rewrite the ingredient type (Citrus Press → Sour)
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
        public string Description { get; }

        public ToolDefinition(string id, string name, int cost, ToolOp op, int maxTargets,
            Enhancement enhancement = Enhancement.None, IngredientType convertTo = default,
            string description = null)
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
            Description = description ?? string.Empty;
        }
    }
}
