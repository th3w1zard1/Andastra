using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to follow another entity.
    /// </summary>
    /// <remarks>
    /// Follow Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) follow system
    /// - Located via string references: "FollowObject" @ 0x007bedb8 (follow object field), "FollowLocation" @ 0x007beda8 (follow location field)
    /// - "FollowInfo" @ 0x007beec0 (follow info structure), "LastFollowerPos" @ 0x007bed88 (last follower position tracking)
    /// - "PT_FOLLOWSTATE" @ 0x007c1758 (party follow state in PARTYTABLE GFF structure)
    /// - GUI references: "gui_mp_followd" @ 0x007b5da4 (follow down GUI), "gui_mp_followu" @ 0x007b5db4 (follow up GUI)
    /// - Party follow: " + %d (Inspire Followers Bonus)" @ 0x007c39f4 (inspire followers effect bonus)
    /// - Error messages:
    ///   - "PathFollowData requesting bad data position %d" @ 0x007ca414 (path follow data error)
    ///   - "CSWTrackFollower: Trying to attach gun to a non-existent bank ID: %d" @ 0x007cb4b8 (weapon attachment error)
    ///   - "CSWTrackFollower: Bank ID %d < 0." @ 0x007cb500 (invalid bank ID error)
    ///   - "CSWTrackFollower: Could not find bank ID in attach part name %s." @ 0x007cb528 (bank ID lookup error)
    /// - "OnHitFollower" @ 0x007cb364 (follower hit event)
    /// - Original implementation: Moves entity to maintain follow distance from target
    /// - Used for party member following (party members follow leader), NPC following behavior, companion following
    /// - Follow distance: Default 2.0 units, maintains distance while target moves (does not close to melee range)
    /// - Movement behavior: Entity faces target while following, uses run speed if target moves far away (2x follow distance threshold)
    /// - Walk speed used when close to follow distance, run speed used when far from follow distance
    /// - Pathfinding: Could use pathfinding (ActionMoveToLocation) if target moves far away, but direct movement is simpler
    /// - Action remains InProgress while following (does not complete until action is cancelled or target becomes invalid)
    /// - Based on NWScript function ActionFollow (routine ID varies by game version)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Follow system implementation
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

            Vector3 newPosition = transform.Position + direction2 * moveDistance;

            // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
            IArea area = actor.World.CurrentArea;
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
            transform.Facing = (float)Math.Atan2(direction2.Y, direction2.X);

            return ActionStatus.InProgress;
        }
    }
}

