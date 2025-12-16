using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of ISpriteBatch.
    /// </summary>
    public class MonoGameSpriteBatch : ISpriteBatch
    {
        private readonly SpriteBatch _spriteBatch;
        private bool _isBegun;

        internal SpriteBatch SpriteBatch => _spriteBatch;

        public MonoGameSpriteBatch(SpriteBatch spriteBatch)
        {
            _spriteBatch = spriteBatch ?? throw new System.ArgumentNullException(nameof(spriteBatch));
        }

        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null)
        {
            if (_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch.Begin() called while already begun. Call End() first.");
            }

            var mgSortMode = ConvertSortMode(sortMode);
            var mgBlendState = ConvertBlendState(blendState);

            _spriteBatch.Begin(mgSortMode, mgBlendState);
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
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            _spriteBatch.Draw(mgTexture, new Microsoft.Xna.Framework.Vector2(position.X, position.Y), mgColor);
        }

        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Color color)
        {
            EnsureBegun();
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            var mgRect = new Microsoft.Xna.Framework.Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            _spriteBatch.Draw(mgTexture, mgRect, mgColor);
        }

        public void Draw(ITexture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            EnsureBegun();
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            Microsoft.Xna.Framework.Rectangle? mgRect = null;
            if (sourceRectangle.HasValue)
            {
                var rect = sourceRectangle.Value;
                mgRect = new Microsoft.Xna.Framework.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            }
            _spriteBatch.Draw(mgTexture, new Microsoft.Xna.Framework.Vector2(position.X, position.Y), mgRect, mgColor);
        }

        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            EnsureBegun();
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            var mgDestRect = new Microsoft.Xna.Framework.Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            Microsoft.Xna.Framework.Rectangle? mgSrcRect = null;
            if (sourceRectangle.HasValue)
            {
                var rect = sourceRectangle.Value;
                mgSrcRect = new Microsoft.Xna.Framework.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            }
            var mgOrigin = new Microsoft.Xna.Framework.Vector2(origin.X, origin.Y);
            var mgEffects = ConvertSpriteEffects(effects);
            _spriteBatch.Draw(mgTexture, mgDestRect, mgSrcRect, mgColor, rotation, mgOrigin, mgEffects, layerDepth);
        }

        public void DrawString(IFont font, string text, Vector2 position, Color color)
        {
            EnsureBegun();
            var mgFont = GetMonoGameFont(font);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            _spriteBatch.DrawString(mgFont, text, new Microsoft.Xna.Framework.Vector2(position.X, position.Y), mgColor);
        }

        private void EnsureBegun()
        {
            if (!_isBegun)
            {
                throw new System.InvalidOperationException("SpriteBatch operations must be called between Begin() and End().");
            }
        }

        private Microsoft.Xna.Framework.Graphics.Texture2D GetMonoGameTexture(ITexture2D texture)
        {
            if (texture is MonoGameTexture2D mgTexture)
            {
                return mgTexture.Texture;
            }
            throw new System.ArgumentException("Texture must be a MonoGameTexture2D", nameof(texture));
        }

        private SpriteFont GetMonoGameFont(IFont font)
        {
            if (font is MonoGameFont mgFont)
            {
                return mgFont.Font;
            }
            throw new System.ArgumentException("Font must be a MonoGameFont", nameof(font));
        }

        private Microsoft.Xna.Framework.Graphics.SpriteSortMode ConvertSortMode(SpriteSortMode sortMode)
        {
            switch (sortMode)
            {
                case SpriteSortMode.Deferred:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred;
                case SpriteSortMode.Immediate:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate;
                case SpriteSortMode.Texture:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Texture;
                case SpriteSortMode.BackToFront:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.BackToFront;
                case SpriteSortMode.FrontToBack:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.FrontToBack;
                default:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred;
            }
        }

        private Microsoft.Xna.Framework.Graphics.BlendState ConvertBlendState(BlendState blendState)
        {
            if (blendState == null)
            {
                return Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend;
            }

            if (blendState.Additive)
            {
                return Microsoft.Xna.Framework.Graphics.BlendState.Additive;
            }

            if (blendState.AlphaBlend)
            {
                return Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend;
            }

            return Microsoft.Xna.Framework.Graphics.BlendState.Opaque;
        }

        private Microsoft.Xna.Framework.Graphics.SpriteEffects ConvertSpriteEffects(SpriteEffects effects)
        {
            Microsoft.Xna.Framework.Graphics.SpriteEffects result = Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
            {
                result |= Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally;
            }
            if ((effects & SpriteEffects.FlipVertically) != 0)
            {
                result |= Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically;
            }
            return result;
        }

        public void Dispose()
        {
            _spriteBatch?.Dispose();
        }
    }
}

