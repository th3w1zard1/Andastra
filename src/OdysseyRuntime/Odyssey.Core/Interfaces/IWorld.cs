using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Combat;
using Odyssey.Core.Enums;
using Odyssey.Core.Templates;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Core entity container and world state.
    /// </summary>
    public interface IWorld
    {
        /// <summary>
        /// Creates a new entity from a template.
        /// </summary>
        IEntity CreateEntity(IEntityTemplate template, Vector3 position, float facing);

        /// <summary>
        /// Creates a new entity of the specified type.
        /// </summary>
        IEntity CreateEntity(ObjectType objectType, Vector3 position, float facing);

        /// <summary>
        /// Destroys an entity by object ID.
        /// </summary>
        void DestroyEntity(uint objectId);

        /// <summary>
        /// Gets an entity by object ID.
        /// </summary>
        IEntity GetEntity(uint objectId);

        /// <summary>
        /// Gets an entity by tag. If nth > 0, gets the nth entity with that tag.
        /// </summary>
        IEntity GetEntityByTag(string tag, int nth = 0);

        /// <summary>
        /// Gets all entities within a radius of a point, optionally filtered by type mask.
        /// </summary>
        IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All);

        /// <summary>
        /// Gets all entities of a specific type.
        /// </summary>
        IEnumerable<IEntity> GetEntitiesOfType(ObjectType type);

        /// <summary>
        /// Gets all entities in the world.
        /// </summary>
        IEnumerable<IEntity> GetAllEntities();

        /// <summary>
        /// The current area.
        /// </summary>
        IArea CurrentArea { get; }

        /// <summary>
        /// The current module.
        /// </summary>
        IModule CurrentModule { get; }

        /// <summary>
        /// The simulation time manager.
        /// </summary>
        ITimeManager TimeManager { get; }

        /// <summary>
        /// The event bus for world and entity events.
        /// </summary>
        IEventBus EventBus { get; }

        /// <summary>
        /// The delay scheduler for delayed actions (DelayCommand).
        /// </summary>
        IDelayScheduler DelayScheduler { get; }

        /// <summary>
        /// The effect system for managing entity effects.
        /// </summary>
        Combat.EffectSystem EffectSystem { get; }

        /// <summary>
        /// Registers an entity with the world.
        /// </summary>
        void RegisterEntity(IEntity entity);

        /// <summary>
        /// Unregisters an entity from the world.
        /// </summary>
        void UnregisterEntity(IEntity entity);
    }
}

