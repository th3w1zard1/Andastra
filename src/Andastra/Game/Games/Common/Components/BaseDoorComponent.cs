using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of door component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Door Component Implementation:
    /// - Common door properties and methods across all engines
    /// - Handles base door state, locking, hit points, transitions
    /// - Provides base for engine-specific door component implementations
    /// - Cross-engine analysis: All engines share common door structure patterns
    /// - Common functionality: Open/Closed state, Locking, Hit Points, Transitions, Basic Operations
    /// - Engine-specific: File format details, transition systems, event handling, field names
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Door system with UTD template loading (FUN_0050a0e0, FUN_00507810)
    /// - swkotor2.exe: Enhanced door system with transition support (FUN_00584f40 @ 0x00584f40, FUN_00585ec0 @ 0x00585ec0, FUN_00580ed0 @ 0x00580ed0)
    /// - nwmain.exe: Aurora door system using CNWSDoor class (LoadDoor @ 0x1404208a0, SaveDoor @ 0x1404228e0)
    /// - daorigins.exe: Eclipse engine (door functionality may differ or be absent)
    /// - DragonAge2.exe: Enhanced Eclipse engine (door functionality may differ or be absent)
    /// - /:  (door functionality may differ or be absent)
    ///
    /// Common structure across engines:
    /// - IsOpen (bool): Whether door is currently open
    /// - OpenState (int): Animation state (0=closed, 1=open, 2=destroyed)
    /// - IsLocked (bool): Whether door is locked
    /// - LockDC (int): Difficulty class for lockpicking
    /// - KeyRequired (bool): Whether a key is required to unlock
    /// - KeyTag/KeyName (string): Key tag/name required to unlock
    /// - HitPoints (int): Current hit points (for bashing)
    /// - MaxHitPoints (int): Maximum hit points
    /// - Hardness (int): Damage reduction when bashing
    /// - IsBashed (bool): Whether door has been bashed open
    /// - LinkedTo (string): Linked destination tag for transitions
    /// - LinkedToModule (string): Linked destination module for transitions
    /// - IsModuleTransition (bool): Whether this door is a module transition
    /// - IsAreaTransition (bool): Whether this door is an area transition
    ///
    /// Common door operations across engines:
    /// - Open(): Opens the door
    /// - Close(): Closes the door
    /// - Lock(): Locks the door
    /// - Unlock(): Unlocks the door
    /// - ApplyDamage(int): Applies damage for bashing
    /// </remarks>
    [PublicAPI]
    public abstract class BaseDoorComponent : IDoorComponent
    {
        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Whether the door is currently open.
        /// </summary>
        public virtual bool IsOpen { get; set; }

        /// <summary>
        /// Whether the door is locked.
        /// </summary>
        public virtual bool IsLocked { get; set; }

        /// <summary>
        /// Whether the door can be locked by scripts.
        /// </summary>
        public virtual bool LockableByScript { get; set; }

        /// <summary>
        /// The DC required to pick the lock.
        /// </summary>
        public virtual int LockDC { get; set; }

        /// <summary>
        /// Whether the door has been bashed open.
        /// </summary>
        public virtual bool IsBashed { get; set; }

        /// <summary>
        /// Hit points of the door (for bashing).
        /// </summary>
        public virtual int HitPoints { get; set; }

        /// <summary>
        /// Maximum hit points of the door.
        /// </summary>
        public virtual int MaxHitPoints { get; set; }

        /// <summary>
        /// Hardness reduces damage taken when bashing.
        /// </summary>
        public virtual int Hardness { get; set; }

        /// <summary>
        /// Key tag required to unlock the door.
        /// </summary>
        public virtual string KeyTag { get; set; }

        /// <summary>
        /// Whether a key is required to unlock the door.
        /// </summary>
        public virtual bool KeyRequired { get; set; }

        /// <summary>
        /// Animation state (0=closed, 1=open, 2=destroyed).
        /// </summary>
        public virtual int OpenState { get; set; }

        /// <summary>
        /// Linked destination tag for transitions.
        /// </summary>
        public virtual string LinkedTo { get; set; }

        /// <summary>
        /// Linked destination module for transitions.
        /// </summary>
        public virtual string LinkedToModule { get; set; }

        /// <summary>
        /// Transition destination waypoint tag for positioning after transition.
        /// </summary>
        /// <remarks>
        /// Transition Destination:
        /// - Common across all engines that support door transitions
        /// - Based on nwmain.exe: CNWSDoor::LoadDoor loads TransitionDestin field from GIT
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005838d0 @ 0x005838d0 reads TransitionDestination from UTD template
        /// - Located via string references: "TransitionDestin" @ 0x007bd7a4 (swkotor2.exe), "TransitionDestin" in GIT format
        /// - Original implementation: TransitionDestin/TransitionDestination specifies waypoint tag where party spawns after transition
        /// - For module transitions: Waypoint tag in destination module where party spawns
        /// - For area transitions: Waypoint tag in destination area where party spawns
        /// - Empty string means use default entry waypoint (STARTWAYPOINT)
        /// </remarks>
        public virtual string TransitionDestination { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        /// <remarks>
        /// Module Transition Check:
        /// - Common across all engines that support module transitions
        /// - Implementation details differ by engine (flag bits, field names)
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public abstract bool IsModuleTransition { get; }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        /// <remarks>
        /// Area Transition Check:
        /// - Common across all engines that support area transitions
        /// - Implementation details differ by engine (flag bits, field names)
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public abstract bool IsAreaTransition { get; }

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// </summary>
        /// <remarks>
        /// Conversation Property:
        /// - Common across all engines that support door conversations
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00580330 @ 0x00580330 saves door data including Conversation field
        /// - Located via string reference: "Conversation" @ 0x007c1abc
        /// - Original implementation: Conversation field in UTD template contains dialogue ResRef
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual string Conversation { get; set; }

        /// <summary>
        /// Opens the door.
        /// </summary>
        /// <remarks>
        /// Door Opening:
        /// - Common behavior across all engines: Sets IsOpen flag, updates OpenState
        /// - Engine-specific: Animation handling, script event firing, transition triggering
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            OpenState = 1; // Open state
        }

        /// <summary>
        /// Closes the door.
        /// </summary>
        /// <remarks>
        /// Door Closing:
        /// - Common behavior across all engines: Sets IsOpen flag to false, updates OpenState
        /// - Engine-specific: Animation handling, script event firing
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            OpenState = 0; // Closed state
        }

        /// <summary>
        /// Locks the door.
        /// </summary>
        /// <remarks>
        /// Door Locking:
        /// - Common behavior across all engines: Sets IsLocked flag to true
        /// - Engine-specific: Lock validation, script event firing
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Lock()
        {
            if (IsLocked)
            {
                return;
            }

            IsLocked = true;
        }

        /// <summary>
        /// Unlocks the door.
        /// </summary>
        /// <remarks>
        /// Door Unlocking:
        /// - Common behavior across all engines: Sets IsLocked flag to false
        /// - Engine-specific: Unlock validation, script event firing
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Unlock()
        {
            if (!IsLocked)
            {
                return;
            }

            IsLocked = false;
        }

        /// <summary>
        /// Applies damage to the door (for bashing).
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <remarks>
        /// Door Bashing:
        /// - Common behavior across all engines: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit)
        /// - When CurrentHP <= 0, door is marked as bashed, unlocked, and opened
        /// - Engine-specific: Damage calculation, script event firing
        /// - Engine-specific subclasses override this to provide correct implementation
        /// - Defaults to Physical damage type (bashing)
        /// </remarks>
        public virtual void ApplyDamage(int damage)
        {
            // Default to Physical damage type for bashing
            ApplyDamage(damage, Core.Combat.DamageType.Physical);
        }

        /// <summary>
        /// Applies damage to the door with a specific damage type.
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <param name="damageType">The type of damage being applied.</param>
        /// <remarks>
        /// Door Damage Application with Type:
        /// - Common behavior across all engines: Applies damage minus hardness, destroys door when HP reaches 0
        /// - Hardness reduces damage taken (minimum 1 damage per hit)
        /// - When CurrentHP <= 0, door is marked as bashed, unlocked, and opened
        /// - Engine-specific: Damage type checking (e.g., NotBlastable flag in KOTOR 2), script event firing
        /// - Engine-specific subclasses override this to provide damage type-specific checks
        /// </remarks>
        public virtual void ApplyDamage(int damage, Core.Combat.DamageType damageType)
        {
            if (damage <= 0)
            {
                return;
            }

            // Apply hardness reduction (minimum 1 damage)
            int actualDamage = Math.Max(1, damage - Hardness);
            HitPoints = Math.Max(0, HitPoints - actualDamage);

            // If door is destroyed, mark as bashed and open
            if (HitPoints <= 0)
            {
                IsBashed = true;
                IsLocked = false;
                IsOpen = true;
                OpenState = 2; // Destroyed state
            }
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
        }
    }
}

