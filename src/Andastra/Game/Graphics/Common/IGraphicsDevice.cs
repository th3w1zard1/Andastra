using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Graphics device abstraction for rendering operations.
    /// Provides unified access to graphics hardware across MonoGame and Stride.
    /// </summary>
    /// <remarks>
    /// Graphics Device Interface:
    /// - Based on swkotor2.exe DirectX device system
    /// - Located via string references: "Render Window" @ 0x007b5680, "render" @ 0x007bab34
    /// - "WGL_NV_render_texture_rectangle" @ 0x007b880c, "WGL_ARB_render_texture" @ 0x007b8890 (OpenGL extensions)
    /// - Original game uses DirectX 8/9 device (IDirect3DDevice8/IDirect3DDevice9)
    /// - Device creation: Original game creates DirectX device with specific parameters (resolution, fullscreen, etc.)
    /// - Rendering: Original game uses fixed-function pipeline (no shaders), immediate mode rendering
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan via MonoGame/Stride)
    /// - Note: Modern graphics APIs use programmable pipelines (shaders), not fixed-function like original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline, immediate mode, no modern features
    /// </remarks>
    public interface IGraphicsDevice : IDisposable
    {
        /// <summary>
        /// Gets the viewport dimensions.
        /// </summary>
        Viewport Viewport { get; }

        /// <summary>
        /// Gets or sets the render target (null for backbuffer).
        /// </summary>
        IRenderTarget RenderTarget { get; set; }

        /// <summary>
        /// Gets or sets the depth-stencil buffer.
        /// </summary>
        IDepthStencilBuffer DepthStencilBuffer { get; set; }

        /// <summary>
        /// Clears the render target with the specified color.
        /// </summary>
        /// <param name="color">Clear color.</param>
        void Clear(Color color);

        /// <summary>
        /// Clears the depth buffer.
        /// </summary>
        /// <param name="depth">Depth value (0.0 to 1.0).</param>
        void ClearDepth(float depth);

        /// <summary>
        /// Clears the stencil buffer.
        /// </summary>
        /// <param name="stencil">Stencil value.</param>
        void ClearStencil(int stencil);

        /// <summary>
        /// Creates a texture from pixel data.
        /// </summary>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <param name="data">Pixel data (RGBA format).</param>
        /// <returns>Created texture.</returns>
        ITexture2D CreateTexture2D(int width, int height, byte[] data);

        /// <summary>
        /// Creates a render target.
        /// </summary>
        /// <param name="width">Render target width.</param>
        /// <param name="height">Render target height.</param>
        /// <param name="hasDepthStencil">Whether to create depth-stencil buffer.</param>
        /// <returns>Created render target.</returns>
        IRenderTarget CreateRenderTarget(int width, int height, bool hasDepthStencil = true);

        /// <summary>
        /// Creates a depth-stencil buffer.
        /// </summary>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        /// <returns>Created depth-stencil buffer.</returns>
        IDepthStencilBuffer CreateDepthStencilBuffer(int width, int height);

        /// <summary>
        /// Creates a vertex buffer.
        /// </summary>
        /// <typeparam name="T">Vertex type.</typeparam>
        /// <param name="data">Vertex data.</param>
        /// <returns>Created vertex buffer.</returns>
        IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : struct;

        /// <summary>
        /// Creates an index buffer.
        /// </summary>
        /// <param name="indices">Index data.</param>
        /// <param name="isShort">Whether indices are 16-bit (true) or 32-bit (false).</param>
        /// <returns>Created index buffer.</returns>
        IIndexBuffer CreateIndexBuffer(int[] indices, bool isShort = true);

        /// <summary>
        /// Creates a sprite batch for 2D rendering.
        /// </summary>
        /// <returns>Created sprite batch.</returns>
        ISpriteBatch CreateSpriteBatch();

        /// <summary>
        /// Gets the native graphics device handle (for advanced operations).
        /// </summary>
        IntPtr NativeHandle { get; }

        // 3D Rendering Methods

        /// <summary>
        /// Sets the vertex buffer for rendering.
        /// </summary>
        /// <param name="vertexBuffer">Vertex buffer to set.</param>
        void SetVertexBuffer(IVertexBuffer vertexBuffer);

        /// <summary>
        /// Sets the index buffer for rendering.
        /// </summary>
        /// <param name="indexBuffer">Index buffer to set.</param>
        void SetIndexBuffer(IIndexBuffer indexBuffer);

        /// <summary>
        /// Draws indexed primitives.
        /// </summary>
        /// <param name="primitiveType">Type of primitives to draw.</param>
        /// <param name="baseVertex">Index of the first vertex to use.</param>
        /// <param name="minVertexIndex">Minimum vertex index.</param>
        /// <param name="numVertices">Number of vertices to use.</param>
        /// <param name="startIndex">Index of the first index to use.</param>
        /// <param name="primitiveCount">Number of primitives to draw.</param>
        void DrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount);

        /// <summary>
        /// Draws primitives.
        /// </summary>
        /// <param name="primitiveType">Type of primitives to draw.</param>
        /// <param name="vertexOffset">Index of the first vertex to use.</param>
        /// <param name="primitiveCount">Number of primitives to draw.</param>
        void DrawPrimitives(PrimitiveType primitiveType, int vertexOffset, int primitiveCount);

        /// <summary>
        /// Sets the rasterizer state.
        /// </summary>
        /// <param name="rasterizerState">Rasterizer state to set.</param>
        void SetRasterizerState(IRasterizerState rasterizerState);

        /// <summary>
        /// Sets the depth-stencil state.
        /// </summary>
        /// <param name="depthStencilState">Depth-stencil state to set.</param>
        void SetDepthStencilState(IDepthStencilState depthStencilState);

        /// <summary>
        /// Sets the blend state.
        /// </summary>
        /// <param name="blendState">Blend state to set.</param>
        void SetBlendState(IBlendState blendState);

        /// <summary>
        /// Sets the sampler state for a texture slot.
        /// </summary>
        /// <param name="index">Texture slot index.</param>
        /// <param name="samplerState">Sampler state to set.</param>
        void SetSamplerState(int index, ISamplerState samplerState);

        /// <summary>
        /// Creates a basic effect for simple 3D rendering.
        /// </summary>
        /// <returns>Created basic effect.</returns>
        IBasicEffect CreateBasicEffect();

        /// <summary>
        /// Creates a default rasterizer state.
        /// </summary>
        /// <returns>Created rasterizer state.</returns>
        IRasterizerState CreateRasterizerState();

        /// <summary>
        /// Creates a default depth-stencil state.
        /// </summary>
        /// <returns>Created depth-stencil state.</returns>
        IDepthStencilState CreateDepthStencilState();

        /// <summary>
        /// Creates a default blend state.
        /// </summary>
        /// <returns>Created blend state.</returns>
        IBlendState CreateBlendState();

        /// <summary>
        /// Creates a default sampler state.
        /// </summary>
        /// <returns>Created sampler state.</returns>
        ISamplerState CreateSamplerState();
    }

    /// <summary>
    /// Primitive type for rendering.
    /// </summary>
    public enum PrimitiveType
    {
        TriangleList,
        TriangleStrip,
        LineList,
        LineStrip,
        PointList
    }

    /// <summary>
    /// Viewport structure.
    /// </summary>
    public struct Viewport
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public float MinDepth;
        public float MaxDepth;

        public Viewport(int x, int y, int width, int height, float minDepth = 0.0f, float maxDepth = 1.0f)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }
    }

    /// <summary>
    /// Color structure (RGBA).
    /// </summary>
    public struct Color : IEquatable<Color>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public Color(float r, float g, float b, float a = 1.0f)
        {
            R = (byte)(r * 255.0f);
            G = (byte)(g * 255.0f);
            B = (byte)(b * 255.0f);
            A = (byte)(a * 255.0f);
        }

        public bool Equals(Color other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            if (obj is Color)
            {
                return Equals((Color)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() ^ G.GetHashCode() ^ B.GetHashCode() ^ A.GetHashCode();
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        public static Color White => new Color(255, 255, 255, 255);
        public static Color Black => new Color(0, 0, 0, 255);
        public static Color Transparent => new Color(0, 0, 0, 0);
        public static Color Red => new Color(255, 0, 0, 255);
        public static Color Green => new Color(0, 255, 0, 255);
        public static Color Blue => new Color(0, 0, 255, 255);
        public static Color Gray => new Color(128, 128, 128, 255);
        public static Color Brown => new Color(139, 69, 19, 255);
        public static Color Orange => new Color(255, 165, 0, 255);
        public static Color Yellow => new Color(255, 255, 0, 255);
        public static Color Cyan => new Color(0, 255, 255, 255);
    }
}

