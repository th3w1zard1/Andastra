using System;
using System.Collections.Generic;
using System.Numerics;
using BioWareEngines.Core.Animation;
using BioWareEngines.Core.Combat;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Templates;

namespace BioWareEngines.Core.Interfaces
{
    /// <summary>
    /// Core entity container and world state.
    /// </summary>
    /// <remarks>
    /// World Interface:
    /// - Based on swkotor2.exe world management system
    /// - Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c, "Tag" (various locations)
    /// - Original engine maintains entity lists by ObjectId, Tag, and ObjectType
    /// - Entity lookup: GetEntityByTag searches by tag string (case-insensitive), nth parameter for multiple entities with same tag
    /// - ObjectId is unique 32-bit identifier assigned sequentially (see FUN_005226d0 @ 0x005226d0 for entity serialization)
    /// - World manages current area/module, time (ITimeManager), events (IEventBus), delay scheduler (IDelayScheduler), and effect system
    /// - Module management: "Module" @ 0x007bc4e0, "ModuleList" @ 0x007bdd3c, "ModuleName" @ 0x007bde2c, "LASTMODULE" @ 0x007be1d0
    /// - Module events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - Module save: "modulesave" @ 0x007bde20, "LinkedToModule" @ 0x007bd7bc (door/trigger module links)
    /// - Area management: "AREANAME" @ 0x007be1dc (area name field), "AreaName" @ 0x007be340, "AreaId" @ 0x007bef48
    /// - "AreaObject" @ 0x007c0b70, "AreaProperties" @ 0x007bd228, "AreaMap" @ 0x007bd118 (area map data)
    /// - "AreaMapResX" @ 0x007bd10c, "AreaMapResY" @ 0x007bd100, "AreaMapData" @ 0x007bd0e4, "AreaMapDataSize" @ 0x007bd0f0
    /// - Area events: "EVENT_AREA_TRANSITION" @ 0x007bcbdc, "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
    /// - "Mod_Area_list" @ 0x007be748 (module area list), "Mod_Entry_Area" @ 0x007be9b4 (module entry area)
    /// - "Target_Area" @ 0x007c02d4 (target area for transitions), "NW_MAP_PIN_AREA_%i" @ 0x007bd824 (map pin format)
    /// - CreateEntity: Creates new entity from template or ObjectType, assigns ObjectId automatically
    /// - DestroyEntity: Removes entity from world and cleans up all components
    /// - GetEntitiesInRadius: Spatial query with optional ObjectType filter mask
    /// </remarks>
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
        /// The perception system for sight/hearing checks.
        /// </summary>
        Perception.PerceptionSystem PerceptionSystem { get; }

        /// <summary>
        /// The combat system for combat resolution.
        /// </summary>
        Combat.CombatSystem CombatSystem { get; }

        /// <summary>
        /// The trigger system for trigger volume events.
        /// </summary>
        Triggers.TriggerSystem TriggerSystem { get; }

        /// <summary>
        /// The AI controller for NPC behavior.
        /// </summary>
        AI.AIController AIController { get; }

        /// <summary>
        /// The animation system for updating entity animations.
        /// </summary>
        Animation.AnimationSystem AnimationSystem { get; }

        /// <summary>
        /// Registers an entity with the world.
        /// </summary>
        void RegisterEntity(IEntity entity);

        /// <summary>
        /// Unregisters an entity from the world.
        /// </summary>
        void UnregisterEntity(IEntity entity);

        /// <summary>
        /// Updates the world (time manager, event bus).
        /// </summary>
        void Update(float deltaTime);
    }
}

