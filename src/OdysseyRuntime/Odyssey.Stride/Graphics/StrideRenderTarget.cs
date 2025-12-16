using System;
using Stride.Graphics;
using BioWareEngines.Graphics;

namespace BioWareEngines.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IRenderTarget.
    /// </summary>
    public class StrideRenderTarget : IRenderTarget
    {
        internal readonly Texture2D RenderTarget;
        private readonly Texture2D _depthBuffer;

        public StrideRenderTarget(Texture2D renderTarget, Texture2D depthBuffer = null)
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

