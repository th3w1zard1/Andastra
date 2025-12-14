using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Actions;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Templates;

namespace Odyssey.Core.Entities
{
    /// <summary>
    /// Core world implementation - entity container and state manager.
    /// </summary>
    public class World : IWorld
    {
        private readonly Dictionary<uint, IEntity> _entitiesById;
        private readonly Dictionary<string, List<IEntity>> _entitiesByTag;
        private readonly Dictionary<ObjectType, List<IEntity>> _entitiesByType;
        private readonly List<IEntity> _allEntities;

        public World()
        {
            _entitiesById = new Dictionary<uint, IEntity>();
            _entitiesByTag = new Dictionary<string, List<IEntity>>(StringComparer.OrdinalIgnoreCase);
            _entitiesByType = new Dictionary<ObjectType, List<IEntity>>();
            _allEntities = new List<IEntity>();
            TimeManager = new TimeManager();
            EventBus = new EventBus();
            DelayScheduler = new DelayScheduler();
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
            RegisterEntity(entity);
            return entity;
        }

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

        public IEntity GetEntity(uint objectId)
        {
            if (_entitiesById.TryGetValue(objectId, out IEntity entity))
            {
                return entity;
            }
            return null;
        }

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
            if (!_entitiesByType.TryGetValue(entity.ObjectType, out List<IEntity> typeList))
            {
                typeList = new List<IEntity>();
                _entitiesByType[entity.ObjectType] = typeList;
            }
            typeList.Add(entity);
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

            EventBus.DispatchQueuedEvents();
        }
    }
}

