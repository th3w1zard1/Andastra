using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;

namespace Andastra.Runtime.Content.Interfaces
{
    /// <summary>
    /// Cache for converted content assets.
    /// </summary>
    /// <remarks>
    /// Content Cache Interface:
    /// - Based on swkotor2.exe asset caching system
    /// - Located via string references: "CACHE" @ 0x007c6848, "z:\cache" @ 0x007c6850, CExoKeyTable resource management
    /// - Original implementation: Caches converted assets (MDL→runtime model, TPC→runtime texture) to disk
    /// - Resource management: CExoKeyTable handles resource key management, tracks loaded resources in memory
    /// - Cache key: Includes game type, ResRef, resource type, source hash, converter version
    /// - Cache invalidation: Source hash changes when source file modified, converter version changes when converter updated
    /// - Cache pruning: LRU eviction when cache exceeds size limits (prevents disk space issues)
    /// - Cache directory: Stores converted assets in cache directory (typically .cache/ or user cache directory)
    /// - Performance: Avoids redundant conversions, speeds up module loading after first load
    /// - Cache safety: Converter version ensures cache invalidation when converter logic changes
    /// - Source hash: MD5/SHA1 hash of source file ensures cache invalidation when source file changes
    /// - Note: Original engine uses in-memory caching via CExoKeyTable, this adds persistent disk cache for converted assets
    /// </remarks>
    public interface IContentCache
    {
        /// <summary>
        /// Tries to get a cached item.
        /// </summary>
        Task<CacheResult<T>> TryGetAsync<T>(CacheKey key, CancellationToken ct) where T : class;

        /// <summary>
        /// Stores an item in the cache.
        /// </summary>
        Task StoreAsync<T>(CacheKey key, T item, CancellationToken ct) where T : class;

        /// <summary>
        /// Invalidates a cache entry.
        /// </summary>
        void Invalidate(CacheKey key);

        /// <summary>
        /// Clears the entire cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the cache directory path.
        /// </summary>
        string CacheDirectory { get; }

        /// <summary>
        /// Gets the total size of the cache in bytes.
        /// </summary>
        long TotalSize { get; }

        /// <summary>
        /// Prunes old cache entries to stay within size limits.
        /// </summary>
        void Prune(long maxSizeBytes);
    }

    /// <summary>
    /// Key for cache entries.
    /// </summary>
    public struct CacheKey : IEquatable<CacheKey>
    {
        public readonly GameType GameType;
        public readonly string ResRef;
        public readonly ResourceType ResourceType;
        public readonly string SourceHash;
        public readonly int ConverterVersion;

        public CacheKey(GameType gameType, string resRef, ResourceType resourceType, string sourceHash, int converterVersion)
        {
            GameType = gameType;
            ResRef = resRef?.ToLowerInvariant() ?? string.Empty;
            ResourceType = resourceType;
            SourceHash = sourceHash ?? string.Empty;
            ConverterVersion = converterVersion;
        }

        public bool Equals(CacheKey other)
        {
            return GameType == other.GameType &&
                   string.Equals(ResRef, other.ResRef, StringComparison.OrdinalIgnoreCase) &&
                   ResourceType == other.ResourceType &&
                   string.Equals(SourceHash, other.SourceHash, StringComparison.Ordinal) &&
                   ConverterVersion == other.ConverterVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)GameType;
                hash = (hash * 397) ^ (ResRef != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(ResRef) : 0);
                hash = (hash * 397) ^ (int)ResourceType;
                hash = (hash * 397) ^ (SourceHash != null ? SourceHash.GetHashCode() : 0);
                hash = (hash * 397) ^ ConverterVersion;
                return hash;
            }
        }

        public string ToFileName()
        {
            return string.Format("{0}_{1}_{2}_{3}_v{4}",
                GameType, ResRef, (int)ResourceType, SourceHash.Substring(0, Math.Min(8, SourceHash.Length)), ConverterVersion);
        }
    }

    /// <summary>
    /// Result of a cache lookup.
    /// </summary>
    public struct CacheResult<T> where T : class
    {
        public readonly bool Found;
        public readonly T Value;

        public CacheResult(bool found, T value)
        {
            Found = found;
            Value = value;
        }

        public static CacheResult<T> Miss()
        {
            return new CacheResult<T>(false, null);
        }

        public static CacheResult<T> Hit(T value)
        {
            return new CacheResult<T>(true, value);
        }
    }
}

