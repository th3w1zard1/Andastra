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
    /// <remarks>
    /// Follow Object Action:
    /// - Based on swkotor2.exe follow system
    /// - Located via string references: "FollowObject" @ 0x007bedb8, "FollowLocation" @ 0x007beda8
    /// - "FollowInfo" @ 0x007beec0, "LastFollowerPos" @ 0x007bed88
    /// - "PT_FOLLOWSTATE" @ 0x007c1758 (party follow state in PARTYTABLE)
    /// - GUI references: "gui_mp_followd" @ 0x007b5da4, "gui_mp_followu" @ 0x007b5db4
    /// - Error messages:
    ///   - "PathFollowData requesting bad data position %d" @ 0x007ca414
    ///   - "CSWTrackFollower: Trying to attach gun to a non-existent bank ID: %d" @ 0x007cb4b8
    ///   - "CSWTrackFollower: Bank ID %d < 0." @ 0x007cb500
    ///   - "CSWTrackFollower: Could not find bank ID in attach part name %s." @ 0x007cb528
    /// - Original implementation: Moves entity to maintain follow distance from target
    /// - Used for party member following, NPC following behavior
    /// - Follow distance: Default 2.0 units, maintains distance while target moves
    /// - Entity faces target while following, runs if target moves far away
    /// - "OnHitFollower" @ 0x007cb364 (follower hit event)
    /// </remarks>
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

            // If we're close enough, just wait
            // Based on swkotor2.exe: Follow system implementation
            // Located via string references: "FollowObject" @ 0x007bedb8, "FollowInfo" @ 0x007beec0
            // Original implementation: Maintains follow distance, faces target while waiting
            // Follow distance: Default 2.0 units, entity waits if within range
            if (distance <= _followDistance)
            {
                // Face target
                if (distance > 0.1f)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    // Y-up system: Atan2(Y, X) for 2D plane facing
                    transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
                }
                return ActionStatus.InProgress;
            }

            // Move towards target
            // Original engine: Uses run speed if target is far away (2x follow distance)
            // Walk speed used when close to follow distance
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
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
            // Y-up system: Atan2(Y, X) for 2D plane facing
            transform.Facing = (float)Math.Atan2(direction2.Y, direction2.X);

            return ActionStatus.InProgress;
        }
    }
}

