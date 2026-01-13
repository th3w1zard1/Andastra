using System;
using System.Text;

namespace BioWare.NET.Utility
{
    /// <summary>
    /// XXHash64 implementation for fast non-cryptographic hashing.
    /// 
    /// XXHash64 is a fast hash algorithm used by RTX Remix for material identification.
    /// This implementation follows the standard XXHash64 algorithm specification.
    /// </summary>
    public static class XXHash64
    {
        // Prime multipliers used in XXHash64
        private const ulong Prime1 = 11400714785074694791UL;
        private const ulong Prime2 = 14029467366897019727UL;
        private const ulong Prime3 = 1609587929392839161UL;
        private const ulong Prime4 = 9650029242287828579UL;
        private const ulong Prime5 = 2870177450012600261UL;

        /// <summary>
        /// Computes XXHash64 hash of a string (UTF-8 encoded).
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <param name="seed">The seed value (default 0).</param>
        /// <returns>The 64-bit hash value.</returns>
        public static ulong ComputeHash(string input, ulong seed = 0)
        {
            if (input == null)
            {
                return ComputeHash(Array.Empty<byte>(), seed);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return ComputeHash(bytes, seed);
        }

        /// <summary>
        /// Computes XXHash64 hash of a byte array.
        /// </summary>
        /// <param name="input">The input bytes to hash.</param>
        /// <param name="seed">The seed value (default 0).</param>
        /// <returns>The 64-bit hash value.</returns>
        public static ulong ComputeHash(byte[] input, ulong seed = 0)
        {
            if (input == null)
            {
                input = Array.Empty<byte>();
            }

            int length = input.Length;
            int offset = 0;

            ulong hash;

            if (length >= 32)
            {
                // Process in 32-byte chunks
                ulong v1 = seed + Prime1 + Prime2;
                ulong v2 = seed + Prime2;
                ulong v3 = seed;
                ulong v4 = seed - Prime1;

                int end = offset + length;
                int limit = end - 32;

                // Process 32-byte chunks
                while (offset <= limit)
                {
                    v1 = Round(v1, ReadUInt64(input, offset));
                    offset += 8;
                    v2 = Round(v2, ReadUInt64(input, offset));
                    offset += 8;
                    v3 = Round(v3, ReadUInt64(input, offset));
                    offset += 8;
                    v4 = Round(v4, ReadUInt64(input, offset));
                    offset += 8;
                }

                hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
                hash = MergeRound(hash, v1);
                hash = MergeRound(hash, v2);
                hash = MergeRound(hash, v3);
                hash = MergeRound(hash, v4);
            }
            else
            {
                hash = seed + Prime5;
            }

            hash += (ulong)length;

            // Process remaining bytes (less than 32)
            int remaining = length - offset;
            while (remaining >= 8)
            {
                ulong k1 = ReadUInt64(input, offset);
                k1 *= Prime2;
                k1 = RotateLeft(k1, 31);
                k1 *= Prime1;
                hash ^= k1;
                hash = RotateLeft(hash, 27) * Prime1 + Prime4;
                offset += 8;
                remaining -= 8;
            }

            if (remaining >= 4)
            {
                uint k1 = ReadUInt32(input, offset);
                k1 = unchecked(k1 * (uint)Prime1);
                hash ^= k1;
                hash = RotateLeft(hash, 23) * Prime2 + Prime3;
                offset += 4;
                remaining -= 4;
            }

            while (remaining > 0)
            {
                hash ^= input[offset] * Prime5;
                hash = RotateLeft(hash, 11) * Prime1;
                offset++;
                remaining--;
            }

            // Finalize with avalanche
            hash ^= hash >> 33;
            hash *= Prime2;
            hash ^= hash >> 29;
            hash *= Prime3;
            hash ^= hash >> 32;

            return hash;
        }

        /// <summary>
        /// Computes XXHash64 hash and returns it as a hexadecimal string.
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <param name="seed">The seed value (default 0).</param>
        /// <returns>The hash as a 16-character hexadecimal string.</returns>
        public static string ComputeHashString(string input, ulong seed = 0)
        {
            ulong hash = ComputeHash(input, seed);
            return hash.ToString("X16");
        }

        private static ulong Round(ulong acc, ulong input)
        {
            acc += input * Prime2;
            acc = RotateLeft(acc, 31);
            acc *= Prime1;
            return acc;
        }

        private static ulong MergeRound(ulong acc, ulong val)
        {
            val = Round(0, val);
            acc ^= val;
            acc = acc * Prime1 + Prime4;
            return acc;
        }

        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            return (ulong)buffer[offset]
                | ((ulong)buffer[offset + 1] << 8)
                | ((ulong)buffer[offset + 2] << 16)
                | ((ulong)buffer[offset + 3] << 24)
                | ((ulong)buffer[offset + 4] << 32)
                | ((ulong)buffer[offset + 5] << 40)
                | ((ulong)buffer[offset + 6] << 48)
                | ((ulong)buffer[offset + 7] << 56);
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)buffer[offset]
                | ((uint)buffer[offset + 1] << 8)
                | ((uint)buffer[offset + 2] << 16)
                | ((uint)buffer[offset + 3] << 24);
        }
    }
}

