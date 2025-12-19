using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for managing entity inventory and equipped items.
    /// </summary>
    /// <remarks>
    /// Inventory Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe inventory system
    /// - Located via string references: "Inventory" @ 0x007c2504, "InventoryRes" @ 0x007bf570, "InventorySlot" @ 0x007bf7d0
    /// - "INVENTORY_SLOT_*" @ 0x007c1fb0 (inventory slot constants: RIGHTWEAPON=4, LEFTWEAPON=5, ARMOR=6, etc.)
    /// - "Equip_ItemList" @ 0x007c2f20 (equipped item list in UTC GFF), "ItemList" @ 0x007c2f28 (inventory item list)
    /// - Inventory slots: Equipped items (weapon, armor, shield, etc.) and inventory bag (array of slots)
    /// - Slot constants: 0=Head, 1=Gloves, 2=LeftRing, 3=RightRing, 4=RightWeapon, 5=LeftWeapon, 6=Armor, 7=Implant, 8=Belt, etc.
    /// - GetItemInSlot: Retrieves item entity in specified slot (returns null if empty)
    /// - SetItemInSlot: Places item entity in slot (null to clear/unequip)
    /// - AddItem: Adds item to first available inventory slot (slots 18+ are inventory bag slots)
    /// - RemoveItem: Removes item from inventory (from any slot)
    /// - HasItemByTag: Checks if entity possesses item with matching tag string
    /// - GetAllItems: Returns all items in inventory (equipped + inventory bag)
    /// - Original engine: Inventory stored in GFF format (Equip_ItemList and ItemList arrays in UTC creature templates, save files)
    /// - Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (save creature inventory), FUN_0050c510 @ 0x0050c510 (load creature inventory)
    /// </remarks>
    public interface IInventoryComponent : IComponent
    {
        /// <summary>
        /// Gets the item in the specified inventory slot.
        /// </summary>
        /// <param name="slot">Inventory slot index (INVENTORY_SLOT_* constants).</param>
        /// <returns>The item entity in the slot, or null if empty.</returns>
        IEntity GetItemInSlot(int slot);

        /// <summary>
        /// Sets an item in the specified inventory slot.
        /// </summary>
        /// <param name="slot">Inventory slot index.</param>
        /// <param name="item">The item entity to place in the slot, or null to clear.</param>
        void SetItemInSlot(int slot, IEntity item);

        /// <summary>
        /// Adds an item to the inventory (finds first available slot).
        /// </summary>
        /// <param name="item">The item entity to add.</param>
        /// <returns>True if the item was added, false if inventory is full.</returns>
        bool AddItem(IEntity item);

        /// <summary>
        /// Removes an item from the inventory.
        /// </summary>
        /// <param name="item">The item entity to remove.</param>
        /// <returns>True if the item was removed, false if not found.</returns>
        bool RemoveItem(IEntity item);

        /// <summary>
        /// Checks if the entity has an item with the specified tag.
        /// </summary>
        /// <param name="tag">The tag to search for.</param>
        /// <returns>True if the item is found, false otherwise.</returns>
        bool HasItemByTag(string tag);

        /// <summary>
        /// Gets all items in the inventory.
        /// </summary>
        /// <returns>Collection of item entities.</returns>
        System.Collections.Generic.IEnumerable<IEntity> GetAllItems();
    }
}

