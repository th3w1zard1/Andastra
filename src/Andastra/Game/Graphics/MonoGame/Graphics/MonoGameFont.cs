using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IFont.
    /// </summary>
    public class MonoGameFont : IFont
    {
        private readonly SpriteFont _font;

        internal SpriteFont Font => _font;

        public MonoGameFont(SpriteFont font)
        {
            _font = font ?? throw new System.ArgumentNullException(nameof(font));
        }

        public Vector2 MeasureString(string text)
        {
            if (text == null)
            {
                return Vector2.Zero;
            }

            var size = _font.MeasureString(text);
            return new Vector2(size.X, size.Y);
        }

        public float LineSpacing => _font.LineSpacing;
    }
}

