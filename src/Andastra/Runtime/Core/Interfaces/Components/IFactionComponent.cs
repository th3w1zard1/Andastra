namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for faction and hostility management.
    /// </summary>
    /// <remarks>
    /// Faction Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe faction system
    /// - Located via string references: "repute.2da" @ 0x007c0a28 (faction reputation table)
    /// - "Faction" @ 0x007c0ca0, "FactionID" @ 0x007c40b4, "FactionName" @ 0x007c2900
    /// - "FactionRep" @ 0x007c290c, "FactionID1" @ 0x007c2924, "FactionID2" @ 0x007c2918
    /// - "FactionParentID" @ 0x007c28f0, "FactionGlobal" @ 0x007c28e0, "FACTIONREP" @ 0x007bcec8
    /// - "FactionList" @ 0x007be604, "Hostile" @ 0x007c2004, "hostile_1" @ 0x007c28cc, "hostile_2" @ 0x007c28b4
    /// - "FORCEHOSTILE" @ 0x007c31f0, "HostileSetting" @ 0x007c30e4, "HostileSkill" @ 0x007c2c70, "HostileFeat" @ 0x007c2e90
    /// - "hostilereticle" @ 0x007cceb8, "hostilereticle2" @ 0x007ccf90, "hostilearrow" @ 0x007ccfa0 (hostile UI elements)
    /// - Error: "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8
    /// - Faction management functions handle hostility relationships between entities
    /// - Original implementation: Factions defined in repute.2da table, relationships determine hostility
    /// - FactionId: Faction identifier from repute.2da (0=Hostile, 1=Friendly, 2=Neutral, etc.)
    /// - Hostility checks: IsHostile, IsFriendly, IsNeutral based on faction relationships from repute.2da
    /// - Temporary hostility can override faction relationships (SetTemporaryHostile) for scripted encounters
    /// - Faction relationships used for combat initiation, AI behavior, dialogue checks
    /// - Original engine: FUN_005226d0 @ 0x005226d0 saves faction relationships in save games
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

