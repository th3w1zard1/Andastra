using Microsoft.Xna.Framework.Input;
using BioWareEngines.Graphics;

namespace BioWareEngines.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IInputManager.
    /// </summary>
    public class MonoGameInputManager : IInputManager
    {
        private MonoGameKeyboardState _keyboardState;
        private MonoGameKeyboardState _previousKeyboardState;
        private MonoGameMouseState _mouseState;
        private MonoGameMouseState _previousMouseState;

        public MonoGameInputManager()
        {
            _keyboardState = new MonoGameKeyboardState(Keyboard.GetState());
            _previousKeyboardState = new MonoGameKeyboardState(Keyboard.GetState());
            _mouseState = new MonoGameMouseState(Mouse.GetState());
            _previousMouseState = new MonoGameMouseState(Mouse.GetState());
        }

        public IKeyboardState KeyboardState => _keyboardState;

        public IMouseState MouseState => _mouseState;

        public IKeyboardState PreviousKeyboardState => _previousKeyboardState;

        public IMouseState PreviousMouseState => _previousMouseState;

        public void Update()
        {
            _previousKeyboardState = _keyboardState;
            _previousMouseState = _mouseState;

            _keyboardState = new MonoGameKeyboardState(Keyboard.GetState());
            _mouseState = new MonoGameMouseState(Mouse.GetState());
        }
    }

    /// <summary>
    /// MonoGame implementation of IKeyboardState.
    /// </summary>
    public class MonoGameKeyboardState : IKeyboardState
    {
        private readonly KeyboardState _state;

        internal MonoGameKeyboardState(KeyboardState state)
        {
            _state = state;
        }

        public bool IsKeyDown(Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyDown(mgKey);
        }

        public bool IsKeyUp(Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyUp(mgKey);
        }

        public Keys[] GetPressedKeys()
        {
            var mgKeys = _state.GetPressedKeys();
            var keys = new Keys[mgKeys.Length];
            for (int i = 0; i < mgKeys.Length; i++)
            {
                keys[i] = ConvertKey(mgKeys[i]);
            }
            return keys;
        }

        private Keys ConvertKey(Microsoft.Xna.Framework.Input.Keys mgKey)
        {
            // Map MonoGame's Keys enum to our Keys enum
            switch (mgKey)
            {
                case Microsoft.Xna.Framework.Input.Keys.None:
                    return Keys.None;
                case Microsoft.Xna.Framework.Input.Keys.Back:
                    return Keys.Back;
                case Microsoft.Xna.Framework.Input.Keys.Tab:
                    return Keys.Tab;
                case Microsoft.Xna.Framework.Input.Keys.Enter:
                    return Keys.Enter;
                case Microsoft.Xna.Framework.Input.Keys.Escape:
                    return Keys.Escape;
                case Microsoft.Xna.Framework.Input.Keys.Space:
                    return Keys.Space;
                case Microsoft.Xna.Framework.Input.Keys.Up:
                    return Keys.Up;
                case Microsoft.Xna.Framework.Input.Keys.Down:
                    return Keys.Down;
                case Microsoft.Xna.Framework.Input.Keys.Left:
                    return Keys.Left;
                case Microsoft.Xna.Framework.Input.Keys.Right:
                    return Keys.Right;
                case Microsoft.Xna.Framework.Input.Keys.A:
                    return Keys.A;
                case Microsoft.Xna.Framework.Input.Keys.B:
                    return Keys.B;
                case Microsoft.Xna.Framework.Input.Keys.C:
                    return Keys.C;
                case Microsoft.Xna.Framework.Input.Keys.D:
                    return Keys.D;
                case Microsoft.Xna.Framework.Input.Keys.E:
                    return Keys.E;
                case Microsoft.Xna.Framework.Input.Keys.F:
                    return Keys.F;
                case Microsoft.Xna.Framework.Input.Keys.G:
                    return Keys.G;
                case Microsoft.Xna.Framework.Input.Keys.H:
                    return Keys.H;
                case Microsoft.Xna.Framework.Input.Keys.I:
                    return Keys.I;
                case Microsoft.Xna.Framework.Input.Keys.J:
                    return Keys.J;
                case Microsoft.Xna.Framework.Input.Keys.K:
                    return Keys.K;
                case Microsoft.Xna.Framework.Input.Keys.L:
                    return Keys.L;
                case Microsoft.Xna.Framework.Input.Keys.M:
                    return Keys.M;
                case Microsoft.Xna.Framework.Input.Keys.N:
                    return Keys.N;
                case Microsoft.Xna.Framework.Input.Keys.O:
                    return Keys.O;
                case Microsoft.Xna.Framework.Input.Keys.P:
                    return Keys.P;
                case Microsoft.Xna.Framework.Input.Keys.Q:
                    return Keys.Q;
                case Microsoft.Xna.Framework.Input.Keys.R:
                    return Keys.R;
                case Microsoft.Xna.Framework.Input.Keys.S:
                    return Keys.S;
                case Microsoft.Xna.Framework.Input.Keys.T:
                    return Keys.T;
                case Microsoft.Xna.Framework.Input.Keys.U:
                    return Keys.U;
                case Microsoft.Xna.Framework.Input.Keys.V:
                    return Keys.V;
                case Microsoft.Xna.Framework.Input.Keys.W:
                    return Keys.W;
                case Microsoft.Xna.Framework.Input.Keys.X:
                    return Keys.X;
                case Microsoft.Xna.Framework.Input.Keys.Y:
                    return Keys.Y;
                case Microsoft.Xna.Framework.Input.Keys.Z:
                    return Keys.Z;
                case Microsoft.Xna.Framework.Input.Keys.D0:
                    return Keys.D0;
                case Microsoft.Xna.Framework.Input.Keys.D1:
                    return Keys.D1;
                case Microsoft.Xna.Framework.Input.Keys.D2:
                    return Keys.D2;
                case Microsoft.Xna.Framework.Input.Keys.D3:
                    return Keys.D3;
                case Microsoft.Xna.Framework.Input.Keys.D4:
                    return Keys.D4;
                case Microsoft.Xna.Framework.Input.Keys.D5:
                    return Keys.D5;
                case Microsoft.Xna.Framework.Input.Keys.D6:
                    return Keys.D6;
                case Microsoft.Xna.Framework.Input.Keys.D7:
                    return Keys.D7;
                case Microsoft.Xna.Framework.Input.Keys.D8:
                    return Keys.D8;
                case Microsoft.Xna.Framework.Input.Keys.D9:
                    return Keys.D9;
                case Microsoft.Xna.Framework.Input.Keys.F1:
                    return Keys.F1;
                case Microsoft.Xna.Framework.Input.Keys.F2:
                    return Keys.F2;
                case Microsoft.Xna.Framework.Input.Keys.F3:
                    return Keys.F3;
                case Microsoft.Xna.Framework.Input.Keys.F4:
                    return Keys.F4;
                case Microsoft.Xna.Framework.Input.Keys.F5:
                    return Keys.F5;
                case Microsoft.Xna.Framework.Input.Keys.F6:
                    return Keys.F6;
                case Microsoft.Xna.Framework.Input.Keys.F7:
                    return Keys.F7;
                case Microsoft.Xna.Framework.Input.Keys.F8:
                    return Keys.F8;
                case Microsoft.Xna.Framework.Input.Keys.F9:
                    return Keys.F9;
                case Microsoft.Xna.Framework.Input.Keys.F10:
                    return Keys.F10;
                case Microsoft.Xna.Framework.Input.Keys.F11:
                    return Keys.F11;
                case Microsoft.Xna.Framework.Input.Keys.F12:
                    return Keys.F12;
                case Microsoft.Xna.Framework.Input.Keys.LeftControl:
                    return Keys.LeftControl;
                case Microsoft.Xna.Framework.Input.Keys.RightControl:
                    return Keys.RightControl;
                case Microsoft.Xna.Framework.Input.Keys.LeftShift:
                    return Keys.LeftShift;
                case Microsoft.Xna.Framework.Input.Keys.RightShift:
                    return Keys.RightShift;
                case Microsoft.Xna.Framework.Input.Keys.LeftAlt:
                    return Keys.LeftAlt;
                case Microsoft.Xna.Framework.Input.Keys.RightAlt:
                    return Keys.RightAlt;
                default:
                    return Keys.None;
            }
        }

        private Microsoft.Xna.Framework.Input.Keys ConvertKey(Keys key)
        {
            // Map our Keys enum to MonoGame's Keys enum
            switch (key)
            {
                case Keys.None:
                    return Microsoft.Xna.Framework.Input.Keys.None;
                case Keys.Back:
                    return Microsoft.Xna.Framework.Input.Keys.Back;
                case Keys.Tab:
                    return Microsoft.Xna.Framework.Input.Keys.Tab;
                case Keys.Enter:
                    return Microsoft.Xna.Framework.Input.Keys.Enter;
                case Keys.Escape:
                    return Microsoft.Xna.Framework.Input.Keys.Escape;
                case Keys.Space:
                    return Microsoft.Xna.Framework.Input.Keys.Space;
                case Keys.Up:
                    return Microsoft.Xna.Framework.Input.Keys.Up;
                case Keys.Down:
                    return Microsoft.Xna.Framework.Input.Keys.Down;
                case Keys.Left:
                    return Microsoft.Xna.Framework.Input.Keys.Left;
                case Keys.Right:
                    return Microsoft.Xna.Framework.Input.Keys.Right;
                case Keys.A:
                    return Microsoft.Xna.Framework.Input.Keys.A;
                case Keys.B:
                    return Microsoft.Xna.Framework.Input.Keys.B;
                case Keys.C:
                    return Microsoft.Xna.Framework.Input.Keys.C;
                case Keys.D:
                    return Microsoft.Xna.Framework.Input.Keys.D;
                case Keys.E:
                    return Microsoft.Xna.Framework.Input.Keys.E;
                case Keys.F:
                    return Microsoft.Xna.Framework.Input.Keys.F;
                case Keys.G:
                    return Microsoft.Xna.Framework.Input.Keys.G;
                case Keys.H:
                    return Microsoft.Xna.Framework.Input.Keys.H;
                case Keys.I:
                    return Microsoft.Xna.Framework.Input.Keys.I;
                case Keys.J:
                    return Microsoft.Xna.Framework.Input.Keys.J;
                case Keys.K:
                    return Microsoft.Xna.Framework.Input.Keys.K;
                case Keys.L:
                    return Microsoft.Xna.Framework.Input.Keys.L;
                case Keys.M:
                    return Microsoft.Xna.Framework.Input.Keys.M;
                case Keys.N:
                    return Microsoft.Xna.Framework.Input.Keys.N;
                case Keys.O:
                    return Microsoft.Xna.Framework.Input.Keys.O;
                case Keys.P:
                    return Microsoft.Xna.Framework.Input.Keys.P;
                case Keys.Q:
                    return Microsoft.Xna.Framework.Input.Keys.Q;
                case Keys.R:
                    return Microsoft.Xna.Framework.Input.Keys.R;
                case Keys.S:
                    return Microsoft.Xna.Framework.Input.Keys.S;
                case Keys.T:
                    return Microsoft.Xna.Framework.Input.Keys.T;
                case Keys.U:
                    return Microsoft.Xna.Framework.Input.Keys.U;
                case Keys.V:
                    return Microsoft.Xna.Framework.Input.Keys.V;
                case Keys.W:
                    return Microsoft.Xna.Framework.Input.Keys.W;
                case Keys.X:
                    return Microsoft.Xna.Framework.Input.Keys.X;
                case Keys.Y:
                    return Microsoft.Xna.Framework.Input.Keys.Y;
                case Keys.Z:
                    return Microsoft.Xna.Framework.Input.Keys.Z;
                case Keys.D0:
                    return Microsoft.Xna.Framework.Input.Keys.D0;
                case Keys.D1:
                    return Microsoft.Xna.Framework.Input.Keys.D1;
                case Keys.D2:
                    return Microsoft.Xna.Framework.Input.Keys.D2;
                case Keys.D3:
                    return Microsoft.Xna.Framework.Input.Keys.D3;
                case Keys.D4:
                    return Microsoft.Xna.Framework.Input.Keys.D4;
                case Keys.D5:
                    return Microsoft.Xna.Framework.Input.Keys.D5;
                case Keys.D6:
                    return Microsoft.Xna.Framework.Input.Keys.D6;
                case Keys.D7:
                    return Microsoft.Xna.Framework.Input.Keys.D7;
                case Keys.D8:
                    return Microsoft.Xna.Framework.Input.Keys.D8;
                case Keys.D9:
                    return Microsoft.Xna.Framework.Input.Keys.D9;
                case Keys.F1:
                    return Microsoft.Xna.Framework.Input.Keys.F1;
                case Keys.F2:
                    return Microsoft.Xna.Framework.Input.Keys.F2;
                case Keys.F3:
                    return Microsoft.Xna.Framework.Input.Keys.F3;
                case Keys.F4:
                    return Microsoft.Xna.Framework.Input.Keys.F4;
                case Keys.F5:
                    return Microsoft.Xna.Framework.Input.Keys.F5;
                case Keys.F6:
                    return Microsoft.Xna.Framework.Input.Keys.F6;
                case Keys.F7:
                    return Microsoft.Xna.Framework.Input.Keys.F7;
                case Keys.F8:
                    return Microsoft.Xna.Framework.Input.Keys.F8;
                case Keys.F9:
                    return Microsoft.Xna.Framework.Input.Keys.F9;
                case Keys.F10:
                    return Microsoft.Xna.Framework.Input.Keys.F10;
                case Keys.F11:
                    return Microsoft.Xna.Framework.Input.Keys.F11;
                case Keys.F12:
                    return Microsoft.Xna.Framework.Input.Keys.F12;
                case Keys.LeftControl:
                    return Microsoft.Xna.Framework.Input.Keys.LeftControl;
                case Keys.RightControl:
                    return Microsoft.Xna.Framework.Input.Keys.RightControl;
                case Keys.LeftShift:
                    return Microsoft.Xna.Framework.Input.Keys.LeftShift;
                case Keys.RightShift:
                    return Microsoft.Xna.Framework.Input.Keys.RightShift;
                case Keys.LeftAlt:
                    return Microsoft.Xna.Framework.Input.Keys.LeftAlt;
                case Keys.RightAlt:
                    return Microsoft.Xna.Framework.Input.Keys.RightAlt;
                default:
                    return Microsoft.Xna.Framework.Input.Keys.None;
            }
        }
    }

    /// <summary>
    /// MonoGame implementation of IMouseState.
    /// </summary>
    public class MonoGameMouseState : IMouseState
    {
        private readonly MouseState _state;

        internal MonoGameMouseState(MouseState state)
        {
            _state = state;
        }

        public int X => _state.X;
        public int Y => _state.Y;
        public Vector2 Position => new Vector2(_state.X, _state.Y);
        public int ScrollWheelValue => _state.ScrollWheelValue;

        public ButtonState LeftButton => _state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState RightButton => _state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState MiddleButton => _state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton1 => _state.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? ButtonState.Pressed : ButtonState.Released;
        public ButtonState XButton2 => _state.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? ButtonState.Pressed : ButtonState.Released;

        public bool IsButtonDown(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return _state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                case MouseButton.Right:
                    return _state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                case MouseButton.Middle:
                    return _state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                case MouseButton.XButton1:
                    return _state.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                case MouseButton.XButton2:
                    return _state.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
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

