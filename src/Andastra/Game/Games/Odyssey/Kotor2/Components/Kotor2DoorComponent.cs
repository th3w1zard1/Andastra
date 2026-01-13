using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;
using Andastra.Game.Games.Odyssey.Components;

namespace Andastra.Game.Games.Odyssey.Kotor2.Components
{
    /// <summary>
    /// KOTOR 2 (swkotor2.exe) specific door component implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Door Component Implementation:
    /// - Game-specific implementation of Odyssey door system for Star Wars: Knights of the Old Republic II - The Sith Lords
    /// - Inherits common Odyssey door functionality from OdysseyDoorComponent
    /// - Based on reverse engineering of swkotor2.exe door system functions
    /// 
    /// KOTOR 2 specific function addresses (swkotor2.exe):
    /// - LoadDoorDataFromUTD @ 0x00584f40 (FUN_00584f40) - loads door data from GFF/UTD template
    ///   - Loads PortraitId/Portrait, CreatorId, script hooks (ScriptHeartbeat, ScriptOnEnter, ScriptOnExit, ScriptUserDefine, OnTrapTriggered, OnDisarm, OnClick)
    ///   - Loads TrapType, TrapOneShot, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, KeyName
    ///   - Loads TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, DisarmDCMod, DetectDCMod, Cursor, TransitionDestination, Type, HighlightHeight
    ///   - Loads position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation), Geometry polygon vertices
    ///   - Loads LoadScreenID, SetByPlayerParty
    ///   - Geometry vertices are transformed by door position/orientation (relative to door transform)
    ///   - Called during door template loading to populate door properties from UTD file
    /// - SaveDoorDataToUTD @ 0x00585ec0 (FUN_00585ec0) - saves door data to GFF/UTD template
    ///   - Saves script hooks (ScriptHeartbeat, ScriptOnEnter, ScriptOnExit, ScriptUserDefine, OnTrapTriggered, OnDisarm, OnClick)
    ///   - Saves TrapType, TrapOneShot, CreatorId, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, Cursor, KeyName
    ///   - Saves TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, PortraitId/Portrait, Type, HighlightHeight
    ///   - Saves position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation)
    ///   - Saves Geometry polygon vertices (PointX, PointY, PointZ) relative to door position
    ///   - Saves LoadScreenID, TransitionDestination, SetByPlayerParty
    ///   - Called during door template saving to persist door properties to UTD file
    /// - LoadDoorInstancesFromGIT @ 0x004e08e0 (FUN_004e08e0) - loads door instances from GIT including position, linked transitions
    ///   - Reads door instances from module GIT file structure
    ///   - Loads door position, orientation, template ResRef, Tag, transition data
    ///   - Loads door state (open/closed, locked/unlocked), HP, trap data
    ///   - Called during module loading to populate door instances in area
    /// - DoorLoadingFunction @ 0x00580ed0 (FUN_00580ed0) - door loading function
    ///   - Main door loading entry point
    ///   - Coordinates loading of door template (UTD) and door instance (GIT) data
    ///   - Initializes door component with loaded data
    ///   - Called during module/area loading to create door entities
    /// - DoorInitialization @ 0x005838d0 (FUN_005838d0) - door initialization
    ///   - Initializes door component after template and instance data are loaded
    ///   - Reads LinkedToModule and LinkedToFlags from UTD template
    ///   - Reads LinkedTo and LinkedToFlags from UTD template
    ///   - Sets up door state, locks, traps, transitions
    ///   - Called after door data is loaded to finalize door setup
    /// - DoorEventHandling @ 0x004dcfb0 (FUN_004dcfb0) - handles door events including transitions
    ///   - Processes door interaction events (open, close, lock, unlock, transition)
    ///   - Handles module transitions: checks LinkedToFlags bit 1 (0x1) and LinkedToModule field
    ///   - Handles area transitions: checks LinkedToFlags bit 2 (0x2) and LinkedTo field
    ///   - Fires script events: OnOpen, OnClose, OnLock, OnUnlock, OnClick, OnTrapTriggered
    ///   - Executes transition logic: loads new module or moves party to waypoint/trigger
    ///   - Called when player interacts with door or door state changes
    ///   - Case 0xd: EVENT_LOCK_OBJECT (fires before script execution)
    /// 
    /// KOTOR 2 specific string references (swkotor2.exe):
    /// - "Door List" @ 0x007bd248 - GIT door list structure identifier
    /// - "GenericDoors" @ 0x007c4ba8 - generic doors 2DA table reference
    /// - "DoorTypes" @ 0x007c4b9c - door types field identifier
    /// - "SecretDoorDC" @ 0x007c1acc - secret door DC field identifier
    /// - "LinkedTo" @ 0x007bd798 - linked to waypoint/area field
    /// - "LinkedToModule" @ 0x007bd7bc - linked to module field
    /// - "LinkedToFlags" @ 0x007bd788 - transition flags field
    /// - "TransitionDestination" @ 0x007bd7a4 - waypoint tag for positioning after transition
    /// - "OnLock" @ 0x007c1a28 - door lock event identifier
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20 - lock object event type (case 0xd in FUN_004dcfb0)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 - script event type for locked door (0x1c)
    /// - "gui_mp_doordp" @ 0x007b5bdc - door GUI panel (down pressed)
    /// - "gui_mp_doorup" @ 0x007b5bec - door GUI panel (up)
    /// - "gui_mp_doord" @ 0x007b5d24 - door GUI panel (down)
    /// - "gui_mp_dooru" @ 0x007b5d34 - door GUI panel (up)
    /// - "gui_doorsaber" @ 0x007c2fe4 - saber door GUI
    /// - "gui_mp_bashdp" @ 0x007b5e04 - door bash GUI panel (down pressed)
    /// - "gui_mp_bashup" @ 0x007b5e14 - door bash GUI panel (up)
    /// - "gui_mp_bashd" @ 0x007b5e24 - door bash GUI panel (down)
    /// - "gui_mp_bashu" @ 0x007b5e34 - door bash GUI panel (up)
    /// - "i_opendoor" @ 0x007c86d4 - open door animation
    /// - "i_doorsaber" @ 0x007ccca0 - saber door animation
    /// - "Cannot load door model '%s'." @ 0x007d2488 - door model loading error message
    /// - "CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d" @ 0x007d24a8 - door animation name error message
    /// 
    /// KOTOR 2 specific behavior:
    /// - Door transition system: LinkedToFlags bit 1 (0x1) = module transition, bit 2 (0x2) = area transition
    /// - Door locking: KeyName field (item ResRef) required to unlock, or LockDC set for lockpicking
    /// - Door bashing: Damage minus hardness, destroys at 0 HP (minimum 1 damage per hit)
    /// - Door traps: TrapType, TrapDetectable, TrapDisarmable, TrapOneShot, OwnerDemolitionsSkill, DisarmDCMod, DetectDCMod
    /// - Door animations: "i_opendoor" (open door), "i_doorsaber" (saber door)
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath (fired via EventBus)
    /// - Module transitions: LinkedToModule + LinkedToFlags bit 1 = module transition (loads new module)
    /// - Area transitions: LinkedToFlags bit 2 = area transition (moves to waypoint/trigger in current module)
    /// - Door HP: Doors can be destroyed (CurrentHP <= 0), have Hardness (damage reduction), saves (Fort/Reflex/Will)
    /// - Secret doors: SecretDoorDC determines detection difficulty for hidden doors
    /// 
    /// Differences from swkotor.exe (KOTOR 1):
    /// - Different function addresses for UTD template loading/saving (swkotor2.exe uses FUN_00584f40/FUN_00585ec0)
    /// - Different function address for GIT loading (swkotor2.exe uses FUN_004e08e0 vs swkotor.exe FUN_0050a0e0)
    /// - Additional door loading functions (DoorLoadingFunction @ 0x00580ed0, DoorInitialization @ 0x005838d0)
    /// - Same door event handling function address (FUN_004dcfb0) but in different executable
    /// - Identical UTD template structure and transition flag system
    /// </remarks>
    public class Kotor2DoorComponent : OdysseyDoorComponent
    {
        /// <summary>
        /// Initializes a new instance of the Kotor2DoorComponent class.
        /// </summary>
        /// <remarks>
        /// TSL Door Initialization:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door system
        /// - TSL-specific fields: Min1HP, NotBlastable (loaded from UTD template)
        /// - These fields are TSL-only and do not exist in KOTOR 1
        /// - swkotor2.exe: FUN_00584f40 @ 0x00584f40 loads Min1HP and NotBlastable from UTD template
        /// </remarks>
        public Kotor2DoorComponent()
            : base()
        {
            // TSL specific initialization
            // Min1HP and NotBlastable default to false (will be loaded from UTD template if present)
            Min1HP = false;
            NotBlastable = false;
        }

