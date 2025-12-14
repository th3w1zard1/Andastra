using System.Numerics;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Transform component for position and orientation.
    /// </summary>
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

