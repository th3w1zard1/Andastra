using System.Numerics;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Input manager interface for keyboard, mouse, and gamepad input.
    /// </summary>
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
    }

    /// <summary>
    /// Mouse state interface.
    /// </summary>
    public interface IMouseState
    {
        /// <summary>
        /// Gets the mouse position.
        /// </summary>
        Vector2 Position { get; }

        /// <summary>
        /// Gets the mouse scroll wheel value.
        /// </summary>
        int ScrollWheelValue { get; }

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
        Z = 90
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

