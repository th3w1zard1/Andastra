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

        public bool IsKeyDown(Andastra.Runtime.Graphics.Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyDown(mgKey);
        }

        public bool IsKeyUp(Andastra.Runtime.Graphics.Keys key)
        {
            var mgKey = ConvertKey(key);
            return _state.IsKeyUp(mgKey);
        }

        public Andastra.Runtime.Graphics.Keys[] GetPressedKeys()
        {
            var mgKeys = _state.GetPressedKeys();
            var keys = new Andastra.Runtime.Graphics.Keys[mgKeys.Length];
            for (int i = 0; i < mgKeys.Length; i++)
            {
                keys[i] = ConvertKey(mgKeys[i]);
            }
            return keys;
        }

        private Andastra.Runtime.Graphics.Keys ConvertKey(Microsoft.Xna.Framework.Input.Keys mgKey)
        {
            // Map MonoGame's Keys enum to our Keys enum
            switch (mgKey)
            {
                case Microsoft.Xna.Framework.Input.Keys.None:
                    return Andastra.Runtime.Graphics.Keys.None;
                case Microsoft.Xna.Framework.Input.Keys.Back:
                    return Andastra.Runtime.Graphics.Keys.Back;
                case Microsoft.Xna.Framework.Input.Keys.Tab:
                    return Andastra.Runtime.Graphics.Keys.Tab;
                case Microsoft.Xna.Framework.Input.Keys.Enter:
                    return Andastra.Runtime.Graphics.Keys.Enter;
                case Microsoft.Xna.Framework.Input.Keys.Escape:
                    return Andastra.Runtime.Graphics.Keys.Escape;
                case Microsoft.Xna.Framework.Input.Keys.Space:
                    return Andastra.Runtime.Graphics.Keys.Space;
                case Microsoft.Xna.Framework.Input.Keys.Up:
                    return Andastra.Runtime.Graphics.Keys.Up;
                case Microsoft.Xna.Framework.Input.Keys.Down:
                    return Andastra.Runtime.Graphics.Keys.Down;
                case Microsoft.Xna.Framework.Input.Keys.Left:
                    return Andastra.Runtime.Graphics.Keys.Left;
                case Microsoft.Xna.Framework.Input.Keys.Right:
                    return Andastra.Runtime.Graphics.Keys.Right;
                case Microsoft.Xna.Framework.Input.Keys.A:
                    return Andastra.Runtime.Graphics.Keys.A;
                case Microsoft.Xna.Framework.Input.Keys.B:
                    return Andastra.Runtime.Graphics.Keys.B;
                case Microsoft.Xna.Framework.Input.Keys.C:
                    return Andastra.Runtime.Graphics.Keys.C;
                case Microsoft.Xna.Framework.Input.Keys.D:
                    return Andastra.Runtime.Graphics.Keys.D;
                case Microsoft.Xna.Framework.Input.Keys.E:
                    return Andastra.Runtime.Graphics.Keys.E;
                case Microsoft.Xna.Framework.Input.Keys.F:
                    return Andastra.Runtime.Graphics.Keys.F;
                case Microsoft.Xna.Framework.Input.Keys.G:
                    return Andastra.Runtime.Graphics.Keys.G;
                case Microsoft.Xna.Framework.Input.Keys.H:
                    return Andastra.Runtime.Graphics.Keys.H;
                case Microsoft.Xna.Framework.Input.Keys.I:
                    return Andastra.Runtime.Graphics.Keys.I;
                case Microsoft.Xna.Framework.Input.Keys.J:
                    return Andastra.Runtime.Graphics.Keys.J;
                case Microsoft.Xna.Framework.Input.Keys.K:
                    return Andastra.Runtime.Graphics.Keys.K;
                case Microsoft.Xna.Framework.Input.Keys.L:
                    return Andastra.Runtime.Graphics.Keys.L;
                case Microsoft.Xna.Framework.Input.Keys.M:
                    return Andastra.Runtime.Graphics.Keys.M;
                case Microsoft.Xna.Framework.Input.Keys.N:
                    return Andastra.Runtime.Graphics.Keys.N;
                case Microsoft.Xna.Framework.Input.Keys.O:
                    return Andastra.Runtime.Graphics.Keys.O;
                case Microsoft.Xna.Framework.Input.Keys.P:
                    return Andastra.Runtime.Graphics.Keys.P;
                case Microsoft.Xna.Framework.Input.Keys.Q:
                    return Andastra.Runtime.Graphics.Keys.Q;
                case Microsoft.Xna.Framework.Input.Keys.R:
                    return Andastra.Runtime.Graphics.Keys.R;
                case Microsoft.Xna.Framework.Input.Keys.S:
                    return Andastra.Runtime.Graphics.Keys.S;
                case Microsoft.Xna.Framework.Input.Keys.T:
                    return Andastra.Runtime.Graphics.Keys.T;
                case Microsoft.Xna.Framework.Input.Keys.U:
                    return Andastra.Runtime.Graphics.Keys.U;
                case Microsoft.Xna.Framework.Input.Keys.V:
                    return Andastra.Runtime.Graphics.Keys.V;
                case Microsoft.Xna.Framework.Input.Keys.W:
                    return Andastra.Runtime.Graphics.Keys.W;
                case Microsoft.Xna.Framework.Input.Keys.X:
                    return Andastra.Runtime.Graphics.Keys.X;
                case Microsoft.Xna.Framework.Input.Keys.Y:
                    return Andastra.Runtime.Graphics.Keys.Y;
                case Microsoft.Xna.Framework.Input.Keys.Z:
                    return Andastra.Runtime.Graphics.Keys.Z;
                case Microsoft.Xna.Framework.Input.Keys.D0:
                    return Andastra.Runtime.Graphics.Keys.D0;
                case Microsoft.Xna.Framework.Input.Keys.D1:
                    return Andastra.Runtime.Graphics.Keys.D1;
                case Microsoft.Xna.Framework.Input.Keys.D2:
                    return Andastra.Runtime.Graphics.Keys.D2;
                case Microsoft.Xna.Framework.Input.Keys.D3:
                    return Andastra.Runtime.Graphics.Keys.D3;
                case Microsoft.Xna.Framework.Input.Keys.D4:
                    return Andastra.Runtime.Graphics.Keys.D4;
                case Microsoft.Xna.Framework.Input.Keys.D5:
                    return Andastra.Runtime.Graphics.Keys.D5;
                case Microsoft.Xna.Framework.Input.Keys.D6:
                    return Andastra.Runtime.Graphics.Keys.D6;
                case Microsoft.Xna.Framework.Input.Keys.D7:
                    return Andastra.Runtime.Graphics.Keys.D7;
                case Microsoft.Xna.Framework.Input.Keys.D8:
                    return Andastra.Runtime.Graphics.Keys.D8;
                case Microsoft.Xna.Framework.Input.Keys.D9:
                    return Andastra.Runtime.Graphics.Keys.D9;
                case Microsoft.Xna.Framework.Input.Keys.F1:
                    return Andastra.Runtime.Graphics.Keys.F1;
                case Microsoft.Xna.Framework.Input.Keys.F2:
                    return Andastra.Runtime.Graphics.Keys.F2;
                case Microsoft.Xna.Framework.Input.Keys.F3:
                    return Andastra.Runtime.Graphics.Keys.F3;
                case Microsoft.Xna.Framework.Input.Keys.F4:
                    return Andastra.Runtime.Graphics.Keys.F4;
                case Microsoft.Xna.Framework.Input.Keys.F5:
                    return Andastra.Runtime.Graphics.Keys.F5;
                case Microsoft.Xna.Framework.Input.Keys.F6:
                    return Andastra.Runtime.Graphics.Keys.F6;
                case Microsoft.Xna.Framework.Input.Keys.F7:
                    return Andastra.Runtime.Graphics.Keys.F7;
                case Microsoft.Xna.Framework.Input.Keys.F8:
                    return Andastra.Runtime.Graphics.Keys.F8;
                case Microsoft.Xna.Framework.Input.Keys.F9:
                    return Andastra.Runtime.Graphics.Keys.F9;
                case Microsoft.Xna.Framework.Input.Keys.F10:
                    return Andastra.Runtime.Graphics.Keys.F10;
                case Microsoft.Xna.Framework.Input.Keys.F11:
                    return Andastra.Runtime.Graphics.Keys.F11;
                case Microsoft.Xna.Framework.Input.Keys.F12:
                    return Andastra.Runtime.Graphics.Keys.F12;
                case Microsoft.Xna.Framework.Input.Keys.LeftControl:
                    return Andastra.Runtime.Graphics.Keys.LeftControl;
                case Microsoft.Xna.Framework.Input.Keys.RightControl:
                    return Andastra.Runtime.Graphics.Keys.RightControl;
                case Microsoft.Xna.Framework.Input.Keys.LeftShift:
                    return Andastra.Runtime.Graphics.Keys.LeftShift;
                case Microsoft.Xna.Framework.Input.Keys.RightShift:
                    return Andastra.Runtime.Graphics.Keys.RightShift;
                case Microsoft.Xna.Framework.Input.Keys.LeftAlt:
                    return Andastra.Runtime.Graphics.Keys.LeftAlt;
                case Microsoft.Xna.Framework.Input.Keys.RightAlt:
                    return Andastra.Runtime.Graphics.Keys.RightAlt;
                default:
                    return Andastra.Runtime.Graphics.Keys.None;
            }
        }

        private Microsoft.Xna.Framework.Input.Keys ConvertKey(Andastra.Runtime.Graphics.Keys key)
        {
            // Map our Keys enum to MonoGame's Keys enum
            switch (key)
            {
                case Andastra.Runtime.Graphics.Keys.None:
                    return Microsoft.Xna.Framework.Input.Keys.None;
                case Andastra.Runtime.Graphics.Keys.Back:
                    return Microsoft.Xna.Framework.Input.Keys.Back;
                case Andastra.Runtime.Graphics.Keys.Tab:
                    return Microsoft.Xna.Framework.Input.Keys.Tab;
                case Andastra.Runtime.Graphics.Keys.Enter:
                    return Microsoft.Xna.Framework.Input.Keys.Enter;
                case Andastra.Runtime.Graphics.Keys.Escape:
                    return Microsoft.Xna.Framework.Input.Keys.Escape;
                case Andastra.Runtime.Graphics.Keys.Space:
                    return Microsoft.Xna.Framework.Input.Keys.Space;
                case Andastra.Runtime.Graphics.Keys.Up:
                    return Microsoft.Xna.Framework.Input.Keys.Up;
                case Andastra.Runtime.Graphics.Keys.Down:
                    return Microsoft.Xna.Framework.Input.Keys.Down;
                case Andastra.Runtime.Graphics.Keys.Left:
                    return Microsoft.Xna.Framework.Input.Keys.Left;
                case Andastra.Runtime.Graphics.Keys.Right:
                    return Microsoft.Xna.Framework.Input.Keys.Right;
                case Andastra.Runtime.Graphics.Keys.A:
                    return Microsoft.Xna.Framework.Input.Keys.A;
                case Andastra.Runtime.Graphics.Keys.B:
                    return Microsoft.Xna.Framework.Input.Keys.B;
                case Andastra.Runtime.Graphics.Keys.C:
                    return Microsoft.Xna.Framework.Input.Keys.C;
                case Andastra.Runtime.Graphics.Keys.D:
                    return Microsoft.Xna.Framework.Input.Keys.D;
                case Andastra.Runtime.Graphics.Keys.E:
                    return Microsoft.Xna.Framework.Input.Keys.E;
                case Andastra.Runtime.Graphics.Keys.F:
                    return Microsoft.Xna.Framework.Input.Keys.F;
                case Andastra.Runtime.Graphics.Keys.G:
                    return Microsoft.Xna.Framework.Input.Keys.G;
                case Andastra.Runtime.Graphics.Keys.H:
                    return Microsoft.Xna.Framework.Input.Keys.H;
                case Andastra.Runtime.Graphics.Keys.I:
                    return Microsoft.Xna.Framework.Input.Keys.I;
                case Andastra.Runtime.Graphics.Keys.J:
                    return Microsoft.Xna.Framework.Input.Keys.J;
                case Andastra.Runtime.Graphics.Keys.K:
                    return Microsoft.Xna.Framework.Input.Keys.K;
                case Andastra.Runtime.Graphics.Keys.L:
                    return Microsoft.Xna.Framework.Input.Keys.L;
                case Andastra.Runtime.Graphics.Keys.M:
                    return Microsoft.Xna.Framework.Input.Keys.M;
                case Andastra.Runtime.Graphics.Keys.N:
                    return Microsoft.Xna.Framework.Input.Keys.N;
                case Andastra.Runtime.Graphics.Keys.O:
                    return Microsoft.Xna.Framework.Input.Keys.O;
                case Andastra.Runtime.Graphics.Keys.P:
                    return Microsoft.Xna.Framework.Input.Keys.P;
                case Andastra.Runtime.Graphics.Keys.Q:
                    return Microsoft.Xna.Framework.Input.Keys.Q;
                case Andastra.Runtime.Graphics.Keys.R:
                    return Microsoft.Xna.Framework.Input.Keys.R;
                case Andastra.Runtime.Graphics.Keys.S:
                    return Microsoft.Xna.Framework.Input.Keys.S;
                case Andastra.Runtime.Graphics.Keys.T:
                    return Microsoft.Xna.Framework.Input.Keys.T;
                case Andastra.Runtime.Graphics.Keys.U:
                    return Microsoft.Xna.Framework.Input.Keys.U;
                case Andastra.Runtime.Graphics.Keys.V:
                    return Microsoft.Xna.Framework.Input.Keys.V;
                case Andastra.Runtime.Graphics.Keys.W:
                    return Microsoft.Xna.Framework.Input.Keys.W;
                case Andastra.Runtime.Graphics.Keys.X:
                    return Microsoft.Xna.Framework.Input.Keys.X;
                case Andastra.Runtime.Graphics.Keys.Y:
                    return Microsoft.Xna.Framework.Input.Keys.Y;
                case Andastra.Runtime.Graphics.Keys.Z:
                    return Microsoft.Xna.Framework.Input.Keys.Z;
                case Andastra.Runtime.Graphics.Keys.D0:
                    return Microsoft.Xna.Framework.Input.Keys.D0;
                case Andastra.Runtime.Graphics.Keys.D1:
                    return Microsoft.Xna.Framework.Input.Keys.D1;
                case Andastra.Runtime.Graphics.Keys.D2:
                    return Microsoft.Xna.Framework.Input.Keys.D2;
                case Andastra.Runtime.Graphics.Keys.D3:
                    return Microsoft.Xna.Framework.Input.Keys.D3;
                case Andastra.Runtime.Graphics.Keys.D4:
                    return Microsoft.Xna.Framework.Input.Keys.D4;
                case Andastra.Runtime.Graphics.Keys.D5:
                    return Microsoft.Xna.Framework.Input.Keys.D5;
                case Andastra.Runtime.Graphics.Keys.D6:
                    return Microsoft.Xna.Framework.Input.Keys.D6;
                case Andastra.Runtime.Graphics.Keys.D7:
                    return Microsoft.Xna.Framework.Input.Keys.D7;
                case Andastra.Runtime.Graphics.Keys.D8:
                    return Microsoft.Xna.Framework.Input.Keys.D8;
                case Andastra.Runtime.Graphics.Keys.D9:
                    return Microsoft.Xna.Framework.Input.Keys.D9;
                case Andastra.Runtime.Graphics.Keys.F1:
                    return Microsoft.Xna.Framework.Input.Keys.F1;
                case Andastra.Runtime.Graphics.Keys.F2:
                    return Microsoft.Xna.Framework.Input.Keys.F2;
                case Andastra.Runtime.Graphics.Keys.F3:
                    return Microsoft.Xna.Framework.Input.Keys.F3;
                case Andastra.Runtime.Graphics.Keys.F4:
                    return Microsoft.Xna.Framework.Input.Keys.F4;
                case Andastra.Runtime.Graphics.Keys.F5:
                    return Microsoft.Xna.Framework.Input.Keys.F5;
                case Andastra.Runtime.Graphics.Keys.F6:
                    return Microsoft.Xna.Framework.Input.Keys.F6;
                case Andastra.Runtime.Graphics.Keys.F7:
                    return Microsoft.Xna.Framework.Input.Keys.F7;
                case Andastra.Runtime.Graphics.Keys.F8:
                    return Microsoft.Xna.Framework.Input.Keys.F8;
                case Andastra.Runtime.Graphics.Keys.F9:
                    return Microsoft.Xna.Framework.Input.Keys.F9;
                case Andastra.Runtime.Graphics.Keys.F10:
                    return Microsoft.Xna.Framework.Input.Keys.F10;
                case Andastra.Runtime.Graphics.Keys.F11:
                    return Microsoft.Xna.Framework.Input.Keys.F11;
                case Andastra.Runtime.Graphics.Keys.F12:
                    return Microsoft.Xna.Framework.Input.Keys.F12;
                case Andastra.Runtime.Graphics.Keys.LeftControl:
                    return Microsoft.Xna.Framework.Input.Keys.LeftControl;
                case Andastra.Runtime.Graphics.Keys.RightControl:
                    return Microsoft.Xna.Framework.Input.Keys.RightControl;
                case Andastra.Runtime.Graphics.Keys.LeftShift:
                    return Microsoft.Xna.Framework.Input.Keys.LeftShift;
                case Andastra.Runtime.Graphics.Keys.RightShift:
                    return Microsoft.Xna.Framework.Input.Keys.RightShift;
                case Andastra.Runtime.Graphics.Keys.LeftAlt:
                    return Microsoft.Xna.Framework.Input.Keys.LeftAlt;
                case Andastra.Runtime.Graphics.Keys.RightAlt:
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

        public Andastra.Runtime.Graphics.ButtonState LeftButton => _state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Andastra.Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState RightButton => _state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Andastra.Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState MiddleButton => _state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Andastra.Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState XButton1 => _state.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Andastra.Runtime.Graphics.ButtonState.Released;
        public Andastra.Runtime.Graphics.ButtonState XButton2 => _state.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed ? Andastra.Runtime.Graphics.ButtonState.Pressed : Andastra.Runtime.Graphics.ButtonState.Released;

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

