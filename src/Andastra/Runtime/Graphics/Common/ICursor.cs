using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Cursor interface for mouse cursor rendering and management.
    /// </summary>
    /// <remarks>
    /// Cursor Interface:
    /// - Based on swkotor.exe and swkotor2.exe cursor system
    /// - Original implementation: Cursor changes when hovering over interactive elements (buttons, doors, etc.)
    /// - Cursor types: Default cursor (normal), hand/pointer cursor (hovering buttons), talk cursor, door cursor, etc.
    /// - Cursor resources: Stored as Windows PE resources in EXE file (cursor groups 1, 2 for default, 11, 12 for talk, etc.)
    /// - Cursor rendering: Rendered as sprite on top of all other graphics, follows mouse position
    /// - Hotspot: Cursor has a hotspot (click point) that determines where clicks are registered
    /// - This interface: Abstraction layer for cursor management and rendering
    /// </remarks>
    public interface ICursor : IDisposable
    {
        /// <summary>
        /// Gets the cursor texture (up state - normal).
        /// </summary>
        ITexture2D TextureUp { get; }

        /// <summary>
        /// Gets the cursor texture (down state - pressed).
        /// </summary>
        ITexture2D TextureDown { get; }

        /// <summary>
        /// Gets the cursor hotspot X coordinate (click point offset from top-left).
        /// </summary>
        int HotspotX { get; }

        /// <summary>
        /// Gets the cursor hotspot Y coordinate (click point offset from top-left).
        /// </summary>
        int HotspotY { get; }

        /// <summary>
        /// Gets the cursor width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the cursor height.
        /// </summary>
        int Height { get; }
    }

    /// <summary>
    /// Cursor type enumeration matching original game cursor types.
    /// </summary>
    /// <remarks>
    /// Cursor Types (based on reone/xoreos implementations):
    /// - Default: Cursor groups 1, 2 (normal cursor)
    /// - Hand/Pointer: Used when hovering over buttons (typically same as default but can be customized)
    /// - Talk: Cursor groups 11, 12 (when hovering over NPCs)
    /// - Door: Cursor groups 23, 24 (when hovering over doors)
    /// - Pickup: Cursor groups 25, 26 (when hovering over items)
    /// - Attack: Cursor groups 51, 52 (when hovering over enemies)
    /// </remarks>
    public enum CursorType
    {
        /// <summary>
        /// Default cursor (normal state).
        /// </summary>
        Default = 0,

        /// <summary>
        /// Hand/pointer cursor (hovering over buttons/interactive UI elements).
        /// </summary>
        Hand = 1,

        /// <summary>
        /// Talk cursor (hovering over NPCs).
        /// </summary>
        Talk = 2,

        /// <summary>
        /// Door cursor (hovering over doors).
        /// </summary>
        Door = 3,

        /// <summary>
        /// Pickup cursor (hovering over items).
        /// </summary>
        Pickup = 4,

        /// <summary>
        /// Attack cursor (hovering over enemies).
        /// </summary>
        Attack = 5
    }

    /// <summary>
    /// Cursor manager interface for loading and managing cursors.
    /// </summary>
    public interface ICursorManager : IDisposable
    {
        /// <summary>
        /// Gets a cursor of the specified type.
        /// </summary>
        /// <param name="type">Cursor type to get.</param>
        /// <returns>Cursor instance, or null if not available.</returns>
        ICursor GetCursor(CursorType type);

        /// <summary>
        /// Sets the current active cursor type.
        /// </summary>
        /// <param name="type">Cursor type to activate.</param>
        void SetCursor(CursorType type);

        /// <summary>
        /// Gets the current active cursor.
        /// </summary>
        ICursor CurrentCursor { get; }

        /// <summary>
        /// Gets whether the cursor is pressed (mouse button down).
        /// </summary>
        bool IsPressed { get; set; }

        /// <summary>
        /// Gets or sets the cursor position.
        /// </summary>
        Vector2 Position { get; set; }
    }
}

