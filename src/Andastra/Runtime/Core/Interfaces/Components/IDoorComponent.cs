namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for door entities.
    /// </summary>
    /// <remarks>
    /// Door Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe door system
    /// - Located via string references: "Door" @ 0x007bc538, "Door List" @ 0x007bd270
    /// - "LinkedTo" @ 0x007c13a0, "LinkedToModule" @ 0x007bd7bc (door transition links)
    /// - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events (EVENT_OPEN_OBJECT case 7, EVENT_CLOSE_OBJECT case 6, EVENT_LOCK_OBJECT case 0xd, EVENT_UNLOCK_OBJECT case 0xc)
    /// - Original implementation: Doors have open/locked states, transitions, HP for bashing
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
        /// Whether this door is a module transition.
        /// </summary>
        bool IsModuleTransition { get; }

        /// <summary>
        /// Whether this door is an area transition.
        /// </summary>
        bool IsAreaTransition { get; }

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
        /// Based on swkotor2.exe: Door bashing damage application
        /// </summary>
        void ApplyDamage(int damage);
    }
}

