using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to unequip an item from a specific inventory slot.
    /// </summary>
    /// <remarks>
    /// Unequip Item Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionUnequipItem NWScript function
    /// - Located via string references: "UnequipItem" @ 0x007be4e8, "EquippedItem" @ 0x007c23a0
    /// - "UnequipHItem" @ 0x007c3870, "UnequipItems" @ 0x007c3880 (unequip operations)
    /// - Inventory system: "Inventory" @ 0x007bd658, "InventorySlot" @ 0x007c49bc, "Item" @ 0x007bc54c
    /// - Equipment slots: "Armor" @ 0x007be1f8, "Helmet" @ 0x007be208, "Implant" @ 0x007be218
    /// - "RightArm" @ 0x007be238, "LeftArm" @ 0x007be248 (equipment slot references)
    /// - Item animations: "i_unequip" @ 0x007ccdec, "unequip" @ 0x007cdf5c
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Unequip item implementation
            // Located via string references: "UnequipHItem" @ 0x007c3870, "UnequipItems" @ 0x007c3880, "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
            // Original implementation: Removes item from equipment slot, removes stat modifications, returns item to inventory
            // Item remains in entity's inventory after unequipping (does not delete item)
            // Unequipping removes stat bonuses/penalties from item properties (AC, attack, damage, etc.)
            IEntity unequippedItem = inventory.GetItemInSlot(_inventorySlot);
            inventory.SetItemInSlot(_inventorySlot, null);

            // Fire OnInventoryDisturbed script event
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ON_INVENTORY_DISTURBED fires when items are equipped/unequipped
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
            // Original implementation: OnInventoryDisturbed script fires on actor entity when inventory is modified (equip/unequip/add/remove)
            IEventBus eventBus = actor.World?.EventBus;
            if (eventBus != null && unequippedItem != null)
            {
                eventBus.FireScriptEvent(actor, ScriptEvent.OnDisturbed, unequippedItem);
                eventBus.Publish(new ItemUnequippedEvent { Actor = actor, Item = unequippedItem, Slot = _inventorySlot });
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when an item is unequipped.
    /// </summary>
    public class ItemUnequippedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Item { get; set; }
        public int Slot { get; set; }
        public IEntity Entity { get { return Actor; } }
    }
}

