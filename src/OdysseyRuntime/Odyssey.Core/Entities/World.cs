using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Actions;
using Odyssey.Core.AI;
using Odyssey.Core.Animation;
using Odyssey.Core.Combat;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Module;
using Odyssey.Core.Perception;
using Odyssey.Core.Save;
using Odyssey.Core.Triggers;
using Odyssey.Core.Templates;

namespace Odyssey.Core.Entities
{
    /// <summary>
    /// Core world implementation - entity container and state manager.
    /// </summary>
    /// <remarks>
    /// World/Entity Management:
    /// - Based on swkotor2.exe world management system
    /// - Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
    /// - "AreaId" @ 0x007bef48 (entity area association), "Area" @ 0x007be340 (area name)
    /// - Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 used for debug/error logging
    /// - Original engine maintains entity lists by ObjectId, Tag, and ObjectType
    /// - Entity lookup: GetEntityByTag searches by tag string (case-insensitive), GetEntity by ObjectId (O(1) lookup)
    /// - ObjectId is unique 32-bit identifier assigned sequentially (OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001)
    /// - Entity registration: Entities are registered in world with ObjectId, Tag, and ObjectType indices
    /// - Entity serialization: FUN_005226d0 @ 0x005226d0 saves entity state including ObjectId, AreaId, Position, Orientation
    ///   - Function signature: `void FUN_005226d0(void *param_1, void *param_2)`
    ///   - param_1: Entity pointer
    ///   - param_2: GFF structure pointer
    ///   - Writes ObjectId (uint32) via FUN_00413880 with "ObjectId" field name
    ///   - Writes AreaId (uint32) via FUN_00413880 with "AreaId" field name
    ///   - Writes Position (XPosition, YPosition, ZPosition as float) via FUN_00413a00
    ///   - Writes Orientation (XOrientation, YOrientation, ZOrientation as float) via FUN_00413a00
    ///   - Calls FUN_00508200 to save action queue, FUN_00505db0 to save effect list
    /// - Entity deserialization: FUN_005223a0 @ 0x005223a0 loads entity data from GFF
    ///   - Reads ObjectId (uint32) via FUN_00412d40 with "ObjectId" field name (default 0x7f000000)
    ///   - Reads AreaId (uint32) via FUN_00412d40 with "AreaId" field name
    ///   - Reads Position and Orientation from GFF structure
    /// - Area management: Entities belong to areas (AreaId field), areas contain entity lists by type
    /// - Module management: "ModuleList" @ 0x007bdd3c, "ModuleName" @ 0x007bde2c, "LASTMODULE" @ 0x007be1d0
    /// - Module events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// </remarks>
    public class World : IWorld
    {
        private readonly Dictionary<uint, IEntity> _entitiesById;
        private readonly Dictionary<string, List<IEntity>> _entitiesByTag;
        private readonly Dictionary<ObjectType, List<IEntity>> _entitiesByType;
        
        // Based on swkotor2.exe: Tag lookup is case-insensitive
        // Located via string references: "GetObjectByTag" function uses case-insensitive tag comparison
        // Original implementation: Tag matching ignores case differences
        private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;
        private readonly List<IEntity> _allEntities;

        public World()
        {
            _entitiesById = new Dictionary<uint, IEntity>();
            _entitiesByTag = new Dictionary<string, List<IEntity>>(TagComparer);
            _entitiesByType = new Dictionary<ObjectType, List<IEntity>>();
            _allEntities = new List<IEntity>();
            TimeManager = new TimeManager();
            EventBus = new EventBus();
            DelayScheduler = new DelayScheduler();
            CombatSystem = new CombatSystem(this);
            EffectSystem = new EffectSystem(this);
            PerceptionSystem = new PerceptionSystem(this);
            TriggerSystem = new TriggerSystem(this);
            AIController = new AIController(this, CombatSystem);
            AnimationSystem = new AnimationSystem(this);
            // ModuleTransitionSystem will be initialized when SaveSystem and ModuleLoader are available
            ModuleTransitionSystem = null;
        }

        public IArea CurrentArea { get; set; }
        public IModule CurrentModule { get; set; }

        /// <summary>
        /// Sets the current area.
        /// </summary>
        public void SetCurrentArea(IArea area)
        {
            CurrentArea = area;
        }

        /// <summary>
        /// Sets the current module.
        /// </summary>
        public void SetCurrentModule(IModule module)
        {
            CurrentModule = module;
        }
        public ITimeManager TimeManager { get; }
        public IEventBus EventBus { get; }
        public IDelayScheduler DelayScheduler { get; }
        public CombatSystem CombatSystem { get; }
        public EffectSystem EffectSystem { get; }
        public PerceptionSystem PerceptionSystem { get; }
        public TriggerSystem TriggerSystem { get; }
        public AIController AIController { get; }
        public ModuleTransitionSystem ModuleTransitionSystem { get; }

        public IEntity CreateEntity(IEntityTemplate template, Vector3 position, float facing)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            return template.Spawn(this, position, facing);
        }

