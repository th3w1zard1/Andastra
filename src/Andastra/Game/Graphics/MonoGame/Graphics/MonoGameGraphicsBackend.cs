using System;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Game.Graphics.MonoGame.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IGraphicsBackend.
    /// </summary>
    public class MonoGameGraphicsBackend : IGraphicsBackend
    {
        private MonoGameWrapper _game;
        private GraphicsDeviceManager _graphicsDeviceManager;
        private MonoGameGraphicsDevice _graphicsDevice;
        private MonoGameContentManager _contentManager;
        private MonoGameWindow _window;
        private MonoGameInputManager _inputManager;
        private bool _isInitialized;
        private bool _isExiting;

        // Store settings for deferred initialization (applied when game is initialized)
        private int _pendingWidth;
        private int _pendingHeight;
        private string _pendingTitle;
        private bool _pendingFullscreen;

        public GraphicsBackendType BackendType => GraphicsBackendType.MonoGame;

        public bool IsInitialized => _isInitialized;

        public IGraphicsDevice GraphicsDevice => _graphicsDevice;

        public IContentManager ContentManager => _contentManager;

        public IWindow Window => _window;

        public IInputManager InputManager => _inputManager;

        /// <summary>
        /// Gets whether the graphics backend supports VSync (vertical synchronization).
        /// MonoGame always supports VSync through GraphicsDeviceManager.
        /// </summary>
        public bool SupportsVSync => _isInitialized && _graphicsDeviceManager != null;

        public MonoGameGraphicsBackend()
        {
            _game = new MonoGameWrapper();
            _graphicsDeviceManager = new GraphicsDeviceManager(_game);
        }

        public void Initialize(int width, int height, string title, bool fullscreen = false)
        {
            if (_isInitialized)
            {
                return;
            }

            // Store settings for deferred initialization
            // Based on MonoGame API: GraphicsDevice and Window are not available until Game.Run() is called
            // Settings will be applied in the Initialized event handler
            _pendingWidth = width;
            _pendingHeight = height;
            _pendingTitle = title;
            _pendingFullscreen = fullscreen;

            // Configure graphics device manager before Run() is called
            _graphicsDeviceManager.PreferredBackBufferWidth = width;
            _graphicsDeviceManager.PreferredBackBufferHeight = height;
            _graphicsDeviceManager.IsFullScreen = fullscreen;

            _isInitialized = true;
        }

        public void Run(Action<float> updateAction, Action drawAction)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before running.");
            }

            // Store callbacks for use in event handlers
            Action<float> storedUpdateAction = updateAction;
            Action storedDrawAction = drawAction;

            // Hook into Initialized event to create wrapper objects after GraphicsDevice is available
            // Based on MonoGame API: Game.Initialize() is called internally after Run() starts
            _game.Initialized += (sender, e) =>
            {
                // Apply deferred settings now that Window is available
                _game.Window.Title = _pendingTitle;
                _game.IsMouseVisible = true;
                _graphicsDeviceManager.ApplyChanges();

                // Create wrapper objects now that GraphicsDevice, Content, and Window are available
                _graphicsDevice = new MonoGameGraphicsDevice(_game.GraphicsDevice);
                _contentManager = new MonoGameContentManager(_game.Content);
                _window = new MonoGameWindow(_game.Window);
                _inputManager = new MonoGameInputManager();
            };

            // Hook into MonoGameWrapper's UpdateFrame event
            // Based on MonoGame API: Update is called every frame before Draw
            _game.UpdateFrame += (sender, e) =>
            {
                BeginFrame();

                if (storedUpdateAction != null)
                {
                    // Use GameTime from MonoGame's Update event for accurate delta time
                    float deltaTime = (float)e.Elapsed.TotalSeconds;
                    storedUpdateAction(deltaTime);
                }
            };

            // Hook into MonoGameWrapper's DrawFrame event
            // Based on MonoGame API: Draw is called every frame after Update
            _game.DrawFrame += (sender, e) =>
            {
                storedDrawAction?.Invoke();

                EndFrame();
            };

            // Run the game - this creates the window, shows it, and starts the game loop
            // Based on MonoGame API: Game.Run() blocks until the game exits
            // This is what actually creates and displays the game window
            _game.Run();
        }

        public void Exit()
        {
            _isExiting = true;
            _game.Exit();
        }

        public void BeginFrame()
        {
            // InputManager is initialized in the Initialized event handler
            // Check for null to handle edge case where Initialized hasn't fired yet
            _inputManager?.Update();
        }

        public void EndFrame()
        {
            // MonoGame handles presentation automatically in Game.Tick()
        }

        public IRoomMeshRenderer CreateRoomMeshRenderer()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new MonoGameRoomMeshRenderer(_game.GraphicsDevice);
        }

        public IEntityModelRenderer CreateEntityModelRenderer(object gameDataManager = null, object installation = null)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new MonoGameEntityModelRenderer(_game.GraphicsDevice, gameDataManager, installation);
        }

        public ISpatialAudio CreateSpatialAudio()
        {
            return new MonoGameSpatialAudio();
        }

        public object CreateDialogueCameraController(object cameraController)
        {
            if (cameraController is Core.Camera.CameraController coreCameraController)
            {
                return new Camera.MonoGameDialogueCameraController(coreCameraController);
            }
            throw new ArgumentException("Camera controller must be a CameraController instance", nameof(cameraController));
        }

        public object CreateSoundPlayer(object resourceProvider)
        {
            if (resourceProvider is Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                var monoGameSpatialAudio = spatialAudio as MonoGameSpatialAudio;
                var underlyingSpatialAudio = monoGameSpatialAudio?.UnderlyingSpatialAudio;
                return new Audio.MonoGameSoundPlayer(provider, underlyingSpatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateMusicPlayer(object resourceProvider)
        {
            if (resourceProvider is Content.Interfaces.IGameResourceProvider provider)
            {
                return new Audio.MonoGameMusicPlayer(provider);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateVoicePlayer(object resourceProvider)
        {
            if (resourceProvider is Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                var monoGameSpatialAudio = spatialAudio as MonoGameSpatialAudio;
                var underlyingSpatialAudio = monoGameSpatialAudio?.UnderlyingSpatialAudio;
                return new Audio.MonoGameVoicePlayer(provider, underlyingSpatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        /// <summary>
        /// Sets the VSync (vertical synchronization) state.
        /// </summary>
        /// <param name="enabled">True to enable VSync, false to disable it.</param>
        /// <remarks>
        /// VSync Implementation:
        /// - Based on MonoGame GraphicsDeviceManager.SynchronizeWithVerticalRetrace
        /// - Original game: VSync controlled via DirectX Present parameters (swkotor2.exe: DirectX device presentation)
        /// - MonoGame uses GraphicsDeviceManager to control VSync
        /// - Changes are applied immediately via ApplyChanges()
        /// - VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
        /// </remarks>
        public void SetVSync(bool enabled)
        {
            if (!_isInitialized || _graphicsDeviceManager == null)
            {
                Console.WriteLine("[MonoGame] WARNING: Cannot set VSync - backend not initialized");
                return;
            }

            try
            {
                // Set VSync state via GraphicsDeviceManager
                // Based on MonoGame API: GraphicsDeviceManager.SynchronizeWithVerticalRetrace
                _graphicsDeviceManager.SynchronizeWithVerticalRetrace = enabled;

                // Apply changes immediately
                // Based on MonoGame API: GraphicsDeviceManager.ApplyChanges() applies VSync setting
                _graphicsDeviceManager.ApplyChanges();

                Console.WriteLine($"[MonoGame] VSync {(enabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonoGame] ERROR: Failed to set VSync: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_graphicsDevice != null)
            {
                _graphicsDevice.Dispose();
                _graphicsDevice = null;
            }

            if (_contentManager != null)
            {
                _contentManager.Dispose();
                _contentManager = null;
            }

            if (_game != null)
            {
                _game.Dispose();
                _game = null;
            }
        }
    }
}

