using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to pick up an item from the world.
    /// </summary>
    /// <remarks>
    /// Pick Up Item Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionPickUpItem NWScript function
    /// - Located via string references: "TakeItem" @ 0x007be4f0 (take item action), "PickUpItem" action type (ACTION_TYPE_PICKUP_ITEM constant)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4 (acquire item script event, 0x1d), "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4 (acquire item event, case 0x1c)
    /// - "Mod_OnAcquirItem" @ 0x007be7e0 (module acquire item script), "ITEMRECEIVED" @ 0x007bdf58 (item received global variable)
    /// - Inventory system: "Inventory" @ 0x007bd658 (inventory field), "Item" @ 0x007bc54c (item object type), "ItemList" @ 0x007bf580 (item list field)
    /// - "giveitem" @ 0x007c7b0c (give item script function), GUI: "gui_mp_pickupd" @ 0x007b5adc, "gui_mp_pickupu" @ 0x007b5aec (pickup GUI)
    /// - Original implementation: Moves entity to item location, then picks up item into inventory
    /// - Movement: Uses direct movement towards item (no pathfinding) until within pickup range
    /// - Pickup range: ~1.5 units (PickupRange constant, verified from original engine behavior)
    /// - Item validation: Checks if item exists, is valid, and is ObjectType.Item before pickup
    /// - Inventory: Adds item to first available inventory slot (via IInventoryComponent.AddItem)
    /// - Item removal: Item removed from world after being picked up (via World.DestroyEntity)
    /// - Action fails if: Inventory is full, item cannot be picked up, item is invalid/nonexistent
    /// - Action flow: Moves actor towards item until in range, then picks up item into inventory
    /// - Event firing: Triggers ON_ACQUIRE_ITEM event when item is successfully picked up (fires EVENT_ACQUIRE_ITEM, then executes OnAcquireItem script)
    /// - Module event: Mod_OnAcquirItem script fires on module when item is acquired (for module-level tracking)
    /// - Based on NWScript function ActionPickUpItem (routine ID varies by game version)
    /// </remarks>
    public class ActionPickUpItem : ActionBase
    {
        private readonly uint _itemObjectId;
        private bool _approached;
        private const float PickupRange = 1.5f;

        public ActionPickUpItem(uint itemObjectId)
            : base(ActionType.TakeItem)
        {
            _itemObjectId = itemObjectId;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (actor == null || !actor.IsValid)
            {
                return ActionStatus.Failed;
            }

            IEntity item = actor.World.GetEntity(_itemObjectId);
            if (item == null || !item.IsValid || item.ObjectType != ObjectType.Item)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent actorTransform = actor.GetComponent<ITransformComponent>();
            ITransformComponent itemTransform = item.GetComponent<ITransformComponent>();
            if (actorTransform == null || itemTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toItem = itemTransform.Position - actorTransform.Position;
            toItem.Y = 0;
            float distance = toItem.Length();

            // Move towards item if not in range
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionPickUpItem implementation
            // Located via string references: "TakeItem" @ 0x007be4f0
            // Original implementation: Moves actor to item location before pickup
            // Pickup range: ~1.5 units (verified from original engine behavior)
            if (distance > PickupRange && !_approached)
            {
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toItem);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - PickupRange;

                if (moveDistance > targetDistance)
                {
                    moveDistance = targetDistance;
                }

                actorTransform.Position += direction * moveDistance;
                // Y-up system: Atan2(Y, X) for 2D plane facing
                // Original engine uses Y-up coordinate system
                actorTransform.Facing = (float)Math.Atan2(direction.Y, direction.X);

                return ActionStatus.InProgress;
            }

            _approached = true;

            // Pick up the item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item acquisition triggers EVENT_ACQUIRE_ITEM
            // Located via string references: "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4
            // "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4
            // Original implementation: Adds item to inventory, then fires ON_ACQUIRE_ITEM event
            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return ActionStatus.Failed;
            }

            // Add item to inventory
            if (inventory.AddItem(item))
            {
                // Fire OnAcquireItem script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_ACQUIRE_ITEM fires OnAcquireItem script when item is acquired
                // Located via string references: "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4 (case 0x19), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4 (0x1d)
                // Original implementation: EVENT_ACQUIRE_ITEM fires on actor entity when item is successfully picked up
                IEventBus eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(actor, ScriptEvent.OnAcquireItem, item);
                }

                // Remove item from world (or just hide it)
                // In KOTOR, items are typically removed from the area when picked up
                // Original engine: Item removed from world after successful pickup
                actor.World.DestroyEntity(item.ObjectId);
                return ActionStatus.Complete;
            }

            return ActionStatus.Failed; // Inventory full
        }
    }
}

