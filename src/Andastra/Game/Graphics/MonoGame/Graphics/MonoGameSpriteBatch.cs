using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.MonoGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
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

        public void Begin(Andastra.Runtime.Graphics.SpriteSortMode sortMode = Andastra.Runtime.Graphics.SpriteSortMode.Deferred, Andastra.Runtime.Graphics.BlendState blendState = null)
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

        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Vector2 position, Andastra.Runtime.Graphics.Color color)
        {
            EnsureBegun();
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            _spriteBatch.Draw(mgTexture, new Microsoft.Xna.Framework.Vector2(position.X, position.Y), mgColor);
        }

        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Rectangle destinationRectangle, Andastra.Runtime.Graphics.Color color)
        {
            EnsureBegun();
            var mgTexture = GetMonoGameTexture(texture);
            var mgColor = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
            var mgRect = new Microsoft.Xna.Framework.Rectangle(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height);
            _spriteBatch.Draw(mgTexture, mgRect, mgColor);
        }

        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Vector2 position, Andastra.Runtime.Graphics.Rectangle? sourceRectangle, Andastra.Runtime.Graphics.Color color)
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

        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Rectangle destinationRectangle, Andastra.Runtime.Graphics.Rectangle? sourceRectangle, Andastra.Runtime.Graphics.Color color, float rotation, Andastra.Runtime.Graphics.Vector2 origin, Andastra.Runtime.Graphics.SpriteEffects effects, float layerDepth)
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

        public void DrawString(IFont font, string text, Andastra.Runtime.Graphics.Vector2 position, Andastra.Runtime.Graphics.Color color)
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

        private Microsoft.Xna.Framework.Graphics.SpriteSortMode ConvertSortMode(Andastra.Runtime.Graphics.SpriteSortMode sortMode)
        {
            switch (sortMode)
            {
                case Andastra.Runtime.Graphics.SpriteSortMode.Deferred:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred;
                case Andastra.Runtime.Graphics.SpriteSortMode.Immediate:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate;
                case Andastra.Runtime.Graphics.SpriteSortMode.Texture:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Texture;
                case Andastra.Runtime.Graphics.SpriteSortMode.BackToFront:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.BackToFront;
                case Andastra.Runtime.Graphics.SpriteSortMode.FrontToBack:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.FrontToBack;
                default:
                    return Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred;
            }
        }

        private Microsoft.Xna.Framework.Graphics.BlendState ConvertBlendState(Andastra.Runtime.Graphics.BlendState blendState)
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

        private Microsoft.Xna.Framework.Graphics.SpriteEffects ConvertSpriteEffects(Andastra.Runtime.Graphics.SpriteEffects effects)
        {
            Microsoft.Xna.Framework.Graphics.SpriteEffects result = Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
            if ((effects & Andastra.Runtime.Graphics.SpriteEffects.FlipHorizontally) != 0)
            {
                result |= Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally;
            }
            if ((effects & Andastra.Runtime.Graphics.SpriteEffects.FlipVertically) != 0)
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

