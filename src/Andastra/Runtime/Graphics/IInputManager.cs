using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Input manager interface for keyboard, mouse, and gamepad input.
    /// </summary>
    /// <remarks>
    /// Input Manager Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) input system
    /// - Original implementation: Uses DirectInput8 (DINPUT8.dll @ 0x0080a6c0, DirectInput8Create @ 0x0080a6ac)
    /// - Located via string references: "CExoInputInternal" (exoinputinternal.cpp @ 0x007c64dc)
    /// - Input class: "CExoInputInternal::GetEvents() Invalid InputClass parameter" @ 0x007c64f4
    /// - This interface abstracts input handling for different graphics backends (MonoGame, Stride)
    /// - See PlayerInputHandler.cs for game-specific input logic with comprehensive Ghidra references
    /// </remarks>
    public interface IInputManager
    {
        /// <summary>
        /// Gets the current keyboard state.
        /// </summary>
        IKeyboardState KeyboardState { get; }

        /// <summary>
        /// Gets the current mouse state.
        /// </summary>
        IMouseState MouseState { get; }

        /// <summary>
        /// Gets the previous keyboard state (for key press detection).
        /// </summary>
        IKeyboardState PreviousKeyboardState { get; }

        /// <summary>
        /// Gets the previous mouse state (for button press detection).
        /// </summary>
        IMouseState PreviousMouseState { get; }

        /// <summary>
        /// Updates input state (call each frame).
        /// </summary>
        void Update();
    }

    /// <summary>
    /// Keyboard state interface.
    /// </summary>
    public interface IKeyboardState
    {
        /// <summary>
        /// Checks if a key is currently pressed.
        /// </summary>
        /// <param name="key">Key to check.</param>
        /// <returns>True if key is pressed.</returns>
        bool IsKeyDown(Keys key);

        /// <summary>
        /// Checks if a key is currently released.
        /// </summary>
        /// <param name="key">Key to check.</param>
        /// <returns>True if key is released.</returns>
        bool IsKeyUp(Keys key);

        /// <summary>
        /// Gets all currently pressed keys.
        /// </summary>
        /// <returns>Array of pressed keys.</returns>
        Keys[] GetPressedKeys();
    }

    /// <summary>
    /// Mouse state interface.
    /// </summary>
    public interface IMouseState
    {
        /// <summary>
        /// Gets the mouse X position.
        /// </summary>
        int X { get; }

        /// <summary>
        /// Gets the mouse Y position.
        /// </summary>
        int Y { get; }

        /// <summary>
        /// Gets the mouse position as a Vector2.
        /// </summary>
        Vector2 Position { get; }

        /// <summary>
        /// Gets the mouse scroll wheel value.
        /// </summary>
        int ScrollWheelValue { get; }

        /// <summary>
        /// Gets the left button state.
        /// </summary>
        ButtonState LeftButton { get; }

        /// <summary>
        /// Gets the right button state.
        /// </summary>
        ButtonState RightButton { get; }

        /// <summary>
        /// Gets the middle button state.
        /// </summary>
        ButtonState MiddleButton { get; }

        /// <summary>
        /// Gets the XButton1 state.
        /// </summary>
        ButtonState XButton1 { get; }

        /// <summary>
        /// Gets the XButton2 state.
        /// </summary>
        ButtonState XButton2 { get; }

        /// <summary>
        /// Checks if a mouse button is currently pressed.
        /// </summary>
        /// <param name="button">Button to check.</param>
        /// <returns>True if button is pressed.</returns>
        bool IsButtonDown(MouseButton button);

        /// <summary>
        /// Checks if a mouse button is currently released.
        /// </summary>
        /// <param name="button">Button to check.</param>
        /// <returns>True if button is released.</returns>
        bool IsButtonUp(MouseButton button);
    }

    /// <summary>
    /// Button state enumeration.
    /// </summary>
    public enum ButtonState
    {
        Released,
        Pressed
    }

    /// <summary>
    /// Keyboard keys enumeration.
    /// </summary>
    public enum Keys
    {
        None = 0,
        Back = 8,
        Tab = 9,
        Enter = 13,
        Escape = 27,
        Space = 32,
        Up = 38,
        Down = 40,
        Left = 37,
        Right = 39,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        LeftControl = 162,
        RightControl = 163,
        LeftShift = 160,
        RightShift = 161,
        LeftAlt = 164,
        RightAlt = 165
    }

    /// <summary>
    /// Mouse button enumeration.
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }
}

