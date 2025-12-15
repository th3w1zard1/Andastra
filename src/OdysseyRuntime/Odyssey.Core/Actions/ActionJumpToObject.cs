using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to instantly teleport to an object's location.
    /// </summary>
    /// <remarks>
    /// Jump To Object Action:
    /// - Based on swkotor2.exe ActionJumpToObject NWScript function
    /// - Located via string references: "JumpToObject" action type, "Position" @ 0x007bef70
    /// - Original implementation: Instantly teleports entity to target object's position and facing
    /// - Used for scripted movement, cutscenes, following behavior
    /// - Position and facing set immediately without animation or movement path
    /// - Action completes immediately if target is valid, fails if target is invalid
    /// - Based on NWScript ActionJumpToObject semantics
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
            // Based on swkotor2.exe: ActionJumpToObject implementation
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

