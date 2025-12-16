using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IGraphicsBackend.
    /// </summary>
    public class MonoGameGraphicsBackend : IGraphicsBackend
    {
        private Game _game;
        private GraphicsDeviceManager _graphicsDeviceManager;
        private MonoGameGraphicsDevice _graphicsDevice;
        private MonoGameContentManager _contentManager;
        private MonoGameWindow _window;
        private MonoGameInputManager _inputManager;
        private bool _isInitialized;

        public GraphicsBackendType BackendType => GraphicsBackendType.MonoGame;

        public IGraphicsDevice GraphicsDevice => _graphicsDevice;

        public IContentManager ContentManager => _contentManager;

        public IWindow Window => _window;

        public IInputManager InputManager => _inputManager;

        public MonoGameGraphicsBackend()
        {
            _game = new Game();
            _graphicsDeviceManager = new GraphicsDeviceManager(_game);
        }

        public void Initialize(int width, int height, string title, bool fullscreen = false)
        {
            if (_isInitialized)
            {
                return;
            }

            _graphicsDeviceManager.PreferredBackBufferWidth = width;
            _graphicsDeviceManager.PreferredBackBufferHeight = height;
            _graphicsDeviceManager.IsFullScreen = fullscreen;
            _graphicsDeviceManager.ApplyChanges();

            _game.Window.Title = title;
            _game.IsMouseVisible = true;

            _game.Initialize();

            _graphicsDevice = new MonoGameGraphicsDevice(_game.GraphicsDevice);
            _contentManager = new MonoGameContentManager(_game.Content);
            _window = new MonoGameWindow(_game.Window);
            _inputManager = new MonoGameInputManager();

            _isInitialized = true;
        }

        public void Run(Action<float> updateAction, Action drawAction)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before running.");
            }

            var gameTime = new GameTime();
            var totalTime = TimeSpan.Zero;
            var lastTime = DateTime.Now;

            while (!_game.IsExiting)
            {
                var currentTime = DateTime.Now;
                var deltaTime = (float)(currentTime - lastTime).TotalSeconds;
                lastTime = currentTime;

                totalTime = totalTime.Add(TimeSpan.FromSeconds(deltaTime));
                gameTime.ElapsedGameTime = TimeSpan.FromSeconds(deltaTime);
                gameTime.TotalGameTime = totalTime;

                _game.Tick();

                BeginFrame();

                if (updateAction != null)
                {
                    updateAction(deltaTime);
                }

                if (drawAction != null)
                {
                    drawAction();
                }

                EndFrame();
            }
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

