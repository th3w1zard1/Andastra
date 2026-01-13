using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Render target pool for efficient render target reuse.
    /// 
    /// Render target pooling reduces allocation overhead by reusing render targets
    /// across frames, preventing GC pressure and improving performance.
    /// 
    /// Features:
    /// - Automatic render target reuse
    /// - Size-based pooling
    /// - Format-based organization
    /// - Automatic cleanup
    /// </summary>
    /// <remarks>
    /// Render Target Pool (Modern Enhancement):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Located via string references: "Frame Buffer" @ 0x007c8408, "CB_FRAMEBUFF" @ 0x007d1d84
    /// - OpenGL render texture: "WGL_NV_render_texture_rectangle" @ 0x007b880c, "WGL_ARB_render_texture" @ 0x007b8890
    /// - Original implementation: KOTOR used minimal render targets (mainly for UI overlays)
    /// - Original engine: DirectX 8/9 era, limited use of render-to-texture features
    /// - This is a modernization feature: Render target pooling improves memory efficiency
    /// - Original behavior: Render targets allocated on-demand, released when done
    /// - Modern enhancement: Pooling reduces allocation overhead and GC pressure
    /// - Render targets: Used for post-processing, shadows, reflections, and multi-pass rendering
    /// </remarks>
    public class RenderTargetPool : IDisposable
    {
        /// <summary>
        /// Render target pool entry.
        /// </summary>
        private class PoolEntry
        {
            public RenderTarget2D RenderTarget;
            public bool InUse;
            public int LastUsedFrame;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, List<PoolEntry>> _pools;
        private readonly object _lock;
        private int _currentFrame;
        private int _maxFramesToKeep;

        /// <summary>
        /// Gets or sets the maximum frames to keep unused render targets.
        /// </summary>
        public int MaxFramesToKeep
        {
            get { return _maxFramesToKeep; }
            set { _maxFramesToKeep = Math.Max(1, value); }
        }

        /// <summary>
        /// Initializes a new render target pool.
        /// </summary>
        public RenderTargetPool(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _pools = new Dictionary<string, List<PoolEntry>>();
            _lock = new object();
            _currentFrame = 0;
            _maxFramesToKeep = 60; // Keep for 1 second at 60 FPS
        }

        /// <summary>
        /// Gets a render target from the pool or creates a new one.
        /// </summary>
        /// <param name="width">Render target width. Must be greater than zero.</param>
        /// <param name="height">Render target height. Must be greater than zero.</param>
        /// <param name="format">Surface format.</param>
        /// <param name="depthFormat">Depth format.</param>
        /// <returns>Render target from pool or newly created.</returns>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public RenderTarget2D GetRenderTarget(int width, int height, SurfaceFormat format, DepthFormat depthFormat)
        {
            if (width <= 0)
            {
                throw new ArgumentException("Width must be greater than zero.", nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentException("Height must be greater than zero.", nameof(height));
            }

            string key = CreateKey(width, height, format, depthFormat);

            lock (_lock)
            {
                List<PoolEntry> pool;
                if (_pools.TryGetValue(key, out pool))
                {
                    // Find unused render target
                    foreach (PoolEntry entry in pool)
                    {
                        if (!entry.InUse)
                        {
                            entry.InUse = true;
                            entry.LastUsedFrame = _currentFrame;
                            return entry.RenderTarget;
                        }
                    }
                }
                else
                {
                    pool = new List<PoolEntry>();
                    _pools[key] = pool;
                }

                // Create new render target
                RenderTarget2D rt = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    format,
                    depthFormat
                );

                PoolEntry newEntry = new PoolEntry
                {
                    RenderTarget = rt,
                    InUse = true,
                    LastUsedFrame = _currentFrame
                };
                pool.Add(newEntry);

                return rt;
            }
        }

        /// <summary>
        /// Returns a render target to the pool.
        /// </summary>
        public void ReturnRenderTarget(RenderTarget2D renderTarget)
        {
            if (renderTarget == null)
            {
                return;
            }

            lock (_lock)
            {
                foreach (List<PoolEntry> pool in _pools.Values)
                {
                    foreach (PoolEntry entry in pool)
                    {
                        if (entry.RenderTarget == renderTarget)
                        {
                            entry.InUse = false;
                            entry.LastUsedFrame = _currentFrame;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Advances to the next frame and cleans up old render targets.
        /// </summary>
        public void AdvanceFrame()
        {
            _currentFrame++;

            lock (_lock)
            {
                // Clean up old unused render targets
                var keysToRemove = new List<string>();
                foreach (var kvp in _pools)
                {
                    List<PoolEntry> pool = kvp.Value;
                    var entriesToRemove = new List<PoolEntry>();

                    foreach (PoolEntry entry in pool)
                    {
                        if (!entry.InUse && (_currentFrame - entry.LastUsedFrame) > _maxFramesToKeep)
                        {
                            entriesToRemove.Add(entry);
                        }
                    }

                    foreach (PoolEntry entry in entriesToRemove)
                    {
                        entry.RenderTarget.Dispose();
                        pool.Remove(entry);
                    }

                    if (pool.Count == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    _pools.Remove(key);
                }
            }
        }

        /// <summary>
        /// Clears all render targets from the pool, disposing them and resetting the pool to an empty state.
        /// The pool object remains usable after clearing and can be reused for new render targets.
        /// </summary>
        /// <remarks>
        /// This method differs from Dispose() in that it keeps the pool object alive and reusable.
        /// Dispose() should be called when the pool is no longer needed and will be garbage collected.
        /// 
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture:
        /// - Original engine: Render targets allocated on-demand, released when done
        /// - Modern enhancement: Pooling with clear capability for memory management
        /// - Clear operation: Disposes all render targets and resets pool state
        /// </remarks>
        public void Clear()
        {
            lock (_lock)
            {
                // Dispose all render targets in all pools
                foreach (List<PoolEntry> pool in _pools.Values)
                {
                    foreach (PoolEntry entry in pool)
                    {
                        entry.RenderTarget?.Dispose();
                    }
                }

                // Clear all pools
                _pools.Clear();

                // Reset frame counter to start fresh
                // Note: This is optional but helps with tracking after clear
                _currentFrame = 0;
            }
        }

        /// <summary>
        /// Creates a key for render target lookup.
        /// </summary>
        private string CreateKey(int width, int height, SurfaceFormat format, DepthFormat depthFormat)
        {
            return $"{width}x{height}_{format}_{depthFormat}";
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (List<PoolEntry> pool in _pools.Values)
                {
                    foreach (PoolEntry entry in pool)
                    {
                        entry.RenderTarget?.Dispose();
                    }
                }
                _pools.Clear();
            }
        }
    }
}
