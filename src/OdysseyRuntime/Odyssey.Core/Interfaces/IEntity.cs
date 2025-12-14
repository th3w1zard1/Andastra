using System.Collections.Generic;
using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Runtime entity with components.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Unique object ID for this entity.
        /// </summary>
        uint ObjectId { get; }

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        string Tag { get; set; }

        /// <summary>
        /// The type of this object (Creature, Door, Placeable, etc.)
        /// </summary>
        ObjectType ObjectType { get; }

        /// <summary>
        /// Gets a component of the specified type.
        /// </summary>
        T GetComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Adds a component to this entity.
        /// </summary>
        void AddComponent<T>(T component) where T : class, IComponent;

        /// <summary>
        /// Removes a component from this entity.
        /// </summary>
        bool RemoveComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Checks if the entity has a component of the specified type.
        /// </summary>
        bool HasComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Gets all components attached to this entity.
        /// </summary>
        IEnumerable<IComponent> GetAllComponents();

        /// <summary>
        /// Whether this entity is valid and not destroyed.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// The world this entity belongs to.
        /// </summary>
        IWorld World { get; }
    }
}

