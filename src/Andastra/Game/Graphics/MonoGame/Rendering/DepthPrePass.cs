using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Depth pre-pass (Z-prepass) rendering system.
    /// 
    /// Depth pre-pass renders geometry depth-only first, then main pass can
    /// early-Z reject fragments, reducing pixel shader overhead.
    /// 
    /// Benefits:
    /// - Reduces pixel shader invocations for occluded fragments
    /// - Enables better hardware early-Z optimization
    /// - Can be used for Hi-Z buffer generation
    /// - Improves performance on fill-rate bound scenes
    /// </summary>
    /// <remarks>
    /// Depth Pre-Pass (Modern Enhancement):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Original implementation: KOTOR uses immediate-mode rendering with depth buffer testing
    /// - Original engine: DirectX 8/9 fixed-function pipeline, no explicit depth pre-pass
    /// - Original rendering: Single-pass rendering with depth testing enabled
    /// - This is a modernization feature: Depth pre-pass reduces pixel shader overhead on modern GPUs
    /// - Modern enhancement: Two-pass rendering (depth-only, then full) for fill-rate optimization
    /// - Original engine: Rendered directly with depth buffer, relied on early-Z hardware when available
    /// </remarks>
    public class DepthPrePass : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _depthTarget;
        private DepthStencilState _depthOnlyState;
        private RasterizerState _depthPrepassRasterizer;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether depth pre-pass is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets the depth render target.
        /// </summary>
        public RenderTarget2D DepthTarget
        {
            get { return _depthTarget; }
        }

        /// <summary>
        /// Initializes a new depth pre-pass system.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        /// <param name="width">Render target width.</param>
        /// <param name="height">Render target height.</param>
        public DepthPrePass(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _enabled = true;

            // Create depth-only render state
            _depthOnlyState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            // Rasterizer state for depth prepass (typically no culling change needed)
            _depthPrepassRasterizer = new RasterizerState
            {
                CullMode = CullMode.CullCounterClockwiseFace,
                DepthBias = 0,
                FillMode = FillMode.Solid
            };

            CreateDepthTarget(width, height);
        }

        /// <summary>
        /// Begins depth pre-pass rendering.
        /// </summary>
        public void Begin()
        {
            if (!_enabled)
            {
                return;
            }

            // Set depth-only render target
            _graphicsDevice.SetRenderTarget(_depthTarget);
            _graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1.0f, 0);

            // Set depth-only state
            _graphicsDevice.DepthStencilState = _depthOnlyState;
            _graphicsDevice.RasterizerState = _depthPrepassRasterizer;

            // Disable color writes (depth only)
            _graphicsDevice.BlendState = BlendState.Opaque; // No blending, just depth
        }

        /// <summary>
        /// Ends depth pre-pass rendering.
        /// </summary>
        /// <param name="mainRenderTarget">Main render target to restore.</param>
        public void End(RenderTarget2D mainRenderTarget)
        {
            if (!_enabled)
            {
                return;
            }

            // Restore main render target
            _graphicsDevice.SetRenderTarget(mainRenderTarget);

            // Depth buffer is now filled - main pass can use early-Z
            // Set depth function to Equal or LessEqual to skip already-written pixels
            _graphicsDevice.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false, // Don't write depth again
                DepthBufferFunction = CompareFunction.Equal // Only render fragments that match depth prepass
            };
        }

        /// <summary>
        /// Resizes the depth target.
        /// </summary>
        public void Resize(int width, int height)
        {
            if (_depthTarget != null)
            {
                _depthTarget.Dispose();
            }
            CreateDepthTarget(width, height);
        }

        private void CreateDepthTarget(int width, int height)
        {
            // Create depth-only render target
            _depthTarget = new RenderTarget2D(
                _graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Single, // Depth stored as float
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.DiscardContents
            );
        }

        public void Dispose()
        {
            if (_depthTarget != null)
            {
                _depthTarget.Dispose();
                _depthTarget = null;
            }
            if (_depthOnlyState != null)
            {
                _depthOnlyState.Dispose();
                _depthOnlyState = null;
            }
            if (_depthPrepassRasterizer != null)
            {
                _depthPrepassRasterizer.Dispose();
                _depthPrepassRasterizer = null;
            }
        }
    }
}

