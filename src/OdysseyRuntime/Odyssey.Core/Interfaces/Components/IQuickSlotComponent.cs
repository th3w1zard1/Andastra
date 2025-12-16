using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for managing quick slot assignments (items and abilities).
    /// </summary>
    /// <remarks>
    /// Quick Slot Component Interface:
    /// - Based on swkotor2.exe quick slot system
    /// - Located via string references: Quick slot system stores items/abilities for quick use
    /// - Quick slots: 0-11 (12 slots total) for storing items or abilities (spells/feats)
    /// - Quick slot types: QUICKSLOT_TYPE_ITEM (0), QUICKSLOT_TYPE_ABILITY (1)
    /// - GetQuickSlot: Retrieves item entity or ability ID in specified slot
    /// - SetQuickSlot: Assigns item entity or ability ID to slot
    /// - GetQuickSlotType: Returns type of content in slot (item vs ability)
    /// - Original implementation: Quick slots stored in creature GFF data (QuickSlot_* fields)
    /// - Quick slot usage: Using a slot triggers ActionUseItem (for items) or ActionCastSpellAtObject (for abilities)
    /// </remarks>
    public interface IQuickSlotComponent : IComponent
    {
        /// <summary>
        /// Gets the item or ability in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index (0-11).</param>
        /// <returns>The item entity if slot contains an item, or null if empty or ability.</returns>
        IEntity GetQuickSlotItem(int slot);

        /// <summary>
        /// Gets the ability ID (spell/feat) in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index (0-11).</param>
        /// <returns>The ability ID if slot contains an ability, or -1 if empty or item.</returns>
        int GetQuickSlotAbility(int slot);

        /// <summary>
        /// Gets the type of content in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index (0-11).</param>
        /// <returns>0 for item, 1 for ability, -1 for empty.</returns>
        int GetQuickSlotType(int slot);

        /// <summary>
        /// Sets an item in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index (0-11).</param>
        /// <param name="item">The item entity to assign, or null to clear.</param>
        void SetQuickSlotItem(int slot, IEntity item);

        /// <summary>
        /// Sets an ability (spell/feat) in the specified quick slot.
        /// </summary>
        /// <param name="slot">Quick slot index (0-11).</param>
        /// <param name="abilityId">The ability ID to assign, or -1 to clear.</param>
        void SetQuickSlotAbility(int slot, int abilityId);
    }
}

