using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to use/interact with a placeable object.
    /// </summary>
    /// <remarks>
    /// Use Object Action:
    /// - Based on swkotor2.exe placeable interaction system
    /// - Located via string references: "OnUsed" @ 0x007be1c4 (placeable script event), "ScriptOnUsed" @ 0x007beeb8
    /// - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events (EVENT_OPEN_OBJECT case 7, EVENT_CLOSE_OBJECT case 6, EVENT_LOCK_OBJECT case 0xd, EVENT_UNLOCK_OBJECT case 0xc)
    /// - Original implementation: Moves actor to placeable use point, checks lock state, fires OnUsed script
    /// - Use distance: ~2.0 units (InteractRange)
    /// - Script events: OnUsed (placeable used), OnLocked (placeable locked), OnOpen (container opened), OnClose (container closed)
    /// - Containers: If HasInventory=true, opens/closes container instead of using
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
            IPlaceableComponent placeableState = placeable.GetComponent<IPlaceableComponent>();
            if (placeableState != null)
            {
                // Check if placeable is useable
                if (!placeableState.IsUseable)
                {
                    return ActionStatus.Failed;
                }

                // Check if placeable is locked
                if (placeableState.IsLocked)
                {
                    // Fire locked placeable event
                    IEventBus eventBus = actor.World.EventBus;
                    if (eventBus != null)
                    {
                        eventBus.Publish(new PlaceableLockedEvent { Actor = actor, Placeable = placeable });
                    }
                    return ActionStatus.Failed;
                }

                // Handle containers (open/close instead of use)
                if (placeableState.HasInventory)
                {
                    placeableState.IsOpen = !placeableState.IsOpen;

                    // Fire container opened/closed event
                    IEventBus eventBus2 = actor.World.EventBus;
                    if (eventBus2 != null)
                    {
                        if (placeableState.IsOpen)
                        {
                            eventBus2.Publish(new PlaceableOpenedEvent { Actor = actor, Placeable = placeable });
                        }
                        else
                        {
                            eventBus2.Publish(new PlaceableClosedEvent { Actor = actor, Placeable = placeable });
                        }
                    }
                }
                else
                {
                    // Fire used event for non-container placeables
                    IEventBus eventBus3 = actor.World.EventBus;
                    if (eventBus3 != null)
                    {
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

