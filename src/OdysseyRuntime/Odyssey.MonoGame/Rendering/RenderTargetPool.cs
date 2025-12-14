using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
{
    /// <summary>
    /// Render target pool for efficient render target reuse.
    /// 
    /// Render target pooling reduces allocation overhead by reusing
    /// render targets instead of creating new ones each frame.
    /// 
    /// Features:
    /// - Automatic render target reuse
    /// - Size-based pooling
    /// - Format-based organization
    /// - Automatic cleanup
    /// </summary>
    public class RenderTargetPool : IDisposable
    {
        /// <summary>
        /// Pool key for render targets.
        /// </summary>
        private struct PoolKey
        {
            public int Width;
            public int Height;
            public SurfaceFormat Format;
            public DepthFormat DepthFormat;
            public int MultiSampleCount;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<PoolKey, Stack<RenderTarget2D>> _pools;
        private readonly List<RenderTarget2D> _allTargets;
        private readonly object _lock;

        /// <summary>
        /// Gets the total number of pooled render targets.
        /// </summary>
        public int PoolSize
        {
            get
            {
                lock (_lock)
                {
                    return _allTargets.Count;
                }
            }
        }

        /// <summary>
        /// Initializes a new render target pool.
        /// </summary>
        public RenderTargetPool(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
            _pools = new Dictionary<PoolKey, Stack<RenderTarget2D>>();
            _allTargets = new List<RenderTarget2D>();
            _lock = new object();
        }

        /// <summary>
        /// Gets a render target from the pool or creates a new one.
        /// </summary>
        public RenderTarget2D Get(int width, int height, SurfaceFormat format = SurfaceFormat.Color, DepthFormat depthFormat = DepthFormat.None, int multiSampleCount = 0)
        {
            PoolKey key = new PoolKey
            {
                Width = width,
                Height = height,
                Format = format,
                DepthFormat = depthFormat,
                MultiSampleCount = multiSampleCount
            };

            lock (_lock)
            {
                Stack<RenderTarget2D> pool;
                if (_pools.TryGetValue(key, out pool) && pool.Count > 0)
                {
                    return pool.Pop();
                }
            }

            // Create new render target
            RenderTarget2D target = new RenderTarget2D(
                _graphicsDevice,
                width,
                height,
                false,
                format,
                depthFormat,
                multiSampleCount,
                RenderTargetUsage.DiscardContents
            );

            lock (_lock)
            {
                _allTargets.Add(target);
            }

            return target;
        }

        /// <summary>
        /// Returns a render target to the pool.
        /// </summary>
        public void Return(RenderTarget2D target)
        {
            if (target == null || target.IsDisposed)
            {
                return;
            }

            PoolKey key = new PoolKey
            {
                Width = target.Width,
                Height = target.Height,
                Format = target.Format,
                DepthFormat = target.DepthStencilFormat,
                MultiSampleCount = target.MultiSampleCount
            };

            lock (_lock)
            {
                Stack<RenderTarget2D> pool;
                if (!_pools.TryGetValue(key, out pool))
                {
                    pool = new Stack<RenderTarget2D>();
                    _pools[key] = pool;
                }

                // Limit pool size to prevent unbounded growth
                if (pool.Count < 4)
                {
                    pool.Push(target);
                }
                else
                {
                    // Dispose excess targets
                    target.Dispose();
                    _allTargets.Remove(target);
                }
            }
        }

        /// <summary>
        /// Clears all pooled render targets.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (Stack<RenderTarget2D> pool in _pools.Values)
                {
                    while (pool.Count > 0)
                    {
                        pool.Pop().Dispose();
                    }
                }
                _pools.Clear();
                _allTargets.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

