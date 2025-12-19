using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.Fonts
{
    /// <summary>
    /// Aurora engine (Neverwinter Nights) bitmap font implementation.
    /// Loads texture-based fonts from TGA files with font metric data.
    /// </summary>
    /// <remarks>
    /// Aurora Bitmap Font (nwmain.exe):
    /// - Font textures: TGA files containing character glyphs
    /// - Font metrics: Font data embedded in TGA or separate font definition files
    /// - Character rendering: Uses texture sampling with character coordinates
    /// - Text alignment: Supports Aurora alignment modes
    /// - Font loading: Loads font texture and metrics from game installation
    /// - Character mapping: Maps ASCII characters to texture coordinates
    /// 
    /// Ghidra Reverse Engineering Analysis Required:
    /// - nwmain.exe: Font loading functions (needs Ghidra address verification)
    /// - nwmain.exe: Font rendering functions (needs Ghidra address verification)
    /// - Font format: Needs Ghidra analysis to determine exact format and metric structure
    /// 
    /// Original implementation: Uses DirectX sprite rendering for text
    /// </remarks>
    public class AuroraBitmapFont : BaseBitmapFont
    {
        private readonly ITexture2D _texture;
        private readonly Dictionary<int, CharacterInfo> _characterMap;
        private readonly float _fontHeight;
        private readonly float _fontWidth;
        private readonly float _baselineHeight;
        private readonly float _spacingR;
        private readonly float _spacingB;
        private readonly int _textureWidth;
        private readonly int _textureHeight;

        /// <summary>
        /// Gets the font texture.
        /// </summary>
        public override ITexture2D Texture => _texture;

        /// <summary>
        /// Gets the font height in pixels.
        /// </summary>
        public override float FontHeight => _fontHeight;

        /// <summary>
        /// Gets the font width in pixels (average character width).
        /// </summary>
        public override float FontWidth => _fontWidth;

        /// <summary>
        /// Gets the baseline height for text alignment.
        /// </summary>
        public override float BaselineHeight => _baselineHeight;

        /// <summary>
        /// Gets the horizontal spacing between characters.
        /// </summary>
        public override float SpacingR => _spacingR;

        /// <summary>
        /// Gets the vertical spacing between lines.
        /// </summary>
        public override float SpacingB => _spacingB;

        /// <summary>
        /// Gets the texture width in pixels.
        /// </summary>
        public override int TextureWidth => _textureWidth;

        /// <summary>
        /// Gets the texture height in pixels.
        /// </summary>
        public override int TextureHeight => _textureHeight;

        /// <summary>
        /// Internal structure for character glyph information.
        /// </summary>
        private class CharacterInfo
        {
            public int SourceX { get; set; }
            public int SourceY { get; set; }
            public int SourceWidth { get; set; }
            public int SourceHeight { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
        }

        /// <summary>
        /// Loads a bitmap font from a ResRef.
        /// </summary>
        /// <param name="fontResRef">The font resource reference (without extension).</param>
        /// <param name="installation">The game installation for resource lookup.</param>
        /// <param name="graphicsDevice">The graphics device for texture creation.</param>
        /// <returns>The loaded bitmap font, or null if loading failed.</returns>
        [CanBeNull]
        public static AuroraBitmapFont Load([NotNull] string fontResRef, [NotNull] Installation installation, [NotNull] IGraphicsDevice graphicsDevice)
        {
            if (string.IsNullOrEmpty(fontResRef))
            {
                Console.WriteLine("[AuroraBitmapFont] ERROR: Font ResRef cannot be null or empty");
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
                // TODO: STUB - Implement Aurora font loading
                // Aurora fonts use TGA format, need Ghidra analysis to determine exact format
                // Based on nwmain.exe font loading functions (needs Ghidra address verification)
                
                Console.WriteLine($"[AuroraBitmapFont] WARNING: Font loading not yet implemented for Aurora engine: {fontResRef}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraBitmapFont] ERROR: Exception loading font {fontResRef}: {ex.Message}");
                Console.WriteLine($"[AuroraBitmapFont] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Private constructor for creating an AuroraBitmapFont instance.
        /// </summary>
        private AuroraBitmapFont([NotNull] ITexture2D texture, Dictionary<int, CharacterInfo> characterMap, float fontHeight, float fontWidth, float baselineHeight, float spacingR, float spacingB)
        {
            if (texture == null)
            {
                throw new ArgumentNullException("texture");
            }

            _texture = texture;
            _characterMap = characterMap ?? new Dictionary<int, CharacterInfo>();
            _textureWidth = texture.Width;
            _textureHeight = texture.Height;
            _fontHeight = fontHeight;
            _fontWidth = fontWidth;
            _baselineHeight = baselineHeight;
            _spacingR = spacingR;
            _spacingB = spacingB;
        }

        /// <summary>
        /// Gets character information for a specific character code.
        /// </summary>
        /// <param name="charCode">The character code (0-255).</param>
        /// <returns>Character information, or null if not found.</returns>
        public override CharacterGlyph? GetCharacter(int charCode)
        {
            if (_characterMap.TryGetValue(charCode, out CharacterInfo info))
            {
                return new CharacterGlyph
                {
                    SourceX = info.SourceX,
                    SourceY = info.SourceY,
                    SourceWidth = info.SourceWidth,
                    SourceHeight = info.SourceHeight,
                    Width = info.Width,
                    Height = info.Height
                };
            }
            return null;
        }
    }
}

