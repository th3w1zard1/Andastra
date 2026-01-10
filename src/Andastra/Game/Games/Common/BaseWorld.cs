using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Runtime.Core.AI;
using Andastra.Runtime.Core.Animation;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Perception;
using Andastra.Runtime.Core.Templates;
using Andastra.Runtime.Core.Triggers;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of world management shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base World Implementation:
    /// - Common entity container and state manager across all engines
    /// - Provides entity lookup by ID, tag, and type
    /// - Manages area-entity relationships
    ///
    /// Based on reverse engineering of multiple BioWare engines.
    /// Common entity and world management patterns identified across all engines.
    /// Common entity structure: ObjectId, Tag, ObjectType, AreaId
    ///
    /// Common functionality across engines:
    /// - Entity registration and lookup by ObjectId (O(1))
    /// - Tag-based lookup (case-insensitive, supports nth occurrence)
    /// - ObjectType-based enumeration
    /// - Area-entity relationships (entities belong to areas)
    /// - Spatial queries (GetEntitiesInRadius)
    /// - Entity lifecycle management (add/remove)
    /// - Serialization support for save/load
    /// </remarks>
    [PublicAPI]
    public abstract class BaseWorld : IWorld
    {
        protected readonly Dictionary<uint, IEntity> _entitiesById = new Dictionary<uint, IEntity>();
        protected readonly Dictionary<uint, IArea> _areasById = new Dictionary<uint, IArea>();
        protected readonly Dictionary<string, List<IEntity>> _entitiesByTag = new Dictionary<string, List<IEntity>>(StringComparer.OrdinalIgnoreCase);
        protected readonly Dictionary<ObjectType, List<IEntity>> _entitiesByType = new Dictionary<ObjectType, List<IEntity>>();
        protected readonly Dictionary<IArea, uint> _areaIds = new Dictionary<IArea, uint>();
        protected readonly IEventBus _eventBus;

        // Area ID assignment: Areas get sequential IDs starting from 0x7F000010
        // Common across all engines: Areas assigned ObjectIds similar to entities
        // Area ObjectId range: 0x7F000010+ (special object ID range for areas)
        private static uint _nextAreaId = 0x7F000010;

        protected BaseWorld(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// The event bus for this world.
        /// </summary>
        public IEventBus EventBus => _eventBus;

        /// <summary>
        /// The current area.
        /// </summary>
        public abstract IArea CurrentArea { get; set; }

        /// <summary>
        /// The current module.
        /// </summary>
        public abstract Andastra.Runtime.Core.Interfaces.IModule CurrentModule { get; set; }

        /// <summary>
        /// The simulation time manager.
        /// </summary>
        public abstract ITimeManager TimeManager { get; }

        /// <summary>
        /// The delay scheduler for delayed actions (DelayCommand).
        /// </summary>
        public abstract IDelayScheduler DelayScheduler { get; }

        /// <summary>
        /// The effect system for managing entity effects.
        /// </summary>
        public abstract Andastra.Runtime.Core.Combat.EffectSystem EffectSystem { get; }

        /// <summary>
        /// The perception system for sight/hearing checks.
        /// </summary>
        public abstract Andastra.Runtime.Core.Perception.PerceptionSystem PerceptionSystem { get; }

        /// <summary>
        /// The combat system for combat resolution.
        /// </summary>
        public abstract Andastra.Runtime.Core.Combat.CombatSystem CombatSystem { get; }

        /// <summary>
        /// The trigger system for trigger volume events.
        /// </summary>
        public abstract Andastra.Runtime.Core.Triggers.TriggerSystem TriggerSystem { get; }

        /// <summary>
        /// The AI controller for NPC behavior.
        /// </summary>
        public abstract Andastra.Runtime.Core.AI.AIController AIController { get; }

        /// <summary>
        /// The animation system for updating entity animations.
        /// </summary>
        public abstract Andastra.Runtime.Core.Animation.AnimationSystem AnimationSystem { get; }

        /// <summary>
        /// The game data provider for accessing engine-agnostic game data tables.
        /// </summary>
        /// <remarks>
        /// Provides access to game data tables (2DA files) for looking up creature properties,
        /// appearance data, and other game configuration data.
        /// Engine-specific implementations handle the actual table loading and lookup.
        /// </remarks>
        public abstract Core.Interfaces.IGameDataProvider GameDataProvider { get; }


        /// <summary>
        /// Gets an entity by its unique ObjectId.
        /// </summary>
        /// <remarks>
        /// O(1) lookup by ObjectId.
        /// Returns null if entity doesn't exist or is invalid.
        /// Common across all engines.
        /// </remarks>
        public virtual IEntity GetEntity(uint objectId)
        {
            return _entitiesById.TryGetValue(objectId, out var entity) && entity.IsValid ? entity : null;
        }

        /// <summary>
        /// Gets an entity by tag.
        /// </summary>
        /// <remarks>
        /// Case-insensitive tag lookup.
        /// Supports nth occurrence for multiple entities with same tag.
        /// Common across all engines.
        /// </remarks>
        public virtual IEntity GetEntityByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag) || !_entitiesByTag.TryGetValue(tag, out var entities))
                return null;

            // Find nth valid entity
            int count = 0;
            foreach (var entity in entities)
            {
                if (entity.IsValid)
                {
                    if (count == nth)
                        return entity;
                    count++;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all entities of a specific type.
        /// </summary>
        /// <remarks>
        /// Returns all valid entities of the specified ObjectType.
        /// Common across all engines.
        /// </remarks>
        public virtual IEnumerable<IEntity> GetEntities(ObjectType objectType)
        {
            if (!_entitiesByType.TryGetValue(objectType, out var entities))
                return Array.Empty<IEntity>();

            return entities.FindAll(e => e.IsValid);
        }

        /// <summary>
        /// Gets all entities of a specific type (IWorld interface method).
        /// </summary>
        public virtual IEnumerable<IEntity> GetEntitiesOfType(ObjectType type)
        {
            return GetEntities(type);
        }

        /// <summary>
        /// Gets entities within a radius of a point.
        /// </summary>
        /// <remarks>
        /// Spatial query for entities near a position.
        /// Engine-specific implementations may use different spatial partitioning.
        /// Common interface across all engines.
        /// </remarks>
        public abstract IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All);

        /// <summary>
        /// Gets all entities in the world.
        /// </summary>
        /// <remarks>
        /// Returns all valid entities regardless of type.
        /// Common across all engines.
        /// </remarks>
        public virtual IEnumerable<IEntity> GetAllEntities()
        {
            foreach (var entity in _entitiesById.Values)
            {
                if (entity.IsValid)
                    yield return entity;
            }
        }

        /// <summary>
        /// Gets an area by its AreaId.
        /// </summary>
        /// <remarks>
        /// O(1) lookup by AreaId.
        /// Returns null if area doesn't exist.
        /// Common across all engines.
        /// </remarks>
        public virtual IArea GetArea(uint areaId)
        {
            return _areasById.TryGetValue(areaId, out var area) ? area : null;
        }

        /// <summary>
        /// Gets all registered areas in the world.
        /// </summary>
        /// <remarks>
        /// Returns all areas that have been registered with the world via RegisterArea.
        /// Common across all engines: Areas are registered when loaded or set as current area.
        /// Used by GetAreaByTag to search through all loaded areas.
        /// </remarks>
        public virtual IEnumerable<IArea> GetAllAreas()
        {
            return _areasById.Values;
        }

        /// <summary>
        /// Registers an area with the world and assigns it an AreaId.
        /// </summary>
        /// <remarks>
        /// Assigns sequential AreaId starting from 0x7F000010.
        /// Common across all engines.
        /// </remarks>
        public virtual void RegisterArea(IArea area)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));

            // If area is already registered, don't re-register
            if (_areaIds.ContainsKey(area))
                return;

            // Assign AreaId to area
            uint areaId = _nextAreaId++;
            _areaIds[area] = areaId;
            _areasById[areaId] = area;
        }

        /// <summary>
        /// Unregisters an area from the world.
        /// </summary>
        /// <remarks>
        /// Removes area from lookup tables when area is unloaded.
        /// Common across all engines.
        /// </remarks>
        public virtual void UnregisterArea(IArea area)
        {
            if (area == null)
                return;

            if (_areaIds.TryGetValue(area, out var areaId))
            {
                _areaIds.Remove(area);
                _areasById.Remove(areaId);
            }
        }

        /// <summary>
        /// Gets the AreaId for an area.
        /// </summary>
        /// <remarks>
        /// Returns the AreaId assigned to an area, or 0 if area is not registered.
        /// Common across all engines.
        /// </remarks>
        public virtual uint GetAreaId(IArea area)
        {
            if (area == null)
                return 0;

            return _areaIds.TryGetValue(area, out var areaId) ? areaId : 0;
        }

        /// <summary>
        /// Gets the ModuleId for a module.
        /// </summary>
        /// <remarks>
        /// Returns the ModuleId assigned to a module, or 0 if module is not registered.
        /// Module ObjectId: Fixed value 0x7F000002 (special object ID for module)
        /// Common across all engines: Modules use fixed ObjectId (0x7F000002) for script references
        /// Based on swkotor2.exe: Module object ID constant
        /// Located via string references: "GetModule" NWScript function, module object references
        /// </remarks>
        public virtual uint GetModuleId(Andastra.Runtime.Core.Interfaces.IModule module)
        {
            if (module == null)
                return 0;

            // Module is registered if it's the current module
            // Module ObjectId is fixed at 0x7F000002 (common across all engines)
            if (module == CurrentModule)
            {
                return 0x7F000002;
            }

            return 0;
        }

        /// <summary>
        /// Creates a new entity from a template.
        /// </summary>
        public abstract IEntity CreateEntity(IEntityTemplate template, Vector3 position, float facing);

        /// <summary>
        /// Creates a new entity of the specified type.
        /// </summary>
        public abstract IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing);

        /// <summary>
        /// Destroys an entity by object ID.
        /// </summary>
        public abstract void DestroyEntity(uint objectId);

        /// <summary>
        /// Registers an entity with the world.
        /// </summary>
        public virtual void RegisterEntity(IEntity entity)
        {
            AddEntity(entity);
        }

        /// <summary>
        /// Unregisters an entity from the world.
        /// </summary>
        public virtual void UnregisterEntity(IEntity entity)
        {
            RemoveEntity(entity);
        }

        /// <summary>
        /// Adds an entity to the world.
        /// </summary>
        /// <remarks>
        /// Registers entity in lookup tables.
        /// Sets entity's world reference.
        /// Common across all engines.
        /// </remarks>
        protected virtual void AddEntity(IEntity entity)
        {
            if (entity == null || !entity.IsValid)
                return;

            // Add to ID lookup
            _entitiesById[entity.ObjectId] = entity;

            // Add to tag lookup
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                if (!_entitiesByTag.TryGetValue(entity.Tag, out var tagList))
                {
                    tagList = new List<IEntity>();
                    _entitiesByTag[entity.Tag] = tagList;
                }
                tagList.Add(entity);
            }

            // Add to type lookup
            if (!_entitiesByType.TryGetValue(entity.ObjectType, out var typeList))
            {
                typeList = new List<IEntity>();
                _entitiesByType[entity.ObjectType] = typeList;
            }
            typeList.Add(entity);

            // Set world reference
            entity.World = this;

            // Set entity's AreaId based on current area
            // Common across all engines: Entities store AreaId of area they belong to
            // Entity AreaId is set when entity is registered to world
            if (CurrentArea != null)
            {
                uint areaId = GetAreaId(CurrentArea);
                if (areaId != 0)
                {
                    entity.AreaId = areaId;
                }
            }

            OnEntityAdded(entity);
        }

        /// <summary>
        /// Removes an entity from the world.
        /// </summary>
        /// <remarks>
        /// Unregisters entity from lookup tables.
        /// Clears entity's world reference.
        /// Common across all engines.
        /// </remarks>
        protected virtual void RemoveEntity(IEntity entity)
        {
            if (entity == null)
                return;

            OnEntityRemoving(entity);

            // Remove from ID lookup
            _entitiesById.Remove(entity.ObjectId);

            // Remove from tag lookup
            if (!string.IsNullOrEmpty(entity.Tag) && _entitiesByTag.TryGetValue(entity.Tag, out var tagList))
            {
                tagList.Remove(entity);
                if (tagList.Count == 0)
                    _entitiesByTag.Remove(entity.Tag);
            }

            // Remove from type lookup
            if (_entitiesByType.TryGetValue(entity.ObjectType, out var typeList))
            {
                typeList.Remove(entity);
                if (typeList.Count == 0)
                    _entitiesByType.Remove(entity.ObjectType);
            }

            // Clear world reference
            entity.World = null;
        }

        /// <summary>
        /// Updates the world state.
        /// </summary>
        /// <remarks>
        /// Updates all entities and systems.
        /// Called once per frame.
        /// Engine-specific subclasses implement update logic.
        /// </remarks>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Called when an entity is added to the world.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific logic when entities are added.
        /// Subclasses can override for additional setup.
        /// </remarks>
        protected virtual void OnEntityAdded(IEntity entity)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when an entity is about to be removed from the world.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific cleanup when entities are removed.
        /// Subclasses can override for additional cleanup.
        /// </remarks>
        protected virtual void OnEntityRemoving(IEntity entity)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Validates world state integrity.
        /// </summary>
        /// <remarks>
        /// Debug function to check lookup table consistency.
        /// Can be called during development to catch bugs.
        /// </remarks>
        public virtual void ValidateIntegrity()
        {
            // Check that all entities in ID lookup are also in type/tag lookups
            foreach (var kvp in _entitiesById)
            {
                var entity = kvp.Value;
                if (!entity.IsValid)
                    continue;

                // Check type lookup
                if (_entitiesByType.TryGetValue(entity.ObjectType, out var typeList))
                {
                    if (!typeList.Contains(entity))
                        throw new InvalidOperationException($"Entity {entity.ObjectId} missing from type lookup");
                }

                // Check tag lookup
                if (!string.IsNullOrEmpty(entity.Tag) && _entitiesByTag.TryGetValue(entity.Tag, out var tagList))
                {
                    if (!tagList.Contains(entity))
                        throw new InvalidOperationException($"Entity {entity.ObjectId} missing from tag lookup");
                }
            }
        }

        /// <summary>
        /// Gets world statistics.
        /// </summary>
        /// <remarks>
        /// Returns counts of entities by type for debugging.
        /// Useful for performance monitoring and debugging.
        /// </remarks>
        public virtual WorldStats GetStats()
        {
            var stats = new WorldStats
            {
                TotalEntities = _entitiesById.Count,
                EntitiesByType = new Dictionary<ObjectType, int>()
            };

            foreach (var kvp in _entitiesByType)
            {
                stats.EntitiesByType[kvp.Key] = kvp.Value.Count(e => e.IsValid);
            }

            return stats;
        }
    }

    /// <summary>
    /// World statistics for debugging and monitoring.
    /// </summary>
    public struct WorldStats
    {
        /// <summary>
        /// Total number of entities in the world.
        /// </summary>
        public int TotalEntities;

        /// <summary>
        /// Number of entities by type.
        /// </summary>
        public Dictionary<ObjectType, int> EntitiesByType;
    }
}
