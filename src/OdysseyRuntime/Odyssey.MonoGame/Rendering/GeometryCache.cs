using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
{
    /// <summary>
    /// Geometry cache for efficient mesh reuse.
    /// 
    /// Geometry caching stores frequently used meshes in GPU memory,
    /// reducing upload overhead and improving rendering performance.
    /// 
    /// Features:
    /// - LRU cache eviction
    /// - Memory budget management
    /// - Automatic cache invalidation
    /// - Per-mesh statistics
    /// </summary>
    public class GeometryCache : IDisposable
    {
        /// <summary>
        /// Cached geometry entry.
        /// </summary>
        private class CacheEntry
        {
            public string MeshName;
            public VertexBuffer VertexBuffer;
            public IndexBuffer IndexBuffer;
            public int VertexCount;
            public int IndexCount;
            public long MemorySize;
            public int LastUsedFrame;
            public int UseCount;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly object _lock;
        private long _memoryBudget;
        private long _currentMemoryUsage;
        private int _currentFrame;

        /// <summary>
        /// Gets or sets the memory budget in bytes.
        /// </summary>
        public long MemoryBudget
        {
            get { return _memoryBudget; }
            set { _memoryBudget = Math.Max(0, value); }
        }

        /// <summary>
        /// Gets the current memory usage in bytes.
        /// </summary>
        public long CurrentMemoryUsage
        {
            get { return _currentMemoryUsage; }
        }

        /// <summary>
        /// Gets the number of cached meshes.
        /// </summary>
        public int CacheSize
        {
            get { return _cache.Count; }
        }

        /// <summary>
        /// Initializes a new geometry cache.
        /// </summary>
        public GeometryCache(GraphicsDevice graphicsDevice, long memoryBudget = 256 * 1024 * 1024) // 256 MB default
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
            _cache = new Dictionary<string, CacheEntry>();
            _lock = new object();
            _memoryBudget = memoryBudget;
            _currentMemoryUsage = 0;
        }

        /// <summary>
        /// Gets cached geometry or loads it.
        /// </summary>
        public bool GetGeometry(string meshName, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer, out int vertexCount, out int indexCount)
        {
            vertexBuffer = null;
            indexBuffer = null;
            vertexCount = 0;
            indexCount = 0;

            lock (_lock)
            {
                CacheEntry entry;
                if (_cache.TryGetValue(meshName, out entry))
                {
                    // Update usage
                    entry.LastUsedFrame = _currentFrame;
                    entry.UseCount++;

                    vertexBuffer = entry.VertexBuffer;
                    indexBuffer = entry.IndexBuffer;
                    vertexCount = entry.VertexCount;
                    indexCount = entry.IndexCount;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds geometry to the cache.
        /// </summary>
        public bool AddGeometry(string meshName, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int vertexCount, int indexCount)
        {
            if (string.IsNullOrEmpty(meshName) || vertexBuffer == null || indexBuffer == null)
            {
                return false;
            }

            lock (_lock)
            {
                // Check if already cached
                if (_cache.ContainsKey(meshName))
                {
                    return true;
                }

                // Calculate memory size
                long memorySize = EstimateMemorySize(vertexCount, indexCount);

                // Evict if over budget
                while (_currentMemoryUsage + memorySize > _memoryBudget && _cache.Count > 0)
                {
                    EvictLRU();
                }

                // Add to cache
                CacheEntry entry = new CacheEntry
                {
                    MeshName = meshName,
                    VertexBuffer = vertexBuffer,
                    IndexBuffer = indexBuffer,
                    VertexCount = vertexCount,
                    IndexCount = indexCount,
                    MemorySize = memorySize,
                    LastUsedFrame = _currentFrame,
                    UseCount = 1
                };

                _cache[meshName] = entry;
                _currentMemoryUsage += memorySize;

                return true;
            }
        }

        /// <summary>
        /// Evicts least recently used entry.
        /// </summary>
        private void EvictLRU()
        {
            if (_cache.Count == 0)
            {
                return;
            }

            CacheEntry lruEntry = null;
            string lruKey = null;
            int oldestFrame = int.MaxValue;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.LastUsedFrame < oldestFrame)
                {
                    oldestFrame = kvp.Value.LastUsedFrame;
                    lruEntry = kvp.Value;
                    lruKey = kvp.Key;
                }
            }

            if (lruEntry != null && lruKey != null)
            {
                _currentMemoryUsage -= lruEntry.MemorySize;
                lruEntry.VertexBuffer?.Dispose();
                lruEntry.IndexBuffer?.Dispose();
                _cache.Remove(lruKey);
            }
        }

        /// <summary>
        /// Advances to the next frame.
        /// </summary>
        public void AdvanceFrame()
        {
            _currentFrame++;
        }

        /// <summary>
        /// Estimates memory size for geometry.
        /// </summary>
        private long EstimateMemorySize(int vertexCount, int indexCount)
        {
            // Rough estimate: assume 32 bytes per vertex and 4 bytes per index
            return (vertexCount * 32L) + (indexCount * 4L);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (CacheEntry entry in _cache.Values)
                {
                    entry.VertexBuffer?.Dispose();
                    entry.IndexBuffer?.Dispose();
                }
                _cache.Clear();
                _currentMemoryUsage = 0;
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

