using Stride.Graphics;
using Stride.Core.Mathematics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IFont.
    /// </summary>
    public class StrideFont : IFont
    {
        private readonly SpriteFont _font;

        internal SpriteFont Font => _font;

        public StrideFont(SpriteFont font)
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

