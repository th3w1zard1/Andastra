using System.Collections.Generic;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;
using BioWareEngines.Kotor.Systems;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of faction component for KOTOR.
    /// </summary>
    /// <remarks>
    /// Faction system in KOTOR:
    /// - Based on swkotor2.exe faction system
    /// - Located via string references: "FactionID" @ 0x007c40b4 (faction ID field), "Faction" @ 0x007c0ca0
    /// - "FactionList" @ 0x007be604 (faction list field), "FactionRep" @ 0x007c290c (faction reputation field)
    /// - "FACTIONREP" @ 0x007bcec8 (faction reputation constant), "FactionGlobal" @ 0x007c28e0 (faction global variable)
    /// - "FactionName" @ 0x007c2900 (faction name field), "FactionParentID" @ 0x007c28f0 (faction parent ID)
    /// - "FactionID1" @ 0x007c2924, "FactionID2" @ 0x007c2918 (faction ID fields in repute.2da)
    /// - Error: "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8
    /// - Debug: "Faction: " @ 0x007caed0 (faction debug output)
    /// - Original implementation: FactionId references repute.2da row (defines faction relationships)
    /// - Faction relationships stored in repute.2da (FactionID1, FactionID2, FactionRep columns)
    /// - FactionRep values: 0=friendly, 1=enemy, 2=neutral (defines relationship between two factions)
    /// - Personal reputation overrides faction-based (temporary hostility from combat)
    /// - Temporary hostility tracked per-entity (stored in _temporaryHostileTargets HashSet)
    /// - Common factions (StandardFactions enum):
    ///   - 1: Player (FACTION_PLAYER)
    ///   - 2: Hostile (FACTION_HOSTILE, always hostile to player)
    ///   - 3: Commoner (FACTION_COMMONER, neutral)
    ///   - 4: Merchant (FACTION_MERCHANT, friendly)
    /// - FactionManager handles complex faction relationships and reputation lookups
    /// - Based on repute.2da file format documentation
    /// </remarks>
    public class FactionComponent : IFactionComponent
    {
        private readonly HashSet<uint> _temporaryHostileTargets;
        private FactionManager _factionManager;

        public FactionComponent()
        {
            _temporaryHostileTargets = new HashSet<uint>();
            FactionId = StandardFactions.Commoner; // Default to commoner
        }

        public FactionComponent(FactionManager factionManager) : this()
        {
            _factionManager = factionManager;
        }

        #region IComponent Implementation

        public IEntity Owner { get; set; }

        public void OnAttach()
        {
            // FactionId is set during entity creation
        }

        public void OnDetach()
        {
            // Clear temporary hostility
            _temporaryHostileTargets.Clear();
        }

        #endregion

        #region IFactionComponent Implementation

        /// <summary>
        /// The faction ID (index into repute.2da).
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Checks if this entity is hostile to another.
        /// </summary>
        public bool IsHostile(IEntity other)
        {
            if (other == null || other == Owner)
            {
                return false;
            }

            // Check temporary hostility first
            if (_temporaryHostileTargets.Contains(other.ObjectId))
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
        public bool IsFriendly(IEntity other)
        {
            if (other == null)
            {
                return false;
            }

            if (other == Owner)
            {
                return true;
            }

            // Temporarily hostile = not friendly
            if (_temporaryHostileTargets.Contains(other.ObjectId))
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
        /// Checks if this entity is neutral to another.
        /// </summary>
        public bool IsNeutral(IEntity other)
        {
            return !IsHostile(other) && !IsFriendly(other);
        }

        /// <summary>
        /// Sets temporary hostility toward a target.
        /// </summary>
        public void SetTemporaryHostile(IEntity target, bool hostile)
        {
            if (target == null)
            {
                return;
            }

            if (hostile)
            {
                _temporaryHostileTargets.Add(target.ObjectId);
            }
            else
            {
                _temporaryHostileTargets.Remove(target.ObjectId);
            }

            // Also update faction manager if available
            if (_factionManager != null)
            {
                _factionManager.SetTemporaryHostile(Owner, target, hostile);
            }
        }

        #endregion

        #region Extended Methods

        /// <summary>
        /// Sets the faction manager reference.
        /// </summary>
        public void SetFactionManager(FactionManager manager)
        {
            _factionManager = manager;
        }

        /// <summary>
        /// Clears all temporary hostility.
        /// </summary>
        public void ClearTemporaryHostility()
        {
            _temporaryHostileTargets.Clear();
        }

        /// <summary>
        /// Gets the number of temporarily hostile targets.
        /// </summary>
        public int TemporaryHostileCount
        {
            get { return _temporaryHostileTargets.Count; }
        }

        /// <summary>
        /// Checks if a specific target is temporarily hostile.
        /// </summary>
        public bool IsTemporarilyHostileTo(IEntity target)
        {
            return target != null && _temporaryHostileTargets.Contains(target.ObjectId);
        }

        #endregion
    }
}
