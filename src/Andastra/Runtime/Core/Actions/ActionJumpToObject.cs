using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to instantly teleport to an object's location.
    /// </summary>
    /// <remarks>
    /// Jump To Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionJumpToObject NWScript function
    /// - Located via string references: "JumpToObject" action type (ACTION_TYPE_JUMP_TO_OBJECT constant), "Position" @ 0x007bef70 (position field)
    /// - Original implementation: Instantly teleports entity to target object's position and facing without movement animation
    /// - Used for scripted movement (cutscenes, scripted sequences), following behavior, teleportation effects
    /// - Position and facing copied directly from target - bypasses pathfinding, walkmesh validation, and movement interpolation
    /// - Target validation: Checks if target entity exists and is valid (IsValid flag), action fails if target is null/invalid
    /// - No validation: Jump does not check if destination is valid (can teleport outside walkmesh, into walls, etc.)
    /// - Animation: No movement animation plays (entity appears at target's location instantly)
    /// - Action completes immediately after position is set (single frame execution if target is valid)
    /// - Usage: Cutscenes, scripted sequences, teleportation spells/effects, following behavior initialization
    /// - Contrast with ActionMoveToObject: MoveToObject uses pathfinding and movement animation, JumpToObject is instant teleport
    /// - Based on NWScript function ActionJumpToObject (routine ID varies by game version)
    /// </remarks>
    public class ActionJumpToObject : ActionBase
    {
        private readonly uint _targetObjectId;

        public ActionJumpToObject(uint targetObjectId)
            : base(ActionType.JumpToObject)
        {
            _targetObjectId = targetObjectId;
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

            // Jump to target's position and facing
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionJumpToObject implementation
            // Located via string references: "JumpToObject" action type, "Position" @ 0x007bef70
            // Original implementation: Instantly teleports entity to target object's position and facing
            // Used for scripted movement, cutscenes, following behavior
            // Position and facing copied directly from target - no interpolation
            transform.Position = targetTransform.Position;
            transform.Facing = targetTransform.Facing;

            return ActionStatus.Complete;
        }
    }
}

