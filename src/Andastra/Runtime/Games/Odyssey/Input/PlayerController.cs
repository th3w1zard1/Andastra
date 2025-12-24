using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Navigation;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey.Input
{
    /// <summary>
    /// Handles player input and movement using click-to-move with pathfinding.
    /// </summary>
    /// <remarks>
    /// Player Controller (Input) - Odyssey-specific:
    /// - Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 (movement handling), CExoInputInternal input system
    /// - Located via string references: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "DirectInput8Create" @ 0x0080a6ac
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): CClientExoAppInternal::ProcessInput, HandleLMouseDown, HandleRMouseDown, Input_WalkToWayPoint - similar click-to-move system
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): "Initialize - Input", "InputSystem", "Mouse::LeftClick", "Keyboard::Button_*" - UnrealScript-based input system
    /// - Inheritance: Base class BasePlayerController (Runtime.Games.Common) - abstract player input, Odyssey override (Runtime.Games.Odyssey) - click-to-move with pathfinding
    /// - Original implementation: Handles click-to-move with pathfinding on walkmesh, uses DirectInput8 for input, NavigationMesh for pathfinding
    /// - Click-to-move: Left-click on walkmesh initiates pathfinding and movement (UpdateCreatureMovement @ 0x0054be70 handles movement)
    /// - Walk/run speed determined by entity stats (WalkSpeed/RunSpeed from appearance.2da)
    /// </remarks>
    public class PlayerController
    {
        private readonly IEntity _player;
        private readonly NavigationMesh _navMesh;

        private List<Vector3> _currentPath;
        private int _pathIndex;
        private bool _isMoving;

        // Movement parameters
        private float _walkSpeed = 2.5f;
        private float _runSpeed = 5.0f;
        private float _arrivalThreshold = 0.3f;

        // Current movement state
        private bool _isRunning = true;
        private Vector3 _destination;

        /// <summary>
        /// Gets or sets the walk speed in meters per second.
        /// </summary>
        public float WalkSpeed
        {
            get { return _walkSpeed; }
            set { _walkSpeed = Math.Max(0.1f, value); }
        }

        /// <summary>
        /// Gets or sets the run speed in meters per second.
        /// </summary>
        public float RunSpeed
        {
            get { return _runSpeed; }
            set { _runSpeed = Math.Max(0.1f, value); }
        }

        /// <summary>
        /// Gets whether the player is currently moving.
        /// </summary>
        public bool IsMoving
        {
            get { return _isMoving; }
        }

        /// <summary>
        /// Gets the player entity.
        /// </summary>
        public IEntity Player
        {
            get { return _player; }
        }

        /// <summary>
        /// Creates a new player controller.
        /// </summary>
        /// <param name="player">The player entity to control.</param>
        /// <param name="navMesh">The navigation mesh for pathfinding.</param>
        public PlayerController([NotNull] IEntity player, NavigationMesh navMesh)
        {
            _player = player ?? throw new ArgumentNullException("player");
            _navMesh = navMesh;
            _currentPath = new List<Vector3>();
        }

        /// <summary>
        /// Handles a click on the world at the given position.
        /// </summary>
        /// <param name="worldPosition">The clicked world position.</param>
        /// <param name="run">Whether to run to the destination.</param>
        public void MoveToPosition(Vector3 worldPosition, bool run = true)
        {
            if (_navMesh == null)
            {
                // No navmesh - direct movement
                MoveDirectly(worldPosition, run);
                return;
            }

            ITransformComponent transform = _player.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            Vector3 start = transform.Position;

            // Find path
            IList<Vector3> path = _navMesh.FindPath(start, worldPosition);
            if (path == null || path.Count == 0)
            {
                Console.WriteLine("[PlayerController] No path found to destination");
                return;
            }

            // Start following path
            _currentPath = new List<Vector3>(path);
            _pathIndex = 0;
            _isMoving = true;
            _isRunning = run;
            _destination = worldPosition;

            Console.WriteLine("[PlayerController] Path found with " + _currentPath.Count + " waypoints");
        }

        /// <summary>
        /// Moves directly to a target without pathfinding.
        /// </summary>
        public void MoveDirectly(Vector3 worldPosition, bool run = true)
        {
            _currentPath = new List<Vector3> { worldPosition };
            _pathIndex = 0;
            _isMoving = true;
            _isRunning = run;
            _destination = worldPosition;
        }

        /// <summary>
        /// Stops the current movement.
        /// </summary>
        public void Stop()
        {
            _isMoving = false;
            _currentPath.Clear();
            _pathIndex = 0;
        }

        /// <summary>
        /// Updates the player movement along the path.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        public void Update(float deltaTime)
        {
            if (!_isMoving || _currentPath.Count == 0)
            {
                return;
            }

            ITransformComponent transform = _player.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            Vector3 currentPos = transform.Position;
            Vector3 targetWaypoint = _currentPath[_pathIndex];

            // Calculate direction to waypoint
            Vector3 toTarget = targetWaypoint - currentPos;
            toTarget.Y = 0; // Keep movement on horizontal plane
            float distance = toTarget.Length();

            // Check if we've reached the waypoint
            if (distance <= _arrivalThreshold)
            {
                _pathIndex++;
                if (_pathIndex >= _currentPath.Count)
                {
                    // Reached destination
                    OnDestinationReached();
                    return;
                }
                targetWaypoint = _currentPath[_pathIndex];
                toTarget = targetWaypoint - currentPos;
                toTarget.Y = 0;
                distance = toTarget.Length();
            }

            // Move toward waypoint
            if (distance > 0.001f)
            {
                var direction = Vector3.Normalize(toTarget);
                float speed = _isRunning ? _runSpeed : _walkSpeed;
                float moveDistance = speed * deltaTime;

                // Don't overshoot
                if (moveDistance > distance)
                {
                    moveDistance = distance;
                }

                Vector3 newPos = currentPos + direction * moveDistance;

                // Project onto navmesh if available
                if (_navMesh != null)
                {
                    float groundHeight;
                    Vector3 projectedPos;
                    if (_navMesh.ProjectToSurface(newPos, out projectedPos, out groundHeight))
                    {
                        newPos = projectedPos;
                    }
                }

                transform.Position = newPos;

                // Update facing direction (Y-up system: Atan2(Y, X) for 2D plane facing)
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
            }
        }

        /// <summary>
        /// Handles click on an entity for interaction.
        /// </summary>
        /// <param name="target">The clicked entity.</param>
        public void InteractWith(IEntity target)
        {
            if (target == null)
            {
                return;
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return;
            }

            // Calculate approach position (in front of target)
            Vector3 targetPos = targetTransform.Position;
            Vector3 playerPos = Vector3.Zero;

            ITransformComponent playerTransform = _player.GetComponent<ITransformComponent>();
            if (playerTransform != null)
            {
                playerPos = playerTransform.Position;
            }

            // Direction from target to player
            Vector3 toPlayer = playerPos - targetPos;
            toPlayer.Y = 0;
            if (toPlayer.LengthSquared() > 0.01f)
            {
                toPlayer = Vector3.Normalize(toPlayer);
            }
            else
            {
                toPlayer = Vector3.UnitZ;
            }

            // Approach position is slightly in front of target
            Vector3 approachPos = targetPos + toPlayer * 1.5f;

            // Move to approach position, then interact
            MoveToPosition(approachPos, true);

            // Queue interaction after movement
            // The game session should check for completed movement and trigger interaction
        }

        /// <summary>
        /// Gets the current destination if moving.
        /// </summary>
        public Vector3? GetDestination()
        {
            if (_isMoving && _currentPath.Count > 0)
            {
                return _destination;
            }
            return null;
        }

        /// <summary>
        /// Event fired when the destination is reached.
        /// </summary>
        public event Action DestinationReached;

        private void OnDestinationReached()
        {
            _isMoving = false;
            _currentPath.Clear();
            _pathIndex = 0;

            Console.WriteLine("[PlayerController] Destination reached");

            DestinationReached?.Invoke();
        }

        /// <summary>
        /// Performs a raycast from the screen position to find the clicked world position.
        /// </summary>
        /// <param name="screenX">Screen X coordinate (0-1).</param>
        /// <param name="screenY">Screen Y coordinate (0-1).</param>
        /// <param name="viewMatrix">Camera view matrix.</param>
        /// <param name="projectionMatrix">Camera projection matrix.</param>
        /// <param name="worldPosition">Output world position on navmesh.</param>
        /// <returns>True if a valid position was found.</returns>
        public bool ScreenToWorld(
            float screenX,
            float screenY,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.Zero;

            if (_navMesh == null)
            {
                return false;
            }

            // Convert screen coordinates to NDC
            float ndcX = screenX * 2.0f - 1.0f;
            float ndcY = 1.0f - screenY * 2.0f;

            // Create ray from camera
            Matrix4x4 invView;
            Matrix4x4 invProj;

            if (!Matrix4x4.Invert(viewMatrix, out invView) ||
                !Matrix4x4.Invert(projectionMatrix, out invProj))
            {
                return false;
            }

            // Near and far points in clip space
            Vector4 nearPoint = new Vector4(ndcX, ndcY, 0, 1);
            Vector4 farPoint = new Vector4(ndcX, ndcY, 1, 1);

            // Transform to view space
            nearPoint = Vector4.Transform(nearPoint, invProj);
            farPoint = Vector4.Transform(farPoint, invProj);

            // Perspective divide
            nearPoint /= nearPoint.W;
            farPoint /= farPoint.W;

            // Transform to world space
            Vector3 rayOrigin = Vector3.Transform(new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z), invView);
            Vector3 rayEnd = Vector3.Transform(new Vector3(farPoint.X, farPoint.Y, farPoint.Z), invView);

            Vector3 rayDir = Vector3.Normalize(rayEnd - rayOrigin);

            // Raycast against navmesh
            return _navMesh.Raycast(rayOrigin, rayDir, 1000f, out worldPosition);
        }
    }
}

