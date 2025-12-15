namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for faction and hostility management.
    /// </summary>
    /// <remarks>
    /// Faction Component Interface:
    /// - Based on swkotor2.exe faction system
    /// - Located via string references: Faction management functions handle hostility relationships
    /// - Original implementation: Factions defined in repute.2da table, relationships determine hostility
    /// - FactionId: Faction identifier from repute.2da (0=Hostile, 1=Friendly, 2=Neutral, etc.)
    /// - Hostility checks: IsHostile, IsFriendly, IsNeutral based on faction relationships
    /// - Temporary hostility can override faction relationships (SetTemporaryHostile)
    /// - Faction relationships used for combat initiation, AI behavior, dialogue checks
    /// </remarks>
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

