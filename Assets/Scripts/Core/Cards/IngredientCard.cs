using System;

namespace LastCall.Core
{
    /// <summary>
    /// One ingredient card instance. Two cards may share a definition id (duplicates in the
    /// starter deck) but every instance gets its own InstanceId so effects can target it.
    /// </summary>
    public sealed class IngredientCard
    {
        private static int _nextInstanceId = 1;

        public string Id { get; }
        public string Name { get; }
        public IngredientType Type { get; }
        public int Flavor { get; }
        public QualityTier Quality { get; }
        public int InstanceId { get; }

        public IngredientCard(string id, string name, IngredientType type, int flavor,
            QualityTier quality = QualityTier.HousePour)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Card id is required", nameof(id));
            if (flavor < 0) throw new ArgumentOutOfRangeException(nameof(flavor));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Type = type;
            Flavor = flavor;
            Quality = quality;
            InstanceId = _nextInstanceId++;
        }

        public override string ToString() => $"{Name} [{Type} {Flavor}]";
    }
}
