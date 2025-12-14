using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Navigation;

namespace Odyssey.Core.Movement
{
    /// <summary>
    /// Controls character movement in the game world.
    /// </summary>
    /// <remarks>
    /// KOTOR Character Movement:
    /// - Point-and-click movement (click destination, character walks/runs)
    /// - Walkmesh-constrained movement
    /// - Automatic pathfinding around obstacles
    /// - Smooth turning towards movement direction
    /// - Speed transitions (walk/run based on distance)
    /// - Door and trigger interaction detection
    /// </remarks>
    public class CharacterController
    {
        private readonly IEntity _entity;
        private readonly IWorld _world;
        private readonly NavigationMesh _navMesh;

        private Vector3 _destination;
        private List<Vector3> _currentPath;
        private int _currentPathIndex;
        private bool _isMoving;
        private bool _isRunning;
        private float _turnSpeed;
        private float _targetFacing;

        /// <summary>
        /// Movement state.
        /// </summary>
        public MovementState State { get; private set; }

        /// <summary>
        /// Current movement speed.
        /// </summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// Whether the character is currently moving.
        /// </summary>
        public bool IsMoving
        {
            get { return _isMoving; }
        }

        /// <summary>
        /// Whether the character is running.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>
        /// Current destination.
        /// </summary>
        public Vector3 Destination
        {
            get { return _destination; }
        }

        /// <summary>
        /// Distance threshold for reaching waypoints.
        /// </summary>
        public float WaypointReachedDistance { get; set; }

        /// <summary>
        /// Distance threshold for switching to walking.
        /// </summary>
        public float WalkDistance { get; set; }

        /// <summary>
        /// Turn speed in radians per second.
        /// </summary>
        public float TurnSpeedRadians { get; set; }

        /// <summary>
        /// Event fired when destination reached.
        /// </summary>
        public event Action OnDestinationReached;

        /// <summary>
        /// Event fired when movement blocked.
        /// </summary>
        public event Action<Vector3> OnMovementBlocked;

        /// <summary>
        /// Event fired when path changes.
        /// </summary>
        public event Action<List<Vector3>> OnPathChanged;

        /// <summary>
        /// Event fired when entity enters a trigger.
        /// </summary>
        public event Action<IEntity> OnTriggerEnter;

        /// <summary>
        /// Event fired when entity exits a trigger.
        /// </summary>
        public event Action<IEntity> OnTriggerExit;

        public CharacterController(IEntity entity, IWorld world, NavigationMesh navMesh)
        {
            _entity = entity ?? throw new ArgumentNullException("entity");
            _world = world ?? throw new ArgumentNullException("world");
            _navMesh = navMesh;

            _currentPath = new List<Vector3>();
            _currentPathIndex = 0;
            _isMoving = false;
            _isRunning = false;

            // Default values
            WaypointReachedDistance = 0.5f;
            WalkDistance = 3.0f;
            TurnSpeedRadians = (float)Math.PI * 2f; // 360 degrees per second

            State = MovementState.Idle;
        }

        #region Movement Commands

        /// <summary>
        /// Moves to a destination point.
        /// </summary>
        /// <param name="destination">Target position.</param>
        /// <param name="run">Whether to run.</param>
        /// <returns>True if path found.</returns>
        public bool MoveTo(Vector3 destination, bool run = true)
        {
            var currentPos = GetCurrentPosition();

            // Check if destination is valid
            if (_navMesh != null)
            {
                if (!_navMesh.IsPointOnMesh(destination))
                {
                    // Try to find nearest valid point
                    var nearestPoint = _navMesh.GetNearestPoint(destination);
                    if (nearestPoint.HasValue)
                    {
                        destination = nearestPoint.Value;
                    }
                    else
                    {
                        return false;
                    }
                }

                // Find path
                var path = _navMesh.FindPath(currentPos, destination);
                if (path == null || path.Count == 0)
                {
                    if (OnMovementBlocked != null)
                    {
                        OnMovementBlocked(destination);
                    }
                    return false;
                }

                _currentPath = path;
            }
            else
            {
                // No navmesh, use direct path
                _currentPath = new List<Vector3> { destination };
            }

            _destination = destination;
            _currentPathIndex = 0;
            _isMoving = true;
            _isRunning = run;
            State = run ? MovementState.Running : MovementState.Walking;

            if (OnPathChanged != null)
            {
                OnPathChanged(_currentPath);
            }

            return true;
        }

        /// <summary>
        /// Moves towards an entity.
        /// </summary>
        /// <param name="target">Target entity.</param>
        /// <param name="stoppingDistance">Distance to stop from target.</param>
        /// <param name="run">Whether to run.</param>
        public bool MoveToEntity(IEntity target, float stoppingDistance = 1.5f, bool run = true)
        {
            if (target == null)
            {
                return false;
            }

            var targetTransform = target.GetComponent<Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return false;
            }

