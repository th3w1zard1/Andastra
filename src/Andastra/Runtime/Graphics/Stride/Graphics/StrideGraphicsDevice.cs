using System;
using StrideGraphics = Stride.Graphics;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsDevice.
    /// </summary>
    public class StrideGraphicsDevice : IGraphicsDevice
    {
        private readonly StrideGraphics.GraphicsDevice _device;

        public StrideGraphicsDevice(StrideGraphics.GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public Andastra.Runtime.Graphics.Viewport Viewport
        {
            get
            {
                var vp = _device.Viewport;
                return new Andastra.Runtime.Graphics.Viewport(vp.X, vp.Y, vp.Width, vp.Height, vp.MinDepth, vp.MaxDepth);
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

        public void Clear(Andastra.Runtime.Graphics.Color color)
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
            var buffer = StrideGraphics.Buffer.Vertex.New(_device, data, StrideGraphics.GraphicsResourceUsage.Dynamic);
            return new StrideVertexBuffer(buffer, data != null ? data.Length : 0, System.Runtime.InteropServices.Marshal.SizeOf<T>());
        }

        public IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true)
        {
            StrideGraphics.Buffer buffer;
            if (isShort)
            {
                var shortIndices = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                buffer = StrideGraphics.Buffer.Index.New(_device, shortIndices, StrideGraphics.GraphicsResourceUsage.Dynamic);
            }
            else
            {
                buffer = StrideGraphics.Buffer.Index.New(_device, indices, StrideGraphics.GraphicsResourceUsage.Dynamic);
            }
            return new StrideIndexBuffer(buffer, indices != null ? indices.Length : 0, isShort);
        }

        public ISpriteBatch CreateSpriteBatch()
        {
            // Stride SpriteBatch requires GraphicsDevice, which we have
            return new StrideSpriteBatch(new StrideGraphics.SpriteBatch(_device));
        }

        public IntPtr NativeHandle => _device.NativeDevice;

        // 3D Rendering Methods
        public void SetVertexBuffer(IVertexBuffer vertexBuffer)
        {
            if (vertexBuffer == null)
            {
                _device.SetVertexBuffer(0, null, 0, 0);
            }
            else if (vertexBuffer is StrideVertexBuffer strideVb)
            {
                _device.SetVertexBuffer(0, strideVb.Buffer, 0, strideVb.VertexStride);
            }
            else
            {
                throw new ArgumentException("Vertex buffer must be a StrideVertexBuffer", nameof(vertexBuffer));
            }
        }

        public void SetIndexBuffer(IIndexBuffer indexBuffer)
        {
            if (indexBuffer == null)
            {
                _device.SetIndexBuffer(null, 0, false);
            }
            else if (indexBuffer is StrideIndexBuffer strideIb)
            {
                _device.SetIndexBuffer(strideIb.Buffer, 0, strideIb.IsShort);
            }
            else
            {
                throw new ArgumentException("Index buffer must be a StrideIndexBuffer", nameof(indexBuffer));
            }
        }

        public void DrawIndexedPrimitives(Andastra.Runtime.Graphics.PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount)
        {
            _device.DrawIndexed(
                ConvertPrimitiveType(primitiveType),
                startIndex,
                baseVertex,
                primitiveCount
            );
        }

        public void DrawPrimitives(Andastra.Runtime.Graphics.PrimitiveType primitiveType, int vertexOffset, int primitiveCount)
        {
            _device.Draw(
                ConvertPrimitiveType(primitiveType),
                vertexOffset,
                primitiveCount
            );
        }

        public void SetRasterizerState(IRasterizerState rasterizerState)
        {
            if (rasterizerState == null)
            {
                _device.SetRasterizerState(RasterizerStateDescription.Default);
            }
            else if (rasterizerState is StrideRasterizerState strideRs)
            {
                _device.SetRasterizerState(strideRs.Description);
            }
            else
            {
                throw new ArgumentException("Rasterizer state must be a StrideRasterizerState", nameof(rasterizerState));
            }
        }

        public void SetDepthStencilState(IDepthStencilState depthStencilState)
        {
            if (depthStencilState == null)
            {
                _device.SetDepthStencilState(DepthStencilStateDescription.Default);
            }
            else if (depthStencilState is StrideDepthStencilState strideDs)
            {
                _device.SetDepthStencilState(strideDs.Description);
            }
            else
            {
                throw new ArgumentException("Depth-stencil state must be a StrideDepthStencilState", nameof(depthStencilState));
            }
        }

        public void SetBlendState(IBlendState blendState)
        {
            if (blendState == null)
            {
                _device.SetBlendState(BlendStateDescription.Default);
            }
            else if (blendState is StrideBlendState strideBs)
            {
                _device.SetBlendState(strideBs.Description);
            }
            else
            {
                throw new ArgumentException("Blend state must be a StrideBlendState", nameof(blendState));
            }
        }

        public void SetSamplerState(int index, ISamplerState samplerState)
        {
            if (samplerState == null)
            {
                _device.SetSamplerState(index, SamplerStateDescription.Default);
            }
            else if (samplerState is StrideSamplerState strideSs)
            {
                _device.SetSamplerState(index, strideSs.Description);
            }
            else
            {
                throw new ArgumentException("Sampler state must be a StrideSamplerState", nameof(samplerState));
            }
        }

        public IBasicEffect CreateBasicEffect()
        {
            return new StrideBasicEffect(_device);
        }

        public IRasterizerState CreateRasterizerState()
        {
            return new StrideRasterizerState();
        }

        public IDepthStencilState CreateDepthStencilState()
        {
            return new StrideDepthStencilState();
        }

        public IBlendState CreateBlendState()
        {
            return new StrideBlendState();
        }

        public ISamplerState CreateSamplerState()
        {
            return new StrideSamplerState();
        }

        public void Dispose()
        {
            // GraphicsDevice is managed by Game, don't dispose it
        }

        private static StrideGraphics.PrimitiveType ConvertPrimitiveType(Andastra.Runtime.Graphics.PrimitiveType type)
        {
            switch (type)
            {
                case Andastra.Runtime.Graphics.PrimitiveType.TriangleList:
                    return StrideGraphics.PrimitiveType.TriangleList;
                case Andastra.Runtime.Graphics.PrimitiveType.TriangleStrip:
                    return StrideGraphics.PrimitiveType.TriangleStrip;
                case Andastra.Runtime.Graphics.PrimitiveType.LineList:
                    return StrideGraphics.PrimitiveType.LineList;
                case Andastra.Runtime.Graphics.PrimitiveType.LineStrip:
                    return StrideGraphics.PrimitiveType.LineStrip;
                case Andastra.Runtime.Graphics.PrimitiveType.PointList:
                    return StrideGraphics.PrimitiveType.PointList;
                default:
                    return StrideGraphics.PrimitiveType.TriangleList;
            }
        }
    }
}

