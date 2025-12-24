using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common.Components
{
    /// <summary>
    /// Base implementation of placeable component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Placeable Component Implementation:
    /// - Common placeable properties and methods across all engines
    /// - Handles base placeable state, locking, inventory, hit points, useability
    /// - Provides base for engine-specific placeable component implementations
    /// - Cross-engine analysis: All engines share common placeable structure patterns
    /// - Common functionality: Useability, Inventory, Static flag, Open/Closed state, Locking, Hit Points, Hardness, Animation State, Conversation
    /// - Engine-specific: File format details, trap systems, appearance systems, event handling, field names
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Placeable system with UTP template loading (LoadPlaceableFromGFF, SavePlaceableToGFF)
    /// - swkotor2.exe: Enhanced placeable system (FUN_00588010 @ 0x00588010, FUN_00589520 @ 0x00589520)
    ///   - Located via string references: "Placeable" @ 0x007bc530, "Placeable List" @ 0x007bd260
    ///   - Script hooks: "OnUsed" @ 0x007be1c4, "ScriptOnUsed" @ 0x007beeb8
    ///   - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    ///   - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    ///   - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events
    /// - nwmain.exe: Aurora placeable system using CNWSPlaceable class
    ///   - LoadPlaceables @ 0x1403619e0, SavePlaceables @ 0x140367260
    ///   - CNWSPlaceable::LoadPlaceable @ 0x1404b4900, CNWSPlaceable::SavePlaceable @ 0x1404b6a60
    ///   - Located via string reference: "Placeable List" @ 0x140ddb7c0
    /// - daorigins.exe: Eclipse engine placeable system (CCPlaceable class)
    ///   - PlaceableList @ 0x00af5028, CPlaceable @ 0x00b0d488
    ///   - COMMAND_GETPLACEABLE* and COMMAND_SETPLACEABLE* functions
    /// - DragonAge2.exe: Enhanced Eclipse engine (similar to daorigins.exe)
    /// - /:  (different placeable system, not fully reverse engineered)
    ///
    /// Common structure across engines:
    /// - IsUseable (bool): Whether placeable can be used/interacted with
    /// - HasInventory (bool): Whether placeable has inventory (container)
    /// - IsStatic (bool): Whether placeable is static (no interaction)
    /// - IsOpen (bool): Whether placeable is currently open (for containers)
    /// - IsLocked (bool): Whether placeable is locked
    /// - LockDC (int): Difficulty class for lockpicking
    /// - KeyTag/KeyName (string): Key tag/name required to unlock
    /// - HitPoints (int): Current hit points (for destructible placeables)
    /// - MaxHitPoints (int): Maximum hit points
    /// - Hardness (int): Damage reduction when attacking
    /// - AnimationState (int): Current animation state (0=closed, 1=open, etc.)
    /// - Conversation (string): Conversation file (dialogue ResRef)
    ///
    /// Common placeable operations across engines:
    /// - Open(): Opens the placeable (for containers)
    /// - Close(): Closes the placeable
    /// - Activate(): Activates the placeable
    /// - Deactivate(): Deactivates the placeable
    /// - Unlock(): Unlocks the placeable
    /// </remarks>
    [PublicAPI]
    public abstract class BasePlaceableComponent : IPlaceableComponent
    {
        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Whether the placeable is usable.
        /// </summary>
        /// <remarks>
        /// Useability Property:
        /// - Common across all engines: Controls whether placeable can be interacted with
        /// - Based on swkotor2.exe: "Useable" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "Useable" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 56)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual bool IsUseable { get; set; }

        /// <summary>
        /// Whether the placeable has inventory (container).
        /// </summary>
        /// <remarks>
        /// Inventory Property:
        /// - Common across all engines: Controls whether placeable can store items
        /// - Based on swkotor2.exe: "HasInventory" field in UTP template (FUN_00589520 @ 0x00589520)
        /// - Based on nwmain.exe: "HasInventory" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 69)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual bool HasInventory { get; set; }

        /// <summary>
        /// Whether the placeable is static (cannot be destroyed).
        /// </summary>
        /// <remarks>
        /// Static Property:
        /// - Common across all engines: Controls whether placeable can be destroyed
        /// - Based on swkotor2.exe: "Static" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "Static" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 57)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual bool IsStatic { get; set; }

        /// <summary>
        /// Whether the placeable has been opened (for containers).
        /// </summary>
        /// <remarks>
        /// Open State Property:
        /// - Common across all engines: Controls whether container placeable is open
        /// - Based on swkotor2.exe: "Open" field in UTP template (FUN_00589520 @ 0x00589520)
        /// - Based on nwmain.exe: "Open" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60)
        /// - AnimationState 0=closed, 1=open (swkotor2.exe)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual bool IsOpen { get; set; }

        /// <summary>
        /// Whether the placeable is locked.
        /// </summary>
        /// <remarks>
        /// Locked Property:
        /// - Common across all engines: Controls whether placeable is locked
        /// - Based on swkotor2.exe: "Locked" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "Locked" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 68)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual bool IsLocked { get; set; }

        /// <summary>
        /// The DC required to pick the lock.
        /// </summary>
        /// <remarks>
        /// Lock DC Property:
        /// - Common across all engines: Difficulty class for lockpicking
        /// - Based on swkotor2.exe: "OpenLockDC" field in UTP template (FUN_00588010 @ 0x00588010, line 93)
        /// - Based on nwmain.exe: "OpenLockDC" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 40)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual int LockDC { get; set; }

        /// <summary>
        /// Key tag required to unlock.
        /// </summary>
        /// <remarks>
        /// Key Tag Property:
        /// - Common across all engines: Key tag/name required to unlock placeable
        /// - Based on swkotor2.exe: "KeyName" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "KeyName" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 41)
        /// - Engine-specific: Field names (KeyName vs KeyTag) and storage may differ, but concept is common
        /// </remarks>
        public virtual string KeyTag { get; set; }

        /// <summary>
        /// Current HP (for destructible placeables).
        /// </summary>
        /// <remarks>
        /// Hit Points Property:
        /// - Common across all engines: Current hit points for destructible placeables
        /// - Based on swkotor2.exe: "CurrentHP" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "CurrentHP" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 62)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual int HitPoints { get; set; }

        /// <summary>
        /// Max HP (for destructible placeables).
        /// </summary>
        /// <remarks>
        /// Max Hit Points Property:
        /// - Common across all engines: Maximum hit points for destructible placeables
        /// - Based on swkotor2.exe: "HP" field in UTP template (FUN_00588010 @ 0x00588010)
        /// - Based on nwmain.exe: "HP" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 60)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual int MaxHitPoints { get; set; }

        /// <summary>
        /// Hardness for damage reduction.
        /// </summary>
        /// <remarks>
        /// Hardness Property:
        /// - Common across all engines: Damage reduction when attacking placeable
        /// - Based on swkotor2.exe: "Hardness" field in UTP template (FUN_00589520 @ 0x00589520)
        /// - Based on nwmain.exe: "Hardness" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 63)
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual int Hardness { get; set; }

        /// <summary>
        /// Animation state of the placeable.
        /// </summary>
        /// <remarks>
        /// Animation State Property:
        /// - Common across all engines: Current animation state (0=closed, 1=open, etc.)
        /// - Based on swkotor2.exe: "Animation" field in UTP template (FUN_00589520 @ 0x00589520)
        /// - AnimationState 0=closed, 1=open for containers (swkotor2.exe)
        /// - Engine-specific: Field names and state values may differ, but concept is common
        /// </remarks>
        public virtual int AnimationState { get; set; }

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// </summary>
        /// <remarks>
        /// Conversation Property:
        /// - Common across all engines: Conversation file for dialogue interactions
        /// - Based on swkotor2.exe: FUN_00588010 @ 0x00588010 loads placeable data including Conversation field
        /// - Located via string reference: "Conversation" @ 0x007c1abc
        /// - Based on nwmain.exe: "Conversation" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 86)
        /// - Original implementation: Conversation field in UTP template contains dialogue ResRef
        /// - Engine-specific: Field names and storage may differ, but concept is common
        /// </remarks>
        public virtual string Conversation { get; set; }

        /// <summary>
        /// Opens the placeable (for containers).
        /// </summary>
        /// <remarks>
        /// Placeable Opening:
        /// - Common behavior across all engines: Sets IsOpen flag, updates AnimationState
        /// - Based on swkotor2.exe: EVENT_OPEN_OBJECT event (FUN_004dcfb0 @ 0x004dcfb0, case 7)
        /// - Engine-specific: Animation handling, script event firing (OnOpen)
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            AnimationState = 1; // Open state
        }

        /// <summary>
        /// Closes the placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Closing:
        /// - Common behavior across all engines: Sets IsOpen flag to false, updates AnimationState
        /// - Based on swkotor2.exe: EVENT_CLOSE_OBJECT event (FUN_004dcfb0 @ 0x004dcfb0, case 6)
        /// - Engine-specific: Animation handling, script event firing (OnClose/OnClosed)
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            AnimationState = 0; // Closed state
        }

        /// <summary>
        /// Activates the placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Activation:
        /// - Common behavior across all engines: Activates placeable (opens container if HasInventory)
        /// - Based on swkotor2.exe: OnUsed script event (FUN_004dcfb0 @ 0x004dcfb0)
        /// - For containers, this opens them
        /// - Engine-specific: Script event firing (OnUsed), use distance checking
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Activate()
        {
            // Placeable activation logic
            // For containers, this opens them
            if (HasInventory)
            {
                Open();
            }
        }

        /// <summary>
        /// Deactivates the placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Deactivation:
        /// - Common behavior across all engines: Deactivates placeable (closes container if HasInventory)
        /// - For containers, this closes them
        /// - Engine-specific: Script event firing
        /// - Engine-specific subclasses override this to provide correct implementation
        /// </remarks>
        public virtual void Deactivate()
        {
            // Placeable deactivation logic
            if (HasInventory)
            {
                Close();
            }
        }

        /// <summary>
        /// Unlocks the placeable.
        /// </summary>
        /// <remarks>
        /// Placeable Unlocking:
        /// - Common behavior across all engines: Sets IsLocked flag to false
        /// - Based on swkotor2.exe: EVENT_UNLOCK_OBJECT event (FUN_004dcfb0 @ 0x004dcfb0, case 0xc)
        /// - Engine-specific: Unlock validation, script event firing (OnUnlock)
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