            // Calculate position offset by stopping distance
            var targetPos = targetTransform.Position;
            var currentPos = GetCurrentPosition();
            var direction = Vector3.Normalize(currentPos - targetPos);

            var destination = targetPos + direction * stoppingDistance;

            return MoveTo(destination, run);
        }

        /// <summary>
        /// Stops all movement.
        /// </summary>
        public void Stop()
        {
            _isMoving = false;
            _isRunning = false;
            _currentPath.Clear();
            _currentPathIndex = 0;
            CurrentSpeed = 0;
            State = MovementState.Idle;
        }

        /// <summary>
        /// Turns to face a direction.
        /// </summary>
        /// <param name="targetFacing">Target facing in radians.</param>
        public void FaceTo(float targetFacing)
        {
            _targetFacing = NormalizeAngle(targetFacing);
            State = MovementState.Turning;
        }

        /// <summary>
        /// Turns to face a position.
        /// </summary>
        public void FaceTowards(Vector3 targetPosition)
        {
            var currentPos = GetCurrentPosition();
            var direction = targetPosition - currentPos;
            var facing = (float)Math.Atan2(direction.Y, direction.X);
            FaceTo(facing);
        }

        /// <summary>
        /// Turns to face an entity.
        /// </summary>
        public void FaceTowards(IEntity target)
        {
            if (target == null)
            {
                return;
            }

            var targetTransform = target.GetComponent<Interfaces.Components.ITransformComponent>();
            if (targetTransform != null)
            {
                FaceTowards(targetTransform.Position);
            }
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates the character controller.
        /// </summary>
        /// <param name="deltaTime">Time since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            if (State == MovementState.Turning)
            {
                UpdateTurning(deltaTime);
            }
            else if (_isMoving)
            {
                UpdateMovement(deltaTime);
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            if (_currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
            {
                ReachDestination();
                return;
            }

            var currentPos = GetCurrentPosition();
            var targetWaypoint = _currentPath[_currentPathIndex];

            // Calculate direction and distance
            var direction = targetWaypoint - currentPos;
            direction.Z = 0; // Keep movement on XY plane
            float distanceToWaypoint = direction.Length();

            // Check if we reached the waypoint
            if (distanceToWaypoint < WaypointReachedDistance)
            {
                _currentPathIndex++;

                if (_currentPathIndex >= _currentPath.Count)
                {
                    ReachDestination();
                    return;
                }

                // Update to next waypoint
                targetWaypoint = _currentPath[_currentPathIndex];
                direction = targetWaypoint - currentPos;
                direction.Z = 0;
                distanceToWaypoint = direction.Length();
            }

            // Calculate target facing
            float targetFacing = (float)Math.Atan2(direction.Y, direction.X);

            // Update facing (smooth turn)
            var transform = _entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                float currentFacing = transform.Facing;
                float facingDelta = NormalizeAngle(targetFacing - currentFacing);

                // Clamp rotation speed
                float maxRotation = TurnSpeedRadians * deltaTime;
                if (Math.Abs(facingDelta) > maxRotation)
                {
                    facingDelta = Math.Sign(facingDelta) * maxRotation;
                }

                transform.Facing = NormalizeAngle(currentFacing + facingDelta);
            }

            // Calculate speed
            var stats = _entity.GetComponent<Interfaces.Components.IStatsComponent>();
            float baseSpeed = stats != null ? (_isRunning ? stats.RunSpeed : stats.WalkSpeed) : 3.5f;

            // Switch to walking when close to destination
            if (distanceToWaypoint < WalkDistance && _isRunning)
            {
                baseSpeed = stats != null ? stats.WalkSpeed : 2.0f;
                State = MovementState.Walking;
            }

            CurrentSpeed = baseSpeed;

            // Calculate movement
            float moveDistance = baseSpeed * deltaTime;
            if (moveDistance > distanceToWaypoint)
            {
                moveDistance = distanceToWaypoint;
            }

            var normalizedDir = Vector3.Normalize(direction);
            var newPosition = currentPos + normalizedDir * moveDistance;

            // Validate position on navmesh
            if (_navMesh != null)
            {
                if (!_navMesh.IsPointOnMesh(newPosition))
                {
                    // Try to project onto mesh
                    var projectedPoint = _navMesh.GetNearestPoint(newPosition);
                    if (projectedPoint.HasValue)
                    {
                        newPosition = projectedPoint.Value;
                    }
                    else
                    {
                        if (OnMovementBlocked != null)
                        {
                            OnMovementBlocked(newPosition);
                        }
                        Stop();
                        return;
                    }
                }
            }

            // Update position
            if (transform != null)
            {
                transform.Position = newPosition;
            }

            // Check trigger intersections
            CheckTriggerIntersections(currentPos, newPosition);
        }

        private void UpdateTurning(float deltaTime)
        {
            var transform = _entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform == null)
            {
                State = MovementState.Idle;
                return;
            }

            float currentFacing = transform.Facing;
            float facingDelta = NormalizeAngle(_targetFacing - currentFacing);

            if (Math.Abs(facingDelta) < 0.01f)
            {
                transform.Facing = _targetFacing;
                State = MovementState.Idle;
                return;
            }

            float maxRotation = TurnSpeedRadians * deltaTime;
            if (Math.Abs(facingDelta) > maxRotation)
            {
                facingDelta = Math.Sign(facingDelta) * maxRotation;
            }

            transform.Facing = NormalizeAngle(currentFacing + facingDelta);
        }

