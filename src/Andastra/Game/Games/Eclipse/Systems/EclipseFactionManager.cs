using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Engines.Eclipse.Systems
{
    /// <summary>
    /// Manages faction relationships and hostility for Eclipse engine (Dragon Age Origins, Dragon Age 2).
    /// </summary>
    /// <remarks>
    /// Eclipse Faction Manager System:
    /// - Based on daorigins.exe and DragonAge2.exe faction systems
    /// - Located via string references: "IsHostile" @ 0x00af7904 (daorigins.exe) / 0x00bf4e84 (DragonAge2.exe)
    /// - "IsConsideredHostile" @ 0x00af6fca (daorigins.exe) / 0x00bf2728 (DragonAge2.exe)
    /// - "GroupHostile" @ 0x00aedc68 (daorigins.exe) / 0x00bf8370 (DragonAge2.exe)
    /// - "ShowAsAllyOnMap" @ 0x00af75c8 (daorigins.exe) / 0x00bf4b1c (DragonAge2.exe)
    /// - "CursorOverHostileNPC" @ 0x00af97d4 (daorigins.exe) / 0x00be9f04 (DragonAge2.exe)
    /// - "CursorOverFriendlyNPC" @ 0x00af9828 (daorigins.exe) / 0x00be9f30 (DragonAge2.exe)
    /// - Original implementation: Eclipse engines use IsHostile/IsConsideredHostile checks for faction relationships
    /// - Eclipse-specific: Hostility may be determined by script flags, plot flags, or other game state
    /// - Faction relationships: Similar to Odyssey/Aurora but may use different underlying data structures
    /// - Personal reputation: Individual entity overrides (stored per entity pair, overrides faction reputation)
    /// - Temporary hostility: Combat-triggered hostility (cleared on combat end or entity death)
    ///
    /// Reputation values (0-100 range, consistent with Odyssey/Aurora):
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
    public class EclipseFactionManager
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

        /// <summary>
        /// Initializes a new instance of the Eclipse faction manager.
        /// </summary>
        /// <param name="world">The world this faction manager belongs to.</param>
        public EclipseFactionManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionReputation = new Dictionary<int, Dictionary<int, int>>();
            _personalReputation = new Dictionary<uint, Dictionary<uint, int>>();
            _temporaryHostility = new Dictionary<uint, HashSet<uint>>();

            // Initialize default faction relationships
            InitializeDefaultFactions();
        }

        /// <summary>
        /// Initializes default faction relationships for Eclipse engines.
        /// </summary>
        /// <remarks>
        /// Eclipse engines (Dragon Age Origins, Dragon Age 2) use similar faction structure to Odyssey.
        /// Default relationships ensure basic hostility/friendliness behavior.
        /// </remarks>
        private void InitializeDefaultFactions()
        {
            // Standard Eclipse factions (similar to Odyssey but Eclipse-specific)
            const int PlayerFaction = 1;
            const int HostileFaction = 2;
            const int NeutralFaction = 3;
            const int FriendlyFaction = 4;

            // Player faction
            SetFactionReputation(PlayerFaction, PlayerFaction, 100);
            SetFactionReputation(PlayerFaction, HostileFaction, 0);
            SetFactionReputation(PlayerFaction, NeutralFaction, 50);
            SetFactionReputation(PlayerFaction, FriendlyFaction, 100);

            // Hostile faction (hostile to everyone except themselves)
            SetFactionReputation(HostileFaction, HostileFaction, 100);
            SetFactionReputation(HostileFaction, PlayerFaction, 0);
            SetFactionReputation(HostileFaction, NeutralFaction, 0);
            SetFactionReputation(HostileFaction, FriendlyFaction, 0);

            // Neutral faction (neutral to most)
            SetFactionReputation(NeutralFaction, NeutralFaction, 100);
            SetFactionReputation(NeutralFaction, PlayerFaction, 50);
            SetFactionReputation(NeutralFaction, HostileFaction, 0);
            SetFactionReputation(NeutralFaction, FriendlyFaction, 80);

            // Friendly faction (friendly to player)
            SetFactionReputation(FriendlyFaction, FriendlyFaction, 100);
            SetFactionReputation(FriendlyFaction, PlayerFaction, 100);
            SetFactionReputation(FriendlyFaction, HostileFaction, 0);
            SetFactionReputation(FriendlyFaction, NeutralFaction, 80);
        }

        /// <summary>
        /// Gets the base reputation between two factions.
        /// </summary>
        /// <param name="sourceFaction">The source faction ID.</param>
        /// <param name="targetFaction">The target faction ID.</param>
        /// <returns>Reputation value (0-100).</returns>
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
        /// <param name="sourceFaction">The source faction ID.</param>
        /// <param name="targetFaction">The target faction ID.</param>
        /// <param name="reputation">The reputation value (0-100, clamped).</param>
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
        /// <param name="sourceFaction">The source faction ID.</param>
        /// <param name="targetFaction">The target faction ID.</param>
        /// <param name="adjustment">The adjustment value (can be negative).</param>
        public void AdjustFactionReputation(int sourceFaction, int targetFaction, int adjustment)
        {
            int current = GetFactionReputation(sourceFaction, targetFaction);
            SetFactionReputation(sourceFaction, targetFaction, current + adjustment);
        }

        /// <summary>
        /// Gets the effective reputation between two entities.
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>Reputation value (0-100).</returns>
        /// <remarks>
        /// Based on Eclipse engine: IsHostile/IsConsideredHostile checks (daorigins.exe: 0x00af7904, 0x00af6fca)
        /// Checks temporary hostility first, then personal reputation, then faction reputation.
        /// </remarks>
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
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <param name="reputation">The reputation value (0-100, clamped).</param>
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
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
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
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        /// <remarks>
        /// Based on Eclipse engine: IsHostile check (daorigins.exe: 0x00af7904, DragonAge2.exe: 0x00bf4e84)
        /// </remarks>
        public bool IsHostile(IEntity source, IEntity target)
        {
            return GetReputation(source, target) <= HostileThreshold;
        }

        /// <summary>
        /// Checks if source is friendly to target.
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>True if friendly, false otherwise.</returns>
        public bool IsFriendly(IEntity source, IEntity target)
        {
            return GetReputation(source, target) >= FriendlyThreshold;
        }

        /// <summary>
        /// Checks if source is neutral to target.
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>True if neutral, false otherwise.</returns>
        public bool IsNeutral(IEntity source, IEntity target)
        {
            int rep = GetReputation(source, target);
            return rep > HostileThreshold && rep < FriendlyThreshold;
        }

        /// <summary>
        /// Sets temporary hostility between two entities.
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <param name="hostile">True to set as hostile, false to clear hostility.</param>
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
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>True if temporarily hostile, false otherwise.</returns>
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
        /// <param name="entity">The entity to clear temporary hostility for.</param>
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
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="target">The target entity being attacked.</param>
        /// <remarks>
        /// Based on Eclipse engine: Combat triggers hostility (daorigins.exe, DragonAge2.exe)
        /// Sets temporary hostility and optionally propagates to faction members.
        /// </remarks>
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
        /// <param name="entity">The entity to check.</param>
        /// <returns>Enumerable of hostile entities.</returns>
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
        /// <param name="entity">The entity to check.</param>
        /// <returns>Enumerable of friendly entities.</returns>
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
    }
}

