using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Graphics.Adapters
{
    /// <summary>
    /// Adapter that makes IGraphicsDevice implement IMovieGraphicsDevice.
    /// Allows Core to use Graphics layer devices without depending on Graphics.
    /// </summary>
    public class MovieGraphicsDeviceAdapter : IMovieGraphicsDevice
    {
        private readonly IGraphicsDevice _device;

        /// <summary>
        /// Initializes a new instance of the MovieGraphicsDeviceAdapter class.
        /// </summary>
        /// <param name="device">The graphics device to adapt.</param>
        public MovieGraphicsDeviceAdapter(IGraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException("device");
        }

        /// <summary>
        /// Gets the viewport dimensions.
        /// </summary>
        public MovieViewport Viewport
        {
            get
            {
                Viewport vp = _device.Viewport;
                return new MovieViewport(vp.Width, vp.Height);
            }
        }

        /// <summary>
        /// Gets the native graphics device handle (for Bink API).
        /// </summary>
        public IntPtr NativeHandle
        {
            get { return _device.NativeHandle; }
        }

        /// <summary>
        /// Clears the render target with the specified color.
        /// </summary>
        public void Clear(MovieColor color)
        {
            Color graphicsColor = new Color(color.R, color.G, color.B, color.A);
            _device.Clear(graphicsColor);
        }

        /// <summary>
        /// Creates a texture from pixel data.
        /// </summary>
        public IMovieTexture2D CreateTexture2D(int width, int height, byte[] data)
        {
            ITexture2D texture = _device.CreateTexture2D(width, height, data);
            return new MovieTexture2DAdapter(texture);
        }

        /// <summary>
        /// Creates a sprite batch for 2D rendering.
        /// </summary>
        public IMovieSpriteBatch CreateSpriteBatch()
        {
            ISpriteBatch spriteBatch = _device.CreateSpriteBatch();
            return new MovieSpriteBatchAdapter(spriteBatch);
        }
    }

    /// <summary>
    /// Adapter that makes ITexture2D implement IMovieTexture2D.
    /// </summary>
    internal class MovieTexture2DAdapter : IMovieTexture2D
    {
        internal readonly ITexture2D Texture;

        /// <summary>
        /// Initializes a new instance of the MovieTexture2DAdapter class.
        /// </summary>
        /// <param name="texture">The texture to adapt.</param>
        public MovieTexture2DAdapter(ITexture2D texture)
        {
            Texture = texture ?? throw new ArgumentNullException("texture");
        }

        /// <summary>
        /// Updates texture data.
        /// </summary>
        public void SetData(byte[] data)
        {
            Texture.SetData(data);
        }

        /// <summary>
        /// Disposes the texture.
        /// </summary>
        public void Dispose()
        {
            Texture.Dispose();
        }
    }

    /// <summary>
    /// Adapter that makes ISpriteBatch implement IMovieSpriteBatch.
    /// </summary>
    internal class MovieSpriteBatchAdapter : IMovieSpriteBatch
    {
        private readonly ISpriteBatch _spriteBatch;

        /// <summary>
        /// Initializes a new instance of the MovieSpriteBatchAdapter class.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to adapt.</param>
        public MovieSpriteBatchAdapter(ISpriteBatch spriteBatch)
        {
            _spriteBatch = spriteBatch ?? throw new ArgumentNullException("spriteBatch");
        }

        /// <summary>
        /// Begins sprite batch rendering.
        /// </summary>
        public void Begin()
        {
            _spriteBatch.Begin();
        }

        /// <summary>
        /// Ends sprite batch rendering.
        /// </summary>
        public void End()
        {
            _spriteBatch.End();
        }

        /// <summary>
        /// Draws a texture with destination rectangle.
        /// </summary>
        public void Draw(IMovieTexture2D texture, MovieRectangle destinationRectangle, MovieColor color)
        {
            // Convert Core types to Graphics types
            ITexture2D graphicsTexture = GetGraphicsTexture(texture);
            Rectangle graphicsRect = new Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            Color graphicsColor = new Color(color.R, color.G, color.B, color.A);

            _spriteBatch.Draw(graphicsTexture, graphicsRect, graphicsColor);
        }

        /// <summary>
        /// Disposes the sprite batch.
        /// </summary>
        public void Dispose()
        {
            _spriteBatch.Dispose();
        }

        /// <summary>
        /// Extracts the underlying ITexture2D from an IMovieTexture2D adapter.
        /// </summary>
        private ITexture2D GetGraphicsTexture(IMovieTexture2D movieTexture)
        {
            // If it's an adapter, extract the underlying texture
            if (movieTexture is MovieTexture2DAdapter adapter)
            {
                return adapter.Texture;
            }

            // If not an adapter, we can't convert it
            throw new ArgumentException("Texture must be created by MovieGraphicsDeviceAdapter", "texture");
        }
    }
}

