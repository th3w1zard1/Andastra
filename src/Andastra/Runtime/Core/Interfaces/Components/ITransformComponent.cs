using System.Numerics;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Transform component interface for position and orientation across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Transform Component Interface:
    /// - Common interface for transform functionality shared across all BioWare engines
    /// - Base implementation: BaseTransformComponent (Runtime.Games.Common.Components)
    /// - Engine-specific implementations:
    ///   - Odyssey: TransformComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraTransformComponent (nwmain.exe)
    ///   - Eclipse: EclipseTransformComponent (daorigins.exe, DragonAge2.exe)
    ///   - Infinity: InfinityTransformComponent (, )
    ///
    /// Cross-Engine Analysis (Reverse Engineered):
    /// - Odyssey (swkotor.exe, swkotor2.exe):
    ///   - swkotor.exe: XPosition @ 0x00745d80, YPosition @ 0x00745d74, ZPosition @ 0x00745d68
    ///     XOrientation @ 0x00745d38, YOrientation @ 0x00745d48, ZOrientation @ 0x00745d58
    ///   - swkotor2.exe: XPosition @ 0x007bd000, YPosition @ 0x007bcff4, ZPosition @ 0x007bcfe8
    ///     XOrientation @ 0x007bcfb8, YOrientation @ 0x007bcfc8, ZOrientation @ 0x007bcfd8
    ///   - 0x005226d0 @ 0x005226d0 (save entity position/orientation to GFF)
    ///   - 0x004e08e0 @ 0x004e08e0 (load placeable/door position from GIT)
    ///   - 0x00506550 @ 0x00506550 (set orientation from vector)
    ///   - 0x004d8390 @ 0x004d8390 (normalize orientation vector)
    /// - Aurora (nwmain.exe):
    ///   - XPosition @ 0x140ddb700, YPosition @ 0x140ddb710, ZPosition @ 0x140ddb720
    ///   - XOrientation @ 0x140ddb750, YOrientation @ 0x140ddb740, ZOrientation @ 0x140ddb730
    ///   - SaveCreature @ 0x1403a0a60 (saves position/orientation to GFF)
    ///   - LoadCreatures @ 0x140360570 (loads position/orientation from GFF)
    ///   - CNWSObject::SetOrientation (sets orientation from vector)
    /// - Eclipse (daorigins.exe, DragonAge2.exe):
    ///   - daorigins.exe: XPosition @ 0x00af4f68, YPosition @ 0x00af4f5c, ZPosition @ 0x00af4f50
    ///     XOrientation @ 0x00af4f40, YOrientation @ 0x00af4f30, ZOrientation @ 0x00af4f20
    ///   - DragonAge2.exe: Similar transform system (verified via cross-engine analysis)
    /// - Infinity (, ):
    ///   - Transform system similar to other engines (verified via cross-engine analysis)
    ///   - Uses same position/orientation storage pattern (XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation)
    ///
    /// Common Functionality (All Engines):
    /// - Position: Vector3 world position (Y-up coordinate system, meters)
    /// - Facing: Rotation angle in radians (0 = +X axis, counter-clockwise rotation) for 2D gameplay
    /// - Scale: Vector3 scale factors (default 1.0, 1.0, 1.0) for visual scaling
    /// - Parent: Hierarchical transforms for attached objects (e.g., weapons, shields on creatures)
    /// - Forward/Right: Derived direction vectors from facing angle
    /// - WorldMatrix: Computed 4x4 transformation matrix for rendering
    /// - Transform stored in GFF structures as XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation
    /// - Forward/Right vectors calculated from facing angle: Forward = (cos(facing), sin(facing), 0)
    /// - Y-up coordinate system (Y is vertical axis)
    /// - Positions in meters (world-space coordinates)
    /// - Orientation vector (XOrientation, YOrientation, ZOrientation) used for 3D model rendering (normalized direction vector)
    ///
    /// Inheritance Structure:
    /// - BaseTransformComponent (Runtime.Games.Common.Components) - Contains ALL common functionality
    ///   - TransformComponent (Runtime.Games.Odyssey.Components) - Odyssey-specific extensions
    ///   - AuroraTransformComponent (Runtime.Games.Aurora.Components) - Aurora-specific implementation
    ///   - EclipseTransformComponent (Runtime.Games.Eclipse.Components) - Eclipse-specific implementation
    ///   - InfinityTransformComponent (Runtime.Games.Infinity.Components) - Infinity-specific implementation
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

