using Stride.Input;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;
using System.Numerics;

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

        public Keys[] GetPressedKeys()
        {
            // Stride doesn't have a direct GetPressedKeys method
            // We'll need to check all keys manually
            var pressedKeys = new System.Collections.Generic.List<Keys>();
            foreach (Keys key in System.Enum.GetValues(typeof(Keys)))
            {
                if (key != Keys.None && IsKeyDown(key))
                {
                    pressedKeys.Add(key);
                }
            }
            return pressedKeys.ToArray();
        }

        private Stride.Input.Keys ConvertKey(Keys key)
        {
            // Map our Keys enum to Stride's Keys enum
            switch (key)
            {
                case Keys.None:
                    return Stride.Input.Keys.None;
                case Keys.Back:
                    return Stride.Input.Keys.Back;
                case Keys.Tab:
                    return Stride.Input.Keys.Tab;
                case Keys.Enter:
                    return Stride.Input.Keys.Enter;
                case Keys.Escape:
                    return Stride.Input.Keys.Escape;
                case Keys.Space:
                    return Stride.Input.Keys.Space;
                case Keys.Up:
                    return Stride.Input.Keys.Up;
                case Keys.Down:
                    return Stride.Input.Keys.Down;
                case Keys.Left:
                    return Stride.Input.Keys.Left;
                case Keys.Right:
                    return Stride.Input.Keys.Right;
                case Keys.A:
                    return Stride.Input.Keys.A;
                case Keys.B:
                    return Stride.Input.Keys.B;
                case Keys.C:
                    return Stride.Input.Keys.C;
                case Keys.D:
                    return Stride.Input.Keys.D;
                case Keys.E:
                    return Stride.Input.Keys.E;
                case Keys.F:
                    return Stride.Input.Keys.F;
                case Keys.G:
                    return Stride.Input.Keys.G;
                case Keys.H:
                    return Stride.Input.Keys.H;
                case Keys.I:
                    return Stride.Input.Keys.I;
                case Keys.J:
                    return Stride.Input.Keys.J;
                case Keys.K:
                    return Stride.Input.Keys.K;
                case Keys.L:
                    return Stride.Input.Keys.L;
                case Keys.M:
                    return Stride.Input.Keys.M;
                case Keys.N:
                    return Stride.Input.Keys.N;
                case Keys.O:
                    return Stride.Input.Keys.O;
                case Keys.P:
                    return Stride.Input.Keys.P;
                case Keys.Q:
                    return Stride.Input.Keys.Q;
                case Keys.R:
                    return Stride.Input.Keys.R;
                case Keys.S:
                    return Stride.Input.Keys.S;
                case Keys.T:
                    return Stride.Input.Keys.T;
                case Keys.U:
                    return Stride.Input.Keys.U;
                case Keys.V:
                    return Stride.Input.Keys.V;
                case Keys.W:
                    return Stride.Input.Keys.W;
                case Keys.X:
                    return Stride.Input.Keys.X;
                case Keys.Y:
                    return Stride.Input.Keys.Y;
                case Keys.Z:
                    return Stride.Input.Keys.Z;
                case Keys.D0:
                    return Stride.Input.Keys.D0;
                case Keys.D1:
                    return Stride.Input.Keys.D1;
                case Keys.D2:
                    return Stride.Input.Keys.D2;
                case Keys.D3:
                    return Stride.Input.Keys.D3;
                case Keys.D4:
                    return Stride.Input.Keys.D4;
                case Keys.D5:
                    return Stride.Input.Keys.D5;
                case Keys.D6:
                    return Stride.Input.Keys.D6;
                case Keys.D7:
                    return Stride.Input.Keys.D7;
                case Keys.D8:
                    return Stride.Input.Keys.D8;
                case Keys.D9:
                    return Stride.Input.Keys.D9;
                case Keys.F1:
                    return Stride.Input.Keys.F1;
                case Keys.F2:
                    return Stride.Input.Keys.F2;
                case Keys.F3:
                    return Stride.Input.Keys.F3;
                case Keys.F4:
                    return Stride.Input.Keys.F4;
                case Keys.F5:
                    return Stride.Input.Keys.F5;
                case Keys.F6:
                    return Stride.Input.Keys.F6;
                case Keys.F7:
                    return Stride.Input.Keys.F7;
                case Keys.F8:
                    return Stride.Input.Keys.F8;
                case Keys.F9:
                    return Stride.Input.Keys.F9;
                case Keys.F10:
                    return Stride.Input.Keys.F10;
                case Keys.F11:
                    return Stride.Input.Keys.F11;
                case Keys.F12:
                    return Stride.Input.Keys.F12;
                case Keys.LeftControl:
                    return Stride.Input.Keys.LeftCtrl;
                case Keys.RightControl:
                    return Stride.Input.Keys.RightCtrl;
                case Keys.LeftShift:
                    return Stride.Input.Keys.LeftShift;
                case Keys.RightShift:
                    return Stride.Input.Keys.RightShift;
                case Keys.LeftAlt:
                    return Stride.Input.Keys.LeftAlt;
                case Keys.RightAlt:
                    return Stride.Input.Keys.RightAlt;
                default:
                    return Stride.Input.Keys.None;
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
        public Vector2 Position => new Vector2(_inputManager.MousePosition.X, _inputManager.MousePosition.Y);
        public int ScrollWheelValue => (int)_inputManager.MouseWheelDelta;

        public ButtonState LeftButton => _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Left) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState RightButton => _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Right) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState MiddleButton => _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Middle) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton1 => _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Extended1) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton2 => _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Extended2) ? ButtonState.Pressed : ButtonState.Released;

        public bool IsButtonDown(Andastra.Runtime.Graphics.MouseButton button)
        {
            switch (button)
            {
                case Andastra.Runtime.Graphics.MouseButton.Left:
                    return _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Left);
                case Andastra.Runtime.Graphics.MouseButton.Right:
                    return _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Right);
                case Andastra.Runtime.Graphics.MouseButton.Middle:
                    return _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Middle);
                case Andastra.Runtime.Graphics.MouseButton.XButton1:
                    return _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Extended1);
                case Andastra.Runtime.Graphics.MouseButton.XButton2:
                    return _inputManager.IsMouseButtonDown(Stride.Input.MouseButton.Extended2);
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

