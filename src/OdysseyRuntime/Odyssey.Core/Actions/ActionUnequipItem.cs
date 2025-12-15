using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to unequip an item from a specific inventory slot.
    /// </summary>
    /// <remarks>
    /// Unequip Item Action:
    /// - Based on swkotor2.exe ActionUnequipItem NWScript function
    /// - Located via string references: "UnequipItem" @ 0x007be4e8, "EquippedItem" @ 0x007c23a0
    /// - Inventory system: "Inventory" @ 0x007bd658, "InventorySlot" @ 0x007c49bc, "Item" @ 0x007bc54c
    /// - Equipment slots: "Armor" @ 0x007be1f8, "Helmet" @ 0x007be208, "Implant" @ 0x007be218
    /// - "RightArm" @ 0x007be238, "LeftArm" @ 0x007be248 (equipment slot references)
    /// - Original implementation: Removes item from specified equipment slot, returns it to inventory
    /// - Unequipping item removes stat modifications (AC, damage, abilities, etc.)
    /// - Item remains in entity's inventory after unequipping
    /// - Equipment slots: Armor, Helmet, Implant, RightArm, LeftArm, etc. (see InventorySlot enum)
    /// - Action queues to entity action queue, completes immediately if slot has equipped item
    /// </remarks>
    public class ActionUnequipItem : ActionBase
    {
        private readonly int _inventorySlot;

        public ActionUnequipItem(int inventorySlot)
            : base(ActionType.UnequipItem)
        {
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

            // Clear the slot (unequip)
            inventory.SetItemInSlot(_inventorySlot, null);

            return ActionStatus.Complete;
        }
    }
}

