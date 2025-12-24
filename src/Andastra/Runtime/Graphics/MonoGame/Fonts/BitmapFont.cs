using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameTexture2D = Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameTexture2D;

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
        private readonly BaseBitmapFont _wrappedFont;

        /// <summary>
        /// Gets the font texture.
        /// </summary>
        public Texture2D Texture
        {
            get
            {
                if (_wrappedFont?.Texture is MonoGameTexture2D mgTexture)
                {
                    return mgTexture.Texture;
                }
                return null;
            }
        }

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
        private BitmapFont(BaseBitmapFont wrappedFont)
        {
            _wrappedFont = wrappedFont;
        }

        /// <summary>
        /// Loads a bitmap font from a ResRef.
        /// Uses reflection to avoid circular dependency with OdysseyBitmapFont.
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
                // Use reflection to call OdysseyBitmapFont.Load to avoid circular dependency
                System.Type odysseyFontType = System.Type.GetType("Andastra.Runtime.Games.Odyssey.Fonts.OdysseyBitmapFont, Andastra.Runtime.Games.Odyssey");
                if (odysseyFontType == null)
                {
                    Console.WriteLine("[BitmapFont] ERROR: Could not find OdysseyBitmapFont type. Please use OdysseyBitmapFont directly.");
                    return null;
                }

                System.Reflection.MethodInfo loadMethod = odysseyFontType.GetMethod("Load", new System.Type[] { typeof(string), typeof(Installation), typeof(GraphicsDevice) });
                if (loadMethod == null)
                {
                    Console.WriteLine("[BitmapFont] ERROR: Could not find OdysseyBitmapFont.Load method. Please use OdysseyBitmapFont directly.");
                    return null;
                }

                object odysseyFont = loadMethod.Invoke(null, new object[] { fontResRef, installation, graphicsDevice });
                if (odysseyFont == null)
                {
                    return null;
                }

                if (odysseyFont is BaseBitmapFont baseFont)
                {
                    return new BitmapFont(baseFont);
                }

                Console.WriteLine("[BitmapFont] ERROR: OdysseyBitmapFont.Load did not return a BaseBitmapFont. Please use OdysseyBitmapFont directly.");
                return null;
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
        public Microsoft.Xna.Framework.Vector2 MeasureString([CanBeNull] string text)
        {
            if (_wrappedFont == null)
            {
                return Microsoft.Xna.Framework.Vector2.Zero;
            }
            var size = _wrappedFont.MeasureString(text);
            return new Microsoft.Xna.Framework.Vector2(size.X, size.Y);
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

