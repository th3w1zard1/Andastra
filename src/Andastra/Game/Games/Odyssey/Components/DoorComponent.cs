using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey engine-specific door component implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Door Component:
    /// - Inherits from BaseDoorComponent for common door functionality
    /// - Odyssey-specific: UTD file format, GFF field names, transition flag system
    /// - Based on swkotor.exe and swkotor2.exe door systems
    /// - KOTOR 1 specific implementation: Kotor1DoorComponent (Runtime.Games.Odyssey.Kotor1.Components)
    /// - KOTOR 2 specific implementation: Kotor2DoorComponent (Runtime.Games.Odyssey.Kotor2.Components)
    /// - Game-specific implementations:
    ///   - KOTOR 1: Kotor1DoorComponent (Runtime.Games.Odyssey.Kotor1.Components) - swkotor.exe specific addresses and behaviors
    ///   - TSL: Kotor2DoorComponent (Runtime.Games.Odyssey.Kotor2.Components) - swkotor2.exe specific addresses and behaviors
    /// - Located via string references: "Door List" @ 0x007bd248 (swkotor2.exe GIT door list), "GenericDoors" @ 0x007c4ba8 (generic doors 2DA table)
    /// - "DoorTypes" @ 0x007c4b9c (door types field), "SecretDoorDC" @ 0x007c1acc (secret door DC field)
    /// - Transition fields: "LinkedTo" @ 0x007bd798 (linked to waypoint/area), "LinkedToModule" @ 0x007bd7bc (linked to module)
    /// - "LinkedToFlags" @ 0x007bd788 (transition flags), "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition)
    /// - Door animations: "i_opendoor" @ 0x007c86d4 (open door animation), "i_doorsaber" @ 0x007ccca0 (saber door animation)
    /// - GUI references: "gui_mp_doordp" @ 0x007b5bdc, "gui_mp_doorup" @ 0x007b5bec, "gui_mp_doord" @ 0x007b5d24, "gui_mp_dooru" @ 0x007b5d34 (door GUI panels)
    /// - "gui_doorsaber" @ 0x007c2fe4 (saber door GUI)
    /// - Error messages:
    ///   - "Cannot load door model '%s'." @ 0x007d2488 (door model loading error)
    ///   - "CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d" @ 0x007d24a8 (door animation name error)
    /// - swkotor2.exe: FUN_00584f40 @ 0x00584f40 (load door data from GFF/UTD template)
    ///   - Loads PortraitId/Portrait, CreatorId, script hooks (ScriptHeartbeat, ScriptOnEnter, ScriptOnExit, ScriptUserDefine, OnTrapTriggered, OnDisarm, OnClick)
    ///   - Loads TrapType, TrapOneShot, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, KeyName
    ///   - Loads TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, DisarmDCMod, DetectDCMod, Cursor, TransitionDestination, Type, HighlightHeight
    ///   - Loads position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation), Geometry polygon vertices
    ///   - Loads LoadScreenID, SetByPlayerParty
    ///   - Geometry vertices are transformed by door position/orientation (relative to door transform)
    /// - swkotor2.exe: FUN_00585ec0 @ 0x00585ec0 (save door data to GFF/UTD template)
    ///   - Saves script hooks (ScriptHeartbeat, ScriptOnEnter, ScriptOnExit, ScriptUserDefine, OnTrapTriggered, OnDisarm, OnClick)
    ///   - Saves TrapType, TrapOneShot, CreatorId, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, Cursor, KeyName
    ///   - Saves TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, PortraitId/Portrait, Type, HighlightHeight
    ///   - Saves position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation)
    ///   - Saves Geometry polygon vertices (PointX, PointY, PointZ) relative to door position
    ///   - Saves LoadScreenID, TransitionDestination, SetByPlayerParty
    /// - swkotor2.exe: FUN_004e08e0 @ 0x004e08e0 (load door instances from GIT including position, linked transitions)
    /// - swkotor2.exe: FUN_00580ed0 @ 0x00580ed0 (door loading function), FUN_005838d0 @ 0x005838d0 (door initialization)
    /// - swkotor.exe: FUN_0050a0e0 @ 0x0050a0e0 (load door list from GIT)
    /// - swkotor.exe: FUN_00507810 @ 0x00507810 (save door list to GIT)
    /// - swkotor.exe: FUN_004dcfb0 @ 0x004dcfb0 (door event handling, including transition events)
    /// - Note: swkotor.exe uses identical UTD template structure and transition flag system as swkotor2.exe; exact function addresses for door property loading from UTD templates in swkotor.exe need verification via Ghidra MCP
    /// - Doors have open/closed states, locks, traps, module transitions
    /// - Based on UTD file format (GFF with "UTD " signature) containing door template data
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath (fired via EventBus)
    /// - Module transitions: LinkedToModule + LinkedToFlags bit 1 = module transition (loads new module)
    /// - Area transitions: LinkedToFlags bit 2 = area transition (moves to waypoint/trigger in current module)
    /// - Door locking: KeyName field (item ResRef) required to unlock, or LockDC set for lockpicking
    /// - Door HP: Doors can be destroyed (CurrentHP <= 0), have Hardness (damage reduction), saves (Fort/Reflex/Will)
    /// - Secret doors: SecretDoorDC determines detection difficulty for hidden doors
    /// </remarks>
    public class OdysseyDoorComponent : BaseDoorComponent
    {
        public OdysseyDoorComponent()
        {
            TemplateResRef = string.Empty;
            KeyName = string.Empty;
            LinkedTo = string.Empty;
            LinkedToModule = string.Empty;
            TransitionDestination = string.Empty;
            KeyTag = string.Empty;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Generic door type (index into genericdoors.2da).
        /// </summary>
        public int GenericType { get; set; }

        /// <summary>
        /// Current hit points (Odyssey-specific field name).
        /// </summary>
        public int CurrentHP
        {
            get { return HitPoints; }
            set { HitPoints = value; }
        }

        /// <summary>
        /// Maximum hit points (Odyssey-specific field name).
        /// </summary>
        public int MaxHP
        {
            get { return MaxHitPoints; }
            set { MaxHitPoints = value; }
        }

        /// <summary>
        /// Fortitude save.
        /// </summary>
        public int Fort { get; set; }

        /// <summary>
        /// Reflex save.
        /// </summary>
        public int Reflex { get; set; }

        /// <summary>
        /// Will save.
        /// </summary>
        public int Will { get; set; }

        /// <summary>
        /// Whether the door is lockable.
        /// </summary>
        public bool Lockable { get; set; }

        /// <summary>
        /// Key auto-removes when used.
        /// </summary>
        public bool AutoRemoveKey { get; set; }

        /// <summary>
        /// Key tag name (Odyssey-specific field name).
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>
        /// Linked flags (1 = module transition).
        /// </summary>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Whether the door has a trap.
        /// </summary>
        public bool TrapFlag { get; set; }

        /// <summary>
        /// Trap type.
        /// </summary>
        public int TrapType { get; set; }

        /// <summary>
        /// Whether the trap is detectable.
        /// </summary>
        public bool TrapDetectable { get; set; }

        /// <summary>
        /// Trap detect DC.
        /// </summary>
        public int TrapDetectDC { get; set; }

        /// <summary>
        /// Whether the trap is disarmable.
        /// </summary>
        public bool TrapDisarmable { get; set; }

        /// <summary>
        /// Trap disarm DC.
        /// </summary>
        public int DisarmDC { get; set; }

        /// <summary>
        /// Whether the trap is detected.
        /// </summary>
        public bool TrapDetected { get; set; }

        /// <summary>
        /// Whether the trap is one-shot.
        /// </summary>
        public bool TrapOneShot { get; set; }

        /// <summary>
        /// Faction ID.
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Whether the door is interruptable.
        /// </summary>
        public bool Interruptable { get; set; }

        /// <summary>
        /// Whether the door is plot-critical.
        /// </summary>
        public bool Plot { get; set; }

        /// <summary>
        /// Whether the door cannot drop below 1 HP (TSL/KotOR2 only).
        /// </summary>
        /// <remarks>
        /// Min1HP Flag:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door system (KotOR2/TSL only)
        /// - Located via UTD field: "Min1HP" (UInt8/Byte, KotOR2 only)
        /// - Original implementation: If Min1HP is true (1), door cannot drop below 1 HP when damaged
        /// - Plot doors: Min1HP=1 prevents door from being destroyed, making it effectively indestructible
        /// - swkotor2.exe: FUN_00584f40 @ 0x00584f40 loads Min1HP from UTD template
        /// - swkotor2.exe: FUN_00585ec0 @ 0x00585ec0 saves Min1HP to UTD template
        /// - Note: This field does not exist in swkotor.exe (KotOR1); always false for K1 doors
        /// </remarks>
        public bool Min1HP { get; set; }

        /// <summary>
        /// Whether the door cannot be blasted (TSL/KotOR2 only).
        /// </summary>
        /// <remarks>
        /// NotBlastable Flag:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) door system (KotOR2/TSL only)
        /// - Located via UTD field: "NotBlastable" (UInt8/Byte, KotOR2 only)
        /// - Original implementation: If NotBlastable is true (1), door cannot be blasted (explosive damage)
        /// - Blasting: Refers to damage from explosives, grenades, or force powers that bypass normal hardness
        /// - swkotor2.exe: FUN_00584f40 @ 0x00584f40 loads NotBlastable from UTD template
        /// - swkotor2.exe: FUN_00585ec0 @ 0x00585ec0 saves NotBlastable to UTD template
        /// - Note: This field does not exist in swkotor.exe (KotOR1); always false for K1 doors
        /// </remarks>
        public bool NotBlastable { get; set; }

        // OpenState property is defined above (line 103)

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        /// <remarks>
        /// Module Transition Check:
        /// - Based on swkotor.exe and swkotor2.exe door transition system
        /// - swkotor.exe: Door list loading (FUN_0050a0e0 @ 0x0050a0e0 loads door list from GIT), door saving (FUN_00507810 @ 0x00507810 saves door list to GIT)
        /// - swkotor.exe: Door event handling (FUN_004dcfb0 @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedToModule, LinkedToFlags) as swkotor2.exe
        /// - Located via string references: "LinkedToModule" @ 0x007bd7bc (swkotor2.exe), "LinkedToFlags" @ 0x007bd788 (swkotor2.exe)
        /// - swkotor2.exe door loading: FUN_005838d0 @ 0x005838d0 reads LinkedToModule and LinkedToFlags from UTD template
        /// - swkotor2.exe door loading: FUN_00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - swkotor2.exe GIT loading: FUN_004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
        /// - Original implementation: LinkedToFlags bit 1 (0x1) = module transition flag (same in both swkotor.exe and swkotor2.exe)
        /// - Module transition: If LinkedToFlags & 1 != 0 and LinkedToModule is non-empty, door triggers module transition
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns in new module
        /// - Note: swkotor.exe uses identical transition flag system to swkotor2.exe; exact function addresses for door property loading in swkotor.exe need verification via Ghidra MCP
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
        /// - Based on swkotor.exe and swkotor2.exe door transition system
        /// - swkotor.exe: Door list loading (FUN_0050a0e0 @ 0x0050a0e0 loads door list from GIT), door saving (FUN_00507810 @ 0x00507810 saves door list to GIT)
        /// - swkotor.exe: Door event handling (FUN_004dcfb0 @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedTo, LinkedToFlags, TransitionDestination) as swkotor2.exe
        /// - Located via string references: "LinkedTo" @ 0x007bd798 (swkotor2.exe), "LinkedToFlags" @ 0x007bd788 (swkotor2.exe), "TransitionDestination" @ 0x007bd7a4 (swkotor2.exe)
        /// - swkotor2.exe door loading: FUN_005838d0 @ 0x005838d0 reads LinkedTo and LinkedToFlags from UTD template
        /// - swkotor2.exe door loading: FUN_00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - swkotor2.exe GIT loading: FUN_004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
        /// - Original implementation: LinkedToFlags bit 2 (0x2) = area transition flag (same in both swkotor.exe and swkotor2.exe)
        /// - Area transition: If LinkedToFlags & 2 != 0 and LinkedTo is non-empty, door triggers area transition within module
        /// - LinkedTo: Waypoint or trigger tag to transition to (within current module)
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns after transition
        /// - Note: swkotor.exe uses identical transition flag system to swkotor2.exe; exact function addresses for door property loading in swkotor.exe need verification via Ghidra MCP
        /// </remarks>
        public override bool IsAreaTransition
        {
            get { return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo); }
        }

        /// <summary>
        /// Key tag (alias for KeyName for interface compatibility).
        /// </summary>
        public override string KeyTag
        {
            get { return KeyName ?? string.Empty; }
            set { KeyName = value; }
        }

        /// <summary>
        /// Locks the door.
        /// </summary>
        /// <remarks>
        /// Door Locking:
        /// - Based on swkotor.exe and swkotor2.exe door locking system
        /// - Located via string references: "OnLock" @ 0x007c1a28 (swkotor2.exe), "EVENT_LOCK_OBJECT" @ 0x007bcd20 (swkotor2.exe, case 0xd in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (swkotor2.exe, 0x1c)
        /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 (swkotor2.exe) handles EVENT_LOCK_OBJECT (case 0xd, fires before script execution)
        /// - Original implementation: Sets IsLocked flag to true, fires OnLock script event
        /// - Lock validation: Only locks if Lockable flag is true (from UTD template)
        /// - Script execution: OnLock script (ScriptOnLock field in UTD template) executes after door is locked
        /// </remarks>
        public override void Lock()
        {
            if (!Lockable || IsLocked)
            {
                return;
            }

            base.Lock();

            // Fire OnLock script event would be handled by action system
        }

        /// <summary>
        /// Applies damage to the door (for bashing).
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <remarks>
        /// Door Bashing:
        /// - Based on swkotor.exe and swkotor2.exe door bashing system
        /// - Located via string references: "gui_mp_bashdp" @ 0x007b5e04 (swkotor2.exe), "gui_mp_bashup" @ 0x007b5e14 (swkotor2.exe, door bash GUI panels)
        /// - "gui_mp_bashd" @ 0x007b5e24 (swkotor2.exe), "gui_mp_bashu" @ 0x007b5e34 (swkotor2.exe, door bash GUI elements)
        /// - Original implementation: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
        /// - Bash damage: Strength modifier + weapon damage (if weapon equipped) vs door Hardness
        /// - Door destruction: When CurrentHP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
        /// - Open state: Set to 2 (destroyed state) when door is bashed open
        /// </remarks>
        public override void ApplyDamage(int damage)
        {
            base.ApplyDamage(damage);
            // CurrentHP is updated via HitPoints property
        }
    }
}
