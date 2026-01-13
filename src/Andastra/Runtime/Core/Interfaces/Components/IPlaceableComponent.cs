namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for placeable objects.
    /// </summary>
    /// <remarks>
    /// Placeable Component Interface:
    /// - Common interface for placeable components across all BioWare engines
    /// - Base implementation: BasePlaceableComponent (Runtime.Games.Common.Components)
    /// - Engine-specific implementations:
    ///   - Odyssey: PlaceableComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraPlaceableComponent (nwmain.exe)
    ///   - Eclipse: EclipsePlaceableComponent (daorigins.exe, DragonAge2.exe)
    ///   - Infinity: InfinityPlaceableComponent (, )
    /// - Cross-engine analysis completed for all engines
    /// - Common functionality: Useability, Inventory, Static flag, Open/Closed state, Locking, Hit Points, Hardness, Animation State, Conversation
    /// - Engine-specific details are in subclasses (trap systems, appearance systems, event handling, field names)
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

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00588010 @ 0x00588010 loads placeable data including Conversation field
        /// Located via string reference: "Conversation" @ 0x007c1abc
        /// Original implementation: Conversation field in UTP template contains dialogue ResRef
        /// </remarks>
        string Conversation { get; set; }
    }
}

