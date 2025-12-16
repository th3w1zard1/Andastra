using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Depth-stencil buffer interface.
    /// </summary>
    public interface IDepthStencilBuffer : IDisposable
    {
        /// <summary>
        /// Gets the buffer width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the buffer height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the native buffer handle.
        /// </summary>
        IntPtr NativeHandle { get; }
    }
}

