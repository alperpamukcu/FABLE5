using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One ingredient — a bottle or a keg on the shelf. Two may share a definition id.
    /// (InstanceId retired with the effects system it served — audit 2026-08-11.)
    ///
    /// Immutable since 2026-07-27: the mutators (Enhance / ConvertType / Refine / ShiftFlavor)
    /// existed for the Tools that reworked cards mid-run, and Tools went with the deck in the
    /// tycoon demolition (PLAN P7). Nothing has called them since.
    /// </summary>
    public sealed class IngredientCard
    {
        public string Id { get; }
        public string Name { get; }
        public IngredientType Type { get; }
        public int Flavor { get; }

        /// <summary>
        /// What this ingredient does to a person (GDD 19 §4). Always printed on the card —
        /// the charges are never the hidden information; the customer is.
        /// </summary>

        /// <summary>
        /// The bottle's identity papers (GDD 22): brand style, tier, origin, ABV, blurb.
        /// Null for bench-test cards; the shipped base bar always carries one.
        /// </summary>
        public IngredientInfo Info { get; }

        public IngredientCard(string id, string name, IngredientType type, int flavor,
            IngredientInfo info = null)
        {
            Info = info;
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Card id is required", nameof(id));
            if (flavor < 0) throw new ArgumentOutOfRangeException(nameof(flavor));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Type = type;
            Flavor = flavor;
        }

        /// <summary>A fresh instance with identical stats; gets its own InstanceId.</summary>
        public IngredientCard Clone() =>
            new IngredientCard(Id, Name, Type, Flavor, Info);

        public override string ToString() => $"{Name} [{Type} {Flavor}]";
    }
}
