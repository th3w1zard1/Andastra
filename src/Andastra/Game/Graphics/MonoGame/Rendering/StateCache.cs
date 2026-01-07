using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Render state cache for minimizing state changes.
    /// 
    /// Caches render states (blend, depth, rasterizer) to avoid
    /// redundant state changes, improving CPU performance.
    /// 
    /// Features:
    /// - State change detection
    /// - Automatic caching
    /// - State comparison optimization
    /// </summary>
    public class StateCache
    {
        private BlendState _currentBlendState;
        private DepthStencilState _currentDepthStencilState;
        private RasterizerState _currentRasterizerState;
        private SamplerState _currentSamplerState;
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>
        /// Gets the number of state changes avoided.
        /// </summary>
        public int StateChangesAvoided { get; private set; }

        /// <summary>
        /// Initializes a new state cache.
        /// </summary>
        /// <summary>
        /// Initializes a new state cache.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for state management.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public StateCache(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Sets blend state (only if changed).
        /// </summary>
        /// <param name="state">Blend state to set. Can be null to use default.</param>
        public void SetBlendState(BlendState state)
        {
            if (state != _currentBlendState)
            {
                _graphicsDevice.BlendState = state;
                _currentBlendState = state;
            }
            else
            {
                StateChangesAvoided++;
            }
        }

        /// <summary>
        /// Sets depth stencil state (only if changed).
        /// </summary>
        /// <param name="state">Depth stencil state to set. Can be null to use default.</param>
        public void SetDepthStencilState(DepthStencilState state)
        {
            if (state != _currentDepthStencilState)
            {
                _graphicsDevice.DepthStencilState = state;
                _currentDepthStencilState = state;
            }
            else
            {
                StateChangesAvoided++;
            }
        }

        /// <summary>
        /// Sets rasterizer state (only if changed).
        /// </summary>
        /// <param name="state">Rasterizer state to set. Can be null to use default.</param>
        public void SetRasterizerState(RasterizerState state)
        {
            if (state != _currentRasterizerState)
            {
                _graphicsDevice.RasterizerState = state;
                _currentRasterizerState = state;
            }
            else
            {
                StateChangesAvoided++;
            }
        }

        /// <summary>
        /// Sets sampler state (only if changed).
        /// </summary>
        /// <param name="index">Sampler state index (0-15).</param>
        /// <param name="state">Sampler state to set.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of valid range (0-15).</exception>
        public void SetSamplerState(int index, SamplerState state)
        {
            if (index < 0 || index > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Sampler state index must be between 0 and 15.");
            }

            if (state != _currentSamplerState)
            {
                _graphicsDevice.SamplerStates[index] = state;
                _currentSamplerState = state;
            }
            else
            {
                StateChangesAvoided++;
            }
        }

        /// <summary>
        /// Resets state cache.
        /// </summary>
        public void Reset()
        {
            _currentBlendState = null;
            _currentDepthStencilState = null;
            _currentRasterizerState = null;
            _currentSamplerState = null;
            StateChangesAvoided = 0;
        }
    }
}

