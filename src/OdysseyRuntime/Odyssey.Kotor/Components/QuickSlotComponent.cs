using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Component for managing quick slot assignments (items and abilities).
    /// </summary>
    /// <remarks>
    /// Quick Slot Component:
    /// - Based on swkotor2.exe quick slot system
    /// - Located via string references: Quick slot system stores items/abilities for quick use
    /// - Quick slots: 0-11 (12 slots total) for storing items or abilities (spells/feats)
    /// - Quick slot types: QUICKSLOT_TYPE_ITEM (0), QUICKSLOT_TYPE_ABILITY (1)
    /// - Original implementation: Quick slots stored in creature GFF data (QuickSlot_* fields in UTC)
    /// - Quick slot storage: FUN_005226d0 @ 0x005226d0 saves QuickSlot_* fields to creature GFF, FUN_005223a0 @ 0x005223a0 loads QuickSlot_* fields from creature GFF
    /// - Quick slot fields: QuickSlot_0 through QuickSlot_11 (12 fields total) in UTC GFF format
    /// - Each QuickSlot_* field contains: Type (0=item, 1=ability), Item/ObjectId (for items), AbilityID (for abilities)
    /// - Quick slot usage: Using a slot triggers ActionUseItem (for items) or ActionCastSpellAtObject (for abilities)
    /// </remarks>
    public class QuickSlotComponent : IQuickSlotComponent
    {
        private const int MaxQuickSlots = 12; // 0-11
        private readonly Dictionary<int, IEntity> _itemSlots; // Slot index -> Item entity
        private readonly Dictionary<int, int> _abilitySlots; // Slot index -> Ability ID (spell/feat)
        private IEntity _owner;

        public QuickSlotComponent([NotNull] IEntity owner)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            _itemSlots = new Dictionary<int, IEntity>();
            _abilitySlots = new Dictionary<int, int>();
        }

        public IEntity Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public void OnAttach() { }

        public void OnDetach() { }

        /// <summary>
        /// Gets the item in the specified quick slot.
        /// </summary>
        public IEntity GetQuickSlotItem(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return null;
            }

            IEntity item;
            if (_itemSlots.TryGetValue(slot, out item))
            {
                return item;
            }

            return null;
        }

        /// <summary>
        /// Gets the ability ID in the specified quick slot.
        /// </summary>
        public int GetQuickSlotAbility(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return -1;
            }

            int abilityId;
            if (_abilitySlots.TryGetValue(slot, out abilityId))
            {
                return abilityId;
            }

            return -1;
        }

        /// <summary>
        /// Gets the type of content in the specified quick slot.
        /// </summary>
        public int GetQuickSlotType(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return -1;
            }

            if (_itemSlots.ContainsKey(slot))
            {
                return 0; // QUICKSLOT_TYPE_ITEM
            }

            if (_abilitySlots.ContainsKey(slot))
            {
                return 1; // QUICKSLOT_TYPE_ABILITY
            }

            return -1; // Empty
        }

        /// <summary>
        /// Sets an item in the specified quick slot.
        /// </summary>
        public void SetQuickSlotItem(int slot, IEntity item)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return;
            }

            // Clear ability from this slot if present
            _abilitySlots.Remove(slot);

            if (item == null)
            {
                // Clear item slot
                _itemSlots.Remove(slot);
            }
            else
            {
                // Set item slot
                _itemSlots[slot] = item;
            }
        }

        /// <summary>
        /// Sets an ability in the specified quick slot.
        /// </summary>
        public void SetQuickSlotAbility(int slot, int abilityId)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return;
            }

            // Clear item from this slot if present
            _itemSlots.Remove(slot);

            if (abilityId < 0)
            {
                // Clear ability slot
                _abilitySlots.Remove(slot);
            }
            else
            {
                // Set ability slot
                _abilitySlots[slot] = abilityId;
            }
        }
    }
}

