using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to equip an item to a specific inventory slot.
    /// </summary>
    /// <remarks>
    /// Equip Item Action:
    /// - Based on swkotor2.exe ActionEquipItem NWScript function
    /// - Located via string references: "EquipItem" @ 0x007be4e0, "EquippedItem" @ 0x007c23a0
    /// - Inventory system: "Inventory" @ 0x007bd658, "InventorySlot" @ 0x007c49bc, "Item" @ 0x007bc54c
    /// - Equipment slots: "Armor" @ 0x007be1f8, "Helmet" @ 0x007be208, "Implant" @ 0x007be218
    /// - "RightArm" @ 0x007be238, "LeftArm" @ 0x007be248 (equipment slot references)
    /// - Original implementation: Equips item from inventory to specified equipment slot
    /// - Items must be in entity's inventory before equipping
    /// - Equipment slots: Armor, Helmet, Implant, RightArm, LeftArm, etc. (see InventorySlot enum)
    /// - Equipping item modifies entity stats (AC, damage, abilities, etc.) based on item properties
    /// - Action queues to entity action queue, completes immediately if item is in inventory and slot is valid
    /// </remarks>
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

