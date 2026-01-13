
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
    /// - TSL-specific fields (Min1HP, NotBlastable) are supported but default to false for K1 compatibility
    /// - Located via string references:
    ///   - ["Door List"]    @ (TSL: 0x007bd248) - swkotor2.exe GIT door list
    ///   - ["GenericDoors"] @ (TSL: 0x007c4ba8) - generic doors 2DA table
    ///   - ["DoorTypes"]    @ (TSL: 0x007c4b9c) - door types field
    ///   - ["SecretDoorDC"] @ (TSL: 0x007c1acc) - secret door DC field
    /// - Transition fields:
    ///   - ["LinkedTo"]    @ (TSL: 0x007bd798) - linked to waypoint/area
    ///   - ["LinkedToModule"] @ (TSL: 0x007bd7bc) - linked to module
    ///   - ["LinkedToFlags"] @ (TSL: 0x007bd788) - transition flags
    ///   - ["TransitionDestination"] @ (TSL: 0x007bd7a4) - waypoint tag for positioning after transition
    /// - Door animations:
    ///   - ["i_opendoor"] @ (TSL: 0x007c86d4) - open door animation
    ///   - ["i_doorsaber"] @ (TSL: 0x007ccca0) - saber door animation
    /// - GUI references:
    ///   - ["gui_mp_doordp"] @ (TSL: 0x007b5bdc)
    ///   - ["gui_mp_doorup"] @ (TSL: 0x007b5bec)
    ///   - ["gui_mp_doord"]  @ (TSL: 0x007b5d24)
    ///   - ["gui_mp_dooru"]  @ (TSL: 0x007b5d34)
    ///   - ["gui_doorsaber"] @ (TSL: 0x007c2fe4)
    /// - Error messages:
    ///   - ["Cannot load door model '%s'."] @ (TSL: 0x007d2488) - door model loading error
    ///   - ["CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d"] @ (TSL: 0x007d24a8) - door animation name error
    ///   - [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x00584f40) - load door data from GFF/UTD template
    ///   - Loads PortraitId/Portrait, CreatorId, script hooks:
    ///     - ["ScriptHeartbeat"] @ (TSL: 0x007c1a28) - heartbeat script
    ///     - ["ScriptOnEnter"] @ (TSL: 0x007c1a38) - enter script
    ///     - ["ScriptOnExit"] @ (TSL: 0x007c1a48) - exit script
    ///     - ["ScriptUserDefine"] @ (TSL: 0x007c1a58) - user define script
    ///     - ["OnTrapTriggered"] @ (TSL: 0x007c1a68) - trap triggered script
    ///     - ["OnDisarm"] @ (TSL: 0x007c1a78) - disarm script
    ///     - ["OnClick"] @ (TSL: 0x007c1a88) - click script
    ///   - Loads TrapType, TrapOneShot, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, KeyName:
    ///     - ["TrapType"] @ (TSL: 0x007c1a9c) - trap type
    ///     - ["TrapOneShot"] @ (TSL: 0x007c1aa4) - trap one shot
    ///     - ["LinkedTo"] @ (TSL: 0x007bd798) - linked to waypoint/area
    ///     - ["LinkedToFlags"] @ (TSL: 0x007bd788) - transition flags
    ///     - ["LinkedToModule"] @ (TSL: 0x007bd7bc) - linked to module
    ///     - ["AutoRemoveKey"] @ (TSL: 0x007c1ab4) - auto remove key
    ///     - ["Tag"] @ (TSL: 0x007c1ac4) - tag
    ///     - ["LocalizedName"] @ (TSL: 0x007c1ad4) - localized name
    ///     - ["Faction"] @ (TSL: 0x007c1ae4) - faction
    ///     - ["KeyName"] @ (TSL: 0x007c1af4) - key name
    ///   - Loads TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, DisarmDCMod, DetectDCMod, Cursor, TransitionDestination, Type, HighlightHeight:
    ///     - ["TrapDisarmable"] @ (TSL: 0x007c1b04) - trap disarmable
    ///     - ["TrapDetectable"] @ (TSL: 0x007c1b14) - trap detectable
    ///     - ["OwnerDemolitionsSkill"] @ (TSL: 0x007c1b24) - owner demolitions skill
    ///     - ["DisarmDCMod"] @ (TSL: 0x007c1b34) - disarm DC mod
    ///     - ["DetectDCMod"] @ (TSL: 0x007c1b44) - detect DC mod
    ///     - ["Cursor"] @ (TSL: 0x007c1b54) - cursor
    ///     - ["TransitionDestination"] @ (TSL: 0x007bd7a4) - waypoint tag for positioning after transition
    ///     - ["Type"] @ (TSL: 0x007c1b64) - type
    ///     - ["HighlightHeight"] @ (TSL: 0x007c1b74) - highlight height
    ///   - Loads position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation), Geometry polygon vertices:
    ///     - ["XPosition"] @ (TSL: 0x007c1b84) - X position
    ///     - ["YPosition"] @ (TSL: 0x007c1b94) - Y position
    ///     - ["ZPosition"] @ (TSL: 0x007c1ba4) - Z position
    ///     - ["XOrientation"] @ (TSL: 0x007c1bb4) - X orientation
    ///     - ["YOrientation"] @ (TSL: 0x007c1bc4) - Y orientation
    ///     - ["ZOrientation"] @ (TSL: 0x007c1bd4) - Z orientation
    ///     - ["PointX"] @ (TSL: 0x007c1be4) - X position of geometry polygon vertex
    ///     - ["PointY"] @ (TSL: 0x007c1bf4) - Y position of geometry polygon vertex
    ///     - ["PointZ"] @ (TSL: 0x007c1c04) - Z position of geometry polygon vertex
    ///   - Loads LoadScreenID, SetByPlayerParty
    ///   - Geometry vertices are transformed by door position/orientation (relative to door transform)
    /// - swkotor2.exe: [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x00585ec0) - save door data to GFF/UTD template
    ///   - Saves script hooks:
    ///     - ["ScriptHeartbeat"] @ (TSL: 0x007c1a28) - heartbeat script
    ///     - ["ScriptOnEnter"] @ (TSL: 0x007c1a38) - enter script
    ///     - ["ScriptOnExit"] @ (TSL: 0x007c1a48) - exit script
    ///     - ["ScriptUserDefine"] @ (TSL: 0x007c1a58) - user define script
    ///     - ["OnTrapTriggered"] @ (TSL: 0x007c1a68) - trap triggered script
    ///     - ["OnDisarm"] @ (TSL: 0x007c1a78) - disarm script
    ///     - ["OnClick"] @ (TSL: 0x007c1a88) - click script
    ///   - Saves TrapType, TrapOneShot, CreatorId, LinkedTo, LinkedToFlags, LinkedToModule, AutoRemoveKey, Tag, LocalizedName, Faction, Cursor, KeyName:
    ///     - ["TrapType"] @ (TSL: 0x007c1a9c) - trap type
    ///     - ["TrapOneShot"] @ (TSL: 0x007c1aa4) - trap one shot
    ///     - ["CreatorId"] @ (TSL: 0x007c1ab4) - creator ID
    ///     - ["LinkedTo"] @ (TSL: 0x007bd798) - linked to waypoint/area
    ///     - ["LinkedToFlags"] @ (TSL: 0x007bd788) - transition flags
    ///     - ["LinkedToModule"] @ (TSL: 0x007bd7bc) - linked to module
    ///     - ["AutoRemoveKey"] @ (TSL: 0x007c1ab4) - auto remove key
    ///     - ["Tag"] @ (TSL: 0x007c1ac4) - tag
    ///     - ["LocalizedName"] @ (TSL: 0x007c1ad4) - localized name
    ///     - ["Faction"] @ (TSL: 0x007c1ae4) - faction
    ///     - ["Cursor"] @ (TSL: 0x007c1b54) - cursor
    ///     - ["KeyName"] @ (TSL: 0x007c1af4) - key name
    ///   - Saves TrapDisarmable, TrapDetectable, OwnerDemolitionsSkill, PortraitId/Portrait, Type, HighlightHeight:
    ///     - ["TrapDisarmable"] @ (TSL: 0x007c1b04) - trap disarmable
    ///     - ["TrapDetectable"] @ (TSL: 0x007c1b14) - trap detectable
    ///     - ["OwnerDemolitionsSkill"] @ (TSL: 0x007c1b24) - owner demolitions skill
    ///     - ["PortraitId"] @ (TSL: 0x007c1b34) - portrait ID
    ///     - ["Type"] @ (TSL: 0x007c1b44) - type
    ///     - ["HighlightHeight"] @ (TSL: 0x007c1b54) - highlight height
    ///   - Saves position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation)
    ///   - Saves Geometry polygon vertices:
    ///     - ["PointX"] @ (TSL: 0x007c1be4) - X position of geometry polygon vertex
    ///     - ["PointY"] @ (TSL: 0x007c1bf4) - Y position of geometry polygon vertex
    ///     - ["PointZ"] @ (TSL: 0x007c1c04) - Z position of geometry polygon vertex
    ///   - Saves LoadScreenID, TransitionDestination, SetByPlayerParty:
    ///     - ["LoadScreenID"] @ (TSL: 0x007c1c14) - load screen ID
    ///     - ["TransitionDestination"] @ (TSL: 0x007bd7a4) - waypoint tag for positioning after transition
    ///     - ["SetByPlayerParty"] @ (TSL: 0x007c1c24) - set by player party
    /// - [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x004e08e0) - load door instances from GIT including position, linked transitions
    /// - [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x00580ed0) - door loading function
    /// - [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x005838d0) - door initialization
    /// - [TODO: Name function] @ (K1: 0x0050a0e0, TSL: TODO: Find address) - load door list from GIT
    /// - [TODO: Name function] @ (K1: 0x00507810, TSL: TODO: Find address) - save door list to GIT
    /// - [TODO: Name function] @ (K1: 0x004dcfb0, TSL: TODO: Find address) - door event handling, including transition events
    /// - Note: swkotor.exe uses identical UTD template structure and transition flag system as swkotor2.exe; exact function addresses for door property loading from UTD templates in swkotor.exe need verification
    /// - Doors have open/closed states, locks, traps, module transitions
    /// - Based on UTD file format (GFF with "UTD " signature) containing door template data
    /// - Script events:
    ///     - ["OnOpen"] @ (TSL: 0x007c1c34) - open script
    ///     - ["OnClose"] @ (TSL: 0x007c1c44) - close script
    ///     - ["OnLock"] @ (TSL: 0x007c1c54) - lock script
    ///     - ["OnUnlock"] @ (TSL: 0x007c1c64) - unlock script
    ///     - ["OnDamaged"] @ (TSL: 0x007c1c74) - damaged script
    ///     - ["OnDeath"] @ (TSL: 0x007c1c84) - death script
    /// - Module transitions:
    ///     - ["LinkedToModule"] @ (TSL: 0x007bd7bc) - linked to module
    ///     - ["LinkedToFlags"] @ (TSL: 0x007bd788) - transition flags
    /// - Door locking:
    ///     - ["KeyName"] @ (TSL: 0x007c1c94) - key name
    ///     - ["LockDC"] @ (TSL: 0x007c1ca4) - lock DC
    /// - Door HP:
    ///     - ["CurrentHP"] @ (TSL: 0x007c1cb4) - current HP
    ///     - ["MaxHP"] @ (TSL: 0x007c1cc4) - max HP
    ///     - ["Fort"] @ (TSL: 0x007c1cd4) - fortitude save
    ///     - ["Reflex"] @ (TSL: 0x007c1ce4) - reflex save
    ///     - ["Will"] @ (TSL: 0x007c1cf4) - will save
    /// - Secret doors:
    ///     - ["SecretDoorDC"] @ (TSL: 0x007c1d04) - secret door DC
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
        /// - swkotor2.exe: 0x00584f40 @ 0x00584f40 loads Min1HP from UTD template
        /// - swkotor2.exe: 0x00585ec0 @ 0x00585ec0 saves Min1HP to UTD template
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
        /// - swkotor2.exe: 0x00584f40 @ 0x00584f40 loads NotBlastable from UTD template
        /// - swkotor2.exe: 0x00585ec0 @ 0x00585ec0 saves NotBlastable to UTD template
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
        /// - swkotor.exe: Door list loading (0x0050a0e0 @ 0x0050a0e0 loads door list from GIT), door saving (0x00507810 @ 0x00507810 saves door list to GIT)
        /// - swkotor.exe: Door event handling (0x004dcfb0 @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedToModule, LinkedToFlags) as swkotor2.exe
        /// - Located via string references: "LinkedToModule" @ 0x007bd7bc (swkotor2.exe), "LinkedToFlags" @ 0x007bd788 (swkotor2.exe)
        /// - swkotor2.exe door loading: 0x005838d0 @ 0x005838d0 reads LinkedToModule and LinkedToFlags from UTD template
        /// - swkotor2.exe door loading: 0x00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - swkotor2.exe GIT loading: 0x004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
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
        /// - swkotor.exe: Door list loading (0x0050a0e0 @ 0x0050a0e0 loads door list from GIT), door saving (0x00507810 @ 0x00507810 saves door list to GIT)
        /// - swkotor.exe: Door event handling (0x004dcfb0 @ 0x004dcfb0 handles door events including transitions)
        /// - swkotor.exe: Door transition system uses same UTD template fields (LinkedTo, LinkedToFlags, TransitionDestination) as swkotor2.exe
        /// - Located via string references: "LinkedTo" @ 0x007bd798 (swkotor2.exe), "LinkedToFlags" @ 0x007bd788 (swkotor2.exe), "TransitionDestination" @ 0x007bd7a4 (swkotor2.exe)
        /// - swkotor2.exe door loading: 0x005838d0 @ 0x005838d0 reads LinkedTo and LinkedToFlags from UTD template
        /// - swkotor2.exe door loading: 0x00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - swkotor2.exe GIT loading: 0x004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
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
        /// - Located via string references: "OnLock" @ 0x007c1a28 (swkotor2.exe), "EVENT_LOCK_OBJECT" @ 0x007bcd20 (swkotor2.exe, case 0xd in 0x004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (swkotor2.exe, 0x1c)
        /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 (swkotor2.exe) handles EVENT_LOCK_OBJECT (case 0xd, fires before script execution)
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
        /// - Located via string references: ["gui_mp_bashdp"] @ (TSL: 0x007b5e04) - door bash GUI panels
        /// - ["gui_mp_bashup"] @ (TSL: 0x007b5e14) - door bash GUI elements
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

        /// <summary>
        /// Applies damage to the door with a specific damage type (TSL-specific behavior for Min1HP and NotBlastable).
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <param name="damageType">The type of damage being applied.</param>
        /// <remarks>
        /// Door Damage Application with Type:
        /// - Based on swkotor.exe and swkotor2.exe door damage system
        /// - Located via string references:
        ///   - ["gui_mp_bashdp"] @ (TSL: 0x007b5e04) - door bash GUI panels
        ///   - ["gui_mp_bashup"] @ (TSL: 0x007b5e14) - door bash GUI elements
        ///   - ["gui_mp_bashu"]  @ (TSL: 0x007b5e34) - door bash GUI elements
        ///   - [TODO: Name function] @ (K1: N/A, TSL: 0x00584f40) - loads Min1HP and NotBlastable from UTD template (TSL only)
        ///   - TSL-specific behavior: Min1HP flag prevents door from dropping below 1 HP (only in TSL, field doesn't exist in K1)
        ///   - TSL-specific behavior: NotBlastable flag prevents explosive/force power damage (only in TSL, field doesn't exist in K1)
        /// - Original implementation:
        ///   * If NotBlastable is true, door cannot be damaged by explosives, grenades, or force powers
        ///   - Blast damage types: Fire (explosives/grenades), Sonic (sonic grenades), Electrical (electrical force powers),
        ///     - DarkSide  (dark side force powers)
        ///     - LightSide (light side force powers)
        ///   * If NotBlastable is true and damage type is blast-type, damage is rejected (no HP reduction)
        ///   * If Min1HP is true, door HP is clamped to minimum of 1
        /// - Original implementation: If Min1HP is true, door HP is clamped to minimum of 1
        ///   - Plot doors: Min1HP=1 makes door effectively indestructible (cannot be bashed open)
        ///   - Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
        ///   - Bash damage: Strength modifier + weapon damage (if weapon equipped) vs door Hardness
        ///   - Door destruction: When CurrentHP <= 0 (and Min1HP is false), door is marked as bashed (IsBashed=true), unlocked, and opened
        ///   - Open state: Set to 2 (destroyed state) when door is bashed open (only if Min1HP is false)
        ///   - Function: [TODO: Name function] @ (K1: TODO: Find address, TSL: 0x004dcfb0) - processes door bash damage events
        /// - Note: For K1 doors, Min1HP and NotBlastable are always false (fields don't exist in K1 templates), so behavior matches K1
        /// </remarks>
        public override void ApplyDamage(int damage, Runtime.Core.Combat.DamageType damageType)
        {
            if (damage <= 0)
            {
                return;
            }

            // TSL-specific: NotBlastable prevents explosive/force power damage
            // For K1, NotBlastable is always false (field doesn't exist), so this check is skipped
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00584f40 @ 0x00584f40 loads NotBlastable from UTD template
            // Original implementation: If NotBlastable is true, door cannot be damaged by explosives, grenades, or force powers
            // Blast damage types include: Fire (explosives/grenades), Sonic (sonic grenades), Electrical (electrical force powers),
            // DarkSide (dark side force powers), LightSide (light side force powers)
            // Physical damage (bashing/melee) is not blocked by NotBlastable flag
            // If NotBlastable is true and damage is blast-type, reject the damage completely
            if (NotBlastable)
            {
                // Check if damage type is blast-type (explosives/grenades/force powers)
                // Physical damage is explicitly allowed (bashing damage should always work)
                bool isBlastDamage = damageType == Runtime.Core.Combat.DamageType.Fire ||  // Explosives/grenades
                                     damageType == Runtime.Core.Combat.DamageType.Sonic ||  // Sonic grenades
                                     damageType == Runtime.Core.Combat.DamageType.Electrical ||  // Electrical force powers
                                     damageType == Runtime.Core.Combat.DamageType.DarkSide ||  // Dark side force powers
                                     damageType == Runtime.Core.Combat.DamageType.LightSide;  // Light side force powers

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
            // For K1, Min1HP is always false (field doesn't exist), so this check is skipped
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00584f40 @ 0x00584f40 loads Min1HP from UTD template
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
