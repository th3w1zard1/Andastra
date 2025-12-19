using System.Collections.Generic;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of entity functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Entity Implementation:
    /// - Common entity system with ObjectId, Tag, ObjectType across all engines
    /// - Component-based architecture for stats, transform, inventory, etc.
    /// - Script hooks for events (OnHeartbeat, OnAttacked, etc.)
    /// - Entity serialization/deserialization for save games
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Entity management and serialization functions
    /// - swkotor2.exe: FUN_004e28c0 (save), FUN_005fb0f0 (load), ObjectId at offset +4
    /// - nwmain.exe: Aurora entity system with similar structure
    /// - daorigins.exe: Eclipse entity system with enhanced components
    /// - DragonAge2.exe/MassEffect.exe/MassEffect2.exe: Advanced entity systems
    ///
    /// Common structure across engines:
    /// - ObjectId (uint32): Unique identifier assigned sequentially
    /// - Tag (string): Script lookup identifier
    /// - ObjectType (enum): Creature, Door, Placeable, Trigger, etc.
    /// - Component system: Modular stats, transform, inventory, etc.
    /// - Script hooks: OnHeartbeat, OnAttacked, OnDeath, etc.
    /// - Event handling: DispatchEvent system routes events to entities
    /// </remarks>
    [PublicAPI]
    public abstract class BaseEntity : IEntity
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        private readonly Dictionary<System.Type, IComponent> _components = new Dictionary<System.Type, IComponent>();

        /// <summary>
        /// Unique object ID for this entity.
        /// </summary>
        /// <remarks>
        /// Based on ObjectId field at offset +4 in entity structure.
        /// Unique 32-bit identifier assigned sequentially across all entities.
        /// Used for script references and save game serialization.
        /// </remarks>
        public abstract uint ObjectId { get; }

        /// <summary>
        /// Tag string for script lookups.
        /// </summary>
        /// <remarks>
        /// Script-accessible identifier for GetObjectByTag functions.
        /// Must be unique within an area for reliable lookups.
        /// </remarks>
        public abstract string Tag { get; set; }

        /// <summary>
        /// The type of this object (Creature, Door, Placeable, etc.)
        /// </summary>
        /// <remarks>
        /// ObjectType enum shared across all engines.
        /// Determines available components and behaviors.
        /// </remarks>
        public abstract ObjectType ObjectType { get; }

        /// <summary>
        /// Gets or sets the area ID this entity belongs to.
        /// </summary>
        /// <remarks>
        /// Based on entity structure across all engines.
        /// AreaId identifies which area the entity is located in.
        /// Set when entity is registered to an area in the world.
        /// </remarks>
        public abstract uint AreaId { get; set; }

        /// <summary>
        /// Whether this entity is valid and not destroyed.
        /// </summary>
        /// <remarks>
        /// Entity validity checking prevents use-after-free issues.
        /// Entities become invalid when destroyed or unloaded.
        /// </remarks>
        public abstract bool IsValid { get; }

        /// <summary>
        /// The world this entity belongs to.
        /// </summary>
        /// <remarks>
        /// Reference to containing world for cross-entity operations.
        /// Used for area transitions and global entity lookups.
        /// </remarks>
        public abstract IWorld World { get; set; }

        /// <summary>
        /// Gets a component of the specified type.
        /// </summary>
        /// <remarks>
        /// Component-based architecture allows modular entity behavior.
        /// Returns null if component type is not attached.
        /// </remarks>
        public T GetComponent<T>() where T : class, IComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
        }

        /// <summary>
        /// Adds a component to this entity.
        /// </summary>
        /// <remarks>
        /// Attaches component to entity, enabling associated behavior.
        /// Only one component of each type allowed per entity.
        /// </remarks>
        public void AddComponent<T>(T component) where T : class, IComponent
        {
            _components[typeof(T)] = component;
        }

        /// <summary>
        /// Removes a component from this entity.
        /// </summary>
        /// <remarks>
        /// Detaches component, disabling associated behavior.
        /// Returns false if component type was not attached.
        /// </remarks>
        public bool RemoveComponent<T>() where T : class, IComponent
        {
            return _components.Remove(typeof(T));
        }

        /// <summary>
        /// Checks if the entity has a component of the specified type.
        /// </summary>
        public bool HasComponent<T>() where T : class, IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Gets all components attached to this entity.
        /// </summary>
        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }

        /// <summary>
        /// Sets arbitrary data on this entity.
        /// </summary>
        /// <remarks>
        /// Generic data storage for script variables and temporary state.
        /// Persisted across save/load operations.
        /// </remarks>
        public void SetData(string key, object value)
        {
            _data[key] = value;
        }

        /// <summary>
        /// Gets arbitrary data from this entity.
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default(T))
        {
            return _data.TryGetValue(key, out var value) ? (T)value : defaultValue;
        }

        /// <summary>
        /// Gets arbitrary data from this entity.
        /// </summary>
        public object GetData(string key)
        {
            return _data.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Checks if this entity has data for the specified key.
        /// </summary>
        public bool HasData(string key)
        {
            return _data.ContainsKey(key);
        }

        /// <summary>
        /// Initializes the entity after creation.
        /// </summary>
        /// <remarks>
        /// Called after entity construction and component attachment.
        /// Sets up initial state and registers with systems.
        /// </remarks>
        protected abstract void Initialize();

        /// <summary>
        /// Updates the entity each frame.
        /// </summary>
        /// <remarks>
        /// Updates components, handles events, processes scripts.
        /// Called from main game loop for active entities.
        /// </remarks>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Destroys the entity and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Removes from world, cleans up components, frees resources.
        /// Entity becomes invalid after destruction.
        /// </remarks>
        public abstract void Destroy();

        /// <summary>
        /// Serializes entity data for save games.
        /// </summary>
        /// <remarks>
        /// Based on entity serialization functions in all engines.
        /// Saves ObjectId, Tag, components, and custom data.
        /// </remarks>
        public abstract byte[] Serialize();

        /// <summary>
        /// Deserializes entity data from save games.
        /// </summary>
        /// <remarks>
        /// Based on entity deserialization functions in all engines.
        /// Restores ObjectId, Tag, components, and custom data.
        /// </remarks>
        public abstract void Deserialize(byte[] data);
    }
}
