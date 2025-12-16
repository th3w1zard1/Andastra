using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Render target interface for off-screen rendering.
    /// </summary>
    public interface IRenderTarget : IDisposable
    {
        /// <summary>
        /// Gets the render target width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the render target height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the color texture.
        /// </summary>
        ITexture2D ColorTexture { get; }

        /// <summary>
        /// Gets the depth-stencil buffer (if created).
        /// </summary>
        IDepthStencilBuffer DepthStencilBuffer { get; }

        /// <summary>
        /// Gets the native render target handle.
        /// </summary>
        IntPtr NativeHandle { get; }
    }
}

