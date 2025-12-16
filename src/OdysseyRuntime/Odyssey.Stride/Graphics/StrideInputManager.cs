using Stride.Input;
using Stride.Core.Mathematics;
using BioWareEngines.Graphics;

namespace BioWareEngines.Stride.Graphics
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

        public bool IsKeyDown(Odyssey.Graphics.Keys key)
        {
            var strideKey = ConvertKey(key);
            return _inputManager.IsKeyDown(strideKey);
        }

        public bool IsKeyUp(Odyssey.Graphics.Keys key)
        {
            var strideKey = ConvertKey(key);
            return _inputManager.IsKeyUp(strideKey);
        }

        public Odyssey.Graphics.Keys[] GetPressedKeys()
        {
            // Stride doesn't have a direct GetPressedKeys method
            // We'll need to check all keys manually
            var pressedKeys = new System.Collections.Generic.List<Odyssey.Graphics.Keys>();
            foreach (Odyssey.Graphics.Keys key in System.Enum.GetValues(typeof(Odyssey.Graphics.Keys)))
            {
                if (key != Odyssey.Graphics.Keys.None && IsKeyDown(key))
                {
                    pressedKeys.Add(key);
                }
            }
            return pressedKeys.ToArray();
        }

        private Stride.Input.Keys ConvertKey(Odyssey.Graphics.Keys key)
        {
            // Map our Keys enum to Stride's Keys enum
            switch (key)
            {
                case Odyssey.Graphics.Keys.None:
                    return Stride.Input.Keys.None;
                case Odyssey.Graphics.Keys.Back:
                    return Stride.Input.Keys.Back;
                case Odyssey.Graphics.Keys.Tab:
                    return Stride.Input.Keys.Tab;
                case Odyssey.Graphics.Keys.Enter:
                    return Stride.Input.Keys.Enter;
                case Odyssey.Graphics.Keys.Escape:
                    return Stride.Input.Keys.Escape;
                case Odyssey.Graphics.Keys.Space:
                    return Stride.Input.Keys.Space;
                case Odyssey.Graphics.Keys.Up:
                    return Stride.Input.Keys.Up;
                case Odyssey.Graphics.Keys.Down:
                    return Stride.Input.Keys.Down;
                case Odyssey.Graphics.Keys.Left:
                    return Stride.Input.Keys.Left;
                case Odyssey.Graphics.Keys.Right:
                    return Stride.Input.Keys.Right;
                case Odyssey.Graphics.Keys.A:
                    return Stride.Input.Keys.A;
                case Odyssey.Graphics.Keys.B:
                    return Stride.Input.Keys.B;
                case Odyssey.Graphics.Keys.C:
                    return Stride.Input.Keys.C;
                case Odyssey.Graphics.Keys.D:
                    return Stride.Input.Keys.D;
                case Odyssey.Graphics.Keys.E:
                    return Stride.Input.Keys.E;
                case Odyssey.Graphics.Keys.F:
                    return Stride.Input.Keys.F;
                case Odyssey.Graphics.Keys.G:
                    return Stride.Input.Keys.G;
                case Odyssey.Graphics.Keys.H:
                    return Stride.Input.Keys.H;
                case Odyssey.Graphics.Keys.I:
                    return Stride.Input.Keys.I;
                case Odyssey.Graphics.Keys.J:
                    return Stride.Input.Keys.J;
                case Odyssey.Graphics.Keys.K:
                    return Stride.Input.Keys.K;
                case Odyssey.Graphics.Keys.L:
                    return Stride.Input.Keys.L;
                case Odyssey.Graphics.Keys.M:
                    return Stride.Input.Keys.M;
                case Odyssey.Graphics.Keys.N:
                    return Stride.Input.Keys.N;
                case Odyssey.Graphics.Keys.O:
                    return Stride.Input.Keys.O;
                case Odyssey.Graphics.Keys.P:
                    return Stride.Input.Keys.P;
                case Odyssey.Graphics.Keys.Q:
                    return Stride.Input.Keys.Q;
                case Odyssey.Graphics.Keys.R:
                    return Stride.Input.Keys.R;
                case Odyssey.Graphics.Keys.S:
                    return Stride.Input.Keys.S;
                case Odyssey.Graphics.Keys.T:
                    return Stride.Input.Keys.T;
                case Odyssey.Graphics.Keys.U:
                    return Stride.Input.Keys.U;
                case Odyssey.Graphics.Keys.V:
                    return Stride.Input.Keys.V;
                case Odyssey.Graphics.Keys.W:
                    return Stride.Input.Keys.W;
                case Odyssey.Graphics.Keys.X:
                    return Stride.Input.Keys.X;
                case Odyssey.Graphics.Keys.Y:
                    return Stride.Input.Keys.Y;
                case Odyssey.Graphics.Keys.Z:
                    return Stride.Input.Keys.Z;
                case Odyssey.Graphics.Keys.D0:
                    return Stride.Input.Keys.D0;
                case Odyssey.Graphics.Keys.D1:
                    return Stride.Input.Keys.D1;
                case Odyssey.Graphics.Keys.D2:
                    return Stride.Input.Keys.D2;
                case Odyssey.Graphics.Keys.D3:
                    return Stride.Input.Keys.D3;
                case Odyssey.Graphics.Keys.D4:
                    return Stride.Input.Keys.D4;
                case Odyssey.Graphics.Keys.D5:
                    return Stride.Input.Keys.D5;
                case Odyssey.Graphics.Keys.D6:
                    return Stride.Input.Keys.D6;
                case Odyssey.Graphics.Keys.D7:
                    return Stride.Input.Keys.D7;
                case Odyssey.Graphics.Keys.D8:
                    return Stride.Input.Keys.D8;
                case Odyssey.Graphics.Keys.D9:
                    return Stride.Input.Keys.D9;
                case Odyssey.Graphics.Keys.F1:
                    return Stride.Input.Keys.F1;
                case Odyssey.Graphics.Keys.F2:
                    return Stride.Input.Keys.F2;
                case Odyssey.Graphics.Keys.F3:
                    return Stride.Input.Keys.F3;
                case Odyssey.Graphics.Keys.F4:
                    return Stride.Input.Keys.F4;
                case Odyssey.Graphics.Keys.F5:
                    return Stride.Input.Keys.F5;
                case Odyssey.Graphics.Keys.F6:
                    return Stride.Input.Keys.F6;
                case Odyssey.Graphics.Keys.F7:
                    return Stride.Input.Keys.F7;
                case Odyssey.Graphics.Keys.F8:
                    return Stride.Input.Keys.F8;
                case Odyssey.Graphics.Keys.F9:
                    return Stride.Input.Keys.F9;
                case Odyssey.Graphics.Keys.F10:
                    return Stride.Input.Keys.F10;
                case Odyssey.Graphics.Keys.F11:
                    return Stride.Input.Keys.F11;
                case Odyssey.Graphics.Keys.F12:
                    return Stride.Input.Keys.F12;
                case Odyssey.Graphics.Keys.LeftControl:
                    return Stride.Input.Keys.LeftCtrl;
                case Odyssey.Graphics.Keys.RightControl:
                    return Stride.Input.Keys.RightCtrl;
                case Odyssey.Graphics.Keys.LeftShift:
                    return Stride.Input.Keys.LeftShift;
                case Odyssey.Graphics.Keys.RightShift:
                    return Stride.Input.Keys.RightShift;
                case Odyssey.Graphics.Keys.LeftAlt:
                    return Stride.Input.Keys.LeftAlt;
                case Odyssey.Graphics.Keys.RightAlt:
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
        public Odyssey.Graphics.Vector2 Position => new Odyssey.Graphics.Vector2(_inputManager.MousePosition.X, _inputManager.MousePosition.Y);
        public int ScrollWheelValue => (int)_inputManager.MouseWheelDelta;

        public ButtonState LeftButton => _inputManager.IsMouseButtonDown(MouseButton.Left) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState RightButton => _inputManager.IsMouseButtonDown(MouseButton.Right) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState MiddleButton => _inputManager.IsMouseButtonDown(MouseButton.Middle) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton1 => _inputManager.IsMouseButtonDown(MouseButton.Extended1) ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton2 => _inputManager.IsMouseButtonDown(MouseButton.Extended2) ? ButtonState.Pressed : ButtonState.Released;

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

