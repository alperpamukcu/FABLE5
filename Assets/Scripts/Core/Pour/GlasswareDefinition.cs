using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One kind of serving glass (v5 P10, the notes' glassware system). Pure description: the
    /// run does not consume these yet — P14 wires auto-selection and upgrades. The silhouette
    /// profile is the same half-width-per-height array the fluid solver takes, so the data
    /// that defines a glass is the data that fills it.
    /// </summary>
    public sealed class GlasswareDefinition
    {
        public string Id { get; }
        public string Name { get; }

        // (SpriteKey retired, audit 2026-08-11: never read, and its json values named
        // PNGs that do not exist — the art keys off the glass id, glass3d_*.)

        /// <summary>Half-width multipliers, floor → rim, for the fluid solver's SetProfile.</summary>
        public IReadOnlyList<double> Profile { get; }

        /// <summary>
        /// How much the glass holds, in the run's pour units (the old single glass was 1.0, so
        /// a highball is 1.0 and everything else is scaled against it). This is what makes the
        /// glass set matter rather than just look different: a coupe is a small drink and a
        /// pint is a large one, and `minFill` and the ratio bands are shares of *this*.
        /// </summary>
        public double Capacity { get; }

        /// <summary>Price of each upgrade step: [1★, 2★, 3★, 4★, 5★-legendary]. The 0★
        /// base set is owned from day one (the author's six-step ladder, 2026-08-02).</summary>
        public IReadOnlyList<int> TierPrices { get; }

        public GlasswareDefinition(string id, string name,
            IReadOnlyList<double> profile, IReadOnlyList<int> tierPrices, double capacity)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Glass needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Glass '{id}' needs a name.", nameof(name));
            if (profile == null || profile.Count < 2)
                throw new ArgumentException($"Glass '{id}' needs a silhouette profile (2+ samples).", nameof(profile));
            foreach (var w in profile)
                if (w <= 0 || w > 1)
                    throw new ArgumentException($"Glass '{id}' has profile value {w}; must be in (0, 1].", nameof(profile));
            if (tierPrices == null || tierPrices.Count != 5)
                throw new ArgumentException(
                    $"Glass '{id}' needs exactly 5 upgrade prices (1-star through the 5-star legendary set).",
                    nameof(tierPrices));
            foreach (var p in tierPrices)
                if (p <= 0) throw new ArgumentException($"Glass '{id}' has a non-positive upgrade price.", nameof(tierPrices));
            if (capacity <= 0 || capacity > 4)
                throw new ArgumentException(
                    $"Glass '{id}' holds {capacity}; must be in (0, 4] pour units.", nameof(capacity));

            Id = id;
            Name = name;
            Profile = profile;
            TierPrices = tierPrices;
            Capacity = capacity;
        }

        public override string ToString() => $"{Name} ({Id})";
    }
}
