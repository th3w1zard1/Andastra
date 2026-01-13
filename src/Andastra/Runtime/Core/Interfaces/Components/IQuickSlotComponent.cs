using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for managing quick slot assignments (items and abilities).
    /// </summary>
    /// <remarks>
    /// Quick Slot Component Interface:
    /// - Common interface for quick slot components across all BioWare engines
    /// - Base implementation: BaseQuickSlotComponent (Runtime.Games.Common.Components)
    /// - Engine-specific implementations:
    ///   - Odyssey: QuickSlotComponent → OdysseyQuickSlotComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraQuickSlotComponent (nwmain.exe, nwn2main.exe)
    ///   - Eclipse: EclipseQuickSlotComponent (daorigins.exe, DragonAge2.exe)
    /// - Cross-engine analysis completed for all engines
    /// - Common functionality: Item slots, Ability slots, Slot type checking, Get/Set operations
    /// - Engine-specific details are in subclasses (number of slots, GFF field names, serialization formats, function addresses, ability ID formats)
    ///
    /// Cross-engine analysis:
    /// - Odyssey (swkotor.exe, swkotor2.exe): 12 slots (0-11), QuickSlot_* fields in UTC GFF, types: 0=item, 1=ability
    ///   - swkotor.exe: Quick slot system (function addresses to be determined via Ghidra)
    ///   - swkotor2.exe: 0x005226d0 @ 0x005226d0 saves QuickSlot_* fields, 0x005223a0 @ 0x005223a0 loads QuickSlot_* fields
    /// - Aurora (nwmain.exe, nwn2main.exe): 36 slots, QuickBar list in UTC GFF, QBObjectType field (0=empty, 1=item, 2=spell, 4=feat, etc.)
    ///   - nwmain.exe: CNWSCreature::SaveQuickBar, CNWSCreature::LoadQuickBar (function addresses to be determined via Ghidra)
    ///   - nwn2main.exe: Enhanced quick bar system (function addresses to be determined via Ghidra)
    /// - Eclipse (daorigins.exe, DragonAge2.exe): 24 slots, Quickslots/Quickslots1-4 fields in save game GFF, types: 0=item, 1=ability/talent
    ///   - daorigins.exe: Quick slot system with talents (function addresses to be determined via Ghidra)
    ///   - DragonAge2.exe: Enhanced quick slot system (function addresses to be determined via Ghidra)
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

