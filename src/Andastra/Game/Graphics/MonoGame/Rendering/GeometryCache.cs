using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
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
    /// <remarks>
    /// Geometry Cache System (Modern Enhancement):
    /// - Based on swkotor2.exe rendering system architecture
    /// - Located via string references: "Geometry" @ 0x007bd044, "m_bIsBackgroundGeometry" @ 0x007baebc
    /// - "vertexindices" @ 0x007baee0, "vertexindicescount" @ 0x007baf00 (vertex index data)
    /// - "Disable Vertex Buffer Objects" @ 0x007b56bc (VBO disable option)
    /// - OpenGL vertex buffer extensions: "GL_ARB_vertex_buffer_object" @ 0x007b882c
    /// - "GL_ARB_vertex_program" @ 0x007b8860, "GL_NV_vertex_program" @ 0x007b8998
    /// - "GL_EXT_compiled_vertex_array" @ 0x007b88a8, "GL_NV_vertex_array_range" @ 0x007b8940
    /// - "GL_NV_vertex_array_range2" @ 0x007b8924
    /// - OpenGL vertex functions: glVertexPointer, glVertex3f, glVertex3fv, glVertex4f
    /// - OpenGL vertex attribute functions: glVertexAttribPointerARB, glEnableVertexAttribArrayARB, glDisableVertexAttribArrayARB
    /// - glVertexAttrib1-4fARB, glVertexAttrib1-4dARB, glVertexAttrib1-4sARB (various attribute setters)
    /// - glGetVertexAttribfvARB, glGetVertexAttribivARB, glGetVertexAttribdvARB, glGetVertexAttribPointervARB
    /// - NV vertex array: glVertexArrayRangeNV, glVertexAttribPointerNV, glVertexAttrib1-4fvNV
    /// - Error message: "Problem loading encounter with tag '%s'.  It has geometry, but no vertices.  Skipping." @ 0x007c0ae0
    /// - Original implementation: KOTOR loads MDL models to DirectX vertex/index buffers, kept in memory during gameplay
    /// - Original memory management: Models loaded per-area, unloaded on area transition
    /// - This is a modernization feature: LRU cache with memory budget management improves memory efficiency
    /// - Original behavior: All area models kept in memory, no automatic eviction
    /// - Modern enhancement: LRU eviction allows more efficient memory usage across areas
    /// </remarks>
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
                throw new ArgumentNullException(nameof(graphicsDevice));
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
        /// <param name="meshName">Mesh name/identifier. Must not be null or empty.</param>
        /// <param name="vertexBuffer">Output vertex buffer, or null if not cached.</param>
        /// <param name="indexBuffer">Output index buffer, or null if not cached.</param>
        /// <param name="vertexCount">Output vertex count.</param>
        /// <param name="indexCount">Output index count.</param>
        /// <returns>True if geometry was found in cache, false otherwise.</returns>
        public bool GetGeometry(string meshName, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer, out int vertexCount, out int indexCount)
        {
            vertexBuffer = null;
            indexBuffer = null;
            vertexCount = 0;
            indexCount = 0;

            if (string.IsNullOrEmpty(meshName))
            {
                return false;
            }

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
        /// <param name="meshName">Mesh name/identifier. Must not be null or empty.</param>
        /// <param name="vertexBuffer">Vertex buffer. Must not be null.</param>
        /// <param name="indexBuffer">Index buffer. Must not be null.</param>
        /// <param name="vertexCount">Number of vertices. Must be non-negative.</param>
        /// <param name="indexCount">Number of indices. Must be non-negative.</param>
        /// <returns>True if geometry was added successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if meshName, vertexBuffer, or indexBuffer is null.</exception>
        /// <exception cref="ArgumentException">Thrown if vertexCount or indexCount is negative.</exception>
        public bool AddGeometry(string meshName, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int vertexCount, int indexCount)
        {
            if (string.IsNullOrEmpty(meshName))
            {
                throw new ArgumentNullException(nameof(meshName));
            }
            if (vertexBuffer == null)
            {
                throw new ArgumentNullException(nameof(vertexBuffer));
            }
            if (indexBuffer == null)
            {
                throw new ArgumentNullException(nameof(indexBuffer));
            }
            if (vertexCount < 0)
            {
                throw new ArgumentException("Vertex count must be non-negative.", nameof(vertexCount));
            }
            if (indexCount < 0)
            {
                throw new ArgumentException("Index count must be non-negative.", nameof(indexCount));
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

