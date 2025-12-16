using Microsoft.Xna.Framework.Input;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
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

        public Vector2 Position => new Vector2(_state.X, _state.Y);

        public int ScrollWheelValue => _state.ScrollWheelValue;

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

