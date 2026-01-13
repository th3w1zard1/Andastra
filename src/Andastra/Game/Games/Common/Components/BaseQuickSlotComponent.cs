using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of quick slot component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Quick Slot Component Implementation:
    /// - Common quick slot properties and methods across all engines
    /// - Handles base quick slot storage, item/ability assignment, slot type checking
    /// - Provides base for engine-specific quick slot component implementations
    /// - Cross-engine analysis: All engines share common quick slot structure patterns
    /// - Common functionality: Item slots, Ability slots, Slot type checking, Get/Set operations
    /// - Engine-specific: Number of slots, GFF field names, Serialization format, Function addresses, Ability ID format
    ///
    /// Based on reverse engineering of quick slot systems across multiple BioWare engines.
    ///
    /// Common structure across engines:
    /// - Item slots: Dictionary mapping slot index to item entity
    /// - Ability slots: Dictionary mapping slot index to ability ID (spell/feat/talent ID depending on engine)
    /// - Slot types: 0 = item, 1 = ability (common across Odyssey, Aurora uses different type codes but same concept)
    /// - Max slots: Engine-specific (Odyssey: 12, Aurora: varies, Eclipse: varies)
    /// - Slot validation: All engines validate slot index bounds
    /// - Slot clearing: Setting item clears ability and vice versa (mutually exclusive per slot)
    ///
    /// Common quick slot operations across engines:
    /// - GetQuickSlotItem: Retrieves item entity from slot (returns null if empty or ability)
    /// - GetQuickSlotAbility: Retrieves ability ID from slot (returns -1 if empty or item)
    /// - GetQuickSlotType: Returns slot type (0=item, 1=ability, -1=empty)
    /// - SetQuickSlotItem: Assigns item to slot (clears ability if present)
    /// - SetQuickSlotAbility: Assigns ability to slot (clears item if present)
    /// </remarks>
    [PublicAPI]
    public abstract class BaseQuickSlotComponent : IQuickSlotComponent
    {
        /// <summary>
        /// Dictionary mapping slot index to item entity.
        /// </summary>
        protected readonly Dictionary<int, IEntity> ItemSlots;

        /// <summary>
        /// Dictionary mapping slot index to ability ID (spell/feat/talent ID depending on engine).
        /// </summary>
        protected readonly Dictionary<int, int> AbilitySlots;

        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Gets the maximum number of quick slots (engine-specific).
        /// </summary>
        protected abstract int MaxQuickSlots { get; }

        /// <summary>
        /// Initializes a new instance of the base quick slot component.
        /// </summary>
        protected BaseQuickSlotComponent()
        {
            ItemSlots = new Dictionary<int, IEntity>();
            AbilitySlots = new Dictionary<int, int>();
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
            // Clear all slots when component is detached
            ItemSlots.Clear();
            AbilitySlots.Clear();
        }

        /// <summary>
        /// Gets the item in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <returns>The item entity if slot contains an item, or null if empty or ability.</returns>
        public virtual IEntity GetQuickSlotItem(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return null;
            }

            IEntity item;
            if (ItemSlots.TryGetValue(slot, out item))
            {
                return item;
            }

            return null;
        }

        /// <summary>
        /// Gets the ability ID in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <returns>The ability ID if slot contains an ability, or -1 if empty or item.</returns>
        public virtual int GetQuickSlotAbility(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return -1;
            }

            int abilityId;
            if (AbilitySlots.TryGetValue(slot, out abilityId))
            {
                return abilityId;
            }

            return -1;
        }

        /// <summary>
        /// Gets the type of content in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <returns>0 for item, 1 for ability, -1 for empty.</returns>
        public virtual int GetQuickSlotType(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return -1;
            }

            if (ItemSlots.ContainsKey(slot))
            {
                return 0; // QUICKSLOT_TYPE_ITEM
            }

            if (AbilitySlots.ContainsKey(slot))
            {
                return 1; // QUICKSLOT_TYPE_ABILITY
            }

            return -1; // Empty
        }

        /// <summary>
        /// Sets an item in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <param name="item">The item entity to assign, or null to clear.</param>
        public virtual void SetQuickSlotItem(int slot, IEntity item)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return;
            }

            // Clear ability from this slot if present
            AbilitySlots.Remove(slot);

            if (item == null)
            {
                // Clear item slot
                ItemSlots.Remove(slot);
            }
            else
            {
                // Set item slot
                ItemSlots[slot] = item;
            }
        }

        /// <summary>
        /// Sets an ability in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <param name="abilityId">The ability ID to assign, or -1 to clear.</param>
        public virtual void SetQuickSlotAbility(int slot, int abilityId)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return;
            }

            // Clear item from this slot if present
            ItemSlots.Remove(slot);

            if (abilityId < 0)
            {
                // Clear ability slot
                AbilitySlots.Remove(slot);
            }
            else
            {
                // Set ability slot
                AbilitySlots[slot] = abilityId;
            }
        }

        /// <summary>
        /// Clears all quick slots.
        /// </summary>
        public virtual void ClearAllSlots()
        {
            ItemSlots.Clear();
            AbilitySlots.Clear();
        }

        /// <summary>
        /// Gets the number of occupied quick slots.
        /// </summary>
        public virtual int OccupiedSlotCount
        {
            get
            {
                HashSet<int> occupiedSlots = new HashSet<int>();
                foreach (int slot in ItemSlots.Keys)
                {
                    occupiedSlots.Add(slot);
                }
                foreach (int slot in AbilitySlots.Keys)
                {
                    occupiedSlots.Add(slot);
                }
                return occupiedSlots.Count;
            }
        }

        /// <summary>
        /// Checks if a slot is empty.
        /// </summary>
        /// <param name="slot">Quick slot index.</param>
        /// <returns>True if slot is empty, false otherwise.</returns>
        public virtual bool IsSlotEmpty(int slot)
        {
            if (slot < 0 || slot >= MaxQuickSlots)
            {
                return true;
            }

            return !ItemSlots.ContainsKey(slot) && !AbilitySlots.ContainsKey(slot);
        }
    }
}

