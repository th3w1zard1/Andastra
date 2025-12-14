namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for door entities.
    /// </summary>
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
    }
}

