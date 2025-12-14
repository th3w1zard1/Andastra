using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to equip an item to a specific inventory slot.
    /// </summary>
    public class ActionEquipItem : ActionBase
    {
        private readonly uint _itemObjectId;
        private readonly int _inventorySlot;

        public ActionEquipItem(uint itemObjectId, int inventorySlot)
            : base(ActionType.EquipItem)
        {
            _itemObjectId = itemObjectId;
            _inventorySlot = inventorySlot;
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

            IEntity item = actor.World.GetEntity(_itemObjectId);
            if (item == null || !item.IsValid || item.ObjectType != ObjectType.Item)
            {
                return ActionStatus.Failed;
            }

            // Check if item is in actor's inventory
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
                // Item not in inventory, try to add it first
                if (!inventory.AddItem(item))
                {
                    return ActionStatus.Failed;
                }
            }

            // Equip item to slot
            inventory.SetItemInSlot(_inventorySlot, item);

            return ActionStatus.Complete;
        }
    }
}

