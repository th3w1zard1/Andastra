using System;
using Microsoft.Xna.Framework;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IWindow.
    /// </summary>
    public class MonoGameWindow : IWindow
    {
        private readonly GameWindow _window;

        internal GameWindow Window => _window;

        public MonoGameWindow(GameWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public string Title
        {
            get { return _window.Title; }
            set { _window.Title = value; }
        }

        public bool IsMouseVisible
        {
            get { return _window.IsMouseVisible; }
            set { _window.IsMouseVisible = value; }
        }

        public bool IsFullscreen
        {
            get
            {
                // MonoGame doesn't expose fullscreen directly on window
                // This would need to be managed through GraphicsDeviceManager
                return false;
            }
            set
            {
                // MonoGame doesn't expose fullscreen directly on window
                // This would need to be managed through GraphicsDeviceManager
            }
        }

        public int Width
        {
            get { return _window.ClientBounds.Width; }
            set
            {
                // MonoGame doesn't allow direct window resizing
                // This would need to be managed through GraphicsDeviceManager
            }
        }

        public int Height
        {
            get { return _window.ClientBounds.Height; }
            set
            {
                // MonoGame doesn't allow direct window resizing
                // This would need to be managed through GraphicsDeviceManager
            }
        }

        public bool IsActive => _window.IsActive;

        public void Close()
        {
            // MonoGame doesn't expose window close directly
            // This would need to be managed through Game.Exit()
        }
    }
}

