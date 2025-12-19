using Stride.Graphics;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
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

        public Andastra.Runtime.Graphics.Vector2 MeasureString(string text)
        {
            if (text == null)
            {
                return Andastra.Runtime.Graphics.Vector2.Zero;
            }

            var size = _font.MeasureString(text);
            return new Andastra.Runtime.Graphics.Vector2(size.X, size.Y);
        }

        public float LineSpacing => _font.LineSpacing;
    }
}

