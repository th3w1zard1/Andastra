using System;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IRenderTarget.
    /// </summary>
    public class StrideRenderTarget : IRenderTarget
    {
        internal readonly global::Stride.Graphics.Texture RenderTarget;
        private readonly global::Stride.Graphics.Texture _depthBuffer;

        public StrideRenderTarget(global::Stride.Graphics.Texture renderTarget, global::Stride.Graphics.Texture depthBuffer = null)
        {
            RenderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
            _depthBuffer = depthBuffer;
        }

        public int Width => RenderTarget.Width;

        public int Height => RenderTarget.Height;

        public ITexture2D ColorTexture => new StrideTexture2D(RenderTarget);

        public IDepthStencilBuffer DepthStencilBuffer
        {
            get
            {
                if (_depthBuffer != null)
                {
                    return new StrideDepthStencilBuffer(_depthBuffer);
                }
                return null;
            }
        }

        public IntPtr NativeHandle => RenderTarget.NativeDeviceTexture;

        public void Dispose()
        {
            RenderTarget?.Dispose();
            _depthBuffer?.Dispose();
        }
    }
}

