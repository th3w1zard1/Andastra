using System;
using Stride.Core.Mathematics;
using Stride.Input;
using Stride.Engine;
using JetBrains.Annotations;

namespace Odyssey.Stride.Camera
{
    /// <summary>
    /// KOTOR-style chase camera that follows the player character.
    /// Supports smooth interpolation, camera collision, and multiple camera modes.
    /// </summary>
    public class ChaseCamera
    {
        private readonly CameraComponent _camera;
        private Entity _followTarget;

        // Current camera state
        private Vector3 _currentPosition;
        private Vector3 _currentLookAt;

        // Chase camera parameters
        private float _distance = 6.0f;
        private float _height = 3.0f;
        private float _pitch = 25.0f; // Degrees down from horizontal
        private float _yaw = 0f;      // Angle around target
        private float _lagFactor = 8.0f;

        // Camera constraints
        private float _minDistance = 2.0f;
        private float _maxDistance = 15.0f;
        private float _minPitch = 5.0f;
        private float _maxPitch = 80.0f;

        // Input smoothing
        private float _yawVelocity = 0f;
        private float _pitchVelocity = 0f;

        // Collision avoidance
        private Func<Vector3, Vector3, bool> _raycastCallback;

        /// <summary>
        /// Gets or sets the camera distance from target.
        /// </summary>
        public float Distance
        {
            get { return _distance; }
            set { _distance = MathUtil.Clamp(value, _minDistance, _maxDistance); }
        }

        /// <summary>
        /// Gets or sets the camera height offset.
        /// </summary>
        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>
        /// Gets or sets the camera pitch in degrees.
        /// </summary>
        public float Pitch
        {
            get { return _pitch; }
            set { _pitch = MathUtil.Clamp(value, _minPitch, _maxPitch); }
        }

        /// <summary>
        /// Gets or sets the camera yaw in degrees.
        /// </summary>
        public float Yaw
        {
            get { return _yaw; }
            set { _yaw = value; }
        }

        /// <summary>
        /// Gets or sets the interpolation lag factor.
        /// Higher values = faster following.
        /// </summary>
        public float LagFactor
        {
            get { return _lagFactor; }
            set { _lagFactor = Math.Max(0.1f, value); }
        }

        /// <summary>
        /// Gets or sets the target entity to follow.
        /// </summary>
        public Entity FollowTarget
        {
            get { return _followTarget; }
            set { _followTarget = value; }
        }

        /// <summary>
        /// Gets the current camera position.
        /// </summary>
        public Vector3 Position
        {
            get { return _currentPosition; }
        }

        /// <summary>
        /// Creates a new chase camera.
        /// </summary>
        /// <param name="camera">The Stride camera component to control.</param>
        public ChaseCamera([NotNull] CameraComponent camera)
        {
            _camera = camera ?? throw new ArgumentNullException("camera");
            _currentPosition = camera.Entity.Transform.Position;
            _currentLookAt = _currentPosition + Vector3.UnitZ;
        }

        /// <summary>
        /// Sets the raycast callback for collision avoidance.
        /// </summary>
        /// <param name="raycast">Function that returns true if ray is blocked.</param>
        public void SetRaycastCallback(Func<Vector3, Vector3, bool> raycast)
        {
            _raycastCallback = raycast;
        }

        /// <summary>
        /// Updates the camera position based on input and target.
        /// </summary>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <param name="input">Input manager for mouse/keyboard control.</param>
        public void Update(float deltaTime, InputManager input)
        {
            // Process input
            ProcessInput(deltaTime, input);

            // Update camera position
            UpdatePosition(deltaTime);
        }

        /// <summary>
        /// Updates the camera without input processing.
        /// </summary>
        /// <param name="deltaTime">Frame delta time.</param>
        public void UpdatePosition(float deltaTime)
        {
            if (_followTarget == null)
            {
                return;
            }

            // Get target position (character position + eye height)
            Vector3 targetPos = _followTarget.Transform.Position;
            Vector3 lookAtPoint = targetPos + Vector3.UnitY * _height * 0.5f;

            // Calculate ideal camera position
            float yawRad = MathUtil.DegreesToRadians(_yaw);
            float pitchRad = MathUtil.DegreesToRadians(_pitch);

            // Calculate offset from target
            Vector3 offset = new Vector3(
                -(float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad) * _distance,
                (float)Math.Sin(pitchRad) * _distance + _height,
                -(float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad) * _distance
            );

            Vector3 idealPosition = targetPos + offset;

            // Check for collision
            if (_raycastCallback != null)
            {
                idealPosition = HandleCollision(lookAtPoint, idealPosition);
            }

            // Smooth interpolation
            float t = 1.0f - (float)Math.Exp(-_lagFactor * deltaTime);
            _currentPosition = Vector3.Lerp(_currentPosition, idealPosition, t);
            _currentLookAt = Vector3.Lerp(_currentLookAt, lookAtPoint, t);

            // Apply to camera entity
            _camera.Entity.Transform.Position = _currentPosition;

            // Look at target
            Matrix lookAtMatrix = Matrix.LookAtRH(_currentPosition, _currentLookAt, Vector3.UnitY);
            Matrix.Invert(ref lookAtMatrix, out Matrix viewInverse);
            _camera.Entity.Transform.Rotation = Quaternion.RotationMatrix(viewInverse);
        }

