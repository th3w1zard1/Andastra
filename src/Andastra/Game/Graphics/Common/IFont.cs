namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Font interface for text rendering.
    /// </summary>
    /// <remarks>
    /// Font Interface:
    /// - Based on swkotor2.exe font rendering system
    /// - Located via string references: "dialogfont16x16" @ 0x007b6380, "fontheight" @ 0x007b6eb8
    /// - "Use Small Fonts" @ 0x007c8538 (font size option)
    /// - Original implementation: Renders text using bitmap fonts (dialogfont16x16.tga, etc.)
    /// - Font files: Bitmap fonts stored as TGA textures with character mappings
    /// - Text rendering: Uses sprite rendering to draw text characters from font texture
    /// - This interface: Abstraction layer for modern font rendering (TrueType, bitmap fonts)
    /// </remarks>
    public interface IFont
    {
        /// <summary>
        /// Measures the size of text when rendered.
        /// </summary>
        /// <param name="text">Text to measure.</param>
        /// <returns>Text size in pixels.</returns>
        Vector2 MeasureString(string text);

        /// <summary>
        /// Gets the line spacing (height of a line of text).
        /// </summary>
        float LineSpacing { get; }
    }
}

