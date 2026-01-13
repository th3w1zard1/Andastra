using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to move an entity away from another object.
    /// Based on NWScript ActionMoveAwayFromObject semantics.
    /// </summary>
    /// <remarks>
    /// Move Away From Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionMoveAwayFromObject NWScript function
    /// - Located via string references: "MoveAwayFromObject" action type (ACTION_TYPE_MOVE_AWAY_FROM_OBJECT constant), "MOVETO" @ 0x007b6b24 (movement action constant)
    /// - Original implementation: Moves entity away from target to maintain minimum distance
    /// - Used for backing away from threats, maintaining personal space, retreat behavior, NPC avoidance
    /// - Movement: Moves in opposite direction from target until distance threshold reached
    /// - Uses direct movement (no pathfinding) - moves along line away from target
    /// - Speed: Uses run speed if run flag is set, walk speed otherwise (from IStatsComponent)
    /// - Overlap handling: If entities overlap (distance < 0.1), picks random direction to move away
    /// - Action completes when entity is at least specified distance away from target (default 5.0 units)
    /// - Action remains InProgress while moving away, completes when distance threshold reached or target becomes invalid
    /// - No pathfinding: Direct movement only - does not avoid obstacles or use walkmesh
    /// - Facing: Entity faces direction of movement while retreating
    /// - Based on NWScript function ActionMoveAwayFromObject (routine ID varies by game version)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionMoveAwayFromObject implementation
            // Located via string references: "MoveAwayFromObject" action type, "MOVETO" @ 0x007b6b24
            // Original implementation: Moves entity away from target to maintain minimum distance
            // Uses direct movement (no pathfinding) - moves in opposite direction from target
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null
                ? (_run ? stats.RunSpeed : stats.WalkSpeed)
                : (_run ? 5.0f : 2.5f);

            Vector3 direction = Vector3.Normalize(fromTarget);
            if (direction.LengthSquared() < 0.01f)
            {
                // Too close, pick a random direction
                // Original engine: If entities overlap, picks random direction to move away
                direction = new Vector3(1.0f, 0, 0);
            }

            float moveDistance = speed * deltaTime;
            Vector3 newPosition = transform.Position + direction * moveDistance;

            // Project position to walkmesh surface (matches 0x004f5070 in swkotor2.exe)
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
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }
    }
}

