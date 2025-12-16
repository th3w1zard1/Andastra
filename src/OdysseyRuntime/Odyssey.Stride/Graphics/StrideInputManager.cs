using Stride.Input;
using Stride.Core.Mathematics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IInputManager.
    /// </summary>
    public class StrideInputManager : IInputManager
    {
        private readonly InputManager _inputManager;
        private StrideKeyboardState _keyboardState;
        private StrideKeyboardState _previousKeyboardState;
        private StrideMouseState _mouseState;
        private StrideMouseState _previousMouseState;

        public StrideInputManager(InputManager inputManager)
        {
            _inputManager = inputManager ?? throw new System.ArgumentNullException(nameof(inputManager));
            _keyboardState = new StrideKeyboardState(_inputManager);
            _previousKeyboardState = new StrideKeyboardState(_inputManager);
            _mouseState = new StrideMouseState(_inputManager);
            _previousMouseState = new StrideMouseState(_inputManager);
        }

        public IKeyboardState KeyboardState => _keyboardState;

        public IMouseState MouseState => _mouseState;

        public IKeyboardState PreviousKeyboardState => _previousKeyboardState;

        public IMouseState PreviousMouseState => _previousMouseState;

        public void Update()
        {
            _previousKeyboardState = _keyboardState;
            _previousMouseState = _mouseState;

            _keyboardState = new StrideKeyboardState(_inputManager);
            _mouseState = new StrideMouseState(_inputManager);
        }
    }

    /// <summary>
    /// Stride implementation of IKeyboardState.
    /// </summary>
    public class StrideKeyboardState : IKeyboardState
    {
        private readonly InputManager _inputManager;

        internal StrideKeyboardState(InputManager inputManager)
        {
            _inputManager = inputManager;
        }

        public bool IsKeyDown(Keys key)
        {
            var strideKey = ConvertKey(key);
            return _inputManager.IsKeyDown(strideKey);
        }

        public bool IsKeyUp(Keys key)
        {
            var strideKey = ConvertKey(key);
            return _inputManager.IsKeyUp(strideKey);
        }

        private Keys ConvertKey(Keys key)
        {
            // Map our Keys enum to Stride's Keys enum
            // Stride uses the same enum values, so we can cast directly
            return (Stride.Input.Keys)key;
        }
    }

    /// <summary>
    /// Stride implementation of IMouseState.
    /// </summary>
    public class StrideMouseState : IMouseState
    {
        private readonly InputManager _inputManager;

        internal StrideMouseState(InputManager inputManager)
        {
            _inputManager = inputManager;
        }

        public Vector2 Position => new Vector2(_inputManager.MousePosition.X, _inputManager.MousePosition.Y);

        public int ScrollWheelValue => (int)_inputManager.MouseWheelDelta;

        public bool IsButtonDown(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return _inputManager.IsMouseButtonDown(MouseButton.Left);
                case MouseButton.Right:
                    return _inputManager.IsMouseButtonDown(MouseButton.Right);
                case MouseButton.Middle:
                    return _inputManager.IsMouseButtonDown(MouseButton.Middle);
                case MouseButton.XButton1:
                    return _inputManager.IsMouseButtonDown(MouseButton.Extended1);
                case MouseButton.XButton2:
                    return _inputManager.IsMouseButtonDown(MouseButton.Extended2);
                default:
                    return false;
            }
        }

        public bool IsButtonUp(MouseButton button)
        {
            return !IsButtonDown(button);
        }
    }
}

