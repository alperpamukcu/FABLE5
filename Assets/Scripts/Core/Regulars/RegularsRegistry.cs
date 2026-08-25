using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Everyone who has walked into this bar during this run (GDD 19 §10). Owns the truth
    /// behind every customer: their live stats, what they usually look like, and how well
    /// the bartender knows them. New faces are rolled from archetypes; once someone exists
    /// they persist, and the registry decides when a familiar face comes back.
    /// The caller owns the streams — <c>"customer"</c> for who walks in, <c>"drift"</c> for
    /// what happens to them between visits.
    /// </summary>
    public sealed class RegularsRegistry
    {
        private readonly IReadOnlyList<ArchetypeDefinition> _archetypes;
        private readonly Dictionary<string, RegularState> _byId = new Dictionary<string, RegularState>();
        private readonly List<RegularState> _order = new List<RegularState>();
        private readonly int _totalWeight;
        private int _nextSerial = 1;

        /// <summary>Chance a customer is someone you have already served, once anyone exists.</summary>
        public int ReturnChancePercent { get; }

        public IReadOnlyList<RegularState> All => _order;
        public int Count => _order.Count;

        public RegularsRegistry(IReadOnlyList<ArchetypeDefinition> archetypes, int returnChancePercent = 55)
        {
            if (archetypes == null || archetypes.Count == 0)
                throw new ArgumentException("At least one archetype is required.", nameof(archetypes));
            if (returnChancePercent < 0 || returnChancePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(returnChancePercent));

            _archetypes = archetypes;
            ReturnChancePercent = returnChancePercent;
            foreach (var archetype in archetypes) _totalWeight += archetype.Weight;
        }

        public RegularState Get(string id) => _byId.TryGetValue(id, out var state) ? state : null;

        /// <summary>
        /// Who is at the bar next. Early in a run everyone is a stranger; later, most nights
        /// are people you have met — which is what makes the persistent stats mean anything.
        ///
        /// <paramref name="allowReturns"/> is how the OPENING NIGHT says no (2026-08-25, the
        /// author: "her müşteri bara ilk defa geliyor olmalı çünkü bar yeni açıldı"). A bar
        /// that opened tonight cannot have a regular in it, and the return roll did not know
        /// that: from the second drinker on it had a 55% chance of sending somebody back in,
        /// so day one printed "2nd visit" and a row of stars they had left at a bar that did
        /// not exist yesterday. The gate lives HERE and not in the HUD because it is a rule
        /// about who the bar knows, and a caller routing around it is exactly the hole the
        /// house rule about trusting the UI was written for.
        ///
        /// The return roll is DRAWN whether or not it can be honoured, so the gate itself
        /// never costs the stream a draw; what it changes is what happens next, which is the
        /// whole point.
        /// </summary>
        public RegularState RollNext(SeededRng rng, bool allowReturns = true)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            bool comingBack = _order.Count > 0 && rng.NextInt(100) < ReturnChancePercent;
            if (comingBack && allowReturns)
                return _order[rng.NextInt(_order.Count)];

            return CreateNew(rng);
        }

        /// <summary>Rolls a brand-new person and registers them.</summary>
        public RegularState CreateNew(SeededRng rng)
        {
            var archetype = PickArchetype(rng);
            string id = $"{archetype.Id}_{_nextSerial++}";
            string name = archetype.NamePool[rng.NextInt(archetype.NamePool.Count)];

            // Licence details roll on the same stream, so a seed reproduces the whole person.
            int age = rng.NextInt(21, 68);
            string hometown = archetype.Hometowns[rng.NextInt(archetype.Hometowns.Count)];
            var state = new RegularState(id, name, archetype.Id, age, hometown);
            _byId[id] = state;
            _order.Add(state);
            return state;
        }


        private ArchetypeDefinition PickArchetype(SeededRng rng)
        {
            int roll = rng.NextInt(_totalWeight);
            foreach (var archetype in _archetypes)
            {
                roll -= archetype.Weight;
                if (roll < 0) return archetype;
            }
            return _archetypes[_archetypes.Count - 1];
        }
    }
}
