using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>An inclusive baseline band for one emotion; the encounter rolls inside it.</summary>
    public readonly struct EmotionBand
    {
        public int Min { get; }
        public int Max { get; }

        public EmotionBand(int min, int max)
        {
            if (max < min) throw new ArgumentException($"Band max {max} is below min {min}.", nameof(max));
            Min = EmotionStats.Clamp(min);
            Max = EmotionStats.Clamp(max);
        }

        public int Roll(SeededRng rng) => Min == Max ? Min : rng.NextInt(Min, Max + 1);

        public override string ToString() => $"{Min}-{Max}";
    }

    /// <summary>
    /// A kind of person who drinks here (GDD 19 §9, <c>customers/archetypes.json</c>).
    /// The archetype is the shape; each named regular is one roll inside that shape, so two
    /// "Night Shift Nurse"s are recognisably the same type and still different people.
    /// </summary>
    public sealed class ArchetypeDefinition
    {
        private readonly EmotionBand[] _bands = new EmotionBand[Emotions.Count];

        public string Id { get; }
        public string Name { get; }

        /// <summary>Relative draw weight when picking who walks in.</summary>
        public int Weight { get; }

        /// <summary>Name pool for regulars rolled from this archetype.</summary>
        public IReadOnlyList<string> NamePool { get; }

        /// <summary>
        /// This kind of person's disposition before the night is taken into account
        /// (GDD 20 §2.1). Someone carrying something heavy is harder to please than someone
        /// out celebrating, and the night pushes everyone up from there.
        /// </summary>
        public DemandLevel BaseDemand { get; }

        /// <summary>Places this kind of person tends to be from (licence + future dialogue).</summary>
        public IReadOnlyList<string> Hometowns { get; }

        public ArchetypeDefinition(string id, string name, IReadOnlyList<EmotionBand> bands,
            IReadOnlyList<string> namePool = null, int weight = 1,
            DemandLevel baseDemand = DemandLevel.Easygoing,
            IReadOnlyList<string> hometowns = null)
        {
            Hometowns = hometowns != null && hometowns.Count > 0
                ? hometowns
                : new[] { "this side of town" };
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Archetype id is required", nameof(id));
            if (bands == null || bands.Count != Emotions.Count)
                throw new ArgumentException($"Expected {Emotions.Count} bands.", nameof(bands));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));

            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            for (int i = 0; i < _bands.Length; i++) _bands[i] = bands[i];
            NamePool = namePool != null && namePool.Count > 0 ? namePool : new[] { Name };
            Weight = weight;
            BaseDemand = baseDemand;
        }

        public EmotionBand this[Emotion emotion] => _bands[(int)emotion];

        /// <summary>Rolls one person's baseline out of this archetype's bands.</summary>
        public EmotionStats RollBaseline(SeededRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var values = new int[Emotions.Count];
            for (int i = 0; i < values.Length; i++) values[i] = _bands[i].Roll(rng);
            return new EmotionStats(values);
        }
    }
}
