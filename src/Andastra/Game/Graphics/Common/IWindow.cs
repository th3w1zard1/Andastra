namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Window interface for window management.
    /// </summary>
    /// <remarks>
    /// Window Interface:
    /// - Based on swkotor2.exe window management system
    /// - Located via string references: "Render Window" @ 0x007b5680, "SW Movie Player Window" @ 0x007b57dc
    /// - "SWMovieWindow" @ 0x007b57f4, "Exo Base Window" @ 0x007b74a0
    /// - "AllowWindowedMode" @ 0x007c75d0 (windowed mode option)
    /// - "GetProcessWindowStation" @ 0x007d95f4, "GetActiveWindow" @ 0x007d963c (Windows API functions)
    /// - "SetWindowTextA" @ 0x00809e1a, "DestroyWindow" @ 0x00809e8a, "ShowWindow" @ 0x00809e9a (window management)
    /// - Original implementation: Creates and manages Windows window (HWND) for game rendering
    /// - Window creation: Creates window with DirectX device, handles window messages, manages fullscreen/windowed mode
    /// - This interface: Abstraction layer for modern window management (MonoGame GameWindow, Stride GameWindow)
    /// </remarks>
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

