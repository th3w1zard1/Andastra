using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TXI;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Fonts
{
    /// <summary>
    /// Eclipse engine (Dragon Age, ) bitmap font implementation.
    /// Loads texture-based fonts with engine-specific formats.
    /// </summary>
    /// <remarks>
    /// Eclipse Bitmap Font (daorigins.exe, DragonAge2.exe, , ):
    /// - Font textures: TEX/DDS formats (currently using TGA/TPC as fallback until TEX/DDS support is added)
    /// - Font metrics: TXI files or embedded font data (similar to Odyssey/Aurora)
    /// - Character rendering: Uses texture sampling with character coordinates
    /// - Text alignment: Supports Eclipse alignment modes
    /// - Font loading: Loads font texture and metrics from game installation
    /// - Character mapping: Maps ASCII characters to texture coordinates using grid-based or TXI coordinate mapping
    /// 
    /// Ghidra Reverse Engineering Analysis:
    /// - daorigins.exe: Font loading functions (address verification pending Ghidra analysis)
    /// - DragonAge2.exe: Font loading functions (address verification pending Ghidra analysis)
    /// - : Font loading functions (address verification pending Ghidra analysis)
    /// - : Font loading functions (address verification pending Ghidra analysis)
    /// - Font format: TEX/DDS formats (needs proper format support implementation)
    /// 
    /// Original implementation: Uses engine-specific rendering systems
    /// </remarks>
    public class EclipseBitmapFont : BaseBitmapFont
    {
        private readonly MonoGameTexture2D _texture;
        private readonly Dictionary<int, CharacterInfo> _characterMap;
        private readonly TXIFeatures _fontMetrics;
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
        /// Gets the MonoGame Texture2D (for direct rendering).
        /// </summary>
        public Texture2D MonoGameTexture => _texture.Texture;

        /// <summary>
        /// Internal structure for character glyph information.
        /// </summary>
        private class CharacterInfo
        {
            public Rectangle SourceRect { get; set; }
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
        public static EclipseBitmapFont Load([NotNull] string fontResRef, [NotNull] Installation installation, [NotNull] IGraphicsDevice graphicsDevice)
        {
            if (string.IsNullOrEmpty(fontResRef))
            {
                Console.WriteLine("[EclipseBitmapFont] ERROR: Font ResRef cannot be null or empty");
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
                // Eclipse fonts use TEX/DDS formats, but for now we'll use TGA as fallback
                // TODO: PLACEHOLDER - Add proper TEX/DDS format support when ResourceType.TEX/ResourceType.DDS are available
                // Based on daorigins.exe, DragonAge2.exe font loading functions (address verification pending Ghidra analysis)
                
                // Load font texture (try TGA first, then TPC as fallback)
                TPC fontTexture = null;
                var textureResult = installation.Resources.LookupResource(fontResRef, ResourceType.TGA, null, null);
                if (textureResult != null && textureResult.Data != null && textureResult.Data.Length > 0)
                {
                    fontTexture = TPCAuto.ReadTpc(textureResult.Data);
                }
                else
                {
                    // Try TPC as fallback
                    textureResult = installation.Resources.LookupResource(fontResRef, ResourceType.TPC, null, null);
                    if (textureResult != null && textureResult.Data != null && textureResult.Data.Length > 0)
                    {
                        fontTexture = TPCAuto.ReadTpc(textureResult.Data);
                    }
                }

                if (fontTexture == null)
                {
                    Console.WriteLine($"[EclipseBitmapFont] ERROR: Font texture not found: {fontResRef}");
                    return null;
                }

                // Convert to MonoGame Texture2D
                GraphicsDevice mgDevice = graphicsDevice as GraphicsDevice;
                if (mgDevice == null && graphicsDevice is MonoGameGraphicsDevice mgGfxDevice)
                {
                    mgDevice = mgGfxDevice.Device;
                }
                if (mgDevice == null)
                {
                    Console.WriteLine($"[EclipseBitmapFont] ERROR: Graphics device must be MonoGame GraphicsDevice");
                    return null;
                }

                // Fonts always use 2D textures, not cube maps
                Texture convertedTexture = TpcToMonoGameTextureConverter.Convert(fontTexture, mgDevice, false);
                if (convertedTexture is TextureCube)
                {
                    Console.WriteLine($"[EclipseBitmapFont] ERROR: Font texture cannot be a cube map: {fontResRef}");
                    return null;
                }
                Texture2D texture = (Texture2D)convertedTexture;
                if (texture == null)
                {
                    Console.WriteLine($"[EclipseBitmapFont] ERROR: Failed to convert font texture: {fontResRef}");
                    return null;
                }

                // Load TXI metrics (Eclipse may use TXI similar to Odyssey/Aurora)
                TXI txi = null;
                string txiText = fontTexture.Txi;
                if (!string.IsNullOrEmpty(txiText))
                {
                    txi = new TXI(txiText);
                }
                else
                {
                    // Try loading separate TXI file
                    var txiResult = installation.Resources.LookupResource(fontResRef, ResourceType.TXI, null, null);
                    if (txiResult != null && txiResult.Data != null && txiResult.Data.Length > 0)
                    {
                        txi = TXIAuto.ReadTxi(txiResult.Data);
                    }
                }

                if (txi == null || txi.Features == null)
                {
                    Console.WriteLine($"[EclipseBitmapFont] WARNING: No TXI metrics found for font: {fontResRef}, using defaults");
                    // Create default TXI with basic metrics
                    txi = new TXI();
                }

                return new EclipseBitmapFont(new MonoGameTexture2D(texture), txi.Features, fontResRef);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EclipseBitmapFont] ERROR: Exception loading font {fontResRef}: {ex.Message}");
                Console.WriteLine($"[EclipseBitmapFont] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Private constructor for creating an EclipseBitmapFont instance.
        /// </summary>
        private EclipseBitmapFont([NotNull] MonoGameTexture2D texture, [NotNull] TXIFeatures fontMetrics, string fontResRef)
        {
            if (texture == null)
            {
                throw new ArgumentNullException("texture");
            }
            if (fontMetrics == null)
            {
                throw new ArgumentNullException("fontMetrics");
            }

            _texture = texture;
            _fontMetrics = fontMetrics;
            _textureWidth = texture.Width;
            _textureHeight = texture.Height;

            // Extract font metrics from TXI
            _fontHeight = fontMetrics.Fontheight ?? fontMetrics.Arturoheight ?? 16.0f;
            _fontWidth = fontMetrics.Fontwidth ?? fontMetrics.Arturowidth ?? 16.0f;
            _baselineHeight = fontMetrics.Baselineheight ?? _fontHeight;
            _spacingR = fontMetrics.SpacingR ?? 0.0f;
            _spacingB = fontMetrics.SpacingB ?? 0.0f;

            // Build character map from TXI coordinates or default grid
            _characterMap = BuildCharacterMap();
        }

        /// <summary>
        /// Builds the character map from TXI coordinates or default grid.
        /// </summary>
        private Dictionary<int, CharacterInfo> BuildCharacterMap()
        {
            Dictionary<int, CharacterInfo> charMap = new Dictionary<int, CharacterInfo>();

            if (_fontMetrics.Upperleftcoords == null || _fontMetrics.Lowerrightcoords == null)
            {
                // No coordinate data - use default grid-based mapping
                // Eclipse fonts typically use 16x16 or 32x32 character grids
                int charsPerRow = _fontMetrics.Cols ?? 16;
                int charsPerCol = _fontMetrics.Rows ?? 16;
                float cellWidth = _textureWidth / (float)charsPerRow;
                float cellHeight = _textureHeight / (float)charsPerCol;

                for (int i = 0; i < 256; i++)
                {
                    int row = i / charsPerRow;
                    int col = i % charsPerRow;
                    float x = col * cellWidth;
                    float y = row * cellHeight;

                    charMap[i] = new CharacterInfo
                    {
                        SourceRect = new Rectangle((int)x, (int)y, (int)cellWidth, (int)cellHeight),
                        Width = cellWidth,
                        Height = cellHeight
                    };
                }
            }
            else
            {
                // Use TXI coordinate data
                int numChars = Math.Min(_fontMetrics.Upperleftcoords.Count, _fontMetrics.Lowerrightcoords.Count);
                for (int i = 0; i < numChars && i < 256; i++)
                {
                    var upperLeft = _fontMetrics.Upperleftcoords[i];
                    var lowerRight = _fontMetrics.Lowerrightcoords[i];

                    // TXI coordinates are normalized (0.0-1.0), convert to pixel coordinates
                    // Note: TXI Y coordinates are inverted (0.0 = bottom, 1.0 = top)
                    float x1 = upperLeft.Item1 * _textureWidth;
                    float y1 = (1.0f - upperLeft.Item2) * _textureHeight; // Invert Y
                    float x2 = lowerRight.Item1 * _textureWidth;
                    float y2 = (1.0f - lowerRight.Item2) * _textureHeight; // Invert Y

                    float width = Math.Abs(x2 - x1);
                    float height = Math.Abs(y2 - y1);

                    charMap[i] = new CharacterInfo
                    {
                        SourceRect = new Rectangle((int)x1, (int)y1, (int)width, (int)height),
                        Width = width,
                        Height = height
                    };
                }
            }

            return charMap;
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
                    SourceX = info.SourceRect.X,
                    SourceY = info.SourceRect.Y,
                    SourceWidth = info.SourceRect.Width,
                    SourceHeight = info.SourceRect.Height,
                    Width = info.Width,
                    Height = info.Height
                };
            }
            return null;
        }
    }
}

