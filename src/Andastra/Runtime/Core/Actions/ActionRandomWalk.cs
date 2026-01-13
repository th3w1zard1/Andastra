using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to make an entity randomly walk around within a bounded area.
    /// Based on NWScript ActionRandomWalk semantics.
    /// </summary>
    /// <remarks>
    /// Random Walk Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionRandomWalk NWScript function
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionType" @ 0x007bf7f8
    /// - Action loading: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - Action saving: FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF)
    /// - Action creation: FUN_00507fd0 @ 0x00507fd0 (create action from parameters)
    /// - Action parameter setting: FUN_00504130 @ 0x00504130 (set action parameter by index/type)
    /// - Walking collision: UpdateCreatureMovement @ 0x0054be70 (walk collision check with "aborted walking" @ 0x007c03c0)
    /// - Walkmesh checking: FUN_005775d0 @ 0x005775d0 (load walkmesh properties, "WalkCheck" @ 0x007c1514)
    /// - Original implementation: Entity randomly walks within max distance from start position using pathfinding
    /// - Used for idle NPC behavior, wandering creatures, ambient activity
    /// - Picks random direction and distance, uses pathfinding to reach target, then picks new target
    /// - Duration parameter limits how long random walking continues (0 = unlimited duration)
    /// - Max distance parameter limits how far entity can wander from start position
    /// - Random walk should use pathfinding via walkmesh (similar to ActionMoveToLocation)
    /// </remarks>
    public class ActionRandomWalk : ActionBase
    {
        private float _maxDistance;
        private float _duration;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private bool _hasTarget;
        private Random _random;
        private IList<Vector3> _path;
        private int _pathIndex;
        private const float ArrivalThreshold = 0.5f;
        private float _waitTime;
        private float _waitDuration;

        public ActionRandomWalk(float maxDistance = 10.0f, float duration = 0f)
            : base(ActionType.RandomWalk)
        {
            _maxDistance = maxDistance;
            _duration = duration;
            _random = new Random();
            _waitDuration = (float)(_random.NextDouble() * 2.0 + 1.0); // Wait 1-3 seconds at destination
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Initialize start position on first update
            if (_startPosition == Vector3.Zero)
            {
                _startPosition = transform.Position;
            }

            // Check duration limit
            if (_duration > 0 && ElapsedTime >= _duration)
            {
                return ActionStatus.Complete;
            }

            // If waiting at destination, check if wait is complete
            if (_waitTime > 0)
            {
                _waitTime -= deltaTime;
                if (_waitTime <= 0)
                {
                    _waitTime = 0;
                    _hasTarget = false;
                    _path = null;
                    _pathIndex = 0;
                }
                return ActionStatus.InProgress;
            }

            // If we don't have a target or reached it, pick a new one
            if (!_hasTarget || (_path != null && _pathIndex >= _path.Count))
            {
                if (PickNewTarget(actor, transform.Position))
                {
                    _hasTarget = true;
                }
                else
                {
                    // Failed to find valid target, wait a bit before trying again
                    _waitTime = 1.0f;
                    return ActionStatus.InProgress;
                }
            }

            // If we have a path, follow it (like ActionMoveToLocation)
            if (_path != null && _pathIndex < _path.Count)
            {
                return FollowPath(actor, transform, deltaTime);
            }

            // Fallback: direct movement if no pathfinding available
            return DirectMovement(actor, transform, deltaTime);
        }

        private ActionStatus FollowPath(IEntity actor, ITransformComponent transform, float deltaTime)
        {
            Vector3 target = _path[_pathIndex];
            Vector3 toTarget = target - transform.Position;
            toTarget.Y = 0; // Ignore vertical for direction
            float distance = toTarget.Length();

            if (distance < ArrivalThreshold)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    // Reached destination, wait before picking new target
                    _waitTime = _waitDuration;
                    _waitDuration = (float)(_random.NextDouble() * 2.0 + 1.0); // New wait time for next destination
                    return ActionStatus.InProgress;
                }
                return ActionStatus.InProgress;
            }

            // Calculate movement
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null ? stats.WalkSpeed : 2.5f;

            Vector3 direction = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;

            if (moveDistance > distance)
            {
                moveDistance = distance;
            }

            Vector3 newPosition = transform.Position + direction * moveDistance;

            // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
            IArea area = actor.World?.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                Vector3 projectedPos;
                float height;
                if (area.NavigationMesh.ProjectToSurface(newPosition, out projectedPos, out height))
                {
                    newPosition = projectedPos;
                }
            }

            transform.Position = newPosition;
            // Set facing to match movement direction (Y-up system: Atan2(Y, X) for 2D plane)
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }

        private ActionStatus DirectMovement(IEntity actor, ITransformComponent transform, float deltaTime)
        {
            Vector3 toTarget = _targetPosition - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            if (distance < ArrivalThreshold)
            {
                // Reached destination, wait before picking new target
                _waitTime = _waitDuration;
                _waitDuration = (float)(_random.NextDouble() * 2.0 + 1.0);
                _hasTarget = false;
                return ActionStatus.InProgress;
            }

            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null ? stats.WalkSpeed : 2.5f;

            Vector3 direction = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;

            if (moveDistance > distance)
            {
                moveDistance = distance;
            }

            Vector3 newPosition = transform.Position + direction * moveDistance;

            // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
            IArea area = actor.World?.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                Vector3 projectedPos;
                float height;
                if (area.NavigationMesh.ProjectToSurface(newPosition, out projectedPos, out height))
                {
                    newPosition = projectedPos;
                }
            }

            transform.Position = newPosition;
            // Y-up system: Atan2(Y, X) for 2D plane facing
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }

        private bool PickNewTarget(IEntity actor, Vector3 currentPosition)
        {
            // Pick a random direction and distance
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionRandomWalk implementation
            // Located via string references: "ActionList" @ 0x007bebdc
            // Original implementation: Picks random angle (0-2π) and random distance (0 to maxDistance)
            // Target position is relative to start position, not current position
            // This ensures entity doesn't wander too far from original spawn point
            // Walkmesh collision checking via UpdateCreatureMovement @ 0x0054be70 handles path validation
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float distance = (float)(_random.NextDouble() * _maxDistance);

            Vector3 offset = new Vector3(
                (float)Math.Sin(angle) * distance,
                0,
                (float)Math.Cos(angle) * distance
            );

            Vector3 candidateTarget = _startPosition + offset;

            // Try to find path using walkmesh (if available)
            IArea area = actor.World?.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                _path = area.NavigationMesh.FindPath(currentPosition, candidateTarget);
                if (_path != null && _path.Count > 0)
                {
                    _targetPosition = candidateTarget;
                    _pathIndex = 0;
                    return true;
                }
            }

            // Fallback: use direct target if no pathfinding
            _targetPosition = candidateTarget;
            _path = null;
            _pathIndex = 0;
            return true;
        }
    }
}

