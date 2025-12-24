using System;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IGraphicsDevice.
    /// </summary>
    public class MonoGameGraphicsDevice : IGraphicsDevice
    {
        private readonly GraphicsDevice _device;

        /// <summary>
        /// Gets the underlying MonoGame GraphicsDevice (for interop use).
        /// </summary>
        public GraphicsDevice Device => _device;

        public MonoGameGraphicsDevice(GraphicsDevice device)
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
            // Create a dedicated depth-stencil buffer using RenderTarget2D
            // This provides separate depth-stencil functionality for advanced rendering techniques
            return new MonoGameDepthStencilBuffer(_device, width, height);
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

        public IntPtr NativeHandle => IntPtr.Zero; // MonoGame GraphicsDevice doesn't expose Handle directly

        // 3D Rendering Methods

        public void SetVertexBuffer(IVertexBuffer vertexBuffer)
        {
            if (vertexBuffer == null)
            {
                _device.SetVertexBuffer(null);
            }
            else if (vertexBuffer is MonoGameVertexBuffer mgVb)
            {
                _device.SetVertexBuffer(mgVb.Buffer);
            }
            else
            {
                throw new ArgumentException("Vertex buffer must be a MonoGameVertexBuffer", nameof(vertexBuffer));
            }
        }

        public void SetIndexBuffer(IIndexBuffer indexBuffer)
        {
            if (indexBuffer == null)
            {
                _device.Indices = null;
            }
            else if (indexBuffer is MonoGameIndexBuffer mgIb)
            {
                _device.Indices = mgIb.Buffer;
            }
            else
            {
                throw new ArgumentException("Index buffer must be a MonoGameIndexBuffer", nameof(indexBuffer));
            }
        }

        public void DrawIndexedPrimitives(Andastra.Runtime.Graphics.PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount)
        {
            _device.DrawIndexedPrimitives(
                ConvertPrimitiveType(primitiveType),
                baseVertex,
                minVertexIndex,
                numVertices,
                startIndex,
                primitiveCount
            );
        }

        public void DrawPrimitives(Andastra.Runtime.Graphics.PrimitiveType primitiveType, int vertexOffset, int primitiveCount)
        {
            _device.DrawPrimitives(
                ConvertPrimitiveType(primitiveType),
                vertexOffset,
                primitiveCount
            );
        }

        public void SetRasterizerState(IRasterizerState rasterizerState)
        {
            if (rasterizerState == null)
            {
                _device.RasterizerState = RasterizerState.CullCounterClockwise;
            }
            else if (rasterizerState is MonoGameRasterizerState mgRs)
            {
                _device.RasterizerState = mgRs.State;
            }
            else
            {
                throw new ArgumentException("Rasterizer state must be a MonoGameRasterizerState", nameof(rasterizerState));
            }
        }

        public void SetDepthStencilState(IDepthStencilState depthStencilState)
        {
            if (depthStencilState == null)
            {
                _device.DepthStencilState = DepthStencilState.Default;
            }
            else if (depthStencilState is MonoGameDepthStencilState mgDs)
            {
                _device.DepthStencilState = mgDs.State;
            }
            else
            {
                throw new ArgumentException("Depth-stencil state must be a MonoGameDepthStencilState", nameof(depthStencilState));
            }
        }

        public void SetBlendState(IBlendState blendState)
        {
            if (blendState == null)
            {
                _device.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Opaque;
            }
            else if (blendState is MonoGameBlendState mgBs)
            {
                _device.BlendState = mgBs.State;
            }
            else
            {
                throw new ArgumentException("Blend state must be a MonoGameBlendState", nameof(blendState));
            }
        }

        public void SetSamplerState(int index, ISamplerState samplerState)
        {
            if (samplerState == null)
            {
                _device.SamplerStates[index] = SamplerState.LinearWrap;
            }
            else if (samplerState is MonoGameSamplerState mgSs)
            {
                _device.SamplerStates[index] = mgSs.State;
            }
            else
            {
                throw new ArgumentException("Sampler state must be a MonoGameSamplerState", nameof(samplerState));
            }
        }

        public IBasicEffect CreateBasicEffect()
        {
            return new MonoGameBasicEffect(_device);
        }

        public IRasterizerState CreateRasterizerState()
        {
            return new MonoGameRasterizerState();
        }

        public IDepthStencilState CreateDepthStencilState()
        {
            return new MonoGameDepthStencilState();
        }

        public IBlendState CreateBlendState()
        {
            return new MonoGameBlendState();
        }

        public ISamplerState CreateSamplerState()
        {
            return new MonoGameSamplerState();
        }

        public void Dispose()
        {
            // GraphicsDevice is managed by Game, don't dispose it
        }

        private static Microsoft.Xna.Framework.Graphics.PrimitiveType ConvertPrimitiveType(Andastra.Runtime.Graphics.PrimitiveType type)
        {
            switch (type)
            {
                case Andastra.Runtime.Graphics.PrimitiveType.TriangleList:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList;
                case Andastra.Runtime.Graphics.PrimitiveType.TriangleStrip:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleStrip;
                case Andastra.Runtime.Graphics.PrimitiveType.LineList:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.LineList;
                case Andastra.Runtime.Graphics.PrimitiveType.LineStrip:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.LineStrip;
                case Andastra.Runtime.Graphics.PrimitiveType.PointList:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.PointList;
                default:
                    return Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList;
            }
        }
    }
}

