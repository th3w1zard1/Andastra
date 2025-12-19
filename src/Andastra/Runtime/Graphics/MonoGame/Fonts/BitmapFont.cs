using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Games.Odyssey.Fonts;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Fonts
{
    /// <summary>
    /// Compatibility wrapper for BitmapFont. Use OdysseyBitmapFont instead.
    /// This class is maintained for backward compatibility.
    /// </summary>
    /// <remarks>
    /// This class is a compatibility wrapper around OdysseyBitmapFont.
    /// New code should use OdysseyBitmapFont directly.
    /// </remarks>
    [Obsolete("Use OdysseyBitmapFont instead. This class is maintained for backward compatibility only.")]
    public class BitmapFont
    {
        private readonly OdysseyBitmapFont _wrappedFont;

        /// <summary>
        /// Gets the font texture.
        /// </summary>
        public Texture2D Texture => _wrappedFont?.MonoGameTexture;

        /// <summary>
        /// Gets the font height in pixels.
        /// </summary>
        public float FontHeight => _wrappedFont?.FontHeight ?? 0;

        /// <summary>
        /// Gets the font width in pixels (average character width).
        /// </summary>
        public float FontWidth => _wrappedFont?.FontWidth ?? 0;

        /// <summary>
        /// Gets the baseline height for text alignment.
        /// </summary>
        public float BaselineHeight => _wrappedFont?.BaselineHeight ?? 0;

        /// <summary>
        /// Gets the horizontal spacing between characters.
        /// </summary>
        public float SpacingR => _wrappedFont?.SpacingR ?? 0;

        /// <summary>
        /// Gets the vertical spacing between lines.
        /// </summary>
        public float SpacingB => _wrappedFont?.SpacingB ?? 0;

        /// <summary>
        /// Gets the texture width in pixels.
        /// </summary>
        public int TextureWidth => _wrappedFont?.TextureWidth ?? 0;

        /// <summary>
        /// Gets the texture height in pixels.
        /// </summary>
        public int TextureHeight => _wrappedFont?.TextureHeight ?? 0;

        /// <summary>
        /// Private constructor for creating a BitmapFont instance.
        /// </summary>
        private BitmapFont(OdysseyBitmapFont wrappedFont)
        {
            _wrappedFont = wrappedFont;
        }

        /// <summary>
        /// Loads a bitmap font from a ResRef.
        /// </summary>
        /// <param name="fontResRef">The font resource reference (without extension).</param>
        /// <param name="installation">The game installation for resource lookup.</param>
        /// <param name="graphicsDevice">The graphics device for texture creation.</param>
        /// <returns>The loaded bitmap font, or null if loading failed.</returns>
        [CanBeNull]
        public static BitmapFont Load([NotNull] string fontResRef, [NotNull] Installation installation, [NotNull] GraphicsDevice graphicsDevice)
        {
            if (string.IsNullOrEmpty(fontResRef))
            {
                Console.WriteLine("[BitmapFont] ERROR: Font ResRef cannot be null or empty");
                return null;
            }
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            try
            {
                // Delegate to OdysseyBitmapFont and wrap it
                OdysseyBitmapFont odysseyFont = OdysseyBitmapFont.Load(fontResRef, installation, graphicsDevice);
                if (odysseyFont == null)
                {
                    return null;
                }
                return new BitmapFont(odysseyFont);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitmapFont] ERROR: Exception loading font {fontResRef}: {ex.Message}");
                Console.WriteLine($"[BitmapFont] Stack trace: {ex.StackTrace}");
                return null;
            }
        }


        /// <summary>
        /// Gets character information for a specific character code.
        /// </summary>
        /// <param name="charCode">The character code (0-255).</param>
        /// <returns>Character information, or null if not found.</returns>
        [CanBeNull]
        public CharacterGlyph? GetCharacter(int charCode)
        {
            if (_wrappedFont == null)
            {
                return null;
            }

            var glyph = _wrappedFont.GetCharacter(charCode);
            if (glyph.HasValue)
            {
                var g = glyph.Value;
                return new CharacterGlyph
                {
                    SourceRect = new Rectangle(g.SourceX, g.SourceY, g.SourceWidth, g.SourceHeight),
                    Width = g.Width,
                    Height = g.Height
                };
            }
            return null;
        }

        /// <summary>
        /// Measures the size of text when rendered with this font.
        /// </summary>
        /// <param name="text">The text to measure.</param>
        /// <returns>The size of the text in pixels.</returns>
        public Vector2 MeasureString([CanBeNull] string text)
        {
            if (_wrappedFont == null)
            {
                return Vector2.Zero;
            }
            return _wrappedFont.MeasureString(text);
        }

        /// <summary>
        /// Character glyph information for rendering.
        /// </summary>
        public struct CharacterGlyph
        {
            public Rectangle SourceRect;
            public float Width;
            public float Height;
        }
    }
}

