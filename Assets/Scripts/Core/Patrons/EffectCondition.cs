using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    public enum ConditionKind
    {
        Always,
        CardTypeIs,          // per-card: the scored card has this type
        CardIndexEquals,     // per-card: position within the scored cards (0 = first)
        MixContainsType,     // the played mix contains at least one card of this type
        MixSizeEquals,       // the played mix has exactly this many cards
        RecipeIdIn,          // the matched recipe id is one of these
        RestocksUsedEquals,  // restocks spent so far this customer
        MixesUsedEquals,     // mixes spent before this one (0 = first mix of the customer)
        ReturningCustomer,   // this face has been served before (Regular's Memory, GDD 19 §8)
        NoSpillsThisCustomer // nothing has been spilled this visit (GDD 21 §3)
    }

    /// <summary>
    /// A single data-driven predicate for patron effects. Deliberately a closed set:
    /// content adds combinations of these, not new code paths (GDD 13).
    /// </summary>
    public sealed class EffectCondition
    {
        public static readonly EffectCondition Always = new EffectCondition(ConditionKind.Always);

        public ConditionKind Kind { get; }
        public IngredientType Type { get; }
        public int IntValue { get; }
        public IReadOnlyList<string> RecipeIds { get; }

        public EffectCondition(ConditionKind kind, IngredientType type = default,
            int intValue = 0, IReadOnlyList<string> recipeIds = null)
        {
            Kind = kind;
            Type = type;
            IntValue = intValue;
            RecipeIds = recipeIds ?? Array.Empty<string>();
        }

        public static EffectCondition CardTypeIs(IngredientType type) =>
            new EffectCondition(ConditionKind.CardTypeIs, type);

        public static EffectCondition CardIndexEquals(int index) =>
            new EffectCondition(ConditionKind.CardIndexEquals, intValue: index);

        public static EffectCondition MixContainsType(IngredientType type) =>
            new EffectCondition(ConditionKind.MixContainsType, type);

        public static EffectCondition MixSizeEquals(int size) =>
            new EffectCondition(ConditionKind.MixSizeEquals, intValue: size);

        public static EffectCondition RecipeIdIn(params string[] ids) =>
            new EffectCondition(ConditionKind.RecipeIdIn, recipeIds: ids);

        public static EffectCondition RestocksUsedEquals(int count) =>
            new EffectCondition(ConditionKind.RestocksUsedEquals, intValue: count);

        public static EffectCondition MixesUsedEquals(int count) =>
            new EffectCondition(ConditionKind.MixesUsedEquals, intValue: count);

        public static EffectCondition ReturningCustomer { get; } =
            new EffectCondition(ConditionKind.ReturningCustomer);

        public static EffectCondition NoSpillsThisCustomer { get; } =
            new EffectCondition(ConditionKind.NoSpillsThisCustomer);

        /// <summary>
        /// Evaluates against the context; <paramref name="card"/>/<paramref name="cardIndex"/>
        /// are only set while scoring an individual card.
        /// </summary>
        public bool Evaluate(EffectContext ctx, IngredientCard card = null, int cardIndex = -1)
        {
            switch (Kind)
            {
                case ConditionKind.Always:
                    return true;
                case ConditionKind.CardTypeIs:
                    return card != null && card.Type == Type;
                case ConditionKind.CardIndexEquals:
                    return cardIndex >= 0 && cardIndex == IntValue;
                case ConditionKind.MixContainsType:
                    return ctx.Mix != null && ctx.Mix.Any(c => c.Type == Type);
                case ConditionKind.MixSizeEquals:
                    return ctx.Mix != null && ctx.Mix.Count == IntValue;
                case ConditionKind.RecipeIdIn:
                    return ctx.Recipe != null && RecipeIds.Contains(ctx.Recipe.Id);
                case ConditionKind.RestocksUsedEquals:
                    return ctx.RestocksUsed == IntValue;
                case ConditionKind.MixesUsedEquals:
                    return ctx.MixesUsedBefore == IntValue;
                case ConditionKind.ReturningCustomer:
                    return ctx.ReturningCustomer;
                case ConditionKind.NoSpillsThisCustomer:
                    return ctx.NoSpills;
                default:
                    return false;
            }
        }
    }
}
