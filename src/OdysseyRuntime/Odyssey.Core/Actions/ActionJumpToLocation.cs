using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to instantly teleport to a location.
    /// </summary>
    /// <remarks>
    /// Jump To Location Action:
    /// - Based on swkotor2.exe ActionJumpToLocation NWScript function
    /// - Located via string references: "JumpToLocation" action type, "Position" @ 0x007bef70
    /// - Original implementation: Instantly teleports entity to specified location and facing
    /// - Used for scripted movement, cutscenes, area transitions
    /// - Can jump between areas if location specifies different area (requires area transition handling)
    /// - Position and facing set immediately without animation or movement path
    /// - Action completes immediately after position is set
    /// </remarks>
    public class ActionJumpToLocation : ActionBase
    {
        private readonly Vector3 _location;
        private readonly float _facing;

        public ActionJumpToLocation(Vector3 location, float facing = 0f)
            : base(ActionType.JumpToLocation)
        {
            _location = location;
            _facing = facing;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            // Based on swkotor2.exe: ActionJumpToLocation implementation
            // Located via string references: "JumpToLocation" action type, "Position" @ 0x007bef70
            // Original implementation: Instantly teleports entity to location without movement animation
            // Used for scripted movement, cutscenes, area transitions
            // Position and facing set immediately - no pathfinding or movement interpolation
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            transform.Position = _location;
            transform.Facing = _facing;

            return ActionStatus.Complete;
        }
    }
}

