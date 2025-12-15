using System.Numerics;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Transform component for position and orientation.
    /// </summary>
    /// <remarks>
    /// Transform Component Interface:
    /// - Based on swkotor2.exe entity transform system
    /// - Located via string references: "XPosition" @ 0x007bce70, "YPosition" @ 0x007bce7c, "ZPosition" @ 0x007bce88,
    ///   "XOrientation" @ 0x007bce94, "YOrientation" @ 0x007bcea0, "ZOrientation" @ 0x007bceac
    /// - Position: Vector3 world position (Y-up coordinate system, meters)
    /// - Facing: Rotation angle in radians (0 = +X axis, counter-clockwise rotation)
    /// - Scale: Vector3 scale factors (default 1.0, 1.0, 1.0)
    /// - Parent: Hierarchical transforms for attached objects (e.g., weapons, shields on creatures)
    /// - Forward/Right: Derived direction vectors from facing angle
    /// - WorldMatrix: Computed 4x4 transformation matrix for rendering
    /// - Based on swkotor2.exe: FUN_00506550 @ 0x00506550 (set orientation), FUN_004d8390 @ 0x004d8390 (normalize orientation vector)
    /// </remarks>
    public interface ITransformComponent : IComponent
    {
        /// <summary>
        /// World position.
        /// </summary>
        Vector3 Position { get; set; }

        /// <summary>
        /// Facing direction in radians.
        /// </summary>
        float Facing { get; set; }

        /// <summary>
        /// Scale factor.
        /// </summary>
        Vector3 Scale { get; set; }

        /// <summary>
        /// The parent entity for hierarchical transforms.
        /// </summary>
        IEntity Parent { get; set; }

        /// <summary>
        /// Gets the forward direction vector.
        /// </summary>
        Vector3 Forward { get; }

        /// <summary>
        /// Gets the right direction vector.
        /// </summary>
        Vector3 Right { get; }

        /// <summary>
        /// Gets the world transform matrix.
        /// </summary>
        Matrix4x4 WorldMatrix { get; }
    }
}

