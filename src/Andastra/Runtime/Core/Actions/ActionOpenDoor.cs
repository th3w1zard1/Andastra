using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to open a door.
    /// </summary>
    /// <remarks>
    /// Open Door Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door interaction system
    /// - Door loading: 0x00580ed0 @ 0x00580ed0 (load door from UTD GFF template)
    /// - Door loading with transitions: 0x005838d0 @ 0x005838d0 (load door with LinkedToModule/LinkedToFlags)
    /// - Located via string references: "OnOpen" @ 0x007be1b0 (door script event), "ScriptOnOpen" @ 0x007beeb8
    /// - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles object events
    ///   - EVENT_OPEN_OBJECT (case 7): Fires OnOpen script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN = 0x16)
    ///   - EVENT_CLOSE_OBJECT (case 6): Fires OnClose script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE = 0x17)
    ///   - EVENT_LOCK_OBJECT (case 0xd): Fires OnLocked script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED = 0x1c)
    ///   - EVENT_UNLOCK_OBJECT (case 0xc): Fires OnUnlocked script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED = 0x1d)
    /// - Door fields from UTD template (0x00580ed0): OpenState (0=closed, 1=open, 2=destroyed, 3=locked),
    ///   LinkedTo (waypoint/trigger tag), LinkedToModule (module ResRef), LinkedToFlags (bit 1=module transition, bit 2=area transition),
    ///   TransitionDestination (waypoint tag for positioning), Locked, Lockable, KeyRequired, KeyName
    /// - Original implementation: Moves actor to door within InteractRange (~2.0 units), checks lock state
    /// - If locked: Fires OnLocked event (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED) and fails
    /// - If unlocked: Sets IsOpen=true, plays open animation ("i_opendoor" @ 0x007c86d4), fires OnOpen script event
    /// - Doors with LinkedToModule and LinkedToFlags bit 1 set trigger module transitions
    /// - Doors with LinkedToFlags bit 2 set trigger area transitions (linked to waypoint/trigger via LinkedTo field)
    /// - Use distance: ~2.0 units (InteractRange) based on door interaction radius
    /// </remarks>
    public class ActionOpenDoor : ActionBase
    {
        private readonly uint _doorObjectId;
        private bool _approached;
        private const float InteractRange = 2.0f;

        public ActionOpenDoor(uint doorObjectId)
            : base(ActionType.OpenDoor)
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
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Open the door
            IDoorComponent doorState = door.GetComponent<IDoorComponent>();
            if (doorState != null)
            {
                if (doorState.IsLocked)
                {
                    // Fire OnLock script event
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_LOCK_OBJECT fires OnLock script event
                    // Located via string references: "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd), "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
                    IEventBus eventBus = actor.World.EventBus;
                    if (eventBus != null)
                    {
                        eventBus.FireScriptEvent(door, ScriptEvent.OnLock, actor);
                        eventBus.Publish(new DoorLockedEvent { Actor = actor, Door = door });
                    }
                    return ActionStatus.Failed;
                }

                doorState.IsOpen = true;
                doorState.OpenState = 1; // Open state

                // Record entering/clicking object for GetEnteringObject() and GetClickingObject() functions
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetEnteringObject/GetClickingObject track last entity that interacted with door
                // Located via string references: "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "OnClick" @ 0x007c1a20
                // Original implementation: Stores last interacting entity ID for script queries
                if (door is Entities.Entity doorEntityImpl)
                {
                    doorEntityImpl.SetData("LastEnteringObjectId", actor.ObjectId);
                    doorEntityImpl.SetData("LastClickingObjectId", actor.ObjectId);
                }

                // Fire OnOpen script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT fires OnOpen script event
                // Located via string references: "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7), "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16)
                IEventBus eventBus2 = actor.World.EventBus;
                if (eventBus2 != null)
                {
                    eventBus2.FireScriptEvent(door, ScriptEvent.OnOpen, actor);
                    eventBus2.Publish(new DoorOpenedEvent { Actor = actor, Door = door });
                }

                // Check for module/area transition
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door transition system
                // Door loading with transitions: 0x005838d0 @ 0x005838d0 (reads LinkedToModule, LinkedToFlags, TransitionDestination from UTD)
                // Located via string references: "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788, "TransitionDestination" @ 0x007bd7a4
                // Original implementation: Doors with LinkedToModule trigger module transitions when opened
                // Module transitions: LinkedToFlags bit 1 = module transition (if LinkedToModule non-empty)
                // Area transitions: LinkedToFlags bit 2 = area transition within module (if LinkedTo waypoint/trigger non-empty)
                // TransitionDestination: Waypoint tag where actor should spawn after transition
                // Use DoorComponent properties which compute from LinkedToFlags (IsModuleTransition, IsAreaTransition)
                bool isModuleTransition = doorState.IsModuleTransition;
                bool isAreaTransition = doorState.IsAreaTransition;
                if (isModuleTransition || isAreaTransition)
                {
                    // Publish transition event - GameSession will handle the actual transition
                    IEventBus eventBus3 = actor.World.EventBus;
                    if (eventBus3 != null)
                    {
                        eventBus3.Publish(new DoorTransitionEvent
                        {
                            Actor = actor,
                            Door = door,
                            TargetModule = doorState.LinkedToModule,
                            TargetWaypoint = doorState.LinkedTo,
                            IsModuleTransition = isModuleTransition,
                            IsAreaTransition = isAreaTransition
                        });
                    }
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when a door is opened.
    /// </summary>
    public class DoorOpenedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }

    /// <summary>
    /// Event fired when trying to open a locked door.
    /// </summary>
    public class DoorLockedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public IEntity Entity { get { return Door; } }
    }

    /// <summary>
    /// Event fired when a door with a transition is opened.
    /// </summary>
    /// <remarks>
    /// Door Transition Event:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door transition system
    /// - Located via string references: "LinkedToModule" @ 0x007bd7bc, "TransitionDestination" @ 0x007bd7a4
    /// - Original implementation: Doors with LinkedToModule trigger module/area transitions
    /// - GameSession listens for this event and initiates the transition
    /// </remarks>
    public class DoorTransitionEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Door { get; set; }
        public string TargetModule { get; set; }
        public string TargetWaypoint { get; set; }
        public bool IsModuleTransition { get; set; }
        public bool IsAreaTransition { get; set; }
        public IEntity Entity { get { return Door; } }
    }
}

