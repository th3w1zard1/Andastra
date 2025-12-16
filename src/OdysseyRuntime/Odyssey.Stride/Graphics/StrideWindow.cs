using System;
using Stride.Games;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IWindow.
    /// </summary>
    public class StrideWindow : IWindow
    {
        private readonly GameWindow _window;

        internal GameWindow Window => _window;

        public StrideWindow(GameWindow window)
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
            get { return _window.IsFullscreen; }
            set { _window.IsFullscreen = value; }
        }

        public int Width
        {
            get { return _window.ClientSize.X; }
            set
            {
                var size = _window.ClientSize;
                size.X = value;
                _window.ClientSize = size;
            }
        }

        public int Height
        {
            get { return _window.ClientSize.Y; }
            set
            {
                var size = _window.ClientSize;
                size.Y = value;
                _window.ClientSize = size;
            }
        }

        public bool IsActive => _window.IsActivated;

        public void Close()
        {
            _window.Close();
        }
    }
}

