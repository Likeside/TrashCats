using System;

namespace TrashCats.Domain
{
    /// <summary>
    /// Small deterministic PRNG (xorshift32 with SplitMix-style seeding). Produces the same sequence on every platform
    /// and runtime, unlike UnityEngine.Random or System.Random. Not cryptographically secure.
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = Scramble(unchecked((uint)seed));
            if (_state == 0)
                _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            var range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)((NextUInt() * range) >> 32));
        }

        /// <summary>True with probability chance/1000; chance ≤ 0 never hits, chance ≥ 1000 always hits.</summary>
        public bool RollPermille(int chancePermille) => NextInt(0, 1000) < chancePermille;

        /// <summary>Deterministic hash of several integers into one seed (FNV-1a over their bytes).</summary>
        public static int CombineSeeds(params int[] parts)
        {
            var hash = 2166136261u;
            foreach (var part in parts)
            {
                var value = unchecked((uint)part);
                for (var i = 0; i < 4; i++)
                {
                    hash ^= (value >> (i * 8)) & 0xFF;
                    hash = unchecked(hash * 16777619u);
                }
            }

            return unchecked((int)hash);
        }

        /// <summary>Stable string hash (FNV-1a over UTF-16 code units); string.GetHashCode is randomized per process.</summary>
        public static int StableHash(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c & 0xFFu;
                hash = unchecked(hash * 16777619u);
                hash ^= (uint)c >> 8;
                hash = unchecked(hash * 16777619u);
            }

            return unchecked((int)hash);
        }

        private static uint Scramble(uint z)
        {
            unchecked
            {
                z += 0x9E3779B9u;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }
    }
}
