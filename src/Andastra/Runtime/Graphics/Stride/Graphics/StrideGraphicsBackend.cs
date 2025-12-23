using System;
using Stride.Engine;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsBackend.
    /// </summary>
    public class StrideGraphicsBackend : IGraphicsBackend
    {
        private global::Stride.Engine.Game _game;
        private StrideGraphicsDevice _graphicsDevice;
        private StrideContentManager _contentManager;
        private StrideWindow _window;
        private StrideInputManager _inputManager;
        private bool _isInitialized;

        public GraphicsBackendType BackendType => GraphicsBackendType.Stride;

        public IGraphicsDevice GraphicsDevice => _graphicsDevice;

        public IContentManager ContentManager => _contentManager;

        public IWindow Window => _window;

        public IInputManager InputManager => _inputManager;

        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets whether the graphics backend supports VSync (vertical synchronization).
        /// Stride supports VSync through GraphicsDevice.Presenter when initialized.
        /// </summary>
        public bool SupportsVSync => _isInitialized && _graphicsDevice != null && _game?.GraphicsDevice?.Presenter != null;

        public StrideGraphicsBackend()
        {
            _game = new global::Stride.Engine.Game();
        }

        public void Initialize(int width, int height, string title, bool fullscreen = false)
        {
            if (_isInitialized)
            {
                return;
            }

            // Stride initialization happens in the game constructor
            // We'll set up the window properties before running
            _game.Window.ClientSize = new Int2(width, height);
            _game.Window.Title = title;
            _game.Window.IsFullscreen = fullscreen;
            _game.Window.IsMouseVisible = true;

            // Initialize graphics device, content manager, window, and input after game starts
            // These will be set up in the Run method when the game is actually running
            _isInitialized = true;
        }

        public void Run(Action<float> updateAction, Action drawAction)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before running.");
            }

            // Initialize graphics components when game starts
            if (_graphicsDevice == null)
            {
                _graphicsDevice = new StrideGraphicsDevice(_game.GraphicsDevice, _game.GraphicsContext);
                _contentManager = new StrideContentManager(_game.Content, _game.GraphicsContext);
                _window = new StrideWindow(_game.Window);
                _inputManager = new StrideInputManager(_game.Input);

                // Register the CommandList from GraphicsContext for use by ImmediateContext() extension method
                // This allows code that only has access to GraphicsDevice to retrieve the CommandList
                if (_game.GraphicsDevice != null && _game.GraphicsContext != null && _game.GraphicsContext.CommandList != null)
                {
                    GraphicsDeviceExtensions.RegisterCommandList(_game.GraphicsDevice, _game.GraphicsContext.CommandList);
                }
            }

            // Stride uses a different game loop pattern
            // We'll need to hook into the game's update and draw callbacks
            _game.UpdateFrame += (sender, e) =>
            {
                BeginFrame();
                if (updateAction != null)
                {
                    updateAction((float)e.Elapsed.TotalSeconds);
                }
            };

            _game.DrawFrame += (sender, e) =>
            {
                if (drawAction != null)
                {
                    drawAction();
                }
                EndFrame();
            };

            _game.Run();
        }

        public void Exit()
        {
            _game.Exit();
        }

        public void BeginFrame()
        {
            _inputManager.Update();
        }

        public void EndFrame()
        {
            // Stride handles presentation automatically
        }

        public IRoomMeshRenderer CreateRoomMeshRenderer()
        {
            if (!_isInitialized || _graphicsDevice == null)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new StrideRoomMeshRenderer(_game.GraphicsDevice);
        }

        public IEntityModelRenderer CreateEntityModelRenderer(object gameDataManager = null, object installation = null)
        {
            if (!_isInitialized || _graphicsDevice == null)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new StrideEntityModelRenderer(_game.GraphicsDevice, gameDataManager, installation);
        }

        public ISpatialAudio CreateSpatialAudio()
        {
            return new StrideSpatialAudio();
        }

        public object CreateDialogueCameraController(object cameraController)
        {
            if (cameraController is Andastra.Runtime.Core.Camera.CameraController coreCameraController)
            {
                return new Odyssey.Stride.Camera.StrideDialogueCameraController(coreCameraController);
            }
            throw new ArgumentException("Camera controller must be a CameraController instance", nameof(cameraController));
        }

        public object CreateSoundPlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                return new Andastra.Runtime.Stride.Audio.StrideSoundPlayer(provider, spatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateMusicPlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                return new Andastra.Runtime.Stride.Audio.StrideMusicPlayer(provider);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateVoicePlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                return new Andastra.Runtime.Stride.Audio.StrideVoicePlayer(provider, spatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        /// <summary>
        /// Sets the VSync (vertical synchronization) state.
        /// </summary>
        /// <param name="enabled">True to enable VSync, false to disable it.</param>
        /// <remarks>
        /// VSync Implementation:
        /// - Based on Stride GraphicsDevice.Presenter.VSyncMode
        /// - Original game: VSync controlled via DirectX Present parameters (swkotor2.exe: DirectX device presentation)
        /// - Stride uses GraphicsDevice.Presenter to control VSync
        /// - Changes are applied immediately to the swap chain
        /// - VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
        /// - Stride VSyncMode: None (disabled), VerticalSync (enabled), or Adaptive (adaptive sync)
        /// </remarks>
        public void SetVSync(bool enabled)
        {
            if (!_isInitialized || _graphicsDevice == null || _game?.GraphicsDevice == null)
            {
                Console.WriteLine("[Stride] WARNING: Cannot set VSync - backend not initialized");
                return;
            }

            try
            {
                var presenter = _game.GraphicsDevice.Presenter;
                if (presenter == null)
                {
                    Console.WriteLine("[Stride] WARNING: Cannot set VSync - Presenter not available");
                    return;
                }

                // Set VSync state via GraphicsDevice.Presenter
                // Based on Stride API: GraphicsDevice.Presenter.Desynchronized
                // Desynchronized = false means VSync enabled, true means VSync disabled
                if (enabled)
                {
                    // Enable VSync (synchronize with vertical refresh)
                    presenter.Desynchronized = false;
                    Console.WriteLine("[Stride] VSync enabled");
                }
                else
                {
                    // Disable VSync (desynchronize from vertical refresh)
                    presenter.Desynchronized = true;
                    Console.WriteLine("[Stride] VSync disabled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stride] ERROR: Failed to set VSync: {ex.Message}");
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

            // Unregister CommandList when disposing
            if (_game != null && _game.GraphicsDevice != null)
            {
                GraphicsDeviceExtensions.UnregisterCommandList(_game.GraphicsDevice);
                _game.Dispose();
                _game = null;
            }
        }
    }
}

