using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
{
    /// <summary>
    /// Render target cache for efficient RT reuse across frames.
    /// 
    /// Caches render targets by size and format, reusing them across
    /// frames to avoid allocation overhead.
    /// 
    /// Features:
    /// - Automatic RT caching
    /// - Format-based organization
    /// - LRU eviction
    /// - Memory budget management
    /// </summary>
    public class RenderTargetCache : IDisposable
    {
        /// <summary>
        /// Cached render target entry.
        /// </summary>
        private class CacheEntry
        {
            public RenderTarget2D RenderTarget;
            public int LastUsedFrame;
            public bool InUse;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly int _maxCacheSize;
        private int _currentFrame;

        /// <summary>
        /// Gets the number of cached render targets.
        /// </summary>
        public int CacheSize
        {
            get { return _cache.Count; }
        }

        /// <summary>
        /// Initializes a new render target cache.
        /// </summary>
        public RenderTargetCache(GraphicsDevice graphicsDevice, int maxCacheSize = 32)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
            _cache = new Dictionary<string, CacheEntry>();
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Gets a render target from cache or creates a new one.
        /// </summary>
        public RenderTarget2D Get(int width, int height, SurfaceFormat format, DepthFormat depthFormat, int multiSampleCount = 0)
        {
            string key = CreateKey(width, height, format, depthFormat, multiSampleCount);

            CacheEntry entry;
            if (_cache.TryGetValue(key, out entry) && !entry.InUse)
            {
                entry.InUse = true;
                entry.LastUsedFrame = _currentFrame;
                return entry.RenderTarget;
            }

            // Create new render target
            RenderTarget2D rt = new RenderTarget2D(
                _graphicsDevice,
                width,
                height,
                false,
                format,
                depthFormat,
                multiSampleCount,
                RenderTargetUsage.DiscardContents
            );

            // Add to cache
            if (_cache.Count < _maxCacheSize)
            {
                _cache[key] = new CacheEntry
                {
                    RenderTarget = rt,
                    LastUsedFrame = _currentFrame,
                    InUse = true
                };
            }

            return rt;
        }

        /// <summary>
        /// Returns a render target to the cache.
        /// </summary>
        public void Return(RenderTarget2D rt)
        {
            if (rt == null)
            {
                return;
            }

            // Find entry
            foreach (var kvp in _cache)
            {
                if (kvp.Value.RenderTarget == rt)
                {
                    kvp.Value.InUse = false;
                    kvp.Value.LastUsedFrame = _currentFrame;
                    break;
                }
            }
        }

        /// <summary>
        /// Updates frame counter and evicts old entries.
        /// </summary>
        public void UpdateFrame()
        {
            _currentFrame++;

            // Evict least recently used entries if cache is full
            if (_cache.Count >= _maxCacheSize)
            {
                EvictLRU();
            }
        }

        private void EvictLRU()
        {
            // Find least recently used, unused entry
            string lruKey = null;
            int lruFrame = int.MaxValue;

            foreach (var kvp in _cache)
            {
                if (!kvp.Value.InUse && kvp.Value.LastUsedFrame < lruFrame)
                {
                    lruFrame = kvp.Value.LastUsedFrame;
                    lruKey = kvp.Key;
                }
            }

            if (lruKey != null)
            {
                CacheEntry entry = _cache[lruKey];
                entry.RenderTarget.Dispose();
                _cache.Remove(lruKey);
            }
        }

        private string CreateKey(int width, int height, SurfaceFormat format, DepthFormat depthFormat, int multiSampleCount)
        {
            return $"{width}x{height}_{format}_{depthFormat}_{multiSampleCount}";
        }

        public void Dispose()
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                entry.RenderTarget?.Dispose();
            }
            _cache.Clear();
        }
    }
}

