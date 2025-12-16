using System;
using Microsoft.Xna.Framework.Graphics;
using BioWareEngines.Graphics;

namespace BioWareEngines.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IRenderTarget.
    /// </summary>
    public class MonoGameRenderTarget : IRenderTarget
    {
        internal readonly RenderTarget2D RenderTarget;

        public MonoGameRenderTarget(RenderTarget2D renderTarget)
        {
            RenderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
        }

        public int Width => RenderTarget.Width;

        public int Height => RenderTarget.Height;

        public ITexture2D ColorTexture => new MonoGameTexture2D(RenderTarget);

        public IDepthStencilBuffer DepthStencilBuffer
        {
            get
            {
                // MonoGame doesn't expose depth buffer separately
                return null;
            }
        }

        public IntPtr NativeHandle => RenderTarget.Handle;

        public void Dispose()
        {
            RenderTarget?.Dispose();
        }
    }
}

