using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics;

namespace Andastra.Game.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey engine input manager implementation.
    /// Uses DirectInput8 or Windows raw input like the original game.
    /// </summary>
    /// <remarks>
    /// Odyssey Input Manager:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game input system: DirectInput8 (DINPUT8.dll @ 0x0080a6c0)
    /// - DirectInput8Create @ 0x0080a6ac for device enumeration
    /// - Located via string references: "CExoInputInternal" (exoinputinternal.cpp @ 0x007c64dc)
    /// - Input class: "CExoInputInternal::GetEvents() Invalid InputClass parameter" @ 0x007c64f4
    /// - This implementation: Uses Windows GetAsyncKeyState for simplicity
    /// </remarks>
    public class OdysseyInputManager : IInputManager
    {
        private readonly OdysseyKeyboardState _currentKeyboard;
        private readonly OdysseyKeyboardState _previousKeyboard;
        private readonly OdysseyMouseState _currentMouse;
        private readonly OdysseyMouseState _previousMouse;
        
        /// <summary>
        /// Creates a new Odyssey input manager.
        /// </summary>
        public OdysseyInputManager()
        {
            _currentKeyboard = new OdysseyKeyboardState();
            _previousKeyboard = new OdysseyKeyboardState();
            _currentMouse = new OdysseyMouseState();
            _previousMouse = new OdysseyMouseState();
        }
        
        /// <summary>
        /// Gets the current keyboard state.
        /// </summary>
        public IKeyboardState KeyboardState => _currentKeyboard;
        
        /// <summary>
        /// Gets the current mouse state.
        /// </summary>
        public IMouseState MouseState => _currentMouse;
        
        /// <summary>
        /// Gets the previous keyboard state (for key press detection).
        /// </summary>
        public IKeyboardState PreviousKeyboardState => _previousKeyboard;
        
        /// <summary>
        /// Gets the previous mouse state (for button press detection).
        /// </summary>
        public IMouseState PreviousMouseState => _previousMouse;
        
        /// <summary>
        /// Updates input state (call each frame).
        /// Based on swkotor.exe/swkotor2.exe: DirectInput device polling
        /// </summary>
        public void Update()
        {
            // Copy current state to previous
            _previousKeyboard.CopyFrom(_currentKeyboard);
            _previousMouse.CopyFrom(_currentMouse);
            
            // Update current state
            _currentKeyboard.Update();
            _currentMouse.Update();
        }
    }
    
    /// <summary>
    /// Odyssey keyboard state implementation.
    /// Uses Windows GetAsyncKeyState.
    /// </summary>
    public class OdysseyKeyboardState : IKeyboardState
    {
        private readonly bool[] _keyStates;
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        public OdysseyKeyboardState()
        {
            _keyStates = new bool[256];
        }
        
        /// <summary>
        /// Updates the keyboard state.
        /// </summary>
        public void Update()
        {
            // Check common game keys
            UpdateKey(Keys.W, 0x57);
            UpdateKey(Keys.A, 0x41);
            UpdateKey(Keys.S, 0x53);
            UpdateKey(Keys.D, 0x44);
            UpdateKey(Keys.Space, 0x20);
            UpdateKey(Keys.Escape, 0x1B);
            UpdateKey(Keys.Enter, 0x0D);
            UpdateKey(Keys.Tab, 0x09);
            UpdateKey(Keys.LeftShift, 0xA0);
            UpdateKey(Keys.RightShift, 0xA1);
            UpdateKey(Keys.LeftControl, 0xA2);
            UpdateKey(Keys.RightControl, 0xA3);
            UpdateKey(Keys.LeftAlt, 0xA4);
            UpdateKey(Keys.RightAlt, 0xA5);
            UpdateKey(Keys.Up, 0x26);
            UpdateKey(Keys.Down, 0x28);
            UpdateKey(Keys.Left, 0x25);
            UpdateKey(Keys.Right, 0x27);
            UpdateKey(Keys.F1, 0x70);
            UpdateKey(Keys.F2, 0x71);
            UpdateKey(Keys.F3, 0x72);
            UpdateKey(Keys.F4, 0x73);
            UpdateKey(Keys.F5, 0x74);
            UpdateKey(Keys.F6, 0x75);
            UpdateKey(Keys.F7, 0x76);
            UpdateKey(Keys.F8, 0x77);
            UpdateKey(Keys.F9, 0x78);
            UpdateKey(Keys.F10, 0x79);
            UpdateKey(Keys.F11, 0x7A);
            UpdateKey(Keys.F12, 0x7B);
            UpdateKey(Keys.D0, 0x30);
            UpdateKey(Keys.D1, 0x31);
            UpdateKey(Keys.D2, 0x32);
            UpdateKey(Keys.D3, 0x33);
            UpdateKey(Keys.D4, 0x34);
            UpdateKey(Keys.D5, 0x35);
            UpdateKey(Keys.D6, 0x36);
            UpdateKey(Keys.D7, 0x37);
            UpdateKey(Keys.D8, 0x38);
            UpdateKey(Keys.D9, 0x39);
        }
        
        private void UpdateKey(Keys key, int vKey)
        {
            short state = GetAsyncKeyState(vKey);
            _keyStates[(int)key] = (state & 0x8000) != 0;
        }
        
        /// <summary>
        /// Copies state from another keyboard state.
        /// </summary>
        public void CopyFrom(OdysseyKeyboardState other)
        {
            Array.Copy(other._keyStates, _keyStates, _keyStates.Length);
        }
        
        /// <summary>
        /// Checks if a key is currently pressed.
        /// </summary>
        public bool IsKeyDown(Keys key)
        {
            int index = (int)key;
            if (index >= 0 && index < _keyStates.Length)
            {
                return _keyStates[index];
            }
            return false;
        }
        
        /// <summary>
        /// Checks if a key is currently released.
        /// </summary>
        public bool IsKeyUp(Keys key)
        {
            return !IsKeyDown(key);
        }
        
        /// <summary>
        /// Gets all currently pressed keys.
        /// </summary>
        public Keys[] GetPressedKeys()
        {
            int count = 0;
            for (int i = 0; i < _keyStates.Length; i++)
            {
                if (_keyStates[i]) count++;
            }
            
            Keys[] result = new Keys[count];
            int index = 0;
            for (int i = 0; i < _keyStates.Length && index < count; i++)
            {
                if (_keyStates[i])
                {
                    result[index++] = (Keys)i;
                }
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Odyssey mouse state implementation.
    /// Uses Windows GetCursorPos and GetAsyncKeyState.
    /// </summary>
    public class OdysseyMouseState : IMouseState
    {
        private int _x;
        private int _y;
        private int _scrollWheelValue;
        private ButtonState _leftButton;
        private ButtonState _rightButton;
        private ButtonState _middleButton;
        private ButtonState _xButton1;
        private ButtonState _xButton2;
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        // Virtual key codes for mouse buttons
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_XBUTTON1 = 0x05;
        private const int VK_XBUTTON2 = 0x06;
        
        public OdysseyMouseState()
        {
            _leftButton = ButtonState.Released;
            _rightButton = ButtonState.Released;
            _middleButton = ButtonState.Released;
            _xButton1 = ButtonState.Released;
            _xButton2 = ButtonState.Released;
        }
        
        /// <summary>
        /// Updates the mouse state.
        /// </summary>
        public void Update()
        {
            // Update cursor position
            if (GetCursorPos(out POINT point))
            {
                _x = point.x;
                _y = point.y;
            }
            
            // Update button states
            _leftButton = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            _rightButton = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            _middleButton = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            _xButton1 = (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            _xButton2 = (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            
            // Note: Scroll wheel handling requires Windows message processing
            // TODO: Implement scroll wheel via WM_MOUSEWHEEL message
        }
        
        /// <summary>
        /// Copies state from another mouse state.
        /// </summary>
        public void CopyFrom(OdysseyMouseState other)
        {
            _x = other._x;
            _y = other._y;
            _scrollWheelValue = other._scrollWheelValue;
            _leftButton = other._leftButton;
            _rightButton = other._rightButton;
            _middleButton = other._middleButton;
            _xButton1 = other._xButton1;
            _xButton2 = other._xButton2;
        }
        
        /// <summary>
        /// Gets the mouse X position.
        /// </summary>
        public int X => _x;
        
        /// <summary>
        /// Gets the mouse Y position.
        /// </summary>
        public int Y => _y;
        
        /// <summary>
        /// Gets the mouse position as a Vector2.
        /// </summary>
        public Vector2 Position => new Vector2(_x, _y);
        
        /// <summary>
        /// Gets the mouse scroll wheel value.
        /// </summary>
        public int ScrollWheelValue => _scrollWheelValue;
        
        /// <summary>
        /// Gets the left button state.
        /// </summary>
        public ButtonState LeftButton => _leftButton;
        
        /// <summary>
        /// Gets the right button state.
        /// </summary>
        public ButtonState RightButton => _rightButton;
        
        /// <summary>
        /// Gets the middle button state.
        /// </summary>
        public ButtonState MiddleButton => _middleButton;
        
        /// <summary>
        /// Gets the XButton1 state.
        /// </summary>
        public ButtonState XButton1 => _xButton1;
        
        /// <summary>
        /// Gets the XButton2 state.
        /// </summary>
        public ButtonState XButton2 => _xButton2;
        
        /// <summary>
        /// Checks if a mouse button is currently pressed.
        /// </summary>
        public bool IsButtonDown(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return _leftButton == ButtonState.Pressed;
                case MouseButton.Right:
                    return _rightButton == ButtonState.Pressed;
                case MouseButton.Middle:
                    return _middleButton == ButtonState.Pressed;
                case MouseButton.XButton1:
                    return _xButton1 == ButtonState.Pressed;
                case MouseButton.XButton2:
                    return _xButton2 == ButtonState.Pressed;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Checks if a mouse button is currently released.
        /// </summary>
        public bool IsButtonUp(MouseButton button)
        {
            return !IsButtonDown(button);
        }
        
        /// <summary>
        /// Sets the scroll wheel value (called from window message processing).
        /// </summary>
        internal void SetScrollWheelValue(int value)
        {
            _scrollWheelValue = value;
        }
    }
}

