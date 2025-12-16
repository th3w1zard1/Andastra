using System.Numerics;

namespace BioWareEngines.Core.Interfaces.Components
{
    /// <summary>
    /// Transform component for position and orientation.
    /// </summary>
    /// <remarks>
    /// Transform Component Interface:
    /// - Based on swkotor2.exe entity transform system
    /// - Located via string references: "XPosition" @ 0x007bd000, "YPosition" @ 0x007bd00c, "ZPosition" @ 0x007bd018
    ///   "XOrientation" @ 0x007bcfb8, "YOrientation" @ 0x007bcfc8, "ZOrientation" @ 0x007bcfd8
    /// - Orientation fields: "Orientation" @ 0x007bd148, "OrientationX" @ 0x007bd0a4, "OrientationY" @ 0x007bd0b4, "OrientationZ" @ 0x007bd0c4
    /// - Animation orientation: "orientation" @ 0x007ba15c, "orientationkey" @ 0x007ba12c, "orientationbezierkey" @ 0x007ba114
    /// - Position: Vector3 world position (Y-up coordinate system, meters)
    /// - Facing: Rotation angle in radians (0 = +X axis, counter-clockwise rotation)
    /// - Scale: Vector3 scale factors (default 1.0, 1.0, 1.0)
    /// - Parent: Hierarchical transforms for attached objects (e.g., weapons, shields on creatures)
    /// - Forward/Right: Derived direction vectors from facing angle
    /// - WorldMatrix: Computed 4x4 transformation matrix for rendering
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation to GFF
    /// - FUN_00506550 @ 0x00506550 (set orientation), FUN_004d8390 @ 0x004d8390 (normalize orientation vector)
    /// - FUN_004e08e0 @ 0x004e08e0 loads position and orientation from GIT instances (creatures, doors, placeables, etc.)
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

