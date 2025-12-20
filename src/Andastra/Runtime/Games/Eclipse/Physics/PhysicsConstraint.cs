using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Eclipse.Physics
{
    /// <summary>
    /// Physics constraint for Eclipse engine physics system.
    /// </summary>
    public class PhysicsConstraint
    {
        /// <summary>
        /// Entity this constraint is attached to.
        /// </summary>
        public IEntity Entity { get; set; }

        /// <summary>
        /// Constraint type.
        /// </summary>
        public ConstraintType Type { get; set; }

        /// <summary>
        /// Constraint position in world space.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Constraint direction/orientation.
        /// </summary>
        public Vector3 Direction { get; set; }

        /// <summary>
        /// Constraint limits (min/max angles or distances).
        /// </summary>
        public Vector2 Limits { get; set; }

        /// <summary>
        /// Constraint stiffness.
        /// </summary>
        public float Stiffness { get; set; }
    }

    /// <summary>
    /// Physics constraint type enumeration.
    /// </summary>
    public enum ConstraintType
    {
        /// <summary>
        /// Fixed constraint (no movement).
        /// </summary>
        Fixed,

        /// <summary>
        /// Hinge constraint (rotation around axis).
        /// </summary>
        Hinge,

        /// <summary>
        /// Ball and socket constraint (rotation in all directions).
        /// </summary>
        BallAndSocket,

        /// <summary>
        /// Slider constraint (translation along axis).
        /// </summary>
        Slider,

        /// <summary>
        /// Spring constraint (elastic connection).
        /// </summary>
        Spring
    }
}

