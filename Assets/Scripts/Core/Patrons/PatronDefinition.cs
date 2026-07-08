using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    public enum PatronRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    public enum EffectValueSource
    {
        /// <summary>Use <see cref="PatronEffect.Value"/> as-is.</summary>
        Constant,
        /// <summary>Use the patron instance's accumulated counter (scaling patrons).</summary>
        Accumulated
    }

    /// <summary>One triggered operation on a patron.</summary>
    public sealed class PatronEffect
    {
        public EffectTrigger Trigger { get; }
        public EffectOp Op { get; }
        public double Value { get; }
        public EffectValueSource ValueSource { get; }
        public EffectCondition Condition { get; }

        public PatronEffect(EffectTrigger trigger, EffectOp op, double value,
            EffectCondition condition = null, EffectValueSource valueSource = EffectValueSource.Constant)
        {
            Trigger = trigger;
            Op = op;
            Value = value;
            ValueSource = valueSource;
            Condition = condition ?? EffectCondition.Always;
        }
    }

    /// <summary>Static description of a patron (GDD 08). Instances carry run state.</summary>
    public sealed class PatronDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public PatronRarity Rarity { get; }
        public int Cost { get; }
        public string Description { get; }
        public IReadOnlyList<PatronEffect> Effects { get; }

        public PatronDefinition(string id, string name, PatronRarity rarity, int cost,
            string description, IReadOnlyList<PatronEffect> effects)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Patron id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Rarity = rarity;
            Cost = cost;
            Description = description ?? string.Empty;
            Effects = effects ?? Array.Empty<PatronEffect>();
        }
    }

    /// <summary>
    /// A patron sitting in a slot. Slot order is scoring order (GDD 04 step 3), so the
    /// roster list's order is gameplay-relevant. <see cref="Accumulated"/> backs scaling
    /// patrons (e.g. The Collector) and persists for the run.
    /// </summary>
    public sealed class PatronInstance
    {
        public PatronDefinition Definition { get; }
        public double Accumulated { get; private set; }

        public PatronInstance(PatronDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public void Accumulate(double amount) => Accumulated += amount;

        public double ResolveValue(PatronEffect effect) =>
            effect.ValueSource == EffectValueSource.Accumulated ? Accumulated : effect.Value;
    }
}
