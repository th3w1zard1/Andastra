using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Andastra.Game.Graphics.MonoGame.Graphics
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

        public bool IsKeyDown(Runtime.Graphics.Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyDown(mgKey);
        }

        public bool IsKeyUp(Runtime.Graphics.Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyUp(mgKey);
        }

        public Runtime.Graphics.Keys[] GetPressedKeys()
        {
            var mgKeys = _state.GetPressedKeys();
            var keys = new Runtime.Graphics.Keys[mgKeys.Length];
            for (int i = 0; i < mgKeys.Length; i++)
            {
                keys[i] = ConvertKey(mgKeys[i]);
            }
            return keys;
        }

        private Runtime.Graphics.Keys ConvertKey(Microsoft.Xna.Framework.Input.Keys mgKey)
        {
            // Map MonoGame's Keys enum to our Keys enum
            switch (mgKey)
            {
                case Microsoft.Xna.Framework.Input.Keys.None:
                    return Runtime.Graphics.Keys.None;
                case Microsoft.Xna.Framework.Input.Keys.Back:
                    return Runtime.Graphics.Keys.Back;
                case Microsoft.Xna.Framework.Input.Keys.Tab:
                    return Runtime.Graphics.Keys.Tab;
                case Microsoft.Xna.Framework.Input.Keys.Enter:
                    return Runtime.Graphics.Keys.Enter;
                case Microsoft.Xna.Framework.Input.Keys.Escape:
                    return Runtime.Graphics.Keys.Escape;
                case Microsoft.Xna.Framework.Input.Keys.Space:
                    return Runtime.Graphics.Keys.Space;
                case Microsoft.Xna.Framework.Input.Keys.Up:
                    return Runtime.Graphics.Keys.Up;
                case Microsoft.Xna.Framework.Input.Keys.Down:
                    return Runtime.Graphics.Keys.Down;
                case Microsoft.Xna.Framework.Input.Keys.Left:
                    return Runtime.Graphics.Keys.Left;
                case Microsoft.Xna.Framework.Input.Keys.Right:
                    return Runtime.Graphics.Keys.Right;
                case Microsoft.Xna.Framework.Input.Keys.A:
                    return Runtime.Graphics.Keys.A;
                case Microsoft.Xna.Framework.Input.Keys.B:
                    return Runtime.Graphics.Keys.B;
                case Microsoft.Xna.Framework.Input.Keys.C:
                    return Runtime.Graphics.Keys.C;
                case Microsoft.Xna.Framework.Input.Keys.D:
                    return Runtime.Graphics.Keys.D;
                case Microsoft.Xna.Framework.Input.Keys.E:
                    return Runtime.Graphics.Keys.E;
                case Microsoft.Xna.Framework.Input.Keys.F:
                    return Runtime.Graphics.Keys.F;
                case Microsoft.Xna.Framework.Input.Keys.G:
                    return Runtime.Graphics.Keys.G;
                case Microsoft.Xna.Framework.Input.Keys.H:
                    return Runtime.Graphics.Keys.H;
                case Microsoft.Xna.Framework.Input.Keys.I:
                    return Runtime.Graphics.Keys.I;
                case Microsoft.Xna.Framework.Input.Keys.J:
                    return Runtime.Graphics.Keys.J;
                case Microsoft.Xna.Framework.Input.Keys.K:
                    return Runtime.Graphics.Keys.K;
                case Microsoft.Xna.Framework.Input.Keys.L:
                    return Runtime.Graphics.Keys.L;
                case Microsoft.Xna.Framework.Input.Keys.M:
                    return Runtime.Graphics.Keys.M;
                case Microsoft.Xna.Framework.Input.Keys.N:
                    return Runtime.Graphics.Keys.N;
                case Microsoft.Xna.Framework.Input.Keys.O:
                    return Runtime.Graphics.Keys.O;
                case Microsoft.Xna.Framework.Input.Keys.P:
                    return Runtime.Graphics.Keys.P;
                case Microsoft.Xna.Framework.Input.Keys.Q:
                    return Runtime.Graphics.Keys.Q;
                case Microsoft.Xna.Framework.Input.Keys.R:
                    return Runtime.Graphics.Keys.R;
                case Microsoft.Xna.Framework.Input.Keys.S:
                    return Runtime.Graphics.Keys.S;
                case Microsoft.Xna.Framework.Input.Keys.T:
                    return Runtime.Graphics.Keys.T;
                case Microsoft.Xna.Framework.Input.Keys.U:
                    return Runtime.Graphics.Keys.U;
                case Microsoft.Xna.Framework.Input.Keys.V:
                    return Runtime.Graphics.Keys.V;
                case Microsoft.Xna.Framework.Input.Keys.W:
                    return Runtime.Graphics.Keys.W;
                case Microsoft.Xna.Framework.Input.Keys.X:
                    return Runtime.Graphics.Keys.X;
                case Microsoft.Xna.Framework.Input.Keys.Y:
                    return Runtime.Graphics.Keys.Y;
                case Microsoft.Xna.Framework.Input.Keys.Z:
                    return Runtime.Graphics.Keys.Z;
                case Microsoft.Xna.Framework.Input.Keys.D0:
                    return Runtime.Graphics.Keys.D0;
                case Microsoft.Xna.Framework.Input.Keys.D1:
                    return Runtime.Graphics.Keys.D1;
                case Microsoft.Xna.Framework.Input.Keys.D2:
                    return Runtime.Graphics.Keys.D2;
                case Microsoft.Xna.Framework.Input.Keys.D3:
                    return Runtime.Graphics.Keys.D3;
                case Microsoft.Xna.Framework.Input.Keys.D4:
                    return Runtime.Graphics.Keys.D4;
                case Microsoft.Xna.Framework.Input.Keys.D5:
                    return Runtime.Graphics.Keys.D5;
                case Microsoft.Xna.Framework.Input.Keys.D6:
                    return Runtime.Graphics.Keys.D6;
                case Microsoft.Xna.Framework.Input.Keys.D7:
                    return Runtime.Graphics.Keys.D7;
                case Microsoft.Xna.Framework.Input.Keys.D8:
                    return Runtime.Graphics.Keys.D8;
                case Microsoft.Xna.Framework.Input.Keys.D9:
                    return Runtime.Graphics.Keys.D9;
                case Microsoft.Xna.Framework.Input.Keys.F1:
                    return Runtime.Graphics.Keys.F1;
                case Microsoft.Xna.Framework.Input.Keys.F2:
                    return Runtime.Graphics.Keys.F2;
                case Microsoft.Xna.Framework.Input.Keys.F3:
                    return Runtime.Graphics.Keys.F3;
                case Microsoft.Xna.Framework.Input.Keys.F4:
                    return Runtime.Graphics.Keys.F4;
                case Microsoft.Xna.Framework.Input.Keys.F5:
                    return Runtime.Graphics.Keys.F5;
                case Microsoft.Xna.Framework.Input.Keys.F6:
                    return Runtime.Graphics.Keys.F6;
                case Microsoft.Xna.Framework.Input.Keys.F7:
                    return Runtime.Graphics.Keys.F7;
                case Microsoft.Xna.Framework.Input.Keys.F8:
                    return Runtime.Graphics.Keys.F8;
                case Microsoft.Xna.Framework.Input.Keys.F9:
                    return Runtime.Graphics.Keys.F9;
                case Microsoft.Xna.Framework.Input.Keys.F10:
                    return Runtime.Graphics.Keys.F10;
                case Microsoft.Xna.Framework.Input.Keys.F11:
                    return Runtime.Graphics.Keys.F11;
                case Microsoft.Xna.Framework.Input.Keys.F12:
                    return Runtime.Graphics.Keys.F12;
                case Microsoft.Xna.Framework.Input.Keys.LeftControl:
                    return Runtime.Graphics.Keys.LeftControl;
                case Microsoft.Xna.Framework.Input.Keys.RightControl:
                    return Runtime.Graphics.Keys.RightControl;
                case Microsoft.Xna.Framework.Input.Keys.LeftShift:
                    return Runtime.Graphics.Keys.LeftShift;
                case Microsoft.Xna.Framework.Input.Keys.RightShift:
                    return Runtime.Graphics.Keys.RightShift;
                case Microsoft.Xna.Framework.Input.Keys.LeftAlt:
                    return Runtime.Graphics.Keys.LeftAlt;
                case Microsoft.Xna.Framework.Input.Keys.RightAlt:
                    return Runtime.Graphics.Keys.RightAlt;
                default:
                    return Runtime.Graphics.Keys.None;
            }
        }

        private Microsoft.Xna.Framework.Input.Keys ConvertKey(Runtime.Graphics.Keys key)
        {
            // Map our Keys enum to MonoGame's Keys enum
            switch (key)
            {
                case Runtime.Graphics.Keys.None:
                    return Microsoft.Xna.Framework.Input.Keys.None;
                case Runtime.Graphics.Keys.Back:
                    return Microsoft.Xna.Framework.Input.Keys.Back;
                case Runtime.Graphics.Keys.Tab:
                    return Microsoft.Xna.Framework.Input.Keys.Tab;
                case Runtime.Graphics.Keys.Enter:
                    return Microsoft.Xna.Framework.Input.Keys.Enter;
                case Runtime.Graphics.Keys.Escape:
                    return Microsoft.Xna.Framework.Input.Keys.Escape;
                case Runtime.Graphics.Keys.Space:
                    return Microsoft.Xna.Framework.Input.Keys.Space;
                case Runtime.Graphics.Keys.Up:
                    return Microsoft.Xna.Framework.Input.Keys.Up;
                case Runtime.Graphics.Keys.Down:
                    return Microsoft.Xna.Framework.Input.Keys.Down;
                case Runtime.Graphics.Keys.Left:
                    return Microsoft.Xna.Framework.Input.Keys.Left;
                case Runtime.Graphics.Keys.Right:
                    return Microsoft.Xna.Framework.Input.Keys.Right;
                case Runtime.Graphics.Keys.A:
                    return Microsoft.Xna.Framework.Input.Keys.A;
                case Runtime.Graphics.Keys.B:
                    return Microsoft.Xna.Framework.Input.Keys.B;
                case Runtime.Graphics.Keys.C:
                    return Microsoft.Xna.Framework.Input.Keys.C;
                case Runtime.Graphics.Keys.D:
                    return Microsoft.Xna.Framework.Input.Keys.D;
                case Runtime.Graphics.Keys.E:
                    return Microsoft.Xna.Framework.Input.Keys.E;
                case Runtime.Graphics.Keys.F:
                    return Microsoft.Xna.Framework.Input.Keys.F;
                case Runtime.Graphics.Keys.G:
                    return Microsoft.Xna.Framework.Input.Keys.G;
                case Runtime.Graphics.Keys.H:
                    return Microsoft.Xna.Framework.Input.Keys.H;
                case Runtime.Graphics.Keys.I:
                    return Microsoft.Xna.Framework.Input.Keys.I;
                case Runtime.Graphics.Keys.J:
                    return Microsoft.Xna.Framework.Input.Keys.J;
                case Runtime.Graphics.Keys.K:
                    return Microsoft.Xna.Framework.Input.Keys.K;
                case Runtime.Graphics.Keys.L:
                    return Microsoft.Xna.Framework.Input.Keys.L;
                case Runtime.Graphics.Keys.M:
                    return Microsoft.Xna.Framework.Input.Keys.M;
                case Runtime.Graphics.Keys.N:
                    return Microsoft.Xna.Framework.Input.Keys.N;
                case Runtime.Graphics.Keys.O:
                    return Microsoft.Xna.Framework.Input.Keys.O;
                case Runtime.Graphics.Keys.P:
                    return Microsoft.Xna.Framework.Input.Keys.P;
                case Runtime.Graphics.Keys.Q:
                    return Microsoft.Xna.Framework.Input.Keys.Q;
                case Runtime.Graphics.Keys.R:
                    return Microsoft.Xna.Framework.Input.Keys.R;
                case Runtime.Graphics.Keys.S:
                    return Microsoft.Xna.Framework.Input.Keys.S;
                case Runtime.Graphics.Keys.T:
                    return Microsoft.Xna.Framework.Input.Keys.T;
                case Runtime.Graphics.Keys.U:
                    return Microsoft.Xna.Framework.Input.Keys.U;
                case Runtime.Graphics.Keys.V:
                    return Microsoft.Xna.Framework.Input.Keys.V;
                case Runtime.Graphics.Keys.W:
                    return Microsoft.Xna.Framework.Input.Keys.W;
                case Runtime.Graphics.Keys.X:
                    return Microsoft.Xna.Framework.Input.Keys.X;
                case Runtime.Graphics.Keys.Y:
                    return Microsoft.Xna.Framework.Input.Keys.Y;
                case Runtime.Graphics.Keys.Z:
                    return Microsoft.Xna.Framework.Input.Keys.Z;
                case Runtime.Graphics.Keys.D0:
                    return Microsoft.Xna.Framework.Input.Keys.D0;
                case Runtime.Graphics.Keys.D1:
                    return Microsoft.Xna.Framework.Input.Keys.D1;
                case Runtime.Graphics.Keys.D2:
                    return Microsoft.Xna.Framework.Input.Keys.D2;
                case Runtime.Graphics.Keys.D3:
                    return Microsoft.Xna.Framework.Input.Keys.D3;
                case Runtime.Graphics.Keys.D4:
                    return Microsoft.Xna.Framework.Input.Keys.D4;
                case Runtime.Graphics.Keys.D5:
                    return Microsoft.Xna.Framework.Input.Keys.D5;
                case Runtime.Graphics.Keys.D6:
                    return Microsoft.Xna.Framework.Input.Keys.D6;
                case Runtime.Graphics.Keys.D7:
                    return Microsoft.Xna.Framework.Input.Keys.D7;
                case Runtime.Graphics.Keys.D8:
                    return Microsoft.Xna.Framework.Input.Keys.D8;
                case Runtime.Graphics.Keys.D9:
                    return Microsoft.Xna.Framework.Input.Keys.D9;
                case Runtime.Graphics.Keys.F1:
                    return Microsoft.Xna.Framework.Input.Keys.F1;
                case Runtime.Graphics.Keys.F2:
                    return Microsoft.Xna.Framework.Input.Keys.F2;
                case Runtime.Graphics.Keys.F3:
                    return Microsoft.Xna.Framework.Input.Keys.F3;
                case Runtime.Graphics.Keys.F4:
                    return Microsoft.Xna.Framework.Input.Keys.F4;
                case Runtime.Graphics.Keys.F5:
                    return Microsoft.Xna.Framework.Input.Keys.F5;
                case Runtime.Graphics.Keys.F6:
                    return Microsoft.Xna.Framework.Input.Keys.F6;
                case Runtime.Graphics.Keys.F7:
                    return Microsoft.Xna.Framework.Input.Keys.F7;
                case Runtime.Graphics.Keys.F8:
                    return Microsoft.Xna.Framework.Input.Keys.F8;
                case Runtime.Graphics.Keys.F9:
                    return Microsoft.Xna.Framework.Input.Keys.F9;
                case Runtime.Graphics.Keys.F10:
                    return Microsoft.Xna.Framework.Input.Keys.F10;
                case Runtime.Graphics.Keys.F11:
                    return Microsoft.Xna.Framework.Input.Keys.F11;
                case Runtime.Graphics.Keys.F12:
                    return Microsoft.Xna.Framework.Input.Keys.F12;
                case Runtime.Graphics.Keys.LeftControl:
                    return Microsoft.Xna.Framework.Input.Keys.LeftControl;
                case Runtime.Graphics.Keys.RightControl:
                    return Microsoft.Xna.Framework.Input.Keys.RightControl;
                case Runtime.Graphics.Keys.LeftShift:
                    return Microsoft.Xna.Framework.Input.Keys.LeftShift;
                case Runtime.Graphics.Keys.RightShift:
                    return Microsoft.Xna.Framework.Input.Keys.RightShift;
                case Runtime.Graphics.Keys.LeftAlt:
                    return Microsoft.Xna.Framework.Input.Keys.LeftAlt;
                case Runtime.Graphics.Keys.RightAlt:
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

        public Andastra.Runtime.Graphics.ButtonState LeftButton => _state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState RightButton => _state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState MiddleButton => _state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState XButton1 => _state.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState XButton2 => _state.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Runtime.Graphics.ButtonState.Released;

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

