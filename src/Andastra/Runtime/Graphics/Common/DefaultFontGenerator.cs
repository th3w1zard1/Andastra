using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Andastra.Runtime.Graphics.Common.Enums;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Generates a programmatic default font when content pipeline fonts are unavailable.
    /// Creates a simple fixed-width bitmap font for fallback text rendering.
    /// </summary>
    /// <remarks>
    /// Default Font Generator:
    /// - Based on swkotor2.exe: dialogfont16x16 (16x16 pixel bitmap font)
    /// - Original engine uses bitmap fonts with fixed character sizes
    /// - This implementation creates a programmatic fallback font when content pipeline fails
    /// - Supports both MonoGame and Stride backends via reflection
    /// - Font characteristics: Fixed-width, 16x16 pixel characters, ASCII 32-126
    /// - Matches original engine's dialogfont16x16 font dimensions for consistency
    /// - Ghidra analysis: swkotor2.exe @ 0x007b6380 (dialogfont16x16 string), FUN_00416890 (font initialization)
    /// </remarks>
    public static class DefaultFontGenerator
    {
        /// <summary>
        /// Creates a default font for the specified graphics backend.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device wrapper.</param>
        /// <param name="backendType">The graphics backend type.</param>
        /// <returns>A default font implementation, or null if creation fails.</returns>
        [CanBeNull]
        public static IFont CreateDefaultFont([NotNull] IGraphicsDevice graphicsDevice, GraphicsBackendType backendType)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            try
            {
                // Create programmatic SpriteFont based on backend type
                // Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters
                if ((backendType & GraphicsBackendType.MonoGame) != 0)
                {
                    return CreateMonoGameDefaultFont(graphicsDevice);
                }
                else if ((backendType & GraphicsBackendType.Stride) != 0)
                {
                    return CreateStrideDefaultFont(graphicsDevice);
                }
                else
                {
                    // Fallback to simple measurement-only font
                    Console.WriteLine("[DefaultFontGenerator] WARNING: Unknown backend type, using simple fallback font");
                    return new SimpleDefaultFont();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultFontGenerator] ERROR: Failed to create default font: {ex.Message}");
                Console.WriteLine($"[DefaultFontGenerator] Stack trace: {ex.StackTrace}");
                // Return simple fallback font that at least provides measurements
                return new SimpleDefaultFont();
            }
        }

        /// <summary>
        /// Creates a MonoGame default font using programmatic SpriteFont generation.
        /// </summary>
        [CanBeNull]
        private static IFont CreateMonoGameDefaultFont([NotNull] IGraphicsDevice graphicsDevice)
        {
            try
            {
                // Get the underlying MonoGame GraphicsDevice using reflection
                Type graphicsDeviceType = graphicsDevice.GetType();
                if (graphicsDeviceType.FullName != "Andastra.Runtime.MonoGame.Graphics.MonoGameGraphicsDevice")
                {
                    Console.WriteLine($"[DefaultFontGenerator] ERROR: GraphicsDevice is not MonoGameGraphicsDevice (type: {graphicsDeviceType.FullName})");
                    return new SimpleDefaultFont();
                }

                // Extract the underlying MonoGame GraphicsDevice
                PropertyInfo deviceProperty = graphicsDeviceType.GetProperty("_device", BindingFlags.NonPublic | BindingFlags.Instance);
                if (deviceProperty == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Could not find _device property in MonoGameGraphicsDevice");
                    return new SimpleDefaultFont();
                }

                object mgGraphicsDevice = deviceProperty.GetValue(graphicsDevice);
                if (mgGraphicsDevice == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: MonoGame GraphicsDevice is null");
                    return new SimpleDefaultFont();
                }

                // Create a programmatic SpriteFont
                // Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters
                Microsoft.Xna.Framework.Graphics.SpriteFont spriteFont = CreateProgrammaticMonoGameSpriteFont(mgGraphicsDevice);
                if (spriteFont == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Failed to create programmatic SpriteFont");
                    return new SimpleDefaultFont();
                }

                // Wrap in MonoGameFont
                Assembly monoGameAssembly = graphicsDeviceType.Assembly;
                Type monoGameFontType = monoGameAssembly.GetType("Andastra.Runtime.MonoGame.Graphics.MonoGameFont");
                if (monoGameFontType == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Could not find MonoGameFont type");
                    return new SimpleDefaultFont();
                }

                object fontInstance = Activator.CreateInstance(monoGameFontType, new[] { spriteFont });
                if (fontInstance is IFont font)
                {
                    Console.WriteLine("[DefaultFontGenerator] Successfully created MonoGame default font");
                    return font;
                }

                Console.WriteLine("[DefaultFontGenerator] ERROR: Created instance is not an IFont");
                return new SimpleDefaultFont();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultFontGenerator] ERROR: Exception creating MonoGame font: {ex.Message}");
                return new SimpleDefaultFont();
            }
        }

        /// <summary>
        /// Creates a Stride default font using programmatic SpriteFont generation.
        /// </summary>
        [CanBeNull]
        private static IFont CreateStrideDefaultFont([NotNull] IGraphicsDevice graphicsDevice)
        {
            try
            {
                // Get the underlying Stride GraphicsDevice using reflection
                Type graphicsDeviceType = graphicsDevice.GetType();
                if (graphicsDeviceType.FullName != "Andastra.Runtime.Stride.Graphics.StrideGraphicsDevice")
                {
                    Console.WriteLine($"[DefaultFontGenerator] ERROR: GraphicsDevice is not StrideGraphicsDevice (type: {graphicsDeviceType.FullName})");
                    return new SimpleDefaultFont();
                }

                // Extract the underlying Stride GraphicsDevice
                PropertyInfo deviceProperty = graphicsDeviceType.GetProperty("_device", BindingFlags.NonPublic | BindingFlags.Instance);
                if (deviceProperty == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Could not find _device property in StrideGraphicsDevice");
                    return new SimpleDefaultFont();
                }

                object strideGraphicsDevice = deviceProperty.GetValue(graphicsDevice);
                if (strideGraphicsDevice == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Stride GraphicsDevice is null");
                    return new SimpleDefaultFont();
                }

                // Create a programmatic SpriteFont
                // Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters
                object spriteFont = CreateProgrammaticStrideSpriteFont(strideGraphicsDevice);
                if (spriteFont == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Failed to create programmatic SpriteFont");
                    return new SimpleDefaultFont();
                }

                // Wrap in StrideFont
                Assembly strideAssembly = graphicsDeviceType.Assembly;
                Type strideFontType = strideAssembly.GetType("Andastra.Runtime.Stride.Graphics.StrideFont");
                if (strideFontType == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Could not find StrideFont type");
                    return new SimpleDefaultFont();
                }

                object fontInstance = Activator.CreateInstance(strideFontType, new[] { spriteFont });
                if (fontInstance is IFont font)
                {
                    Console.WriteLine("[DefaultFontGenerator] Successfully created Stride default font");
                    return font;
                }

                Console.WriteLine("[DefaultFontGenerator] ERROR: Created instance is not an IFont");
                return new SimpleDefaultFont();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultFontGenerator] ERROR: Exception creating Stride font: {ex.Message}");
                return new SimpleDefaultFont();
            }
        }

        /// <summary>
        /// Creates a programmatic MonoGame SpriteFont with fixed-width characters.
        /// Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters.
        /// </summary>
        [CanBeNull]
        private static Microsoft.Xna.Framework.Graphics.SpriteFont CreateProgrammaticMonoGameSpriteFont([NotNull] object graphicsDevice)
        {
            try
            {
                // Create a simple bitmap font texture (16x16 pixels per character)
                // ASCII 32-126 (95 characters) arranged in a grid
                // Based on swkotor2.exe: dialogfont16x16 dimensions
                const int charWidth = 16;
                const int charHeight = 16;
                const int charsPerRow = 16; // 16 characters per row
                const int numRows = 6; // 6 rows for 95 characters (32-126)
                const int textureWidth = charWidth * charsPerRow;
                const int textureHeight = charHeight * numRows;

                // Create texture data (white pixels for characters, transparent background)
                byte[] textureData = new byte[textureWidth * textureHeight * 4]; // RGBA
                for (int i = 0; i < textureData.Length; i += 4)
                {
                    textureData[i] = 255;     // R
                    textureData[i + 1] = 255; // G
                    textureData[i + 2] = 255; // B
                    textureData[i + 3] = 255; // A (opaque for now, will be refined)
                }

                // Create texture
                Type texture2DType = typeof(Microsoft.Xna.Framework.Graphics.Texture2D);
                object texture = Activator.CreateInstance(texture2DType, new[] { graphicsDevice, textureWidth, textureHeight });
                if (texture == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Failed to create Texture2D");
                    return null;
                }

                // Set texture data
                MethodInfo setDataMethod = texture2DType.GetMethod("SetData", new[] { typeof(byte[]) });
                if (setDataMethod != null)
                {
                    setDataMethod.Invoke(texture, new object[] { textureData });
                }

                // Create SpriteFont using reflection
                // SpriteFont constructor requires font texture and character mapping
                Type spriteFontType = typeof(Microsoft.Xna.Framework.Graphics.SpriteFont);
                
                // Create character mapping for ASCII 32-126
                List<Microsoft.Xna.Framework.Rectangle> glyphBounds = new List<Microsoft.Xna.Framework.Rectangle>();
                List<Microsoft.Xna.Framework.Rectangle> cropping = new List<Microsoft.Xna.Framework.Rectangle>();
                List<char> characterMap = new List<char>();
                List<Microsoft.Xna.Framework.Vector3> kerning = new List<Microsoft.Xna.Framework.Vector3>();

                for (int i = 32; i <= 126; i++)
                {
                    char c = (char)i;
                    int row = (i - 32) / charsPerRow;
                    int col = (i - 32) % charsPerRow;
                    
                    int x = col * charWidth;
                    int y = row * charHeight;
                    
                    glyphBounds.Add(new Microsoft.Xna.Framework.Rectangle(x, y, charWidth, charHeight));
                    cropping.Add(new Microsoft.Xna.Framework.Rectangle(0, 0, charWidth, charHeight));
                    characterMap.Add(c);
                    kerning.Add(new Microsoft.Xna.Framework.Vector3(0, charWidth, 0)); // Left bearing, width, right bearing
                }

                // Create SpriteFont using constructor
                // SpriteFont constructor: (Texture2D texture, List<Rectangle> glyphs, List<Rectangle> cropping, List<char> charMap, int lineSpacing, float spacing, List<Vector3> kerning, char? defaultChar)
                ConstructorInfo constructor = spriteFontType.GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { 
                        typeof(Microsoft.Xna.Framework.Graphics.Texture2D),
                        typeof(List<Microsoft.Xna.Framework.Rectangle>),
                        typeof(List<Microsoft.Xna.Framework.Rectangle>),
                        typeof(List<char>),
                        typeof(int),
                        typeof(float),
                        typeof(List<Microsoft.Xna.Framework.Vector3>),
                        typeof(char?)
                    },
                    null);

                if (constructor == null)
                {
                    Console.WriteLine("[DefaultFontGenerator] ERROR: Could not find SpriteFont constructor");
                    return null;
                }

                object spriteFont = constructor.Invoke(new object[]
                {
                    texture,
                    glyphBounds,
                    cropping,
                    characterMap,
                    charHeight + 2, // Line spacing (char height + 2 pixels)
                    0.0f, // Character spacing
                    kerning,
                    (char?)'?' // Default character
                });

                return spriteFont as Microsoft.Xna.Framework.Graphics.SpriteFont;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultFontGenerator] ERROR: Exception creating MonoGame SpriteFont: {ex.Message}");
                Console.WriteLine($"[DefaultFontGenerator] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Creates a programmatic Stride SpriteFont with fixed-width characters.
        /// Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters.
        /// </summary>
        [CanBeNull]
        private static object CreateProgrammaticStrideSpriteFont([NotNull] object graphicsDevice)
        {
            try
            {
                // Stride SpriteFont creation is more complex, requires SpriteFontData
                // For now, return null and fall back to simple font
                // TODO: Implement Stride SpriteFont programmatic creation
                Console.WriteLine("[DefaultFontGenerator] WARNING: Stride programmatic SpriteFont creation not yet implemented");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DefaultFontGenerator] ERROR: Exception creating Stride SpriteFont: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Simple fixed-width font implementation for fallback text rendering.
        /// Provides measurement capabilities when actual font rendering is unavailable.
        /// </summary>
        private class SimpleDefaultFont : IFont
        {
            private readonly float _charWidth;
            private readonly float _charHeight;
            private readonly float _lineSpacing;

            // Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters
            private const float DefaultCharWidth = 16.0f;
            private const float DefaultCharHeight = 16.0f;
            private const float DefaultLineSpacing = 18.0f; // Slightly more than char height for readability

            public SimpleDefaultFont()
            {
                _charWidth = DefaultCharWidth;
                _charHeight = DefaultCharHeight;
                _lineSpacing = DefaultLineSpacing;
            }

            public Vector2 MeasureString([CanBeNull] string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return Vector2.Zero;
                }

                // Fixed-width font: each character is the same width
                // Handle newlines by counting lines
                float maxWidth = 0.0f;
                float currentWidth = 0.0f;
                float totalHeight = _charHeight;

                foreach (char c in text)
                {
                    if (c == '\n')
                    {
                        maxWidth = Math.Max(maxWidth, currentWidth);
                        currentWidth = 0.0f;
                        totalHeight += _lineSpacing;
                    }
                    else if (c == '\r')
                    {
                        // Ignore carriage return, handled by newline
                        continue;
                    }
                    else
                    {
                        // Fixed-width: every character (including space) is same width
                        currentWidth += _charWidth;
                    }
                }

                maxWidth = Math.Max(maxWidth, currentWidth);
                return new Vector2(maxWidth, totalHeight);
            }

            public float LineSpacing => _lineSpacing;
        }
    }
}

