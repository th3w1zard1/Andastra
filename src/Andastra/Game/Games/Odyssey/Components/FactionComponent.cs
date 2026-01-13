using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Engines.Odyssey.Components
{
    /// <summary>
    /// Odyssey engine-specific implementation of faction component for KOTOR 1 and KOTOR 2.
    /// </summary>
    /// <remarks>
    /// Odyssey Faction Component:
    /// - Inherits common functionality from BaseFactionComponent
    /// - Implements Odyssey-specific faction system using repute.2da table
    /// - Based on swkotor.exe and swkotor2.exe faction systems
    ///
    /// Odyssey-specific details:
    /// - swkotor.exe: Faction component system with repute.2da table (FUN_005b1b90 @ 0x005b1b90 loads faction data)
    /// - swkotor2.exe: Enhanced faction system with repute.2da table (FUN_005fb0f0 @ 0x005fb0f0 loads faction data)
    /// - Located via string references: "FactionID" @ 0x007c40b4 (swkotor2.exe) / 0x0074ae48 (swkotor.exe)
    /// - "Faction" @ 0x007c0ca0 (swkotor2.exe), "FactionList" @ 0x007be604 (swkotor2.exe)
    /// - "FactionRep" @ 0x007c290c (swkotor2.exe), "FACTIONREP" @ 0x007bcec8 (swkotor2.exe)
    /// - "FactionGlobal" @ 0x007c28e0 (swkotor2.exe), "FactionName" @ 0x007c2900 (swkotor2.exe)
    /// - "FactionParentID" @ 0x007c28f0 (swkotor2.exe), "FactionID1" @ 0x007c2924, "FactionID2" @ 0x007c2918
    /// - Error: "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8 (swkotor2.exe)
    /// - Debug: "Faction: " @ 0x007caed0 (swkotor2.exe)
    /// - Original implementation: FactionId references repute.2da row (defines faction relationships)
    /// - Faction relationships stored in repute.2da (FactionID1, FactionID2, FactionRep columns)
    /// - FactionRep values: 0=friendly, 1=enemy, 2=neutral (defines relationship between two factions)
    /// - Personal reputation overrides faction-based (temporary hostility from combat)
    /// - Temporary hostility tracked per-entity (stored in TemporaryHostileTargets HashSet from base class)
    /// - Common factions (StandardFactions enum):
    ///   - 1: Player (FACTION_PLAYER)
    ///   - 2: Hostile (FACTION_HOSTILE, always hostile to player)
    ///   - 3: Commoner (FACTION_COMMONER, neutral)
    ///   - 4: Merchant (FACTION_MERCHANT, friendly)
    /// - FactionManager handles complex faction relationships and reputation lookups
    /// - Based on repute.2da file format documentation
    /// </remarks>
    public class OdysseyFactionComponent : BaseFactionComponent
    {
        private FactionManager _factionManager;

        /// <summary>
        /// Initializes a new instance of the Odyssey faction component.
        /// </summary>
        public OdysseyFactionComponent()
        {
            FactionId = StandardFactions.Commoner; // Default to commoner
        }

        /// <summary>
        /// Initializes a new instance of the Odyssey faction component with a faction manager.
        /// </summary>
        /// <param name="factionManager">The faction manager to use for reputation lookups.</param>
        public OdysseyFactionComponent(FactionManager factionManager) : this()
        {
            _factionManager = factionManager;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            // FactionId is set during entity creation
            base.OnAttach();
        }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        /// <param name="other">The other entity to check hostility against.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        public override bool IsHostile(IEntity other)
        {
            if (other == null || other == Owner)
            {
                return false;
            }

            // Check temporary hostility first (from base class)
            if (TemporaryHostileTargets.Contains(other.ObjectId))
            {
                return true;
            }

            // Use faction manager if available
            if (_factionManager != null)
            {
                return _factionManager.IsHostile(Owner, other);
            }

            // Fall back to simple faction comparison
            IFactionComponent otherFaction = other.GetComponent<IFactionComponent>();
            if (otherFaction == null)
            {
                return false;
            }

            // Same faction = friendly
            if (FactionId == otherFaction.FactionId)
            {
                return false;
            }

            // Hostile faction is always hostile
            if (FactionId == StandardFactions.Hostile || otherFaction.FactionId == StandardFactions.Hostile)
            {
                return true;
            }

            // Player vs hostile
            if ((FactionId == StandardFactions.Player && otherFaction.FactionId == StandardFactions.Hostile) ||
                (FactionId == StandardFactions.Hostile && otherFaction.FactionId == StandardFactions.Player))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if this entity is friendly to another.
        /// </summary>
        /// <param name="other">The other entity to check friendliness against.</param>
        /// <returns>True if friendly, false otherwise.</returns>
        public override bool IsFriendly(IEntity other)
        {
            if (other == null)
            {
                return false;
            }

            if (other == Owner)
            {
                return true;
            }

            // Temporarily hostile = not friendly (from base class)
            if (TemporaryHostileTargets.Contains(other.ObjectId))
            {
                return false;
            }

            // Use faction manager if available
            if (_factionManager != null)
            {
                return _factionManager.IsFriendly(Owner, other);
            }

            // Fall back to simple faction comparison
            IFactionComponent otherFaction = other.GetComponent<IFactionComponent>();
            if (otherFaction == null)
            {
                return false;
            }

            // Same faction = friendly
            return FactionId == otherFaction.FactionId;
        }

        /// <summary>
        /// Sets temporary hostility toward a target.
        /// </summary>
        /// <param name="target">The target entity.</param>
        /// <param name="hostile">True to set as hostile, false to clear hostility.</param>
        public override void SetTemporaryHostile(IEntity target, bool hostile)
        {
            base.SetTemporaryHostile(target, hostile);

            // Also update faction manager if available
            if (_factionManager != null)
            {
                _factionManager.SetTemporaryHostile(Owner, target, hostile);
            }
        }

        /// <summary>
        /// Sets the faction manager reference.
        /// </summary>
        /// <param name="manager">The faction manager to use for reputation lookups.</param>
        public void SetFactionManager(FactionManager manager)
        {
            _factionManager = manager;
        }
    }
}
