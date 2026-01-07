using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Combat;
using Andastra.Runtime.Engines.Odyssey.Components;

namespace Andastra.Runtime.Engines.Odyssey.Systems
{
    /// <summary>
    /// Manages encounter spawning when creatures enter encounter areas.
    /// </summary>
    /// <remarks>
    /// Encounter System:
    /// - Based on swkotor2.exe encounter system
    /// - LoadEncounterList @ 0x004e01a0 - Loads encounter list from GIT GFF into area (located via "Encounter List" @ 0x007bd050)
    ///   - Iterates through "Encounter List" GFF list, reads ObjectId, TemplateResRef, position (XPosition, YPosition, ZPosition), geometry polygon, SpawnPointList. Creates encounter entities and adds to area. Validates encounter template exists, loads geometry and spawn points
    /// - SaveEncounterList @ 0x004e2be0 - Saves encounter list from area to GFF save data (located via "Encounter List" @ 0x007bd050)
    /// - Located via string references: "Encounter" @ 0x007bc524 (encounter object type), "Encounter List" @ 0x007bd050 (GFF list field in GIT)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED" @ 0x007bc868 (encounter exhausted event, 0x10)
    /// - Encounter creature management: "CreatureList" @ 0x007c0c80 (creature template list), "RecCreatures" @ 0x007c0cb4 (recommended creatures count)
    /// - "MaxCreatures" @ 0x007c0cc4 (maximum creatures count), "LastSpawnTime" @ 0x007c0c10 (last spawn timestamp)
    /// - "ResetTime" @ 0x007c0cec (encounter reset time), "DifficultyIndex" @ 0x007c0c58 (encounter difficulty)
    /// - Error messages:
    ///   - "Problem loading encounter with tag '%s'. It has geometry, but no vertices. Skipping." @ 0x007c0ae0
    ///   - "Encounter template %s doesn't exist.\n" @ 0x007c0df0
    /// - Encounters spawn creatures when hostile creatures enter the encounter polygon area
    /// - SpawnOption 0 = continuous spawn (spawns as creatures die, maintains RecCreatures count)
    /// - SpawnOption 1 = single-shot spawn (fires once when entered, spawns RecCreatures up to MaxCreatures)
    /// - PlayerOnly: if true, only player characters can trigger spawns (checks isPlayerCheck callback)
    /// - Faction: encounter only spawns for creatures hostile to this faction (checks faction reputation)
    /// - Encounter geometry must have at least 3 vertices (polygon check for ContainsPoint)
    /// - SpawnPointList defines spawn positions within encounter area (spawns creatures at random spawn points)
    /// - Reset logic: If Reset flag is true, encounter resets after ResetTime seconds (allows respawning)
    /// - Exhausted state: Encounter becomes exhausted when MaxCreatures reached (fires OnExhausted script)
    /// - Single spawn: Creature templates with SingleSpawn flag can only spawn once per encounter
    /// - Based on UTE file format (GFF with "UTE " signature) documentation
    /// </remarks>
    public class EncounterSystem
    {
        private readonly IWorld _world;
        private readonly FactionManager _factionManager;
        private readonly List<IEntity> _encounters;
        private readonly Dictionary<uint, HashSet<uint>> _creaturesInEncounter; // encounterId -> set of creature IDs
        private readonly Action<IEntity, ScriptEvent, IEntity> _scriptExecutor;
        private readonly Loading.EntityFactory _entityFactory;
        private readonly Loading.ModuleLoader _moduleLoader;
        private readonly Func<IEntity, bool> _isPlayerCheck;
        private readonly Func<Andastra.Parsing.Common.Module> _getCurrentModule;

        public EncounterSystem(IWorld world, FactionManager factionManager)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionManager = factionManager ?? throw new ArgumentNullException("factionManager");
            _encounters = new List<IEntity>();
            _creaturesInEncounter = new Dictionary<uint, HashSet<uint>>();
            _entityFactory = new Loading.EntityFactory();
        }

