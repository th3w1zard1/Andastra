using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine implementation of quick slot component.
    /// </summary>
    /// <remarks>
    /// Eclipse Quick Slot Component:
    /// - Based on daorigins.exe and DragonAge2.exe quick slot system
    /// - Located via string references: Quickslots system stores items/abilities for quick use
    /// - Quick slots: Variable number of slots (typically 12-24 slots depending on game version)
    /// - Quick slot types: Similar to Odyssey (0=item, 1=ability/talent)
    /// - Original implementation: Quick slots stored in save game GFF data (Quickslots, Quickslots1-4, QuickslotAbility, QuickslotItemTag fields in GFF)
    /// - Quick slot storage: Save game serialization functions (function addresses to be determined via Ghidra)
    /// - Quick slot fields: Quickslots, Quickslots1, Quickslots2, Quickslots3, Quickslots4 in save game GFF format
    /// - Each quickslot entry contains: Ability ID (for talents/spells), Item Tag (for items)
    /// - Quick slot usage: Using a slot triggers appropriate action (ActionUseItem for items, ActionCastSpell/UseTalent for abilities)
    /// - daorigins.exe: Eclipse quick slot system with talents and items (function addresses to be determined via Ghidra)
    /// - DragonAge2.exe: Enhanced Eclipse quick slot system with additional features (function addresses to be determined via Ghidra)
    /// - Eclipse uses talents instead of feats/spells (talent system similar to Odyssey's force powers)
    /// - Eclipse supports multiple quick bar pages (Quickslots1-4) unlike Odyssey's single bar
    /// </remarks>
    public class EclipseQuickSlotComponent : BaseQuickSlotComponent
    {
        private const int EclipseMaxQuickSlots = 24; // Eclipse typically supports more slots than Odyssey

        /// <summary>
        /// Gets the maximum number of quick slots for Eclipse engine (24 slots).
        /// </summary>
        protected override int MaxQuickSlots
        {
            get { return EclipseMaxQuickSlots; }
        }

        /// <summary>
        /// Initializes a new instance of the Eclipse quick slot component.
        /// </summary>
        /// <param name="owner">The entity this component is attached to.</param>
        public EclipseQuickSlotComponent([NotNull] IEntity owner)
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
            // Eclipse-specific initialization if needed
            base.OnAttach();
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Eclipse-specific cleanup if needed
            base.OnDetach();
        }
    }
}

