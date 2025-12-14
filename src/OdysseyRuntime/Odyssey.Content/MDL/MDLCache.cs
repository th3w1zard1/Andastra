using System;
using System.Collections.Generic;
using System.Threading;

namespace Odyssey.Content.MDL
{
    /// <summary>
    /// Thread-safe model cache for loaded MDL models.
    /// Implements LRU-style eviction when cache size exceeds maximum.
    /// 
    /// Reference: KotOR.js MDLLoader.ts - ModelCache pattern
    /// </summary>
    public sealed class MDLCache
    {
        private static readonly Lazy<MDLCache> _instance = new Lazy<MDLCache>(() => new MDLCache());

        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly object _lock = new object();
        private int _maxEntries;

        /// <summary>
        /// Gets the global MDL cache instance.
        /// </summary>
        public static MDLCache Instance
        {
            get { return _instance.Value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of cached models.
        /// Default is 100 models.
        /// </summary>
        public int MaxEntries
        {
            get { return _maxEntries; }
            set { _maxEntries = Math.Max(1, value); }
        }

        /// <summary>
        /// Gets the current number of cached models.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        private MDLCache()
        {
            _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            _maxEntries = 100;
        }

        /// <summary>
        /// Tries to get a cached model.
        /// </summary>
        /// <param name="resRef">Resource reference (case-insensitive)</param>
        /// <param name="model">The cached model, or null if not found</param>
        /// <returns>True if the model was found in cache</returns>
        public bool TryGet(string resRef, out MDLModel model)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                model = null;
                return false;
            }

            lock (_lock)
            {
                CacheEntry entry;
                if (_cache.TryGetValue(resRef, out entry))
                {
                    entry.LastAccess = DateTime.UtcNow;
                    entry.AccessCount++;
                    model = entry.Model;
                    return true;
                }
            }

            model = null;
            return false;
        }

        /// <summary>
        /// Adds a model to the cache.
        /// </summary>
        /// <param name="resRef">Resource reference</param>
        /// <param name="model">The model to cache</param>
        public void Add(string resRef, MDLModel model)
        {
            if (string.IsNullOrEmpty(resRef) || model == null)
            {
                return;
            }

            lock (_lock)
            {
                // Remove existing entry if present
                _cache.Remove(resRef);

                // Evict if at capacity
                if (_cache.Count >= _maxEntries)
                {
                    EvictLeastRecentlyUsed();
                }

                _cache[resRef] = new CacheEntry
                {
                    Model = model,
                    LastAccess = DateTime.UtcNow,
                    AccessCount = 1
                };
            }
        }

        /// <summary>
        /// Removes a model from the cache.
        /// </summary>
        /// <param name="resRef">Resource reference</param>
        /// <returns>True if the model was removed</returns>
        public bool Remove(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return false;
            }

            lock (_lock)
            {
                return _cache.Remove(resRef);
            }
        }

        /// <summary>
        /// Clears all cached models.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Checks if a model is in the cache.
        /// </summary>
        /// <param name="resRef">Resource reference</param>
        /// <returns>True if cached</returns>
        public bool Contains(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return false;
            }

            lock (_lock)
            {
                return _cache.ContainsKey(resRef);
            }
        }

        private void EvictLeastRecentlyUsed()
        {
            // Find least recently used entry
            string lruKey = null;
            DateTime lruTime = DateTime.MaxValue;

            foreach (KeyValuePair<string, CacheEntry> kv in _cache)
            {
                if (kv.Value.LastAccess < lruTime)
                {
                    lruTime = kv.Value.LastAccess;
                    lruKey = kv.Key;
                }
            }

            if (lruKey != null)
            {
                _cache.Remove(lruKey);
            }
        }

        private sealed class CacheEntry
        {
            public MDLModel Model;
            public DateTime LastAccess;
            public int AccessCount;
        }
    }
}

