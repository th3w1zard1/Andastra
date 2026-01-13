using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Aurora.Systems
{
    /// <summary>
    /// Manages faction relationships and hostility for Aurora engine (Neverwinter Nights, Neverwinter Nights 2).
    /// </summary>
    /// <remarks>
    /// Aurora Faction Manager System:
    /// - Based on nwmain.exe: CFactionManager class and CNWSFaction class
    /// - Located via string references: "FactionID" @ 0x140de5930, "FactionName" @ 0x140dda160
    /// - "FactionParentID" @ 0x140dda170, "FactionGlobal" @ 0x140dda180
    /// - "FactionID1" @ 0x140dda190, "FactionID2" @ 0x140dda1a0, "FactionRep" @ 0x140dda1b0
    /// - "FactionList" @ 0x140defe58, "FACTIONREP" @ 0x140dfc4e0, "Faction: " @ 0x140e421f8
    /// - Error: "Unable to load faction table!" @ 0x140defe38, "Source faction id invalid" @ 0x140de1500
    /// - CFactionManager::GetFaction @ 0x140357900 (gets faction by ID)
    /// - GetStandardFactionReputation @ 0x1403d5700 (nwmain.exe: gets reputation between two factions)
    /// - CNWSFaction constructor @ 0x1404ad3e0 (CNWSFaction::CNWSFaction)
    /// - Original implementation: 
    ///   1. CFactionManager manages all factions and their relationships
    ///   2. CNWSFaction represents individual faction with member list and reputation matrix
    ///   3. Faction relationships stored in faction table (similar to repute.2da but Aurora-specific format)
    ///   4. Faction reputation values determine hostility (0-10 = hostile, 11-89 = neutral, 90-100 = friendly)
    ///   5. Personal reputation can override faction reputation for specific entity pairs
    ///   6. Temporary hostility tracked per-entity (stored in TemporaryHostileTargets HashSet from base class)
    ///   7. CFactionManager::GetNPCFactionReputation gets reputation between two factions
    ///   8. GetStandardFactionReputation checks personal reputation overrides before faction reputation
    /// - Faction table format: Aurora-specific 2DA table (faction.2da) with FactionID1, FactionID2, FactionRep columns
    /// - Faction IDs: Integer identifiers (0-255 range), defined in faction.2da
    /// - Standard factions: Player (1), Hostile (2), Commoner (3), Merchant (4), etc.
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
    public class AuroraFactionManager
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
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 uses threshold 10
        /// </summary>
        public const int HostileThreshold = 10;

        /// <summary>
        /// Threshold above which factions are friendly.
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 uses threshold 90
        /// </summary>
        public const int FriendlyThreshold = 90;

        /// <summary>
        /// Initializes a new instance of the Aurora faction manager.
        /// </summary>
        /// <param name="world">The world instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if world is null.</exception>
        public AuroraFactionManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionReputation = new Dictionary<int, Dictionary<int, int>>();
            _personalReputation = new Dictionary<uint, Dictionary<uint, int>>();
            _temporaryHostility = new Dictionary<uint, HashSet<uint>>();

            // Initialize default faction relationships
            // Based on nwmain.exe: CFactionManager initialization loads default faction relationships
            InitializeDefaultFactions();
        }

        /// <summary>
        /// Initializes default faction relationships.
        /// Based on nwmain.exe: CFactionManager initialization sets up default faction relationships
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
        /// Based on nwmain.exe: CFactionManager::GetNPCFactionReputation (called from GetStandardFactionReputation @ 0x1403d5700)
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
        /// Based on nwmain.exe: CFactionManager::SetFactionReputation (sets reputation in faction table)
        /// </summary>
        /// <param name="sourceFaction">The source faction ID.</param>
        /// <param name="targetFaction">The target faction ID.</param>
        /// <param name="reputation">The reputation value (0-100).</param>
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
        /// Based on nwmain.exe: CFactionManager::AdjustFactionReputation (adjusts reputation in faction table)
        /// </summary>
        /// <param name="sourceFaction">The source faction ID.</param>
        /// <param name="targetFaction">The target faction ID.</param>
        /// <param name="adjustment">The reputation adjustment (can be negative).</param>
        public void AdjustFactionReputation(int sourceFaction, int targetFaction, int adjustment)
        {
            int current = GetFactionReputation(sourceFaction, targetFaction);
            SetFactionReputation(sourceFaction, targetFaction, current + adjustment);
        }

        /// <summary>
        /// Gets the effective reputation between two entities.
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 (nwmain.exe)
        /// Original implementation:
        ///   1. Checks temporary hostility first (returns 0 if temporarily hostile)
        ///   2. Checks personal reputation overrides (stored in creature's personal reputation list)
        ///   3. Falls back to faction reputation (from CFactionManager::GetNPCFactionReputation)
        ///   4. Returns reputation value (0-100, where 0-10 = hostile, 11-89 = neutral, 90-100 = friendly)
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>Reputation value (0-100).</returns>
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
            // Based on nwmain.exe: GetStandardFactionReputation checks temporary hostility before reputation
            if (IsTemporarilyHostile(source, target))
            {
                return 0;
            }

            // Check personal reputation override
            // Based on nwmain.exe: GetStandardFactionReputation checks personal reputation overrides
            // Personal reputation is stored in creature's personal reputation list and overrides faction reputation
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
            // Based on nwmain.exe: GetStandardFactionReputation calls CFactionManager::GetNPCFactionReputation
            IFactionComponent sourceFaction = source.GetComponent<IFactionComponent>();
            IFactionComponent targetFaction = target.GetComponent<IFactionComponent>();

            int sourceFactionId = sourceFaction != null ? sourceFaction.FactionId : 0;
            int targetFactionId = targetFaction != null ? targetFaction.FactionId : 0;

            return GetFactionReputation(sourceFactionId, targetFactionId);
        }

        /// <summary>
        /// Sets personal reputation between two entities.
        /// Based on nwmain.exe: CNWSCreature::SetPersonalReputation (sets personal reputation override)
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <param name="reputation">The reputation value (0-100).</param>
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
        /// Based on nwmain.exe: CNWSCreature::ClearPersonalReputation (clears personal reputation override)
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
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 returns true if reputation <= 10
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="target">The target entity.</param>
        /// <returns>True if hostile, false otherwise.</returns>
        public bool IsHostile(IEntity source, IEntity target)
        {
            return GetReputation(source, target) <= HostileThreshold;
        }

        /// <summary>
        /// Checks if source is friendly to target.
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 returns true if reputation >= 90
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
        /// Based on nwmain.exe: GetStandardFactionReputation @ 0x1403d5700 returns true if reputation > 10 and < 90
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
        /// Based on nwmain.exe: CNWSCreature::SetTemporaryHostile (sets temporary hostility flag)
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
        /// Based on nwmain.exe: CNWSCreature::IsTemporarilyHostile (checks temporary hostility flag)
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
        /// Based on nwmain.exe: CNWSCreature::ClearTemporaryHostility (clears temporary hostility flags)
        /// </summary>
        /// <param name="entity">The entity to clear hostility for.</param>
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
        /// Based on nwmain.exe: CFactionManager::ClearAllTemporaryHostility (clears all temporary hostility)
        /// </summary>
        public void ClearAllTemporaryHostility()
        {
            _temporaryHostility.Clear();
        }

        /// <summary>
        /// Processes an attack event, updating faction relationships.
        /// Based on nwmain.exe: CNWSCreature::OnAttack (updates faction relationships on attack)
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="target">The target entity.</param>
        public void OnAttack(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Set temporary hostility
            // Based on nwmain.exe: CNWSCreature::OnAttack sets temporary hostility immediately
            SetTemporaryHostile(target, attacker, true);

            // Optionally propagate to faction members
            // Based on nwmain.exe: CNWSCreature::OnAttack propagates hostility to faction members
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
        /// Based on nwmain.exe: CFactionManager::GetHostileEntities (gets all hostile entities)
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
        /// Based on nwmain.exe: CFactionManager::GetFriendlyEntities (gets all friendly entities)
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

        /// <summary>
        /// Loads faction data from faction.2da table (Aurora-specific format).
        /// Based on nwmain.exe: CFactionManager::LoadFactionTable (loads faction.2da table)
        /// Located via string references: "Unable to load faction table!" @ 0x140defe38
        /// Original implementation: Loads faction.2da table with FactionID1, FactionID2, FactionRep columns
        /// </summary>
        /// <param name="factionData">2DA data rows from faction.2da table.</param>
        public void LoadFromFaction2DA(IEnumerable<Dictionary<string, string>> factionData)
        {
            // Clear existing faction data (keep defaults)
            // faction.2da format: FactionID1, FactionID2, FactionRep
            // Based on nwmain.exe: CFactionManager::LoadFactionTable loads faction.2da table
            foreach (Dictionary<string, string> row in factionData)
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

    /// <summary>
    /// Standard factions from faction.2da (Aurora-specific).
    /// </summary>
    /// <remarks>
    /// Aurora has several standard factions:
    /// - Player (usually faction 1)
    /// - Hostile (always hostile to player)
    /// - Commoner (neutral NPCs)
    /// - Various enemy factions
    /// Based on nwmain.exe: Standard faction IDs from faction.2da table
    /// </remarks>
    public static class StandardFactions
    {
        public const int Player = 1;
        public const int Hostile = 2;
        public const int Commoner = 3;
        public const int Merchant = 4;
        // Add more as needed from faction.2da
    }
}

