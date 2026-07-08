using System;

namespace LastCall.Core
{
    /// <summary>
    /// One ingredient card instance. Two cards may share a definition id (duplicates in the
    /// starter deck) but every instance gets its own InstanceId so effects can target it.
    /// Type and Enhancement are mutable because Tools rework cards mid-run (GDD 7.3) —
    /// all mutation goes through the explicit methods below.
    /// </summary>
    public sealed class IngredientCard
    {
        private static int _nextInstanceId = 1;

        public string Id { get; }
        public string Name { get; }
        public IngredientType Type { get; private set; }
        public int Flavor { get; }
        public QualityTier Quality { get; }
        public Enhancement Enhancement { get; private set; }
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
            Enhancement = Enhancement.None;
            InstanceId = _nextInstanceId++;
        }

        /// <summary>Applies (or replaces) an enhancement — Tools like Muddler/Jigger.</summary>
        public void Enhance(Enhancement enhancement) => Enhancement = enhancement;

        /// <summary>Rewrites the ingredient type — Tools like Citrus Press.</summary>
        public void ConvertType(IngredientType type) => Type = type;

        /// <summary>A fresh instance with identical stats (Bar Spoon); gets its own InstanceId.</summary>
        public IngredientCard Clone()
        {
            var copy = new IngredientCard(Id, Name, Type, Flavor, Quality);
            copy.Enhancement = Enhancement;
            return copy;
        }

        public override string ToString() => $"{Name} [{Type} {Flavor}]";
    }
}
