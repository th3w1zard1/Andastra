using System.Numerics;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Core.Actions
{
    /// <summary>
    /// Action to instantly teleport to a location.
    /// </summary>
    /// <remarks>
    /// Jump To Location Action:
    /// - Based on swkotor2.exe ActionJumpToLocation NWScript function
    /// - Located via string references: "JumpToLocation" action type (ACTION_TYPE_JUMP_TO_LOCATION constant), "Position" @ 0x007bef70 (position field)
    /// - Original implementation: Instantly teleports entity to specified location and facing without movement animation
    /// - Used for scripted movement (cutscenes, scripted sequences), area transitions, teleportation effects
    /// - Teleportation: Position and facing set immediately - bypasses pathfinding, walkmesh validation, and movement interpolation
    /// - Area transitions: Can jump between areas if location specifies different area (requires area transition system handling)
    /// - No validation: Jump does not check if destination is valid (can teleport outside walkmesh, into walls, etc.)
    /// - Animation: No movement animation plays (entity appears at new location instantly)
    /// - Action completes immediately after position is set (single frame execution)
    /// - Usage: Cutscenes, scripted sequences, area loading positioning, teleportation spells/effects
    /// - Contrast with ActionMoveToLocation: MoveToLocation uses pathfinding and movement animation, JumpToLocation is instant teleport
    /// - Based on NWScript function ActionJumpToLocation (routine ID varies by game version)
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

