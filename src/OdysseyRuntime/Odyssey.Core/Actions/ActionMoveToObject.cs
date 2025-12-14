using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to move an entity to another object.
    /// Based on NWScript ActionMoveToObject semantics.
    /// </summary>
    /// <remarks>
    /// Move To Object Action:
    /// - Based on swkotor2.exe movement action system
    /// - Original implementation: Moves actor towards target object within specified range
    /// - Uses direct movement (no pathfinding) - follows target if it moves
    /// - Faces target when within range
    /// - Walk/run speed determined by entity stats
    /// </remarks>
    public class ActionMoveToObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly bool _run;
        private readonly float _range;
        private const float ArrivalThreshold = 0.1f;

        public ActionMoveToObject(uint targetObjectId, bool run = false, float range = 1.0f)
            : base(ActionType.MoveToObject)
        {
            _targetObjectId = targetObjectId;
            _run = run;
            _range = range;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            IEntity target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = targetTransform.Position - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            // If we're within range, we're done
            if (distance <= _range)
            {
                // Face target
                if (distance > ArrivalThreshold)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    transform.Facing = (float)Math.Atan2(direction.X, direction.Z);
                }
                return ActionStatus.Complete;
            }

            // Move towards target
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null
                ? (_run ? stats.RunSpeed : stats.WalkSpeed)
                : (_run ? 5.0f : 2.5f);

            Vector3 direction2 = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;
            float targetDistance = distance - _range;

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

