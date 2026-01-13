using Andastra.Runtime.Graphics;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
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

        public Runtime.Graphics.Vector2 MeasureString(string text)
        {
            if (text == null)
            {
                return Runtime.Graphics.Vector2.Zero;
            }

            var size = _font.MeasureString(text);
            return new Runtime.Graphics.Vector2(size.X, size.Y);
        }

        public float LineSpacing
        {
            get
            {
                // Stride SpriteFont doesn't have a direct LineSpacing property
                // Calculate line spacing based on font size (typical line spacing is 1.2x the font size)
                // For Stride, we can use a default multiplier or calculate from character height
                var testSize = _font.MeasureString("Ag");
                return testSize.Y * 1.2f; // Approximate line spacing as 1.2x character height
            }
        }
    }
}

