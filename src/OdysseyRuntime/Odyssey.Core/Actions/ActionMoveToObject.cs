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
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - Located via string reference: "ActionList" @ 0x007bebdc, "MOVETO" @ 0x007b6b24
    /// - Moves actor towards target object within specified range
    /// - Uses direct movement (no pathfinding) - follows target if it moves
    /// - Faces target when within range (Y-up: Atan2(Y, X) for 2D plane facing)
    /// - Walk/run speed determined by entity stats (WalkSpeed/RunSpeed from appearance.2da)
    /// - Action parameters stored as ActionId, GroupActionId, NumParams, Paramaters (Type/Value pairs)
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
            // Based on swkotor2.exe: ActionMoveToObject implementation
            // Located via string references: "MOVETO" @ 0x007b6b24, "ActionList" @ 0x007bebdc
            // Walking collision checking: FUN_0054be70 @ 0x0054be70
            // Located via string references:
            //   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            //   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
            //   - "aborted walking, Maximum number of bumps happened" @ 0x007c0458
            // Original implementation (from decompiled FUN_0054be70):
            // 1. Direct movement: Moves directly to target (no pathfinding), follows if target moves
            // 2. Creature collision: Checks for collisions with other creatures along movement path
            // 3. Bump counter: Tracks number of creature bumps (stored at offset 0x268 in entity structure)
            // 4. Maximum bumps: If bump count exceeds 5, aborts movement and clears path
            // 5. Total blocking: If same creature blocks repeatedly, aborts movement
            // 6. Position projection: Projects position to walkmesh surface using FUN_004f5070
            // 7. Distance calculation: Uses 2D distance (X/Z plane, ignores Y) for movement calculations
            // Action completes when within specified range of target object
            if (distance <= _range)
            {
                // Face target
                if (distance > ArrivalThreshold)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    // Y-up system: Atan2(Y, X) for 2D plane facing
                    transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
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
            // Y-up system: Atan2(Y, X) for 2D plane facing
            transform.Facing = (float)Math.Atan2(direction2.Y, direction2.X);

            return ActionStatus.InProgress;
        }
    }
}

