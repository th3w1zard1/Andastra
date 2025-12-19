namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for placeable objects.
    /// </summary>
    /// <remarks>
    /// Placeable Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe placeable system
    /// - Located via string references: "Placeable" @ 0x007bc530, "Placeable List" @ 0x007bd260
    /// - Script hooks: "OnUsed" @ 0x007be1c4, "ScriptOnUsed" @ 0x007beeb8
    /// - Object events: "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles object events (EVENT_OPEN_OBJECT case 7, EVENT_CLOSE_OBJECT case 6, EVENT_LOCK_OBJECT case 0xd, EVENT_UNLOCK_OBJECT case 0xc)
    /// - Original implementation: Placeables have appearance, useability, locks, inventory, HP
    /// - Placeables can be useable (IsUseable), have inventory (HasInventory), be static (IsStatic)
    /// - Containers can be opened/closed (IsOpen), locked (IsLocked) with lock DC (LockDC)
    /// - Destructible placeables have HP (HitPoints, MaxHitPoints) and hardness (Hardness)
    /// - Script events: OnUsed, OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// </remarks>
    public interface IPlaceableComponent : IComponent
    {
        /// <summary>
        /// Whether the placeable is usable.
        /// </summary>
        bool IsUseable { get; set; }

        /// <summary>
        /// Whether the placeable has been used.
        /// </summary>
        bool HasInventory { get; set; }

        /// <summary>
        /// Whether the placeable is static (cannot be destroyed).
        /// </summary>
        bool IsStatic { get; set; }

        /// <summary>
        /// Whether the placeable has been opened (for containers).
        /// </summary>
        bool IsOpen { get; set; }

        /// <summary>
        /// Whether the placeable is locked.
        /// </summary>
        bool IsLocked { get; set; }

        /// <summary>
        /// The DC required to pick the lock.
        /// </summary>
        int LockDC { get; set; }

        /// <summary>
        /// Key tag required to unlock.
        /// </summary>
        string KeyTag { get; set; }

        /// <summary>
        /// Current HP (for destructible placeables).
        /// </summary>
        int HitPoints { get; set; }

        /// <summary>
        /// Max HP (for destructible placeables).
        /// </summary>
        int MaxHitPoints { get; set; }

        /// <summary>
        /// Hardness for damage reduction.
        /// </summary>
        int Hardness { get; set; }

        /// <summary>
        /// Animation state of the placeable.
        /// </summary>
        int AnimationState { get; set; }

        /// <summary>
        /// Opens the placeable (for containers).
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the placeable.
        /// </summary>
        void Close();

        /// <summary>
        /// Activates the placeable.
        /// </summary>
        void Activate();

        /// <summary>
        /// Deactivates the placeable.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Unlocks the placeable.
        /// </summary>
        void Unlock();
    }
}

