using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;
using Andastra.Runtime.Games.Odyssey.Components;

namespace Andastra.Runtime.Games.Odyssey.Kotor1.Components
{
    /// <summary>
    /// KOTOR 1 (swkotor.exe) specific door component implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Door Component Implementation:
    /// - Game-specific implementation of Odyssey door system for Star Wars: Knights of the Old Republic
    /// - Inherits common Odyssey door functionality from OdysseyDoorComponent
    /// - Based on reverse engineering of swkotor.exe door system functions
    /// 
    /// KOTOR 1 specific function addresses (swkotor.exe):
    /// - LoadDoorListFromGIT @ 0x0050a0e0 (FUN_0050a0e0) - loads door list from GIT file
    ///   - Reads door instances from module GIT file structure
    ///   - Loads door position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation)
    ///   - Loads door template ResRef, Tag, LinkedTo, LinkedToFlags, LinkedToModule, TransitionDestination
    ///   - Loads door state (open/closed, locked/unlocked), HP, trap data
    ///   - Called during module loading to populate door instances in area
    /// - SaveDoorListToGIT @ 0x00507810 (FUN_00507810) - saves door list to GIT file
    ///   - Writes door instances to module GIT file structure
    ///   - Saves door position, orientation, template ResRef, Tag, transition data
    ///   - Saves door state (open/closed, locked/unlocked), HP, trap data
    ///   - Called during module saving to persist door states
    /// - DoorEventHandling @ 0x004dcfb0 (FUN_004dcfb0) - handles door events including transitions
    ///   - Processes door interaction events (open, close, lock, unlock, transition)
    ///   - Handles module transitions: checks LinkedToFlags bit 1 (0x1) and LinkedToModule field
    ///   - Handles area transitions: checks LinkedToFlags bit 2 (0x2) and LinkedTo field
    ///   - Fires script events: OnOpen, OnClose, OnLock, OnUnlock, OnClick, OnTrapTriggered
    ///   - Executes transition logic: loads new module or moves party to waypoint/trigger
    ///   - Called when player interacts with door or door state changes
    /// 
    /// KOTOR 1 door property loading from UTD templates:
    /// - Note: swkotor.exe uses identical UTD template structure and transition flag system as swkotor2.exe
    /// - Exact function addresses for door property loading from UTD templates in swkotor.exe need verification via Ghidra MCP
    /// - UTD template fields are identical to swkotor2.exe:
    ///   - PortraitId/Portrait, CreatorId, script hooks (ScriptHeartbeat, ScriptOnEnter, ScriptOnExit, ScriptUserDefine, OnTrapTriggered, OnDisarm, OnClick)
    ///   - TrapType, TrapOneShot, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, KeyName
    ///   - TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, DisarmDCMod, DetectDCMod, Cursor, TransitionDestination, Type, HighlightHeight
    ///   - Position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation), Geometry polygon vertices
    ///   - LoadScreenID, SetByPlayerParty
    /// 
    /// KOTOR 1 specific string references (swkotor.exe):
    /// - String addresses need verification via Ghidra MCP for exact locations
    /// - Expected strings (based on swkotor2.exe pattern):
    ///   - "Door List" - GIT door list structure identifier
    ///   - "GenericDoors" - generic doors 2DA table reference
    ///   - "DoorTypes" - door types field identifier
    ///   - "SecretDoorDC" - secret door DC field identifier
    ///   - "LinkedTo" - linked to waypoint/area field
    ///   - "LinkedToModule" - linked to module field
    ///   - "LinkedToFlags" - transition flags field
    ///   - "TransitionDestination" - waypoint tag for positioning after transition
    ///   - "OnLock" - door lock event identifier
    ///   - "EVENT_LOCK_OBJECT" - lock object event type
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" - script event type for locked door
    ///   - "gui_mp_doordp", "gui_mp_doorup", "gui_mp_doord", "gui_mp_dooru" - door GUI panels
    ///   - "gui_doorsaber" - saber door GUI
    ///   - "gui_mp_bashdp", "gui_mp_bashup", "gui_mp_bashd", "gui_mp_bashu" - door bash GUI panels
    ///   - "Cannot load door model '%s'." - door model loading error message
    ///   - "CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d" - door animation name error message
    /// 
    /// KOTOR 1 specific behavior:
    /// - Door transition system: Identical to swkotor2.exe (LinkedToFlags bit 1 = module transition, bit 2 = area transition)
    /// - Door locking: Identical to swkotor2.exe (KeyName field or LockDC for lockpicking)
    /// - Door bashing: Identical to swkotor2.exe (damage minus hardness, destroys at 0 HP)
    /// - Door traps: Identical to swkotor2.exe (TrapType, TrapDetectable, TrapDisarmable, TrapOneShot)
    /// - Door animations: Identical to swkotor2.exe ("i_opendoor", "i_doorsaber")
    /// - Script events: Identical to swkotor2.exe (OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath)
    /// 
    /// Differences from swkotor2.exe:
    /// - Different function addresses for GIT loading/saving (swkotor.exe uses FUN_0050a0e0/FUN_00507810 vs swkotor2.exe FUN_004e08e0)
    /// - Different function address for door event handling (swkotor.exe FUN_004dcfb0 vs swkotor2.exe FUN_004dcfb0 - same address but different executable)
    /// - Exact function addresses for UTD template property loading in swkotor.exe need verification (swkotor2.exe uses FUN_00584f40/FUN_00585ec0)
    /// </remarks>
    public class Kotor1DoorComponent : OdysseyDoorComponent
    {
        /// <summary>
        /// Initializes a new instance of the Kotor1DoorComponent class.
        /// </summary>
        public Kotor1DoorComponent()
            : base()
        {
        }

