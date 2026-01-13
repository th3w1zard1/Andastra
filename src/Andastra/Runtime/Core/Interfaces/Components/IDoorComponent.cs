namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for door entities.
    /// </summary>
    /// <remarks>
    /// Door Component Interface:
    /// - Common interface for door functionality across all BioWare engines
    /// - Base implementation: BaseDoorComponent in Runtime.Games.Common.Components
    /// - Engine-specific implementations:
    ///   - Odyssey: OdysseyDoorComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraDoorComponent (nwmain.exe)
    ///   - Eclipse: EclipseDoorComponent (daorigins.exe, DragonAge2.exe) - if doors are supported
    ///   - Infinity: InfinityDoorComponent (, ) - if doors are supported
    /// - Common functionality: Open/Closed state, Locking, Hit Points, Transitions, Basic Operations
    /// - OpenState: 0=closed, 1=open, 2=destroyed
    /// - Doors can be locked (IsLocked), require keys (KeyRequired, KeyTag), have lock DC (LockDC)
    /// - Doors can be bashed open (IsBashed) if HP reduced to 0
    /// - Transitions link to other areas/modules (LinkedTo, LinkedToModule)
    /// - Script events: OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// </remarks>
    public interface IDoorComponent : IComponent
    {
        /// <summary>
        /// Whether the door is currently open.
        /// </summary>
        bool IsOpen { get; set; }

        /// <summary>
        /// Whether the door is locked.
        /// </summary>
        bool IsLocked { get; set; }

        /// <summary>
        /// Whether the door can be locked by scripts.
        /// </summary>
        bool LockableByScript { get; set; }

        /// <summary>
        /// The DC required to pick the lock.
        /// </summary>
        int LockDC { get; set; }

        /// <summary>
        /// Whether the door has been bashed open.
        /// </summary>
        bool IsBashed { get; set; }

        /// <summary>
        /// Hit points of the door (for bashing).
        /// </summary>
        int HitPoints { get; set; }

        /// <summary>
        /// Maximum hit points of the door.
        /// </summary>
        int MaxHitPoints { get; set; }

        /// <summary>
        /// Hardness reduces damage taken when bashing.
        /// </summary>
        int Hardness { get; set; }

        /// <summary>
        /// Key tag required to unlock the door.
        /// </summary>
        string KeyTag { get; set; }

        /// <summary>
        /// Whether a key is required to unlock the door.
        /// </summary>
        bool KeyRequired { get; set; }

        /// <summary>
        /// Animation state (0=closed, 1=open, 2=destroyed).
        /// </summary>
        int OpenState { get; set; }

        /// <summary>
        /// Linked destination tag for transitions.
        /// </summary>
        string LinkedTo { get; set; }

        /// <summary>
        /// Linked destination module for transitions.
        /// </summary>
        string LinkedToModule { get; set; }

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
        string TransitionDestination { get; set; }

        /// <summary>
        /// Whether this door is a module transition.
        /// </summary>
        bool IsModuleTransition { get; }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        bool IsAreaTransition { get; }

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00580330 @ 0x00580330 saves door data including Conversation field
        /// Located via string reference: "Conversation" @ 0x007c1abc
        /// Original implementation: Conversation field in UTD template contains dialogue ResRef
        /// </remarks>
        string Conversation { get; set; }

        /// <summary>
        /// Opens the door.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the door.
        /// </summary>
        void Close();

        /// <summary>
        /// Locks the door.
        /// </summary>
        void Lock();

        /// <summary>
        /// Unlocks the door.
        /// </summary>
        void Unlock();

        /// <summary>
        /// Applies damage to the door (for bashing).
        /// Reduces HP by damage amount (minus hardness), destroys door if HP reaches 0.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door bashing damage application
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        void ApplyDamage(int damage);

        /// <summary>
        /// Applies damage to the door with a specific damage type.
        /// Reduces HP by damage amount (minus hardness), destroys door if HP reaches 0.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door bashing damage application with damage type checking
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        /// <param name="damageType">The type of damage being applied.</param>
        /// <remarks>
        /// Damage Type Application:
        /// - Allows damage type-specific checks (e.g., NotBlastable flag in KOTOR 2)
        /// - Default implementation treats all damage as Physical (bashing)
        /// - Engine-specific implementations can override to check damage type flags
        /// </remarks>
        void ApplyDamage(int damage, Core.Combat.DamageType damageType);
    }
}

