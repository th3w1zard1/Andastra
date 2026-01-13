using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of transform component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Transform Component Implementation:
    /// - Common transform functionality shared across all engines
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (if any differences exist)
    /// - Cross-engine analysis shows common transform component patterns across all engines:
    ///   - Common structure: XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation fields
    ///   - Common operations: Position/orientation saving/loading, orientation from vector, vector normalization
    ///
    /// Common functionality across all engines:
    /// - Position: Vector3 world position (Y-up coordinate system, meters)
    /// - Facing: Rotation angle in radians (0 = +X axis, counter-clockwise rotation) for 2D gameplay
    /// - Scale: Vector3 scale factors (default 1.0, 1.0, 1.0)
    /// - Parent: Hierarchical transforms for attached objects (e.g., weapons, shields on creatures)
    /// - Forward/Right: Derived direction vectors from facing angle
    /// - WorldMatrix: Computed 4x4 transformation matrix for rendering
    /// - Transform stored in GFF structures as XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation
    /// - Forward/Right vectors calculated from facing angle for 2D movement (cos/sin pattern matches engine: Forward = (cos(facing), sin(facing), 0))
    /// - Y-up coordinate system (same as most game engines, Y is vertical)
    /// - Positions in meters (world-space coordinates)
    /// - Orientation vector (XOrientation, YOrientation, ZOrientation) used for 3D model rendering (normalized direction vector)
    /// - Scale typically (1,1,1) but can be modified for effects (visual scaling, not physics)
    ///
    /// Engine-specific differences (handled in entity serialization, not component):
    /// - GFF field names may vary slightly between engines
    /// - Serialization format details differ (handled by entity Serialize/Deserialize methods)
    /// - Memory layout offsets differ (handled by engine-specific entity implementations)
    /// </remarks>
    public abstract class BaseTransformComponent : IComponent, ITransformComponent
    {
        private Vector3 _position;
        private float _facing;
        private Vector3 _scale;
        private IEntity _parent;
        private Matrix4x4 _worldMatrixCache;
        private bool _worldMatrixDirty;

        public IEntity Owner { get; set; }

        public virtual void OnAttach()
        {
            // Position and facing are managed by this component
        }

        public virtual void OnDetach()
        {
            _parent = null;
        }

        protected BaseTransformComponent()
        {
            _position = Vector3.Zero;
            _facing = 0f;
            _scale = Vector3.One;
            _parent = null;
            _worldMatrixDirty = true;
        }

        protected BaseTransformComponent(Vector3 position, float facing) : this()
        {
            _position = position;
            _facing = facing;
        }

        /// <summary>
        /// World position.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Vector3 world position in Y-up coordinate system (meters).
        /// </remarks>
        public virtual Vector3 Position
        {
            get { return _position; }
            set
            {
                if (_position != value)
                {
                    _position = value;
                    _worldMatrixDirty = true;
                }
            }
        }

        /// <summary>
        /// Facing direction in radians.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Rotation angle in radians (0 = +X axis, counter-clockwise positive) for 2D gameplay.
        /// Normalized to [0, 2π) range.
        /// </remarks>
        public virtual float Facing
        {
            get { return _facing; }
            set
            {
                // Normalize to [0, 2π)
                float normalized = value % ((float)(2 * Math.PI));
                if (normalized < 0)
                {
                    normalized += (float)(2 * Math.PI);
                }

                if (_facing != normalized)
                {
                    _facing = normalized;
                    _worldMatrixDirty = true;
                }
            }
        }

        /// <summary>
        /// Scale factor.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Vector3 scale factors (default 1.0, 1.0, 1.0).
        /// Used for visual scaling, not physics.
        /// </remarks>
        public virtual Vector3 Scale
        {
            get { return _scale; }
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    _worldMatrixDirty = true;
                }
            }
        }

        /// <summary>
        /// The parent entity for hierarchical transforms.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Hierarchical transforms for attached objects (e.g., weapons, shields on creatures).
        /// </remarks>
        public virtual IEntity Parent
        {
            get { return _parent; }
            set
            {
                if (_parent != value)
                {
                    _parent = value;
                    _worldMatrixDirty = true;
                }
            }
        }

        /// <summary>
        /// Gets the forward direction vector.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Derived direction vector from facing angle.
        /// Forward = (cos(facing), sin(facing), 0) for 2D movement.
        /// </remarks>
        public virtual Vector3 Forward
        {
            get
            {
                return new Vector3(
                    (float)Math.Cos(_facing),
                    (float)Math.Sin(_facing),
                    0f
                );
            }
        }

        /// <summary>
        /// Gets the right direction vector.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Derived direction vector from facing angle.
        /// Right = (cos(facing - π/2), sin(facing - π/2), 0) for 2D movement.
        /// </remarks>
        public virtual Vector3 Right
        {
            get
            {
                return new Vector3(
                    (float)Math.Cos(_facing - Math.PI / 2),
                    (float)Math.Sin(_facing - Math.PI / 2),
                    0f
                );
            }
        }

        /// <summary>
        /// Gets the world transform matrix.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Computed 4x4 transformation matrix for rendering.
        /// Cached and recalculated when position, facing, scale, or parent changes.
        /// </remarks>
        public virtual Matrix4x4 WorldMatrix
        {
            get
            {
                if (_worldMatrixDirty)
                {
                    UpdateWorldMatrix();
                }
                return _worldMatrixCache;
            }
        }

        /// <summary>
        /// Updates the cached world matrix.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Builds local transform (Scale * Rotation * Translation) and applies parent transform if present.
        /// </remarks>
        protected virtual void UpdateWorldMatrix()
        {
            // Build local transform: Scale * Rotation * Translation
            Matrix4x4 scale = Matrix4x4.CreateScale(_scale);
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ(_facing);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(_position);

            Matrix4x4 localMatrix = scale * rotation * translation;

            // Apply parent transform if present
            if (_parent != null)
            {
                ITransformComponent parentTransform = _parent.GetComponent<ITransformComponent>();
                if (parentTransform != null)
                {
                    _worldMatrixCache = localMatrix * parentTransform.WorldMatrix;
                }
                else
                {
                    _worldMatrixCache = localMatrix;
                }
            }
            else
            {
                _worldMatrixCache = localMatrix;
            }

            _worldMatrixDirty = false;
        }

        /// <summary>
        /// Marks the world matrix as needing recalculation.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Forces recalculation of world matrix on next access.
        /// </remarks>
        public virtual void InvalidateMatrix()
        {
            _worldMatrixDirty = true;
        }

        /// <summary>
        /// Moves the entity by a delta.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Translates the entity by the specified delta vector.
        /// </remarks>
        public virtual void Translate(Vector3 delta)
        {
            Position = _position + delta;
        }

        /// <summary>
        /// Moves the entity forward by a distance.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Moves the entity forward along its facing direction by the specified distance.
        /// </remarks>
        public virtual void MoveForward(float distance)
        {
            Position = _position + Forward * distance;
        }

        /// <summary>
        /// Rotates the entity by a delta angle (radians).
        /// </summary>
        /// <remarks>
        /// Common across all engines: Rotates the entity by the specified delta angle.
        /// </remarks>
        public virtual void Rotate(float deltaRadians)
        {
            Facing = _facing + deltaRadians;
        }

        /// <summary>
        /// Sets the facing to look at a target position.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Sets the facing angle to point toward the target position.
        /// </remarks>
        public virtual void LookAt(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - _position;
            if (direction.LengthSquared() > 0.0001f)
            {
                Facing = (float)Math.Atan2(direction.Y, direction.X);
            }
        }

        /// <summary>
        /// Gets the distance to another position.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the 3D distance to the specified position.
        /// </remarks>
        public virtual float DistanceTo(Vector3 otherPosition)
        {
            return Vector3.Distance(_position, otherPosition);
        }

        /// <summary>
        /// Gets the 2D distance (ignoring Z) to another position.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the 2D distance (ignoring Z coordinate) to the specified position.
        /// </remarks>
        public virtual float DistanceTo2D(Vector3 otherPosition)
        {
            float dx = _position.X - otherPosition.X;
            float dy = _position.Y - otherPosition.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Gets the angle to another position.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the angle (in radians) from this entity to the specified position.
        /// </remarks>
        public virtual float AngleTo(Vector3 otherPosition)
        {
            Vector3 direction = otherPosition - _position;
            return (float)Math.Atan2(direction.Y, direction.X);
        }

        /// <summary>
        /// Checks if a target is in front of this entity.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns true if the target position is within the specified field of view.
        /// </remarks>
        public virtual bool IsInFront(Vector3 targetPosition, float fovRadians = (float)Math.PI)
        {
            float angleToTarget = AngleTo(targetPosition);
            float angleDiff = Math.Abs(angleToTarget - _facing);

            // Normalize to [0, π]
            if (angleDiff > Math.PI)
            {
                angleDiff = (float)(2 * Math.PI) - angleDiff;
            }

            return angleDiff <= fovRadians / 2f;
        }
    }
}

