namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for faction and hostility management.
    /// </summary>
    public interface IFactionComponent : IComponent
    {
        /// <summary>
        /// The faction ID.
        /// </summary>
        int FactionId { get; set; }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        bool IsHostile(IEntity other);

        /// <summary>
        /// Checks if this entity is friendly to another.
        /// </summary>
        bool IsFriendly(IEntity other);

        /// <summary>
        /// Checks if this entity is neutral to another.
        /// </summary>
        bool IsNeutral(IEntity other);

        /// <summary>
        /// Sets temporary hostility toward a target.
        /// </summary>
        void SetTemporaryHostile(IEntity target, bool hostile);
    }
}

