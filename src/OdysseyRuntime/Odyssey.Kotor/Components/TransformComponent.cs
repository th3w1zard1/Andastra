using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for entity position and orientation.
    /// </summary>
    public class TransformComponent : IComponent, ITransformComponent
    {
        private Vector3 _position;
        private float _facing;

        public TransformComponent()
        {
            _position = Vector3.Zero;
            _facing = 0f;
            Scale = 1f;
        }

        /// <summary>
        /// Entity position in world space.
        /// </summary>
        public Vector3 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        /// <summary>
        /// Entity facing angle in radians.
        /// </summary>
        public float Facing
        {
            get { return _facing; }
            set { _facing = value; }
        }

        /// <summary>
        /// Entity scale factor.
        /// </summary>
        public float Scale { get; set; }

        /// <summary>
        /// Gets the forward direction vector.
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                return new Vector3(
                    (float)System.Math.Sin(_facing),
                    (float)System.Math.Cos(_facing),
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
                    (float)System.Math.Cos(_facing),
                    -(float)System.Math.Sin(_facing),
                    0f
                );
            }
        }

        /// <summary>
        /// Sets facing to look at a target position.
        /// </summary>
        public void LookAt(Vector3 target)
        {
            Vector3 direction = target - _position;
            if (direction.LengthSquared() > 0.0001f)
            {
                _facing = (float)System.Math.Atan2(direction.X, direction.Y);
            }
        }

        /// <summary>
        /// Moves the entity forward by the specified distance.
        /// </summary>
        public void MoveForward(float distance)
        {
            _position += Forward * distance;
        }

        /// <summary>
        /// Rotates the entity by the specified angle.
        /// </summary>
        public void Rotate(float angle)
        {
            _facing += angle;
            // Normalize to [0, 2*PI]
            while (_facing < 0)
            {
                _facing += (float)(System.Math.PI * 2);
            }
            while (_facing >= System.Math.PI * 2)
            {
                _facing -= (float)(System.Math.PI * 2);
            }
        }
    }
}
