using System;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Andastra.Game.Graphics.MonoGame.Camera
{
    /// <summary>
    /// KOTOR-style chase camera that follows the player character.
    /// Supports smooth interpolation, camera collision, and multiple camera modes.
    /// </summary>
    /// <remarks>
    /// Chase Camera:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) camera system
    /// - Located via string references: Camera system references (see CameraController.cs for details)
    /// - "CameraRotate" @ 0x007cb910, "CameraViewAngle" @ 0x007cb940
    /// - "Keyboard Camera Deceleration" @ 0x007c834c, "Keyboard Camera Acceleration" @ 0x007c836c
    /// - "Keyboard Camera DPS" @ 0x007c838c (keyboard camera degrees per second)
    /// - Original implementation: KOTOR uses chase camera that follows player character
    /// - Camera follows: Camera maintains distance and height offset from target
    /// - Camera rotation: Controllable pitch and yaw with keyboard/mouse input
    /// - Camera collision: Raycast from target to camera position to avoid seeing through walls
    /// - Smooth interpolation: Camera position interpolates smoothly to target position
    /// </remarks>
    public class ChaseCamera
    {
        // Current camera state
        private Vector3 _currentPosition;
        private Vector3 _currentLookAt;
        private Vector3 _targetPosition;
        private Vector3 _targetLookAt;

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

        // Mouse input settings
        private bool _invertMouseY = false;

        // Collision avoidance
        private Func<Vector3, Vector3, bool> _raycastCallback;

        /// <summary>
        /// Gets or sets the camera distance from target.
        /// </summary>
        public float Distance
        {
            get { return _distance; }
            set { _distance = MathHelper.Clamp(value, _minDistance, _maxDistance); }
        }

        /// <summary>
        /// Gets or sets the camera height offset from target.
        /// </summary>
        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>
        /// Gets or sets the camera pitch angle in degrees.
        /// </summary>
        public float Pitch
        {
            get { return _pitch; }
            set { _pitch = MathHelper.Clamp(value, _minPitch, _maxPitch); }
        }

        /// <summary>
        /// Gets or sets the camera yaw angle in degrees.
        /// </summary>
        public float Yaw
        {
            get { return _yaw; }
            set { _yaw = value; }
        }

        /// <summary>
        /// Gets or sets the interpolation lag factor (higher = smoother but slower).
        /// </summary>
        public float LagFactor
        {
            get { return _lagFactor; }
            set { _lagFactor = Math.Max(1.0f, value); }
        }

        /// <summary>
        /// Gets or sets whether mouse Y axis is inverted for camera pitch control.
        /// When true, moving mouse up looks down and moving mouse down looks up.
        /// </summary>
        public bool InvertMouseY
        {
            get { return _invertMouseY; }
            set { _invertMouseY = value; }
        }

        /// <summary>
        /// Gets the current camera position.
        /// </summary>
        public Vector3 Position
        {
            get { return _currentPosition; }
        }

        /// <summary>
        /// Gets the current camera look-at target.
        /// </summary>
        public Vector3 LookAt
        {
            get { return _currentLookAt; }
        }

        /// <summary>
        /// Gets the view matrix for this camera.
        /// </summary>
        public Matrix ViewMatrix
        {
            get
            {
                // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Matrix.html
                // Matrix.CreateLookAt creates a view matrix for camera positioning
                // Method signature: static Matrix CreateLookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
                // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_RotateMoveCamera.html
                return Matrix.CreateLookAt(_currentPosition, _currentLookAt, Vector3.Up);
            }
        }

        /// <summary>
        /// Sets the target position to follow.
        /// </summary>
        public void SetTarget(Vector3 targetPosition, Vector3 targetLookAt)
        {
            _targetLookAt = targetLookAt;
            UpdateTargetPosition(targetPosition);
        }

        /// <summary>
        /// Updates the camera based on target position and input.
        /// </summary>
        public void Update(float deltaTime, Vector3 targetPosition, KeyboardState keyboardState, MouseState mouseState)
        {
            // Process input for camera rotation
            ProcessInput(deltaTime, keyboardState, mouseState);

            // Update target position
            UpdateTargetPosition(targetPosition);

            // Interpolate camera position smoothly
            float lerpFactor = 1.0f - (float)Math.Exp(-_lagFactor * deltaTime);
            _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, lerpFactor);
            _currentLookAt = Vector3.Lerp(_currentLookAt, _targetLookAt, lerpFactor);

            // Apply collision avoidance if callback is set
            if (_raycastCallback != null)
            {
                ApplyCollisionAvoidance();
            }
        }

        private void UpdateTargetPosition(Vector3 targetPosition)
        {
            // Calculate camera position based on distance, height, pitch, and yaw
            // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Matrix.html
            // Matrix.CreateRotationX/Y creates rotation matrices
            // Method signature: static Matrix CreateRotationX(float radians)
            // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_RotateMoveCamera.html
            float pitchRad = MathHelper.ToRadians(_pitch);
            float yawRad = MathHelper.ToRadians(_yaw);

            // Calculate offset from target
            Vector3 offset = new Vector3(0, _height, -_distance);

            // Apply pitch rotation (around X axis)
            Matrix pitchMatrix = Matrix.CreateRotationX(pitchRad);
            offset = Vector3.Transform(offset, pitchMatrix);

            // Apply yaw rotation (around Y axis)
            Matrix yawMatrix = Matrix.CreateRotationY(yawRad);
            offset = Vector3.Transform(offset, yawMatrix);

            // Set target position
            _targetPosition = targetPosition + offset;
        }

        private void ProcessInput(float deltaTime, KeyboardState keyboardState, MouseState mouseState)
        {
            float rotateSpeed = 90.0f * deltaTime; // Degrees per second

            // Mouse input for camera rotation
            if (mouseState.RightButton == ButtonState.Pressed)
            {
                int deltaX = mouseState.X - _lastMouseState.X;
                int deltaY = mouseState.Y - _lastMouseState.Y;

                // Apply mouse Y inversion if enabled
                if (_invertMouseY)
                {
                    deltaY = -deltaY;
                }

                _yaw += deltaX * 0.1f;
                _pitch += deltaY * 0.1f;
                _pitch = MathHelper.Clamp(_pitch, _minPitch, _maxPitch);
            }

            // Keyboard input for camera rotation
            if (keyboardState.IsKeyDown(Keys.Q))
            {
                _yaw -= rotateSpeed;
            }
            if (keyboardState.IsKeyDown(Keys.E))
            {
                _yaw += rotateSpeed;
            }
            if (keyboardState.IsKeyDown(Keys.R))
            {
                _pitch -= rotateSpeed;
                _pitch = MathHelper.Clamp(_pitch, _minPitch, _maxPitch);
            }
            if (keyboardState.IsKeyDown(Keys.F))
            {
                _pitch += rotateSpeed;
                _pitch = MathHelper.Clamp(_pitch, _minPitch, _maxPitch);
            }

            // Mouse wheel for distance
            int scrollDelta = mouseState.ScrollWheelValue - _lastMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _distance -= scrollDelta * 0.01f;
                _distance = MathHelper.Clamp(_distance, _minDistance, _maxDistance);
            }

            _lastMouseState = mouseState;
        }

        private MouseState _lastMouseState;

        private void ApplyCollisionAvoidance()
        {
            // Raycast-based collision avoidance
            // Check if there's a collision between target position and camera position
            // If blocked, move camera closer to target along the line
            if (_raycastCallback != null)
            {
                // Get the look-at position (target position)
                Vector3 targetPos = _targetLookAt;
                Vector3 cameraPos = _currentPosition;

                // Check if there's a collision from target to camera
                bool isBlocked = _raycastCallback(targetPos, cameraPos);

                if (isBlocked)
                {
                    // If blocked, try to find a closer position that's not blocked
                    // Use binary search to find the closest valid position
                    Vector3 direction = cameraPos - targetPos;
                    float maxDistance = direction.Length();
                    direction.Normalize();

                    float minDistance = 0.5f; // Minimum distance from target
                    float currentDistance = maxDistance;
                    int iterations = 8; // Binary search iterations

                    for (int i = 0; i < iterations; i++)
                    {
                        float testDistance = (minDistance + currentDistance) * 0.5f;
                        Vector3 testPosition = targetPos + direction * testDistance;

                        bool testBlocked = _raycastCallback(targetPos, testPosition);
                        if (testBlocked)
                        {
                            currentDistance = testDistance;
                        }
                        else
                        {
                            minDistance = testDistance;
                        }
                    }

                    // Set camera to the closest valid position
                    _currentPosition = targetPos + direction * minDistance;
                }
            }
        }

        /// <summary>
        /// Sets a callback for raycast collision detection.
        /// </summary>
        public void SetRaycastCallback(Func<Vector3, Vector3, bool> callback)
        {
            _raycastCallback = callback;
        }

        /// <summary>
        /// Resets camera to default position behind target.
        /// </summary>
        public void Reset()
        {
            _yaw = 0f;
            _pitch = 25.0f;
            _distance = 6.0f;
            _height = 3.0f;
        }
    }
}