        public EncounterSystem(IWorld world, FactionManager factionManager, Action<IEntity, ScriptEvent, IEntity> scriptExecutor, Loading.ModuleLoader moduleLoader, Func<IEntity, bool> isPlayerCheck = null, Func<Andastra.Parsing.Common.Module> getCurrentModule = null)
            : this(world, factionManager)
        {
            _scriptExecutor = scriptExecutor;
            _moduleLoader = moduleLoader;
            _isPlayerCheck = isPlayerCheck;
            _getCurrentModule = getCurrentModule;
        }

        /// <summary>
        /// Registers an encounter entity.
        /// </summary>
        public void RegisterEncounter(IEntity encounter)
        {
            if (encounter == null)
            {
                return;
            }

            if (!_encounters.Contains(encounter))
            {
                _encounters.Add(encounter);
                _creaturesInEncounter[encounter.ObjectId] = new HashSet<uint>();
            }
        }

        /// <summary>
        /// Unregisters an encounter entity.
        /// </summary>
        public void UnregisterEncounter(IEntity encounter)
        {
            if (encounter == null)
            {
                return;
            }

            _encounters.Remove(encounter);
            _creaturesInEncounter.Remove(encounter.ObjectId);
        }

        /// <summary>
        /// Updates encounter system - checks for creatures entering/leaving encounters and spawns creatures.
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (IEntity encounter in _encounters)
            {
                EncounterComponent encounterComp = encounter.GetComponent<EncounterComponent>();
                if (encounterComp == null || !encounterComp.Active)
                {
                    continue;
                }

                // Update time since spawn for reset logic
                if (encounterComp.HasSpawned)
                {
                    encounterComp.TimeSinceSpawn += deltaTime;
                }

                // Check for creatures entering/leaving encounter area
                CheckEncounterArea(encounter, encounterComp);

                // Handle continuous spawning (if spawn option is 0)
                if (encounterComp.SpawnOption == 0 && encounterComp.HasSpawned && !encounterComp.IsExhausted)
                {
                    // Check if we need to spawn more creatures (if some have died)
                    int currentSpawnCount = encounterComp.SpawnedCreatures.Count;
                    int aliveCount = CountAliveSpawnedCreatures(encounterComp);

                    if (aliveCount < encounterComp.RecCreatures && currentSpawnCount < encounterComp.MaxCreatures)
                    {
                        SpawnEncounterCreatures(encounter, encounterComp, encounterComp.RecCreatures - aliveCount);
                    }
                }

                // Handle reset logic
                if (encounterComp.Reset && encounterComp.HasSpawned && encounterComp.TimeSinceSpawn >= encounterComp.ResetTime)
                {
                    ResetEncounter(encounter, encounterComp);
                }
            }
        }

        /// <summary>
        /// Checks for creatures entering/leaving encounter area and triggers spawns.
        /// </summary>
        private void CheckEncounterArea(IEntity encounter, EncounterComponent encounterComp)
        {
            if (encounterComp.Vertices.Count < 3)
            {
                return; // Invalid geometry
            }

            HashSet<uint> currentCreatures = _creaturesInEncounter[encounter.ObjectId];
            HashSet<uint> newCreatures = new HashSet<uint>();

            // Check all creatures in the world (we'll filter by area later)
            if (_world == null)
            {
                return;
            }

            foreach (IEntity creature in _world.GetEntitiesOfType(ObjectType.Creature))
            {
                if (creature.ObjectType != ObjectType.Creature)
                {
                    continue;
                }

                ITransformComponent transform = creature.GetComponent<ITransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                // Check if creature is inside encounter area
                if (encounterComp.ContainsPoint(transform.Position))
                {
                    newCreatures.Add(creature.ObjectId);

                    // Check if this is a new entry
                    if (!currentCreatures.Contains(creature.ObjectId))
                    {
                        OnCreatureEntered(encounter, encounterComp, creature);
                    }
                }
            }

            // Check for creatures that left
            foreach (uint creatureId in currentCreatures)
            {
                if (!newCreatures.Contains(creatureId))
                {
                    IEntity creature = _world.GetEntity(creatureId);
                    if (creature != null)
                    {
                        OnCreatureExited(encounter, encounterComp, creature);
                    }
                }
            }

            // Update tracked creatures
            currentCreatures.Clear();
            foreach (uint creatureId in newCreatures)
            {
                currentCreatures.Add(creatureId);
            }
        }

        /// <summary>
        /// Called when a creature enters an encounter area.
        /// </summary>
        private void OnCreatureEntered(IEntity encounter, EncounterComponent encounterComp, IEntity creature)
        {
            // Check if encounter should spawn for this creature
            if (!ShouldSpawnForCreature(encounter, encounterComp, creature))
            {
                return;
            }

            // Check if already spawned (for single-shot encounters)
            if (encounterComp.SpawnOption == 1 && encounterComp.HasSpawned)
            {
                return; // Single-shot already fired
            }

            // Spawn creatures
            int numToSpawn = Math.Min(encounterComp.RecCreatures, encounterComp.MaxCreatures - encounterComp.SpawnedCreatures.Count);
            if (numToSpawn > 0)
            {
                SpawnEncounterCreatures(encounter, encounterComp, numToSpawn);
                encounterComp.HasSpawned = true;
                encounterComp.TimeSinceSpawn = 0f;
            }

            // Fire OnEntered script
            if (_scriptExecutor != null)
            {
                _scriptExecutor(encounter, ScriptEvent.OnEnter, creature);
            }
        }

        /// <summary>
        /// Called when a creature exits an encounter area.
        /// </summary>
        private void OnCreatureExited(IEntity encounter, EncounterComponent encounterComp, IEntity creature)
        {
            // Fire OnExit script
            _scriptExecutor?.Invoke(encounter, ScriptEvent.OnExit, creature);
        }

        /// <summary>
        /// Checks if encounter should spawn for the given creature.
        /// </summary>
        private bool ShouldSpawnForCreature(IEntity encounter, EncounterComponent encounterComp, IEntity creature)
        {
            // Check PlayerOnly flag
            if (encounterComp.PlayerOnly)
            {
                // Only spawn for player characters
                if (!(_isPlayerCheck?.Invoke(creature) ?? false))
                {
                    return false; // Not a player character
                }
            }

            // Check faction hostility
            IFactionComponent creatureFaction = creature.GetComponent<IFactionComponent>();
            if (creatureFaction == null)
            {
                return false;
            }

            // Encounter only spawns for creatures hostile to encounter's faction
            // Check reputation between factions
            int creatureFactionId = creatureFaction.FactionId;
            int encounterFactionId = encounterComp.Faction;
            int reputation = _factionManager.GetFactionReputation(creatureFactionId, encounterFactionId);
            bool isHostile = reputation <= FactionManager.HostileThreshold;
            return isHostile;
        }

        /// <summary>
        /// Spawns creatures for an encounter.
        /// </summary>
        private void SpawnEncounterCreatures(IEntity encounter, EncounterComponent encounterComp, int count)
        {
            if (encounterComp.CreatureTemplates.Count == 0 || encounterComp.SpawnPoints.Count == 0)
            {
                return; // No templates or spawn points
            }

            IArea area = _world.CurrentArea;
            if (area == null)
            {
                return;
            }

            Random random = new Random();
            int spawned = 0;

            for (int i = 0; i < count && spawned < encounterComp.MaxCreatures; i++)
            {
                // Select random creature template
                int templateIndex = random.Next(encounterComp.CreatureTemplates.Count);
                EncounterCreature template = encounterComp.CreatureTemplates[templateIndex];

                // Check if this template can spawn (single spawn check)
                if (template.SingleSpawn && IsCreatureTypeSpawned(encounterComp, template.ResRef))
                {
                    continue; // Already spawned this type
                }

                // Select random spawn point
                int spawnPointIndex = random.Next(encounterComp.SpawnPoints.Count);
                EncounterSpawnPoint spawnPoint = encounterComp.SpawnPoints[spawnPointIndex];

                // Spawn creature from template
                IEntity creature = null;
                if (_moduleLoader != null && _getCurrentModule != null)
                {
                    Andastra.Parsing.Common.Module csharpModule = _getCurrentModule();
                    if (csharpModule != null)
                    {
                        creature = _entityFactory.CreateCreatureFromTemplate(
                            csharpModule,
                            template.ResRef,
                            spawnPoint.Position,
                            spawnPoint.Orientation
                        );

                        if (creature != null)
                        {
                            // Set encounter faction if specified
                            if (encounterComp.Faction > 0)
                            {
                                IFactionComponent factionComp = creature.GetComponent<IFactionComponent>();
                                if (factionComp != null)
                                {
                                    factionComp.FactionId = encounterComp.Faction;
                                }
                            }

                            // Override appearance if specified
                            if (template.Appearance > 0)
                            {
                                if (creature is Core.Entities.Entity entity)
                                {
                                    entity.SetData("Appearance_Type", template.Appearance);
                                }
                            }

                            // Register entity with world
                            _world.RegisterEntity(creature);

                            // Add to area
                            if (area is Andastra.Runtime.Core.Module.RuntimeArea runtimeArea)
                            {
                                runtimeArea.AddEntity(creature);
                            }

                            // Track spawned creature
                            encounterComp.SpawnedCreatures.Add(creature.ObjectId);
                            spawned++;
                        }
                    }
                }

                if (creature == null)
                {
                    Console.WriteLine("[EncounterSystem] Failed to spawn " + template.ResRef + " at " + spawnPoint.Position);
                }
            }

            // Check if exhausted
            if (encounterComp.SpawnedCreatures.Count >= encounterComp.MaxCreatures)
            {
                encounterComp.IsExhausted = true;

                // Fire OnExhausted script
                if (_scriptExecutor != null)
                {
                    _scriptExecutor(encounter, ScriptEvent.OnExhausted, null);
                }
            }
        }

        /// <summary>
        /// Checks if a creature type has already been spawned.
        /// </summary>
        /// <remarks>
        /// Creature Type Spawn Check:
        /// - Based on swkotor2.exe encounter system
        /// - Original implementation: Checks if any spawned creature matches the template ResRef
        /// - SingleSpawn flag prevents spawning the same creature type multiple times
        /// </remarks>
        private bool IsCreatureTypeSpawned(EncounterComponent encounterComp, string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _world == null)
            {
                return false;
            }

            foreach (uint creatureId in encounterComp.SpawnedCreatures)
            {
                IEntity creature = _world.GetEntity(creatureId);
                if (creature != null)
                {
                    CreatureComponent creatureComp = creature.GetComponent<CreatureComponent>();
                    if (creatureComp != null &&
                        string.Equals(creatureComp.TemplateResRef, resRef, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Counts how many spawned creatures are still alive.
        /// </summary>
        private int CountAliveSpawnedCreatures(EncounterComponent encounterComp)
        {
            int alive = 0;
            foreach (uint creatureId in encounterComp.SpawnedCreatures)
            {
                IEntity creature = _world.GetEntity(creatureId);
                if (creature != null)
                {
                    IStatsComponent stats = creature.GetComponent<IStatsComponent>();
                    if (stats != null && stats.CurrentHP > 0)
                    {
                        alive++;
                    }
                }
            }
            return alive;
        }

        /// <summary>
        /// Resets an encounter (allows it to spawn again).
        /// </summary>
        private void ResetEncounter(IEntity encounter, EncounterComponent encounterComp)
        {
            encounterComp.HasSpawned = false;
            encounterComp.TimeSinceSpawn = 0f;
            encounterComp.IsExhausted = false;
            encounterComp.SpawnedCreatures.Clear();
        }

        /// <summary>
        /// Clears all encounters.
        /// </summary>
        public void Clear()
        {
            _encounters.Clear();
            _creaturesInEncounter.Clear();
        }
    }
}

