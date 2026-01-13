using System;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Minimal graphics device interface for movie playback.
    /// Allows Core to render movies without depending on Graphics layer.
    /// </summary>
    /// <remarks>
    /// Movie Graphics Device Interface:
    /// - Based on swkotor.exe/swkotor2.exe: Movie rendering system
    /// - Movie playback function: 0x00404c80 @ 0x00404c80 (main playback loop)
    /// - Movie initialization: 0x004053e0 @ 0x004053e0 (creates movie window, opens BIK file)
    /// - Bink API usage: BinkBufferBlit @ 0x00404cc5 (blits buffer to screen)
    /// - Original implementation: Movies render fullscreen using DirectX device
    /// - This interface: Minimal contract for Core to render movie frames
    /// - Graphics layer implementations (IGraphicsDevice) should implement this interface
    /// </remarks>
    public interface IMovieGraphicsDevice
    {
        /// <summary>
        /// Gets the viewport dimensions.
        /// </summary>
        MovieViewport Viewport { get; }

        /// <summary>
        /// Gets the native graphics device handle (for Bink API).
        /// </summary>
        IntPtr NativeHandle { get; }

        /// <summary>
        /// Clears the render target with the specified color.
        /// </summary>
        /// <param name="color">Clear color (RGBA).</param>
        void Clear(MovieColor color);

        /// <summary>
        /// Creates a texture from pixel data.
        /// </summary>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <param name="data">Pixel data (RGBA format, can be null for empty texture).</param>
        /// <returns>Created texture.</returns>
        IMovieTexture2D CreateTexture2D(int width, int height, byte[] data);

        /// <summary>
        /// Creates a sprite batch for 2D rendering.
        /// </summary>
        /// <returns>Created sprite batch.</returns>
        IMovieSpriteBatch CreateSpriteBatch();
    }

    /// <summary>
    /// Viewport structure for movie rendering.
    /// </summary>
    public struct MovieViewport
    {
        public int Width;
        public int Height;

        public MovieViewport(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Color structure (RGBA) for movie rendering.
    /// </summary>
    public struct MovieColor
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public MovieColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    /// <summary>
    /// Texture interface for movie rendering.
    /// </summary>
    public interface IMovieTexture2D : IDisposable
    {
        /// <summary>
        /// Updates texture data.
        /// </summary>
        /// <param name="data">Pixel data (RGBA format).</param>
        void SetData(byte[] data);
    }

    /// <summary>
    /// Sprite batch interface for movie rendering.
    /// </summary>
    public interface IMovieSpriteBatch : IDisposable
    {
        /// <summary>
        /// Begins sprite batch rendering.
        /// </summary>
        void Begin();

        /// <summary>
        /// Ends sprite batch rendering.
        /// </summary>
        void End();

        /// <summary>
        /// Draws a texture with destination rectangle.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination rectangle.</param>
        /// <param name="color">Tint color.</param>
        void Draw(IMovieTexture2D texture, MovieRectangle destinationRectangle, MovieColor color);
    }

    /// <summary>
    /// Rectangle structure for movie rendering.
    /// </summary>
    public struct MovieRectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public MovieRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}

