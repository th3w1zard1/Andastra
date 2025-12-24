using Andastra.Runtime.Graphics;
using Stride.Input;
using GraphicsKeys = Andastra.Runtime.Graphics.Keys;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;

namespace Andastra.Runtime.Stride.Graphics
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

        public bool IsKeyDown(GraphicsKeys key)
        {
            var strideKey = ConvertKey(key);
            return _inputManager.IsKeyDown(strideKey);
        }

        public bool IsKeyUp(GraphicsKeys key)
        {
            var strideKey = ConvertKey(key);
            // Stride InputManager doesn't have IsKeyUp, use !IsKeyDown instead
            return !_inputManager.IsKeyDown(strideKey);
        }

        public GraphicsKeys[] GetPressedKeys()
        {
            // Stride doesn't have a direct GetPressedKeys method
            // We'll need to check all keys manually
            var pressedKeys = new System.Collections.Generic.List<GraphicsKeys>();
            foreach (GraphicsKeys key in System.Enum.GetValues(typeof(GraphicsKeys)))
            {
                if (key != GraphicsKeys.None && IsKeyDown(key))
                {
                    pressedKeys.Add(key);
                }
            }
            return pressedKeys.ToArray();
        }

        private global::Stride.Input.Keys ConvertKey(GraphicsKeys key)
        {
            // Map our Keys enum to Stride's Keys enum
            switch (key)
            {
                case GraphicsKeys.None:
                    return global::Stride.Input.Keys.None;
                case GraphicsKeys.Back:
                    return global::Stride.Input.Keys.Back;
                case GraphicsKeys.Tab:
                    return global::Stride.Input.Keys.Tab;
                case GraphicsKeys.Enter:
                    return global::Stride.Input.Keys.Enter;
                case GraphicsKeys.Escape:
                    return global::Stride.Input.Keys.Escape;
                case GraphicsKeys.Space:
                    return global::Stride.Input.Keys.Space;
                case GraphicsKeys.Up:
                    return global::Stride.Input.Keys.Up;
                case GraphicsKeys.Down:
                    return global::Stride.Input.Keys.Down;
                case GraphicsKeys.Left:
                    return global::Stride.Input.Keys.Left;
                case GraphicsKeys.Right:
                    return global::Stride.Input.Keys.Right;
                case GraphicsKeys.A:
                    return global::Stride.Input.Keys.A;
                case GraphicsKeys.B:
                    return global::Stride.Input.Keys.B;
                case GraphicsKeys.C:
                    return global::Stride.Input.Keys.C;
                case GraphicsKeys.D:
                    return global::Stride.Input.Keys.D;
                case GraphicsKeys.E:
                    return global::Stride.Input.Keys.E;
                case GraphicsKeys.F:
                    return global::Stride.Input.Keys.F;
                case GraphicsKeys.G:
                    return global::Stride.Input.Keys.G;
                case GraphicsKeys.H:
                    return global::Stride.Input.Keys.H;
                case GraphicsKeys.I:
                    return global::Stride.Input.Keys.I;
                case GraphicsKeys.J:
                    return global::Stride.Input.Keys.J;
                case GraphicsKeys.K:
                    return global::Stride.Input.Keys.K;
                case GraphicsKeys.L:
                    return global::Stride.Input.Keys.L;
                case GraphicsKeys.M:
                    return global::Stride.Input.Keys.M;
                case GraphicsKeys.N:
                    return global::Stride.Input.Keys.N;
                case GraphicsKeys.O:
                    return global::Stride.Input.Keys.O;
                case GraphicsKeys.P:
                    return global::Stride.Input.Keys.P;
                case GraphicsKeys.Q:
                    return global::Stride.Input.Keys.Q;
                case GraphicsKeys.R:
                    return global::Stride.Input.Keys.R;
                case GraphicsKeys.S:
                    return global::Stride.Input.Keys.S;
                case GraphicsKeys.T:
                    return global::Stride.Input.Keys.T;
                case GraphicsKeys.U:
                    return global::Stride.Input.Keys.U;
                case GraphicsKeys.V:
                    return global::Stride.Input.Keys.V;
                case GraphicsKeys.W:
                    return global::Stride.Input.Keys.W;
                case GraphicsKeys.X:
                    return global::Stride.Input.Keys.X;
                case GraphicsKeys.Y:
                    return global::Stride.Input.Keys.Y;
                case GraphicsKeys.Z:
                    return global::Stride.Input.Keys.Z;
                case GraphicsKeys.D0:
                    return global::Stride.Input.Keys.D0;
                case GraphicsKeys.D1:
                    return global::Stride.Input.Keys.D1;
                case GraphicsKeys.D2:
                    return global::Stride.Input.Keys.D2;
                case GraphicsKeys.D3:
                    return global::Stride.Input.Keys.D3;
                case GraphicsKeys.D4:
                    return global::Stride.Input.Keys.D4;
                case GraphicsKeys.D5:
                    return global::Stride.Input.Keys.D5;
                case GraphicsKeys.D6:
                    return global::Stride.Input.Keys.D6;
                case GraphicsKeys.D7:
                    return global::Stride.Input.Keys.D7;
                case GraphicsKeys.D8:
                    return global::Stride.Input.Keys.D8;
                case GraphicsKeys.D9:
                    return global::Stride.Input.Keys.D9;
                case GraphicsKeys.F1:
                    return global::Stride.Input.Keys.F1;
                case GraphicsKeys.F2:
                    return global::Stride.Input.Keys.F2;
                case GraphicsKeys.F3:
                    return global::Stride.Input.Keys.F3;
                case GraphicsKeys.F4:
                    return global::Stride.Input.Keys.F4;
                case GraphicsKeys.F5:
                    return global::Stride.Input.Keys.F5;
                case GraphicsKeys.F6:
                    return global::Stride.Input.Keys.F6;
                case GraphicsKeys.F7:
                    return global::Stride.Input.Keys.F7;
                case GraphicsKeys.F8:
                    return global::Stride.Input.Keys.F8;
                case GraphicsKeys.F9:
                    return global::Stride.Input.Keys.F9;
                case GraphicsKeys.F10:
                    return global::Stride.Input.Keys.F10;
                case GraphicsKeys.F11:
                    return global::Stride.Input.Keys.F11;
                case GraphicsKeys.F12:
                    return global::Stride.Input.Keys.F12;
                case GraphicsKeys.LeftControl:
                    return global::Stride.Input.Keys.LeftCtrl;
                case GraphicsKeys.RightControl:
                    return global::Stride.Input.Keys.RightCtrl;
                case GraphicsKeys.LeftShift:
                    return global::Stride.Input.Keys.LeftShift;
                case GraphicsKeys.RightShift:
                    return global::Stride.Input.Keys.RightShift;
                case GraphicsKeys.LeftAlt:
                    return global::Stride.Input.Keys.LeftAlt;
                case GraphicsKeys.RightAlt:
                    return global::Stride.Input.Keys.RightAlt;
                default:
                    return global::Stride.Input.Keys.None;
            }
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

        public int X => (int)_inputManager.MousePosition.X;
        public int Y => (int)_inputManager.MousePosition.Y;
        public GraphicsVector2 Position => new GraphicsVector2(_inputManager.MousePosition.X, _inputManager.MousePosition.Y);
        public int ScrollWheelValue => (int)_inputManager.MouseWheelDelta;

        public ButtonState LeftButton => _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Left) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState RightButton => _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Right) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState MiddleButton => _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Middle) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton1 => _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Extended1) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton2 => _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Extended2) ? ButtonState.Pressed : ButtonState.Released;

        public bool IsButtonDown(Andastra.Runtime.Graphics.MouseButton button)
        {
            switch (button)
            {
                case Andastra.Runtime.Graphics.MouseButton.Left:
                    return _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Left);
                case Andastra.Runtime.Graphics.MouseButton.Right:
                    return _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Right);
                case Andastra.Runtime.Graphics.MouseButton.Middle:
                    return _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Middle);
                case Andastra.Runtime.Graphics.MouseButton.XButton1:
                    return _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Extended1);
                case Andastra.Runtime.Graphics.MouseButton.XButton2:
                    return _inputManager.IsMouseButtonDown(global::Stride.Input.MouseButton.Extended2);
                default:
                    return false;
            }
        }

        public bool IsButtonUp(Andastra.Runtime.Graphics.MouseButton button)
        {
            return !IsButtonDown(button);
        }
    }
}

