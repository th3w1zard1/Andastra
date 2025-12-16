using System;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IGraphicsDevice.
    /// </summary>
    public class MonoGameGraphicsDevice : IGraphicsDevice
    {
        private readonly GraphicsDevice _device;

        public MonoGameGraphicsDevice(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public Viewport Viewport
        {
            get
            {
                var vp = _device.Viewport;
                return new Viewport(vp.X, vp.Y, vp.Width, vp.Height, vp.MinDepth, vp.MaxDepth);
            }
        }

        public IRenderTarget RenderTarget
        {
            get
            {
                var rt = _device.GetRenderTargets();
                if (rt != null && rt.Length > 0 && rt[0].RenderTarget is RenderTarget2D mgRt)
                {
                    return new MonoGameRenderTarget(mgRt);
                }
                return null;
            }
            set
            {
                if (value == null)
                {
                    _device.SetRenderTarget(null);
                }
                else if (value is MonoGameRenderTarget mgRt)
                {
                    _device.SetRenderTarget(mgRt.RenderTarget);
                }
                else
                {
                    throw new ArgumentException("Render target must be a MonoGameRenderTarget", nameof(value));
                }
            }
        }

        public IDepthStencilBuffer DepthStencilBuffer
        {
            get
            {
                // MonoGame doesn't expose depth buffer separately, it's part of render target
                return null;
            }
            set
            {
                // MonoGame doesn't support separate depth buffer setting
            }
        }

        public void Clear(Color color)
        {
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            _device.Clear(mgColor);
        }

        public void ClearDepth(float depth)
        {
            _device.Clear(Microsoft.Xna.Framework.Graphics.ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, depth, 0);
        }

        public void ClearStencil(int stencil)
        {
            _device.Clear(Microsoft.Xna.Framework.Graphics.ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1.0f, stencil);
        }

        public ITexture2D CreateTexture2D(int width, int height, byte[] data)
        {
            var texture = new Texture2D(_device, width, height);
            if (data != null)
            {
                texture.SetData(data);
            }
            return new MonoGameTexture2D(texture);
        }

        public IRenderTarget CreateRenderTarget(int width, int height, bool hasDepthStencil = true)
        {
            var rt = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, hasDepthStencil ? DepthFormat.Depth24Stencil8 : DepthFormat.None);
            return new MonoGameRenderTarget(rt);
        }

        public IDepthStencilBuffer CreateDepthStencilBuffer(int width, int height)
        {
            // MonoGame doesn't support separate depth buffers, they're part of render targets
            throw new NotSupportedException("MonoGame does not support separate depth-stencil buffers. Use CreateRenderTarget with hasDepthStencil=true.");
        }

        public IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : struct
        {
            var buffer = new VertexBuffer(_device, typeof(T), data.Length, BufferUsage.None);
            if (data != null && data.Length > 0)
            {
                buffer.SetData(data);
            }
            return new MonoGameVertexBuffer(buffer, data != null ? data.Length : 0, System.Runtime.InteropServices.Marshal.SizeOf<T>());
        }

        public IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true)
        {
            IndexElementSize elementSize = isShort ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits;
            var buffer = new IndexBuffer(_device, elementSize, indices.Length, BufferUsage.None);
            if (indices != null && indices.Length > 0)
            {
                buffer.SetData(indices);
            }
            return new MonoGameIndexBuffer(buffer, indices != null ? indices.Length : 0, isShort);
        }

        public ISpriteBatch CreateSpriteBatch()
        {
            return new MonoGameSpriteBatch(new SpriteBatch(_device));
        }

        public IntPtr NativeHandle => _device.Handle;

        public void Dispose()
        {
            // GraphicsDevice is managed by Game, don't dispose it
        }
    }
}

