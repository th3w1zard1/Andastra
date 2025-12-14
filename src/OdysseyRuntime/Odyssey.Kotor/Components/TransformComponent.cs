using System;
using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of transform component for KOTOR.
    /// </summary>
    /// <remarks>
    /// KOTOR coordinate system:
    /// - Y-up coordinate system (same as most game engines)
    /// - Positions in meters
    /// - Facing angle in radians (0 = +X axis, counter-clockwise)
    /// - Scale typically (1,1,1) but can be modified for effects
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
            // Load position from entity if available
            if (Owner != null)
            {
                _position = Owner.Position;
                _facing = Owner.Facing;
            }
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
                    
                    // Sync to entity if attached
                    if (Owner != null)
                    {
                        Owner.Position = value;
                    }
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
                    
                    // Sync to entity if attached
                    if (Owner != null)
                    {
                        Owner.Facing = normalized;
                    }
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
                var parentTransform = _parent.GetComponent<ITransformComponent>();
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
