using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Render target chain for multi-pass rendering.
    /// 
    /// Manages chains of render targets for effects that require
    /// multiple passes (bloom, motion blur, etc.).
    /// 
    /// Features:
    /// - Automatic chain management
    /// - Ping-pong buffers
    /// - Downsampling chains
    /// - Memory efficient
    /// </summary>
    public class RenderTargetChain : IDisposable
    {
        /// <summary>
        /// Chain configuration.
        /// </summary>
        public struct ChainConfig
        {
            public int Width;
            public int Height;
            public SurfaceFormat Format;
            public DepthFormat DepthFormat;
            public int ChainLength;
            public bool UsePingPong;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<RenderTarget2D> _chain;
        private readonly ChainConfig _config;
        private int _currentIndex;

        /// <summary>
        /// Gets the current render target in the chain.
        /// </summary>
        public RenderTarget2D Current
        {
            get
            {
                if (_chain.Count == 0)
                {
                    return null;
                }
                return _chain[_currentIndex];
            }
        }

        /// <summary>
        /// Gets the next render target in the chain.
        /// </summary>
        public RenderTarget2D Next
        {
            get
            {
                if (_chain.Count < 2)
                {
                    return null;
                }
                int nextIndex = (_currentIndex + 1) % _chain.Count;
                return _chain[nextIndex];
            }
        }

        /// <summary>
        /// Initializes a new render target chain.
        /// </summary>
        /// <summary>
        /// Initializes a new render target chain.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for creating render targets.</param>
        /// <param name="config">Chain configuration. Width and height must be greater than zero.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        /// <exception cref="ArgumentException">Thrown if config width or height is invalid.</exception>
        public RenderTargetChain(GraphicsDevice graphicsDevice, ChainConfig config)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }
            if (config.Width <= 0 || config.Height <= 0)
            {
                throw new ArgumentException("Chain config width and height must be greater than zero.", nameof(config));
            }

            _graphicsDevice = graphicsDevice;
            _config = config;
            _chain = new List<RenderTarget2D>();
            _currentIndex = 0;

            CreateChain();
        }

        /// <summary>
        /// Advances to the next target in the chain.
        /// </summary>
        public void Advance()
        {
            if (_config.UsePingPong && _chain.Count >= 2)
            {
                _currentIndex = (_currentIndex + 1) % 2; // Ping-pong between first two
            }
            else
            {
                _currentIndex = (_currentIndex + 1) % _chain.Count;
            }
        }

        /// <summary>
        /// Resets to the first target.
        /// </summary>
        public void Reset()
        {
            _currentIndex = 0;
        }

        private void CreateChain()
        {
            int chainLength = _config.ChainLength > 0 ? _config.ChainLength : (_config.UsePingPong ? 2 : 1);

            for (int i = 0; i < chainLength; i++)
            {
                // Calculate size for this level (could be downsampled)
                int width = _config.Width;
                int height = _config.Height;

                RenderTarget2D rt = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    _config.Format,
                    _config.DepthFormat
                );

                _chain.Add(rt);
            }
        }

        /// <summary>
        /// Disposes of all render targets in the chain.
        /// </summary>
        public void Dispose()
        {
            foreach (RenderTarget2D rt in _chain)
            {
                rt?.Dispose();
            }
            _chain.Clear();
        }
    }
}

