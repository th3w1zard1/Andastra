using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Component for managing entity inventory and equipped items in Eclipse engine (Dragon Age Origins, Dragon Age 2).
    /// </summary>
    /// <remarks>
    /// Eclipse Inventory Component:
    /// - Based on daorigins.exe and DragonAge2.exe inventory system
    /// - Located via string references: "Inventory" @ 0x00ae88ec (daorigins.exe)
    /// - "Equip_ItemList" @ 0x00af6e54 (daorigins.exe) - Equipped items list structure
    /// - "EquippedItems" @ 0x00aeda94 (daorigins.exe) - Equipped items field
    /// - "Equipment" @ 0x00af7768 (daorigins.exe) - Equipment field
    /// - "EquipmentLayout" @ 0x00af7690 (daorigins.exe) - Equipment layout system
    /// - "COMMAND_GETITEMINEQUIPSLOT" @ 0x00af24cc (daorigins.exe) - Get item in equip slot command
    /// - "COMMAND_GETITEMEQUIPSLOT" @ 0x00af2af4 (daorigins.exe) - Get item equip slot command
    /// - "EquipItemMessage" @ 0x00aec670 (daorigins.exe) - Equip item message
    /// - "UnequipItemMessage" @ 0x00aec694 (daorigins.exe) - Unequip item message
    /// - "AcquireItemMessage" @ 0x00aec5f8 (daorigins.exe) - Acquire item message
    /// - "DropItemMessage" @ 0x00aec650 (daorigins.exe) - Drop item message
    /// - Original implementation: Inventory stored in binary format (not GFF like Odyssey/Aurora)
    /// - Equip_ItemList structure: Contains equipped items with slot indices
    /// - Inventory slots: Equipped items (weapon, armor, etc.) and inventory bag (array of slots)
    /// - Slot indices: Eclipse uses different slot numbering than Odyssey/Aurora (varies by game)
    /// - Equipment layout: Eclipse has EquipmentLayout system for visual equipment display
    /// - Inventory bag: Stores items in numbered slots, similar to Odyssey/Aurora but different slot numbering
    /// - Items in inventory can be equipped to slots via EquipItemMessage, removed via UnequipItemMessage
    /// - Inventory events: InventoryClosedMessage, ForceInventoryRefreshMessage
    /// - Eclipse-specific: Uses Equip_ItemList structure instead of GFF format
    /// - Eclipse-specific: EquipmentLayout system for visual equipment management
    /// - Eclipse-specific: Different slot numbering system than Odyssey/Aurora
    /// 
    /// Key differences from Odyssey/Aurora:
    /// - Uses Equip_ItemList binary structure instead of GFF format
    /// - EquipmentLayout system for visual equipment management
    /// - Different slot numbering (Eclipse-specific slot indices)
    /// - Different inventory bag structure
    /// 
    /// Cross-engine comparison:
    /// - Odyssey (swkotor.exe, swkotor2.exe): GFF format, INVENTORY_SLOT_* constants (0-17 for equipped, 18+ for bag)
    /// - Aurora (nwmain.exe, nwn2main.exe): GFF format, similar slot numbering to Odyssey
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Equip_ItemList binary format, different slot numbering, EquipmentLayout system
    /// </remarks>
    public class EclipseInventoryComponent : IInventoryComponent
    {
        private readonly Dictionary<int, IEntity> _slots;
        private IEntity _owner;
        private const int MaxInventorySlots = 100; // Maximum inventory bag size (Eclipse-specific)

        /// <summary>
        /// Initializes a new instance of the Eclipse inventory component.
        /// </summary>
        /// <param name="owner">The entity that owns this inventory.</param>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Inventory component initialization
        /// </remarks>
        public EclipseInventoryComponent([NotNull] IEntity owner)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            _slots = new Dictionary<int, IEntity>();
        }

        #region IComponent Implementation

        public IEntity Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public void OnAttach()
        {
            // Initialize from entity data if available
            if (Owner != null)
            {
                // Try to load inventory from entity's stored data
                LoadFromEntityData();
            }
        }

        public void OnDetach()
        {
            // Save inventory back to entity data if needed
        }

        #endregion

        #region IInventoryComponent Implementation

        /// <summary>
        /// Gets the item in the specified inventory slot.
        /// </summary>
        /// <param name="slot">Inventory slot index (Eclipse-specific slot numbering).</param>
        /// <returns>The item entity in the slot, or null if empty.</returns>
        /// <remarks>
        /// Based on daorigins.exe: "COMMAND_GETITEMINEQUIPSLOT" @ 0x00af24cc
        /// Eclipse uses different slot numbering than Odyssey/Aurora.
        /// </remarks>
        public IEntity GetItemInSlot(int slot)
        {
            IEntity item;
            if (_slots.TryGetValue(slot, out item))
            {
                return item;
            }
            return null;
        }

        /// <summary>
        /// Sets an item in the specified inventory slot.
        /// </summary>
        /// <param name="slot">Inventory slot index (Eclipse-specific slot numbering).</param>
        /// <param name="item">The item entity to place in the slot, or null to clear.</param>
        /// <remarks>
        /// Based on daorigins.exe: Equip_ItemList structure manipulation
        /// Eclipse uses Equip_ItemList structure for equipped items.
        /// </remarks>
        public void SetItemInSlot(int slot, IEntity item)
        {
            if (item == null)
            {
                // Clear slot
                _slots.Remove(slot);
            }
            else
            {
                // Remove item from any previous slot
                RemoveItem(item);

                // Place item in new slot
                _slots[slot] = item;
            }
        }

        /// <summary>
        /// Adds an item to the inventory (finds first available slot).
        /// </summary>
        /// <param name="item">The item entity to add.</param>
        /// <returns>True if the item was added, false if inventory is full.</returns>
        /// <remarks>
        /// Based on daorigins.exe: "AcquireItemMessage" @ 0x00aec5f8
        /// Eclipse inventory bag starts after equipped slots (Eclipse-specific slot numbering).
        /// </remarks>
        public bool AddItem(IEntity item)
        {
            if (item == null)
            {
                return false;
            }

            // Check if item is already in inventory
            if (HasItem(item))
            {
                return false; // Already in inventory
            }

            // Find first available inventory slot
            // Eclipse uses different slot numbering than Odyssey/Aurora
            // Inventory bag slots typically start after equipped slots (exact numbering varies by game)
            // For compatibility, we use slot 18+ for inventory bag (similar to Odyssey/Aurora)
            // but Eclipse-specific implementations may use different numbering
            for (int slot = 18; slot < 18 + MaxInventorySlots; slot++)
            {
                if (!_slots.ContainsKey(slot))
                {
                    _slots[slot] = item;
                    return true;
                }
            }

            return false; // Inventory full
        }

        /// <summary>
        /// Removes an item from the inventory.
        /// </summary>
        /// <param name="item">The item entity to remove.</param>
        /// <returns>True if the item was removed, false if not found.</returns>
        /// <remarks>
        /// Based on daorigins.exe: "DropItemMessage" @ 0x00aec650
        /// Eclipse removes items from Equip_ItemList structure.
        /// </remarks>
        public bool RemoveItem(IEntity item)
        {
            if (item == null)
            {
                return false;
            }

            // Find and remove item from any slot
            var slotsToRemove = new List<int>();
            foreach (KeyValuePair<int, IEntity> kvp in _slots)
            {
                if (kvp.Value == item)
                {
                    slotsToRemove.Add(kvp.Key);
                }
            }

            foreach (int slot in slotsToRemove)
            {
                _slots.Remove(slot);
            }

            return slotsToRemove.Count > 0;
        }

        /// <summary>
        /// Checks if the entity has an item with the specified tag.
        /// </summary>
        /// <param name="tag">The tag to search for.</param>
        /// <returns>True if the item is found, false otherwise.</returns>
        /// <remarks>
        /// Based on daorigins.exe: Item tag lookup in Equip_ItemList structure
        /// </remarks>
        public bool HasItemByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            foreach (IEntity item in _slots.Values)
            {
                if (item != null && item.IsValid && string.Equals(item.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all items in the inventory.
        /// </summary>
        /// <returns>Collection of item entities.</returns>
        /// <remarks>
        /// Based on daorigins.exe: Iterates through Equip_ItemList structure
        /// </remarks>
        public IEnumerable<IEntity> GetAllItems()
        {
            return _slots.Values.Where(item => item != null && item.IsValid).Distinct();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if an item is in the inventory.
        /// </summary>
        private bool HasItem(IEntity item)
        {
            return _slots.Values.Contains(item);
        }

        /// <summary>
        /// Loads inventory from entity's stored data.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Inventory is loaded from Equip_ItemList structure
        /// Eclipse uses "EquippedItems" and "Equip_ItemList" terminology
        /// </remarks>
        private void LoadFromEntityData()
        {
            if (Owner == null)
            {
                return;
            }

            // Load equipped items from entity data
            // Based on daorigins.exe: "EquippedItems" @ 0x00aeda94, "Equip_ItemList" @ 0x00af6e54
            if (Owner.HasData("EquippedItems") || Owner.HasData("Equip_ItemList"))
            {
                object equippedItemsObj = Owner.GetData("EquippedItems") ?? Owner.GetData("Equip_ItemList");
                if (equippedItemsObj != null)
                {
                    // Equipped items stored as a dictionary/list of slot -> item mappings
                    System.Collections.Generic.IDictionary<string, object> equippedItemsDict = equippedItemsObj as System.Collections.Generic.IDictionary<string, object>;
                    if (equippedItemsDict != null)
                    {
                        foreach (var kvp in equippedItemsDict)
                        {
                            int slot;
                            if (int.TryParse(kvp.Key, out slot))
                            {
                                // Item stored as ObjectId or entity reference
                                object itemObj = kvp.Value;
                                if (itemObj != null)
                                {
                                    // Try to get entity by ObjectId if stored as uint
                                    if (itemObj is uint objectId)
                                    {
                                        // Look up entity by ObjectId using world reference
                                        // Based on daorigins.exe: Entity lookup by ObjectId for equipped items
                                        // Located via string references: "ObjectId" @ 0x00af4e74 (daorigins.exe)
                                        // Original implementation: FUN_004e9de0 @ 0x004e9de0 (GetObject wrapper)
                                        // O(1) dictionary lookup by ObjectId (uint32)
                                        if (Owner.World != null)
                                        {
                                            IEntity item = Owner.World.GetEntity(objectId);
                                            if (item != null && item.IsValid)
                                            {
                                                _slots[slot] = item;
                                            }
                                        }
                                    }
                                    else if (itemObj is IEntity item)
                                    {
                                        _slots[slot] = item;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Try as list of slot/item pairs
                        // Based on daorigins.exe: Equip_ItemList can be stored as a list structure
                        // Eclipse save format: List of (slot, item) pairs or list of objects with Slot/Item properties
                        // Serialization format: List<(int slot, IEntity item)> or List<(int slot, uint objectId)>
                        System.Collections.IEnumerable enumerable = equippedItemsObj as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (object itemEntry in enumerable)
                            {
                                int slot = -1;
                                IEntity item = null;
                                uint objectId = 0;

                                // Try to parse as tuple (int slot, IEntity item) or (int slot, uint objectId)
                                // Based on EclipseEntity.Serialize: allItems.Add((slot, item))
                                // C# tuples are stored as ValueTuple<...> or Tuple<...>
                                if (itemEntry != null)
                                {
                                    // Check if it's a ValueTuple with 2 elements (slot, item)
                                    System.Type entryType = itemEntry.GetType();
                                    if (entryType.IsGenericType)
                                    {
                                        System.Type genericTypeDef = entryType.GetGenericTypeDefinition();
                                        // ValueTuple<T1, T2> or Tuple<T1, T2>
                                        if (genericTypeDef == typeof(System.ValueTuple<,>) || genericTypeDef == typeof(System.Tuple<,>))
                                        {
                                            // Get the two type arguments
                                            System.Type[] typeArgs = entryType.GetGenericArguments();
                                            if (typeArgs.Length == 2)
                                            {
                                                // First element should be int (slot)
                                                if (typeArgs[0] == typeof(int))
                                                {
                                                    // Get slot value using reflection
                                                    var slotProperty = entryType.GetProperty("Item1");
                                                    if (slotProperty != null)
                                                    {
                                                        object slotObj = slotProperty.GetValue(itemEntry);
                                                        if (slotObj is int slotValue)
                                                        {
                                                            slot = slotValue;
                                                        }
                                                    }

                                                    // Second element could be IEntity or uint (ObjectId)
                                                    var itemProperty = entryType.GetProperty("Item2");
                                                    if (itemProperty != null)
                                                    {
                                                        object itemObj = itemProperty.GetValue(itemEntry);
                                                        if (itemObj is IEntity entityItem)
                                                        {
                                                            item = entityItem;
                                                        }
                                                        else if (itemObj is uint objectIdValue)
                                                        {
                                                            objectId = objectIdValue;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // If not a tuple, try as object with Slot and Item/ObjectId properties
                                    // Based on Eclipse save format: Objects with Slot and Item properties
                                    if (slot == -1 && item == null && objectId == 0)
                                    {
                                        System.Type objType = itemEntry.GetType();
                                        // Try to get Slot property
                                        var slotProp = objType.GetProperty("Slot");
                                        if (slotProp != null)
                                        {
                                            object slotObj = slotProp.GetValue(itemEntry);
                                            if (slotObj is int slotValue)
                                            {
                                                slot = slotValue;
                                            }
                                        }

                                        // Try to get Item property (IEntity)
                                        var itemProp = objType.GetProperty("Item");
                                        if (itemProp != null)
                                        {
                                            object itemObj = itemProp.GetValue(itemEntry);
                                            if (itemObj is IEntity entityItem)
                                            {
                                                item = entityItem;
                                            }
                                        }

                                        // Try to get ObjectId property (uint)
                                        var objectIdProp = objType.GetProperty("ObjectId");
                                        if (objectIdProp != null && objectId == 0)
                                        {
                                            object objectIdObj = objectIdProp.GetValue(itemEntry);
                                            if (objectIdObj is uint objectIdValue)
                                            {
                                                objectId = objectIdValue;
                                            }
                                        }
                                    }

                                    // If still not found, try as array/list where [0] = slot, [1] = item/objectId
                                    // Based on Eclipse save format: Arrays can represent slot/item pairs
                                    if (slot == -1 && item == null && objectId == 0)
                                    {
                                        System.Collections.IEnumerable arrayEnum = itemEntry as System.Collections.IEnumerable;
                                        if (arrayEnum != null)
                                        {
                                            var arrayList = new System.Collections.ArrayList();
                                            foreach (object arrayItem in arrayEnum)
                                            {
                                                arrayList.Add(arrayItem);
                                            }
                                            if (arrayList.Count >= 2)
                                            {
                                                // First element is slot
                                                if (arrayList[0] is int slotValue)
                                                {
                                                    slot = slotValue;
                                                }

                                                // Second element is item or objectId
                                                if (arrayList[1] is IEntity entityItem)
                                                {
                                                    item = entityItem;
                                                }
                                                else if (arrayList[1] is uint objectIdValue)
                                                {
                                                    objectId = objectIdValue;
                                                }
                                            }
                                        }
                                    }
                                }

                                // If we have a valid slot, try to get the item
                                if (slot >= 0)
                                {
                                    // If we have objectId but not item, look up entity by ObjectId
                                    if (item == null && objectId != 0 && Owner.World != null)
                                    {
                                        // Look up entity by ObjectId using world reference
                                        // Based on daorigins.exe: Entity lookup by ObjectId for equipped items
                                        // Located via string references: "ObjectId" @ 0x00af4e74 (daorigins.exe)
                                        // Original implementation: FUN_004e9de0 @ 0x004e9de0 (GetObject wrapper)
                                        // O(1) dictionary lookup by ObjectId (uint32)
                                        item = Owner.World.GetEntity(objectId);
                                    }

                                    // Add item to slot if we have a valid item
                                    if (item != null && item.IsValid)
                                    {
                                        _slots[slot] = item;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Load inventory bag items from entity data
            // Eclipse inventory bag structure (varies by game)
            if (Owner.HasData("InventoryBag") || Owner.HasData("Inventory"))
            {
                object inventoryBagObj = Owner.GetData("InventoryBag") ?? Owner.GetData("Inventory");
                if (inventoryBagObj != null)
                {
                    // Inventory bag stored as a list/dictionary of items
                    System.Collections.IEnumerable enumerable = inventoryBagObj as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        int slot = 18; // Start inventory bag slots at 18 (Eclipse-specific numbering may differ)
                        foreach (object itemObj in enumerable)
                        {
                            IEntity item = null;

                            // Handle items stored as ObjectId (uint)
                            if (itemObj is uint objectId)
                            {
                                // Look up entity by ObjectId using world reference
                                // Based on daorigins.exe: Entity lookup by ObjectId for inventory bag items
                                // Located via string references: "ObjectId" @ 0x00af4e74 (daorigins.exe)
                                // Original implementation: FUN_004e9de0 @ 0x004e9de0 (GetObject wrapper)
                                // O(1) dictionary lookup by ObjectId (uint32)
                                if (Owner.World != null)
                                {
                                    item = Owner.World.GetEntity(objectId);
                                }
                            }
                            // Handle items stored as direct entity references
                            else if (itemObj is IEntity entityItem)
                            {
                                item = entityItem;
                            }

                            // Add item to slot if valid
                            if (item != null && item.IsValid)
                            {
                                _slots[slot] = item;
                                slot++;
                                if (slot >= 18 + MaxInventorySlots)
                                {
                                    break; // Inventory full
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}

