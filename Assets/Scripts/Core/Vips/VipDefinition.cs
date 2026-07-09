using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>The closed set of VIP rule mechanics (GDD 6). Content combines these.</summary>
    public enum VipRuleKind
    {
        DebuffType,            // cards of Type score nothing and trigger nothing
        DebuffRandomType,      // as above, type rolled at order start (The Critic)
        OnlyFirstMixScores,    // later mixes are voided (The Purist)
        MinRecipeLevel,        // recipes below IntValue score nothing (The Snob)
        EachMixDifferentRecipe,// repeating a recipe this order voids it (Regular's Ghost)
        RailSizeDelta,         // IntValue added to rail size (The Health Inspector, -3)
        TargetScale            // DoubleValue multiplies the satisfaction target (The Critic, 1.5)
    }

    public sealed class VipRule
    {
        public VipRuleKind Kind { get; }
        public IngredientType Type { get; }
        public int IntValue { get; }
        public double DoubleValue { get; }

        public VipRule(VipRuleKind kind, IngredientType type = default, int intValue = 0, double doubleValue = 0)
        {
            Kind = kind;
            Type = type;
            IntValue = intValue;
            DoubleValue = doubleValue;
        }
    }

    /// <summary>
    /// A VIP/Critic customer (boss). One is drawn per Night; its rules apply to that
    /// order only. Gentle VIPs form the Night 1–2 pool (GDD 11); a FinaleOnly VIP
    /// (The Critic) is reserved for the final Night.
    /// </summary>
    public sealed class VipDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool Gentle { get; }
        public bool FinaleOnly { get; }
        public IReadOnlyList<VipRule> Rules { get; }

        public VipDefinition(string id, string name, string description,
            bool gentle, bool finaleOnly, IReadOnlyList<VipRule> rules)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("VIP id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Description = description ?? string.Empty;
            Gentle = gentle;
            FinaleOnly = finaleOnly;
            Rules = rules ?? Array.Empty<VipRule>();
        }
    }
}