        private void ProcessInput(float deltaTime, InputManager input)
        {
            // Mouse rotation when right button held
            if (input.IsMouseButtonDown(MouseButton.Right))
            {
                float sensitivity = 0.2f;
                _yaw -= input.MouseDelta.X * sensitivity;
                _pitch -= input.MouseDelta.Y * sensitivity;
                _pitch = MathUtil.Clamp(_pitch, _minPitch, _maxPitch);
            }

            // Scroll wheel for zoom
            float scroll = input.MouseWheelDelta;
            if (Math.Abs(scroll) > 0.01f)
            {
                _distance -= scroll * 0.5f;
                _distance = MathUtil.Clamp(_distance, _minDistance, _maxDistance);
            }

            // Keyboard rotation (arrow keys)
            float rotateSpeed = 90f * deltaTime; // 90 degrees per second

            if (input.IsKeyDown(Keys.Left))
            {
                _yaw -= rotateSpeed;
            }
            if (input.IsKeyDown(Keys.Right))
            {
                _yaw += rotateSpeed;
            }
            if (input.IsKeyDown(Keys.Up))
            {
                _pitch -= rotateSpeed * 0.5f;
                _pitch = MathUtil.Clamp(_pitch, _minPitch, _maxPitch);
            }
            if (input.IsKeyDown(Keys.Down))
            {
                _pitch += rotateSpeed * 0.5f;
                _pitch = MathUtil.Clamp(_pitch, _minPitch, _maxPitch);
            }
        }

        private Vector3 HandleCollision(Vector3 target, Vector3 camera)
        {
            // Check if line from target to camera is blocked
            if (_raycastCallback != null && _raycastCallback(target, camera))
            {
                // Binary search to find closest unblocked position
                Vector3 direction = camera - target;
                float maxDist = direction.Length();
                direction.Normalize();

                float minDist = 0.5f; // Minimum distance from target
                float testDist = maxDist;

                for (int i = 0; i < 8; i++) // 8 iterations of binary search
                {
                    float mid = (minDist + testDist) * 0.5f;
                    Vector3 testPos = target + direction * mid;

                    if (_raycastCallback(target, testPos))
                    {
                        testDist = mid;
                    }
                    else
                    {
                        minDist = mid;
                    }
                }

                // Use the closest valid position with small offset
                return target + direction * (minDist - 0.2f);
            }

            return camera;
        }

        /// <summary>
        /// Immediately snaps the camera to its target position.
        /// </summary>
        public void SnapToTarget()
        {
            if (_followTarget == null)
            {
                return;
            }

            Vector3 targetPos = _followTarget.Transform.Position;
            Vector3 lookAtPoint = targetPos + Vector3.UnitY * _height * 0.5f;

            float yawRad = MathUtil.DegreesToRadians(_yaw);
            float pitchRad = MathUtil.DegreesToRadians(_pitch);

            Vector3 offset = new Vector3(
                -(float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad) * _distance,
                (float)Math.Sin(pitchRad) * _distance + _height,
                -(float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad) * _distance
            );

            _currentPosition = targetPos + offset;
            _currentLookAt = lookAtPoint;

            UpdatePosition(0);
        }

        /// <summary>
        /// Rotates the camera to face a specific direction.
        /// </summary>
        /// <param name="targetYaw">Target yaw in degrees.</param>
        /// <param name="instant">If true, snap immediately.</param>
        public void SetYaw(float targetYaw, bool instant = false)
        {
            if (instant)
            {
                _yaw = targetYaw;
                _yawVelocity = 0;
            }
            else
            {
                // Will smoothly interpolate on next update
                _yaw = targetYaw;
            }
        }

        /// <summary>
        /// Resets the camera to face the same direction as the target.
        /// </summary>
        public void AlignWithTarget()
        {
            if (_followTarget != null)
            {
                // Get target's facing direction and set yaw to match
                Quaternion rot = _followTarget.Transform.Rotation;
                Vector3 forward = Vector3.Transform(Vector3.UnitZ, rot);

                _yaw = MathUtil.RadiansToDegrees((float)Math.Atan2(forward.X, forward.Z));
            }
        }
    }
}

