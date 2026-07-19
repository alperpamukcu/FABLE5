using System;
using System.Collections.Generic;

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
        public int Flavor { get; private set; }
        public QualityTier Quality { get; private set; }
        public Enhancement Enhancement { get; private set; }
        public int InstanceId { get; }

        /// <summary>
        /// What this ingredient does to a person (GDD 19 §4). Always printed on the card —
        /// the charges are never the hidden information; the customer is. Immutable and tied
        /// to the ingredient's identity, so a Tool that rewrites Type leaves them alone.
        /// </summary>
        public IReadOnlyList<EmotionCharge> Charges { get; }

        /// <summary>
        /// The bottle's identity papers (GDD 22): brand style, tier, origin, ABV, blurb.
        /// Null for bench-test cards; the shipped base bar always carries one.
        /// </summary>
        public IngredientInfo Info { get; }

        public IngredientCard(string id, string name, IngredientType type, int flavor,
            QualityTier quality = QualityTier.HousePour,
            IReadOnlyList<EmotionCharge> charges = null,
            IngredientInfo info = null)
        {
            Info = info;
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Card id is required", nameof(id));
            if (flavor < 0) throw new ArgumentOutOfRangeException(nameof(flavor));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Type = type;
            Flavor = flavor;
            Quality = quality;
            Enhancement = Enhancement.None;
            Charges = charges ?? Array.Empty<EmotionCharge>();
            InstanceId = _nextInstanceId++;
        }

        /// <summary>Applies (or replaces) an enhancement — Tools like Muddler/Jigger.</summary>
        public void Enhance(Enhancement enhancement) => Enhancement = enhancement;

        /// <summary>Rewrites the ingredient type — Tools like Citrus Press.</summary>
        public void ConvertType(IngredientType type) => Type = type;

        /// <summary>Rewrites the quality tier — Tools like Cocktail Umbrella.</summary>
        public void Refine(QualityTier quality) => Quality = quality;

        /// <summary>Shifts the Flavor value, floored at 1 — Tools like Muddling Stick (GDD 02 v1.1).</summary>
        public void ShiftFlavor(int delta) => Flavor = Math.Max(1, Flavor + delta);

        /// <summary>A fresh instance with identical stats (Bar Spoon); gets its own InstanceId.</summary>
        public IngredientCard Clone()
        {
            var copy = new IngredientCard(Id, Name, Type, Flavor, Quality, Charges, Info);
            copy.Enhancement = Enhancement;
            return copy;
        }

        public override string ToString() => $"{Name} [{Type} {Flavor}]";
    }
}
