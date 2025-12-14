using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
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
        public StateCache(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Sets blend state (only if changed).
        /// </summary>
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
        public void SetSamplerState(int index, SamplerState state)
        {
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

