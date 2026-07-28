using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Deterministic PCG32 generator. Custom implementation instead of System.Random so
    /// sequences are stable across .NET runtimes/platforms — seeds must be shareable
    /// as strings between players (GDD 13, determinism & seeding).
    /// </summary>
    public sealed class SeededRng
    {
        private ulong _state;
        private readonly ulong _inc;

        public SeededRng(ulong seed, ulong sequence = 1)
        {
            _state = 0;
            _inc = (sequence << 1) | 1;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << (-rot & 31));
        }

        /// <summary>Uniform int in [0, maxExclusive). maxExclusive must be positive.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            // Rejection sampling to avoid modulo bias.
            uint bound = (uint)maxExclusive;
            uint threshold = (uint)(-bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        public int NextInt(int minInclusive, int maxExclusive) =>
            minInclusive + NextInt(maxExclusive - minInclusive);

        public double NextDouble() => NextUInt() / 4294967296.0;
    }

    /// <summary>
    /// One RNG per run, split into named independent streams (deck / shop / vip …)
    /// so drawing cards never perturbs future shop rolls.
    /// </summary>
    public sealed class RunRng
    {
        private readonly ulong _seedHash;
        private readonly Dictionary<string, SeededRng> _streams = new Dictionary<string, SeededRng>();

        public string Seed { get; }

        public RunRng(string seed)
        {
            Seed = seed ?? string.Empty;
            _seedHash = Fnv1a64(Seed);
        }

        public SeededRng GetStream(string name)
        {
            if (!_streams.TryGetValue(name, out var rng))
            {
                rng = new SeededRng(_seedHash, Fnv1a64(name));
                _streams[name] = rng;
            }
            return rng;
        }

        private static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