        /// <summary>
        /// Whether this door is a module transition (KOTOR 1 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 1 Module Transition Check:
        /// - Based on swkotor.exe door transition system
        /// - swkotor.exe: Door list loading (LoadDoorListFromGIT @ 0x0050a0e0 loads door list from GIT)
        /// - swkotor.exe: Door event handling (DoorEventHandling @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedToModule, LinkedToFlags) as swkotor2.exe
        /// - Original implementation: LinkedToFlags bit 1 (0x1) = module transition flag
        /// - Module transition: If LinkedToFlags & 1 != 0 and LinkedToModule is non-empty, door triggers module transition
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns in new module
        /// - Function: DoorEventHandling @ 0x004dcfb0 checks LinkedToFlags bit 1 and LinkedToModule field to determine if door is module transition
        /// </remarks>
        public override bool IsModuleTransition
        {
            get
            {
                // KOTOR 1 uses identical transition flag system to swkotor2.exe
                // LinkedToFlags bit 1 (0x1) = module transition flag
                // Based on swkotor.exe: DoorEventHandling @ 0x004dcfb0
                return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule);
            }
        }

        /// <summary>
        /// Whether this door is an area transition (KOTOR 1 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 1 Area Transition Check:
        /// - Based on swkotor.exe door transition system
        /// - swkotor.exe: Door list loading (LoadDoorListFromGIT @ 0x0050a0e0 loads door list from GIT)
        /// - swkotor.exe: Door event handling (DoorEventHandling @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedTo, LinkedToFlags, TransitionDestination) as swkotor2.exe
        /// - Original implementation: LinkedToFlags bit 2 (0x2) = area transition flag
        /// - Area transition: If LinkedToFlags & 2 != 0 and LinkedTo is non-empty, door triggers area transition within module
        /// - LinkedTo: Waypoint or trigger tag to transition to (within current module)
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns after transition
        /// - Function: DoorEventHandling @ 0x004dcfb0 checks LinkedToFlags bit 2 and LinkedTo field to determine if door is area transition
        /// </remarks>
        public override bool IsAreaTransition
        {
            get
            {
                // KOTOR 1 uses identical transition flag system to swkotor2.exe
                // LinkedToFlags bit 2 (0x2) = area transition flag
                // Based on swkotor.exe: DoorEventHandling @ 0x004dcfb0
                return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo);
            }
        }

        /// <summary>
        /// Locks the door (KOTOR 1 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 1 Door Locking:
        /// - Based on swkotor.exe door locking system
        /// - swkotor.exe: Door event handling (DoorEventHandling @ 0x004dcfb0 handles lock events)
        /// - Original implementation: Sets IsLocked flag to true, fires OnLock script event
        /// - Lock validation: Only locks if Lockable flag is true (from UTD template)
        /// - Script execution: OnLock script (ScriptOnLock field in UTD template) executes after door is locked
        /// - Function: DoorEventHandling @ 0x004dcfb0 processes EVENT_LOCK_OBJECT events
        /// </remarks>
        public override void Lock()
        {
            // KOTOR 1 uses identical locking system to swkotor2.exe
            // Based on swkotor.exe: DoorEventHandling @ 0x004dcfb0
            base.Lock();
        }

        /// <summary>
        /// Applies damage to the door (KOTOR 1 specific implementation for bashing).
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <remarks>
        /// KOTOR 1 Door Bashing:
        /// - Based on swkotor.exe door bashing system
        /// - Original implementation: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
        /// - Bash damage: Strength modifier + weapon damage (if weapon equipped) vs door Hardness
        /// - Door destruction: When CurrentHP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
        /// - Open state: Set to 2 (destroyed state) when door is bashed open
        /// - Function: DoorEventHandling @ 0x004dcfb0 processes door bash damage events
        /// </remarks>
        public override void ApplyDamage(int damage)
        {
            // KOTOR 1 uses identical bashing system to swkotor2.exe
            // Based on swkotor.exe: DoorEventHandling @ 0x004dcfb0
            base.ApplyDamage(damage);
        }
    }
}
