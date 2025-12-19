using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for managing entity inventory and equipped items.
    /// </summary>
    /// <remarks>
    /// Inventory Component Interface:
    /// - Common interface for inventory management across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Provides slot-based inventory system with equipped items and inventory bag
    /// - Inventory slots: Equipped items (weapon, armor, shield, etc.) and inventory bag (array of slots)
    /// - Slot-based system: Items are stored in numbered slots, with specific slots reserved for equipment
    /// - GetItemInSlot: Retrieves item entity in specified slot (returns null if empty)
    /// - SetItemInSlot: Places item entity in slot (null to clear/unequip)
    /// - AddItem: Adds item to first available inventory slot
    /// - RemoveItem: Removes item from inventory (from any slot)
    /// - HasItemByTag: Checks if entity possesses item with matching tag string
    /// - GetAllItems: Returns all items in inventory (equipped + inventory bag)
    /// - Engine-specific implementations handle slot numbering, equipment types, and storage formats
    /// - Inventory state is typically persisted in save files and entity templates
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

