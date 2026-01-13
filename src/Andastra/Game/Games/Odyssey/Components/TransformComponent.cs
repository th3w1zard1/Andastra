using System;
using System.Numerics;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey Engine (KOTOR) specific transform component implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Transform Component:
    /// - Based on swkotor.exe and swkotor2.exe entity transform system
    /// - swkotor.exe: XPosition @ 0x00745d80, YPosition @ 0x00745d74, ZPosition @ 0x00745d68, XOrientation @ 0x00745d38, YOrientation @ 0x00745d48, ZOrientation @ 0x00745d58
    /// - swkotor2.exe: XPosition @ 0x007bd000, YPosition @ 0x007bcff4, ZPosition @ 0x007bcfe8, XOrientation @ 0x007bcfb8, YOrientation @ 0x007bcfc8, ZOrientation @ 0x007bcfd8
    /// - "PositionX" @ 0x007bc474 (position X field), "PositionY" @ 0x007bc468 (position Y field), "PositionZ" @ 0x007bc45c (position Z field)
    /// - "Position" @ 0x007bd154 (position field), "position" @ 0x007ba168 (position constant)
    /// - "positionkey" @ 0x007ba150 (position key field), "positionbezierkey" @ 0x007ba13c (position bezier key field)
    /// - "UpdateDependentPosition" @ 0x007bb984 (update dependent position function), "flarepositions" @ 0x007bac94 (flare positions field)
    /// - Position debug: "Position: (%3.2f, %3.2f, %3.2f)" @ 0x007c79a8 (position debug format string)
    /// - Pathfinding position errors:
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510 (pathfinding error)
    ///   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0 (walking collision error)
    ///   - "Bailed the desired position is unsafe." @ 0x007c0584 (unsafe position error)
    ///   - "PathFollowData requesting bad data position %d" @ 0x007ca414 (path follow data error)
    /// - Original implementation: 0x005226d0 @ 0x005226d0 (save entity position/orientation to GFF), 0x004e08e0 @ 0x004e08e0 (load placeable/door position from GIT)
    /// - Position stored at offsets 0x94 (X), 0x98 (Y), 0x9c (Z) in creature objects (in-memory layout)
    /// - Orientation stored at offsets 0xa0 (X), 0xa4 (Y), 0xa8 (Z) as normalized direction vector (in-memory layout)
    /// - 0x00506550 @ 0x00506550 sets orientation from vector, 0x004d8390 @ 0x004d8390 normalizes orientation vector
    /// - KOTOR coordinate system:
    ///   - Y-up coordinate system (same as most game engines, Y is vertical)
    ///   - Positions in meters (world-space coordinates)
    ///   - Facing angle in radians (0 = +X axis, counter-clockwise positive) for 2D gameplay
    ///   - Orientation vector (XOrientation, YOrientation, ZOrientation) used for 3D model rendering (normalized direction vector)
    ///   - Scale typically (1,1,1) but can be modified for effects (visual scaling, not physics)
    /// - Transform stored in GFF structures as XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation
    /// - Forward/Right vectors calculated from facing angle for 2D movement (cos/sin pattern matches engine: Forward = (cos(facing), sin(facing), 0))
    /// </remarks>
    public class TransformComponent : BaseTransformComponent
    {
        /// <summary>
        /// Creates a new Odyssey transform component with default values.
        /// </summary>
        public TransformComponent() : base()
        {
        }

        /// <summary>
        /// Creates a new Odyssey transform component with specified position and facing.
        /// </summary>
        /// <param name="position">Initial world position.</param>
        /// <param name="facing">Initial facing angle in radians.</param>
        public TransformComponent(Vector3 position, float facing) : base(position, facing)
        {
        }

        #region Extended Properties

        /// <summary>
        /// Gets the up direction vector (Z axis in KOTOR).
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Returns Vector3.UnitZ for KOTOR's coordinate system.
        /// </remarks>
        public Vector3 Up
        {
            get { return Vector3.UnitZ; }
        }

        /// <summary>
        /// Facing direction in degrees.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Convenience property for working with degrees instead of radians.
        /// </remarks>
        public float FacingDegrees
        {
            get { return Facing * (180f / (float)Math.PI); }
            set { Facing = value * ((float)Math.PI / 180f); }
        }

        #endregion
    }
}