        /// <summary>
        /// Whether this door is a module transition (KOTOR 2 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 2 Module Transition Check:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door transition system
        /// - swkotor2.exe: Door loading (DoorInitialization @ 0x005838d0 reads LinkedToModule and LinkedToFlags from UTD template)
        /// - swkotor2.exe: Door loading (DoorLoadingFunction @ 0x00580ed0 loads door properties including transition data)
        /// - swkotor2.exe: GIT loading (LoadDoorInstancesFromGIT @ 0x004e08e0 loads door instances from GIT with transition fields)
        /// - swkotor2.exe: Door event handling (DoorEventHandling @ 0x004dcfb0 handles door events including transitions)
        /// - Located via string references: "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
        /// - Original implementation: LinkedToFlags bit 1 (0x1) = module transition flag
        /// - Module transition: If LinkedToFlags & 1 != 0 and LinkedToModule is non-empty, door triggers module transition
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns in new module
        /// - Function: DoorInitialization @ 0x005838d0 reads LinkedToModule and LinkedToFlags from UTD template
        /// - Function: DoorEventHandling @ 0x004dcfb0 checks LinkedToFlags bit 1 and LinkedToModule field to determine if door is module transition
        /// </remarks>
        public override bool IsModuleTransition
        {
            get
            {
                // KOTOR 2 uses LinkedToFlags bit 1 (0x1) = module transition flag
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DoorInitialization @ 0x005838d0, DoorEventHandling @ 0x004dcfb0
                return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule);
            }
        }

