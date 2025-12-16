using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Core graphics backend interface that all rendering backends must implement.
    /// Provides unified access to graphics device, content loading, window management, and input.
    /// </summary>
    public interface IGraphicsBackend : IDisposable
    {
        /// <summary>
        /// Gets the backend type.
        /// </summary>
        GraphicsBackendType BackendType { get; }

        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        IGraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// Gets the content manager for loading assets.
        /// </summary>
        IContentManager ContentManager { get; }

        /// <summary>
        /// Gets the window manager.
        /// </summary>
        IWindow Window { get; }

        /// <summary>
        /// Gets the input manager.
        /// </summary>
        IInputManager InputManager { get; }

        /// <summary>
        /// Initializes the graphics backend.
        /// </summary>
        /// <param name="width">Initial window width.</param>
        /// <param name="height">Initial window height.</param>
        /// <param name="title">Window title.</param>
        /// <param name="fullscreen">Whether to start in fullscreen mode.</param>
        void Initialize(int width, int height, string title, bool fullscreen = false);

        /// <summary>
        /// Runs the game loop (blocks until exit).
        /// </summary>
        /// <param name="updateAction">Action called each frame for game logic update.</param>
        /// <param name="drawAction">Action called each frame for rendering.</param>
        void Run(Action<float> updateAction, Action drawAction);

        /// <summary>
        /// Exits the game loop.
        /// </summary>
        void Exit();

        /// <summary>
        /// Begins a new frame.
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// Ends the current frame and presents to screen.
        /// </summary>
        void EndFrame();
    }
}

