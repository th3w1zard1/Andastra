using System;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IDepthStencilBuffer.
    /// Note: MonoGame doesn't support separate depth buffers natively, they're part of render targets.
    /// This implementation creates a dedicated RenderTarget2D for depth-stencil operations.
    /// Based on swkotor2.exe DirectX depth-stencil buffer system (IDirect3DDevice9::CreateDepthStencilSurface).
    /// </summary>
    public class MonoGameDepthStencilBuffer : IDepthStencilBuffer
    {
        private readonly RenderTarget2D _depthStencilBuffer;

        /// <summary>
        /// Creates a new depth-stencil buffer.
        /// </summary>
        /// <param name="graphicsDevice">MonoGame graphics device.</param>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        public MonoGameDepthStencilBuffer(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("Width and height must be positive values.");
            }

            // Create a render target that serves as our depth-stencil buffer
            // Use SurfaceFormat.Single for depth data storage, DepthFormat.Depth24Stencil8 for full depth-stencil support
            // This matches the original engine's depth-stencil surface creation pattern
            _depthStencilBuffer = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false, // No mipmaps for depth buffer
                SurfaceFormat.Single, // Store depth values as single-precision floats
                DepthFormat.Depth24Stencil8 // 24-bit depth + 8-bit stencil, matching DirectX 9 standards
            );
        }

        /// <summary>
        /// Gets the buffer width.
        /// </summary>
        public int Width => _depthStencilBuffer.Width;

        /// <summary>
        /// Gets the buffer height.
        /// </summary>
        public int Height => _depthStencilBuffer.Height;

        /// <summary>
        /// Gets the native buffer handle for advanced operations.
        /// Note: MonoGame's RenderTarget2D does not expose a Handle property directly,
        /// so we return IntPtr.Zero. Use the underlying RenderTarget2D for direct access.
        /// </summary>
        public IntPtr NativeHandle => IntPtr.Zero;

        /// <summary>
        /// Disposes of the depth-stencil buffer resources.
        /// </summary>
        public void Dispose()
        {
            _depthStencilBuffer?.Dispose();
        }
    }
}

