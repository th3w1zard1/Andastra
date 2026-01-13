using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Party
{
    /// <summary>
    /// Manages shared party inventory.
    /// </summary>
    /// <remarks>
    /// Party Inventory:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) inventory system
    /// - Located via string references: "Inventory" @ 0x007c2504, "InventoryRes" @ 0x007bf570, "InventorySlot" @ 0x007bf7d0
    /// - "GAMEINPROGRESS:INVENTORY" @ 0x007c1570, "=INVENTORY" @ 0x007c1927, "HasInventory" @ 0x007c1fb0
    /// - "GuiInventory" @ 0x007c24f4, "InventorySound" @ 0x007c7164, "inventorysnds" @ 0x007c7174
    /// - "inventory_p" @ 0x007d1094, "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778
    /// - Original implementation: Inventory is shared across all party members
    /// - Maximum inventory slots: 100 (MaxSlots) - hardcoded limit in original engine
    /// - Items can be equipped to individual party members via equipment slots (0-17 for equipment, 18+ for inventory)
    /// - Inventory items stored in creature/item entities, managed per-party-member
    /// - Items persist across saves as part of creature/party state (saved in PARTYTABLE.res)
    /// - Inventory disturbance: Script event fires when items added/removed/moved (ON_INVENTORY_DISTURBED)
    /// - Inventory sounds: Play sound effects when items are moved or equipped
    /// </remarks>
    public class PartyInventory
    {
        private readonly List<InventoryItem> _items;

        /// <summary>
        /// Maximum inventory slots.
        /// </summary>
        public const int MaxSlots = 100;

        /// <summary>
        /// Event fired when item added.
        /// </summary>
        public event Action<InventoryItem> OnItemAdded;

        /// <summary>
        /// Event fired when item removed.
        /// </summary>
        public event Action<InventoryItem> OnItemRemoved;

        /// <summary>
        /// Event fired when item stack changes.
        /// </summary>
        public event Action<InventoryItem, int> OnStackChanged;

        public PartyInventory()
        {
            _items = new List<InventoryItem>();
        }

        #region Query

        /// <summary>
        /// Gets all items in inventory.
        /// </summary>
        public IReadOnlyList<InventoryItem> Items
        {
            get { return _items.AsReadOnly(); }
        }

        /// <summary>
        /// Gets current item count.
        /// </summary>
        public int ItemCount
        {
            get { return _items.Count; }
        }

        /// <summary>
        /// Checks if inventory is full.
        /// </summary>
        public bool IsFull
        {
            get { return _items.Count >= MaxSlots; }
        }

        /// <summary>
        /// Gets item by slot index.
        /// </summary>
        public InventoryItem GetItemAt(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return null;
            }
            return _items[index];
        }

        /// <summary>
        /// Finds item by ResRef.
        /// </summary>
        public InventoryItem FindByResRef(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            foreach (InventoryItem item in _items)
            {
                if (string.Equals(item.ResRef, resRef, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds item by tag.
        /// </summary>
        public InventoryItem FindByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            foreach (InventoryItem item in _items)
            {
                if (string.Equals(item.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Counts items with specific ResRef.
        /// </summary>
        public int CountItems(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return 0;
            }

            int count = 0;
            foreach (InventoryItem item in _items)
            {
                if (string.Equals(item.ResRef, resRef, StringComparison.OrdinalIgnoreCase))
                {
                    count += item.StackSize;
                }
            }

            return count;
        }

        /// <summary>
        /// Checks if inventory contains item.
        /// </summary>
        public bool HasItem(string resRef, int count = 1)
        {
            return CountItems(resRef) >= count;
        }

        #endregion

        #region Add/Remove

        /// <summary>
        /// Adds item to inventory.
        /// </summary>
        /// <param name="resRef">Item template ResRef.</param>
        /// <param name="count">Stack count.</param>
        /// <returns>True if added successfully.</returns>
        public bool AddItem(string resRef, int count = 1)
        {
            if (string.IsNullOrEmpty(resRef) || count <= 0)
            {
                return false;
            }

            // Check if stackable and already exists
            InventoryItem existing = FindByResRef(resRef);
            if (existing != null && existing.IsStackable)
            {
                int oldStack = existing.StackSize;
                existing.StackSize += count;

                if (OnStackChanged != null)
                {
                    OnStackChanged(existing, oldStack);
                }

                return true;
            }

            // Add new item
            if (IsFull)
            {
                return false;
            }

            var newItem = new InventoryItem
            {
                ResRef = resRef,
                StackSize = count
            };

            _items.Add(newItem);

            if (OnItemAdded != null)
            {
                OnItemAdded(newItem);
            }

            return true;
        }

        /// <summary>
        /// Adds item to inventory from entity.
        /// </summary>
        public bool AddItem(IEntity itemEntity)
        {
            if (itemEntity == null)
            {
                return false;
            }

            if (IsFull)
            {
                return false;
            }

            var item = new InventoryItem
            {
                Entity = itemEntity,
                ResRef = (itemEntity as Entities.Entity)?.TemplateResRef ?? "",
                Tag = itemEntity.Tag,
                StackSize = 1
            };

            _items.Add(item);

            if (OnItemAdded != null)
            {
                OnItemAdded(item);
            }

            return true;
        }

        /// <summary>
        /// Removes item from inventory.
        /// </summary>
        /// <param name="resRef">Item ResRef.</param>
        /// <param name="count">Amount to remove.</param>
        /// <returns>True if removed successfully.</returns>
        public bool RemoveItem(string resRef, int count = 1)
        {
            if (string.IsNullOrEmpty(resRef) || count <= 0)
            {
                return false;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                InventoryItem item = _items[i];
                if (!string.Equals(item.ResRef, resRef, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.StackSize > count)
                {
                    int oldStack = item.StackSize;
                    item.StackSize -= count;

                    if (OnStackChanged != null)
                    {
                        OnStackChanged(item, oldStack);
                    }

                    return true;
                }
                else if (item.StackSize == count)
                {
                    _items.RemoveAt(i);

                    if (OnItemRemoved != null)
                    {
                        OnItemRemoved(item);
                    }

                    return true;
                }
                else
                {
                    // Need to remove more from other stacks
                    count -= item.StackSize;
                    _items.RemoveAt(i);

                    if (OnItemRemoved != null)
                    {
                        OnItemRemoved(item);
                    }

                    i--; // Adjust index after removal
                }
            }

            return count == 0;
        }

        /// <summary>
        /// Removes specific item instance.
        /// </summary>
        public bool RemoveItem(InventoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (_items.Remove(item))
            {
                if (OnItemRemoved != null)
                {
                    OnItemRemoved(item);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes item at index.
        /// </summary>
        public bool RemoveItemAt(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            InventoryItem item = _items[index];
            _items.RemoveAt(index);

            if (OnItemRemoved != null)
            {
                OnItemRemoved(item);
            }

            return true;
        }

        /// <summary>
        /// Clears all items.
        /// </summary>
        public void Clear()
        {
            while (_items.Count > 0)
            {
                RemoveItemAt(_items.Count - 1);
            }
        }

        #endregion

        #region Sorting

        /// <summary>
        /// Sorts inventory by type.
        /// </summary>
        public void SortByType()
        {
            _items.Sort((a, b) => a.ItemType.CompareTo(b.ItemType));
        }

        /// <summary>
        /// Sorts inventory by name.
        /// </summary>
        public void SortByName()
        {
            _items.Sort((a, b) =>
            {
                string nameA = a.DisplayName ?? a.ResRef ?? "";
                string nameB = b.DisplayName ?? b.ResRef ?? "";
                return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });
        }

        #endregion
    }

    /// <summary>
    /// An item in the inventory.
    /// </summary>
    public class InventoryItem
    {
        /// <summary>
        /// Item entity (if spawned).
        /// </summary>
        public IEntity Entity { get; set; }

        /// <summary>
        /// Item template ResRef.
        /// </summary>
        public string ResRef { get; set; }

        /// <summary>
        /// Item tag.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Current stack size.
        /// </summary>
        public int StackSize { get; set; }

        /// <summary>
        /// Maximum stack size.
        /// </summary>
        public int MaxStack { get; set; }

        /// <summary>
        /// Whether this item can stack.
        /// </summary>
        public bool IsStackable
        {
            get { return MaxStack > 1; }
        }

        /// <summary>
        /// Item type/category.
        /// </summary>
        public ItemType ItemType { get; set; }

        /// <summary>
        /// Icon texture reference.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Base price in credits.
        /// </summary>
        public int BasePrice { get; set; }

        /// <summary>
        /// Current charges (for usable items).
        /// </summary>
        public int Charges { get; set; }

        /// <summary>
        /// Whether identified.
        /// </summary>
        public bool Identified { get; set; }

        /// <summary>
        /// Upgrade slots.
        /// </summary>
        public List<ItemUpgradeSlot> UpgradeSlots { get; set; }

        public InventoryItem()
        {
            StackSize = 1;
            MaxStack = 1;
            Identified = true;
            UpgradeSlots = new List<ItemUpgradeSlot>();
        }
    }

    /// <summary>
    /// Item type categories.
    /// </summary>
    public enum ItemType
    {
        None = 0,
        MeleeWeapon = 1,
        Lightsaber = 2,
        RangedWeapon = 3,
        Armor = 4,
        Headgear = 5,
        Gloves = 6,
        Belt = 7,
        Implant = 8,
        Shield = 9,
        ArmBand = 10,
        Usable = 11,
        Misc = 12,
        Quest = 13,
        Upgrade = 14,
        Crystal = 15
    }

    /// <summary>
    /// Item upgrade slot.
    /// </summary>
    public class ItemUpgradeSlot
    {
        /// <summary>
        /// Slot type.
        /// </summary>
        public int SlotType { get; set; }

        /// <summary>
        /// Installed upgrade ResRef.
        /// </summary>
        public string InstalledUpgrade { get; set; }

        /// <summary>
        /// Whether slot is available.
        /// </summary>
        public bool IsAvailable { get; set; }
    }
}
