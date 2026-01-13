using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific door component implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Door Component:
    /// - Inherits from BaseDoorComponent for common door functionality
    /// - Aurora-specific: CNWSDoor class, GFF field names, transition system
    /// - Based on nwmain.exe door system (Neverwinter Nights)
    /// - Original implementation: CNWSDoor class in nwmain.exe
    /// - LoadDoor @ 0x1404208a0 (nwmain.exe: load door data from GFF)
    ///   - Loads Appearance, GenericType_New/GenericType, AnimationState (OpenState), AutoRemoveKey, Bearing
    ///   - Loads Faction, Fort, Will, Ref (saves), HP, CurrentHP, Invulnerable/Plot, KeyName
    /// - SaveDoor @ 0x1404228e0 (nwmain.exe: save door data to GFF)
    ///   - Saves Appearance, GenericType_New, AnimationState, AutoRemoveKey, Bearing, X/Y/Z position
    ///   - Saves Faction, Fort, Will, Ref, HP, CurrentHP, Plot, KeyName, KeyRequired, OpenLockDC, CloseLockDC, SecretDoorDC
    ///   - Saves Tag, TemplateResRef, Conversation, Portrait/PortraitId, Hardness, Useable
    ///   - Saves script hooks (OnClosed, OnDamaged, OnDeath, OnDisarm, OnHeartbeat, OnLock, OnMeleeAttacked, OnOpen, OnSpellCastAt, OnTrapTriggered)
    /// - LoadDoors @ 0x1403608f0 (nwmain.exe: load door list from area GIT)
    /// - SaveDoors @ 0x140365b50 (nwmain.exe: save door list to area GIT)
    /// - CNWSDoor constructor @ 0x14041d6b0 (nwmain.exe: create door instance)
    /// - Doors have open/closed states, locks, traps, transitions
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath (fired via EventBus)
    /// - Door locking: KeyName field required to unlock, or OpenLockDC/CloseLockDC set for lockpicking
    /// - Door HP: Doors can be destroyed (CurrentHP <= 0), have Hardness (damage reduction), saves (Fort/Reflex/Will)
    /// - Secret doors: SecretDoorDC determines detection difficulty for hidden doors
    /// </remarks>
    public class AuroraDoorComponent : BaseDoorComponent
    {
        /// <summary>
        /// Linked flags for transitions (Aurora-specific implementation).
        /// </summary>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        /// <remarks>
        /// Module Transition Check:
        /// - Based on nwmain.exe door transition system
        /// - Aurora engine uses different transition system than Odyssey
        /// - Implementation details: Check LinkedToFlags and LinkedToModule
        /// - Original implementation: CNWSDoor transition handling in nwmain.exe
        /// </remarks>
        public override bool IsModuleTransition
        {
            get { return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        /// <remarks>
        /// Area Transition Check:
        /// - Based on nwmain.exe door transition system
        /// - Aurora engine uses different transition system than Odyssey
        /// - Implementation details: Check LinkedToFlags and LinkedTo
        /// - Original implementation: CNWSDoor transition handling in nwmain.exe
        /// </remarks>
        public override bool IsAreaTransition
        {
            get { return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo); }
        }
    }
}

