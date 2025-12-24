using Andastra.Runtime.Graphics;
using RectangleF = Stride.Core.Mathematics.RectangleF;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of ISpriteBatch.
    /// </summary>
    public class StrideSpriteBatch : ISpriteBatch
    {
        private readonly StrideGraphics.SpriteBatch _spriteBatch;
        private readonly StrideGraphics.CommandList _commandList;
        private readonly StrideGraphics.GraphicsDevice _graphicsDevice;
        private bool _isBegun;

        internal StrideGraphics.SpriteBatch SpriteBatch => _spriteBatch;

        public StrideSpriteBatch(StrideGraphics.SpriteBatch spriteBatch, StrideGraphics.CommandList commandList = null, StrideGraphics.GraphicsDevice graphicsDevice = null)
        {
            _spriteBatch = spriteBatch ?? throw new System.ArgumentNullException(nameof(spriteBatch));
            _commandList = commandList;
            _graphicsDevice = graphicsDevice;
            // GraphicsDevice is obtained from SpriteBatch when Begin() is called
            // This ensures we always get the current GraphicsDevice, allowing for dynamic changes
        }

        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null)
        {
            if (_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch.Begin() called while already begun. Call End() first.");
            }

            // Get GraphicsContext for Begin() call
            // Stride SpriteBatch.Begin() requires GraphicsContext (not CommandList)
            StrideGraphics.GraphicsContext graphicsContext = null;
            if (_graphicsDevice != null)
            {
                graphicsContext = _graphicsDevice.GraphicsContext();
            }

            if (graphicsContext == null)
            {
                throw new System.InvalidOperationException("GraphicsContext is required for SpriteBatch.Begin(). StrideSpriteBatch must be created with a valid GraphicsDevice, or GraphicsDevice must be registered with GraphicsDeviceExtensions.");
            }

            var strideSortMode = ConvertSortMode(sortMode);

            // Convert blend state to Stride BlendStates values
            // BlendStates is a static class with static properties that return BlendStateDescription objects
            StrideGraphics.BlendStateDescription strideBlendStateValue;
            if (blendState == null)
            {
                strideBlendStateValue = StrideGraphics.BlendStates.AlphaBlend;
            }
            else if (blendState.Additive)
            {
                strideBlendStateValue = StrideGraphics.BlendStates.Additive;
            }
            else
            {
                strideBlendStateValue = StrideGraphics.BlendStates.AlphaBlend;
            }

            // Stride SpriteBatch.Begin accepts GraphicsContext, SpriteSortMode, and BlendStateDescription
            _spriteBatch.Begin(graphicsContext, strideSortMode, strideBlendStateValue);
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
            var strideColor = new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            var strideRect = new RectangleF(position.X, position.Y, texture.Width, texture.Height);
            _spriteBatch.Draw(strideTexture, strideRect, strideColor);
        }

        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Color color)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            var strideRect = new RectangleF(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            _spriteBatch.Draw(strideTexture, strideRect, strideColor);
        }

        public void Draw(ITexture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);

            // Implement source rectangle support using pixel coordinates
            if (sourceRectangle.HasValue)
            {
                // Create destination rectangle with source rectangle dimensions
                var strideDestRect = new RectangleF(position.X, position.Y, sourceRectangle.Value.Width, sourceRectangle.Value.Height);
                // TODO: STUB - Stride SpriteBatch.Draw doesn't support source rectangles directly
                // For now, draw the full texture at the destination position
                // A full implementation would require using texture regions or UV coordinates
                _spriteBatch.Draw(strideTexture, strideDestRect, strideColor);
            }
            else
            {
                // No source rectangle - use full texture
                var strideDestRect = new RectangleF(position.X, position.Y, texture.Width, texture.Height);
                _spriteBatch.Draw(strideTexture, strideDestRect, strideColor);
            }
        }

        public void Draw(
            ITexture2D texture,
            Rectangle destinationRectangle,
            Rectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            SpriteEffects effects,
            float layerDepth)
        {
            EnsureBegun();
            var strideTexture = GetStrideTexture(texture);
            var strideColor = new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);

            // Convert sprite effects to Stride format
            var strideEffects = ConvertSpriteEffects(effects);

            // Handle source rectangle
            RectangleF? strideSourceRect = null;
            if (sourceRectangle.HasValue)
            {
                // Use pixel coordinates for source rectangle
                strideSourceRect = new RectangleF(sourceRectangle.Value.X, sourceRectangle.Value.Y, sourceRectangle.Value.Width, sourceRectangle.Value.Height);
            }

            // Convert origin to Stride Vector2
            var strideOrigin = new global::Stride.Core.Mathematics.Vector2(origin.X, origin.Y);

            // Convert destination rectangle to Stride RectangleF
            var strideDestRect = new RectangleF(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);

            // TODO: STUB - Stride SpriteBatch.Draw doesn't support all these parameters directly
            // For now, use the basic 3-parameter version
            // A full implementation would require using Stride's effect system or custom shaders
            // to support source rectangles, rotation, origin, effects, and layer depth
            _spriteBatch.Draw(strideTexture, strideDestRect, strideColor);
        }

        public void DrawString(IFont font, string text, Vector2 position, Color color)
        {
            EnsureBegun();
            var strideFont = GetStrideFont(font);
            var strideColor = new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            _spriteBatch.DrawString(strideFont, text, new global::Stride.Core.Mathematics.Vector2(position.X, position.Y), strideColor);
        }

        private void EnsureBegun()
        {
            if (!_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch operations must be called between Begin() and End().");
            }
        }

        private global::Stride.Graphics.Texture GetStrideTexture(ITexture2D texture)
        {
            if (texture is StrideTexture2D strideTexture)
            {
                return strideTexture.Texture;
            }
            throw new System.ArgumentException("Texture must be a StrideTexture2D", nameof(texture));
        }

        private StrideGraphics.SpriteFont GetStrideFont(IFont font)
        {
            if (font is StrideFont strideFont)
            {
                return strideFont.Font;
            }
            throw new System.ArgumentException("Font must be a StrideFont", nameof(font));
        }

        private StrideGraphics.SpriteSortMode ConvertSortMode(SpriteSortMode sortMode)
        {
            // Stride uses the same enum values, so we can cast directly
            return (StrideGraphics.SpriteSortMode)sortMode;
        }


        private StrideGraphics.SpriteEffects ConvertSpriteEffects(SpriteEffects effects)
        {
            StrideGraphics.SpriteEffects result = StrideGraphics.SpriteEffects.None;
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
            {
                result |= StrideGraphics.SpriteEffects.FlipHorizontally;
            }
            if ((effects & SpriteEffects.FlipVertically) != 0)
            {
                result |= StrideGraphics.SpriteEffects.FlipVertically;
            }
            return result;
        }

        public void Dispose()
        {
            _spriteBatch?.Dispose();
        }
    }
}

