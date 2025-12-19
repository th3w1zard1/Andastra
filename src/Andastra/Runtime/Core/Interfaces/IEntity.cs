using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Runtime entity with components.
    /// </summary>
    /// <remarks>
    /// Entity Interface:
    /// Common entity system shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    ///
    /// Common structure across all engines:
    /// - ObjectId (uint32): Unique identifier assigned sequentially, used for script references and save game serialization
    /// - Tag (string): Script lookup identifier for GetObjectByTag functions, must be unique within an area
    /// - ObjectType (enum): Type of object (Creature, Door, Placeable, Trigger, Waypoint, Sound, etc.)
    /// - AreaId (uint32): Identifies which area the entity belongs to
    /// - Component system: Modular component-based architecture for stats, transform, inventory, etc.
    /// - Script hooks: Entities store script ResRefs for various events (OnHeartbeat, OnAttacked, OnDeath, etc.)
    /// - Event handling: DispatchEvent system routes events to entities
    /// - Entity serialization/deserialization: Save/load entity state for save games
    ///
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): OdysseyEntity - ObjectId at offset +4, GFF-based serialization
    /// - Aurora (nwmain.exe, nwn2main.exe): AuroraEntity - CExoString-based Tag, similar structure
    /// - Eclipse (daorigins.exe, DragonAge2.exe): EclipseEntity - Enhanced component system
    /// - Infinity (MassEffect.exe, MassEffect2.exe): InfinityEntity - Streamlined entity system
    ///
    /// All engine-specific details are in subclasses. This interface defines only common functionality.
    /// </remarks>
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
        /// Gets or sets the area ID this entity belongs to.
        /// </summary>
        /// <remarks>
        /// AreaId identifies which area the entity is located in.
        /// Set when entity is registered to an area in the world.
        /// Common across all engines (Odyssey, Aurora, Eclipse, Infinity).
        /// </remarks>
        uint AreaId { get; set; }

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
        IWorld World { get; set; }

        /// <summary>
        /// Sets arbitrary data on this entity.
        /// </summary>
        void SetData(string key, object value);

        /// <summary>
        /// Gets arbitrary data from this entity.
        /// </summary>
        T GetData<T>(string key, T defaultValue = default(T));

        /// <summary>
        /// Gets arbitrary data from this entity.
        /// </summary>
        object GetData(string key);

        /// <summary>
        /// Checks if this entity has data for the specified key.
        /// </summary>
        bool HasData(string key);
    }
}

