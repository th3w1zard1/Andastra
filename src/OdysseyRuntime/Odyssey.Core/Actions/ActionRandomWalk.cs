using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to make an entity randomly walk around.
    /// Based on NWScript ActionRandomWalk semantics.
    /// </summary>
    /// <remarks>
    /// Random Walk Action:
    /// - Based on swkotor2.exe ActionRandomWalk NWScript function
    /// - Located via string references: "ActionRandomWalk" NWScript function, "ActionList" @ 0x007bebdc
    /// - Original implementation: Entity randomly walks within max distance from start position
    /// - Used for idle NPC behavior, wandering creatures, ambient activity
    /// - Picks random direction and distance, walks to target, then picks new target
    /// - Duration parameter limits how long random walking continues (0 = unlimited duration)
    /// - Max distance parameter limits how far entity can wander from start position
    /// - Random walk uses ActionMoveToLocation internally to move to random points
    /// - Based on NWScript ActionRandomWalk semantics for NPC wandering behavior
    /// </remarks>
    public class ActionRandomWalk : ActionBase
    {
        private float _maxDistance;
        private float _duration;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private bool _hasTarget;
        private Random _random;

        public ActionRandomWalk(float maxDistance = 10.0f, float duration = 0f)
            : base(ActionType.RandomWalk)
        {
            _maxDistance = maxDistance;
            _duration = duration;
            _random = new Random();
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

            // If we don't have a target or reached it, pick a new one
            if (!_hasTarget || Vector3.Distance(transform.Position, _targetPosition) < 0.5f)
            {
                PickNewTarget(transform.Position);
            }

            // Move towards target
            Vector3 toTarget = _targetPosition - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            if (distance < 0.5f)
            {
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

            transform.Position += direction * moveDistance;
            // Y-up system: Atan2(Y, X) for 2D plane facing
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }

        private void PickNewTarget(Vector3 currentPosition)
        {
            // Pick a random direction and distance
            // Based on swkotor2.exe: ActionRandomWalk implementation
            // Located via string references: "ActionRandomWalk" NWScript function
            // Original implementation: Picks random angle (0-2π) and random distance (0 to maxDistance)
            // Target position is relative to start position, not current position
            // This ensures entity doesn't wander too far from original spawn point
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float distance = (float)(_random.NextDouble() * _maxDistance);

            Vector3 offset = new Vector3(
                (float)Math.Sin(angle) * distance,
                0,
                (float)Math.Cos(angle) * distance
            );

            _targetPosition = _startPosition + offset;
            _hasTarget = true;
        }
    }
}

