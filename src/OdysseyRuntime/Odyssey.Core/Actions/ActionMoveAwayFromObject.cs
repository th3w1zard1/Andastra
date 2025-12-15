using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to move an entity away from another object.
    /// Based on NWScript ActionMoveAwayFromObject semantics.
    /// </summary>
    /// <remarks>
    /// Move Away From Object Action:
    /// - Based on swkotor2.exe ActionMoveAwayFromObject NWScript function
    /// - Original implementation: Moves entity away from target to maintain minimum distance
    /// - Used for backing away from threats, maintaining personal space, retreat behavior
    /// - Moves in opposite direction from target until distance threshold reached
    /// </remarks>
    public class ActionMoveAwayFromObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly bool _run;
        private readonly float _distance;
        private const float ArrivalThreshold = 0.1f;

        public ActionMoveAwayFromObject(uint targetObjectId, bool run = false, float distance = 5.0f)
            : base(ActionType.MoveToPoint)
        {
            _targetObjectId = targetObjectId;
            _run = run;
            _distance = distance;
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

            Vector3 fromTarget = transform.Position - targetTransform.Position;
            fromTarget.Y = 0; // Ignore vertical
            float currentDistance = fromTarget.Length();

            // If we're far enough away, we're done
            if (currentDistance >= _distance)
            {
                return ActionStatus.Complete;
            }

            // Move away from target
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null
                ? (_run ? stats.RunSpeed : stats.WalkSpeed)
                : (_run ? 5.0f : 2.5f);

            Vector3 direction = Vector3.Normalize(fromTarget);
            if (direction.LengthSquared() < 0.01f)
            {
                // Too close, pick a random direction
                direction = new Vector3(1.0f, 0, 0);
            }

            float moveDistance = speed * deltaTime;
            transform.Position += direction * moveDistance;
            transform.Facing = (float)Math.Atan2(direction.X, direction.Z);

            return ActionStatus.InProgress;
        }
    }
}

