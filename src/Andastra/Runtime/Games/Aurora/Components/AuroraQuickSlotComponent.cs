using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine implementation of quick slot component.
    /// </summary>
    /// <remarks>
    /// Aurora Quick Slot Component:
    /// - Based on nwmain.exe and nwn2main.exe quick bar system
    /// - Located via string references: QuickBar system stores items/abilities for quick use
    /// - Quick bar slots: Variable number of slots (typically 12-36 slots depending on game version)
    /// - Quick bar object types: QBObjectType field in GFF (0=empty, 1=item, 2=spell, 3=skill, 4=feat, 5=script, 6=dialog, 7=attack, 8=emote, etc.)
    /// - Original implementation: Quick bar stored in creature GFF data (QuickBar list in UTC/GFF)
    /// - Quick bar storage: CNWSCreature::SaveQuickBar @ nwmain.exe (function address to be determined via Ghidra), CNWSCreature::LoadQuickBar @ nwmain.exe (function address to be determined via Ghidra)
    /// - Quick bar fields: QuickBar list in UTC GFF format, each entry contains QBObjectType, QBItemInvSlot (for items), QBINTParam1 (for spells/feats)
    /// - Each QuickBar entry contains: QBObjectType (0=empty, 1=item, 2=spell, 4=feat), QBItemInvSlot (object ID for items), QBINTParam1 (spell/feat index for abilities)
    /// - Quick bar usage: Using a slot triggers appropriate action (ActionUseItem for items, ActionCastSpell for spells, ActionUseFeat for feats)
    /// - nwmain.exe: Aurora quick bar system with QBObjectType field (function addresses to be determined via Ghidra)
    /// - nwn2main.exe: Enhanced Aurora quick bar system with additional object types (function addresses to be determined via Ghidra)
    /// - Aurora uses different object type codes than Odyssey (1=item, 2=spell, 4=feat vs Odyssey's 0=item, 1=ability)
    /// - Aurora supports more object types than Odyssey (scripts, dialogs, attacks, emotes, etc.)
    /// </remarks>
    public class AuroraQuickSlotComponent : BaseQuickSlotComponent
    {
        private const int AuroraMaxQuickSlots = 36; // Aurora typically supports more slots than Odyssey

        /// <summary>
        /// Gets the maximum number of quick slots for Aurora engine (36 slots).
        /// </summary>
        protected override int MaxQuickSlots
        {
            get { return AuroraMaxQuickSlots; }
        }

        /// <summary>
        /// Initializes a new instance of the Aurora quick slot component.
        /// </summary>
        /// <param name="owner">The entity this component is attached to.</param>
        public AuroraQuickSlotComponent([NotNull] IEntity owner)
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
            // Aurora-specific initialization if needed
            base.OnAttach();
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Aurora-specific cleanup if needed
            base.OnDetach();
        }
    }
}

