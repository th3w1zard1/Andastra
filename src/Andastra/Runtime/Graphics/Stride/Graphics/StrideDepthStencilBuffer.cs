using System;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IDepthStencilBuffer.
    /// </summary>
    public class StrideDepthStencilBuffer : IDepthStencilBuffer
    {
        private readonly global::Stride.Graphics.Texture _depthBuffer;

        public StrideDepthStencilBuffer(global::Stride.Graphics.Texture depthBuffer)
        {
            _depthBuffer = depthBuffer ?? throw new ArgumentNullException(nameof(depthBuffer));
        }

        public int Width => _depthBuffer.Width;

        public int Height => _depthBuffer.Height;

        public IntPtr NativeHandle => _depthBuffer?.NativeDeviceTexture ?? IntPtr.Zero;

        public void Dispose()
        {
            _depthBuffer?.Dispose();
        }
    }
}

