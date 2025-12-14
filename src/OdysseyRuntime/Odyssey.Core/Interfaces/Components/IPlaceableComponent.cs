namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for placeable objects.
    /// </summary>
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
    }
}

