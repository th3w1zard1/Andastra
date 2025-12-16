namespace Odyssey.Graphics
{
    /// <summary>
    /// Font interface for text rendering.
    /// </summary>
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

