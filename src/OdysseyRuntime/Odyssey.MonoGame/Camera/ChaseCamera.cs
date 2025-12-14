using System;
using Stride.Core.Mathematics;
using Stride.Input;
using Stride.Engine;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.Camera
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
        // Initialize chase camera with camera component
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.CameraComponent.html
        // CameraComponent defines camera properties for rendering
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
        // CameraComponent.Entity property gets the entity that owns the camera component
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
        // Transform.Position property gets the current world position of the camera entity
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
        // Vector3.UnitZ is a static property representing forward vector (0, 0, 1)
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/cameras/index.html
        public ChaseCamera([NotNull] CameraComponent camera)
        {
            _camera = camera ?? throw new ArgumentNullException("camera");
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position gets the initial camera position from the entity
            _currentPosition = camera.Entity.Transform.Position;
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3 addition creates look-at point offset from camera position
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position property gets the world position of the follow target entity
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
            Vector3 targetPos = _followTarget.Transform.Position;
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3.UnitY is a static property representing up vector (0, 1, 0)
            // Vector3 multiplication scales the up vector by height offset
            // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
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

            // Smooth interpolation using exponential decay
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3.Lerp(Vector3, Vector3, float) linearly interpolates between two vectors
            // Method signature: static Vector3 Lerp(Vector3 value1, Vector3 value2, float amount)
            // amount: Interpolation factor (0.0 = value1, 1.0 = value2)
            // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
            float t = 1.0f - (float)Math.Exp(-_lagFactor * deltaTime);
            _currentPosition = Vector3.Lerp(_currentPosition, idealPosition, t);
            _currentLookAt = Vector3.Lerp(_currentLookAt, lookAtPoint, t);

            // Apply to camera entity
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position - Sets the world position of the camera entity
            _camera.Entity.Transform.Position = _currentPosition;

            // Look at target
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Matrix.html
            // Matrix.LookAtRH(Vector3, Vector3, Vector3) - Creates a right-handed look-at matrix
            // Method signature: LookAtRH(Vector3 eye, Vector3 target, Vector3 up)
            // eye: Camera position, target: Point to look at, up: Up vector direction
            Matrix lookAtMatrix = Matrix.LookAtRH(_currentPosition, _currentLookAt, Vector3.UnitY);
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Matrix.html
            // Matrix.Invert(ref Matrix, out Matrix) - Inverts a matrix (view to world transform)
            Matrix.Invert(ref lookAtMatrix, out Matrix viewInverse);
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
            // Quaternion.RotationMatrix(Matrix) - Converts rotation matrix to quaternion
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Rotation - Sets the rotation of the camera entity
            _camera.Entity.Transform.Rotation = Quaternion.RotationMatrix(viewInverse);
        }

        private void ProcessInput(float deltaTime, InputManager input)
        {
            // Mouse rotation when right button held
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsMouseButtonDown(MouseButton) checks if a mouse button is currently held down
            // MouseDelta property gets the mouse movement delta since last frame (X, Y)
            // Method signatures: bool IsMouseButtonDown(MouseButton button), Vector2 MouseDelta { get; }
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            if (input.IsMouseButtonDown(MouseButton.Right))
            {
                float sensitivity = 0.2f;
                // Access mouse delta X component for horizontal rotation
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector2.html
                // Vector2.X property gets the X component of the mouse delta vector
                // Method signature: float X { get; set; }
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                _yaw -= input.MouseDelta.X * sensitivity;
                // Access mouse delta Y component for vertical rotation
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector2.html
                // Vector2.Y property gets the Y component of the mouse delta vector
                // Method signature: float Y { get; set; }
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                _pitch -= input.MouseDelta.Y * sensitivity;
                _pitch = MathUtil.Clamp(_pitch, _minPitch, _maxPitch);
            }

            // Scroll wheel for zoom
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // MouseWheelDelta property gets the mouse wheel scroll delta since last frame
            // Method signature: float MouseWheelDelta { get; }
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            float scroll = input.MouseWheelDelta;
            if (Math.Abs(scroll) > 0.01f)
            {
                _distance -= scroll * 0.5f;
                _distance = MathUtil.Clamp(_distance, _minDistance, _maxDistance);
            }

            // Keyboard rotation (arrow keys)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsKeyDown(Keys) checks if a key is currently held down (returns true while key is pressed)
            // Method signature: bool IsKeyDown(Keys key)
            // Keys enum defines keyboard key codes (Left, Right, Up, Down arrow keys)
            // Source: https://doc.stride3d.net/latest/en/manual/input/keyboard.html
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
                // Get direction vector length
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3.Length property gets the length (magnitude) of the vector
                // Method signature: float Length { get; }
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                float maxDist = direction.Length();
                // Normalize direction vector to unit length
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3.Normalize() normalizes the vector in-place to unit length (length = 1.0)
                // Method signature: void Normalize()
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
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
        // Align camera yaw with target entity's facing direction
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
        // Transform.Rotation property gets the rotation quaternion of the target entity
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
        // Vector3.UnitZ is a static property representing forward vector (0, 0, 1)
        // Vector3.Transform(Vector3, Quaternion) transforms a vector by a quaternion rotation
        // Method signature: static Vector3 Transform(Vector3 vector, Quaternion rotation)
        // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
        public void AlignWithTarget()
        {
            if (_followTarget != null)
            {
                // Get target's facing direction and set yaw to match
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Rotation gets the target entity's rotation quaternion
                Quaternion rot = _followTarget.Transform.Rotation;
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3.Transform transforms forward vector by target's rotation to get facing direction
                Vector3 forward = Vector3.Transform(Vector3.UnitZ, rot);

                _yaw = MathUtil.RadiansToDegrees((float)Math.Atan2(forward.X, forward.Z));
            }
        }
    }
}

