namespace Odyssey.Graphics
{
    /// <summary>
    /// Window interface for window management.
    /// </summary>
    public interface IWindow
    {
        /// <summary>
        /// Gets or sets the window title.
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// Gets or sets whether the mouse cursor is visible.
        /// </summary>
        bool IsMouseVisible { get; set; }

        /// <summary>
        /// Gets or sets whether the window is in fullscreen mode.
        /// </summary>
        bool IsFullscreen { get; set; }

        /// <summary>
        /// Gets or sets the window width.
        /// </summary>
        int Width { get; set; }

        /// <summary>
        /// Gets or sets the window height.
        /// </summary>
        int Height { get; set; }

        /// <summary>
        /// Gets whether the window is active (has focus).
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Closes the window.
        /// </summary>
        void Close();
    }
}

