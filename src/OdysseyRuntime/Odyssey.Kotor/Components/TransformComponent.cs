using System;
using System.Numerics;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of transform component for KOTOR.
    /// </summary>
    /// <remarks>
    /// Transform Component:
    /// - Based on swkotor2.exe entity transform system
    /// - Located via string references: "XPosition" @ 0x007bd000 (X position field), "YPosition" @ 0x007bcff4 (Y position field), "ZPosition" @ 0x007bcfe8 (Z position field)
    /// - "PositionX" @ 0x007bc474 (position X field), "PositionY" @ 0x007bc468 (position Y field), "PositionZ" @ 0x007bc45c (position Z field)
    /// - "Position" @ 0x007bd154 (position field), "position" @ 0x007ba168 (position constant)
    /// - "positionkey" @ 0x007ba150 (position key field), "positionbezierkey" @ 0x007ba13c (position bezier key field)
    /// - "UpdateDependentPosition" @ 0x007bb984 (update dependent position function), "flarepositions" @ 0x007bac94 (flare positions field)
    /// - "XOrientation" @ 0x007bcfb8 (X orientation field), "YOrientation" @ 0x007bcfc8 (Y orientation field), "ZOrientation" @ 0x007bcfd8 (Z orientation field)
    /// - Position debug: "Position: (%3.2f, %3.2f, %3.2f)" @ 0x007c79a8 (position debug format string)
    /// - Pathfinding position errors:
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510 (pathfinding error)
    ///   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0 (walking collision error)
    ///   - "Bailed the desired position is unsafe." @ 0x007c0584 (unsafe position error)
    ///   - "PathFollowData requesting bad data position %d" @ 0x007ca414 (path follow data error)
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save entity position/orientation to GFF), FUN_004e08e0 @ 0x004e08e0 (load placeable/door position from GIT)
    /// - Position stored at offsets 0x94 (X), 0x98 (Y), 0x9c (Z) in creature objects (in-memory layout)
    /// - Orientation stored at offsets 0xa0 (X), 0xa4 (Y), 0xa8 (Z) as normalized direction vector (in-memory layout)
    /// - FUN_00506550 @ 0x00506550 sets orientation from vector, FUN_004d8390 @ 0x004d8390 normalizes orientation vector
    /// - KOTOR coordinate system:
    ///   - Y-up coordinate system (same as most game engines, Y is vertical)
    ///   - Positions in meters (world-space coordinates)
    ///   - Facing angle in radians (0 = +X axis, counter-clockwise positive) for 2D gameplay
    ///   - Orientation vector (XOrientation, YOrientation, ZOrientation) used for 3D model rendering (normalized direction vector)
    ///   - Scale typically (1,1,1) but can be modified for effects (visual scaling, not physics)
    /// - Transform stored in GFF structures as XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation
    /// - Forward/Right vectors calculated from facing angle for 2D movement (cos/sin pattern matches engine: Forward = (cos(facing), sin(facing), 0))
    /// </remarks>
    public class TransformComponent : ITransformComponent
    {
        private Vector3 _position;
        private float _facing;
        private Vector3 _scale;
        private IEntity _parent;
        private Matrix4x4 _worldMatrixCache;
        private bool _worldMatrixDirty;

        public TransformComponent()
        {
            _position = Vector3.Zero;
            _facing = 0f;
            _scale = Vector3.One;
            _parent = null;
            _worldMatrixDirty = true;
        }

        public TransformComponent(Vector3 position, float facing) : this()
        {
            _position = position;
            _facing = facing;
        }

        #region IComponent Implementation

        public IEntity Owner { get; set; }

        public void OnAttach()
        {
            // Position and facing are managed by this component
        }

        public void OnDetach()
        {
            _parent = null;
        }

        #endregion

        #region ITransformComponent Implementation

        /// <summary>
        /// World position.
        /// </summary>
        public Vector3 Position
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
        public float Facing
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
        public Vector3 Scale
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
        public IEntity Parent
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
        public Vector3 Forward
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
        public Vector3 Right
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
        public Matrix4x4 WorldMatrix
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

        #endregion

        #region Extended Properties

        /// <summary>
        /// Gets the up direction vector (Z axis in KOTOR).
        /// </summary>
        public Vector3 Up
        {
            get { return Vector3.UnitZ; }
        }

        /// <summary>
        /// Facing direction in degrees.
        /// </summary>
        public float FacingDegrees
        {
            get { return _facing * (180f / (float)Math.PI); }
            set { Facing = value * ((float)Math.PI / 180f); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the cached world matrix.
        /// </summary>
        private void UpdateWorldMatrix()
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
        /// Moves the entity by a delta.
        /// </summary>
        public void Translate(Vector3 delta)
        {
            Position = _position + delta;
        }

        /// <summary>
        /// Moves the entity forward by a distance.
        /// </summary>
        public void MoveForward(float distance)
        {
            Position = _position + Forward * distance;
        }

        /// <summary>
        /// Rotates the entity by a delta angle (radians).
        /// </summary>
        public void Rotate(float deltaRadians)
        {
            Facing = _facing + deltaRadians;
        }

        /// <summary>
        /// Sets the facing to look at a target position.
        /// </summary>
        public void LookAt(Vector3 targetPosition)
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
        public float DistanceTo(Vector3 otherPosition)
        {
            return Vector3.Distance(_position, otherPosition);
        }

        /// <summary>
        /// Gets the 2D distance (ignoring Z) to another position.
        /// </summary>
        public float DistanceTo2D(Vector3 otherPosition)
        {
            float dx = _position.X - otherPosition.X;
            float dy = _position.Y - otherPosition.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Gets the angle to another position.
        /// </summary>
        public float AngleTo(Vector3 otherPosition)
        {
            Vector3 direction = otherPosition - _position;
            return (float)Math.Atan2(direction.Y, direction.X);
        }

        /// <summary>
        /// Checks if a target is in front of this entity.
        /// </summary>
        public bool IsInFront(Vector3 targetPosition, float fovRadians = (float)Math.PI)
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

        /// <summary>
        /// Marks the world matrix as needing recalculation.
        /// </summary>
        public void InvalidateMatrix()
        {
            _worldMatrixDirty = true;
        }

        #endregion
    }
}
