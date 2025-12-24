using System;
using Andastra.Runtime.Graphics;
using Stride.Engine;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IRenderTarget.
    /// </summary>
    public class StrideRenderTarget : IRenderTarget
    {
        internal readonly global::Stride.Graphics.Texture RenderTarget;
        private readonly global::Stride.Graphics.Texture _depthBuffer;
        private readonly global::Stride.Graphics.CommandList _graphicsContext;

        public StrideRenderTarget(global::Stride.Graphics.Texture renderTarget, global::Stride.Graphics.Texture depthBuffer = null, global::Stride.Graphics.CommandList graphicsContext = null)
        {
            RenderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
            _depthBuffer = depthBuffer;
            _graphicsContext = graphicsContext;
        }

        public int Width => RenderTarget.Width;

        public int Height => RenderTarget.Height;

        public ITexture2D ColorTexture => new StrideTexture2D(RenderTarget, _graphicsContext);

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

        public IntPtr NativeHandle
        {
            get
            {
                // Stride Texture doesn't expose NativeDeviceTexture directly
                // Return IntPtr.Zero as Stride manages native resources internally
                return IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            RenderTarget?.Dispose();
            _depthBuffer?.Dispose();
        }
    }
}

