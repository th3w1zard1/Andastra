using System.Collections.Generic;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Systems;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of faction component for KOTOR.
    /// </summary>
    /// <remarks>
    /// Faction system in KOTOR:
    /// - FactionId references repute.2da
    /// - Personal reputation overrides faction-based
    /// - Temporary hostility from combat
    /// 
    /// Common factions:
    /// - 1: Player
    /// - 2: Hostile (always hostile to player)
    /// - 3: Commoner
    /// - 4: Merchant
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
            // Load faction from entity data if available
            if (Owner != null && Owner.HasData("FactionID"))
            {
                FactionId = Owner.GetData<int>("FactionID", StandardFactions.Commoner);
            }
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
            var otherFaction = other.GetComponent<IFactionComponent>();
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
            var otherFaction = other.GetComponent<IFactionComponent>();
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
