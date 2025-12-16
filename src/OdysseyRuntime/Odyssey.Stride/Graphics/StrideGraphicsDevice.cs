using System;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsDevice.
    /// </summary>
    public class StrideGraphicsDevice : IGraphicsDevice
    {
        private readonly GraphicsDevice _device;

        public StrideGraphicsDevice(GraphicsDevice device)
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
                var rt = _device.CurrentRenderTargets;
                if (rt != null && rt.Length > 0 && rt[0] != null)
                {
                    return new StrideRenderTarget(rt[0]);
                }
                return null;
            }
            set
            {
                if (value == null)
                {
                    _device.SetRenderTargets(null);
                }
                else if (value is StrideRenderTarget strideRt)
                {
                    _device.SetRenderTargets(strideRt.RenderTarget);
                }
                else
                {
                    throw new ArgumentException("Render target must be a StrideRenderTarget", nameof(value));
                }
            }
        }

        public IDepthStencilBuffer DepthStencilBuffer
        {
            get
            {
                // Stride depth buffer is part of render target
                return null;
            }
            set
            {
                // Stride doesn't support separate depth buffer setting
            }
        }

        public void Clear(Color color)
        {
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            _device.Clear(strideColor);
        }

        public void ClearDepth(float depth)
        {
            _device.Clear(Stride.Core.Mathematics.Color4.Black, ClearOptions.DepthBuffer, depth, 0);
        }

        public void ClearStencil(int stencil)
        {
            _device.Clear(Stride.Core.Mathematics.Color4.Black, ClearOptions.Stencil, 1.0f, stencil);
        }

        public ITexture2D CreateTexture2D(int width, int height, byte[] data)
        {
            var texture = Texture2D.New2D(_device, width, height, PixelFormat.R8G8B8A8_UNorm);
            if (data != null)
            {
                var colorData = new Stride.Core.Mathematics.Color[data.Length / 4];
                for (int i = 0; i < colorData.Length; i++)
                {
                    int offset = i * 4;
                    colorData[i] = new Stride.Core.Mathematics.Color(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
                }
                texture.SetData(_device.ImmediateContext, colorData);
            }
            return new StrideTexture2D(texture);
        }

        public IRenderTarget CreateRenderTarget(int width, int height, bool hasDepthStencil = true)
        {
            var rt = Texture2D.New2D(_device, width, height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget);
            var depthBuffer = hasDepthStencil ? Texture2D.New2D(_device, width, height, PixelFormat.D24_UNorm_S8_UInt, TextureFlags.DepthStencil) : null;
            return new StrideRenderTarget(rt, depthBuffer);
        }

        public IDepthStencilBuffer CreateDepthStencilBuffer(int width, int height)
        {
            // Stride doesn't support separate depth buffers, they're part of render targets
            throw new NotSupportedException("Stride does not support separate depth-stencil buffers. Use CreateRenderTarget with hasDepthStencil=true.");
        }

        public IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : struct
        {
            var buffer = Stride.Graphics.Buffer.Vertex.New(_device, data, GraphicsResourceUsage.Dynamic);
            return new StrideVertexBuffer(buffer, data != null ? data.Length : 0, System.Runtime.InteropServices.Marshal.SizeOf<T>());
        }

        public IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true)
        {
            Stride.Graphics.Buffer buffer;
            if (isShort)
            {
                var shortIndices = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                buffer = Stride.Graphics.Buffer.Index.New(_device, shortIndices, GraphicsResourceUsage.Dynamic);
            }
            else
            {
                buffer = Stride.Graphics.Buffer.Index.New(_device, indices, GraphicsResourceUsage.Dynamic);
            }
            return new StrideIndexBuffer(buffer, indices != null ? indices.Length : 0, isShort);
        }

        public ISpriteBatch CreateSpriteBatch()
        {
            return new StrideSpriteBatch(new SpriteBatch(_device));
        }

        public IntPtr NativeHandle => _device.NativeDevice;

        public void Dispose()
        {
            // GraphicsDevice is managed by Game, don't dispose it
        }
    }
}

