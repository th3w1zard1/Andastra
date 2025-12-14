using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to follow another entity.
    /// </summary>
    public class ActionFollowObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly float _followDistance;
        private const float ArrivalThreshold = 0.5f;
        private const float UpdatePathThreshold = 2.0f;
        private float _lastPathUpdate;
        private Vector3 _lastTargetPosition;

        public ActionFollowObject(uint targetObjectId, float followDistance = 2.0f)
            : base(ActionType.FollowLeader)
        {
            _targetObjectId = targetObjectId;
            _followDistance = followDistance;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            var transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            var target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Failed;
            }

            var targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = targetTransform.Position - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            // If we're close enough, just wait
            if (distance <= _followDistance)
            {
                // Face target
                if (distance > 0.1f)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    transform.Facing = (float)Math.Atan2(direction.X, direction.Z);
                }
                return ActionStatus.InProgress;
            }

            // Move towards target
            var stats = actor.GetComponent<IStatsComponent>();
            bool run = distance > _followDistance * 2;
            float speed = stats != null
                ? (run ? stats.RunSpeed : stats.WalkSpeed)
                : (run ? 5.0f : 2.5f);

            Vector3 direction2 = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;
            float targetDistance = distance - _followDistance;

            if (moveDistance > targetDistance)
            {
                moveDistance = targetDistance;
            }

            transform.Position += direction2 * moveDistance;
            transform.Facing = (float)Math.Atan2(direction2.X, direction2.Z);

            return ActionStatus.InProgress;
        }
    }
}