        public IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing)
        {
            var entity = new Entity(objectType, this);
            entity.Position = position;
            entity.Facing = facing;
            RegisterEntity(entity);
            return entity;
        }

        /// <summary>
        /// Destroys an entity by its ObjectId.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: DestroyObject NWScript function
        /// Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (destroy object event)
        /// Original implementation: Unregisters entity from world, fires destroy events, marks as invalid
        /// Destroy sequence: Unregister from world indices, fire OnDestroy script event, mark entity as invalid
        /// </remarks>
        public void DestroyEntity(uint objectId)
        {
            IEntity entity = GetEntity(objectId);
            if (entity != null)
            {
                UnregisterEntity(entity);

                if (entity is Entity concreteEntity)
                {
                    concreteEntity.Destroy();
                }
            }
        }

        /// <summary>
        /// Gets an entity by its ObjectId.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GetObject function
        /// Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
        /// Original implementation: FUN_004dc030 @ 0x004dc030 (wrapper that calls FUN_004e9de0)
        /// ObjectId lookup: O(1) dictionary lookup by ObjectId (uint32)
        /// Returns null if ObjectId not found (OBJECT_INVALID = 0x7F000000)
        /// </remarks>
        public IEntity GetEntity(uint objectId)
        {
            if (_entitiesById.TryGetValue(objectId, out IEntity entity))
            {
                return entity;
            }
            return null;
        }

        /// <summary>
        /// Gets an entity by its Tag string.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GetObjectByTag NWScript function
        /// Located via string references: "GetObjectByTag" function uses case-insensitive tag comparison
        /// Original implementation: Tag lookup is case-insensitive, supports nth parameter for multiple entities with same tag
        /// Tag matching: Uses case-insensitive string comparison (StringComparer.OrdinalIgnoreCase)
        /// nth parameter: Returns nth entity with matching tag (0 = first, 1 = second, etc.)
        /// Returns null if tag not found or nth is out of range
        /// </remarks>
        public IEntity GetEntityByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            if (_entitiesByTag.TryGetValue(tag, out List<IEntity> entities))
            {
                if (nth >= 0 && nth < entities.Count)
                {
                    return entities[nth];
                }
            }
            return null;
        }

        public IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All)
        {
            float radiusSquared = radius * radius;
            var result = new List<IEntity>();

            foreach (IEntity entity in _allEntities)
            {
                if ((entity.ObjectType & typeMask) == 0)
                {
                    continue;
                }

                Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    float distSquared = Vector3.DistanceSquared(center, transform.Position);
                    if (distSquared <= radiusSquared)
                    {
                        result.Add(entity);
                    }
                }
            }

            return result;
        }

        public IEnumerable<IEntity> GetEntitiesOfType(ObjectType type)
        {
            if (_entitiesByType.TryGetValue(type, out List<IEntity> entities))
            {
                return entities;
            }
            return new List<IEntity>();
        }

        public IEnumerable<IEntity> GetAllEntities()
        {
            return _allEntities;
        }

        public void RegisterEntity(IEntity entity)
        {
            // Based on swkotor2.exe: Entity registration system
            // Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
            // Original implementation: Entities registered in world with ObjectId, Tag, and ObjectType indices
            // Entity lookup: GetEntity by ObjectId (O(1) lookup), GetEntityByTag searches by tag string (case-insensitive)
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            if (_entitiesById.ContainsKey(entity.ObjectId))
            {
                return; // Already registered
            }

            _entitiesById[entity.ObjectId] = entity;
            _allEntities.Add(entity);

            // Register by tag
            // Original engine: Entities indexed by tag for GetObjectByTag NWScript function
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                if (!_entitiesByTag.TryGetValue(entity.Tag, out List<IEntity> tagList))
                {
                    tagList = new List<IEntity>();
                    _entitiesByTag[entity.Tag] = tagList;
                }
                tagList.Add(entity);
            }

            // Register by type
            // Original engine: Entities indexed by ObjectType for efficient type-based queries
            if (!_entitiesByType.TryGetValue(entity.ObjectType, out List<IEntity> typeList))
            {
                typeList = new List<IEntity>();
                _entitiesByType[entity.ObjectType] = typeList;
            }
            typeList.Add(entity);

            // Fire OnSpawn script event
            // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN fires when entity is spawned/created
            // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN" @ 0x007bc7d0 (0x8), "ScriptSpawn" @ 0x007bee30
            // Original implementation: OnSpawn script fires on entity when it's first created/spawned into the world
            // OnSpawn fires after entity is fully initialized and registered in the world
            if (EventBus != null)
            {
                EventBus.FireScriptEvent(entity, ScriptEvent.OnSpawn, null);
            }
        }

        public void UnregisterEntity(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _entitiesById.Remove(entity.ObjectId);
            _allEntities.Remove(entity);

            // Remove from tag list
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                if (_entitiesByTag.TryGetValue(entity.Tag, out List<IEntity> tagList))
                {
                    tagList.Remove(entity);
                }
            }

            // Remove from type list
            if (_entitiesByType.TryGetValue(entity.ObjectType, out List<IEntity> typeList))
            {
                typeList.Remove(entity);
            }
        }

        /// <summary>
        /// Updates all entities for a single frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            TimeManager.Update(deltaTime);

            while (TimeManager.HasPendingTicks())
            {
                TimeManager.Tick();
                // Fixed update logic would go here
            }

            // Update delay scheduler
            DelayScheduler.Update(deltaTime);

            // Update perception system
            PerceptionSystem.Update(deltaTime);

            // Update trigger system
            TriggerSystem.Update(deltaTime);

            // Update AI controller
            AIController.Update(deltaTime);

            // Update combat system
            CombatSystem.Update(deltaTime);

            EventBus.DispatchQueuedEvents();
        }
    }
}

