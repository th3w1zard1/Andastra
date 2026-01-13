using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to drop/put down an item from inventory to the world.
    /// </summary>
    /// <remarks>
    /// Put Down Item Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionPutDownItem NWScript function
    /// - Located via string references: "GiveItem" @ 0x007be4f8 (give item action), "PutDownItem" action type (ACTION_TYPE_PUT_DOWN_ITEM constant)
    /// - Inventory system: "Inventory" @ 0x007bd658 (inventory field), "Item" @ 0x007bc54c (item object type), "ItemList" @ 0x007bf580 (item list field)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c (lose item script event, 0x1c) - fires when item is removed from inventory
    /// - Original implementation: Removes item from inventory and places it in world at specified location
    /// - Item validation: Checks if item exists in actor's inventory before dropping
    /// - Item removal: Removes item from inventory (via IInventoryComponent.RemoveItem)
    /// - Item placement: Item position set to drop location (via ITransformComponent.Position)
    /// - World item: Item becomes a world-dropped item that can be picked up by other entities (ObjectType.Item in world)
    /// - Usage: Dropping items on ground, giving items to other entities (via different action), placing items in containers (via ActionUseObject)
    /// - Item visibility: Item becomes visible in world after being dropped (rendering system should display item model)
    /// - Action completes immediately if item is in inventory and location is valid (single frame execution)
    /// - Event firing: ON_LOSE_ITEM event may fire when item is removed from inventory (for item tracking)
    /// - Based on NWScript function ActionPutDownItem (routine ID varies by game version)
    /// </remarks>
    public class ActionPutDownItem : ActionBase
    {
        private readonly uint _itemObjectId;
        private readonly Vector3 _dropLocation;

        public ActionPutDownItem(uint itemObjectId, Vector3 dropLocation)
            : base(ActionType.GiveItem)
        {
            _itemObjectId = itemObjectId;
            _dropLocation = dropLocation;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (actor == null || !actor.IsValid)
            {
                return ActionStatus.Failed;
            }

            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return ActionStatus.Failed;
            }

            // Check if item is in inventory
            bool hasItem = false;
            foreach (IEntity invItem in inventory.GetAllItems())
            {
                if (invItem != null && invItem.ObjectId == _itemObjectId)
                {
                    hasItem = true;
                    break;
                }
            }

            if (!hasItem)
            {
                return ActionStatus.Failed;
            }

            // Remove item from inventory
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionPutDownItem implementation
            // Located via string references: "GiveItem" @ 0x007be4f8, "PutDownItem" action type
            // Original implementation: Removes item from inventory, places in world at drop location
            // Item becomes world-dropped item that can be picked up by other entities
            IEntity item = actor.World.GetEntity(_itemObjectId);
            if (item != null && inventory.RemoveItem(item))
            {
                // Fire OnLoseItem script event
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM fires when item is removed from inventory
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c (0x1c)
                // Original implementation: OnLoseItem script fires on actor entity when item is removed from inventory
                IEventBus eventBus = actor.World.EventBus;
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(actor, ScriptEvent.OnUnacquireItem, item);
                }

                // Place item in world at drop location
                // Original engine: Item position set to drop location, item becomes visible in world
                ITransformComponent itemTransform = item.GetComponent<ITransformComponent>();
                if (itemTransform != null)
                {
                    itemTransform.Position = _dropLocation;
                }

                return ActionStatus.Complete;
            }

            return ActionStatus.Failed;
        }
    }
}

