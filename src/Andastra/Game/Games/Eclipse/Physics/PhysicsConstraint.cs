using System.Collections.Generic;
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
        /// The entity this constraint is attached to (primary body).
        /// </summary>
        public IEntity EntityA { get; set; }

        /// <summary>
        /// The other entity this constraint connects to (secondary body, null for world constraints).
        /// </summary>
        public IEntity EntityB { get; set; }

        /// <summary>
        /// Constraint type.
        /// </summary>
        public ConstraintType Type { get; set; }

        /// <summary>
        /// Local anchor point on EntityA.
        /// </summary>
        public Vector3 AnchorA { get; set; }

        /// <summary>
        /// Local anchor point on EntityB (or world position if EntityB is null).
        /// </summary>
        public Vector3 AnchorB { get; set; }

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

        /// <summary>
        /// Constraint-specific parameters (e.g., limits, spring constants).
        /// </summary>
        public Dictionary<string, float> Parameters { get; set; }

        /// <summary>
        /// Reference frame quaternion for angle calculations (stored when constraint is created).
        /// For hinge constraints, this represents the initial orientation relative to which angles are measured.
        /// Based on daorigins.exe/DragonAge2.exe: Hinge constraints store reference frame for angle limit calculations.
        /// </summary>
        public Quaternion ReferenceFrame { get; set; }

        public PhysicsConstraint()
        {
            Parameters = new Dictionary<string, float>();
            ReferenceFrame = Quaternion.Identity;
        }
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



