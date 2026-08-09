using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// A kind of person who drinks here (GDD 19 §9, <c>customers/archetypes.json</c>;
    /// trimmed 2026-08-02 — the emotion bands went with the emotion layer). The archetype
    /// is the shape; each named regular is one roll inside that shape: a name, an age, a
    /// hometown, a disposition.
    /// </summary>
    public sealed class ArchetypeDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Relative draw weight when picking who walks in.</summary>
        public int Weight { get; }

        /// <summary>Name pool for regulars rolled from this archetype.</summary>
        public IReadOnlyList<string> NamePool { get; }

        /// <summary>Places this kind of person tends to be from (licence + future dialogue).</summary>
        public IReadOnlyList<string> Hometowns { get; }

        public ArchetypeDefinition(string id, string name,
            IReadOnlyList<string> namePool = null, int weight = 1,
            IReadOnlyList<string> hometowns = null)
        {
            Hometowns = hometowns != null && hometowns.Count > 0
                ? hometowns
                : new[] { "this side of town" };
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Archetype id is required", nameof(id));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));

            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            NamePool = namePool != null && namePool.Count > 0 ? namePool : new[] { Name };
            Weight = weight;
        }
    }
}
