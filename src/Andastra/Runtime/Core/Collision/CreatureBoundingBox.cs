using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Collision
{
    /// <summary>
    /// Represents a creature's bounding box for collision detection.
    /// </summary>
    /// <remarks>
    /// Creature Bounding Box:
    /// - Based on swkotor.exe and swkotor2.exe further analysis
    /// - K1 (swkotor.exe): Bounding box pointer at offset 0x340, radius at +8, width at +4
    ///   - 0x004ed6e0 @ 0x004ed6e0: `*(float *)(*(int *)(param_1 + 0x340) + 8)` (radius)
    ///   - 0x004f1310 @ 0x004f1310: Adds two radii for collision distance
    /// - K2 (swkotor2.exe): Bounding box pointer at offset 0x380, width at +0x14, height at +0xbc
    ///   - 0x005479f0 @ 0x005479f0: `*(float *)(iVar1 + 0x14)` (width), `*(undefined4 *)(iVar1 + 0xbc)` (height)
    /// - Width and height are half-extents (radius-like values)
    /// - Bounding box is axis-aligned and centered at entity position
    /// - Used for collision detection in movement actions (ActionMoveToLocation, ActionMoveToObject)
    /// - Original implementation: 0x004e17a0 @ 0x004e17a0 (spatial query), 0x004f5290 @ 0x004f5290 (detailed collision)
    /// </remarks>
    public struct CreatureBoundingBox
    {
        /// <summary>
        /// Half-width (X-axis extent).
        /// </summary>
        public float Width;

        /// <summary>
        /// Half-height (Y-axis extent).
        /// </summary>
        public float Height;

        /// <summary>
        /// Half-depth (Z-axis extent).
        /// </summary>
        public float Depth;

        /// <summary>
        /// Creates a bounding box from half-extents.
        /// </summary>
        public CreatureBoundingBox(float width, float height, float depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }

        /// <summary>
        /// Creates a bounding box from a radius (spherical approximation).
        /// </summary>
        public static CreatureBoundingBox FromRadius(float radius)
        {
            return new CreatureBoundingBox(radius, radius, radius);
        }

        /// <summary>
        /// Gets the minimum corner of the bounding box in world space.
        /// </summary>
        public Vector3 GetMin(Vector3 center)
        {
            return new Vector3(
                center.X - Width,
                center.Y - Height,
                center.Z - Depth
            );
        }

        /// <summary>
        /// Gets the maximum corner of the bounding box in world space.
        /// </summary>
        public Vector3 GetMax(Vector3 center)
        {
            return new Vector3(
                center.X + Width,
                center.Y + Height,
                center.Z + Depth
            );
        }
    }
}

