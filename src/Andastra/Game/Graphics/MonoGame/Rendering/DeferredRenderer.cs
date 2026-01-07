using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Deferred rendering pipeline with G-buffer.
    /// 
    /// Deferred rendering separates geometry rendering from lighting:
    /// 1. Geometry pass: Render to G-buffer (albedo, normal, depth, material)
    /// 2. Lighting pass: Calculate lighting from G-buffer data
    /// 3. Forward pass: Render transparent objects
    /// 
    /// Benefits:
    /// - Supports many lights efficiently
    /// - Decouples geometry complexity from light count
    /// - Enables advanced lighting techniques (SSAO, SSR, etc.)
    /// </summary>
    public class DeferredRenderer : IDisposable
    {
        /// <summary>
        /// G-buffer render targets.
        /// </summary>
        public struct GBuffer
        {
            /// <summary>
            /// Albedo + roughness (RGBA).
            /// </summary>
            public RenderTarget2D AlbedoRoughness;

            /// <summary>
            /// Normal + metallic (RGBA).
            /// </summary>
            public RenderTarget2D NormalMetallic;

            /// <summary>
            /// Depth buffer.
            /// </summary>
            public RenderTarget2D Depth;

            /// <summary>
            /// Emissive + AO (RGBA).
            /// </summary>
            public RenderTarget2D EmissiveAO;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private GBuffer _gBuffer;
        private RenderTarget2D _lightingBuffer;
        private int _width;
        private int _height;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether deferred rendering is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets the G-buffer.
        /// </summary>
        public GBuffer Buffer
        {
            get { return _gBuffer; }
        }

        /// <summary>
        /// Gets the lighting buffer.
        /// </summary>
        public RenderTarget2D LightingBuffer
        {
            get { return _lightingBuffer; }
        }

        /// <summary>
        /// Initializes a new deferred renderer.
        /// </summary>
        public DeferredRenderer(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _width = width;
            _height = height;
            _enabled = true;

            CreateGBuffer();
        }

        /// <summary>
        /// Begins geometry pass (G-buffer rendering).
        /// </summary>
        public void BeginGeometryPass()
        {
            if (!_enabled)
            {
                return;
            }

            // Set multiple render targets (MRT)
            _graphicsDevice.SetRenderTargets(
                _gBuffer.AlbedoRoughness,
                _gBuffer.NormalMetallic,
                _gBuffer.Depth,
                _gBuffer.EmissiveAO
            );

            // Clear all targets
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1.0f, 0);
        }

        /// <summary>
        /// Ends geometry pass and begins lighting pass.
        /// </summary>
        public void BeginLightingPass()
        {
            if (!_enabled)
            {
                return;
            }

            // Set lighting buffer as render target
            _graphicsDevice.SetRenderTarget(_lightingBuffer);
            _graphicsDevice.Clear(Color.Black);

            // Bind G-buffer textures as shader resources
            // _graphicsDevice.Textures[0] = _gBuffer.AlbedoRoughness;
            // _graphicsDevice.Textures[1] = _gBuffer.NormalMetallic;
            // _graphicsDevice.Textures[2] = _gBuffer.Depth;
            // _graphicsDevice.Textures[3] = _gBuffer.EmissiveAO;
        }

        /// <summary>
        /// Ends lighting pass.
        /// </summary>
        public void EndLightingPass()
        {
            if (!_enabled)
            {
                return;
            }

            // Lighting pass complete
        }

        /// <summary>
        /// Resizes G-buffer for new resolution.
        /// </summary>
        public void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            DisposeGBuffer();
            CreateGBuffer();
        }

        private void CreateGBuffer()
        {
            // Albedo + Roughness (RGBA8)
            _gBuffer.AlbedoRoughness = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None
            );

            // Normal + Metallic (RGBA16F for precision)
            _gBuffer.NormalMetallic = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None
            );

            // Depth (R32F)
            _gBuffer.Depth = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Single,
                DepthFormat.None
            );

            // Emissive + AO (RGBA8)
            _gBuffer.EmissiveAO = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None
            );

            // Lighting buffer (RGBA16F for HDR)
            _lightingBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None
            );
        }

        private void DisposeGBuffer()
        {
            _gBuffer.AlbedoRoughness?.Dispose();
            _gBuffer.NormalMetallic?.Dispose();
            _gBuffer.Depth?.Dispose();
            _gBuffer.EmissiveAO?.Dispose();
            _lightingBuffer?.Dispose();
        }

        public void Dispose()
        {
            DisposeGBuffer();
        }
    }
}

