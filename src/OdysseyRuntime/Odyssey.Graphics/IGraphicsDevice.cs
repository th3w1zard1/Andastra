using System;
using System.Numerics;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Graphics device abstraction for rendering operations.
    /// Provides unified access to graphics hardware across MonoGame and Stride.
    /// </summary>
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
    public struct Color
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

        public static Color White => new Color(255, 255, 255, 255);
        public static Color Black => new Color(0, 0, 0, 255);
        public static Color Transparent => new Color(0, 0, 0, 0);
        public static Color Red => new Color(255, 0, 0, 255);
        public static Color Green => new Color(0, 255, 0, 255);
        public static Color Blue => new Color(0, 0, 255, 255);
    }
}

