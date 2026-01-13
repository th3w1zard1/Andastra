using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Odyssey.Systems.PerceptionManager
{
    /// <summary>
    /// Faction reputation/hostility values.
    /// </summary>
    public enum FactionReputation
    {
        Hostile = 0,    // Will attack on sight
        Neutral = 50,   // Will not attack, but not friendly
        Friendly = 100  // Allied
    }

    /// <summary>
    /// Standard factions from repute.2da.
    /// </summary>
    /// <remarks>
    /// KOTOR has several standard factions:
    /// - Player (usually faction 1)
    /// - Hostile (always hostile to player)
    /// - Commoner (neutral NPCs)
    /// - Various enemy factions
    /// </remarks>
    public static class StandardFactions
    {
        public const int Player = 1;
        public const int Hostile = 2;
        public const int Commoner = 3;
        public const int Merchant = 4;
        public const int Gizka = 5;
        // Add more as needed from repute.2da
    }

    /// <summary>
    /// Manages faction relationships and hostility.
    /// </summary>
    /// <remarks>
    /// Faction Manager System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) faction system
    /// - Located via string references: "FactionRep" @ 0x007c290c, "FactionID1" @ 0x007c2924, "FactionID2" @ 0x007c2918
    /// - "FACTIONREP" @ 0x007bcec8, "FactionList" @ 0x007be604, "Faction" @ 0x007c24dc
    /// - "repute.2da" @ 0x007c2900 (faction relationship table file), "FACTIONREP" @ 0x007bcec8 (faction reputation field)
    /// - Original implementation: Faction relationships stored in GFF structures with FactionID, FactionRep fields
    /// - repute.2da: Defines faction relationships in 2DA table format (FactionID1, FactionID2, FactionRep columns)
    /// - Faction IDs: Integer identifiers (0-255 range), defined in repute.2da
    /// - Standard factions: Player (1), Hostile (2), Commoner (3), Merchant (4), Gizka (5), etc.
    /// - Personal reputation: Individual entity overrides (stored per entity pair, overrides faction reputation)
    /// - Temporary hostility: Combat-triggered hostility (cleared on combat end or entity death)
    ///
    /// Reputation values (0-100 range):
    /// - 0-10: Hostile (will attack on sight)
    /// - 11-89: Neutral (will not attack, but not friendly)
    /// - 90-100: Friendly (allied, will assist in combat)
    ///
    /// Combat triggers:
    /// - Attacking a creature makes attacker hostile to target's faction
    /// - Temporary hostility set immediately on attack (SetTemporaryHostile)
    /// - Faction-wide hostility: All members of target's faction become hostile to attacker
    /// - Hostility can be permanent (persists after combat) or temporary (cleared on combat end)
    /// - Personal reputation can override faction reputation for specific entity pairs
    /// </remarks>
    public class FactionManager
    {
        private readonly IWorld _world;

        // Faction to faction reputation matrix
        // _factionReputation[source][target] = reputation (0-100)
        private readonly Dictionary<int, Dictionary<int, int>> _factionReputation;

        // Personal reputation overrides (creature to creature)
        // _personalReputation[sourceId][targetId] = reputation
        private readonly Dictionary<uint, Dictionary<uint, int>> _personalReputation;

        // Temporary hostility flags (cleared on combat end)
        private readonly Dictionary<uint, HashSet<uint>> _temporaryHostility;

        /// <summary>
        /// Threshold below which factions are hostile.
        /// </summary>
        public const int HostileThreshold = 10;

        /// <summary>
        /// Threshold above which factions are friendly.
        /// </summary>
        public const int FriendlyThreshold = 90;

        public FactionManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionReputation = new Dictionary<int, Dictionary<int, int>>();
            _personalReputation = new Dictionary<uint, Dictionary<uint, int>>();
            _temporaryHostility = new Dictionary<uint, HashSet<uint>>();

            // Initialize default faction relationships
            InitializeDefaultFactions();
        }

        /// <summary>
        /// Initializes default faction relationships.
        /// </summary>
        private void InitializeDefaultFactions()
        {
            // Player faction
            SetFactionReputation(StandardFactions.Player, StandardFactions.Player, 100);
            SetFactionReputation(StandardFactions.Player, StandardFactions.Hostile, 0);
            SetFactionReputation(StandardFactions.Player, StandardFactions.Commoner, 50);
            SetFactionReputation(StandardFactions.Player, StandardFactions.Merchant, 80);

            // Hostile faction (hostile to everyone except themselves)
            SetFactionReputation(StandardFactions.Hostile, StandardFactions.Hostile, 100);
            SetFactionReputation(StandardFactions.Hostile, StandardFactions.Player, 0);
            SetFactionReputation(StandardFactions.Hostile, StandardFactions.Commoner, 0);

            // Commoner faction (neutral to most)
            SetFactionReputation(StandardFactions.Commoner, StandardFactions.Commoner, 100);
            SetFactionReputation(StandardFactions.Commoner, StandardFactions.Player, 50);
            SetFactionReputation(StandardFactions.Commoner, StandardFactions.Hostile, 0);

            // Merchant faction (friendly to player)
            SetFactionReputation(StandardFactions.Merchant, StandardFactions.Merchant, 100);
            SetFactionReputation(StandardFactions.Merchant, StandardFactions.Player, 80);
            SetFactionReputation(StandardFactions.Merchant, StandardFactions.Hostile, 0);
        }

        /// <summary>
        /// Gets the base reputation between two factions.
        /// </summary>
        public int GetFactionReputation(int sourceFaction, int targetFaction)
        {
            if (sourceFaction == targetFaction)
            {
                return 100; // Same faction always friendly
            }

            Dictionary<int, int> targetReps;
            if (_factionReputation.TryGetValue(sourceFaction, out targetReps))
            {
                int rep;
                if (targetReps.TryGetValue(targetFaction, out rep))
                {
                    return rep;
                }
            }

            return 50; // Default neutral
        }

        /// <summary>
        /// Sets the base reputation between two factions.
        /// </summary>
        public void SetFactionReputation(int sourceFaction, int targetFaction, int reputation)
        {
            reputation = Math.Max(0, Math.Min(100, reputation));

            if (!_factionReputation.ContainsKey(sourceFaction))
            {
                _factionReputation[sourceFaction] = new Dictionary<int, int>();
            }
            _factionReputation[sourceFaction][targetFaction] = reputation;
        }

        /// <summary>
        /// Adjusts the reputation between two factions.
        /// </summary>
        public void AdjustFactionReputation(int sourceFaction, int targetFaction, int adjustment)
        {
            int current = GetFactionReputation(sourceFaction, targetFaction);
            SetFactionReputation(sourceFaction, targetFaction, current + adjustment);
        }

        /// <summary>
        /// Gets the effective reputation between two entities.
        /// </summary>
        public int GetReputation(IEntity source, IEntity target)
        {
            if (source == null || target == null)
            {
                return 50;
            }

            if (source == target)
            {
                return 100; // Self
            }

            // Check temporary hostility first
            if (IsTemporarilyHostile(source, target))
            {
                return 0;
            }

            // Check personal reputation override
            Dictionary<uint, int> personalReps;
            if (_personalReputation.TryGetValue(source.ObjectId, out personalReps))
            {
                int personalRep;
                if (personalReps.TryGetValue(target.ObjectId, out personalRep))
                {
                    return personalRep;
                }
            }

            // Fall back to faction reputation
            IFactionComponent sourceFaction = source.GetComponent<IFactionComponent>();
            IFactionComponent targetFaction = target.GetComponent<IFactionComponent>();

            int sourceFactionId = sourceFaction != null ? sourceFaction.FactionId : 0;
            int targetFactionId = targetFaction != null ? targetFaction.FactionId : 0;

            return GetFactionReputation(sourceFactionId, targetFactionId);
        }

        /// <summary>
        /// Sets personal reputation between two entities.
        /// </summary>
        public void SetPersonalReputation(IEntity source, IEntity target, int reputation)
        {
            if (source == null || target == null)
            {
                return;
            }

            reputation = Math.Max(0, Math.Min(100, reputation));

            if (!_personalReputation.ContainsKey(source.ObjectId))
            {
                _personalReputation[source.ObjectId] = new Dictionary<uint, int>();
            }
            _personalReputation[source.ObjectId][target.ObjectId] = reputation;
        }

        /// <summary>
        /// Clears personal reputation between two entities.
        /// </summary>
        public void ClearPersonalReputation(IEntity source, IEntity target)
        {
            if (source == null || target == null)
            {
                return;
            }

            Dictionary<uint, int> personalReps;
            if (_personalReputation.TryGetValue(source.ObjectId, out personalReps))
            {
                personalReps.Remove(target.ObjectId);
            }
        }

        /// <summary>
        /// Checks if source is hostile to target.
        /// </summary>
        public bool IsHostile(IEntity source, IEntity target)
        {
            return GetReputation(source, target) <= HostileThreshold;
        }

        /// <summary>
        /// Checks if source is friendly to target.
        /// </summary>
        public bool IsFriendly(IEntity source, IEntity target)
        {
            return GetReputation(source, target) >= FriendlyThreshold;
        }

        /// <summary>
        /// Checks if source is neutral to target.
        /// </summary>
        public bool IsNeutral(IEntity source, IEntity target)
        {
            int rep = GetReputation(source, target);
            return rep > HostileThreshold && rep < FriendlyThreshold;
        }

        /// <summary>
        /// Sets temporary hostility between two entities.
        /// </summary>
        public void SetTemporaryHostile(IEntity source, IEntity target, bool hostile)
        {
            if (source == null || target == null)
            {
                return;
            }

            if (!_temporaryHostility.ContainsKey(source.ObjectId))
            {
                _temporaryHostility[source.ObjectId] = new HashSet<uint>();
            }

            if (hostile)
            {
                _temporaryHostility[source.ObjectId].Add(target.ObjectId);
            }
            else
            {
                _temporaryHostility[source.ObjectId].Remove(target.ObjectId);
            }
        }

        /// <summary>
        /// Checks if source is temporarily hostile to target.
        /// </summary>
        public bool IsTemporarilyHostile(IEntity source, IEntity target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            HashSet<uint> targets;
            if (_temporaryHostility.TryGetValue(source.ObjectId, out targets))
            {
                return targets.Contains(target.ObjectId);
            }
            return false;
        }

        /// <summary>
        /// Clears all temporary hostility for an entity.
        /// </summary>
        public void ClearTemporaryHostility(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _temporaryHostility.Remove(entity.ObjectId);
        }

        /// <summary>
        /// Clears all temporary hostility in the world.
        /// </summary>
        public void ClearAllTemporaryHostility()
        {
            _temporaryHostility.Clear();
        }

        /// <summary>
        /// Processes an attack event, updating faction relationships.
        /// </summary>
        public void OnAttack(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Set temporary hostility
            SetTemporaryHostile(target, attacker, true);

            // Optionally propagate to faction members
            IFactionComponent targetFaction = target.GetComponent<IFactionComponent>();
            if (targetFaction != null)
            {
                // Make the entire target faction hostile to attacker
                foreach (IEntity entity in _world.GetAllEntities())
                {
                    IFactionComponent entityFaction = entity.GetComponent<IFactionComponent>();
                    if (entityFaction != null && entityFaction.FactionId == targetFaction.FactionId)
                    {
                        SetTemporaryHostile(entity, attacker, true);
                    }
                }
            }
        }

        /// <summary>
        /// Gets all entities hostile to the given entity.
        /// </summary>
        public IEnumerable<IEntity> GetHostileEntities(IEntity entity)
        {
            if (entity == null)
            {
                yield break;
            }

            foreach (IEntity other in _world.GetAllEntities())
            {
                if (other != entity && IsHostile(other, entity))
                {
                    yield return other;
                }
            }
        }

        /// <summary>
        /// Gets all entities friendly to the given entity.
        /// </summary>
        public IEnumerable<IEntity> GetFriendlyEntities(IEntity entity)
        {
            if (entity == null)
            {
                yield break;
            }

            foreach (IEntity other in _world.GetAllEntities())
            {
                if (other != entity && IsFriendly(other, entity))
                {
                    yield return other;
                }
            }
        }

        /// <summary>
        /// Loads faction data from repute.2da.
        /// </summary>
        /// <param name="reputeData">2DA data rows</param>
        public void LoadFromRepute2DA(IEnumerable<Dictionary<string, string>> reputeData)
        {
            // Clear existing faction data (keep defaults)
            // repute.2da format: FactionID1, FactionID2, FactionRep
            foreach (Dictionary<string, string> row in reputeData)
            {
                string faction1Str, faction2Str, repStr;
                if (!row.TryGetValue("FactionID1", out faction1Str) ||
                    !row.TryGetValue("FactionID2", out faction2Str) ||
                    !row.TryGetValue("FactionRep", out repStr))
                {
                    continue;
                }

                int faction1, faction2, rep;
                if (!int.TryParse(faction1Str, out faction1) ||
                    !int.TryParse(faction2Str, out faction2) ||
                    !int.TryParse(repStr, out rep))
                {
                    continue;
                }

                SetFactionReputation(faction1, faction2, rep);
            }
        }
    }
}
