using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.GameSystems
{
    /// <summary>
    /// Handles faction relationships and hostility checks.
    /// </summary>
    /// <remarks>
    /// Factions determine how creatures react to each other.
    /// Reputation values range from 0 (hostile) to 100 (friendly).
    /// </remarks>
    public class FactionSystem
    {
        private readonly Dictionary<int, string> _factionNames;
        private readonly Dictionary<int, Dictionary<int, int>> _factionReputations;
        private readonly Dictionary<uint, Dictionary<uint, int>> _personalReputations;

        /// <summary>
        /// Reputation threshold for hostility.
        /// </summary>
        public const int HostileThreshold = 10;

        /// <summary>
        /// Reputation threshold for friendliness.
        /// </summary>
        public const int FriendlyThreshold = 50;

        // Standard faction IDs (from repute.2da)
        public const int FactionHostile1 = 1;
        public const int FactionFriendly1 = 2;
        public const int FactionMerchant = 3;
        public const int FactionDefender = 4;
        public const int FactionCommoner = 5;
        public const int FactionPC = 6;
        public const int FactionHostile2 = 7;
        public const int FactionFriendly2 = 8;
        public const int FactionInsane = 9;
        public const int FactionGizka = 10;
        public const int FactionPreyLow = 11;
        public const int FactionPreyHigh = 12;
        public const int FactionPredator = 13;
        public const int FactionSurrender1 = 14;
        public const int FactionSurrender2 = 15;

        public FactionSystem()
        {
            _factionNames = new Dictionary<int, string>();
            _factionReputations = new Dictionary<int, Dictionary<int, int>>();
            _personalReputations = new Dictionary<uint, Dictionary<uint, int>>();

            InitializeDefaultFactions();
        }

        private void InitializeDefaultFactions()
        {
            // Initialize standard faction names
            _factionNames[FactionHostile1] = "Hostile_1";
            _factionNames[FactionFriendly1] = "Friendly_1";
            _factionNames[FactionMerchant] = "Merchant";
            _factionNames[FactionDefender] = "Defender";
            _factionNames[FactionCommoner] = "Commoner";
            _factionNames[FactionPC] = "PC";
            _factionNames[FactionHostile2] = "Hostile_2";
            _factionNames[FactionFriendly2] = "Friendly_2";
            _factionNames[FactionInsane] = "Insane";
            _factionNames[FactionGizka] = "Gizka";
            _factionNames[FactionPreyLow] = "Prey_Low";
            _factionNames[FactionPreyHigh] = "Prey_High";
            _factionNames[FactionPredator] = "Predator";
            _factionNames[FactionSurrender1] = "Surrender_1";
            _factionNames[FactionSurrender2] = "Surrender_2";

            // Set default faction relationships
            // PC friendly with most factions, hostile with Hostile factions
            SetDefaultReputation(FactionPC, FactionFriendly1, 100);
            SetDefaultReputation(FactionPC, FactionMerchant, 100);
            SetDefaultReputation(FactionPC, FactionDefender, 100);
            SetDefaultReputation(FactionPC, FactionCommoner, 100);
            SetDefaultReputation(FactionPC, FactionFriendly2, 100);

            SetDefaultReputation(FactionPC, FactionHostile1, 0);
            SetDefaultReputation(FactionPC, FactionHostile2, 0);
            SetDefaultReputation(FactionPC, FactionInsane, 0);

            // Hostile factions hate everyone except themselves
            SetDefaultReputation(FactionHostile1, FactionHostile1, 100);
            SetDefaultReputation(FactionHostile2, FactionHostile2, 100);
            SetDefaultReputation(FactionHostile1, FactionFriendly1, 0);
            SetDefaultReputation(FactionHostile1, FactionPC, 0);

            // Insane hates everyone including itself
            SetDefaultReputation(FactionInsane, FactionInsane, 0);

            // Predator/Prey relationships
            SetDefaultReputation(FactionPredator, FactionPreyLow, 0);
            SetDefaultReputation(FactionPredator, FactionPreyHigh, 0);
        }

        private void SetDefaultReputation(int faction1, int faction2, int reputation)
        {
            if (!_factionReputations.ContainsKey(faction1))
            {
                _factionReputations[faction1] = new Dictionary<int, int>();
            }
            _factionReputations[faction1][faction2] = reputation;

            // Make it bidirectional
            if (!_factionReputations.ContainsKey(faction2))
            {
                _factionReputations[faction2] = new Dictionary<int, int>();
            }
            _factionReputations[faction2][faction1] = reputation;
        }

        /// <summary>
        /// Gets the reputation between two factions.
        /// </summary>
        public int GetFactionReputation(int faction1, int faction2)
        {
            if (faction1 == faction2)
            {
                return 100; // Same faction is always friendly
            }

            Dictionary<int, int> factionReps;
            if (_factionReputations.TryGetValue(faction1, out factionReps))
            {
                int rep;
                if (factionReps.TryGetValue(faction2, out rep))
                {
                    return rep;
                }
            }

            return 50; // Default neutral
        }

        /// <summary>
        /// Sets the reputation between two factions.
        /// </summary>
        public void SetFactionReputation(int faction1, int faction2, int reputation)
        {
            reputation = Math.Max(0, Math.Min(100, reputation));

            if (!_factionReputations.ContainsKey(faction1))
            {
                _factionReputations[faction1] = new Dictionary<int, int>();
            }
            _factionReputations[faction1][faction2] = reputation;
        }

        /// <summary>
        /// Gets the personal reputation between two entities.
        /// </summary>
        public int GetPersonalReputation(IEntity entity1, IEntity entity2)
        {
            if (entity1 == null || entity2 == null || entity1 == entity2)
            {
                return 100;
            }

            Dictionary<uint, int> personalReps;
            if (_personalReputations.TryGetValue(entity1.ObjectId, out personalReps))
            {
                int rep;
                if (personalReps.TryGetValue(entity2.ObjectId, out rep))
                {
                    return rep;
                }
            }

            return -1; // No personal reputation - use faction
        }

        /// <summary>
        /// Sets the personal reputation between two entities.
        /// </summary>
        public void SetPersonalReputation(IEntity entity1, IEntity entity2, int reputation)
        {
            if (entity1 == null || entity2 == null)
            {
                return;
            }

            reputation = Math.Max(0, Math.Min(100, reputation));

            if (!_personalReputations.ContainsKey(entity1.ObjectId))
            {
                _personalReputations[entity1.ObjectId] = new Dictionary<uint, int>();
            }
            _personalReputations[entity1.ObjectId][entity2.ObjectId] = reputation;
        }

        /// <summary>
        /// Clears personal reputation for an entity.
        /// </summary>
        public void ClearPersonalReputation(IEntity entity1, IEntity entity2)
        {
            if (entity1 == null || entity2 == null)
            {
                return;
            }

            Dictionary<uint, int> personalReps;
            if (_personalReputations.TryGetValue(entity1.ObjectId, out personalReps))
            {
                personalReps.Remove(entity2.ObjectId);
            }
        }

        /// <summary>
        /// Gets the effective reputation between two entities.
        /// </summary>
        public int GetReputation(IEntity entity1, IEntity entity2)
        {
            if (entity1 == null || entity2 == null || entity1 == entity2)
            {
                return 100;
            }

            // Check personal reputation first
            int personalRep = GetPersonalReputation(entity1, entity2);
            if (personalRep >= 0)
            {
                return personalRep;
            }

            // Fall back to faction reputation
            var creature1 = entity1.GetComponent<CreatureComponent>();
            var creature2 = entity2.GetComponent<CreatureComponent>();

            int faction1 = creature1 != null ? creature1.FactionId : FactionCommoner;
            int faction2 = creature2 != null ? creature2.FactionId : FactionCommoner;

            return GetFactionReputation(faction1, faction2);
        }

        /// <summary>
        /// Checks if one entity is hostile to another.
        /// </summary>
        public bool IsHostile(IEntity entity1, IEntity entity2)
        {
            int reputation = GetReputation(entity1, entity2);
            return reputation <= HostileThreshold;
        }

        /// <summary>
        /// Checks if one entity is friendly to another.
        /// </summary>
        public bool IsFriendly(IEntity entity1, IEntity entity2)
        {
            int reputation = GetReputation(entity1, entity2);
            return reputation >= FriendlyThreshold;
        }

        /// <summary>
        /// Checks if one entity is neutral to another.
        /// </summary>
        public bool IsNeutral(IEntity entity1, IEntity entity2)
        {
            int reputation = GetReputation(entity1, entity2);
            return reputation > HostileThreshold && reputation < FriendlyThreshold;
        }

        /// <summary>
        /// Adjusts faction reputation.
        /// </summary>
        public void AdjustFactionReputation(int faction1, int faction2, int adjustment)
        {
            int current = GetFactionReputation(faction1, faction2);
            SetFactionReputation(faction1, faction2, current + adjustment);
        }

        /// <summary>
        /// Gets the faction name.
        /// </summary>
        public string GetFactionName(int factionId)
        {
            string name;
            if (_factionNames.TryGetValue(factionId, out name))
            {
                return name;
            }
            return "Faction_" + factionId;
        }

        /// <summary>
        /// Registers a new faction.
        /// </summary>
        public void RegisterFaction(int factionId, string name)
        {
            _factionNames[factionId] = name;
            if (!_factionReputations.ContainsKey(factionId))
            {
                _factionReputations[factionId] = new Dictionary<int, int>();
            }
        }

        /// <summary>
        /// Clears all personal reputations for an entity (on despawn).
        /// </summary>
        public void ClearEntity(uint objectId)
        {
            _personalReputations.Remove(objectId);

            foreach (var personalReps in _personalReputations.Values)
            {
                personalReps.Remove(objectId);
            }
        }
    }
}
