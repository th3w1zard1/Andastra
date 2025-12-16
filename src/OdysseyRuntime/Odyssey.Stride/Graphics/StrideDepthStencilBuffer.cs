using System;
using Stride.Graphics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IDepthStencilBuffer.
    /// </summary>
    public class StrideDepthStencilBuffer : IDepthStencilBuffer
    {
        private readonly Texture2D _depthBuffer;

        public StrideDepthStencilBuffer(Texture2D depthBuffer)
        {
            _depthBuffer = depthBuffer ?? throw new ArgumentNullException(nameof(depthBuffer));
        }

        public int Width => _depthBuffer.Width;

        public int Height => _depthBuffer.Height;

        public IntPtr NativeHandle => _depthBuffer.NativeDeviceTexture;

        public void Dispose()
        {
            _depthBuffer?.Dispose();
        }
    }
}

