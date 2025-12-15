using System;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for door entities.
    /// </summary>
    /// <remarks>
    /// Door Component:
    /// - Based on swkotor2.exe door system
    /// - Located via string references: "Door List" @ 0x007bd248 (GIT door list), "GenericDoors" @ 0x007c4ba8 (generic doors 2DA table)
    /// - "DoorTypes" @ 0x007c4b9c (door types field), "SecretDoorDC" @ 0x007c1acc (secret door DC field)
    /// - Transition fields: "LinkedTo" @ 0x007bd798 (linked to waypoint/area), "LinkedToModule" @ 0x007bd7bc (linked to module)
    /// - "LinkedToFlags" @ 0x007bd788 (transition flags), "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition)
    /// - Door animations: "i_opendoor" @ 0x007c86d4 (open door animation), "i_doorsaber" @ 0x007ccca0 (saber door animation)
    /// - GUI references: "gui_mp_doordp" @ 0x007b5bdc, "gui_mp_doorup" @ 0x007b5bec, "gui_mp_doord" @ 0x007b5d24, "gui_mp_dooru" @ 0x007b5d34 (door GUI panels)
    /// - "gui_doorsaber" @ 0x007c2fe4 (saber door GUI)
    /// - Error messages:
    ///   - "Cannot load door model '%s'." @ 0x007d2488 (door model loading error)
    ///   - "CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d" @ 0x007d24a8 (door animation name error)
    /// - Original implementation: FUN_00584f40 @ 0x00584f40 (save door data to GFF including state, HP, lock status)
    /// - FUN_004e08e0 @ 0x004e08e0 (load door instances from GIT including position, linked transitions)
    /// - FUN_00580ed0 @ 0x00580ed0 (door loading function), FUN_005838d0 @ 0x005838d0 (door initialization)
    /// - Doors have open/closed states, locks, traps, module transitions
    /// - Based on UTD file format (GFF with "UTD " signature) containing door template data
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath (fired via EventBus)
    /// - Module transitions: LinkedToModule + LinkedToFlags bit 1 = module transition (loads new module)
    /// - Area transitions: LinkedToFlags bit 2 = area transition (moves to waypoint/trigger in current module)
    /// - Door locking: KeyName field (item ResRef) required to unlock, or LockDC set for lockpicking
    /// - Door HP: Doors can be destroyed (CurrentHP <= 0), have Hardness (damage reduction), saves (Fort/Reflex/Will)
    /// - Secret doors: SecretDoorDC determines detection difficulty for hidden doors
    /// </remarks>
    public class DoorComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public DoorComponent()
        {
            TemplateResRef = string.Empty;
            KeyName = string.Empty;
            LinkedTo = string.Empty;
            LinkedToModule = string.Empty;
            TransitionDestination = string.Empty;
        }

        public void OnAttach()
        {
        }

        public void OnDetach()
        {
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
        /// Current hit points.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Hardness (damage reduction).
        /// </summary>
        public int Hardness { get; set; }

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
        /// Whether the door is currently open.
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Animation state (0=closed, 1=open, 2=destroyed).
        /// </summary>
        public int OpenState { get; set; }

        /// <summary>
        /// Whether the door is locked.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Whether the door is lockable.
        /// </summary>
        public bool Lockable { get; set; }

        /// <summary>
        /// Lock difficulty class.
        /// </summary>
        public int LockDC { get; set; }

        /// <summary>
        /// Whether a key is required.
        /// </summary>
        public bool KeyRequired { get; set; }

        /// <summary>
        /// Key auto-removes when used.
        /// </summary>
        public bool AutoRemoveKey { get; set; }

        /// <summary>
        /// Key tag name.
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>
        /// Linked trigger/waypoint tag.
        /// </summary>
        public string LinkedTo { get; set; }

        /// <summary>
        /// Linked module resource.
        /// </summary>
        public string LinkedToModule { get; set; }

        /// <summary>
        /// Linked flags (1 = module transition).
        /// </summary>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Transition destination waypoint tag.
        /// </summary>
        public string TransitionDestination { get; set; }

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
        /// Conversation file.
        /// </summary>
        public string Conversation { get; set; }

        /// <summary>
        /// Whether the door is interruptable.
        /// </summary>
        public bool Interruptable { get; set; }

        /// <summary>
        /// Whether the door is plot-critical.
        /// </summary>
        public bool Plot { get; set; }

        /// <summary>
        /// Open state (0=closed, 1=open, 2=destroyed).
        /// Based on swkotor2.exe: OpenState field from UTD template (FUN_00580ed0 @ 0x00580ed0)
        /// Located via string references: "OpenState" field in door GFF structure
        /// Original implementation: 0=closed, 1=open, 2=destroyed, 3=locked (but locked is separate IsLocked flag)
        /// </summary>
        public int OpenState { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        /// <remarks>
        /// Module Transition Check:
        /// - Based on swkotor2.exe door transition system
        /// - Located via string references: "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
        /// - Door loading: FUN_005838d0 @ 0x005838d0 reads LinkedToModule and LinkedToFlags from UTD template
        /// - FUN_00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - FUN_004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
        /// - Original implementation: LinkedToFlags bit 1 (0x1) = module transition flag
        /// - Module transition: If LinkedToFlags & 1 != 0 and LinkedToModule is non-empty, door triggers module transition
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns in new module
        /// </remarks>
        public bool IsModuleTransition
        {
            get { return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        /// <remarks>
        /// Area Transition Check:
        /// - Based on swkotor2.exe door transition system
        /// - Located via string references: "LinkedTo" @ 0x007bd798, "LinkedToFlags" @ 0x007bd788, "TransitionDestination" @ 0x007bd7a4
        /// - Door loading: FUN_005838d0 @ 0x005838d0 reads LinkedTo and LinkedToFlags from UTD template
        /// - FUN_00580ed0 @ 0x00580ed0 loads door properties including transition data
        /// - FUN_004e5920 @ 0x004e5920 loads door instances from GIT with transition fields
        /// - Original implementation: LinkedToFlags bit 2 (0x2) = area transition flag
        /// - Area transition: If LinkedToFlags & 2 != 0 and LinkedTo is non-empty, door triggers area transition within module
        /// - LinkedTo: Waypoint or trigger tag to transition to (within current module)
        /// - Transition destination: TransitionDestination waypoint tag specifies where party spawns after transition
        /// </remarks>
        public bool IsAreaTransition
        {
            get { return (LinkedToFlags & 2) != 0 && !string.IsNullOrEmpty(LinkedTo); }
        }

        /// <summary>
        /// Whether the door can be locked by scripts.
        /// </summary>
        public bool LockableByScript { get; set; }

        /// <summary>
        /// Whether the door has been bashed open.
        /// </summary>
        public bool IsBashed { get; set; }

        /// <summary>
        /// Key tag (alias for KeyName for interface compatibility).
        /// </summary>
        public string KeyTag
        {
            get { return KeyName; }
            set { KeyName = value; }
        }

        /// <summary>
        /// Opens the door.
        /// </summary>
        /// <remarks>
        /// Door Opening:
        /// - Based on swkotor2.exe door opening system
        /// - Located via string references: "OnOpen" @ 0x007c1a54, "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7 in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16), "i_opendoor" @ 0x007c86d4 (open door animation)
        /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_OPEN_OBJECT (case 7, fires before script execution)
        /// - Original implementation: Sets IsOpen flag, plays open animation ("i_opendoor"), fires OnOpen script event
        /// - Door state: AnimationState set to 1 (open), IsOpen flag set to true
        /// - Script execution: OnOpen script (ScriptOnOpen field in UTD template) executes after door opens
        /// - Transition doors: If IsModuleTransition or IsAreaTransition, door opening triggers transition event
        /// </remarks>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            OpenState = 1; // Open state

            // Fire OnOpen script event would be handled by action system
        }

        /// <summary>
        /// Closes the door.
        /// </summary>
        /// <remarks>
        /// Door Closing:
        /// - Based on swkotor2.exe door closing system
        /// - Located via string references: "OnClosed" @ 0x007be1c8, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6 in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17)
        /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_CLOSE_OBJECT (case 6, fires before script execution)
        /// - Original implementation: Sets IsOpen flag to false, plays close animation, fires OnClose script event
        /// - Door state: AnimationState set to 0 (closed), IsOpen flag set to false
        /// - Script execution: OnClose script (ScriptOnClose field in UTD template) executes after door closes
        /// </remarks>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            OpenState = 0; // Closed state

            // Fire OnClose script event would be handled by action system
        }

        /// <summary>
        /// Locks the door.
        /// </summary>
        /// <remarks>
        /// Door Locking:
        /// - Based on swkotor2.exe door locking system
        /// - Located via string references: "OnLock" @ 0x007c1a28, "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
        /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_LOCK_OBJECT (case 0xd, fires before script execution)
        /// - Original implementation: Sets IsLocked flag to true, fires OnLock script event
        /// - Lock validation: Only locks if Lockable flag is true (from UTD template)
        /// - Script execution: OnLock script (ScriptOnLock field in UTD template) executes after door is locked
        /// </remarks>
        public void Lock()
        {
            if (!Lockable || IsLocked)
            {
                return;
            }

            IsLocked = true;

            // Fire OnLock script event would be handled by action system
        }

        /// <summary>
        /// Unlocks the door.
        /// </summary>
        /// <remarks>
        /// Door Unlocking:
        /// - Based on swkotor2.exe door unlocking system
        /// - Located via string references: "OnUnlock" @ 0x007c1a28, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34 (case 0xc in FUN_004dcfb0)
        /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (0x1d)
        /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_UNLOCK_OBJECT (case 0xc, fires before script execution)
        /// - Original implementation: Clears IsLocked flag (sets to false), fires OnUnlock script event
        /// - Unlock methods: Can be unlocked via key (KeyName match), lockpicking (Security skill check vs LockDC), or bashing (strength check)
        /// - Script execution: OnUnlock script (ScriptOnUnlock field in UTD template) executes after door is unlocked
        /// </remarks>
        public void Unlock()
        {
            if (!IsLocked)
            {
                return;
            }

            IsLocked = false;

            // Fire OnUnlock script event would be handled by action system
        }

        /// <summary>
        /// Applies damage to the door (for bashing).
        /// </summary>
        /// <remarks>
        /// Door Bashing:
        /// - Based on swkotor2.exe door bashing system
        /// - Located via string references: "gui_mp_bashdp" @ 0x007b5e04, "gui_mp_bashup" @ 0x007b5e14 (door bash GUI panels)
        /// - "gui_mp_bashd" @ 0x007b5e24, "gui_mp_bashu" @ 0x007b5e34 (door bash GUI elements)
        /// - Original implementation: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
        /// - Bash damage: Strength modifier + weapon damage (if weapon equipped) vs door Hardness
        /// - Door destruction: When CurrentHP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
        /// - Open state: Set to 2 (destroyed state) when door is bashed open
        /// </remarks>
        public void ApplyDamage(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            // Apply hardness reduction (minimum 1 damage)
            int actualDamage = Math.Max(1, damage - Hardness);
            CurrentHP = Math.Max(0, CurrentHP - actualDamage);

            // If door is destroyed, mark as bashed and open
            if (CurrentHP <= 0)
            {
                IsBashed = true;
                IsLocked = false;
                IsOpen = true;
                OpenState = 2; // Destroyed state
            }
        }
    }
}
