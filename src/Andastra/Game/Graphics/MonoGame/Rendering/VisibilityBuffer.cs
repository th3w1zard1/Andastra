using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Visibility buffer rendering system.
    /// 
    /// Visibility buffers store per-pixel geometry IDs instead of full G-buffer data,
    /// enabling deferred shading with minimal memory bandwidth.
    /// 
    /// Features:
    /// - Reduced memory bandwidth vs traditional G-buffer
    /// - Support for many materials
    /// - Efficient material evaluation
    /// - Modern rendering technique used in AAA games
    /// </summary>
    public class VisibilityBuffer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _visibilityBuffer;
        private RenderTarget2D _depthBuffer;
        private int _width;
        private int _height;

        /// <summary>
        /// Gets or sets whether visibility buffer is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets the visibility buffer render target.
        /// </summary>
        public RenderTarget2D Buffer
        {
            get { return _visibilityBuffer; }
        }

        /// <summary>
        /// Gets the depth buffer.
        /// </summary>
        public RenderTarget2D DepthBuffer
        {
            get { return _depthBuffer; }
        }

        /// <summary>
        /// Initializes a new visibility buffer system.
        /// </summary>
        public VisibilityBuffer(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _width = width;
            _height = height;

            CreateBuffers();
        }

        /// <summary>
        /// Begins visibility buffer pass.
        /// </summary>
        public void Begin()
        {
            if (!Enabled)
            {
                return;
            }

            _graphicsDevice.SetRenderTargets(_visibilityBuffer, _depthBuffer);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1.0f, 0);
        }

        /// <summary>
        /// Ends visibility buffer pass.
        /// </summary>
        public void End()
        {
            if (!Enabled)
            {
                return;
            }

            _graphicsDevice.SetRenderTarget(null);
        }

        /// <summary>
        /// Resizes visibility buffer.
        /// </summary>
        public void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            DisposeBuffers();
            CreateBuffers();
        }

        private void CreateBuffers()
        {
            // Visibility buffer: stores geometry ID (R16G16 = draw call ID + triangle ID)
            _visibilityBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Rg32, // Two 16-bit values
                DepthFormat.None
            );

            // Depth buffer
            _depthBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Single,
                DepthFormat.Depth24
            );
        }

        private void DisposeBuffers()
        {
            _visibilityBuffer?.Dispose();
            _depthBuffer?.Dispose();
        }

        public void Dispose()
        {
            DisposeBuffers();
        }
    }
}