        /// <summary>
        /// Whether this door is an area transition (KOTOR 2 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 2 Area Transition Check:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door transition system
        /// - swkotor2.exe: Door loading (DoorInitialization @ 0x005838d0 reads LinkedTo and LinkedToFlags from UTD template)
        /// - swkotor2.exe: Door loading (DoorLoadingFunction @ 0x00580ed0 loads door properties including transition data)
        /// - swkotor2.exe: GIT loading (LoadDoorInstancesFromGIT @ 0x004e08e0 loads door instances from GIT with transition fields)
        /// - swkotor2.exe: Door event handling (DoorEventHandling @ 0x004dcfb0 handles door events including transitions)
        /// - Located via string references: "LinkedTo" @ 0x007bd798, "LinkedToFlags" @ 0x007bd788, "TransitionDestination" @ 0x007bd7a4
        /// - Original implementation: LinkedToFlags bit 2 (0x2) = area transition flag
        /// - Area transition: If LinkedToFlags & 2 != 0 and LinkedTo is non-empty, door triggers area transition within module
        /// - LinkedTo: Waypoint or trigger tag to transition to (within current module)
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns after transition
        /// - Function: DoorInitialization @ 0x005838d0 reads LinkedTo and LinkedToFlags from UTD template
        /// - Function: DoorEventHandling @ 0x004dcfb0 checks LinkedToFlags bit 2 and LinkedTo field to determine if door is area transition
        /// </remarks>
        public override bool IsAreaTransition
        {
            get
            {
                // KOTOR 2 uses LinkedToFlags bit 2 (0x2) = area transition flag
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DoorInitialization @ 0x005838d0, DoorEventHandling @ 0x004dcfb0
                return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo);
            }
        }

        /// <summary>
        /// Locks the door (KOTOR 2 specific implementation).
        /// </summary>
        /// <remarks>
        /// KOTOR 2 Door Locking:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door locking system
        /// - Located via string references: "OnLock" @ 0x007c1a28, "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
        /// - Event dispatching: DoorEventHandling @ 0x004dcfb0 handles EVENT_LOCK_OBJECT (case 0xd, fires before script execution)
        /// - Original implementation: Sets IsLocked flag to true, fires OnLock script event
        /// - Lock validation: Only locks if Lockable flag is true (from UTD template)
        /// - Script execution: OnLock script (ScriptOnLock field in UTD template) executes after door is locked
        /// - Function: DoorEventHandling @ 0x004dcfb0 processes EVENT_LOCK_OBJECT events (case 0xd)
        /// </remarks>
        public override void Lock()
        {
            // KOTOR 2 uses DoorEventHandling @ 0x004dcfb0 for lock events
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DoorEventHandling @ 0x004dcfb0 (case 0xd: EVENT_LOCK_OBJECT)
            base.Lock();
        }

        /// <summary>
        /// Applies damage to the door with a specific damage type (KOTOR 2 specific implementation).
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <param name="damageType">The type of damage being applied.</param>
        /// <remarks>
        /// KOTOR 2 Door Damage Application:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door damage system
        /// - Located via string references: "gui_mp_bashdp" @ 0x007b5e04, "gui_mp_bashup" @ 0x007b5e14 (door bash GUI panels)
        /// - "gui_mp_bashd" @ 0x007b5e24, "gui_mp_bashu" @ 0x007b5e34 (door bash GUI elements)
        /// - swkotor2.exe: FUN_00584f40 @ 0x00584f40 loads Min1HP and NotBlastable from UTD template
        /// - TSL-specific behavior: Min1HP flag prevents door from dropping below 1 HP
        /// - TSL-specific behavior: NotBlastable flag prevents explosive/force power damage
        /// - Original implementation: If NotBlastable is true, door cannot be damaged by explosives, grenades, or force powers
        /// - Blast damage types: Fire (explosives/grenades), DarkSide (dark side force powers), LightSide (light side force powers)
        /// - If NotBlastable is true and damage type is blast-type, damage is rejected (no HP reduction)
        /// - Original implementation: If Min1HP is true, door HP is clamped to minimum of 1
        /// - Plot doors: Min1HP=1 makes door effectively indestructible (cannot be bashed open)
        /// - Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
        /// - Bash damage: Strength modifier + weapon damage (if weapon equipped) vs door Hardness
        /// - Door destruction: When CurrentHP <= 0 (and Min1HP is false), door is marked as bashed (IsBashed=true), unlocked, and opened
        /// - Open state: Set to 2 (destroyed state) when door is bashed open (only if Min1HP is false)
        /// - Function: DoorEventHandling @ 0x004dcfb0 processes door bash damage events
        /// </remarks>
        public override void ApplyDamage(int damage, Core.Combat.DamageType damageType)
        {
            if (damage <= 0)
            {
                return;
            }

            // TSL-specific: NotBlastable prevents explosive/force power damage
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00584f40 @ 0x00584f40 loads NotBlastable from UTD template
            // Original implementation: If NotBlastable is true, door cannot be damaged by explosives, grenades, or force powers
            // Blast damage types include: Fire (explosives/grenades), Sonic (sonic grenades), Electrical (electrical force powers),
            // DarkSide (dark side force powers), LightSide (light side force powers)
            // Physical damage (bashing/melee) is not blocked by NotBlastable flag
            // If NotBlastable is true and damage is blast-type, reject the damage completely
            if (NotBlastable)
            {
                // Check if damage type is blast-type (explosives/grenades/force powers)
                // Physical damage is explicitly allowed (bashing damage should always work)
                bool isBlastDamage = damageType == Core.Combat.DamageType.Fire ||  // Explosives/grenades
                                     damageType == Core.Combat.DamageType.Sonic ||  // Sonic grenades
                                     damageType == Core.Combat.DamageType.Electrical ||  // Electrical force powers
                                     damageType == Core.Combat.DamageType.DarkSide ||  // Dark side force powers
                                     damageType == Core.Combat.DamageType.LightSide;  // Light side force powers

                if (isBlastDamage)
                {
                    // Reject blast-type damage if NotBlastable flag is set
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NotBlastable flag prevents door from being damaged by blast-type damage
                    // Original implementation: Door takes no damage from explosives, grenades, or force powers when NotBlastable is true
                    // Physical damage (bashing) is not affected by NotBlastable flag
                    return;
                }
            }

            // Apply hardness reduction (minimum 1 damage)
            int actualDamage = System.Math.Max(1, damage - Hardness);
            int newHP = System.Math.Max(0, HitPoints - actualDamage);

            // TSL-specific: Min1HP prevents door from dropping below 1 HP
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00584f40 @ 0x00584f40 loads Min1HP from UTD template
            // Original implementation: If Min1HP is true, door cannot be destroyed (HP clamped to 1)
            // Plot doors: Min1HP=1 makes door effectively indestructible
            if (Min1HP && newHP < 1)
            {
                newHP = 1;
            }

            HitPoints = newHP;

            // If door is destroyed (and Min1HP is false), mark as bashed and open
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DoorEventHandling @ 0x004dcfb0 processes door bash damage events
            if (HitPoints <= 0)
            {
                IsBashed = true;
                IsLocked = false;
                IsOpen = true;
                OpenState = 2; // Destroyed state
            }
        }
    }
}