        private void ReachDestination()
        {
            Stop();

            if (OnDestinationReached != null)
            {
                OnDestinationReached();
            }
        }

        #endregion

        #region Trigger Detection

        private readonly HashSet<uint> _activeTriggers = new HashSet<uint>();

        private void CheckTriggerIntersections(Vector3 oldPos, Vector3 newPos)
        {
            var area = _world.CurrentArea;
            if (area == null)
            {
                return;
            }

            var currentTriggers = new HashSet<uint>();

            foreach (var trigger in area.Triggers)
            {
                var triggerComp = trigger.GetComponent<Interfaces.Components.ITriggerComponent>();
                if (triggerComp == null)
                {
                    continue;
                }

                // Check if entity is inside trigger bounds
                if (IsInsideTrigger(newPos, triggerComp))
                {
                    currentTriggers.Add(trigger.ObjectId);

                    // Fire enter event if newly entered
                    if (!_activeTriggers.Contains(trigger.ObjectId))
                    {
                        if (OnTriggerEnter != null)
                        {
                            OnTriggerEnter(trigger);
                        }
                    }
                }
            }

            // Check for trigger exits
            foreach (var triggerId in _activeTriggers)
            {
                if (!currentTriggers.Contains(triggerId))
                {
                    // Find the trigger entity and fire exit event
                    foreach (var trigger in area.Triggers)
                    {
                        if (trigger.ObjectId == triggerId)
                        {
                            if (OnTriggerExit != null)
                            {
                                OnTriggerExit(trigger);
                            }
                            break;
                        }
                    }
                }
            }

            _activeTriggers.Clear();
            foreach (var id in currentTriggers)
            {
                _activeTriggers.Add(id);
            }
        }

        private bool IsInsideTrigger(Vector3 position, Interfaces.Components.ITriggerComponent trigger)
        {
            // Use the trigger's built-in containment test if available
            if (trigger.ContainsPoint(position))
            {
                return true;
            }

            // Fallback to simple point-in-polygon test for trigger geometry
            var geometry = trigger.Geometry;
            if (geometry == null || geometry.Count < 3)
            {
                return false;
            }

            // 2D point-in-polygon (ignoring Z)
            int crossings = 0;
            for (int i = 0; i < geometry.Count; i++)
            {
                var p1 = geometry[i];
                var p2 = geometry[(i + 1) % geometry.Count];

                if ((p1.Y <= position.Y && p2.Y > position.Y) ||
                    (p2.Y <= position.Y && p1.Y > position.Y))
                {
                    float xIntersect = p1.X + (position.Y - p1.Y) / (p2.Y - p1.Y) * (p2.X - p1.X);
                    if (position.X < xIntersect)
                    {
                        crossings++;
                    }
                }
            }

            return (crossings % 2) == 1;
        }

        #endregion

        #region Helpers

        private Vector3 GetCurrentPosition()
        {
            var transform = _entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                return transform.Position;
            }
            return Vector3.Zero;
        }

        private float NormalizeAngle(float angle)
        {
            const float TwoPi = (float)(Math.PI * 2);
            while (angle > Math.PI)
            {
                angle -= TwoPi;
            }
            while (angle < -Math.PI)
            {
                angle += TwoPi;
            }
            return angle;
        }

        #endregion

        #region Path Visualization

        /// <summary>
        /// Gets the current path for visualization.
        /// </summary>
        public IReadOnlyList<Vector3> GetCurrentPath()
        {
            return _currentPath.AsReadOnly();
        }

        /// <summary>
        /// Gets the current path index.
        /// </summary>
        public int GetCurrentPathIndex()
        {
            return _currentPathIndex;
        }

        #endregion
    }

    /// <summary>
    /// Character movement state.
    /// </summary>
    public enum MovementState
    {
        /// <summary>
        /// Not moving.
        /// </summary>
        Idle,

        /// <summary>
        /// Walking towards destination.
        /// </summary>
        Walking,

        /// <summary>
        /// Running towards destination.
        /// </summary>
        Running,

        /// <summary>
        /// Turning in place.
        /// </summary>
        Turning
    }
}
