using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Pipeline state object (PSO) cache for modern graphics APIs.
    /// 
    /// PSOs precompile render state combinations, reducing driver overhead
    /// and enabling better GPU utilization in modern graphics APIs.
    /// 
    /// Features:
    /// - State combination caching
    /// - Automatic PSO creation
    /// - State validation
    /// - Performance optimization
    /// </summary>
    public class PipelineStateCache
    {
        /// <summary>
        /// Pipeline state key for caching.
        /// </summary>
        public struct PipelineStateKey
        {
            /// <summary>
            /// Vertex shader identifier.
            /// </summary>
            public uint VertexShaderId;

            /// <summary>
            /// Pixel shader identifier.
            /// </summary>
            public uint PixelShaderId;

            /// <summary>
            /// Blend state hash.
            /// </summary>
            public uint BlendStateHash;

            /// <summary>
            /// Depth stencil state hash.
            /// </summary>
            public uint DepthStencilStateHash;

            /// <summary>
            /// Rasterizer state hash.
            /// </summary>
            public uint RasterizerStateHash;

            /// <summary>
            /// Render target format.
            /// </summary>
            public uint RenderTargetFormat;

            /// <summary>
            /// Depth format.
            /// </summary>
            public uint DepthFormat;
        }

        private readonly Dictionary<PipelineStateKey, object> _psoCache;
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>
        /// Gets the number of cached PSOs.
        /// </summary>
        public int CacheSize
        {
            get { return _psoCache.Count; }
        }

        /// <summary>
        /// Initializes a new pipeline state cache.
        /// </summary>
        public PipelineStateCache(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _psoCache = new Dictionary<PipelineStateKey, object>();
        }

        /// <summary>
        /// Gets or creates a pipeline state object.
        /// </summary>
        public object GetOrCreatePSO(PipelineStateKey key, Func<object> createFunc)
        {
            object pso;
            if (_psoCache.TryGetValue(key, out pso))
            {
                return pso;
            }

            // Create new PSO
            pso = createFunc();
            _psoCache[key] = pso;

            return pso;
        }

        /// <summary>
        /// Creates a pipeline state key from current render state.
        /// </summary>
        public PipelineStateKey CreateKey(
            uint vertexShaderId,
            uint pixelShaderId,
            BlendState blendState,
            DepthStencilState depthStencilState,
            RasterizerState rasterizerState,
            SurfaceFormat renderTargetFormat,
            DepthFormat depthFormat)
        {
            return new PipelineStateKey
            {
                VertexShaderId = vertexShaderId,
                PixelShaderId = pixelShaderId,
                BlendStateHash = GetBlendStateHash(blendState),
                DepthStencilStateHash = GetDepthStencilStateHash(depthStencilState),
                RasterizerStateHash = GetRasterizerStateHash(rasterizerState),
                RenderTargetFormat = (uint)renderTargetFormat,
                DepthFormat = (uint)depthFormat
            };
        }

        /// <summary>
        /// Clears the PSO cache.
        /// </summary>
        public void Clear()
        {
            // Dispose cached PSOs if needed
            foreach (object pso in _psoCache.Values)
            {
                IDisposable disposable = pso as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
            _psoCache.Clear();
        }

        private uint GetBlendStateHash(BlendState state)
        {
            if (state == null)
            {
                return 0;
            }
            // Simple hash of blend state properties
            return (uint)(state.AlphaBlendFunction.GetHashCode() ^
                         state.ColorBlendFunction.GetHashCode() ^
                         state.AlphaSourceBlend.GetHashCode() ^
                         state.ColorSourceBlend.GetHashCode());
        }

        private uint GetDepthStencilStateHash(DepthStencilState state)
        {
            if (state == null)
            {
                return 0;
            }
            return (uint)(state.DepthBufferEnable.GetHashCode() ^
                         state.DepthBufferFunction.GetHashCode() ^
                         state.DepthBufferWriteEnable.GetHashCode());
        }

        private uint GetRasterizerStateHash(RasterizerState state)
        {
            if (state == null)
            {
                return 0;
            }
            return (uint)(state.CullMode.GetHashCode() ^
                         state.FillMode.GetHashCode() ^
                         state.DepthBias.GetHashCode());
        }
    }
}

