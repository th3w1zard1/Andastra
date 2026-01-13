using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to equip an item to a specific inventory slot.
    /// </summary>
    /// <remarks>
    /// Equip Item Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionEquipItem NWScript function
    /// - Located via string references: "EquipItem" @ 0x007be4e0, "EquippedItem" @ 0x007c23a0
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594, "Mod_OnEquipItem" @ 0x007beac8
    /// - Inventory system: "Inventory" @ 0x007bd658, "InventorySlot" @ 0x007c49bc, "Item" @ 0x007bc54c
    /// - "ItemList" @ 0x007bf580, "Equip_ItemList" @ 0x007bf5a4, "EquippedRes" @ 0x007bf598
    /// - Equipment slots: "Armor" @ 0x007be1f8, "Helmet" @ 0x007be208, "Implant" @ 0x007be218
    /// - "RightArm" @ 0x007be238, "LeftArm" @ 0x007be248, "RightEquip" @ 0x007c2d60, "LeftEquip" @ 0x007c2d6c
    /// - "EquipableSlots" @ 0x007c4584, "NonEquippable" @ 0x007c0938, "EquipSlotsLocked" @ 0x007cf540
    /// - Unequip: "UnequipHItem" @ 0x007c3870, "UnequipItems" @ 0x007c3880
    /// - Item fields: "ItemId" @ 0x007bef40, "BaseItem" @ 0x007c0a78, "ItemType" @ 0x007c437c
    /// - "ItemValue" @ 0x007c4f24, "ItemPropertyIndex" @ 0x007beb58, "ItemComponent" @ 0x007c41e4
    /// - "ItemClass" @ 0x007c455c, "BaseItemStatRef" @ 0x007c4428, "PoweredItem" @ 0x007c43b0
    /// - "AmmoItem" @ 0x007bf84c, "NewItem" @ 0x007c0930, "ItemCreate" @ 0x007c4f84
    /// - Item events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4, "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c, "ITEMLOST" @ 0x007bdf4c, "ITEMRECEIVED" @ 0x007bdf58
    /// - "LoseItems" @ 0x007bdaa8, "LoseItemsNum" @ 0x007bda6c, "LoseStolenItems" @ 0x007bdab4
    /// - "Mod_OnAcquirItem" @ 0x007be7e0, "Mod_OnUnAqreItem" @ 0x007be7cc, "Mod_OnActvtItem" @ 0x007be7f4
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0, "EVENT_ITEM_ON_HIT_SPELL_IMPACT" @ 0x007bcc8c
    /// - Item animations: "i_equip" @ 0x007cce04, "i_equipm" @ 0x007ccdf8, "i_unequip" @ 0x007ccdec
    /// - "equip" @ 0x007cdf54, "unequip" @ 0x007cdf5c, "i_useitemm" @ 0x007ccde0, "4?itemuse" @ 0x007c22f2
    /// - Item properties: "ITEMPROPS" @ 0x007caec4, "ItemPropDef" @ 0x007c4c20, "Item_Property" @ 0x007cb2f8
    /// - "ItemTargeting" @ 0x007c30a0, "ForbidItemMask" @ 0x007c30b0, "RequireItemMask" @ 0x007c30c0
    /// - "HideEquippedItems" @ 0x007c4e00, "BASEITEMS" @ 0x007c4594, "PROTOITEM" @ 0x007b6c0c
    /// - GUI: "BTN_EQUIP" @ 0x007cf894, "LBL_CANTEQUIP" @ 0x007cf884, "equip_p" @ 0x007cf970
    /// - "BTN_USEITEM" @ 0x007d1080, "BTN_GIVEITEMS" @ 0x007cf598, "BTN_UPGRADEITEM" @ 0x007d09d4
    /// - "BTN_CREATEITEMS" @ 0x007d0b48, "BTN_UPGRADEITEMS" @ 0x007d0b58, "upgradeitems_p" @ 0x007d09e4
    /// - "LB_ITEMS" @ 0x007ca9ac, "LB_INVITEMS" @ 0x007d0168, "LB_SHOPITEMS" @ 0x007d0174
    /// - "LBL_ITEM_DESCRIPTION" @ 0x007ca994, "LBL_ITEMRCVD" @ 0x007ccd4c, "BLBL_ITEMLOST" @ 0x007ccd3b
    /// - Debug: "EquippedItems: " @ 0x007cb110, "RepositoryItems: " @ 0x007cb020
    /// - Error messages:
    ///   - "Item template %s doesn't exist.\n" @ 0x007c2028
    ///   - "CreateItem::CreateItemEntry() -- Could not find a row for an item. Major error: " @ 0x007d07c8
    ///   - "Error: Invalid item" @ 0x007d110c
    /// - "Hide Unequippable" @ 0x007c8548, "MAXSINGLEITEMVALUE" @ 0x007c0774, "MaxItemPoints" @ 0x007bdb2c
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Equip item implementation
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594, "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
            // Original implementation: Sets item in equipment slot, modifies entity stats, fires OnInventoryDisturbed script event
            // OnInventoryDisturbed fires when items are added/removed/equipped/unequipped (CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED = 0x1b)
            // Item properties from baseitems.2da modify entity stats (AC, attack bonus, damage, abilities, etc.)
            inventory.SetItemInSlot(_inventorySlot, item);

            // Fire OnInventoryDisturbed script event
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ON_INVENTORY_DISTURBED fires when items are equipped/unequipped
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
            // Original implementation: OnInventoryDisturbed script fires on actor entity when inventory is modified (equip/unequip/add/remove)
            IEventBus eventBus = actor.World?.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(actor, ScriptEvent.OnDisturbed, item);
                eventBus.Publish(new ItemEquippedEvent { Actor = actor, Item = item, Slot = _inventorySlot });
            }

            return ActionStatus.Complete;
        }
    }

    /// <summary>
    /// Event fired when an item is equipped.
    /// </summary>
    public class ItemEquippedEvent : IGameEvent
    {
        public IEntity Actor { get; set; }
        public IEntity Item { get; set; }
        public int Slot { get; set; }
        public IEntity Entity { get { return Actor; } }
    }
}

