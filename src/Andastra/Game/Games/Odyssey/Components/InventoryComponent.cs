using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Odyssey.Components
{
    /// <summary>
    /// Component for managing entity inventory and equipped items.
    /// </summary>
    /// <remarks>
    /// Inventory Component:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) inventory system
    /// - Located via string references: "Inventory" @ 0x007c2504 (inventory field), "InventoryRes" @ 0x007bf570 (inventory resource)
    /// - "InventorySlot" @ 0x007bf7d0 (inventory slot field), "=INVENTORY" @ 0x007c1927 (inventory GFF structure)
    /// - "GAMEINPROGRESS:INVENTORY" @ 0x007c1570 (inventory game state), "HasInventory" @ 0x007c1fb0 (has inventory flag)
    /// - "GuiInventory" @ 0x007c24f4 (inventory GUI), "inventory_p" @ 0x007d1094 (inventory panel)
    /// - "InventorySound" @ 0x007c7164 (inventory sound), "inventorysnds" @ 0x007c7174 (inventory sounds directory)
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (inventory disturbed event, 0x1b)
    /// - Original implementation: Inventory stored in GFF format (UTC creature templates, save files)
    /// - FUN_005226d0 @ 0x005226d0 saves inventory data including equipped items and inventory bag
    /// - Inventory slots: Equipped items (weapon, armor, shield, etc.) and inventory bag (array of slots)
    /// - Slot indices: INVENTORY_SLOT_* constants from NWScript (0-17 for equipped, 18+ for inventory bag)
    /// - Equipped slots: INVENTORY_SLOT_HEAD (0), INVENTORY_SLOT_ARMS (1), INVENTORY_SLOT_IMPLANT (2), INVENTORY_SLOT_LEFTWEAPON (4),
    ///   INVENTORY_SLOT_BODY (6), INVENTORY_SLOT_LEFTHAND (7), INVENTORY_SLOT_RIGHTWEAPON (8), INVENTORY_SLOT_RIGHTHAND (9)
    /// - Inventory bag starts at slot 18 (INVENTORY_SLOT_CARMOUR + 1), stores up to MaxInventorySlots items
    /// - Items in inventory can be equipped to slots via ActionEquipItem, removed via ActionUnequipItem
    /// - Inventory events: ON_INVENTORY_DISTURBED fires when items are added/removed/equipped/unequipped
    /// - Based on KOTOR inventory system from vendor/PyKotor/wiki/ and plan documentation
    /// </remarks>
    public class InventoryComponent : IInventoryComponent
    {
        private readonly Dictionary<int, IEntity> _slots;
        private IEntity _owner;
        private const int MaxInventorySlots = 100; // Maximum inventory bag size

        public InventoryComponent([NotNull] IEntity owner)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            _slots = new Dictionary<int, IEntity>();
        }

        public IEntity Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public void OnAttach() { }

        public void OnDetach() { }

        /// <summary>
        /// Gets the item in the specified inventory slot.
        /// </summary>
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

            // Find first available inventory slot (start from slot 18, which is first inventory bag slot)
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
        public IEnumerable<IEntity> GetAllItems()
        {
            return _slots.Values.Where(item => item != null && item.IsValid).Distinct();
        }

        /// <summary>
        /// Checks if an item is in the inventory.
        /// </summary>
        private bool HasItem(IEntity item)
        {
            return _slots.Values.Contains(item);
        }
    }
}

