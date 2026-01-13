using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey engine implementation of quick slot component.
    /// </summary>
    /// <remarks>
    /// Odyssey Quick Slot Component:
    /// - Based on swkotor.exe and swkotor2.exe quick slot system
    /// - Located via string references: Quick slot system stores items/abilities for quick use
    /// - Quick slots: 0-11 (12 slots total) for storing items or abilities (spells/feats)
    /// - Quick slot types: QUICKSLOT_TYPE_ITEM (0), QUICKSLOT_TYPE_ABILITY (1)
    /// - Original implementation: Quick slots stored in creature GFF data (QuickSlot_* fields in UTC)
    /// - Quick slot storage: 0x005226d0 @ 0x005226d0 saves QuickSlot_* fields to creature GFF, 0x005223a0 @ 0x005223a0 loads QuickSlot_* fields from creature GFF
    /// - Quick slot fields: QuickSlot_0 through QuickSlot_11 (12 fields total) in UTC GFF format
    /// - Each QuickSlot_* field contains: Type (0=item, 1=ability), Item/ObjectId (for items), AbilityID (for abilities)
    /// - Quick slot usage: Using a slot triggers ActionUseItem (for items) or ActionCastSpellAtObject (for abilities)
    /// - swkotor.exe: Quick slot system identical to swkotor2.exe (function addresses to be determined via Ghidra)
    /// - swkotor2.exe: Enhanced quick slot system with 12 slots (0x005226d0 @ 0x005226d0 saves, 0x005223a0 @ 0x005223a0 loads)
    /// </remarks>
    public class OdysseyQuickSlotComponent : BaseQuickSlotComponent
    {
        private const int OdysseyMaxQuickSlots = 12; // 0-11

        /// <summary>
        /// Gets the maximum number of quick slots for Odyssey engine (12 slots).
        /// </summary>
        protected override int MaxQuickSlots
        {
            get { return OdysseyMaxQuickSlots; }
        }

        /// <summary>
        /// Initializes a new instance of the Odyssey quick slot component.
        /// </summary>
        /// <param name="owner">The entity this component is attached to.</param>
        public OdysseyQuickSlotComponent([NotNull] IEntity owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }

            Owner = owner;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            // Odyssey-specific initialization if needed
            base.OnAttach();
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Odyssey-specific cleanup if needed
            base.OnDetach();
        }
    }
}

