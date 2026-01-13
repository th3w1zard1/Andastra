using System;
using System.Numerics;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base class for bitmap font implementations across all engines.
    /// Contains common font loading, measurement, and character mapping patterns.
    /// </summary>
    /// <remarks>
    /// Base Bitmap Font System:
    /// - Common patterns: Font texture loading, character mapping, text measurement
    /// - All engines use texture-based fonts with character glyphs in grids
    /// - Common operations: MeasureString, GetCharacter, character coordinate mapping
    /// - Engine-specific: Font format (TPC/TGA/TEX), metric format (TXI/other), coordinate system
    /// 
    /// Inheritance Structure:
    /// - BaseBitmapFont (Runtime.Games.Common) - Common font operations
    ///   - Odyssey: OdysseyBitmapFont : BaseBitmapFont (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraBitmapFont : BaseBitmapFont (nwmain.exe)
    ///   - Eclipse: EclipseBitmapFont : BaseBitmapFont (daorigins.exe, DragonAge2.exe)
    ///   - Infinity: InfinityBitmapFont : BaseBitmapFont (, )
    /// </remarks>
    public abstract class BaseBitmapFont
    {
        /// <summary>
        /// Gets the font texture.
        /// </summary>
        public abstract ITexture2D Texture { get; }

        /// <summary>
        /// Gets the font height in pixels.
        /// </summary>
        public abstract float FontHeight { get; }

        /// <summary>
        /// Gets the font width in pixels (average character width).
        /// </summary>
        public abstract float FontWidth { get; }

        /// <summary>
        /// Gets the baseline height for text alignment.
        /// </summary>
        public abstract float BaselineHeight { get; }

        /// <summary>
        /// Gets the horizontal spacing between characters.
        /// </summary>
        public abstract float SpacingR { get; }

        /// <summary>
        /// Gets the vertical spacing between lines.
        /// </summary>
        public abstract float SpacingB { get; }

        /// <summary>
        /// Gets the texture width in pixels.
        /// </summary>
        public abstract int TextureWidth { get; }

        /// <summary>
        /// Gets the texture height in pixels.
        /// </summary>
        public abstract int TextureHeight { get; }

        /// <summary>
        /// Character glyph information for rendering.
        /// </summary>
        public struct CharacterGlyph
        {
            public int SourceX;
            public int SourceY;
            public int SourceWidth;
            public int SourceHeight;
            public float Width;
            public float Height;
        }

        /// <summary>
        /// Gets character information for a specific character code.
        /// </summary>
        /// <param name="charCode">The character code (0-255).</param>
        /// <returns>Character information, or null if not found.</returns>
        [CanBeNull]
        public abstract CharacterGlyph? GetCharacter(int charCode);

        /// <summary>
        /// Measures the size of text when rendered with this font.
        /// Common implementation that works for all engines.
        /// </summary>
        /// <param name="text">The text to measure.</param>
        /// <returns>The size of the text in pixels.</returns>
        public virtual Graphics.Vector2 MeasureString([CanBeNull] string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Graphics.Vector2.Zero;
            }

            float maxWidth = 0.0f;
            float currentWidth = 0.0f;
            float totalHeight = FontHeight;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    maxWidth = Math.Max(maxWidth, currentWidth);
                    currentWidth = 0.0f;
                    totalHeight += FontHeight + SpacingB;
                }
                else
                {
                    int charCode = (int)c;
                    CharacterGlyph? glyph = GetCharacter(charCode);
                    if (glyph.HasValue)
                    {
                        currentWidth += glyph.Value.Width + SpacingR;
                    }
                    else
                    {
                        // Unknown character - use default width
                        currentWidth += FontWidth + SpacingR;
                    }
                }
            }

            maxWidth = Math.Max(maxWidth, currentWidth);
            return new Graphics.Vector2(maxWidth, totalHeight);
        }
    }
}

