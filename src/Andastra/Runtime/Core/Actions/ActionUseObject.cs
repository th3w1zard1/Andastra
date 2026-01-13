using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to use/interact with a placeable object.
    /// </summary>
    /// <remarks>
    /// Use Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) placeable interaction system
    /// - Located via string references: "OnUsed" @ 0x007c1f70 (placeable script event)
    /// - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events
    ///   - EVENT_OPEN_OBJECT (case 7): Used for container opening and non-container placeable usage
    ///   - EVENT_CLOSE_OBJECT (case 6): Used for container closing
    ///   - EVENT_LOCK_OBJECT (case 0xd): Fires OnLocked script event (CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED = 0x1c)
    ///   - OnUsed script event: CSWSSCRIPTEVENT_EVENTTYPE_ON_USED = 0x19 (for non-container placeables)
    /// - Original implementation: Moves actor to placeable use point within InteractRange (~2.0 units)
    /// - Checks Useable flag from UTP template: If FALSE, action fails immediately
    /// - Checks Locked state: If locked, fires EVENT_LOCK_OBJECT, executes OnLocked script, action fails
    /// - Containers (HasInventory=true): Toggles IsOpen state, fires EVENT_OPEN_OBJECT/EVENT_CLOSE_OBJECT, executes OnOpen/OnClosed script
    /// - Non-containers: Fires EVENT_OPEN_OBJECT, executes OnUsed script (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED)
    /// - Use distance: ~2.0 units (InteractRange) based on placeable interaction radius
    /// - Based on NWScript ActionUseObject semantics
    /// </remarks>
    public class ActionUseObject : ActionBase
    {
        private readonly uint _placeableObjectId;
        private bool _approached;
        private const float InteractRange = 2.0f;

        public ActionUseObject(uint placeableObjectId)
            : base(ActionType.UseObject)
        {
            _placeableObjectId = placeableObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get placeable entity
            IEntity placeable = actor.World.GetEntity(_placeableObjectId);
            if (placeable == null || !placeable.IsValid)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent placeableTransform = placeable.GetComponent<ITransformComponent>();
            if (placeableTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = placeableTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards placeable if not in range
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

            // Use the placeable
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Placeable interaction implementation
            // Located via string references: "OnUsed" @ 0x007c1f70, "EVENT_OPEN_OBJECT" @ 0x007bcda0
            // Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events
            // Original implementation: Checks Useable flag (from UTP template), Locked state, HasInventory flag to determine behavior
            // Non-container placeables: Fires EVENT_OPEN_OBJECT, executes OnUsed script (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED = 0x19)
            // Container placeables: Toggles open/close, fires EVENT_OPEN_OBJECT or EVENT_CLOSE_OBJECT, executes OnOpen or OnClosed script
            IPlaceableComponent placeableState = placeable.GetComponent<IPlaceableComponent>();
            if (placeableState != null)
            {
                // Check if placeable is useable
                // Original engine: Useable field in UTP template must be TRUE
                if (!placeableState.IsUseable)
                {
                    return ActionStatus.Failed;
                }

                // Check if placeable is locked
                // Original engine: Locked field in UTP template, requires key or lockpick
                if (placeableState.IsLocked)
                {
                    // Fire OnLock script event
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_LOCK_OBJECT fires OnLock script event
                    // Located via string references: "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd), "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
                    IEventBus eventBus = actor.World.EventBus;
                    if (eventBus != null)
                    {
                        eventBus.FireScriptEvent(placeable, ScriptEvent.OnLock, actor);
                        eventBus.Publish(new PlaceableLockedEvent { Actor = actor, Placeable = placeable });
                    }
                    return ActionStatus.Failed;
                }

                // Record entering/clicking object for GetEnteringObject() and GetClickingObject() functions
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetEnteringObject/GetClickingObject track last entity that interacted with placeable
                // Located via string references: "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "OnClick" @ 0x007c1a20
                // Original implementation: Stores last interacting entity ID for script queries
                if (placeable is Entities.Entity placeableEntityImpl)
                {
                    placeableEntityImpl.SetData("LastEnteringObjectId", actor.ObjectId);
                    placeableEntityImpl.SetData("LastClickingObjectId", actor.ObjectId);
                }

                // Handle containers (open/close instead of use)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HasInventory flag determines if placeable is a container
                // Original implementation: Containers toggle open/close state, non-containers fire OnUsed
                if (placeableState.HasInventory)
                {
                    bool wasOpen = placeableState.IsOpen;
                    placeableState.IsOpen = !placeableState.IsOpen;
                    placeableState.AnimationState = placeableState.IsOpen ? 1 : 0; // 0=closed, 1=open

                    // Fire OnOpen/OnClose script events
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT/EVENT_CLOSE_OBJECT fire OnOpen/OnClose script events
                    // Located via string references: "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7), "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6)
                    IEventBus eventBus2 = actor.World.EventBus;
                    if (eventBus2 != null)
                    {
                        if (placeableState.IsOpen)
                        {
                            eventBus2.FireScriptEvent(placeable, ScriptEvent.OnOpen, actor);
                            eventBus2.Publish(new PlaceableOpenedEvent { Actor = actor, Placeable = placeable });
                        }
                        else
                        {
                            eventBus2.FireScriptEvent(placeable, ScriptEvent.OnClose, actor);
                            eventBus2.Publish(new PlaceableClosedEvent { Actor = actor, Placeable = placeable });
                        }
                    }
                }
                else
                {
                    // Fire OnUsed script event for non-container placeables
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_OPEN_OBJECT fires OnUsed script event for non-containers
                    // Located via string references: "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7), "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED" @ 0x007bc7d8 (0x19), "OnUsed" @ 0x007c1f70
                    IEventBus eventBus3 = actor.World.EventBus;
                    if (eventBus3 != null)
                    {
                        eventBus3.FireScriptEvent(placeable, ScriptEvent.OnUsed, actor);
                        eventBus3.Publish(new PlaceableUsedEvent { Actor = actor, Placeable = placeable });
                    }
                }
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when a placeable is used.
    /// </summary>
    public class PlaceableUsedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Placeable { get; set; }
        public IEntity Entity { get { return Placeable; } }
    }

    /// <summary>
    /// Event fired when a placeable container is opened.
    /// </summary>
    public class PlaceableOpenedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Placeable { get; set; }
        public IEntity Entity { get { return Placeable; } }
    }

    /// <summary>
    /// Event fired when a placeable container is closed.
    /// </summary>
    public class PlaceableClosedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Placeable { get; set; }
        public IEntity Entity { get { return Placeable; } }
    }

    /// <summary>
    /// Event fired when trying to use a locked placeable.
    /// </summary>
    public class PlaceableLockedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Placeable { get; set; }
        public IEntity Entity { get { return Placeable; } }
    }
}

