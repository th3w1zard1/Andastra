using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to close a door.
    /// </summary>
    /// <remarks>
    /// Close Door Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door closing system
    /// - Located via string references: "OnClosed" @ 0x007be1c8 (door closed script), "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (close object event, case 6)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (close script event, 0x10), door loading: 0x005838d0 @ 0x005838d0 (load door properties)
    /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles EVENT_CLOSE_OBJECT event (case 6, fires before script execution)
    /// - Original implementation: Closes door, fires OnClosed script event
    /// - Movement: Moves actor towards door if not in interaction range (InteractRange ~2.0 units)
    /// - Door interaction: Checks if actor is within InteractRange before closing door
    /// - Door state: IsOpen flag set to false (via IDoorComponent.IsOpen property)
    /// - Animation: Door close animation plays (controlled by rendering system, AnimationState changes from 1 to 0)
    /// - Script events: OnClosed script executes after door is closed (ScriptOnClose field in UTD template)
    /// - Event firing: EVENT_CLOSE_OBJECT fires first, then OnClosed script executes on door entity
    /// - Lock state: Closing door does not change lock state (door can be closed while locked)
    /// - Transition doors: Closing transition doors (module/area transitions) does not trigger transitions
    /// - Action completes immediately after door is closed and event is fired (single frame execution if already in range)
    /// - Based on NWScript function ActionCloseDoor (routine ID varies by game version)
    /// </remarks>
    public class ActionCloseDoor : ActionBase
    {
        private readonly uint _doorObjectId;
        private bool _approached;
        private const float InteractRange = 2.0f;

        public ActionCloseDoor(uint doorObjectId)
            : base(ActionType.CloseDoor)
        {
            _doorObjectId = doorObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get door entity
            IEntity door = actor.World.GetEntity(_doorObjectId);
            if (door == null || !door.IsValid)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent doorTransform = door.GetComponent<ITransformComponent>();
            if (doorTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = doorTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards door if not in range
            if (distance > InteractRange && !_approached)
            {
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - InteractRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                transform.Position += direction * moveDistance;
                // Y-up system: Atan2(Y, X) for 2D plane facing
                transform.Facing = (float)System.Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Close the door
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door closing implementation
            // Located via string references: "OnClosed" @ 0x007be1c8, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
            // Original implementation: 0x004dcfb0 @ 0x004dcfb0 handles EVENT_CLOSE_OBJECT (case 6)
            // Door state: IsOpen flag set to false, fires OnClosed script event
            IDoorComponent doorState = door.GetComponent<IDoorComponent>();
            if (doorState != null)
            {
                doorState.IsOpen = false;
                doorState.OpenState = 0; // Closed state

                // Record entering/clicking object for GetEnteringObject() and GetClickingObject() functions
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetEnteringObject/GetClickingObject track last entity that interacted with door
                // Located via string references: "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "OnClick" @ 0x007c1a20
                // Original implementation: Stores last interacting entity ID for script queries
                if (door is Entities.Entity doorEntityImpl)
                {
                    doorEntityImpl.SetData("LastEnteringObjectId", actor.ObjectId);
                    doorEntityImpl.SetData("LastClickingObjectId", actor.ObjectId);
                }

                // Fire OnClose script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_CLOSE_OBJECT fires OnClose script event
                // Located via string references: "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6), "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17)
                IEventBus eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(door, ScriptEvent.OnClose, actor);
                    eventBus.Publish(new DoorClosedEvent { Actor = actor, Door = door });
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when a door is closed.
    /// </summary>
    public class DoorClosedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }
}

