using System;
using System.Reflection;
using Andastra.Runtime.Graphics;
using Stride.Core.Mathematics;
using Stride.Games;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IWindow.
    /// </summary>
    public class StrideWindow : IWindow
    {
        private readonly GameWindow _window;
        private PropertyInfo _clientSizeProperty;
        private MethodInfo _closeMethod;

        internal GameWindow Window => _window;

        public StrideWindow(GameWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));

            // Use reflection to access ClientSize property (may not be directly accessible in all Stride versions)
            _clientSizeProperty = _window.GetType().GetProperty("ClientSize");

            // Use reflection to access Close method (may not be directly accessible in all Stride versions)
            _closeMethod = _window.GetType().GetMethod("Close");
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
            get
            {
                if (_clientSizeProperty != null)
                {
                    var size = _clientSizeProperty.GetValue(_window);
                    if (size is Int2 int2Size)
                    {
                        return int2Size.X;
                    }
                }
                // Fallback: return a default size if ClientSize is not available
                return 1280;
            }
            set
            {
                if (_clientSizeProperty != null && _clientSizeProperty.CanWrite)
                {
                    var currentSize = _clientSizeProperty.GetValue(_window);
                    if (currentSize is Int2 currentInt2)
                    {
                        var newSize = new Int2(value, currentInt2.Y);
                        _clientSizeProperty.SetValue(_window, newSize);
                        return;
                    }
                }
                // If ClientSize is not available, size cannot be set directly
                // Window size will be managed through GraphicsDevice.Presenter in this case
            }
        }

        public int Height
        {
            get
            {
                if (_clientSizeProperty != null)
                {
                    var size = _clientSizeProperty.GetValue(_window);
                    if (size is Int2 int2Size)
                    {
                        return int2Size.Y;
                    }
                }
                // Fallback: return a default size if ClientSize is not available
                return 720;
            }
            set
            {
                if (_clientSizeProperty != null && _clientSizeProperty.CanWrite)
                {
                    var currentSize = _clientSizeProperty.GetValue(_window);
                    if (currentSize is Int2 currentInt2)
                    {
                        var newSize = new Int2(currentInt2.X, value);
                        _clientSizeProperty.SetValue(_window, newSize);
                        return;
                    }
                }
                // If ClientSize is not available, size cannot be set directly
                // Window size will be managed through GraphicsDevice.Presenter in this case
            }
        }

        public bool IsActive => _window.IsActivated;

        public void Close()
        {
            if (_closeMethod != null)
            {
                _closeMethod.Invoke(_window, null);
            }
            else
            {
                // Fallback: try to exit through the game if Close is not available
                // This will be handled by the graphics backend
                throw new NotSupportedException("Close method is not available on GameWindow");
            }
        }
    }
}

