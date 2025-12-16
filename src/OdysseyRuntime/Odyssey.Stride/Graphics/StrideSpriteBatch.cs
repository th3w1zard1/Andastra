using Stride.Graphics;
using Stride.Core.Mathematics;
using BioWareEngines.Graphics;

namespace BioWareEngines.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of ISpriteBatch.
    /// </summary>
    public class StrideSpriteBatch : ISpriteBatch
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly GraphicsDevice _graphicsDevice;
        private bool _isBegun;

        internal SpriteBatch SpriteBatch => _spriteBatch;

        public StrideSpriteBatch(SpriteBatch spriteBatch)
        {
            _spriteBatch = spriteBatch ?? throw new System.ArgumentNullException(nameof(spriteBatch));
            // Store GraphicsDevice reference for accessing ImmediateContext
            // SpriteBatch is created with GraphicsDevice, so we need to get it from the constructor
            // For now, we'll get it from the GraphicsContext when needed
            _graphicsDevice = spriteBatch.GraphicsDevice;
        }

        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null)
        {
            if (_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch.Begin() called while already begun. Call End() first.");
            }

            var strideSortMode = ConvertSortMode(sortMode);
            var strideBlendState = ConvertBlendState(blendState);

            _spriteBatch.Begin(_graphicsDevice.ImmediateContext, strideSortMode, strideBlendState);
            _isBegun = true;
        }

        public void End()
        {
            if (!_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch.End() called without matching Begin().");
            }

            _spriteBatch.End();
            _isBegun = false;
        }

        public void Draw(ITexture2D texture, Vector2 position, Color color)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            _spriteBatch.Draw(strideTexture, new Vector2(position.X, position.Y), strideColor);
        }

        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Color color)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            var strideRect = new RectangleF(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            _spriteBatch.Draw(strideTexture, strideRect, strideColor);
        }

        public void Draw(ITexture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            RectangleF? strideRect = null;
            if (sourceRectangle.HasValue)
            {
                var rect = sourceRectangle.Value;
                strideRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
            }
            _spriteBatch.Draw(strideTexture, new Vector2(position.X, position.Y), strideRect, strideColor);
        }

        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            var strideDestRect = new RectangleF(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            RectangleF? strideSrcRect = null;
            if (sourceRectangle.HasValue)
            {
                var rect = sourceRectangle.Value;
                strideSrcRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
            }
            var strideOrigin = new Vector2(origin.X, origin.Y);
            var strideEffects = ConvertSpriteEffects(effects);
            _spriteBatch.Draw(strideTexture, strideDestRect, strideSrcRect, strideColor, rotation, strideOrigin, strideEffects, layerDepth);
        }

        public void DrawString(IFont font, string text, Vector2 position, Color color)
        {
            EnsureBegun();
            var strideFont = GetStrideFont(font);
            var strideColor = new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            _spriteBatch.DrawString(strideFont, text, new Vector2(position.X, position.Y), strideColor);
        }

        private void EnsureBegun()
        {
            if (!_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch operations must be called between Begin() and End().");
            }
        }

        private Texture2D GetStrideTexture(ITexture2D texture)
        {
            if (texture is StrideTexture2D strideTexture)
            {
                return strideTexture.Texture;
            }
            throw new System.ArgumentException("Texture must be a StrideTexture2D", nameof(texture));
        }

        private SpriteFont GetStrideFont(IFont font)
        {
            if (font is StrideFont strideFont)
            {
                return strideFont.Font;
            }
            throw new System.ArgumentException("Font must be a StrideFont", nameof(font));
        }

        private SpriteSortMode ConvertSortMode(SpriteSortMode sortMode)
        {
            // Stride uses the same enum values, so we can cast directly
            return (Stride.Graphics.SpriteSortMode)sortMode;
        }

        private BlendStateDescription ConvertBlendState(BlendState blendState)
        {
            if (blendState == null)
            {
                return BlendStateDescription.Default();
            }

            if (blendState.Additive)
            {
                return BlendStateDescription.Additive();
            }

            if (blendState.AlphaBlend)
            {
                return BlendStateDescription.AlphaBlend();
            }

            return BlendStateDescription.Default();
        }

        private SpriteEffects ConvertSpriteEffects(SpriteEffects effects)
        {
            Stride.Graphics.SpriteEffects result = Stride.Graphics.SpriteEffects.None;
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
            {
                result |= Stride.Graphics.SpriteEffects.FlipHorizontally;
            }
            if ((effects & SpriteEffects.FlipVertically) != 0)
            {
                result |= Stride.Graphics.SpriteEffects.FlipVertically;
            }
            return result;
        }

        public void Dispose()
        {
            _spriteBatch?.Dispose();
        }
    }
}

