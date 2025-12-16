using System;
using Stride.Engine;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsBackend.
    /// </summary>
    public class StrideGraphicsBackend : IGraphicsBackend
    {
        private Game _game;
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

        public StrideGraphicsBackend()
        {
            _game = new Game();
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
                _graphicsDevice = new StrideGraphicsDevice(_game.GraphicsDevice);
                _contentManager = new StrideContentManager(_game.Content);
                _window = new StrideWindow(_game.Window);
                _inputManager = new StrideInputManager(_game.Input);
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

