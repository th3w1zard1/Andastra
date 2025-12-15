using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to open a door.
    /// </summary>
    /// <remarks>
    /// Open Door Action:
    /// - Based on swkotor2.exe door interaction system
    /// - Located via string references: "OnOpen" @ 0x007be1b0 (door script event), "ScriptOnOpen" @ 0x007beeb8
    /// - Original implementation: Moves actor to door, checks lock state, opens door if unlocked
    /// - Fires OnOpen script event when door opens (ScriptOnOpen field in UTD template)
    /// - Use distance: ~2.0 units (InteractRange)
    /// - Script events: OnOpen (door opened), OnLocked (door locked)
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
                    // Fire locked door event
                    IEventBus eventBus = actor.World.EventBus;
                    if (eventBus != null)
                    {
                        eventBus.Publish(new DoorLockedEvent { Actor = actor, Door = door });
                    }
                    return ActionStatus.Failed;
                }

                doorState.IsOpen = true;

                // Fire opened event
                IEventBus eventBus2 = actor.World.EventBus;
                if (eventBus2 != null)
                {
                    eventBus2.Publish(new DoorOpenedEvent { Actor = actor, Door = door });
                }

                // Check for module/area transition
                // Based on swkotor2.exe door transition system
                // Located via string references: "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
                // Original implementation: Doors with LinkedToModule trigger module transitions when opened
                // Module transitions: LinkedToFlags bit 1 = module transition
                // Area transitions: LinkedToFlags bit 2 = area transition within module
                if (doorState.IsModuleTransition || doorState.IsAreaTransition)
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
                            IsModuleTransition = doorState.IsModuleTransition,
                            IsAreaTransition = doorState.IsAreaTransition
                        });
                    }
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Action to close a door.
    /// </summary>
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

            IDoorComponent doorState = door.GetComponent<IDoorComponent>();
            if (doorState != null)
            {
                doorState.IsOpen = false;

                IEventBus eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.Publish(new DoorClosedEvent { Actor = actor, Door = door });
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
    /// Event fired when a door is closed.
    /// </summary>
    public class DoorClosedEvent : IGameEvent
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
    /// - Based on swkotor2.exe door transition system
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

