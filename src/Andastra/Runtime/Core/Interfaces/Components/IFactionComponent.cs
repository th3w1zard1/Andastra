namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for faction and hostility management.
    /// </summary>
    /// <remarks>
    /// Faction Component Interface:
    /// - Common interface for faction and hostility management across all BioWare engines
    /// - Base implementation: BaseFactionComponent (Runtime.Games.Common.Components)
    /// - Engine-specific implementations:
    ///   - Odyssey: OdysseyFactionComponent (swkotor.exe: 0x005b1b90 @ 0x005b1b90, swkotor2.exe: 0x005fb0f0 @ 0x005fb0f0)
    ///   - Aurora: AuroraFactionComponent (nwmain.exe: CNWSFaction @ 0x1404ad3e0, GetFaction @ 0x140357900)
    ///   - Eclipse: EclipseFactionComponent (daorigins.exe, DragonAge2.exe: IsHostile/IsFriendly checks)
    ///   - Infinity: InfinityFactionComponent (, : BioFaction classes)
    /// - Common functionality: FactionId, IsHostile, IsFriendly, IsNeutral, SetTemporaryHostile
    /// - Engine-specific: Faction relationship tables, reputation systems, hostility calculations
    /// - Faction relationships stored in 2DA tables (repute.2da for Odyssey, faction.2da for Aurora, etc.)
    /// - Faction reputation values determine hostility (0-10 = hostile, 11-89 = neutral, 90-100 = friendly)
    /// - Temporary hostility can override faction relationships (SetTemporaryHostile) for scripted encounters
    /// - Personal reputation can override faction reputation for specific entity pairs
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

