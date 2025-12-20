using System;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Core graphics backend interface that all rendering backends must implement.
    /// Provides unified access to graphics device, content loading, window management, and input.
    /// </summary>
    /// <remarks>
    /// Graphics Backend Interface:
    /// - Based on swkotor2.exe graphics initialization system
    /// - Located via string references: "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "optgraphics_p" @ 0x007d2064
    /// - "Render Window" @ 0x007b5680, "render" @ 0x007bab34, "renderorder" @ 0x007bab50
    /// - Original game uses DirectX 8/9 for rendering (D3D8.dll, D3D9.dll)
    /// - Engine initialization: FUN_00404250 @ 0x00404250 initializes graphics device during startup
    /// - Original implementation: Initializes DirectX device, sets up rendering pipeline, creates window
    /// - This interface: Abstraction layer for modern graphics backends (MonoGame, Stride)
    /// - Note: MonoGame and Stride are modern graphics frameworks, not present in original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline, no modern post-processing or upscaling
    /// </remarks>
    public interface IGraphicsBackend : IDisposable
    {
        /// <summary>
        /// Gets the backend type.
        /// </summary>
        GraphicsBackendType BackendType { get; }

        /// <summary>
        /// Gets whether the graphics backend has been initialized.
        /// </summary>
        bool IsInitialized { get; }

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

        /// <summary>
        /// Creates a room mesh renderer.
        /// </summary>
        /// <returns>Created room mesh renderer.</returns>
        IRoomMeshRenderer CreateRoomMeshRenderer();

        /// <summary>
        /// Creates an entity model renderer.
        /// </summary>
        /// <param name="gameDataManager">Game data manager for resolving models (can be null).</param>
        /// <param name="installation">Installation for loading resources (can be null).</param>
        /// <returns>Created entity model renderer.</returns>
        IEntityModelRenderer CreateEntityModelRenderer(object gameDataManager = null, object installation = null);

        /// <summary>
        /// Creates a spatial audio system.
        /// </summary>
        /// <returns>Created spatial audio system.</returns>
        ISpatialAudio CreateSpatialAudio();

        /// <summary>
        /// Creates a dialogue camera controller.
        /// </summary>
        /// <param name="cameraController">The camera controller to use.</param>
        /// <returns>Created dialogue camera controller.</returns>
        object CreateDialogueCameraController(object cameraController);

        /// <summary>
        /// Creates a sound player for playing sound effects.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading audio files.</param>
        /// <returns>Created sound player.</returns>
        object CreateSoundPlayer(object resourceProvider);

        /// <summary>
        /// Creates a music player for background music playback.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading music files.</param>
        /// <returns>IMusicPlayer instance.</returns>
        object CreateMusicPlayer(object resourceProvider);

        /// <summary>
        /// Creates a voice player for playing voice-over dialogue.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading audio files.</param>
        /// <returns>Created voice player.</returns>
        object CreateVoicePlayer(object resourceProvider);

        /// <summary>
        /// Gets whether the graphics backend supports VSync (vertical synchronization).
        /// </summary>
        /// <remarks>
        /// VSync Support:
        /// - Based on swkotor.exe and swkotor2.exe graphics options system
        /// - Original implementation: VSync controlled through DirectX Present parameters
        /// - VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
        /// - Some graphics backends may not support VSync (e.g., headless renderers)
        /// </remarks>
        bool SupportsVSync { get; }

        /// <summary>
        /// Sets the VSync (vertical synchronization) state.
        /// </summary>
        /// <param name="enabled">True to enable VSync, false to disable it.</param>
        /// <remarks>
        /// VSync Setting:
        /// - Based on swkotor.exe and swkotor2.exe graphics options
        /// - Original implementation: VSync controlled via DirectX Present flags (swkotor2.exe: DirectX device presentation)
        /// - When enabled, frames are synchronized with monitor refresh rate (typically 60Hz, 120Hz, etc.)
        /// - Reduces screen tearing but may limit frame rate to monitor refresh rate
        /// - Can be applied immediately without restarting the game
        /// - If VSync is not supported, this method should do nothing (no-op)
        /// </remarks>
        void SetVSync(bool enabled);
    }
}

