using System.Collections.Generic;
using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Runtime entity with components.
    /// </summary>
    /// <remarks>
    /// Entity Interface:
    /// - Based on swkotor2.exe entity system
    /// - Located via string references: "ObjectId" @ 0x007bce5c, "Tag" (various locations)
    /// - Object logging format: "OID: %08x, Tag: %s, %s" @ 0x007c76b8 used for debug/error logging
    /// - Object list handling: "ObjectIDList" @ 0x007bfd7c, "ObjectList" @ 0x007bfdbc, "ObjectValue" @ 0x007bfd70
    /// - Entity serialization: FUN_004e28c0 @ 0x004e28c0 saves Creature List with ObjectId fields (offset +4 in object structure)
    /// - Entity deserialization: FUN_005fb0f0 @ 0x005fb0f0 loads creature data from GFF, reads ObjectId at offset +4
    /// - Original engine: Entities have ObjectId (uint32), Tag (string), ObjectType (enum)
    /// - Component system: Entities use component-based architecture for stats, transform, inventory, etc.
    /// - Script hooks: Entities store script ResRefs for various events (OnHeartbeat, OnAttacked, etc.)
    /// - Original entity structure includes: Position (Vector3), Orientation (Vector3), AreaId, ObjectId at offset +4
    /// - Object events: "EVENT_DESTROY_OBJECT" @ 0x007bcd48, "EVENT_OPEN_OBJECT" @ 0x007bcda0, "EVENT_CLOSE_OBJECT" @ 0x007bcdb4
    /// - "EVENT_LOCK_OBJECT" @ 0x007bcd20, "EVENT_UNLOCK_OBJECT" @ 0x007bcd34
    /// - Object entry/exit events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9f8, "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc
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

