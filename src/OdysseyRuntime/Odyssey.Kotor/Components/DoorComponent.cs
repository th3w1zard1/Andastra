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
    /// - Located via string references: "Door List" @ 0x007bd248, "GenericDoors" @ 0x007c4ba8
    /// - "DoorTypes" @ 0x007c4b9c, "SecretDoorDC" @ 0x007c1acc
    /// - Original implementation: FUN_00584f40 @ 0x00584f40 (save door data to GFF)
    /// - FUN_004e08e0 @ 0x004e08e0 (load door instances from GIT)
    /// - Doors have open/closed states, locks, traps, module transitions
    /// - Based on UTD file format (GFF with "UTD " signature)
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Module transitions: LinkedToModule + LinkedToFlags bit 1 = module transition
    /// - Area transitions: LinkedToFlags bit 2 = area transition (linked to waypoint/trigger)
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
        /// Current animation state.
        /// </summary>
        public int AnimationState { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        public bool IsModuleTransition
        {
            get { return (LinkedToFlags & 1) != 0 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
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
        /// - Original implementation: Sets IsOpen flag, plays open animation, fires OnOpen script
        /// </remarks>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            AnimationState = 1; // Open state

            // Fire OnOpen script event would be handled by action system
        }

        /// <summary>
        /// Closes the door.
        /// </summary>
        /// <remarks>
        /// Door Closing:
        /// - Based on swkotor2.exe door closing system
        /// - Original implementation: Sets IsOpen flag, plays close animation, fires OnClose script
        /// </remarks>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            AnimationState = 0; // Closed state

            // Fire OnClose script event would be handled by action system
        }

        /// <summary>
        /// Locks the door.
        /// </summary>
        /// <remarks>
        /// Door Locking:
        /// - Based on swkotor2.exe door locking system
        /// - Original implementation: Sets IsLocked flag, fires OnLock script
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
        /// - Original implementation: Clears IsLocked flag, fires OnUnlock script
        /// - Can be unlocked via key, lockpicking (Security skill), or bashing
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
        /// - Original implementation: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit)
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
                AnimationState = 2; // Destroyed state
            }
        }
    }
}
